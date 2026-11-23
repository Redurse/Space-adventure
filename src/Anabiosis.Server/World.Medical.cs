using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Medicine, in the manner of the Neurotrauma mod for Barotrauma (without its blood types): a person is six body parts with injuries of
// their own (BodyHealth), hurt through DamageCharacter - a bullet picks a part and tears it, a blast batters everything, thin air
// starves the brain - and the injuries have consequences: broken legs slow you, a broken pair of arms drops the weapon, pain and
// concussion and blood loss take you down. A person who is down can do nothing; one whose heart has stopped has a few minutes to be
// brought back before they are dead.
public enum DamageKind
{
    Bullet,      // a round: one part, trauma and bleeding, sometimes a break
    Blast,       // everything at once: trauma, burns, a concussion, breaks
    Burn,
    Suffocation, // thin air: oxygen starvation
    Vacuum,      // no air at all: running out of vitality is death
    Generic,     // damage with no part to put it on
}

public sealed partial class World
{
    // Where a stray bullet lands, by share.
    private static readonly (BodyPart Part, float Weight)[] HitChance =
    {
        (BodyPart.Torso, 0.40f), (BodyPart.Head, 0.10f), (BodyPart.LeftArm, 0.125f), (BodyPart.RightArm, 0.125f),
        (BodyPart.LeftLeg, 0.125f), (BodyPart.RightLeg, 0.125f),
    };

    private BodyPart RollHitPart()
    {
        var roll = (float)_random.NextDouble();
        foreach (var (part, weight) in HitChance)
        {
            roll -= weight;
            if (roll <= 0f)
                return part;
        }
        return BodyPart.Torso;
    }

    private static bool IsLimb(BodyPart part) => part is not (BodyPart.Head or BodyPart.Torso);

    // The one way a character is hurt. For Suffocation/Vacuum, `amount` is the oxygen starvation added this tick.
    internal void DamageCharacter(Character character, DamageKind kind, float amount, BodyPart? part = null)
    {
        if (character.IsDead)
            return;
        DamageBody(character.Body, kind, amount, part, ArmorOf(character));
    }

    // A round from a gun whose wound is known exactly (GunDefinitions - the Barotrauma numbers, one to one): the part it hits takes
    // that much trauma and that much bleeding, no more and no less, and the stun of the round puts the target down for that long.
    internal void ShootBody(BodyHealth body, float wound, float bleeding, float stunSeconds, BodyPart? part = null,
        float penetration = 0f, ArmorWorn armor = default, float burn = 0f)
    {
        if (body.Dead || wound <= 0f)
            return;
        var hit = part ?? RollHitPart();
        if (burn > 0f)
            body.AddBurn(hit, burn);
        // Armour on the part that is hit keeps some of the wound and the bleeding out (ArmorDefinitions.cs).
        var (woundKept, bleedingKept, concussionKept) = ArmorDefinitions.Against(armor, hit, penetration);
        WoundBody(body, hit, wound * woundKept, bleeding * bleedingKept, wound * woundKept, concussionKept);
        if (stunSeconds > 0f)
            body.StunSeconds = Math.Max(body.StunSeconds, stunSeconds);
    }

    // What a round does to the part it lands in. `force` is what the knock-on injuries scale with: a concussion from a head hit,
    // a chance of reaching the brain, the heart or the lungs, a chance of breaking a limb.
    private void WoundBody(BodyHealth body, BodyPart hit, float trauma, float bleeding, float force, float concussionKept = 1f)
    {
        body.AddTrauma(hit, trauma);
        body.AddBleeding(hit, bleeding);
        if (hit == BodyPart.Head)
            body.AddConcussion(force * 1.2f * concussionKept);
        // A round into the head can reach the brain, one into the chest the heart or the lungs.
        if (hit == BodyPart.Head && _random.NextDouble() < 0.6)
            body.AddOrganDamage(Organ.Brain, force * 0.9f);
        else if (hit == BodyPart.Torso && force >= 18f && _random.NextDouble() < 0.35)
            body.AddOrganDamage(_random.NextDouble() < 0.6 ? Organ.Heart : Organ.Lungs, force * 0.8f);
        if (IsLimb(hit) && force >= 25f && _random.NextDouble() < 0.25)
            body.Fracture(hit);
    }

    // The same for anybody with a body - a hostile crew member included.
    internal void DamageBody(BodyHealth body, DamageKind kind, float amount, BodyPart? part = null, ArmorWorn armor = default)
    {
        if (body.Dead || amount <= 0f)
            return;
        switch (kind)
        {
            case DamageKind.Bullet:
            {
                var hit = part ?? RollHitPart();
                var (woundKept, bleedingKept, concussionKept) = ArmorDefinitions.Against(armor, hit, 0f);
                WoundBody(body, hit, amount * 1.4f * woundKept, amount * 0.6f * bleedingKept, amount * woundKept, concussionKept);
                break;
            }
            case DamageKind.Blast:
            {
                foreach (var each in body.AllParts)
                {
                    body.AddTrauma(each, amount * 0.25f * (each == BodyPart.Torso && armor.Vest ? ArmorDefinitions.VestBlast : 1f));
                    body.AddBurn(each, amount * 0.12f);
                }
                body.AddBleeding(RollHitPart(), amount * 0.3f);
                body.AddBleeding(RollHitPart(), amount * 0.2f);
                body.AddConcussion(amount * 0.8f);
                body.AddOrganDamage(Organ.Lungs, amount * 0.5f);
                if (amount > 30f)
                    body.AddOrganDamage(Organ.Brain, amount * 0.3f);
                body.AddOrganDamage(Organ.Heart, amount * 0.1f);
                if (amount > 30f)
                    foreach (var limb in new[] { BodyPart.LeftArm, BodyPart.RightArm, BodyPart.LeftLeg, BodyPart.RightLeg })
                        if (_random.NextDouble() < 0.3)
                            body.Fracture(limb);
                break;
            }
            case DamageKind.Burn:
                body.AddBurn(part ?? RollHitPart(), amount);
                break;
            case DamageKind.Suffocation:
                body.AddHypoxia(amount, vacuum: false);
                return; // AddHypoxia already settled the new state
            case DamageKind.Vacuum:
                body.AddHypoxia(amount, vacuum: true);
                return;
            default:
                body.AddOtherLoss(amount);
                break;
        }
        body.Normalize();
    }

    private void StepMedical(double deltaSeconds)
    {
        StepTreatments(deltaSeconds);
        foreach (var character in _characters.Values)
        {
            if (character.Body.NeedsTreatment && IsInMedbay(character))
                character.Body.Heal(MedbayRecoveryPerSecond * (float)deltaSeconds);
            character.Body.Step((float)deltaSeconds, (float)_random.NextDouble());
        }
    }

    // The first-aid kit pressed with F (also usable from the health window): the same effect as MedicalTreatment gives it.
    private void TryUseMedKit(Character character)
    {
        if (!character.Inventory.IsHolding(ItemType.MedKit) || !MedicalTreatment.Apply(character.Body, ItemType.MedKit, BodyPart.Torso, 1f))
            return;
        character.Inventory.TryTakeHeldItem(ItemType.MedKit);
    }

    // Test hook: hurts a character the way the game does.
    public void DebugDamageCharacter(int playerId, DamageKind kind, float amount, BodyPart? part = null)
    {
        if (_characters.TryGetValue(playerId, out var character))
            DamageCharacter(character, kind, amount, part);
    }

    // Test hook: one round of this gun into this part of a character.
    public void DebugShootCharacter(int playerId, ItemType gun, BodyPart part)
    {
        if (_characters.TryGetValue(playerId, out var character) && GunDefinitions.Of(gun) is { } spec)
            ShootBody(character.Body, spec.HitWound, spec.HitBleeding, spec.StunChance >= 1f ? spec.StunSeconds : 0f, part, spec.Penetration, ArmorOf(character));
    }

    public BodyHealth DebugBody(int playerId) => _characters[playerId].Body;

    internal static BodyState CreateBodyState(Character character)
    {
        var body = character.Body;
        var parts = body.AllParts
            .Select(p => new BodyPartState(p, body[p].Trauma, body[p].Burn, body[p].Bleeding, body[p].Fractured, body[p].Infection, body[p].Bandaged,
                body[p].Tourniquet, body[p].Splinted, body[p].Cast, body[p].CastSeconds, body[p].DisinfectedSeconds))
            .ToArray();
        return new BodyState(parts, body.BloodLoss, body.Hypoxia, body.Pain, body.Concussion, body.Unconscious, body.ArrestSecondsLeft,
            body.AnalgesiaSeconds, body.IsDowned, body.Sepsis, body.HeartDamage, body.LungDamage, body.BrainDamage, body.Dose, body.AdrenalineSeconds,
            body.AntibioticSeconds, body.OtherLoss, body.Dead);
    }
}
