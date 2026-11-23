using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Autopilot braking on every kind of hull: it must not overshoot its destination or sail off the edge of the map, however little thrust the hull has
// in the direction it needs to brake.
internal static partial class TestRunner
{
    // (overshoot past the destination along the travel line in units, whether the autopilot arrived, fastest speed in the last 150 units)
    private static (float Overshoot, bool Arrived, float EndSpeed, float MaxSpeed) RunAutopilotTrip(World world, float tripUnits, float dirX, float dirY, bool trace = false)
    {
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }
        world.DebugPlaceShip(new Vec2(600, 600));
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);
        SitAtHelm(world, 1);

        var start = world.CreateSnapshot().ShipField;
        var from = new Vec2(start.X, start.Y);
        var target = new Vec2(from.X + tripUnits * dirX, from.Y + tripUnits * dirY);
        world.SetAutopilotDestination(target);

        var maxOvershoot = 0f;
        var maxSpeed = 0f;
        for (var i = 0; i < 240 * 30 && world.IsAutopilotActive; i++)
        {
            world.Step(RealtimeStep);
            var field = world.CreateSnapshot().ShipField;
            maxOvershoot = MathF.Max(maxOvershoot, (float)((field.X - target.X) * dirX + (field.Y - target.Y) * dirY));
            maxSpeed = MathF.Max(maxSpeed, MathF.Sqrt(field.VelocityX * field.VelocityX + field.VelocityY * field.VelocityY));
            if (trace && i % 30 == 0 && i < 900)
                Console.WriteLine($"  t={i / 30f,5:0.0}s x={(float)(field.X - from.X),7:0.0} v=({field.VelocityX:0.0},{field.VelocityY:0.0}) rot={field.RotationDegrees:0}");
        }
        StepFor(world, 90);
        var end = world.CreateSnapshot().ShipField;
        return (maxOvershoot, !world.IsAutopilotActive, MathF.Sqrt(end.VelocityX * end.VelocityX + end.VelocityY * end.VelocityY), maxSpeed);
    }

    private static bool World_Autopilot_EveryHullStopsAtItsDestination()
    {
        var fine = true;
        foreach (var (name, make) in new (string, Func<World>)[]
        {
            ("default", () => new World()),
            ("cosmoteer1", () => new World(ShipKind.Custom, ShipBuiltInFleet.Cosmoteer1)),
            ("working", () => new World(ShipKind.Custom, ShipBuiltInFleet.WorkingShip)),
            ("simple", () => new World(ShipKind.Custom, BuildSimpleCustomShipDefinition())),
            ("engine", () => new World(ShipKind.Custom, BuildEngineCustomShipDefinition())),
            ("symmetric", () => new World(ShipKind.Custom, BuildSymmetricEngineCustomShipDefinition())),
            ("double", () => new World(ShipKind.Custom, BuildDoubleEngineCustomShipDefinition())),
            ("northEngine", () => new World(ShipKind.Custom, BuildNorthFacingEngineCustomShipDefinition())),
        })
        {
            foreach (var (dx, dy) in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
            {
                var trip = 500f;
                var world = make();
                world.SpawnCharacter(1);
                var (overshoot, arrived, endSpeed, maxSpeed) = RunAutopilotTrip(world, trip, dx, dy);
                if (!(overshoot < 80f && arrived && endSpeed < 1.5f))
                    Console.WriteLine($"  {name} dir=({dx},{dy}): overshoot={overshoot:0.0} arrived={arrived} endSpeed={endSpeed:0.00}");
                fine &= overshoot < 80f && arrived && endSpeed < 1.5f;
            }
        }
        return fine;
    }
}
