namespace Anabiosis.Shared.Model;

// What a crew member is wearing against gunfire. Vest is the body armour, Helmet the ballistic helmet.
public readonly record struct ArmorWorn(bool Vest, bool Helmet)
{
    public static readonly ArmorWorn None = new(false, false);
}

// Body armour and the ballistic helmet, after Barotrauma - the multipliers are the ones in its item
// definitions. The vest covers the torso and nothing else; the helmet covers the head. A hit on a covered part
// has its wound and its bleeding multiplied down; a round with penetration takes that much of the protection
// away again (0 leaves the armour whole, 1 would cancel it). Arms and legs are never covered.
public static class ArmorDefinitions
{
    public const float VestWound = 0.3f;      // gunshot wounds, lacerations, blunt trauma
    public const float VestBleeding = 0.2f;
    public const float VestBlast = 0.5f;      // explosion damage to the torso
    public const float HelmetWound = 0.2f;    // gunshot wounds, lacerations
    public const float HelmetBleeding = 0.1f;
    public const float HelmetConcussion = 0f; // the helmet stops concussion outright

    public static (float Wound, float Bleeding, float Concussion) Against(ArmorWorn armor, BodyPart part, float penetration)
    {
        var (wound, bleeding, concussion) = part switch
        {
            BodyPart.Torso when armor.Vest => (VestWound, VestBleeding, 1f),
            BodyPart.Head when armor.Helmet => (HelmetWound, HelmetBleeding, HelmetConcussion),
            _ => (1f, 1f, 1f),
        };
        var through = Math.Clamp(penetration, 0f, 1f);
        return (wound + (1f - wound) * through, bleeding + (1f - bleeding) * through, concussion + (1f - concussion) * through);
    }

    public static bool IsArmor(ItemType type) => type is ItemType.BodyArmor or ItemType.BallisticHelmet;
}
