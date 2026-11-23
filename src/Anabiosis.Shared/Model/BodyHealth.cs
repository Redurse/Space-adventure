namespace Anabiosis.Shared.Model;

public enum BodyPart { Head, Torso, LeftArm, RightArm, LeftLeg, RightLeg }

// The organs that can be hurt inside the body (and mended only by surgery).
public enum Organ { Heart, Lungs, Brain }

// What is wrong with one part of the body, and what has been done about it: blunt/gunshot trauma, burns, bleeding (how fast it is losing
// blood right now), a break, an infection of the wound - and the dressings, tourniquet, splint, cast and antiseptic that treat them.
public sealed class PartInjury
{
    public float Trauma;   // 0..100
    public float Burn;     // 0..100
    public float Bleeding; // 0..100, intensity
    public bool Fractured;
    public float Infection; // 0..100
    public bool Bandaged;
    public float BandageTimer;      // how long the wound has been dry under the bandage
    public bool Tourniquet;
    public float TourniquetSeconds;
    public bool Splinted;
    public bool Cast;
    public float CastSeconds;
    public float DisinfectedSeconds; // antiseptic still protecting the wound
}

// A person's medical state, in the manner of the Neurotrauma mod for Barotrauma (without its blood-type system): the body is six
// parts that each carry their own injuries, three organs inside, plus whole-body conditions (blood lost, oxygen starvation,
// concussion, pain, infection). Health is not stored but READ OFF these, the way a compartment's health is read off its walls:
// Vitality = 100 minus everything that is wrong. When it runs out the person does not die at once - the heart stops, and for a few
// minutes they can still be brought back by chest compressions, a defibrillator, adrenaline (CardiacArrest, then Dead when the window
// closes). Below a threshold of vitality, or with too much pain or concussion, they are unconscious and can do nothing.
//
// Plain old "set Health" callers (a destroyed compartment kills, a respawn restores) still work through SetHealth.
public sealed class BodyHealth
{
    public const float MaxVitality = 100f;
    public const float ArrestWindowSeconds = 30f;

    // How much of a part's trauma counts against vitality - the head and torso are life, an arm is not.
    private static readonly float[] TraumaWeight = { 1.0f, 1.0f, 0.25f, 0.25f, 0.3f, 0.3f };
    private const float BurnWeightFactor = 0.8f;
    private const float BleedToBloodLoss = 0.03f;  // blood loss per second per point of bleeding intensity
    private const float ClottingPerSecond = 0.4f;  // a wound's bleeding eases by this much per second on its own
    private const float TraumaHealPerSecond = 0.03f;
    private const float BurnHealPerSecond = 0.02f;
    private const float BloodRecoverPerSecond = 0.15f;
    private const float HypoxiaRecoverPerSecond = 3f;
    private const float ConcussionRecoverPerSecond = 0.3f;
    private const float CastHealSeconds = 180f;
    private const float OverdoseThreshold = 2.5f;

    private readonly PartInjury[] _parts = { new(), new(), new(), new(), new(), new() };
    private bool _suffocatedThisTick;
    private bool _vacuumThisTick;
    private bool _cprThisTick;
    private float _sinceCpr = 999f;

    public PartInjury this[BodyPart part] => _parts[(int)part];
    public IEnumerable<BodyPart> AllParts => Enum.GetValues<BodyPart>();

    // Whole-body conditions.
    public float BloodLoss { get; private set; }   // 0..100, lost blood (just a number - no blood types)
    public float Hypoxia { get; private set; }     // 0..100, oxygen starvation
    public float OtherLoss { get; private set; }   // damage with no body part to put it on
    public float Concussion { get; private set; }  // 0..100
    public float Sepsis { get; private set; }      // 0..100, an infection that has reached the blood
    public float HeartDamage { get; private set; } // 0..100
    public float LungDamage { get; private set; }  // 0..100
    public float BrainDamage { get; private set; } // 0..100
    public float AnalgesiaSeconds { get; private set; } // painkiller still working
    public float Dose { get; private set; }        // painkiller still in the body - too much is an overdose
    public float AdrenalineSeconds { get; private set; }
    public float AntibioticSeconds { get; private set; }
    public float StunSeconds { get; set; }
    public bool Unconscious { get; private set; }
    public float ArrestSecondsLeft { get; private set; } // > 0: the heart has stopped, this long to restart it
    public bool Dead { get; private set; }

    public bool InArrest => ArrestSecondsLeft > 0f;
    // Alive but out of action: unconscious or in cardiac arrest.
    public bool IsDowned => !Dead && (Unconscious || InArrest || StunSeconds > 0f);
    // Stunned: knocked flat for a few seconds (a bullet, a blast), then back up - not the same as passing out.
    public bool Stunned => !Dead && StunSeconds > 0f;
    public bool Overdosing => Dose > OverdoseThreshold;

    public float OrganDamage(Organ organ) => organ switch { Organ.Heart => HeartDamage, Organ.Lungs => LungDamage, _ => BrainDamage };

    private static float EffectiveBleeding(PartInjury part) =>
        part.Tourniquet ? 0f : part.Bleeding * (part.Bandaged ? 0.25f : 1f);

    public float TotalBleeding => _parts.Sum(EffectiveBleeding);
    public bool IsBleeding => !Dead && TotalBleeding > 0.5f;
    // Anything at all that a doctor could still do something about.
    public bool NeedsTreatment => !Dead && (Vitality < MaxVitality - 0.5f || Concussion > 1f || Sepsis > 1f || OrganTotal > 1f
        || _parts.Any(p => p.Trauma > 0.5f || p.Burn > 0.5f || p.Bleeding > 0.5f || p.Fractured || p.Infection > 1f));

    private float OrganTotal => HeartDamage + LungDamage + BrainDamage;
    public float OrganLoss => HeartDamage * 0.25f + LungDamage * 0.15f + BrainDamage * 0.3f;

    public float TraumaLoss
    {
        get
        {
            var loss = 0f;
            for (var i = 0; i < _parts.Length; i++)
                loss += _parts[i].Trauma * TraumaWeight[i] + _parts[i].Burn * TraumaWeight[i] * BurnWeightFactor;
            return loss;
        }
    }

    public float Vitality => MaxVitality - TraumaLoss - BloodLoss - Hypoxia - OtherLoss - OrganLoss - Sepsis * 0.5f;

    // What the rest of the game calls Health: 0 only when dead; at least 1 while the heart is stopped but can still be restarted.
    public float Health => Dead ? 0f : InArrest ? Math.Clamp(Vitality, 1f, MaxVitality) : Math.Clamp(Vitality, 0f, MaxVitality);

    public float Pain
    {
        get
        {
            var pain = 0f;
            foreach (var part in _parts)
            {
                var fracture = part.Fractured ? (part.Cast ? 8f : part.Splinted ? 15f : 30f) : 0f;
                pain += part.Trauma * 0.35f + part.Burn * 0.5f + fracture + part.Infection * 0.12f;
            }
            pain = Math.Clamp(pain + Sepsis * 0.2f, 0f, 100f);
            return AnalgesiaSeconds > 0f ? pain * 0.3f : pain;
        }
    }

    private static float LegPenalty(PartInjury leg) => leg.Fractured ? 0.3f * (leg.Cast ? 0.4f : leg.Splinted ? 0.7f : 1f) : 0f;

    // How fast they can walk: broken or badly hurt legs and heavy pain slow them.
    public float MobilityFactor
    {
        get
        {
            var factor = 1f;
            foreach (var leg in new[] { BodyPart.LeftLeg, BodyPart.RightLeg })
            {
                factor -= LegPenalty(this[leg]);
                if (this[leg].Trauma > 60f) factor -= 0.15f;
            }
            if (Pain > 50f) factor *= 0.85f;
            return Math.Clamp(factor, 0.25f, 1f);
        }
    }

    public bool CanSprint => !this[BodyPart.LeftLeg].Fractured && !this[BodyPart.RightLeg].Fractured && Pain < 70f;

    // An arm in a cast is still good for little, but a plain broken one is not.
    private bool ArmDisabled(BodyPart arm) => this[arm].Fractured && !this[arm].Cast;

    // Both arms out of action: no holding a weapon or a tool.
    public bool HandsUsable => !(ArmDisabled(BodyPart.LeftArm) && ArmDisabled(BodyPart.RightArm));

    // ---- taking injuries ----

    public void AddTrauma(BodyPart part, float amount) => this[part].Trauma = Math.Clamp(this[part].Trauma + amount, 0f, 100f);
    public void AddBurn(BodyPart part, float amount) => this[part].Burn = Math.Clamp(this[part].Burn + amount, 0f, 100f);
    public void AddBleeding(BodyPart part, float amount) => this[part].Bleeding = Math.Clamp(this[part].Bleeding + amount, 0f, 100f);
    public void Fracture(BodyPart part) => this[part].Fractured = true;
    public void AddConcussion(float amount) => Concussion = Math.Clamp(Concussion + amount, 0f, 100f);
    public void AddOtherLoss(float amount) => OtherLoss = Math.Clamp(OtherLoss + amount, 0f, 200f);
    public void AddInfection(BodyPart part, float amount) => this[part].Infection = Math.Clamp(this[part].Infection + amount, 0f, 100f);

    public void AddOrganDamage(Organ organ, float amount)
    {
        amount = Math.Max(0f, amount);
        switch (organ)
        {
            case Organ.Heart: HeartDamage = Math.Min(100f, HeartDamage + amount); break;
            case Organ.Lungs: LungDamage = Math.Min(100f, LungDamage + amount); break;
            default: BrainDamage = Math.Min(100f, BrainDamage + amount); break;
        }
        Normalize();
    }

    // Oxygen starvation; in hard vacuum (nothing to resuscitate in) running out of vitality is death at once.
    public void AddHypoxia(float amount, bool vacuum)
    {
        Hypoxia = Math.Clamp(Hypoxia + amount, 0f, 100f);
        _suffocatedThisTick = true;
        _vacuumThisTick |= vacuum;
        Normalize();
    }

    // ---- treatment ----
    // Each returns whether it did any good, so that what is used up is never wasted on someone who did not need it.

    public bool GivePainkiller(float seconds)
    {
        AnalgesiaSeconds = Math.Max(AnalgesiaSeconds, seconds);
        Dose += 1f;
        Normalize();
        return true;
    }

    public bool GiveAntibiotic()
    {
        if (Sepsis <= 0.5f && _parts.All(p => p.Infection <= 0.5f))
            return false;
        AntibioticSeconds = 240f;
        return true;
    }

    public bool GiveAdrenaline()
    {
        AdrenalineSeconds = 90f;
        StunSeconds = 0f;
        Concussion = Math.Max(0f, Concussion - 30f);
        Normalize();
        return true;
    }

    public bool ApplyBandage(BodyPart part)
    {
        var injury = this[part];
        if (injury.Bandaged || (injury.Bleeding <= 0.5f && injury.Trauma <= 0.5f && injury.Burn <= 0.5f))
            return false;
        injury.Bandaged = true;
        injury.BandageTimer = 0f;
        return true;
    }

    // Puts a tourniquet on a limb, or takes the one that is there off. Only arms and legs.
    public bool ApplyTourniquet(BodyPart part)
    {
        if (part is BodyPart.Head or BodyPart.Torso)
            return false;
        var injury = this[part];
        if (injury.Tourniquet)
        {
            injury.Tourniquet = false;
            injury.TourniquetSeconds = 0f;
            return true;
        }
        if (injury.Bleeding <= 0.5f)
            return false;
        injury.Tourniquet = true;
        injury.TourniquetSeconds = 0f;
        return true;
    }

    public bool ApplyAntiseptic(BodyPart part)
    {
        var injury = this[part];
        if (injury.DisinfectedSeconds > 60f && injury.Infection <= 0.5f)
            return false;
        injury.DisinfectedSeconds = 300f;
        injury.Infection = Math.Max(0f, injury.Infection - 25f);
        return true;
    }

    public bool ApplyBurnGel(BodyPart part)
    {
        var injury = this[part];
        if (injury.Burn <= 0.5f)
            return false;
        injury.Burn = Math.Max(0f, injury.Burn - 30f);
        injury.DisinfectedSeconds = Math.Max(injury.DisinfectedSeconds, 120f);
        return true;
    }

    public bool ApplySplint(BodyPart part)
    {
        var injury = this[part];
        if (!injury.Fractured || injury.Splinted || injury.Cast)
            return false;
        injury.Splinted = true;
        return true;
    }

    public bool ApplyCast(BodyPart part)
    {
        var injury = this[part];
        if (!injury.Fractured || injury.Cast)
            return false;
        injury.Cast = true;
        injury.CastSeconds = 0f;
        return true;
    }

    // Giving up while the heart is stopped: the player chooses to die now rather than wait out the window (and becomes a spectator).
    public void GiveUp()
    {
        if (InArrest)
            Dead = true;
    }

    // Chest compressions, this tick: the heart's clock stops running down and the brain is kept fed a little.
    public void Compress()
    {
        if (!InArrest)
            return;
        _cprThisTick = true;
        _sinceCpr = 0f;
    }

    // An electric shock to a stopped heart; `roll` is a random 0..1. Works better after chest compressions and with adrenaline, worse the
    // more the heart is damaged.
    public bool Defibrillate(float roll)
    {
        if (!InArrest || Dead)
            return false;
        var chance = 0.3f + (_sinceCpr < 6f ? 0.25f : 0f) + (AdrenalineSeconds > 0f ? 0.15f : 0f) - HeartDamage * 0.004f;
        if (roll >= Math.Clamp(chance, 0.05f, 0.9f))
            return true; // it was used, and did not take
        Resuscitate(15f);
        return true;
    }

    // Surgery on an organ: mends it, at the price of a cut.
    public bool Operate(Organ organ, float amount)
    {
        if (OrganDamage(organ) <= 0.5f)
            return false;
        switch (organ)
        {
            case Organ.Heart: HeartDamage = Math.Max(0f, HeartDamage - amount); break;
            case Organ.Lungs: LungDamage = Math.Max(0f, LungDamage - amount); break;
            default: BrainDamage = Math.Max(0f, BrainDamage - amount); break;
        }
        var cut = organ == Organ.Brain ? BodyPart.Head : BodyPart.Torso;
        AddTrauma(cut, 6f);
        AddBleeding(cut, 6f);
        return true;
    }

    public void StopBleeding(BodyPart part) => this[part].Bleeding = 0f;
    public void StopAllBleeding() { foreach (var part in _parts) part.Bleeding = 0f; }
    public void SetFracture(BodyPart part, bool fractured) => this[part].Fractured = fractured;

    public void HealTrauma(BodyPart part, float amount) => this[part].Trauma = Math.Max(0f, this[part].Trauma - amount);
    public void HealBurn(BodyPart part, float amount) => this[part].Burn = Math.Max(0f, this[part].Burn - amount);
    public void RecoverBlood(float amount) => BloodLoss = Math.Max(0f, BloodLoss - amount);
    public void RecoverHypoxia(float amount) => Hypoxia = Math.Max(0f, Hypoxia - amount);
    public void RelieveConcussion(float amount) => Concussion = Math.Max(0f, Concussion - amount);

    // Gives vitality back, taking it off whatever is worst: oxygen starvation, lost blood, loose damage, then the worst-hurt parts.
    public void Heal(float amount)
    {
        if (Dead || amount <= 0f)
            return;
        var rest = amount;
        float Take(ref float pool) { var taken = Math.Min(pool, rest); pool -= taken; rest -= taken; return taken; }
        var hypoxia = Hypoxia; Take(ref hypoxia); Hypoxia = hypoxia;
        var blood = BloodLoss; Take(ref blood); BloodLoss = blood;
        var other = OtherLoss; Take(ref other); OtherLoss = other;
        while (rest > 0.001f)
        {
            // The part whose trauma + burn cost the most vitality.
            var worst = AllParts.OrderByDescending(p => (this[p].Trauma + this[p].Burn) * TraumaWeight[(int)p]).First();
            var part = this[worst];
            if (part.Trauma + part.Burn <= 0f)
                break;
            var weight = TraumaWeight[(int)worst];
            var healTrauma = Math.Min(part.Trauma, rest / weight);
            part.Trauma -= healTrauma;
            rest -= healTrauma * weight;
            if (rest > 0.001f)
            {
                var healBurn = Math.Min(part.Burn, rest / (weight * BurnWeightFactor));
                part.Burn -= healBurn;
                rest -= healBurn * weight * BurnWeightFactor;
            }
        }
        Normalize();
    }

    // The heart is going again, with this much vitality left.
    public void Resuscitate(float vitality)
    {
        if (Dead)
            return;
        ArrestSecondsLeft = 0f;
        Hypoxia = 0f;
        HeartDamage = Math.Min(HeartDamage, 85f);
        var excess = vitality - Vitality;
        if (excess > 0f)
            Heal(excess);
        Normalize();
    }

    public void Reset()
    {
        foreach (var part in _parts)
        {
            part.Trauma = part.Burn = part.Bleeding = part.Infection = part.BandageTimer = part.TourniquetSeconds = part.CastSeconds = part.DisinfectedSeconds = 0f;
            part.Fractured = part.Bandaged = part.Tourniquet = part.Splinted = part.Cast = false;
        }
        BloodLoss = Hypoxia = OtherLoss = Concussion = Sepsis = HeartDamage = LungDamage = BrainDamage = 0f;
        AnalgesiaSeconds = Dose = AdrenalineSeconds = AntibioticSeconds = StunSeconds = ArrestSecondsLeft = 0f;
        Unconscious = Dead = false;
        _sinceCpr = 999f;
    }

    // Rebuilds this body from what the server sent (so the client can ask it the same questions: what helps, how bad is it).
    public void LoadState(Anabiosis.Shared.Protocol.BodyState state)
    {
        Reset();
        foreach (var part in state.Parts)
        {
            var injury = this[part.Part];
            injury.Trauma = part.Trauma; injury.Burn = part.Burn; injury.Bleeding = part.Bleeding; injury.Fractured = part.Fractured;
            injury.Infection = part.Infection; injury.Bandaged = part.Bandaged; injury.Tourniquet = part.Tourniquet;
            injury.Splinted = part.Splinted; injury.Cast = part.Cast; injury.CastSeconds = part.CastSeconds;
            injury.DisinfectedSeconds = part.DisinfectedSeconds;
        }
        BloodLoss = state.BloodLoss; Hypoxia = state.Hypoxia; Concussion = state.Concussion; Sepsis = state.Sepsis;
        HeartDamage = state.HeartDamage; LungDamage = state.LungDamage; BrainDamage = state.BrainDamage; AnalgesiaSeconds = state.AnalgesiaSeconds;
        Dose = state.Dose; AdrenalineSeconds = state.AdrenalineSeconds; AntibioticSeconds = state.AntibioticSeconds; OtherLoss = state.OtherLoss;
        Unconscious = state.Unconscious; ArrestSecondsLeft = state.ArrestSecondsLeft; Dead = state.Dead;
    }

    // The old "Health" interface, for callers that just want to hurt, heal or kill: setting it to 0 kills at once, to full restores
    // everything (a respawn), anything else is loose damage or healing.
    public void SetHealth(float value)
    {
        if (value <= 0f)
        {
            Dead = true;
            return;
        }
        if (value >= MaxVitality)
        {
            Reset();
            return;
        }
        var current = Health;
        if (value < current)
            AddOtherLoss(current - value);
        else
            Heal(value - current);
        Normalize();
    }

    // ---- time ----

    // `roll` is a random 0..1 for the chances that happen over time (a heart restarting under compressions).
    public void Step(float dt, float roll = 1f)
    {
        if (Dead)
            return;
        _sinceCpr += dt;
        if (InArrest)
        {
            StepArrest(dt, roll);
            _suffocatedThisTick = _vacuumThisTick = _cprThisTick = false;
            return;
        }

        var bleeding = TotalBleeding;
        BloodLoss = Math.Min(100f, BloodLoss + bleeding * BleedToBloodLoss * dt);
        var infectionLoad = 0f;
        foreach (var part in _parts)
        {
            part.Bleeding = Math.Max(0f, part.Bleeding - ClottingPerSecond * dt);
            if (part.Bleeding <= 0f)
            {
                part.Trauma = Math.Max(0f, part.Trauma - TraumaHealPerSecond * dt);
                part.Burn = Math.Max(0f, part.Burn - BurnHealPerSecond * dt);
            }
            StepDressings(part, dt);
            StepInfection(part, dt);
            if (part.Infection > 30f)
                infectionLoad += (part.Infection - 30f) / 70f;
        }

        // Sepsis: an infection that has reached the blood.
        var sepsisGrowth = infectionLoad * 0.03f * (AntibioticSeconds > 0f ? 0.2f : 1f);
        Sepsis = Math.Clamp(Sepsis + (sepsisGrowth - (infectionLoad <= 0f ? 0.05f : 0f) - (AntibioticSeconds > 0f ? 0.3f : 0f)) * dt, 0f, 100f);

        if (bleeding < 0.01f)
            BloodLoss = Math.Max(0f, BloodLoss - BloodRecoverPerSecond * dt);

        // Hurt lungs recover from oxygen starvation slower, and past a point starve the body on their own; too much painkiller stops breathing.
        if (LungDamage > 50f)
            AddHypoxiaQuiet((LungDamage - 50f) * 0.02f * dt);
        if (Overdosing)
        {
            AddHypoxiaQuiet(3f * dt);
            StunSeconds = Math.Max(StunSeconds, 1f);
        }
        if (!_suffocatedThisTick)
            Hypoxia = Math.Max(0f, Hypoxia - HypoxiaRecoverPerSecond * (1f - LungDamage / 120f) * dt);
        if (Hypoxia > 85f)
            BrainDamage = Math.Min(100f, BrainDamage + 0.1f * dt);

        Concussion = Math.Max(Math.Min(100f, BrainDamage * 0.4f), Concussion - ConcussionRecoverPerSecond * dt);
        AnalgesiaSeconds = Math.Max(0f, AnalgesiaSeconds - dt);
        Dose = Math.Max(0f, Dose - dt / 150f);
        AdrenalineSeconds = Math.Max(0f, AdrenalineSeconds - dt);
        AntibioticSeconds = Math.Max(0f, AntibioticSeconds - dt);
        StunSeconds = Math.Max(0f, StunSeconds - dt);
        Normalize();
        _suffocatedThisTick = _vacuumThisTick = _cprThisTick = false;
    }

    private void AddHypoxiaQuiet(float amount)
    {
        Hypoxia = Math.Clamp(Hypoxia + amount, 0f, 100f);
        _suffocatedThisTick = true;
    }

    private void StepArrest(float dt, float roll)
    {
        if (_cprThisTick)
        {
            // Compressions keep the clock from running down, and with a sound heart may even start it.
            if (HeartDamage < 70f && LungDamage < 80f && roll < 0.025f * dt)
            {
                Resuscitate(12f);
                return;
            }
        }
        else
        {
            ArrestSecondsLeft -= dt;
        }
        if (ArrestSecondsLeft <= 0f && !_cprThisTick)
        {
            ArrestSecondsLeft = 0f;
            Dead = true;
        }
    }

    // A dressing comes off by itself once the wound under it is dry; a tourniquet left on too long hurts the limb.
    private void StepDressings(PartInjury part, float dt)
    {
        if (part.Bandaged)
        {
            part.BandageTimer = part.Bleeding <= 0.01f ? part.BandageTimer + dt : 0f;
            if (part.BandageTimer > 60f)
                part.Bandaged = false;
        }
        if (part.Tourniquet)
        {
            part.TourniquetSeconds += dt;
            if (part.TourniquetSeconds > 150f)
                part.Trauma = Math.Min(100f, part.Trauma + 0.15f * dt);
        }
        if (part.Fractured && part.Cast)
        {
            part.CastSeconds += dt;
            if (part.CastSeconds >= CastHealSeconds)
                part.Fractured = part.Splinted = part.Cast = false;
        }
        part.DisinfectedSeconds = Math.Max(0f, part.DisinfectedSeconds - dt);
    }

    private void StepInfection(PartInjury part, float dt)
    {
        var open = part.Bleeding > 0.5f || part.Burn > 15f;
        if (open && part.DisinfectedSeconds <= 0f)
            part.Infection = Math.Min(100f, part.Infection + 0.2f * (part.Bandaged ? 0.25f : 1f) * dt);
        else if (!open)
            part.Infection = Math.Max(0f, part.Infection - 0.03f * dt);
        if (AntibioticSeconds > 0f)
            part.Infection = Math.Max(0f, part.Infection - 1f * dt);
    }

    // Out of vitality: the heart stops (in a vacuum there is nothing to bring anyone back - that is death). Then consciousness.
    public void Normalize()
    {
        if (Dead)
            return;
        if (BrainDamage >= 100f)
        {
            Dead = true;
            return;
        }
        if (!InArrest && (Vitality <= 0f || HeartDamage >= 95f))
        {
            if (_vacuumThisTick)
            {
                Dead = true;
                return;
            }
            ArrestSecondsLeft = ArrestWindowSeconds;
            Unconscious = true;
            return;
        }

        var vitality = Vitality;
        // Adrenaline holds a person up a while longer.
        var faintBelow = AdrenalineSeconds > 0f ? 12f : 25f;
        var wakeAbove = AdrenalineSeconds > 0f ? 15f : 35f;
        if (!Unconscious && (vitality < faintBelow || Pain > 85f || Concussion > 60f))
            Unconscious = true;
        else if (Unconscious && !InArrest && vitality >= wakeAbove && Pain < 70f && Concussion < 45f && BrainDamage < 60f)
            Unconscious = false;
    }
}
