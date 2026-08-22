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
        if (!world.IsDocked) // starts docked at the home station
            return false;

        EnterBattle(world); // generous margin over a real cruise at ShipMaxSpeed built into FlyToward's own budget

        return world.IsInBattle;
    }

    private static bool World_Voyage_DefeatingEnemyReturnsToTraveling()
    {
        var world = new World();
        world.SpawnCharacter(1);

        EnterBattle(world);

        FireBowTurretUntilEnemyDefeated(world, 1);

        // Open navigation: victory drops back into open space rather than auto-docking, so the
        // player can freely pick the next destination (a station, or another fight).
        return !world.IsDocked && !world.IsInBattle;
    }

    // REMOVED (M39): World_Voyage_FlyingClearOfTheAsteroidFieldReturnsToTraveling and
    // World_Voyage_FlyingClearOfTheAsteroidFieldDoesNotStrandTheShipAtTheEdge used to check a
    // discrete VoyagePhase.AsteroidField -> VoyagePhase.Traveling transition, triggered by flying
    // past a dedicated AsteroidFieldExitRadius from the field's centre. Neither the phase nor that
    // radius exist any more - "AsteroidField" and "Traveling" were always the same undocked,
    // out-of-battle state, and the ship's field position is simply clamped to the system's
    // Width x Height rectangle (World.ShipField.cs's StepShipFieldPhysics) with no separate "left
    // the field" event to observe. There is nothing left for either test to check: flying to the
    // far corner or clear along one axis no longer changes any observable state at all, and
    // "parked at the edge of the field" isn't a stranded-ship bug any more - it is just one edge of
    // the map, same as flying to any other corner of open space. See the final report for this
    // milestone for the explicit call-out.

    // Flying away instead of alongside the berth is a valid way to leave a docking attempt, not
    // just successfully docking - there's no separate "approach" state to abort out of any more
    // (M39), so this is really just CanDockNow going false again once the ship backs off, and
    // staying undocked throughout.
    private static bool World_Voyage_FlyingAwayFromTheBerthAbortsTheApproach()
    {
        var world = new World();
        world.SpawnCharacter(1);
        // leaveAtHelm: true - this test keeps flying (HelmThrottle) right after ApproachBerth
        // returns, which World.cs only ever applies while the character is still seated there.
        ApproachBerth(world, "trade-station", leaveAtHelm: true);
        if (!world.CanDockNow)
            return false;

        // Straight astern - ApproachBerth leaves the ship lined up bow-first on the berth, so
        // backing off the throttle moves directly away from it without needing to turn around.
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: -1f));
        for (var i = 0; i < 60 * 30 && world.CanDockNow; i++)
            world.Step(RealtimeStep);

        return !world.CanDockNow && !world.IsDocked;
    }

    private static bool World_Voyage_StationRefuelsAndClearsBreaches()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Keep the reactor under real load throughout so there's fuel left to refill. Issued
        // after EnterBattle, not before it - EnterBattle's own DockPressed command (a fresh
        // ClientCommand, PowerSystemIndex defaulting to -1) would otherwise overwrite this held
        // slider input before a single Step ever applies it.
        EnterBattle(world);
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 0, PowerDirection: 1f));
        for (var i = 0; i < 10 * 30; i++) // let the slider ramp to full and actually burn fuel for a while
            world.Step(RealtimeStep);
        var fuelDuringFlight = world.CreateSnapshot().Power.ReactorFuel;

        FireBowTurretUntilEnemyDefeated(world, 1);

        // Head back to the home station to resupply.
        DockAtStation(world, "home-station");

        var snapshot = world.CreateSnapshot();
        // Refuel snaps to MaxFuel exactly on arrival, but firing/repair activity can continue
        // (and burn a little more) right after — assert "topped back up", not "still exactly 500".
        // Measured against the tank being nearly full again rather than against a fixed number of
        // units gained: the burn rate is deliberately slow now (PowerGrid), so a fight's worth of
        // flying costs only a few units and any "gained at least N" threshold is really an
        // assertion about the rate, not about refuelling.
        return world.IsDocked
            && fuelDuringFlight < 500f
            && snapshot.Power.ReactorFuel > fuelDuringFlight
            && snapshot.Power.ReactorFuel > 490f
            && snapshot.WallBlockStates.All(s => !s.Breached);
    }

    private static bool World_EnemyAi_DormantWhileTraveling()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // undock
        for (var i = 0; i < 10; i++) // a handful of ticks — nowhere near any hostile sector yet
            world.Step(RealtimeStep);

        return !world.IsInBattle && world.CreateSnapshot().WallBlockStates.All(s => !s.Breached);
    }

    private static bool World_Voyage_ShipMovesContinuouslyTowardTarget()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // undock
        world.Step(RealtimeStep);
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);
        SitAtHelm(world, 1);

        var target = world.GalaxyMap.GetPoint("sector-alpha").Position;
        var before = world.CreateSnapshot().Voyage.ShipMapPosition;
        // 2s of continued manual flight, still nowhere near arrival at the now much longer cruise.
        for (var i = 0; i < 60; i++)
        {
            world.ApplyCommand(1, SteerToward(world, 1, target));
            world.Step(RealtimeStep);
        }
        var after = world.CreateSnapshot().Voyage.ShipMapPosition;

        return (after - before).Length() > 0f && !world.IsDocked && !world.IsInBattle; // moving, not yet arrived
    }

    // Free-form destination (game_design.md - click anywhere in the system, not just a point of
    // interest): a coordinate with nothing at it still starts real, gradual flight.
    private static bool World_Voyage_FreeFormClickFliesShipTowardArbitraryPoint()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // undock
        world.Step(RealtimeStep);
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);
        SitAtHelm(world, 1);

        // +Y, not +X: the station's own room row extends toward +X from the berth
        // (Station.Default.cs), so a target off to the side of it (the same direction
        // PeelAwayFromBerth backs away in) is open space, not a course straight through the hull.
        var target = world.CreateSnapshot().Voyage.ShipMapPosition + new Vec2(0f, 50f);
        var before = world.CreateSnapshot().Voyage.ShipMapPosition;
        // +Y sits roughly 90 degrees off the ship's rest heading, unlike the old +X target this
        // replaced (which sat close to dead ahead) - throttle only engages once turned to within
        // 25 degrees of the bearing (SteerToward), so this needs enough ticks to actually finish
        // that turn before movement can start, not just enough to confirm it already had.
        for (var i = 0; i < 90; i++) // 3s - comfortably outlasts a worst-case 180-degree RCS turn
        {
            world.ApplyCommand(1, SteerToward(world, 1, target));
            world.Step(RealtimeStep);
        }
        var after = world.CreateSnapshot().Voyage.ShipMapPosition;

        return (after - before).Length() > 0f && !world.IsDocked && !world.IsInBattle;
    }

    // The generalized capture-radius scan (World.Voyage.cs's TryEngageHostileSector): every
    // hostile sector catches the ship on its own radius, not just when steered directly at its
    // marker - flying close enough to sector-alpha (without ever aiming right at it) still starts
    // the fight.
    private static bool World_Voyage_FreeFormClickNearHostileSectorStillTriggersBattle()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // 5 units off sector-alpha's own marker - inside its CaptureRadius (8) but not the point
        // itself, so this can only pass through the proximity scan, not landing on it exactly.
        // targetPointId excludes sector-alpha from AvoidIncidentalHazards - without it, the
        // generic hazard-avoidance would treat the very sector this test is trying to reach as
        // something to detour around, and the ship would circle it forever without ever crossing
        // its capture radius.
        FlyToward(world, world.GalaxyMap.GetPoint("sector-alpha").Position + new Vec2(5f, 0f), () => world.IsInBattle, 1, targetPointId: "sector-alpha");

        return world.IsInBattle;
    }

    private static bool World_Voyage_CannotChangeDestinationMidBattle()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterBattle(world);

        // Steering away for a single tick doesn't end the fight by itself any more than picking a
        // new destination used to - only actually flying clear of the sector does
        // (World.Voyage.cs's HasFledTheSector, exercised in full by EnemyFleet.cs's own test).
        SitAtHelm(world, 1);
        var homeTarget = world.GalaxyMap.GetPoint("home-station").Position;
        world.ApplyCommand(1, SteerToward(world, 1, homeTarget)); // try to flee
        world.Step(RealtimeStep);

        return world.IsInBattle; // still fighting — one tick of turning away doesn't drop you out
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
        EnterBattle(world);

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
        // Flying there needs a hand on the helm first (no more autopilot) - man the laser turret
        // only once the fight has actually started.
        EnterBattle(world);
        MoveCharacterTo(world, 1, 6.5f, 3f); // laser turret periscope, reactor room
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man it — no crate needed

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
