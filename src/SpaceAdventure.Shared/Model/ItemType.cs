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
    // Швейцарский армейский топор экипажа - держит его в одной руке, ломает запертую/повреждённую
    // дверь за пару ударов (World.Doors.cs's ChopDoor), а не только служит оружием ближнего боя.
    Axe,
    // "Отвёртка поломки" - починить ею нельзя ничего: ЛКМ по прибору ломает его вместо ремонта
    // (World.Wiring.cs's DamageDeviceWiring, Game1.Input.cs's own left-click branch for it).
    GoshaScrewdriver,
    // Worn, not held (EquipSlotDefinitions) - Barotrauma-style equipment slots (game_design.md
    // section 13). BeltBag opens its own small 2x3 sub-inventory once worn (Inventory.BeltBagSlots);
    // IdCard is purely a worn slot filler for now, no access-control mechanic behind it yet.
    BeltBag,
    IdCard,
    Rifle,
    LaserRifle,
    FuelRod,
    MedKit,
    WireSpool,
    Mineral,
    // Slots into a suit or a cutter rather than being used on its own (OxygenTankDefinitions):
    // the suit needs it to keep anyone alive in vacuum, the cutter needs it to burn at all.
    OxygenTank,
    // Slots into a welding tool rather than being used on its own (WeldingTankDefinitions) - the
    // welder doesn't light without one, same as the cutter and its oxygen tank.
    WeldingTank,
    // Purchasable wiring components (ComponentKind, World.ComponentMounts.cs) - one item per kind,
    // installed into an empty ComponentMount and wired up like the ship's own built-in devices.
    GateAnd,
    GateOr,
    GateNot,
    GateXor,
    Timer,
    Memory,
    Relay,
    OxygenSensor,
    BreachSensor,
    PowerLossSensor,
    MotionSensor,
    AutoDoorController,
    AlarmKlaxon,
    LightToggle,
}
