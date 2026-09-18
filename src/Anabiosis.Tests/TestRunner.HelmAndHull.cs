using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

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

    // Shared setup for the M15 helm tests: undock, ramp Engine power up, arrive at rest at the
    // field's own asteroid-dense marker, then man the helm. There is no VoyagePhase.AsteroidField
    // to fly into any more (M39) - the field (asteroids and all) is simply wherever the ship
    // already is once it's not docked or fighting - and several callers below (asteroid-collision
    // tests, EVA targets calibrated relative to this marker) need the ship to actually be at rest
    // there, the same guarantee the old autopilot's arrival gave for free.
    //
    // Used to fly there for real (FlyNearAndStop) - M53's KSP-scale rework pushed
    // asteroid-field-epsilon (AsteroidField.ClusterCenter, deliberately clear of every body's own
    // SOI) far enough out that FlyToward's fixed tick budget stopped reaching it, silently leaving
    // dozens of otherwise-unrelated callers (medkit, shield, mining, EVA...) starting from
    // wherever the ship happened to still be mid-flight instead of the resting spot they all
    // assume. None of those callers are actually testing FLIGHT itself (World.DebugPlaceShip's own
    // doc comment: "most of the test suite needs 'the ship is resting at X' purely as scaffolding")
    // - the dedicated piloting tests right here in this file apply thrust AFTER this setup
    // completes and don't care how the ship got to its start position, only that it started at
    // rest - so this just teleports there directly instead.
    private static void EnterAsteroidFieldAndManHelm(World world, int playerId = 1)
    {
        if (world.IsDocked)
        {
            world.ApplyCommand(playerId, new ClientCommand(playerId, DockPressed: true));
            world.Step(RealtimeStep);
        }

        world.ApplyCommand(playerId, new ClientCommand(playerId, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        SitAtHelm(world, playerId);
        world.DebugPlaceShip(world.GalaxyMap.GetPoint("asteroid-field-epsilon").Position); // already at rest, rotation 0
        world.Step(RealtimeStep);
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

        // .Position alone is wrong for any HOSTED station (M52/M53 - "станции летали на орбитах
        // вокруг планет"): X/Y there are a local offset from the host planet's own live position,
        // not an absolute field coordinate. ResolveGalaxyPointPosition (World.GalaxyPoints.cs) is
        // the one place production code already funnels every GalaxyPoint position read through -
        // this test helper needs the exact same resolution, not the raw record field.
        var target = world.ResolveGalaxyPointPosition(world.GalaxyMap.GetPoint(stationPointId));
        world.DebugPlaceShip(target);
        world.Step(RealtimeStep); // World.Voyage.cs's UpdateNearestStation now recognizes this point as nearest
        ResolveStationDefenseIfAny(world, playerId);

        // M58 follow-up - "перевести стыковку на относительный кадр": CanDockNow judges RELATIVE
        // speed against the station's own live velocity (World.StationDocking.cs), which is
        // genuinely nonzero now that World.cs's own Tick fix (same milestone) lets a hosted
        // station's real Kepler orbit actually advance - tens of thousands of units/s. Two live
        // samples of the berth's own position (same technique TestRunner.StationDocking.cs's
        // ApproachBerth uses, since ResolveGalaxyPointVelocity itself is private to World) give that
        // real velocity, applied via DebugSetShipVelocity right below - without it the ship sits at
        // ABSOLUTE zero, which CanDockNow reads as wildly overspeed relative to a station that fast,
        // and simply falls behind the berth (still moving at full orbital speed) during any further
        // Step call before the press, missing DockCaptureRadius too.
        var berthSample1 = world.DockBerthPosition;
        world.Step(RealtimeStep);
        var berthSample2 = world.DockBerthPosition;
        var stationVelocity = (berthSample2 - berthSample1) * (1.0 / RealtimeStep);

        world.DebugPlaceShip(berthSample2); // re-snap onto THIS station's own hull-centre offset
        world.DebugSetShipVelocity(stationVelocity); // keep pace with the berth through ResolveStationDefenseIfAny below

        // A still-hostile station re-engages every tick it finds the ship within CaptureRadius and
        // no battle already running (World.Voyage.cs's UpdateNearestStation) - beating the defenders
        // once is not the same as staying clear of them, since the very next Step (including the one
        // that processes DockPressed itself) can re-arm a fresh squadron before the press actually
        // lands. Retried rather than a single try: sitting exactly on the correct berth (unlike a
        // slightly-off approach) makes this reliably reproducible, not a rare race.
        for (var attempt = 0; attempt < 5 && !world.IsDocked; attempt++)
        {
            ResolveStationDefenseIfAny(world, playerId);
            world.ApplyCommand(playerId, new ClientCommand(playerId, DockPressed: true));
            world.Step(RealtimeStep);
        }
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

    // Direct user request ("почти точь в точь как в cosmoteer... корабль на автопилоте летит в эту
    // точку") - a plain destination click, no obstacles in the way, has to actually get there and
    // then stop and stay stopped (World.Autopilot.cs's own arrival radius/speed check clearing
    // _autopilotDestination, IsAutopilotActive flipping back to false).
    private static bool World_Autopilot_ReachesDestinationAndStops()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var target = new Vec2(world.DockBerthPosition.X + 300f, world.DockBerthPosition.Y + 150f);
        FlyToward(world, target, () => !world.IsAutopilotActive, 1);
        StepFor(world, 30); // a couple more ticks for auto-stabilize to settle any last residual drift

        var field = world.CreateSnapshot().ShipField;
        var distance = (target - new Vec2(field.X, field.Y)).Length();
        return !world.IsAutopilotActive && distance < 10f &&
            MathF.Abs(field.VelocityX) < 0.5f && MathF.Abs(field.VelocityY) < 0.5f;
    }

    // Direct user request ("избегая препятствий") - a destination straight on the far side of a
    // rock has to be reached (or at least approached) without ever actually breaching the hull on
    // it, unlike the deliberate-collision test below which bypasses the autopilot specifically to
    // force that outcome.
    private static bool World_Autopilot_AvoidsAsteroidInPath()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        var field = world.AsteroidField;
        var blocker = field.Asteroids.OrderBy(a => (a.Position - field.Center).Length()).First();
        var shipField = world.CreateSnapshot().ShipField;
        var shipPos = new Vec2(shipField.X, shipField.Y);
        // Straight past the rock's own centre, twice its own distance beyond it - a naive
        // straight-line pilot aimed here would drive right through it.
        var beyond = blocker.Position + (blocker.Position - shipPos).Normalized() * 40f;

        world.ApplyCommand(1, new ClientCommand(1, AutopilotTargetX: (float)beyond.X, AutopilotTargetY: (float)beyond.Y));

        var breached = false;
        for (var i = 0; i < 60 * 30 && !breached && world.IsAutopilotActive; i++)
        {
            world.Step(RealtimeStep);
            breached = world.CreateSnapshot().WallBlockStates.Any(s => s.Breached);
        }

        return !breached;
    }

    // Direct user request ("при зажатом ПКМ возможность выбрать в какую сторону корабль должен
    // быть наведён") - holding the override has to actually win over the autopilot's own default
    // "nose points where it's going" behavior, not just nudge it.
    private static bool World_Autopilot_FacingOverrideDivergesFromTravel()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        var shipField = world.CreateSnapshot().ShipField;
        var target = new Vec2(shipField.X + 300f, shipField.Y);
        const float overrideDegrees = 90f;

        for (var i = 0; i < 10 * 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, AutopilotTargetX: (float)target.X, AutopilotTargetY: (float)target.Y, DesiredFacingDegrees: overrideDegrees));
            world.Step(RealtimeStep);
        }

        var field = world.CreateSnapshot().ShipField;
        var nose = field.RotationDegrees + world.Ship.ForwardDegrees;
        var diffFromOverride = ((nose - overrideDegrees) % 360f + 540f) % 360f - 180f;
        var bearingToTarget = MathF.Atan2((float)(target.Y - field.Y), (float)(target.X - field.X)) * (180f / MathF.PI);
        var diffFromBearing = ((nose - bearingToTarget) % 360f + 540f) % 360f - 180f;

        // The nose tracks the RMB override, not the default "point where you're going" bearing - and
        // the two are 90 degrees apart here, far enough that "close to the override" and "close to
        // the bearing" can never both be true by accident.
        return MathF.Abs(diffFromOverride) < 5f && MathF.Abs(diffFromBearing) > 30f;
    }

    // Direct user request (the helm's "Стоп") - has to actually cancel the destination the instant
    // it's pressed (no waiting for the next tick) and let the ship coast/brake to a halt well short
    // of wherever it was headed, rather than merely pausing before resuming toward it.
    private static bool World_Autopilot_StopCancelsEarly()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterAsteroidFieldAndManHelm(world);

        var shipField = world.CreateSnapshot().ShipField;
        var target = new Vec2(shipField.X + 500f, shipField.Y + 500f);

        world.ApplyCommand(1, new ClientCommand(1, AutopilotTargetX: (float)target.X, AutopilotTargetY: (float)target.Y));
        StepFor(world, 3 * 30); // a few seconds to actually get moving
        var movingBefore = world.CreateSnapshot().ShipField;
        var wasMoving = MathF.Abs(movingBefore.VelocityX) > 1f || MathF.Abs(movingBefore.VelocityY) > 1f;

        world.ApplyCommand(1, new ClientCommand(1, AutopilotStopPressed: true));
        var cancelledImmediately = !world.IsAutopilotActive;

        StepFor(world, 30 * 30); // long enough to fully brake to a stop
        var stopped = world.CreateSnapshot().ShipField;
        var distanceFromTarget = (target - new Vec2(stopped.X, stopped.Y)).Length();

        return wasMoving && cancelledImmediately &&
            MathF.Abs(stopped.VelocityX) < 0.1f && MathF.Abs(stopped.VelocityY) < 0.1f &&
            distanceFromTarget > 20f; // stopped well short, not coincidentally arrived
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
            radii[i] = (float)(first[i] - rock.Position).Length();
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

    // HullSilhouette_TreatsTheGapBetweenPylonsAsOpenSpace used to live here, testing the gap between
    // the Corvette's own engine pylons - the only hand-authored hull with a genuine non-rectangular
    // notch in its own bounding box. Removed along with that hull class (direct user request, "удали
    // все текущие корабли... полностью удалить из кода") rather than hand-building an equivalent
    // notched Custom fixture just to keep exercising this exact shape.

    // World.Autopilot.cs now actively steers AROUND any asteroid in its path (that's the whole
    // point of the rework) - a deliberate collision has to bypass it entirely and drive the stick
    // directly, the same test-only DebugSetHelmInput bypass TestRunner.Engines.cs uses to isolate
    // engine activation from the autopilot.
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
            var shipField = world.CreateSnapshot().ShipField;
            var toRock = nearestAsteroid.Position - new Vec2(shipField.X, shipField.Y);
            var bearingDegrees = MathF.Atan2((float)toRock.Y, (float)toRock.X) * (180f / MathF.PI) - world.Ship.ForwardDegrees;
            var error = ((bearingDegrees - shipField.RotationDegrees) % 360f + 540f) % 360f - 180f;
            world.DebugSetHelmInput(MathF.Abs(error) < 25f ? 1f : 0f, 0f, MathF.Abs(error) < 2f ? 0f : MathF.Sign(error));
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
            return (float)(nearestAsteroid.Position - new Vec2(field.X, field.Y)).Length();
        }

        // Astern on the same heading - the bow is still pointed at the rock, so this is the ship
        // backing straight out of it. Kept on the same DebugSetHelmInput bypass as above (calling
        // it once per tick, same as the approach loop) rather than switching back to the autopilot
        // mid-test, which would just steer around the rock's own gap.
        var gapAtImpact = GapToRock();
        for (var i = 0; i < 8 * 30; i++)
        {
            world.DebugSetHelmInput(-1f, 0f, 0f);
            world.Step(RealtimeStep);
        }

        return breached && GapToRock() > gapAtImpact + 5f;
    }
}
