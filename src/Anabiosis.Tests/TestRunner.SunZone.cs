using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    private static bool CelestialBodyGenerator_SunZoneIntensity_IsOneOnDiscZeroOutsideAndLinearBetween()
    {
        var star = new CelestialBody("s", null, 0f, 0f, 200f, BodyMassTier.Star);
        var zone = CelestialBodyGenerator.SunZoneRadius(star);
        var mid = (200f + zone) / 2f;
        return CelestialBodyGenerator.SunZoneIntensity(star, 0f) == 1f
            && CelestialBodyGenerator.SunZoneIntensity(star, 200f) == 1f
            && CelestialBodyGenerator.SunZoneIntensity(star, zone) == 0f
            && CelestialBodyGenerator.SunZoneIntensity(star, zone + 50f) == 0f
            && Math.Abs(CelestialBodyGenerator.SunZoneIntensity(star, mid) - 0.5f) < 0.001f;
    }

    private static float SolStarDistanceToTotalRoomHp(World world, double distanceFromStar)
    {
        var sol = world.GalaxyMap.GetSystem("sol");
        world.DebugPlaceShip(sol.Field.Center + new Vec2(1f, 0f) * distanceFromStar);
        world.Step(RealtimeStep);
        return world.CreateSnapshot().RoomHp!.Sum(r => r.Hp);
    }

    private static World UndockedSolWorld()
    {
        var world = new World();
        world.SpawnCharacter(1);
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }
        return world;
    }

    private static bool World_SunZone_ShipInsideRing_LosesRoomHp()
    {
        var world = UndockedSolWorld();
        var star = world.GalaxyMap.GetSystem("sol").Bodies.First(b => b.ParentId is null);
        var before = SolStarDistanceToTotalRoomHp(world, star.Radius * 3f);
        var after = SolStarDistanceToTotalRoomHp(world, star.Radius * 1.1f);
        for (var i = 0; i < 20; i++)
            world.Step(RealtimeStep);
        var hot = world.CreateSnapshot().RoomHp!.Sum(r => r.Hp);
        return after <= before && hot < before;
    }

    private static bool World_SunZone_ShipOutsideRing_KeepsRoomHp()
    {
        var world = UndockedSolWorld();
        var star = world.GalaxyMap.GetSystem("sol").Bodies.First(b => b.ParentId is null);
        var before = SolStarDistanceToTotalRoomHp(world, star.Radius * 3f);
        for (var i = 0; i < 20; i++)
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().RoomHp!.Sum(r => r.Hp);
        return Math.Abs(after - before) < 0.001f && world.SunZoneIntensityNow == 0f;
    }
}
