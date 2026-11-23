using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;
using Anabiosis.Shared.War;

// The galaxy of four thousand stars: the war's graph and paths over all of it, and the star list not being rebuilt for the wire every tick.
internal static partial class TestRunner
{
    // The routes the war uses (computed row by row on demand, not tabulated for every pair) are real: each step follows a lane, the
    // length adds up, and the answers do not depend on which end is asked about.
    private static bool WarGalaxy_RoutesOverAllFourThousandStars_AreConsistent()
    {
        var map = GalaxyMap.CreateStarter(4242);
        map.EnsureAtLeast(GalaxyMap.MaxProceduralSystems);
        if (map.Systems.Count != GalaxyMap.TotalSystemCount)
            return false;
        var galaxy = WarGalaxy.FromGalaxyMap(map);
        if (galaxy.Count != GalaxyMap.TotalSystemCount)
            return false;

        var random = new Random(5);
        for (var i = 0; i < 60; i++)
        {
            var a = random.Next(galaxy.Count);
            var b = random.Next(galaxy.Count);
            if (a == b)
                continue;
            var path = galaxy.Path(a, b);
            if (path.Count == 0 || path[^1] != b || path.Contains(a))
                return false;
            var length = 0f;
            var at = a;
            foreach (var step in path)
            {
                length += galaxy.LaneLength(at, step); // throws if two systems on the route are not joined
                at = step;
            }
            if (MathF.Abs(length - galaxy.PathLength(a, b)) > 0.01f * MathF.Max(1f, length)
                || MathF.Abs(galaxy.PathLength(a, b) - galaxy.PathLength(b, a)) > 0.01f
                || galaxy.Hops(a, b) != galaxy.Hops(b, a)
                || galaxy.Hops(a, b) > path.Count
                || galaxy.HopsFromAny(new[] { a })[b] != galaxy.Hops(a, b))
                return false;
        }

        // Several sources at once: the nearest of them, in lanes.
        var sources = new[] { 0, 1500, 3000 };
        var nearest = galaxy.HopsFromAny(sources);
        for (var i = 0; i < 30; i++)
        {
            var target = random.Next(galaxy.Count);
            if (nearest[target] != sources.Min(s => galaxy.Hops(s, target)))
                return false;
        }
        return true;
    }

    // The galaxy's star list is half a megabyte of JSON: it must not be serialised again every tick just to find out it is the same.
    private static bool Wire_DeltaFrames_DoNotResendTheStarListWhileItIsUnchanged()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var encoder = new SnapshotDeltaEncoder();
        var decoder = new SnapshotDeltaDecoder();

        world.Step(RealtimeStep);
        var first = RoundTripDelta(encoder, decoder, world.CreateSnapshot(), out var firstBytes);
        world.Step(RealtimeStep);
        var second = RoundTripDelta(encoder, decoder, world.CreateSnapshot(), out var secondBytes);
        return secondBytes < firstBytes / 2
            && second.Snapshot!.StarSystems.Count == first.Snapshot!.StarSystems.Count
            && second.Unchanged!.Contains("StarSystems");
    }
}
