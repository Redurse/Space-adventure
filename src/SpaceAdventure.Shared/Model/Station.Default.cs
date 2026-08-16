namespace SpaceAdventure.Shared.Model;

// The per-kind station layouts (game_design.md section 10 — "у разных станций разный набор
// модулей/услуг"). All three share the same spine: a docking chamber at the entrance, then one
// room per service, built from the same helper so adding a kind is just listing its services.
// Room/door ids are prefixed per kind, since World tracks door open/closed state in one flat
// dictionary keyed by id across every structure in the game.
public sealed partial class Station
{
    private const float RoomWidth = 5f;
    private const float RoomHeight = 6f;
    private const float DoorRowY = 3f;

    // Which services each kind offers, in the order their rooms are laid out from the dock.
    private static IReadOnlyList<NpcKind> ServicesFor(StationKind kind) => kind switch
    {
        // Security always comes last so it stands in the room furthest from the dock - a thief
        // gets a few rooms of warning rather than walking straight into the guard.
        StationKind.Trade => new[] { NpcKind.Trader, NpcKind.Administrator, NpcKind.Mechanic, NpcKind.Security },
        StationKind.Shipyard => new[] { NpcKind.Trader, NpcKind.Mechanic, NpcKind.Shipwright, NpcKind.Security },
        _ => new[] { NpcKind.Administrator, NpcKind.Trader, NpcKind.Security }, // Outpost: the bare minimum
    };

    // Station property left out in the open, one crate per service room (game_design.md section 10
    // — theft). Nothing valuable enough to break the economy; the point is the risk, not the haul.
    // Deliberately no AmmoCrate: carrying one makes World.Interact.cs treat every [F] as "reload a
    // turret", which would leave a thief unable to use their own helm or lockers until they got
    // rid of it - a confusing punishment that has nothing to do with the crime.
    private static ItemType CrateItemFor(NpcKind service) => service switch
    {
        NpcKind.Trader => ItemType.MedKit,
        NpcKind.Mechanic => ItemType.WireSpool,
        NpcKind.Shipwright => ItemType.FuelRod,
        _ => ItemType.Mineral, // an administrator's office keeps valuables, not munitions
    };

    private static (string RoomName, string NpcName) LabelsFor(NpcKind kind) => kind switch
    {
        NpcKind.Trader => ("Торговый зал", "Торговец"),
        NpcKind.Administrator => ("Кабинет администратора", "Администратор станции"),
        NpcKind.Mechanic => ("Мастерская", "Механик станции"),
        NpcKind.Shipwright => ("Верфь", "Корабельный мастер"),
        NpcKind.Security => ("Пост охраны", "Охранник"),
        _ => ("Отсек", "Сотрудник"),
    };

    // Fixed location in the docking-approach field space - the same spot for every station, since
    // only one is ever "the station you're approaching" at a time (World.StationDocking.cs).
    private static readonly Vec2 WorldCenter = new(150f, 150f);

    public static Station CreateDefault() => Create(StationKind.Outpost, new Vec2(0, DoorRowY));

    // connectorAnchor is where the station's own umbilical door has to sit: the exact position of
    // the ship's outer airlock door in the ship's interior coordinates. The whole layout is built
    // around it, so a docked ship's airlock and the station's dock chamber share one door rectangle
    // rather than being two places joined by a teleport.
    public static Station Create(StationKind kind, Vec2 connectorAnchor)
    {
        var services = ServicesFor(kind);
        var prefix = kind.ToString().ToLowerInvariant();
        var originX = connectorAnchor.X;
        var top = connectorAnchor.Y - RoomHeight / 2;
        var rowY = connectorAnchor.Y;

        var rooms = new List<Room> { new($"{prefix}-dock", "Стыковочный отсек", originX, top, RoomWidth, RoomHeight) };
        var doors = new List<Door>();
        var npcs = new List<StationNpc>();
        var crates = new List<StationCrate>();

        for (var i = 0; i < services.Count; i++)
        {
            var service = services[i];
            var (roomName, npcName) = LabelsFor(service);
            var left = originX + RoomWidth * (i + 1);

            var roomId = $"{prefix}-{service.ToString().ToLowerInvariant()}";
            var slug = service.ToString().ToLowerInvariant();
            rooms.Add(new Room(roomId, roomName, left, top, RoomWidth, RoomHeight));
            doors.Add(new Door($"{prefix}-door-{i}", rooms[i].Id, roomId, left, rowY, 1.0f, 1.8f));
            npcs.Add(new StationNpc($"{prefix}-npc-{slug}", npcName, service,
                X: left + RoomWidth / 2, Y: rowY));

            // The crate sits away from the room's own staffer, so lifting it is a deliberate walk
            // into a corner rather than something you brush past while trading.
            if (service != NpcKind.Security)
                crates.Add(new StationCrate($"{prefix}-crate-{slug}", roomId, left + RoomWidth / 2, rowY + 2f, CrateItemFor(service)));
        }

        // Left wall of the docking chamber - the same physical rectangle as the docked ship's outer
        // airlock door, which is what makes walking across it an ordinary doorway crossing.
        var shipConnector = new AirlockOuterDoor($"{prefix}-connector", rooms[0].Id, originX, rowY, 1.0f, 1.8f);

        return new Station(rooms, doors, shipConnector, npcs, crates, WorldCenter, rooms[0].Id);
    }
}
