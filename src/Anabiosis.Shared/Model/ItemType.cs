namespace Anabiosis.Shared.Model;

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
    // M62 - the consumable a room build actually burns (World.ShipBuilding.cs's ship-wide
    // _hullPlatingStock counter, restocked at any station the same way ammo/oxygen/hull already
    // are) - a physical resource rather than only credits, so building has a real supply-chain limit
    // in the middle of a fight, not just a price tag. Never actually held in a character's own
    // Inventory (no pickup path exists for it) - it lives purely as a ship-wide count, the same
    // "quiet number, not a real Item slot" treatment AmmoStorage's own stock already gets before a
    // crate is ever taken off the rack.
    HullPlating,
    // Worn into EquipSlot.Headset (EquipSlotDefinitions) - required to transmit on the radio voice
    // channel (World.Voice.cs's RelayVoiceChunk gates IsRadio chunks on this); local proximity
    // voice needs no item at all. Direct user request, matching Barotrauma's own radio-as-item
    // model instead of a free always-available channel.
    Radio,
    // Direct user request ("добавим множество предметов материалов") - raw ores, mined from
    // OreDeposit.OreType (World.Cutting.cs), replacing the old one-size-fits-all Mineral drop
    // (Mineral itself stays in the enum, still used by Station.Procedural.cs's own unrelated crate
    // loot table, just no longer what mining actually yields). FabricatorCatalog.cs's own recipes
    // turn a subset of these into the refined materials below.
    IronOre, NickelOre, ZincOre, CopperOre, TitaniumOre, UraniumOre, Silicon, Carbon, AluminumOre, PlastalloyOre,
    // Refined materials - each one a FabricatorCatalog.cs recipe's own output, craftable at the
    // Фабрикатор (Fabricator) device once a character stands near it with the right ores/plates
    // already in their own Inventory.
    SteelPlate, TitaniumPlate, CopperCable, Plastic, Plastalloy,
    // Cosmoteer-style jump fuel: every hop to another star system burns some (World.StarSystems.cs).
    // Appended last so saved games' numeric enum values stay valid.
    Hyperium,
    // The rifle's magazine: plugs into a Rifle like a tank into a cutter and is worn down by firing
    // (MagazineDefinitions, World.PersonalShots.cs). Appended last so saved enum values stay valid.
    Magazine,
    // Medicine (World.Treatment.cs, MedicalTreatment.cs) - used on a body part through the health window (H); appended last so saved enum values stay valid.
    Bandage, Tourniquet, Antiseptic, Splint, PlasterCast, Painkiller, Antibiotic, BurnGel, Adrenaline, Defibrillator, SurgeryKit,
    // The revolver (RevolverDefinitions.cs): a one-handed single-shot sidearm that carries its own six rounds, as in Barotrauma. RevolverRounds are loaded
    // into it by dragging them on (a stack of up to six, shown as "x6"); appended last so saved enum values stay valid.
    Revolver, RevolverRounds,
    // The rest of the firearms after Barotrauma (GunDefinitions.cs) and what they fire: loose rounds in stacks, or magazines.
    // Appended last so saved enum values stay valid.
    Handcannon, MachinePistol, Smg, Shotgun, AutoShotgun, BoltRifle, Hmg, HarpoonGun,
    HandcannonRounds, SmgMagazine, ShotgunShells, RifleRounds, HmgMagazine, Harpoons,
    // Armour after Barotrauma (ArmorDefinitions.cs): the vest goes where a suit would, the helmet on the head.
    BodyArmor, BallisticHelmet,
    // Other rounds the same guns take (GunDefinitions.Variants): a gun remembers which kind is loaded in it.
    RevolverRoundsDepleted, SmgMagazineDepleted, ShotgunShellsRubber, ShotgunSlugs, HarpoonsHeavy, HarpoonsExplosive,
    // Raw, unprocessed ore: what an asteroid deposit actually yields (OreDeposit.OreType). The fabricator's "Переработка"
    // recipes turn each one into the matching material above (IronOre, NickelOre ... Hyperium - see ItemDefinitions.RefinedFrom).
    // Appended last so saved enum values stay valid.
    RawIron, RawNickel, RawZinc, RawCopper, RawTitanium, RawUranium, RawSilicon, RawCarbon, RawAluminum, RawPlastalloy, RawHyperium,
    // Wiring parts, W7/W8 of wiring-plan.md (ComponentKind.Delay ... Wireless): one item per kind like the ones above. Appended last so
    // saved enum values stay valid.
    Delay, Oscillator, Comparator, SignalCheck, Button, MathPart, FunctionPart, WirelessPart,
    // The two parts with a text setting: the regular expression and the terminal (typed messages).
    RegexPart, TerminalPart,
}
