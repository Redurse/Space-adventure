using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    private static bool World_Voyage_TravelingToHostileSectorStartsBattle()
    {
        var world = new World();
        world.SpawnCharacter(1);
        if (world.Phase != VoyagePhase.Station) // starts docked at the home station
            return false;

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++) // generous margin over a real cruise at ShipMaxSpeed
            world.Step(RealtimeStep);

        return world.Phase == VoyagePhase.Battle;
    }

    private static bool World_Voyage_DefeatingEnemyReturnsToTraveling()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        FireBowTurretUntilEnemyDefeated(world, 1);

        // Open navigation: victory drops back into open space rather than auto-docking, so the
        // player can freely pick the next destination (a station, or another fight).
        return world.Phase == VoyagePhase.Traveling;
    }

    private static bool World_Voyage_StationRefuelsAndClearsBreaches()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Keep the reactor under real load throughout so there's fuel left to refill. Both go in
        // one command — a second ApplyCommand with default power fields would otherwise reset
        // the slider input before it ever gets a tick to act on.
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 0, PowerDirection: 1f, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);
        for (var i = 0; i < 10 * 30; i++) // let the slider ramp to full and actually burn fuel for a while
            world.Step(RealtimeStep);
        var fuelDuringFlight = world.CreateSnapshot().Power.ReactorFuel;

        FireBowTurretUntilEnemyDefeated(world, 1);

        // Head back to the home station to resupply.
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "home-station"));
        DockAtStation(world);

        var snapshot = world.CreateSnapshot();
        // Refuel snaps to MaxFuel exactly on arrival, but firing/repair activity can continue
        // (and burn a little more) right after — assert "topped back up", not "still exactly 500".
        // Measured against the tank being nearly full again rather than against a fixed number of
        // units gained: the burn rate is deliberately slow now (PowerGrid), so a fight's worth of
        // flying costs only a few units and any "gained at least N" threshold is really an
        // assertion about the rate, not about refuelling.
        return snapshot.Voyage.Phase == VoyagePhase.Station
            && fuelDuringFlight < 500f
            && snapshot.Power.ReactorFuel > fuelDuringFlight
            && snapshot.Power.ReactorFuel > 490f
            && snapshot.WallBlockStates.All(s => !s.Breached);
    }

    private static bool World_EnemyAi_DormantWhileTraveling()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 10; i++) // a handful of ticks — nowhere near arrival yet
            world.Step(RealtimeStep);

        return world.Phase == VoyagePhase.Traveling && world.CreateSnapshot().WallBlockStates.All(s => !s.Breached);
    }

    private static bool World_Voyage_ShipMovesContinuouslyTowardTarget()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        var before = world.CreateSnapshot().Voyage.ShipMapPosition;
        // 2s - past even a full about-turn (ShipRotationDegreesPerSecond's slowest case, up to 180
        // degrees) before the autopilot's throttle re-engages (AutopilotToward cuts thrust above a
        // 25-degree heading error), and still nowhere near arrival at the now much longer cruise.
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().Voyage.ShipMapPosition;

        return (after - before).Length() > 0f && world.Phase == VoyagePhase.Traveling; // moving, not yet arrived
    }

    // Free-form destination (game_design.md - click anywhere in the system, not just a point of
    // interest): a coordinate with nothing at it still starts real, gradual flight.
    private static bool World_Voyage_FreeFormClickFliesShipTowardArbitraryPoint()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToX: 140f, TravelToY: 150f)); // open space near the starting berth
        var before = world.CreateSnapshot().Voyage.ShipMapPosition;
        for (var i = 0; i < 15; i++) // half a second
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().Voyage.ShipMapPosition;

        return (after - before).Length() > 0f && world.Phase == VoyagePhase.Traveling;
    }

    // The generalized capture-radius scan (World.Voyage.cs's StepTraveling): every point of
    // interest catches the ship on its own radius, not just whichever one was actually clicked -
    // flying close enough to sector-alpha (without ever naming it) still starts the fight.
    private static bool World_Voyage_FreeFormClickNearHostileSectorStillTriggersBattle()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // 5 units off sector-alpha's own (52, 97) marker - inside its CaptureRadius (8) but not the
        // point itself, so this can only pass through the proximity scan, not an id match.
        world.ApplyCommand(1, new ClientCommand(1, TravelToX: 57f, TravelToY: 97f));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        return world.Phase == VoyagePhase.Battle;
    }

    private static bool World_Voyage_CannotChangeDestinationMidBattle()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "home-station")); // try to flee
        world.Step(RealtimeStep);

        return world.Phase == VoyagePhase.Battle; // still fighting — the command was ignored
    }

    private static bool World_SuitAction_RequiresProximityToLocker()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor — far from the engine-room locker

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).SuitActionRemaining == 0f;
    }

    private static bool World_SuitAction_TakesTimeAndLocksMovement()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 20f, 3f); // engine-room suit locker

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // start equipping
        var justStarted = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (justStarted.WearingSuit || justStarted.SuitActionRemaining <= 0)
            return false; // must not be instant

        world.ApplyCommand(1, new ClientCommand(1, MoveX: -1, MoveY: 0)); // try to walk away mid-action
        for (var i = 0; i < 10; i++) // well short of the 2s action duration
            world.Step(RealtimeStep);
        var mid = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (Math.Abs(mid.X - justStarted.X) > 0.01f)
            return false; // moved while busy

        for (var i = 0; i < 60; i++) // finish the action
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

        return after.WearingSuit && after.SuitActionRemaining == 0f;
    }

    private static bool World_SuitedCharacter_ImmuneToDecompression()
    {
        var world = new World();
        world.SpawnCharacter(1);

        EquipSuit(world, 1); // suit and its tank: an empty suit is no protection at all now

        // Enemy AI only attacks once in Battle — get there first via the galaxy map. Character 1
        // is suited (fully immune) so it can safely sit in engine through the whole search below;
        // character 2 (the unsuited control) isn't spawned until right before measuring — see why
        // in World_Decompression_DrainsHealthInBreachedRoom just above.
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 600 * 30 && !RoomHasBreach(world.CreateSnapshot(), "engine"); i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 300 * 30; i++) // wait for oxygen to be clearly under the safe threshold
        {
            world.Step(RealtimeStep);
            if (world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "engine").Oxygen < 40f)
                break;
        }

        world.SpawnCharacter(2); // fresh, full health, spawns in the corridor
        MoveCharacterTo(world, 2, 20f, 3f); // brief walk into the now-dangerous engine room
        world.ApplyCommand(2, new ClientCommand(2)); // stop drifting once close enough

        var before1 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        var before2 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 2).Health;
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        var after1 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        var after2 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 2).Health;

        return Math.Abs(after1 - before1) < 0.01f // suited: untouched
            && after2 < before2; // unsuited: takes damage
    }

    private static bool World_SuitAction_IgnoredWhileMidAction()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 20f, 3f);

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // start equipping
        for (var i = 0; i < 10; i++)
            world.Step(RealtimeStep);
        var remainingBefore = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).SuitActionRemaining;

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // pressed again mid-action
        var remainingAfter = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).SuitActionRemaining;

        // A restart would jump remaining back up near the full 2s duration.
        return Math.Abs(remainingBefore - remainingAfter) < 0.01f;
    }

    private static bool World_Character_FacingTracksLastMoveDirection()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: -1)); // face "up"
        world.Step(RealtimeStep);
        var facingUp = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (facingUp.FacingY >= 0)
            return false;

        world.ApplyCommand(1, new ClientCommand(1)); // stop moving — facing should hold, not reset
        world.Step(RealtimeStep);
        var stillFacingUp = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

        return stillFacingUp.FacingY < 0;
    }

    private static bool World_LaserTurret_FiresUsingChargeWithoutAmmoCrate()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 6.5f, 3f); // laser turret periscope, reactor room
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man it — no crate needed
        EnterBattle(world);

        var before = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-laser").Charge; // starts full
        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
        StepFor(world, 60);
        var snapshot = world.CreateSnapshot();
        var after = snapshot.TurretStates.Single(t => t.Id == "turret-laser").Charge;

        return before > 0 && after < before && snapshot.Enemy.Hp < 100f;
    }

    private static bool World_LaserTurret_RechargesOnlyFromWeaponChargerAllocation()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 6.5f, 3f);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man

        for (var shot = 0; shot < 3; shot++) // 3 shots * 10 charge empties the 30-charge capacitor
        {
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
            for (var i = 0; i < 15; i++) // outlast the 0.4s cooldown
                world.Step(RealtimeStep);
        }
        var depleted = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-laser").Charge;

        for (var i = 0; i < 60; i++) // no power allocated to WeaponCharger -> should not recharge
            world.Step(RealtimeStep);
        var stillDepleted = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-laser").Charge;

        // PowerSystemId order: Oxygen, Engine, Shields, WeaponCharger(3), Secondary.
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 3, PowerDirection: 1f));
        for (var i = 0; i < 90; i++)
            world.Step(RealtimeStep);
        var recharged = world.CreateSnapshot().TurretStates.Single(t => t.Id == "turret-laser").Charge;

        return depleted < 1f && Math.Abs(stillDepleted - depleted) < 0.01f && recharged > depleted;
    }

}
