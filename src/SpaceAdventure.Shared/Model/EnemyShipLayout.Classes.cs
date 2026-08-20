namespace SpaceAdventure.Shared.Model;

// The floor plans themselves. Ids are prefixed per class because door state, and the room a
// character is standing in, are tracked in flat dictionaries shared by every structure in the game -
// two hulls with a "bridge" each would be the same bridge as far as those are concerned.
//
// Each plan puts the breach at one end, so boarding is always a fight inward from the hull, and each
// leaves the compartment behind the breach in vacuum. What differs is the shape of the run in, how
// many defenders hold it, and whether they can survive losing their air.
public sealed partial class EnemyShipLayout
{
    public static EnemyShipLayout Create(EnemyShipClass kind) => kind switch
    {
        EnemyShipClass.Freighter => CreateFreighter(),
        EnemyShipClass.Gunship => CreateGunship(),
        _ => CreateRaider(),
    };

    // Three rooms in a line, three unsuited crew: the ordinary opposition, and the shape boarding
    // was built and tested against.
    private static EnemyShipLayout CreateRaider()
    {
        var rooms = new[]
        {
            new Room("raider-breach", "Пробитый отсек", 0, 0, 5, 6),
            new Room("raider-hold", "Трюм", 5, 0, 5, 6),
            new Room("raider-bridge", "Мостик", 10, 0, 5, 6),
        };

        var doors = new[]
        {
            new Door("raider-door-breach-hold", "raider-breach", "raider-hold", 5, 3, 1.0f, Door.StandardSpanUnits),
            new Door("raider-door-hold-bridge", "raider-hold", "raider-bridge", 10, 3, 1.0f, Door.StandardSpanUnits),
        };

        // Defenders spread one per room so boarding is a room-by-room fight rather than one brawl.
        var crew = new[]
        {
            // Whoever holds the breached compartment is standing in vacuum, so they are in a suit -
            // anywhere else and an unsuited defender there would be dead before the boarding party
            // arrived, which would hand away the entry fight for free.
            new EnemyCrewSpawn("raider-crew-1", "Пехотинец", "raider-breach", 3.5f, 3f, ItemType.Knife, Suited: true),
            new EnemyCrewSpawn("raider-crew-2", "Стрелок", "raider-hold", 7.5f, 3f, ItemType.Rifle),
            new EnemyCrewSpawn("raider-crew-3", "Капитан", "raider-bridge", 12.5f, 3f, ItemType.LaserRifle),
        };

        return new EnemyShipLayout(EnemyShipClass.Raider, "Рейдер", rooms, doors,
            new AirlockOuterDoor("raider-hatch", "raider-breach", 0, 3, 1.0f, Door.StandardSpanUnits), crew, "raider-breach");
    }

    // A hauler: a long hold with a crew berth hung off it, four defenders but only one of them
    // really a fighter, and not a suit among them. Open its doors and wait and the ship is yours -
    // which is exactly the point of it existing alongside the gunship.
    private static EnemyShipLayout CreateFreighter()
    {
        var rooms = new[]
        {
            new Room("freighter-breach", "Пробитый шлюз", 0, 0, 4, 6),
            new Room("freighter-hold", "Грузовой трюм", 4, 0, 8, 6),
            new Room("freighter-berth", "Кубрик", 4, 6, 8, 4),
            new Room("freighter-bridge", "Мостик", 12, 0, 5, 6),
        };

        var doors = new[]
        {
            new Door("freighter-door-breach-hold", "freighter-breach", "freighter-hold", 4, 3, 1.0f, Door.StandardSpanUnits),
            new Door("freighter-door-hold-berth", "freighter-hold", "freighter-berth", 7.1f, 6, Door.StandardSpanUnits, 1.0f),
            new Door("freighter-door-hold-bridge", "freighter-hold", "freighter-bridge", 12, 3, 1.0f, Door.StandardSpanUnits),
        };

        var crew = new[]
        {
            new EnemyCrewSpawn("freighter-crew-1", "Грузчик", "freighter-hold", 6f, 2f, ItemType.Knife),
            new EnemyCrewSpawn("freighter-crew-2", "Грузчик", "freighter-hold", 10f, 4f, ItemType.Knife),
            new EnemyCrewSpawn("freighter-crew-3", "Охранник", "freighter-berth", 8f, 8f, ItemType.Rifle),
            new EnemyCrewSpawn("freighter-crew-4", "Капитан", "freighter-bridge", 14.5f, 3f, ItemType.Rifle),
        };

        return new EnemyShipLayout(EnemyShipClass.Freighter, "Грузовик", rooms, doors,
            new AirlockOuterDoor("freighter-hatch", "freighter-breach", 0, 3, 1.0f, Door.StandardSpanUnits), crew, "freighter-breach");
    }

    // A warship: a gun deck to cross under fire, an engine room off it, and a bridge at the far end.
    // The crew fights in suits, so venting it does nothing and the whole hull has to be cleared by
    // shooting - the counterpart to the freighter, and the reason the vent tactic isn't the answer
    // to every boarding.
    private static EnemyShipLayout CreateGunship()
    {
        var rooms = new[]
        {
            new Room("gunship-breach", "Абордажный тамбур", 0, 0, 4, 6),
            new Room("gunship-gundeck", "Орудийная палуба", 4, 0, 6, 6),
            new Room("gunship-engine", "Машинное", 4, 6, 6, 4),
            new Room("gunship-bridge", "Мостик", 10, 0, 5, 6),
        };

        var doors = new[]
        {
            new Door("gunship-door-breach-gundeck", "gunship-breach", "gunship-gundeck", 4, 3, 1.0f, Door.StandardSpanUnits),
            new Door("gunship-door-gundeck-engine", "gunship-gundeck", "gunship-engine", 6.1f, 6, Door.StandardSpanUnits, 1.0f),
            new Door("gunship-door-gundeck-bridge", "gunship-gundeck", "gunship-bridge", 10, 3, 1.0f, Door.StandardSpanUnits),
        };

        var crew = new[]
        {
            new EnemyCrewSpawn("gunship-crew-1", "Канонир", "gunship-gundeck", 5.5f, 2f, ItemType.Rifle, Suited: true),
            new EnemyCrewSpawn("gunship-crew-2", "Канонир", "gunship-gundeck", 8.5f, 4.5f, ItemType.Rifle, Suited: true),
            // The engineer works in shirtsleeves next to his machinery - the one soft spot on the hull.
            new EnemyCrewSpawn("gunship-crew-3", "Механик", "gunship-engine", 7f, 8f, ItemType.Knife),
            new EnemyCrewSpawn("gunship-crew-4", "Командир", "gunship-bridge", 12.5f, 3f, ItemType.LaserRifle, Suited: true),
        };

        return new EnemyShipLayout(EnemyShipClass.Gunship, "Канонерка", rooms, doors,
            new AirlockOuterDoor("gunship-hatch", "gunship-breach", 0, 3, 1.0f, Door.StandardSpanUnits), crew, "gunship-breach");
    }
}
