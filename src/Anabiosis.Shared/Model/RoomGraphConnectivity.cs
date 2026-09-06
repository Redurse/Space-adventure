namespace Anabiosis.Shared.Model;

// M61 - the general "does this room graph stay connected" utility the plan (humble-soaring-cat.md)
// calls for, built on the simplest possible case first (World.ShipBuilding.cs's TryDemolishRoom,
// docked, one room at a time, player-initiated). Plain BFS over the door graph - CustomDoorDef is
// already exactly this graph's edge list (Ship.ToDefinition() emits one per real inter-room Door;
// an AirlockOuterDoor is an edge to open space, not to another room, so it never appears here and
// correctly plays no part in room-to-room reachability). M63's structural-detachment check and
// M65's enemy-generator connectivity check are both meant to reuse this unchanged.
public static class RoomGraphConnectivity
{
    // Every room reachable from `fromRoomId` by crossing zero or more doors - a plain reachability
    // set, not a shortest-path one, since nothing here cares about distance.
    public static HashSet<string> ReachableFrom(IReadOnlyList<CustomRoomDef> rooms, IReadOnlyList<CustomDoorDef> doors, string fromRoomId)
    {
        var adjacency = new Dictionary<string, List<string>>();
        foreach (var room in rooms)
            adjacency[room.Id] = new List<string>();
        foreach (var door in doors)
        {
            if (adjacency.TryGetValue(door.RoomAId, out var fromA))
                fromA.Add(door.RoomBId);
            if (adjacency.TryGetValue(door.RoomBId, out var fromB))
                fromB.Add(door.RoomAId);
        }

        var visited = new HashSet<string>();
        if (!adjacency.ContainsKey(fromRoomId))
            return visited; // not a room in this graph at all - nothing is reachable from it

        var queue = new Queue<string>();
        visited.Add(fromRoomId);
        queue.Enqueue(fromRoomId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in adjacency[current])
                if (visited.Add(neighbor))
                    queue.Enqueue(neighbor);
        }
        return visited;
    }

    // Whether the WHOLE graph is one connected piece, judged from `fromRoomId` (typically wherever
    // the reactor/spawn sits - the room every other one has to stay able to reach).
    public static bool AllReachable(IReadOnlyList<CustomRoomDef> rooms, IReadOnlyList<CustomDoorDef> doors, string fromRoomId) =>
        ReachableFrom(rooms, doors, fromRoomId).Count == rooms.Count;
}
