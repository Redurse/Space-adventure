namespace Anabiosis.Shared.Model;

// A* over a TileGrid - the first pathfinding in the codebase (station residents use it to walk around;
// anything else that needs to get from A to B on tiles can too). Works on tile centres with 8-way moves,
// never cutting a corner (a diagonal step needs both orthogonal neighbours passable), so a walker that
// follows the returned tiles centre to centre never clips a wall.
//
// "Passable" is deliberately the walker's view, not the player's: a door tile always counts (people open
// doors for themselves), a solid wall, a device or missing floor never does, and `forbidden` lets the
// caller fence off places it must never enter (a station's connector to the player's ship).
public static class TilePathfinder
{
    private static readonly (int Dx, int Dy)[] Steps =
    {
        (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
    };

    public static TileCoord TileAt(Vec2 position) =>
        new((int)MathF.Floor((float)position.X), (int)MathF.Floor((float)position.Y));

    public static Vec2 CenterOf(TileCoord coord) => new(coord.X + 0.5f, coord.Y + 0.5f);

    public static bool IsPassable(TileGrid grid, TileCoord coord, ISet<TileCoord>? forbidden = null)
    {
        if (forbidden is not null && forbidden.Contains(coord))
            return false;
        if (grid.CellAt(coord) is not { HasFloor: true } cell || cell.DeviceId is not null)
            return false;
        if (cell.Wall == TileWallKind.Door)
            return true;
        return TileGrid.IsWalkable(cell, coord, CenterOf(coord));
    }

    // The tiles to walk through to get from `start` to `goal` (start excluded, goal included), or null if
    // there is no way. An empty list means "already there".
    public static List<TileCoord>? FindPath(TileGrid grid, TileCoord start, TileCoord goal,
        ISet<TileCoord>? forbidden = null, int maxNodes = 6000)
    {
        if (start == goal)
            return new List<TileCoord>();
        if (!IsPassable(grid, goal, forbidden))
            return null;

        var open = new PriorityQueue<TileCoord, float>();
        var cameFrom = new Dictionary<TileCoord, TileCoord>();
        var cost = new Dictionary<TileCoord, float> { [start] = 0f };
        open.Enqueue(start, Heuristic(start, goal));
        var expanded = 0;

        while (open.TryDequeue(out var current, out _))
        {
            if (current == goal)
                return Rebuild(cameFrom, start, goal);
            if (++expanded > maxNodes)
                return null;

            var currentCost = cost[current];
            foreach (var (dx, dy) in Steps)
            {
                var next = new TileCoord(current.X + dx, current.Y + dy);
                if (!IsPassable(grid, next, forbidden) || !CanStep(grid, current, next, forbidden))
                    continue;

                var stepCost = dx != 0 && dy != 0 ? 1.4142f : 1f;
                var newCost = currentCost + stepCost;
                if (cost.TryGetValue(next, out var known) && known <= newCost)
                    continue;
                cost[next] = newCost;
                cameFrom[next] = current;
                open.Enqueue(next, newCost + Heuristic(next, goal));
            }
        }
        return null;
    }

    // The passable tile closest to `near` (itself if it is passable), searched outward ring by ring.
    public static TileCoord? NearestPassable(TileGrid grid, TileCoord near, ISet<TileCoord>? forbidden = null, int maxRadius = 6)
    {
        for (var radius = 0; radius <= maxRadius; radius++)
        {
            TileCoord? best = null;
            var bestDistance = int.MaxValue;
            for (var dx = -radius; dx <= radius; dx++)
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                        continue;
                    var candidate = new TileCoord(near.X + dx, near.Y + dy);
                    var distance = dx * dx + dy * dy;
                    if (distance < bestDistance && IsPassable(grid, candidate, forbidden))
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }
            if (best is not null)
                return best;
        }
        return null;
    }

    // An orthogonal step must not cross a closed door edge; a diagonal one additionally needs both of the
    // tiles it would brush past, so a walker never squeezes through a wall corner.
    private static bool CanStep(TileGrid grid, TileCoord from, TileCoord to, ISet<TileCoord>? forbidden)
    {
        if (from.X == to.X || from.Y == to.Y)
            return grid.IsWalkableAcrossEdge(from, to);
        var viaX = new TileCoord(to.X, from.Y);
        var viaY = new TileCoord(from.X, to.Y);
        return IsPassable(grid, viaX, forbidden) && IsPassable(grid, viaY, forbidden)
            && grid.IsWalkableAcrossEdge(from, viaX) && grid.IsWalkableAcrossEdge(viaX, to)
            && grid.IsWalkableAcrossEdge(from, viaY) && grid.IsWalkableAcrossEdge(viaY, to);
    }

    // Octile distance - exact for 8-way movement on an open grid, so the search stays tight.
    private static float Heuristic(TileCoord a, TileCoord b)
    {
        var dx = Math.Abs(a.X - b.X);
        var dy = Math.Abs(a.Y - b.Y);
        return Math.Max(dx, dy) + 0.4142f * Math.Min(dx, dy);
    }

    private static List<TileCoord> Rebuild(Dictionary<TileCoord, TileCoord> cameFrom, TileCoord start, TileCoord goal)
    {
        var path = new List<TileCoord>();
        for (var at = goal; at != start; at = cameFrom[at])
            path.Add(at);
        path.Reverse();
        return path;
    }
}
