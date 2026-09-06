using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // M48 - hull cameras became real devices bolted to the plating (Ship.Cameras/HullCameraMount)
    // instead of a purely client-side 4-direction toggle. Every hand-authored hull needs at least
    // one working mount, or ExternalCameraPanel would just show an empty grid on that class.
    private static bool World_Cameras_EveryHandAuthoredHullHasWorkingCameraGeometry()
    {
        foreach (var kind in new[] { ShipKind.Scout, ShipKind.Frigate, ShipKind.Cruiser, ShipKind.Corvette })
        {
            var ship = Ship.Create(kind);
            if (ship.Cameras.Count == 0)
                return false;

            var seenIds = new HashSet<string>();
            foreach (var camera in ship.Cameras)
            {
                if (!seenIds.Add(camera.Id))
                    return false; // duplicate id on this hull

                // Just needs to resolve to a real point on this hull's own room bounds without
                // throwing - HullCameraMount.For looks up the camera's room by its interior
                // position, the same way TurretMount.For already does for turrets.
                var mount = HullCameraMount.For(ship.Rooms, ship.Cameras, camera);
                var minX = ship.Rooms.Min(r => r.Left) - 1f;
                var maxX = ship.Rooms.Max(r => r.Right) + 1f;
                if (mount.Position.X < minX || mount.Position.X > maxX)
                    return false;
            }
        }
        return true;
    }

    // A fresh ship starts with every camera's own drop/trunk wire intact, same as every other
    // device (WireGraphFactory.CreateDefaultForHull) - and the static layout plus the per-camera
    // Damaged flag both ride along in the snapshot the client actually reads
    // (ExternalCameraPanel.DrawGrid/DrawOneCamera).
    private static bool World_Cameras_StartConnectedAndAppearInSnapshot()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var snapshot = world.CreateSnapshot();

        if (snapshot.Cameras.Count == 0)
            return false;

        return snapshot.Cameras.All(c =>
            world.IsDeviceConnected(c.Id) &&
            snapshot.SystemStates.FirstOrDefault(s => s.DeviceId == c.Id) is { Damaged: false });
    }

    // The actual bug independent per-device wiring exists to fix (World_Wiring_
    // RepairingOneMultiDeviceUnit_DoesNotRepairItsSibling covers the same thing for engines/
    // shields): two cameras sharing the Secondary channel must never share a junction/trunk, so
    // cutting one's wire can never take its sibling down with it.
    private static bool World_Cameras_CuttingOneCameraWireLeavesItsSiblingConnected()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var cameras = world.CreateSnapshot().Cameras;
        if (cameras.Count < 2)
            return false; // every hand-authored hull carries at least 2 - see the geometry test above

        world.CutWire($"trunk-{cameras[0].Id}");
        return !world.IsDeviceConnected(cameras[0].Id) && world.IsDeviceConnected(cameras[1].Id);
    }

    // A camera is wired into the exact same Component/Wire graph as every other device, so it gets
    // the exact same F-key wrench/screwdriver repair minigame (World.Interact.cs's own
    // nearbyDamagedCamera check, World.SystemRepair.cs) - not a parallel mechanic of its own.
    private static bool World_Cameras_RepairingWithWrenchReconnectsIt()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var camera = world.CreateSnapshot().Cameras[0];

        world.CutWire($"trunk-{camera.Id}");
        if (world.IsDeviceConnected(camera.Id))
            return false;

        var wrenchSlot = TakeFromRack(world, ItemType.Wrench);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: wrenchSlot));
        WalkAcrossShipTo(world, camera.X, camera.Y);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // starts the repair

        // World.SystemRepair.cs's own real 12-hour elapsed-time timer - see
        // World_RepairSystem_RequiresWrenchHeldInHand's own comment on DebugFastForwardAllRepairs.
        world.DebugFastForwardAllRepairs(13.0 * 3600.0);
        world.Step(RealtimeStep);

        return world.IsDeviceConnected(camera.Id);
    }
}
