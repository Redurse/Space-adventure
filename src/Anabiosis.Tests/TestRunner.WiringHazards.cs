using System.Linq;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// W9 of wiring-plan.md: a junction box asked for too much burns out, and a live wire bites whoever takes it off.
internal static partial class TestRunner
{
    private static float PlayerHealth(World world) => world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;

    private static bool World_WireRemove_LiveWireShocksAndDeadOneDoesNot()
    {
        var live = new World();
        live.SpawnCharacter(1);
        GiveAndHold(live, ItemType.Screwdriver);
        MoveToOxygenJunction(live);
        live.Step(RealtimeStep);
        var before = PlayerHealth(live);
        live.ApplyCommand(1, new ClientCommand(1, RemoveWireId: "trunk-oxygen"));
        live.Step(RealtimeStep);
        var shocked = PlayerHealth(live) < before;

        var dead = new World();
        dead.SpawnCharacter(1);
        GiveAndHold(dead, ItemType.Screwdriver);
        MoveToOxygenJunction(dead);
        dead.CutWire("trunk-oxygen"); // already burnt through: no current
        dead.Step(RealtimeStep);
        var healthyBefore = PlayerHealth(dead);
        dead.ApplyCommand(1, new ClientCommand(1, RemoveWireId: "trunk-oxygen"));
        dead.Step(RealtimeStep);
        return shocked && PlayerHealth(dead) >= healthyBefore && dead.Wires.All(w => w.Id != "trunk-oxygen");
    }

    private static World OverloadedOxygenBoxWorld(out string boxId)
    {
        var world = new World();
        var oxygen = OxygenDeviceId(world);
        boxId = world.Wires.First(w => w.ToPin.ComponentId == oxygen).FromPin.ComponentId;
        // Most of the ship's machines hung on the one box (each system's draw is split between its devices, so it takes several to pass 40).
        var others = world.Ship.SystemDevices.Where(d => d.System != PowerSystemId.Oxygen).ToList();
        for (var i = 0; i < others.Count; i++)
            world.AddWire($"overload-{i}", new PinRef(boxId, "out-0"), new PinRef(others[i].Id, "in"));
        return world;
    }

    private static bool World_Junction_CarryingTooMuchHeatsUpThenBurnsOut()
    {
        var world = OverloadedOxygenBoxWorld(out var box);
        if (world.Ship.SystemDevices.Count < 4)
            return false; // the hull has to have enough machines to overload one box
        StepFor(world, 90); // a second and a half
        var warming = world.DebugJunctionHeat(box) > 0.25f && !world.IsJunctionDamaged(box)
            && world.CreateSnapshot().Wiring.OverheatingJunctions?.Contains(box) == true;
        StepFor(world, 240); // well past the limit
        return warming && world.IsJunctionDamaged(box);
    }

    private static bool World_Junction_WithOneDeviceNeverOverheats()
    {
        var world = new World();
        StepFor(world, 600);
        return world.CreateSnapshot().Wiring.OverheatingJunctions is null or { Count: 0 }
            && world.DebugAllJunctionsIntact();
    }

    // W11, the multiplayer half: part settings, the numbers on value pins and a box heating up all reach another client exactly, frame
    // after frame, through the socket's delta encoding.
    private static bool Wire_DeltaFrames_CarryWiringPartsSettingsAndOverheating()
    {
        var world = OverloadedOxygenBoxWorld(out var box);
        world.SpawnCharacter(1);
        world.AddComponent(new Component("math", ComponentKind.Math, "corridor", 0, 0, Value: 2f));
        world.AddComponent(new Component("osc", ComponentKind.Oscillator, "corridor", 0, 0, TimerSeconds: 1f));
        world.AddComponent(new Component("wl", ComponentKind.Wireless, "corridor", 0, 0, Value: 3f));
        world.AddWire("osc-math", new PinRef("osc", "out"), new PinRef("math", "in-a"));
        var encoder = new Anabiosis.Shared.Networking.SnapshotDeltaEncoder();
        var decoder = new Anabiosis.Shared.Networking.SnapshotDeltaDecoder();

        var sawHeat = false;
        for (var i = 0; i < 200; i++)
        {
            if (i == 40)
                world.ChangeComponentSetting("math", ComponentSettingAction.ModeNext);
            if (i == 80)
                world.ChangeComponentSetting("wl", ComponentSettingAction.ValueUp);
            world.Step(RealtimeStep);
            var snapshot = world.CreateSnapshot();
            sawHeat |= snapshot.Wiring.OverheatingJunctions?.Contains(box) == true;
            var message = RoundTripDelta(encoder, decoder, snapshot, out _);
            if (!Anabiosis.Shared.Networking.Wire.Serialize(snapshot).AsSpan().SequenceEqual(Anabiosis.Shared.Networking.Wire.Serialize(message.Snapshot!)))
                return false;
        }
        return sawHeat;
    }

    private static bool World_Junction_CoolsDownOnceTheLoadGoes()
    {
        var world = OverloadedOxygenBoxWorld(out var box);
        StepFor(world, 90);
        var hot = world.DebugJunctionHeat(box);
        foreach (var wire in world.Wires.Where(w => w.Id.StartsWith("overload-")).Select(w => w.Id).ToList())
            world.DebugRemoveWire(wire);
        StepFor(world, 240);
        return hot > 0.25f && world.DebugJunctionHeat(box) == 0f && !world.IsJunctionDamaged(box);
    }
}
