using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // Gets the ship out of dock and into open space. Needed by anything about venting to vacuum:
    // while docked, the outer airlock opens onto the station's pressurized dock chamber instead
    // (World.Atmosphere.cs), so the same door does nothing there.
    private static void CastOffIntoSpace(World world)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "asteroid-field-epsilon"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.AsteroidField; i++)
            world.Step(RealtimeStep);
    }

    // Shared setup for the M15 helm tests: fly to the asteroid field, ramp Engine power up, then
    // walk the character to the helm console and man it.
    private static void EnterAsteroidFieldAndManHelm(World world)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "asteroid-field-epsilon"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.AsteroidField; i++)
            world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        MoveCharacterTo(world, 1, 3f, 3f); // corridor -> reactor -> cockpit, at the doors' shared height
        MoveCharacterTo(world, 1, 3f, 4f); // helm console
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man it
    }

    // Shared setup for tests that need the ship actually docked at a station: arriving now only
    // drops the ship into VoyagePhase.StationApproach (World.StationDocking.cs, manual docking) -
    // fly it the rest of the way in ourselves, same helm pattern as EnterAsteroidFieldAndManHelm.
    // EnterStationApproach always places the ship directly in line with the station (facing +X),
    // so a straight HelmThrustX:1 is all that's needed to reach the docking capture zone.
    private static void DockAtStation(World world)
    {
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.StationApproach; i++)
            world.Step(RealtimeStep);

        // Three attempts, because the recovery below is best-effort: a badly shot-up ship can need
        // the engine repaired *and* the power grid re-balanced before it will move at all, and a
        // single pass occasionally leaves it still dead in space just short of the dock.
        // ...and the second engine block can be the damaged one, and the wiring can be cut on top of
        // that, so a run of bad luck needs more than three passes to walk off. The seeded roll
        // sequence (World.EnemyAi.cs) makes such a run reproducible rather than occasional, which is
        // exactly why the recovery has to be able to grind through it.
        for (var attempt = 0; attempt < 8 && world.Phase == VoyagePhase.StationApproach; attempt++)
            TryDockingRun(world);
    }

    private static void TryDockingRun(World world)
    {
        // A caller that just fought its way out of a battle (FireBowTurretUntilEnemyDefeated)
        // leaves the character manning the bow turret - can't walk anywhere until standing up.
        if (world.CreateSnapshot().TurretStates.Any(t => t.MannedByPlayerId == 1))
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        // Random combat (World.EnemyAi.cs) can happen to damage the Engine system device itself,
        // not just breach a wall block (game_design.md's "known pitfall" about random attack
        // targets, see continue.md) - if so the ship simply can't move at all here no matter how
        // much power is allocated (IsDeviceConnected gates GetEffectivePower). Repair it first.
        // Every engine block, not just the first: each class carries two of them (WireNetwork's
        // system-engine/system-engine-2), and a run where the second is the damaged one left the
        // ship dead in space with this recovery reporting success.
        foreach (var engineDevice in world.Ship.SystemDevices.Where(d => d.System == PowerSystemId.Engine))
        {
            if (!world.CreateSnapshot().SystemStates.First(s => s.DeviceId == engineDevice.Id).Damaged)
                continue;

            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var holdingTool = me.Inventory!.HeldMainSlotIndices
                .Select(i => me.Inventory.MainSlots[i])
                .Any(t => t is ItemType.Wrench or ItemType.Screwdriver);
            if (!holdingTool)
            {
                var slot = TakeFromRack(world, ItemType.Wrench);
                world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));
            }

            MoveCharacterTo(world, 1, engineDevice.Position.X, 3f);
            MoveCharacterTo(world, 1, engineDevice.Position.X, engineDevice.Position.Y);
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // repair
        }

        // A caller may have already boosted some other system to the reactor's full output
        // (e.g. World_Voyage_StationRefuelsAndClearsBreaches keeps Oxygen maxed) - that leaves
        // zero headroom for Engine (PowerGrid.Step's maxForThis is capped by othersTotal), so
        // free it back up first or Engine would never actually ramp above 0.
        foreach (var systemIndex in new[] { 0, 2, 3, 4 })
        {
            world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: systemIndex, PowerDirection: -1f));
            for (var i = 0; i < 90; i++)
                world.Step(RealtimeStep);
        }

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        MoveCharacterTo(world, 1, 3f, 3f);
        MoveCharacterTo(world, 1, 3f, 4f); // helm console
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsAtHelm)
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // man it
        // Docking is a deliberate press now, not an automatic capture (World.StationDocking.cs),
        // so the approach has to actually be flown: a plain bang-bang controller that thrusts
        // toward the berth whenever the ship is going too slowly to close the gap and brakes
        // whenever it's going too fast to mate. Just "full thrust then stabilize" deadlocks -
        // braking from full speed stops the ship short of the berth and it sits there forever.
        for (var i = 0; i < 60 * 30 && world.Phase == VoyagePhase.StationApproach; i++)
        {
            if (world.CanDockNow)
            {
                world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
                world.Step(RealtimeStep);
                continue;
            }

            var shipField = world.CreateSnapshot().ShipField;
            var toPort = world.DockBerthPosition - new Vec2(shipField.X, shipField.Y); // the berth, not the airlock rectangle
            var speed = new Vec2(shipField.VelocityX, shipField.VelocityY).Length();

            if (speed > 1.5f)
                world.ApplyCommand(1, new ClientCommand(1, HelmStabilizePressed: true));
            else
                world.ApplyCommand(1, SteerToward(world, 1, world.DockBerthPosition));

            world.Step(RealtimeStep);
        }

        if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsAtHelm)
            world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // stand up from the helm
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
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "asteroid-field-epsilon"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.AsteroidField; i++)
            world.Step(RealtimeStep);

        MoveCharacterTo(world, 1, 3f, 3f); // corridor -> reactor -> cockpit, at the doors' shared height
        MoveCharacterTo(world, 1, 3f, 4f); // helm console
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));

        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        var field = world.CreateSnapshot().ShipField;
        return field.VelocityX == 0f && field.VelocityY == 0f;
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
