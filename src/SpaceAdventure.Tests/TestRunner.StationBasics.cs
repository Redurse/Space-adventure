using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // Arriving at a station (game_design.md section 10 - walkable stations, manual docking) no
    // longer teleports straight into VoyagePhase.Station - it drops into StationApproach first
    // (World.StationDocking.cs), same as the M15 asteroid-field arrival pattern.
    private static bool World_Station_ArrivingSetsStationApproachNotInstantDock()
    {
        var world = new World();
        world.SpawnCharacter(1); // starts already docked at home-station - travel elsewhere first
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.StationApproach; i++)
            world.Step(RealtimeStep);

        return world.Phase == VoyagePhase.StationApproach;
    }

    private static bool World_Station_DockAtStation_ReachesStationPhase()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        DockAtStation(world);

        return world.Phase == VoyagePhase.Station && world.CreateSnapshot().Voyage.DockedPointId == "outpost-gamma";
    }

    // Walking through the ship's own outer airlock door while actually docked leads onto the
    // station instead of into vacuum (World.StationDocking.cs's TryCrossIntoStation) - no suit
    // needed, unlike the EVA case, since it's a sealed connector.
    private static bool World_Station_WalkThroughOpenOuterDoor_EntersStation()
    {
        var world = new World();
        world.SpawnCharacter(1); // starts already docked at home-station
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return me.OnStation && !me.IsOutside;
    }

    private static bool World_Station_WalkBackThroughConnector_ReturnsToShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);

        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false; // didn't make it onto the station as expected

        WalkFixedDirection(world, 1, -1f, 0f);
        return !world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation;
    }

    // Same open outer door, but the ship isn't docked (mid-battle): the connector-to-station
    // special case (TryCrossIntoVacuum's own connectorId, only set while Phase == Station) plays
    // no part here, so this can never land the character on a station it isn't anywhere near -
    // it falls through to an ordinary vacuum exit instead, exactly like the same door already
    // does out in the asteroid field (World.Eva.cs). This used to be blocked outright on the
    // grounds that "not docked" meant "nowhere to go" - that stopped being true the moment M31-
    // M33 made every voyage phase a real, physically-simulated field with the ship's own hull
    // right there to walk out onto.
    private static bool World_Station_OuterDoorWhileNotDocked_EntersVacuumNotStation()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        EquipSuit(world, 1); // suit+tank, so this isolates the docked/not-docked question alone
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !me.OnStation && me.IsOutside;
    }

    // Shared boarding setup (game_design.md Phase 3): start a battle, arm the character with a
}
