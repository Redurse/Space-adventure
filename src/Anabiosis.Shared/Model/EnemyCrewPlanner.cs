namespace Anabiosis.Shared.Model;

// Who is aboard a hostile ship and where each one starts - one captain at the helm, a scientist at every gun (up to
// two), two engineers in the engineering compartments, and fighters holding the reactor, the cockpit and the airlock
// (8 people, the "6-8" the design asked for). Everyone starts standing on a real walkable tile of the ship, never inside a
// device.
public static class EnemyCrewPlanner
{
    private static readonly string[] Names =
    {
        "Игорь", "Марина", "Дмитрий", "Ольга", "Сергей", "Анна", "Виктор", "Елена", "Павел", "Наталья", "Артём", "Ксения",
    };

    public static IReadOnlyList<EnemyCrewSpawn> Plan(Ship ship)
    {
        var crew = new List<EnemyCrewSpawn>();
        var next = 0;

        (string RoomId, Vec2 Position)? Stand(Vec2 near)
        {
            var tile = TilePathfinder.NearestPassable(ship.Tiles, TilePathfinder.TileAt(near), null, 8);
            if (tile is not { } found)
                return null;
            var position = TilePathfinder.CenterOf(found);
            return (TileMovement.RoomIdAt(ship.Rooms, position) ?? ship.Rooms[0].Id, position);
        }

        void Add(EnemyCrewRole role, string label, Vec2 near, ItemType weapon, bool suited, string? postId = null)
        {
            if (Stand(near) is not { } spot)
                return;
            // Two people never start on the same tile.
            while (crew.Any(c => (c.Position - spot.Position).Length() < 0.9))
                if (Stand(spot.Position + new Vec2(1, 0)) is { } shifted && shifted.Position != spot.Position)
                    spot = shifted;
                else
                    break;
            crew.Add(new EnemyCrewSpawn($"enemy-crew-{next}", $"{label} {Names[next % Names.Length]}", spot.RoomId,
                (float)spot.Position.X, (float)spot.Position.Y, weapon, suited, role, postId));
            next++;
        }

        Add(EnemyCrewRole.Captain, "Капитан", ship.HelmConsole.Position, ItemType.LaserRifle, suited: false);

        foreach (var turret in ship.Turrets.Take(2))
            Add(EnemyCrewRole.Scientist, "Учёный", turret.PeriscopePosition, ItemType.Knife, suited: false, postId: turret.Id);

        // Engineers go to the engineering compartments (by name), falling back to the largest rooms.
        var engineeringRooms = ship.Rooms.Where(r => r.Name.Contains("нжинер", StringComparison.OrdinalIgnoreCase)).ToList();
        if (engineeringRooms.Count < 2)
            engineeringRooms = engineeringRooms.Concat(ship.Rooms.Except(engineeringRooms)
                .OrderByDescending(r => r.Width * r.Height)).Take(2).ToList();
        foreach (var room in engineeringRooms.Take(2))
            Add(EnemyCrewRole.Engineer, "Инженер", room.Center, ItemType.Knife, suited: true); // they go into breached, airless compartments to patch them

        // Fighters: the reactor, the cockpit, and the airlock (or whatever is left) - the places a boarding party heads for.
        var reactorRoom = ship.Devices.FirstOrDefault(d => d.Kind == DeviceKind.Reactor)?.RoomId;
        var fighterRooms = new List<Room>();
        foreach (var id in new[] { reactorRoom, ship.HelmConsole.RoomId })
            if (ship.Rooms.FirstOrDefault(r => r.Id == id) is { } room && !fighterRooms.Contains(room))
                fighterRooms.Add(room);
        foreach (var room in ship.Rooms.Where(r => r.Name.Contains("люз", StringComparison.OrdinalIgnoreCase)).Take(1))
            if (!fighterRooms.Contains(room))
                fighterRooms.Add(room);
        foreach (var room in ship.Rooms.OrderByDescending(r => r.Width * r.Height))
            if (fighterRooms.Count < 3 && !fighterRooms.Contains(room))
                fighterRooms.Add(room);
        for (var i = 0; i < fighterRooms.Count; i++)
            Add(EnemyCrewRole.Fighter, "Боец", fighterRooms[i].Center, i == 0 ? ItemType.Rifle : ItemType.LaserRifle, suited: true);

        return crew;
    }
}
