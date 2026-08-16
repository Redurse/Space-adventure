namespace SpaceAdventure.Shared.Model;

// Carryable item types (game_design.md sections 2, 3, 13). AmmoCrate/Spacesuit are carried or
// worn only — never held in a hand (see ItemDefinitions.HandsRequired); the rest are hand tools
// or personal weapons that must be equipped into a hand via the inventory's hold strip.
public enum ItemType
{
    AmmoCrate,
    Spacesuit,
    Wrench,
    Screwdriver,
    WeldingTool,
    Cutter,
    Knife,
    Rifle,
    LaserRifle,
    FuelRod,
    MedKit,
    WireSpool,
    Mineral,
    // Slots into a suit or a cutter rather than being used on its own (OxygenTankDefinitions):
    // the suit needs it to keep anyone alive in vacuum, the cutter needs it to burn at all.
    OxygenTank,
}
