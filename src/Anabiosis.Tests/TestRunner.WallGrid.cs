using Anabiosis.Client.Rendering;
using Microsoft.Xna.Framework;

internal static partial class TestRunner
{
    private static List<WallSegment> WallGridSampleWalls() => new()
    {
        new WallSegment(0, 0, 10, 0), new WallSegment(10, 0, 10, 10), new WallSegment(10, 10, 0, 10),
        new WallSegment(0, 10, 0, 0), new WallSegment(40, 40, 60, 40), new WallSegment(5, 3, 5, 7),
    };

    private static bool WallGrid_LazyReset_GivesTheSameNearbyWallsAsAnEagerRebuild()
    {
        var walls = WallGridSampleWalls();
        var eager = new WallGrid();
        eager.Rebuild(walls);
        var lazy = new WallGrid();
        lazy.Reset(walls);

        var fromEager = new List<WallSegment>();
        var fromLazy = new List<WallSegment>();
        foreach (var (point, radius) in new[] { (new Vector2(5, 5), 8f), (new Vector2(50, 40), 6f), (new Vector2(200, 200), 5f) })
        {
            eager.QueryNearby(fromEager, point, radius);
            lazy.QueryNearby(fromLazy, point, radius);
            if (!fromEager.OrderBy(w => w.Ax).ThenBy(w => w.Ay).SequenceEqual(fromLazy.OrderBy(w => w.Ax).ThenBy(w => w.Ay)))
                return false;
        }
        return true;
    }

    private static bool WallGrid_ResetWithAChangedWallList_QueriesTheNewWalls()
    {
        var grid = new WallGrid();
        grid.Reset(WallGridSampleWalls());
        var nearby = new List<WallSegment>();
        grid.QueryNearby(nearby, new Vector2(5, 5), 8f);
        var before = nearby.Count;

        grid.Reset(new List<WallSegment> { new(100, 100, 110, 100) });
        grid.QueryNearby(nearby, new Vector2(5, 5), 8f);
        return before > 0 && nearby.Count == 0;
    }
}
