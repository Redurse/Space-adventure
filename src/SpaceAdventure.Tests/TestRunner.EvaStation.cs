using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // The station's own plating is exactly as solid to a drifting suit as the ship's own hull -
    // magnetic boots should grab it on contact the same way (World.Eva.cs's TryAutoAttach), which
    // used to only ever check the ship and the asteroid field, leaving the station's own hull
    // silently un-attachable.
    private static bool World_Eva_MagneticBoots_AttachToStationHull()
    {
        var world = new World();
        world.SpawnCharacter(1);
        ApproachBerth(world);
        if (world.Phase != VoyagePhase.StationApproach)
            return false; // setup problem, not the behavior under test

        ExitShipIntoVacuum(world); // boots on by this helper's own last step

        // The farthest block on the station's own hull from the connector it mates the ship's
        // airlock to - unambiguously solid plating, not the open dock.
        var target = world.Station.WallBlocks
            .OrderByDescending(b => (b.Position - world.Station.ShipConnector.Position).Length())
            .First();
        var targetWorld = world.Station.WorldOffset + target.Position;

        var exitPos = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var pushDir = new Vec2(targetWorld.X - exitPos.X, targetWorld.Y - exitPos.Y).Normalized();
        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: pushDir.X, PushOffDirectionY: pushDir.Y));
        world.Step(RealtimeStep);

        // A straight push aimed right at the block's own position passes through the attach
        // margin on the way, so this needs no jetpack correction to land - just enough ticks to
        // coast the distance between the ship's airlock and the station's hull.
        for (var i = 0; i < 40 * 30; i++)
        {
            if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsEvaAttached)
                return true;
            world.Step(RealtimeStep);
        }
        return false;
    }

    // Flying into the station's hull without boots on used to just stop the drifter dead the
    // instant their position landed inside its rooms (StepFreeFloating's old blanket containment
    // check) - not a bounce, not a graze, just an invisible wall with no push-back at all. It
    // should read exactly like bouncing off the ship's own hull instead (TryAutoAttach's bounce
    // branch, shared by the new Station case): reflected away, never left resting inside it.
    private static bool World_Eva_BootsOff_BouncesOffStationHull()
    {
        var world = new World();
        world.SpawnCharacter(1);
        ApproachBerth(world);
        if (world.Phase != VoyagePhase.StationApproach)
            return false;

        ExitShipIntoVacuum(world); // boots on...
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // ...toggled straight back off
        world.Step(RealtimeStep);

        var target = world.Station.WallBlocks
            .OrderByDescending(b => (b.Position - world.Station.ShipConnector.Position).Length())
            .First();
        var targetWorld = world.Station.WorldOffset + target.Position;

        var exitPos = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var pushDir = new Vec2(targetWorld.X - exitPos.X, targetWorld.Y - exitPos.Y).Normalized();
        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: pushDir.X, PushOffDirectionY: pushDir.Y));

        // Coasting on the push-off alone, same as flying into a wall and letting physics answer
        // back - no jetpack correction, which would just fly a second attempt straight back at
        // the same spot while still immune from the first bounce (TryAutoAttach's own
        // BouncedOffFrom check, shared with the ship/asteroid cases it was copied from).
        for (var i = 0; i < 40 * 30; i++)
            world.Step(RealtimeStep);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !me.IsEvaAttached && !world.Station.ContainsPoint(new Vec2(me.X, me.Y) - world.Station.WorldOffset);
    }
}
