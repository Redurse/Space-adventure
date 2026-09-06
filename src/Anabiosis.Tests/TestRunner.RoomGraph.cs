using Anabiosis.Server;
using Anabiosis.Shared.Model;

// Plain BFS over a station's (or a ship's) own door graph - small enough (at most a couple dozen
// rooms) that this is instant, and robust to ANY room layout (a straight row, a ring, a tree),
// unlike the old "walk along the one shared height every room happens to share" heuristic this
// replaces for station navigation (M49 - stations are no longer a straight row of rooms).
internal static partial class TestRunner
{
    private static Dictionary<string, List<(Door Door, string OtherRoomId)>> BuildDoorAdjacency(IReadOnlyList<Door> doors)
    {
        var adjacency = new Dictionary<string, List<(Door, string)>>();
        void AddEdge(string roomId, Door door, string otherRoomId)
        {
            if (!adjacency.TryGetValue(roomId, out var edges))
                adjacency[roomId] = edges = new List<(Door, string)>();
            edges.Add((door, otherRoomId));
        }
        foreach (var door in doors)
        {
            AddEdge(door.RoomAId, door, door.RoomBId);
            AddEdge(door.RoomBId, door, door.RoomAId);
        }
        return adjacency;
    }

    // Every room reachable from fromRoomId by crossing doors, fromRoomId itself included - used to
    // assert a generated station has no isolated compartment (TestRunner.StationProcedural.cs).
    private static HashSet<string> ReachableRoomIds(IReadOnlyList<Door> doors, string fromRoomId)
    {
        var adjacency = BuildDoorAdjacency(doors);
        var visited = new HashSet<string> { fromRoomId };
        var queue = new Queue<string>();
        queue.Enqueue(fromRoomId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var edges))
                continue;
            foreach (var (_, otherRoomId) in edges)
                if (visited.Add(otherRoomId))
                    queue.Enqueue(otherRoomId);
        }
        return visited;
    }

    // The door centre points to walk through, in order, to get from fromRoomId to toRoomId - empty
    // if they're the same room, empty (not null - callers just get no waypoints) if unreachable.
    private static List<Vec2> FindDoorPath(IReadOnlyList<Door> doors, string fromRoomId, string toRoomId)
    {
        var waypoints = new List<Vec2>();
        if (fromRoomId == toRoomId)
            return waypoints;

        var adjacency = BuildDoorAdjacency(doors);
        var cameFrom = new Dictionary<string, (string PrevRoomId, Door Door)>();
        var visited = new HashSet<string> { fromRoomId };
        var queue = new Queue<string>();
        queue.Enqueue(fromRoomId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == toRoomId)
                break;
            if (!adjacency.TryGetValue(current, out var edges))
                continue;
            foreach (var (door, otherRoomId) in edges)
            {
                if (!visited.Add(otherRoomId))
                    continue;
                cameFrom[otherRoomId] = (current, door);
                queue.Enqueue(otherRoomId);
            }
        }

        if (!visited.Contains(toRoomId))
            return waypoints; // unreachable - TestRunner.StationProcedural.cs's own connectivity test is what should catch this

        var doors2 = new List<Door>();
        var room = toRoomId;
        while (room != fromRoomId)
        {
            var (prevRoomId, door) = cameFrom[room];
            doors2.Add(door);
            room = prevRoomId;
        }
        doors2.Reverse();
        foreach (var door in doors2)
            waypoints.Add(new Vec2(door.X, door.Y));
        return waypoints;
    }
}
