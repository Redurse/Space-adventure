using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// W3 of wiring-plan.md - the fusion reactor: start/stop button, smooth spin-up/down, auto mode
// following the grid's load, fuel burnt for what it actually gives.
internal static partial class TestRunner
{
    private static Reactor NewTestReactor() => new(maxOutput: 80f, maxFuel: 400f, fuelPerPowerUnitPerSecond: 0.01f);

    // Holds the request steady for `seconds`, in realtime-sized steps.
    private static void RegulateFor(Reactor reactor, float requested, float seconds)
    {
        for (var t = 0f; t < seconds; t += 0.05f)
            reactor.Regulate(0.05, requested);
    }

    private static bool Reactor_Stopped_SpinsDownGraduallyToZero()
    {
        var reactor = NewTestReactor();
        RegulateFor(reactor, 80f, 1f);
        reactor.Running = false;
        RegulateFor(reactor, 80f, 1f);
        var midway = reactor.CurrentOutput;
        RegulateFor(reactor, 80f, Reactor.RampDownSeconds + 1f);
        return midway > 50f && midway < 70f && reactor.CurrentOutput == 0f;
    }

    private static bool Reactor_Started_RampsUpOverSeconds()
    {
        var reactor = NewTestReactor();
        reactor.Running = false;
        RegulateFor(reactor, 80f, Reactor.RampDownSeconds + 1f);
        reactor.Running = true;
        RegulateFor(reactor, 80f, 1f);
        var afterOneSecond = reactor.CurrentOutput;
        RegulateFor(reactor, 80f, Reactor.RampUpSeconds);
        return afterOneSecond > 10f && afterOneSecond < 20f && Near(reactor.CurrentOutput, 80f);
    }

    // Auto mode: the output settles on the load, not on the maximum, and a sudden extra load is
    // a short climb rather than an instant jump.
    private static bool Reactor_AutoMode_FollowsTheLoadWithALag()
    {
        var reactor = NewTestReactor();
        RegulateFor(reactor, 20f, 5f);
        var settled = reactor.CurrentOutput;
        RegulateFor(reactor, 60f, 0.5f);
        var catchingUp = reactor.CurrentOutput;
        RegulateFor(reactor, 60f, 5f);
        return Near(settled, 20f) && catchingUp > 20f && catchingUp < 60f && Near(reactor.CurrentOutput, 60f);
    }

    private static bool Reactor_EmergencyShutdown_CollapsesThePlasmaAtOnce()
    {
        var reactor = NewTestReactor();
        RegulateFor(reactor, 80f, 1f);
        reactor.EmergencyShutdown = true;
        reactor.Regulate(0.05, 80f);
        return reactor.CurrentOutput == 0f;
    }

    private static bool Reactor_FuelBurn_IsProportionalToWhatItGives()
    {
        var light = NewTestReactor();
        var heavy = NewTestReactor();
        light.Step(10.0, 20f);
        heavy.Step(10.0, 40f);
        var lightBurn = light.MaxFuel - light.Fuel;
        var heavyBurn = heavy.MaxFuel - heavy.Fuel;
        return lightBurn > 0f && Near(heavyBurn, lightBurn * 2f);
    }

    private static bool Reactor_PlasmaTemperature_HotWhileLitColdWhenStopped()
    {
        var reactor = NewTestReactor();
        RegulateFor(reactor, 60f, 5f);
        var hot = reactor.PlasmaTemperature;
        reactor.Running = false;
        RegulateFor(reactor, 60f, 15f);
        return hot > 50f && reactor.PlasmaTemperature < 1f;
    }

    // In the ship: a stopped reactor leaves the grid on its battery, which drains; the snapshot
    // carries what the panel shows (load, delivered, the button's state).
    private static bool World_ReactorStopped_ShipRunsOnBatteryUntilRestarted()
    {
        var world = new World(ShipKind.Custom);
        world.StartCampaign();
        world.Step(RealtimeStep);
        var chargeBefore = world.PowerGrid.Battery.Charge;
        world.PowerGrid.Reactor.Running = false;
        for (var i = 0; i < 200; i++)
            world.Step(RealtimeStep);
        var stopped = world.CreateSnapshot().Power;
        var drained = world.PowerGrid.Battery.Charge < chargeBefore;

        world.PowerGrid.Reactor.Running = true;
        for (var i = 0; i < 400; i++)
            world.Step(RealtimeStep);
        var restarted = world.CreateSnapshot().Power;

        return !stopped.ReactorRunning && stopped.ReactorOutput == 0f && stopped.ReactorDelivered == 0f && drained
            && restarted.ReactorRunning && restarted.ReactorLoad > 0f && restarted.ReactorDelivered > 0f
            && world.GetEffectivePower(PowerSystemId.Secondary) >= 12f - 0.01f;
    }

    private static bool World_ToggleReactorRunning_RequiresProximityToReactor()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var far = (world.Ship.ReactorBlock.Position - new Vec2(me.X, me.Y)).Length() > 3f;
        world.ApplyCommand(1, new ClientCommand(1, ToggleReactorRunningPressed: true));
        return far && world.PowerGrid.Reactor.Running;
    }
}
