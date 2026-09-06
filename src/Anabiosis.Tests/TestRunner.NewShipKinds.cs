using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // M85 follow-up (humble-soaring-cat.md) - ShipKind.Destroyer/Freighter, two additional starter
    // hulls built from the M80 compartment catalog (see Ship.CatalogHulls.cs's own doc comment for
    // why they're assembled directly from known compartment geometry rather than routed through
    // TileShipBuilder.BuildDefinition's tile-inference pipeline). These tests are the concrete proof
    // both hulls are valid, fully connected, and actually playable - not just "doesn't throw."

    private static bool Ship_Destroyer_BuildsSuccessfully_AndPassesValidation()
    {
        var ship = Ship.Create(ShipKind.Destroyer);
        return ShipHasExpectedCatalogHullShape(ship);
    }

    private static bool Ship_Freighter_BuildsSuccessfully_AndPassesValidation()
    {
        var ship = Ship.Create(ShipKind.Freighter);
        return ShipHasExpectedCatalogHullShape(ship);
    }

    // Shared shape check for both new hulls: 9 compartments, exactly one real marching engine (the
    // spine's own engine-medium), and at least one of every device kind CustomShipValidator requires
    // plus Distribution (which the validator also requires but this doubles as a sanity check that
    // Ship.Devices' own aggregation - Ship.cs's BuildDevices - actually surfaces every kind).
    private static bool ShipHasExpectedCatalogHullShape(Ship ship)
    {
        if (ship.Rooms.Count != 9)
            return false;
        if (ship.Engines.Count != 1)
            return false;

        bool Has(DeviceKind kind) => ship.Devices.Count(d => d.Kind == kind) >= 1;
        return Has(DeviceKind.Reactor) && Has(DeviceKind.Distribution) && Has(DeviceKind.Helm)
            && Has(DeviceKind.Navigation) && Has(DeviceKind.Oxygen) && Has(DeviceKind.SuitLocker)
            && Has(DeviceKind.StorageRack);
    }

    // The concrete proof the 9-compartment layout is actually fully connected, not just individually
    // valid - RoomGraphConnectivity.AllReachable is the same connectivity utility M61's own room-
    // demolition/M65's enemy-generator checks already rely on (RoomGraphConnectivity.cs).
    private static bool World_Destroyer_AllRoomsReachableFromSpawn()
    {
        var ship = Ship.Create(ShipKind.Destroyer);
        var def = ship.ToDefinition();
        return RoomGraphConnectivity.AllReachable(def.Rooms, def.Doors, ship.SpawnRoomId);
    }

    private static bool World_Freighter_AllRoomsReachableFromSpawn()
    {
        var ship = Ship.Create(ShipKind.Freighter);
        var def = ship.ToDefinition();
        return RoomGraphConnectivity.AllReachable(def.Rooms, def.Doors, ship.SpawnRoomId);
    }

    // Same level of proof the other ShipKind smoke-tests already use (World_ShipKindScout_
    // SpawnsAndSteps/World_ShipKindCruiser_SpawnsAndSteps, TestRunner.ShipHulls.cs) - a real hosted
    // World actually starts and steps cleanly, with a spawned character able to move.
    private static bool World_Destroyer_CanActuallyStartAHostedWorld()
    {
        var world = new World(ShipKind.Destroyer);
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);

        var character = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return character.X > world.Ship.SpawnPoint.X;
    }

    private static bool World_Freighter_CanActuallyStartAHostedWorld()
    {
        var world = new World(ShipKind.Freighter);
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);

        var character = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return character.X > world.Ship.SpawnPoint.X;
    }
}
