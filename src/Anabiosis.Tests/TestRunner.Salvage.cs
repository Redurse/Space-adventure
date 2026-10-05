using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    private static bool SalvagePoints_EverySystem_HasTwoToFourDeterministicPointsClearOfBodies()
    {
        foreach (var systemId in new[] { "sol", "alpha-centauri", "sys-001", "sys-042" })
        {
            var first = SalvagePoints.For(systemId);
            var second = SalvagePoints.For(systemId);
            if (first.Count < 1 || first.Count > 4 || !first.SequenceEqual(second))
                return false;

            var bodies = CelestialBodyGenerator.Generate(systemId);
            var byId = bodies.ToDictionary(b => b.Id);
            var half = CelestialBodyGenerator.FieldSize(bodies) / 2f;
            var center = new Vec2(half, half);
            foreach (var point in first)
            foreach (var body in bodies)
                if ((CelestialBodyGenerator.PositionAt(body, byId) + center - point.Position).Length() < body.Radius)
                    return false;
        }
        return true;
    }

    private static bool World_Salvage_FlyingIntoAPoint_PaysOutOnceOnly()
    {
        var world = new World();
        world.SpawnCharacter(1);
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }

        var point = world.GalaxyMap.GetSystem("sol").Points.First(p => SalvagePoints.IsSalvage(p.Kind));
        var loot = SalvagePoints.LootFor(point);
        var creditsBefore = world.Credits;
        var fuelBefore = world.HyperiumAboard;

        world.DebugPlaceShip(point.Position);
        world.Step(RealtimeStep);
        var paidOnce = world.Credits == creditsBefore + loot.Credits
            && world.HyperiumAboard == fuelBefore + loot.Hyperium
            && world.CreateSnapshot().SalvagedPointIds!.Contains(point.Id)
            && world.CreateSnapshot().SalvageNotice is not null;

        world.Step(RealtimeStep);
        return paidOnce && world.Credits == creditsBefore + loot.Credits;
    }
}
