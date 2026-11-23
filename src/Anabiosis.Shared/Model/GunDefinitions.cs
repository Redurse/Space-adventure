namespace Anabiosis.Shared.Model;

// The firearms, after Barotrauma (direct user request: new guns after its weapon sheet, "чтобы они наносили урон
// и стреляли 1 в 1 как в баротравме", then "сделай все как в баротравме кроме ранений"). Every number here is
// the one the game itself uses for that gun and its round - read out of its own item definitions, not estimated:
//   * ReloadSeconds is its "reload": the time between two shots, which is all that paces fire - every gun
//     keeps firing at that pace for as long as the trigger is held, as it does there. Automatic is its
//     "hold trigger" mark (the guns meant to be fired that way); nothing in the rules depends on it.
//   * Wound and Bleeding are the two afflictions of the round (gunshot wound - or lacerations for the harpoon -
//     and bleeding) at their listed strength; DamageModifier is the multiplier of the gun itself on both (the
//     SMG fires its round hotter than the machine pistol does the same round).
//   * StunSeconds is the stun of the round: a hit leaves the target unable to act for that long. StunChance is
//     how often it lands at all (the SMG round stuns one hit in ten); BonusStun* is a second, rarer and longer
//     stun some rounds carry on top.
//   * SpreadDegrees / UnskilledSpreadDegrees is the whole cone a shot may leave in, for a perfect hand and a
//     hopeless one; RequiredSkill is the weapons skill the gun asks for, and SpreadFor is where a given skill
//     lands between the two (WeaponSkills.cs). Pellets leave in a cone of their own (PelletSpreadDegrees).
//   * Penetration is how much of a piece of armour the round ignores (round and gun together - the SMG adds
//     some of its own): 0 leaves the armour whole, 1 would cancel it (ArmorDefinitions.cs).
//   * StructureDamage is what one round does to a wall it stops in, on Barotrauma's scale (ShotTrace.cs).
//   * Hitscan: a bullet arrives the instant it is fired, as there; only the harpoon is a thing that flies.
//   * Capacity is how many rounds the gun holds: a magazine of that size, or that many loose rounds.
// What is not from Barotrauma is only what it has no counterpart for here: how fast the harpoon (and the tracer
// drawn for a bullet) crosses a room (ShotSpeed, in the units of this game) and how far a hostile crew member
// will open fire from (Range).
public sealed record GunSpec(
    ItemType Weapon, ItemType Ammo, int Capacity, bool Automatic, float ReloadSeconds,
    float Wound, float Bleeding, float StunSeconds,
    float SpreadDegrees, float UnskilledSpreadDegrees,
    float ShotSpeed, float Range, int Hands)
{
    public int Pellets { get; init; } = 1;
    public float PelletSpreadDegrees { get; init; }
    public float DamageModifier { get; init; } = 1f;
    public float RequiredSkill { get; init; }
    public float Penetration { get; init; }
    // The part of Penetration that is the gun and not the round (the SMG) - it stays when another round is loaded.
    public float WeaponPenetration { get; init; }
    // Some rounds burn as well as wound (the explosive harpoon).
    public float Burn { get; init; }
    public float StructureDamage { get; init; }
    public bool Hitscan { get; init; } = true;
    public float StunChance { get; init; } = 1f;
    public float BonusStunSeconds { get; init; }
    public float BonusStunChance { get; init; }
    // The bolt rifle is slow to cycle below a certain skill: UnskilledReloadSeconds with no skill at all, easing
    // to ReloadSeconds at ReloadSkillRequirement. 0 means the gun cycles the same for everyone.
    public float UnskilledReloadSeconds { get; init; }
    public float ReloadSkillRequirement { get; init; }

    public float HitWound => Wound * DamageModifier;
    public float HitBleeding => Bleeding * DamageModifier;

    public float ReloadFor(float weaponsSkill) => UnskilledReloadSeconds > 0f && ReloadSkillRequirement > 0f
        ? UnskilledReloadSeconds + (ReloadSeconds - UnskilledReloadSeconds) * Math.Clamp(weaponsSkill / ReloadSkillRequirement, 0f, 1f)
        : ReloadSeconds;

    // The cone for a given weapons skill: the further the skill falls short of what the gun asks for, the more
    // of the unskilled spread comes in - by the square of the shortfall, so a small gap costs little.
    public float SpreadFor(float weaponsSkill)
    {
        var failure = 1f - WeaponSkills.DegreeOfSuccess(weaponsSkill, RequiredSkill);
        return SpreadDegrees + (UnskilledSpreadDegrees - SpreadDegrees) * failure * failure;
    }

    // Loose rounds go in one at a time and leave nothing behind; a magazine goes in whole and stays in when empty.
    public bool StackFed => GunDefinitions.IsStackAmmo(Ammo);
}

public static class GunDefinitions
{
    private static readonly GunSpec[] All =
    {
        //          gun                    ammo                       cap  auto   reload  wound bleed  stun  spread unskilled speed range hands
        new GunSpec(ItemType.Revolver,     ItemType.RevolverRounds,     6, false, 0.5f,   24f,  16f,  0.3f,  0.1f,  6f,  34f, 7f, 1)
            { RequiredSkill = 40f, Penetration = 0.1f, StructureDamage = 10f },
        new GunSpec(ItemType.Handcannon,   ItemType.HandcannonRounds,   6, false, 1.0f,   70f,  50f,  1.5f,  0f,    8f,  36f, 7f, 1)
            { RequiredSkill = 60f, Penetration = 0.5f, StructureDamage = 20f },
        new GunSpec(ItemType.MachinePistol, ItemType.SmgMagazine,      21, true,  0.175f, 12.5f, 12.5f, 0.2f, 10f, 18f,  30f, 5f, 1)
            { DamageModifier = 0.8f, RequiredSkill = 50f, StructureDamage = 4f, StunChance = 0.1f },
        new GunSpec(ItemType.Smg,          ItemType.SmgMagazine,       21, true,  0.19f,  12.5f, 12.5f, 0.2f, 10f, 16f,  30f, 6f, 2)
            { DamageModifier = 1.3f, RequiredSkill = 50f, Penetration = 0.15f, WeaponPenetration = 0.15f, StructureDamage = 4f, StunChance = 0.1f },
        new GunSpec(ItemType.Shotgun,      ItemType.ShotgunShells,      6, false, 1.0f,   8f,   6f,   0.1f,  0.1f,  3f,  28f, 5f, 2)
            { Pellets = 8, PelletSpreadDegrees = 15f, RequiredSkill = 50f, StructureDamage = 1f },
        new GunSpec(ItemType.AutoShotgun,  ItemType.ShotgunShells,     24, true,  0.45f,  8f,   6f,   0.1f,  5f,   18f,  28f, 5f, 2)
            { Pellets = 8, PelletSpreadDegrees = 15f, DamageModifier = 0.9f, RequiredSkill = 75f, StructureDamage = 1f },
        // ItemType.Rifle is the "автомат" this game had before the other guns existed - it is the assault rifle.
        new GunSpec(ItemType.Rifle,        ItemType.Magazine,          30, true,  0.24f,  25f,  15f,  0.25f, 4f,   16f,  34f, 6f, 2)
            { RequiredSkill = 50f, Penetration = 0.3f, StructureDamage = 6f, BonusStunSeconds = 0.5f, BonusStunChance = 0.2f },
        new GunSpec(ItemType.BoltRifle,    ItemType.RifleRounds,        6, false, 2.0f,   55f,  33f,  0.6f,  0f,    2f,  44f, 9f, 2)
            { RequiredSkill = 60f, Penetration = 0.6f, StructureDamage = 15f, UnskilledReloadSeconds = 5f, ReloadSkillRequirement = 40f },
        new GunSpec(ItemType.Hmg,          ItemType.HmgMagazine,      100, true,  0.1f,   12f,  10f,  0.1f,  6f,   50f,  34f, 7f, 2)
            { RequiredSkill = 90f, Penetration = 0.4f, StructureDamage = 4f, BonusStunSeconds = 0.5f, BonusStunChance = 0.01f },
        new GunSpec(ItemType.HarpoonGun,   ItemType.Harpoons,           6, false, 1.75f,  35f,  35f,  0.5f,  1f,   10f,  18f, 6f, 2)
            { RequiredSkill = 30f, StructureDamage = 15f, Hitscan = false },
    };

    private static readonly Dictionary<ItemType, GunSpec> ByWeapon = All.ToDictionary(g => g.Weapon);

    // The other rounds a gun takes besides its ordinary one, after Barotrauma: each replaces the ordinary round of
    // whichever guns fire that round (Base) and changes what a hit does. Pellets, where set, replaces the pellet
    // count (the slug is one projectile out of a shotgun).
    public sealed record AmmoVariant(ItemType Item, ItemType Base, float Wound, float Bleeding, float StunSeconds,
        float Penetration, float StructureDamage, int? Pellets = null, float Burn = 0f);

    private static readonly AmmoVariant[] VariantList =
    {
        // depleted fuel: a heavier wound that bleeds less and goes through armour better
        new(ItemType.RevolverRoundsDepleted, ItemType.RevolverRounds, 35f, 10f, 0.3f, 0.25f, 10f),
        new(ItemType.SmgMagazineDepleted, ItemType.SmgMagazine, 15f, 5f, 0.15f, 0.25f, 5f),
        // rubber shot: bruises and knocks down, draws no blood, does nothing to walls
        new(ItemType.ShotgunShellsRubber, ItemType.ShotgunShells, 3f, 0f, 0.3f, 0f, 0f),
        // the explosive slug: one projectile instead of eight pellets
        new(ItemType.ShotgunSlugs, ItemType.ShotgunShells, 20f, 10f, 0.8f, 0.5f, 20f, Pellets: 1),
        // the heavy harpoon (Barotrauma calls its own an alien spear) and the explosive one
        new(ItemType.HarpoonsHeavy, ItemType.Harpoons, 60f, 60f, 0.5f, 0f, 20f),
        new(ItemType.HarpoonsExplosive, ItemType.Harpoons, 25f, 0f, 1.5f, 0.5f, 20f, Burn: 50f),
    };

    private static readonly Dictionary<ItemType, AmmoVariant> VariantByItem = VariantList.ToDictionary(v => v.Item);
    private static readonly Dictionary<(ItemType, ItemType), GunSpec> LoadedSpecs = new();

    public static IReadOnlyList<AmmoVariant> Variants => VariantList;

    public static AmmoVariant? VariantOf(ItemType ammo) => VariantByItem.GetValueOrDefault(ammo);

    // Whether this gun takes this ammunition: its ordinary round, or any variant of it.
    public static bool Accepts(GunSpec gun, ItemType ammo) => gun.Ammo == ammo || VariantByItem.GetValueOrDefault(ammo)?.Base == gun.Ammo;

    // The gun as it fires with this kind of round in it: the same gun, with the numbers of the round swapped in.
    public static GunSpec Loaded(GunSpec gun, ItemType? ammo)
    {
        if (ammo is not { } kind || !VariantByItem.TryGetValue(kind, out var variant) || variant.Base != gun.Ammo)
            return gun;
        lock (LoadedSpecs)
        {
            if (!LoadedSpecs.TryGetValue((gun.Weapon, kind), out var spec))
                LoadedSpecs[(gun.Weapon, kind)] = spec = gun with
                {
                    Wound = variant.Wound, Bleeding = variant.Bleeding, StunSeconds = variant.StunSeconds, StunChance = 1f,
                    BonusStunSeconds = 0f, BonusStunChance = 0f,
                    Penetration = variant.Penetration + gun.WeaponPenetration, StructureDamage = variant.StructureDamage,
                    Pellets = variant.Pellets ?? gun.Pellets, Burn = variant.Burn,
                };
            return spec;
        }
    }

    public static IReadOnlyList<GunSpec> Guns => All;

    public static GunSpec? Of(ItemType weapon) => ByWeapon.GetValueOrDefault(weapon);

    public static bool IsAmmo(ItemType type) => StackSize(type) > 0f;

    // Loose rounds: carried as a stack ("x6"), topped into the gun, and the stack keeps what did not fit.
    public static bool IsStackAmmo(ItemType type) => (VariantByItem.GetValueOrDefault(type)?.Base ?? type) is ItemType.RevolverRounds
        or ItemType.HandcannonRounds or ItemType.ShotgunShells or ItemType.RifleRounds or ItemType.Harpoons;

    // How many rounds one item of this ammunition is: a full stack of loose rounds, or a full magazine.
    public static float StackSize(ItemType ammo) => (VariantByItem.GetValueOrDefault(ammo)?.Base ?? ammo) switch
    {
        ItemType.RevolverRounds => RevolverDefinitions.CylinderRounds,
        ItemType.HandcannonRounds => 6f,
        ItemType.ShotgunShells => 12f,
        ItemType.RifleRounds => 6f,
        ItemType.Harpoons => 6f,
        ItemType.Magazine => MagazineDefinitions.FullCharge,
        ItemType.SmgMagazine => 21f,
        ItemType.HmgMagazine => 100f,
        _ => 0f,
    };
}
