using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // Gets the ship out of dock and into open space. Needed by anything about venting to vacuum:
    // while docked, the outer airlock opens onto the station's pressurized dock chamber instead
    // (World.Atmosphere.cs), so the same door does nothing there. There's no separate "asteroid
    // field" state to travel into any more (M39) - the field is simply wherever the ship already
    // is once it's undocked, so casting off is the whole job.
    private static void CastOffIntoSpace(World world)
    {
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // undock
        world.Step(RealtimeStep);
    }

    // Shared setup for the M15 helm tests: undock, ramp Engine power up, fly out to the field's
    // own asteroid-dense marker and brake to a stop there, then man the helm. There is no
    // VoyagePhase.AsteroidField to fly into any more (M39) - the field (asteroids and all) is
    // simply wherever the ship already is once it's not docked or fighting - but flying there
    // manually needs a hand on the helm, and several callers below (asteroid-collision tests, EVA
    // targets calibrated relative to this marker) still need the ship to actually arrive near it,
    // stationary, the same guarantee the old autopilot's arrival gave for free.
    private static void EnterAsteroidFieldAndManHelm(World world, int playerId = 1)
    {
        // FlyNearAndStop already undocks, ramps the Engine and mans the helm before flying - it
        // only brakes at the end, so the pilot is still sitting at the console right after this.
        FlyNearAndStop(world, world.GalaxyMap.GetPoint("asteroid-field-epsilon").Position, playerId);
    }

    // Shared setup for tests that need the ship actually docked at a station - almost every caller
    // is using "docked at X" purely as scaffolding for something unrelated (a faction/quest/trade
    // mechanic), not testing the approach itself, so this places the ship directly rather than
    // flying it there for real (World.DebugPlaceShip - test-only, see its own doc comment). The
    // dedicated docking-mechanic tests (this file's own Helm tests, TestRunner.StationDocking.cs's
    // ApproachBerth) fly for real and don't call this.
    private static void DockAtStation(World world, string stationPointId, int playerId = 1)
    {
        if (world.IsDocked)
        {
            world.ApplyCommand(playerId, new ClientCommand(playerId, DockPressed: true));
            world.Step(RealtimeStep);
        }

        var target = world.GalaxyMap.GetPoint(stationPointId).Position;
        world.DebugPlaceShip(target);
        world.Step(RealtimeStep); // World.Voyage.cs's UpdateNearestStation now recognizes this point as nearest
        ResolveStationDefenseIfAny(world, playerId);
        world.DebugPlaceShip(world.DockBerthPosition); // re-snap onto THIS station's own hull-centre offset
        world.Step(RealtimeStep);
        ResolveStationDefenseIfAny(world, playerId);

        world.ApplyCommand(playerId, new ClientCommand(playerId, DockPressed: true));
        world.Step(RealtimeStep);
    }

    // A station whose owner has fallen to hostile standing meets an approach with its own
    // defensive squadron instead of a clean approach (World.Voyage.cs's UpdateNearestStation,
    // M37's "won't stand down for you any more, but still not the deeper WarThreshold lockout") -
    // win it the same way any other incidental battle gets cleared before the dock actually lands.
    private static void ResolveStationDefenseIfAny(World world, int playerId)
    {
        if (!world.IsInBattle)
            return;
        FireBowTurretUntilEnemyDefeated(world, playerId);
        for (var i = 0; i < 30 && world.IsInBattle; i++)
            world.Step(RealtimeStep);
    }

    // The helm's whole control model: A/D swing the bow without moving the ship, W drives it along
    // whatever heading it's holding, and X backs it straight out again.
    private static bool World_Helm_WasdSteersByHeadingAndReverseBacksOut()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        var before = world.CreateSnapshot().ShipField;
        world.ApplyCommand(1, new ClientCommand(1, HelmTurn: 1f));
        StepFor(world, 30);
        var turned = world.CreateSnapshot().ShipField;
        if (Math.Abs(turned.RotationDegrees - before.RotationDegrees) < 45f)
            return false; // the bow didn't swing
        if (new Vec2(turned.X - before.X, turned.Y - before.Y).Length() > 0.01f)
            return false; // turning is not travelling

        float AlignmentWithNose()
        {
            var field = world.CreateSnapshot().ShipField;
            var nose = TurretMount.FromDegrees(field.RotationDegrees + world.Ship.ForwardDegrees);
            var course = new Vec2(field.VelocityX, field.VelocityY).Normalized();
            return nose.X * course.X + nose.Y * course.Y;
        }

        world.ApplyCommand(1, new ClientCommand(1, HelmTurn: 0f, HelmThrottle: 1f));
        StepFor(world, 60);
        if (AlignmentWithNose() < 0.99f)
            return false; // ahead has to mean along the bow, not along some world axis

        world.ApplyCommand(1, new ClientCommand(1, HelmStabilizePressed: true));
        StepFor(world, 120);
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: -1f));
        StepFor(world, 60);

        return AlignmentWithNose() < -0.99f; // moving against its own bow, without having turned round
    }

    private static bool World_Helm_Thrust_AcceleratesShipWithInertia()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        var before = world.CreateSnapshot().ShipField;
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
        for (var i = 0; i < 60; i++) // 2s
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().ShipField;

        return before.VelocityX == 0f && after.VelocityX > 0f && after.X > before.X;
    }

    // The saved thrust vector must keep being applied even after the pilot stands up (game_design.md
    // Phase 3, M15 - "если игрок не за пультом... корабль продолжает лететь") - checked here by
    // confirming the ship is still accelerating (not just coasting) with nobody manning the helm.
    private static bool World_Helm_ThrustPersists_AfterStandingUp()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        // Only a few ticks of acceleration before standing up - engine power is ramped enough here
        // that the ship would already be at max speed (and thus no longer measurably accelerating)
        // if given the full 30-tick build-up the other helm tests use.
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
        for (var i = 0; i < 5; i++)
            world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // stand up
        var stillManning = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsAtHelm;
        var velocityAtStandUp = world.CreateSnapshot().ShipField.VelocityX;

        for (var i = 0; i < 30; i++) // no further input at all
            world.Step(RealtimeStep);
        var velocityLater = world.CreateSnapshot().ShipField.VelocityX;

        return !stillManning && velocityLater > velocityAtStandUp;
    }

    private static bool World_Helm_Stabilize_BringsShipToStop()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
        for (var i = 0; i < 60; i++) // build up speed
            world.Step(RealtimeStep);
        var movingFast = world.CreateSnapshot().ShipField.VelocityX > 1f;

        world.ApplyCommand(1, new ClientCommand(1, HelmStabilizePressed: true));
        for (var i = 0; i < 5 * 30; i++) // plenty of time to fully decelerate
            world.Step(RealtimeStep);
        var stopped = world.CreateSnapshot().ShipField;

        return movingFast && Math.Abs(stopped.VelocityX) < 0.01f && Math.Abs(stopped.VelocityY) < 0.01f;
    }

    // No power on Engine at all -> the ship must not accelerate (game_design.md Phase 3 -
    // "двигается... если на него подана энергия"); deliberately never allocates power here.
    private static bool World_Helm_NoEnginePower_ShipDoesNotAccelerate()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // undock - no engine power at all
        world.Step(RealtimeStep);

        // Undocking itself now gives a small deliberate push-off (World.StationDocking.cs's own
        // Undock - "плавно уходил от станции") independent of engine power, so velocity right after
        // casting off is no longer exactly zero - captured here rather than assumed, so the actual
        // assertion below stays about what this test means to check: throttle with no engine power
        // adds nothing on top of that push-off, not that the ship never moves at all.
        var velocityAfterUndock = world.CreateSnapshot().ShipField;

        SitAtHelm(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));

        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        var field = world.CreateSnapshot().ShipField;
        return field.VelocityX == velocityAfterUndock.VelocityX && field.VelocityY == velocityAfterUndock.VelocityY;
    }

    // The rock's outline is the thing everything else is measured against, so it has to be the same
    // outline every time it's asked for and it has to actually differ from a circle.
    private static bool AsteroidShape_IsAStableNonCircularOutline()
    {
        var rock = new Asteroid("asteroid-test", 100f, 100f, 5f);

        var first = AsteroidShape.Outline(rock);
        var second = AsteroidShape.Outline(rock);
        for (var i = 0; i < first.Length; i++)
            if ((first[i] - second[i]).Length() > 0.0001f)
                return false; // must not reshuffle between calls

        var radii = new float[first.Length];
        for (var i = 0; i < first.Length; i++)
            radii[i] = (first[i] - rock.Position).Length();
        if (radii.Max() - radii.Min() < 0.5f)
            return false; // that's a circle, not a rock

        // Every vertex sits exactly on the surface by the same measure the physics uses.
        foreach (var vertex in first)
            if (Math.Abs(AsteroidShape.DistanceOutside(rock, vertex)) > 0.01f)
                return false;

        // And a point at the nominal radius is inside on some bearings and outside on others -
        // which is precisely what a circular test could never tell you.
        var insideSomewhere = false;
        var outsideSomewhere = false;
        for (var i = 0; i < 32; i++)
        {
            var angle = i * (MathF.PI * 2f / 32);
            var probe = rock.Position + new Vec2(MathF.Cos(angle), MathF.Sin(angle)) * rock.Radius;
            if (AsteroidShape.Contains(rock, probe))
                insideSomewhere = true;
            else
                outsideSomewhere = true;
        }

        return insideSomewhere && outsideSomewhere;
    }

    // The gap between the Corvette's engine pylons is open space. The bounding box the boots used
    // to walk on covers it, so a crewman could stroll across the hole with nothing underfoot.
    private static bool HullSilhouette_TreatsTheGapBetweenPylonsAsOpenSpace()
    {
        var rooms = Ship.Create(ShipKind.Corvette).Rooms;
        var gap = new Vec2(6.75f, 17f); // below the reactor hall, between the two side bays

        var insideBoundingBox = gap.X >= rooms.Min(r => r.Left) && gap.X <= rooms.Max(r => r.Right) &&
                                gap.Y >= rooms.Min(r => r.Top) && gap.Y <= rooms.Max(r => r.Bottom);

        // Standing there should put the boots on the nearest real plating, not leave them hanging
        // in the middle of the notch.
        var stood = HullSilhouette.SnapToSurface(rooms, gap, 0.35f);

        return insideBoundingBox
            && !HullSilhouette.Contains(rooms, gap)
            && HullSilhouette.DistanceOutside(rooms, gap) > 0.5f
            && Math.Abs(HullSilhouette.DistanceOutside(rooms, stood) - 0.35f) < 0.02f;
    }

    private static bool World_Ship_CollidesWithAsteroid_StopsShipAndBreachesHull()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        var field = world.AsteroidField;
        var nearestAsteroid = field.Asteroids.OrderBy(a => (a.Position - field.Center).Length()).First();
        var breached = false;
        for (var i = 0; i < 30 * 30 && !breached; i++)
        {
            world.ApplyCommand(1, SteerToward(world, 1, nearestAsteroid.Position));
            world.Step(RealtimeStep);
            breached = world.CreateSnapshot().WallBlockStates.Any(s => s.Breached);
        }

        // The rock holes the hull and stops the ship - and then the pilot can back out of it. That
        // last part is the whole point: refusing the entire step on contact used to weld the ship
        // to whatever it touched, because every direction with any component into the rock was
        // thrown away along with the part that would have carried it clear.
        float GapToRock()
        {
            var field = world.CreateSnapshot().ShipField;
            return (nearestAsteroid.Position - new Vec2(field.X, field.Y)).Length();
        }

        // Astern on the same heading - the bow is still pointed at the rock, so this is the ship
        // backing straight out of it (HelmThrottle < 0).
        var gapAtImpact = GapToRock();
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: -1f));
        StepFor(world, 8 * 30);

        return breached && GapToRock() > gapAtImpact + 5f;
    }

}
