using Anabiosis.Client;
using Anabiosis.Server;
using Anabiosis.Shared.Model;

// What the client draws between two server snapshots: things that move are carried part of the way to where they are going, and a relocation is not
// mistaken for travel.
internal static partial class TestRunner
{
    private static bool Client_SnapshotSmoother_CarriesTheShipPartwayIntoTheNextTick()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new Anabiosis.Shared.Protocol.ClientCommand(1, DockPressed: true)); // cast off - a docked ship does not move
        world.Step(RealtimeStep);
        world.DebugPlaceShip(new Vec2(3000, 3000));
        world.DebugSetShipVelocity(new Vec2(60, 0));
        world.Step(RealtimeStep);
        var first = world.CreateSnapshot();
        world.Step(RealtimeStep);
        var second = world.CreateSnapshot();

        var smoother = new SnapshotSmoother();
        smoother.Accept(first, 0.0);
        smoother.Accept(second, 1.0 / 30);
        var atArrival = smoother.Advance(1.0 / 30)!.ShipField.X;
        var halfWay = smoother.Advance(1.0 / 30 + 1.0 / 60)!.ShipField.X;
        var step = second.ShipField.X - first.ShipField.X;
        return Math.Abs(atArrival - second.ShipField.X) < 1e-6
            && Math.Abs(halfWay - (second.ShipField.X + step / 2)) < step * 0.2
            && smoother.Advance(1.0 / 30 + 5.0)!.ShipField.X <= second.ShipField.X + step * 1.6; // a stalled server is not followed for ever
    }

    private static bool Client_SnapshotSmoother_ShowsARelocationAsItIs()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugPlaceShip(new Vec2(3000, 3000));
        world.Step(RealtimeStep);
        var first = world.CreateSnapshot();
        world.DebugPlaceShip(new Vec2(6000, 4000));
        world.Step(RealtimeStep);
        var second = world.CreateSnapshot();

        var smoother = new SnapshotSmoother();
        smoother.Accept(first, 0.0);
        smoother.Accept(second, 1.0 / 30);
        var shown = smoother.Advance(1.0 / 30 + 1.0 / 60)!;
        return Math.Abs(shown.ShipField.X - second.ShipField.X) < 1e-6 && Math.Abs(shown.ShipField.Y - second.ShipField.Y) < 1e-6;
    }
}
