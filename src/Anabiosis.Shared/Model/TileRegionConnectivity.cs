namespace Anabiosis.Shared.Model;

// M77 (humble-soaring-cat.md) - region-level reachability over a live TileGrid, walking door-kind
// boundary cells as edges - the tile-native equivalent of RoomGraphConnectivity, but built fresh
// rather than extending that one, since RoomGraphConnectivity operates on CustomRoomDef/
// CustomDoorDef DTOs (ship-authoring time), not on a live TileGrid (gameplay time).
//
// A region never merges with its neighbor through a door (open or closed, intact or breached - a
// breached door tile already stops being "blocking" and TileGrid's own incremental logic merges the
// two sides into one region on its own, see TileGrid.IsBlockingForRegion) - TileGrid's own mutators
// already enforce this for SealedRegion membership. But for REACHABILITY (can a person/atmosphere/
// structural connection actually get from region A to region B) an intact, still-sealed door still
// counts as a live connection - only a genuinely MISSING wall/door, or a destroyed/removed region,
// severs it. SealedRegion itself carries no adjacency list (by design - see TileGrid.cs's own doc
// comment), so this builds one fresh from the door tiles each call, exactly the same "cheap enough,
// a hull has tens of doors, not thousands" reasoning RoomGraphConnectivity.ReachableFrom already
// relies on for its own per-call adjacency build.
public static class TileRegionConnectivity
{
    // Every region reachable from `fromRegionId` by crossing zero or more still-sealed door tiles -
    // a plain reachability set, not a shortest-path one, matching RoomGraphConnectivity.ReachableFrom's
    // own shape.
    public static HashSet<int> ReachableRegionsFrom(TileGrid tiles, int fromRegionId)
    {
        var adjacency = BuildRegionAdjacency(tiles);
        var visited = new HashSet<int> { fromRegionId };
        var queue = new Queue<int>();
        queue.Enqueue(fromRegionId);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var neighbors))
                continue;
            foreach (var next in neighbors)
                if (visited.Add(next))
                    queue.Enqueue(next);
        }
        return visited;
    }

    private static Dictionary<int, HashSet<int>> BuildRegionAdjacency(TileGrid tiles)
    {
        var adjacency = new Dictionary<int, HashSet<int>>();
        void AddEdge(int a, int b)
        {
            if (a == b)
                return; // already the same (merged) region - see the class doc comment
            if (!adjacency.TryGetValue(a, out var set))
                adjacency[a] = set = new HashSet<int>();
            set.Add(b);
            if (!adjacency.TryGetValue(b, out var set2))
                adjacency[b] = set2 = new HashSet<int>();
            set2.Add(a);
        }

        foreach (var (coord, cell) in tiles.Cells)
        {
            if (cell.Wall != TileWallKind.Door)
                continue;
            // A door tile is never itself a region member (RegionIdAt returns null for it while it's
            // still sealed - see TileGrid.IsBlockingForRegion), so only its flanking neighbors matter.
            // Checking both axis pairs on every door tile is harmless: whichever pair isn't the door's
            // real orientation just reads two null/same-region neighbors and adds nothing.
            var north = tiles.RegionIdAt(new TileCoord(coord.X, coord.Y - 1));
            var south = tiles.RegionIdAt(new TileCoord(coord.X, coord.Y + 1));
            var east = tiles.RegionIdAt(new TileCoord(coord.X + 1, coord.Y));
            var west = tiles.RegionIdAt(new TileCoord(coord.X - 1, coord.Y));
            if (north is { } n && south is { } s && n != s)
                AddEdge(n, s);
            if (east is { } e && west is { } w && e != w)
                AddEdge(e, w);
        }
        return adjacency;
    }
}
