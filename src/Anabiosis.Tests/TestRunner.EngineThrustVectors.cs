using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // Direct user request ("сделай тягу зависимой от расположения движков... как в Cosmoteer") -
    // two engines placed mirror-symmetric about the hull's own bounding-box centre Y (World.
    // ShipField.cs's GetHullLocalBounds, the same stand-in "hull centre" ComputeEngineForces uses
    // as its pivot), both Facing West. ForwardDegrees is 0 below, so the hull's own local "forward"
    // is East (TurretMount.FromDegrees(0) = (1,0)) - a West-facing engine's push direction (opposite
    // its own Facing) is exactly East, i.e. fully aligned with straight throttle, so both engines
    // fully activate for a pure HelmThrottle request (World.Engines.cs's MarchingRawControl) rather
    // than sitting at zero the way an engine facing 90 degrees off that axis would. Positioned at
    // the SAME X but mirrored Y (1 and 3.5, either side of the hull's own centre Y=2) - net torque
    // (arm.X*force.Y - arm.Y*force.X) is proportional to the Y offset when force is purely
    // horizontal, so the two torques are equal and opposite and cancel exactly, unlike
    // BuildEngineCustomShipDefinition's single off-center engine (TestRunner.Engines.cs), which does
    // not cancel anything.
    private static CustomShipDefinition BuildSymmetricEngineCustomShipDefinition() => new(
        "Тестовый корабль с симметричными двигателями",
        new[]
        {
            new CustomRoomDef("a", "Мостик", 0, 0, 4, 4),
            new CustomRoomDef("b", "Шлюз", 4, 0, 4, 4),
        },
        new[] { new CustomDoorDef(4, 2, true, true) },
        new[] { new CustomAirlockDef("b", EdgeSide.Right) },
        new[]
        {
            new CustomDeviceDef(CustomDeviceKind.Reactor, 1, 1),
            new CustomDeviceDef(CustomDeviceKind.Distribution, 2, 1),
            new CustomDeviceDef(CustomDeviceKind.Helm, 1, 2),
            new CustomDeviceDef(CustomDeviceKind.Navigation, 2, 2),
            new CustomDeviceDef(CustomDeviceKind.Oxygen, 2, 3),
            new CustomDeviceDef(CustomDeviceKind.SuitLocker, 5, 1),
            new CustomDeviceDef(CustomDeviceKind.StorageRack, 5, 2),
        },
        0f,
        EnginesRaw: new[]
        {
            new CustomEngineDef(2.5f, 0.5f, TileSide.West, 20f),
            new CustomEngineDef(2.5f, 3.5f, TileSide.West, 20f),
        });

    // Direct user request ("как в Cosmoteer... включались двигатели которые смотрят в
    // противоположную сторону от того куда я хочу") - a Marching engine facing 90 degrees off the
    // requested ship-local direction (North, while ForwardDegrees=0 means "forward" is East) should
    // sit completely idle for a pure forward-throttle request, and fire for a pure strafe request
    // instead - the two tests right below this ship exercise exactly that.
    private static CustomShipDefinition BuildNorthFacingEngineCustomShipDefinition() => new(
        "Тестовый корабль с боковым двигателем",
        new[]
        {
            new CustomRoomDef("a", "Мостик", 0, 0, 4, 4),
            new CustomRoomDef("b", "Шлюз", 4, 0, 4, 4),
        },
        new[] { new CustomDoorDef(4, 2, true, true) },
        new[] { new CustomAirlockDef("b", EdgeSide.Right) },
        new[]
        {
            new CustomDeviceDef(CustomDeviceKind.Reactor, 1, 1),
            new CustomDeviceDef(CustomDeviceKind.Distribution, 2, 1),
            new CustomDeviceDef(CustomDeviceKind.Helm, 1, 2),
            new CustomDeviceDef(CustomDeviceKind.Navigation, 2, 2),
            new CustomDeviceDef(CustomDeviceKind.Oxygen, 2, 3),
            new CustomDeviceDef(CustomDeviceKind.SuitLocker, 5, 1),
            new CustomDeviceDef(CustomDeviceKind.StorageRack, 5, 2),
        },
        0f,
        EnginesRaw: new[] { new CustomEngineDef(1.5f, 3.5f, TileSide.North, 20f) });

    private static bool World_Engine_MisalignedFacing_DoesNotFireForThrottle()
    {
        var world = new World(ShipKind.Custom, BuildNorthFacingEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        UndockEngineTestShip(world, 1);
        SitAtEngineTestHelm(world, 1);

        world.DebugSetHelmInput(1f, 0f, 0f);
        world.Step(RealtimeStep);
        return !world.CreateSnapshot().EngineStates!.Single().IsThrusting;
    }

    private static bool World_Engine_Strafe_FiresThePerpendicularEngine()
    {
        var world = new World(ShipKind.Custom, BuildNorthFacingEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        UndockEngineTestShip(world, 1);
        SitAtEngineTestHelm(world, 1);

        world.DebugSetHelmInput(0f, 1f, 0f);
        world.Step(RealtimeStep);
        return world.CreateSnapshot().EngineStates!.Single().IsThrusting;
    }

    // Direct user request ("для поворота... как в реальности") - two Rcs engines whose own real
    // torque (from their own position and Facing, not a shared assumption) has OPPOSITE signs for
    // the same turn: only the one that actually helps should fire.
    private static CustomShipDefinition BuildDualRcsCustomShipDefinition() => new(
        "Тестовый корабль с 2 РКС",
        new[]
        {
            new CustomRoomDef("a", "Мостик", 0, 0, 4, 4),
            new CustomRoomDef("b", "Шлюз", 4, 0, 4, 4),
        },
        new[] { new CustomDoorDef(4, 2, true, true) },
        new[] { new CustomAirlockDef("b", EdgeSide.Right) },
        new[]
        {
            new CustomDeviceDef(CustomDeviceKind.Reactor, 1, 1),
            new CustomDeviceDef(CustomDeviceKind.Distribution, 2, 1),
            new CustomDeviceDef(CustomDeviceKind.Helm, 1, 2),
            new CustomDeviceDef(CustomDeviceKind.Navigation, 2, 2),
            new CustomDeviceDef(CustomDeviceKind.Oxygen, 2, 3),
            new CustomDeviceDef(CustomDeviceKind.SuitLocker, 5, 3),
            new CustomDeviceDef(CustomDeviceKind.StorageRack, 6, 3),
        },
        0f,
        EnginesRaw: new[]
        {
            // Hull centre is (4,2) (GetHullLocalBounds over both 4x4 rooms) - West at (1,1.5) pushes
            // East with an arm of (-3,-0.5), a positive torque; East at (7,1.5) pushes West with an
            // arm of (3,-0.5), a negative torque - opposite signs by construction, same magnitude.
            new CustomEngineDef(1f, 1.5f, TileSide.West, 20f, EngineRole.Rcs),
            new CustomEngineDef(7f, 1.5f, TileSide.East, 20f, EngineRole.Rcs),
        });

    private static bool World_Engine_Rotation_OnlyTheHelpfulRcsEngineFires()
    {
        var world = new World(ShipKind.Custom, BuildDualRcsCustomShipDefinition());
        world.SpawnCharacter(1);
        UndockEngineTestShip(world, 1);
        SitAtEngineTestHelm(world, 1);

        world.DebugSetHelmInput(0f, 0f, 1f);
        world.Step(RealtimeStep);

        var states = world.CreateSnapshot().EngineStates!;
        var helpful = states.Single(s => s.X == 1f);
        var unhelpful = states.Single(s => s.X == 7f);
        return helpful.IsThrusting && !unhelpful.IsThrusting;
    }

    // A fresh World starts docked (World.Voyage.cs's StepVoyage: IsDocked pins _shipFieldPosition to
    // the berth and returns before ever reaching StepShipFieldPhysics) - undocking first is what
    // TestRunner.HelmAndHull.cs's own EnterAsteroidFieldAndManHelm already does before any of ITS
    // flight assertions; these engine-focused tests need the same step; found the hard way here
    // when the first version of these tests failed with the ship simply never moving at all.
    private static void UndockEngineTestShip(World world, int playerId = 1)
    {
        if (world.IsDocked)
        {
            world.ApplyCommand(playerId, new ClientCommand(playerId, DockPressed: true));
            world.Step(RealtimeStep);
        }
        // Undock() only nudges the ship clear of the berth at a slow drift (World.StationDocking.cs's
        // own UndockDriftSpeed) - still well within HullTouchesStation range for a while afterward,
        // during which StepShipFieldPhysics's own "moving candidate position would touch the station"
        // guard zeroes _shipVelocity every tick regardless of what any engine is doing. Found the hard
        // way: a test asserting on RotationDegrees alone (not velocity) never notices this, since that
        // guard only ever zeroes velocity - DebugPlaceShip relocates well clear of the berth so a real
        // velocity check isn't confounded by still being parked at the station.
        world.DebugPlaceShip(new Vec2(5000, 5000));
    }

    // Regression guard for the enginePowerScale bug found while writing this test: World.ShipField.cs
    // used to scale the new per-engine force/torque by GetEffectivePower(PowerSystemId.Engine), which
    // only ever counts the OLD flat-bonus CustomDeviceKind.Engine system-devices - a hull built purely
    // from the new Ship.Engines fixtures (like every ship in this file) has none, so that scale was
    // always exactly 0 and the whole feature was silently inert. Fixed by dropping that scale from the
    // new model entirely (World.ShipField.cs's own doc comment on engineForce/engineTorque) - these
    // three tests exercise exactly the path that bug broke.
    private static bool World_Engine_OffCenterEngine_InducesGrowingAngularVelocity()
    {
        var world = new World(ShipKind.Custom, BuildEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        UndockEngineTestShip(world, 1);
        SitAtEngineTestHelm(world, 1);

        var rotations = new float[4];
        for (var i = 0; i < rotations.Length; i++)
        {
            world.DebugSetHelmInput(1f, 0f, 0f);
            world.Step(RealtimeStep);
            rotations[i] = world.CreateSnapshot().ShipField.RotationDegrees;
        }

        // A single engine well off the hull's own bounding-box centre (BuildEngineCustomShipDefinition's
        // Control at (1,1.5) vs. the hull centre at (4,2)) induces nonzero net torque - nothing damps
        // it (HelmThrottle != 0 disengages auto-stabilize, SetHelmInput's own doc comment), so the
        // ship keeps rotating faster each tick rather than settling at some fixed turn rate.
        if (rotations[0] == 0f)
            return false;
        var earlyDelta = MathF.Abs(rotations[1] - rotations[0]);
        var laterDelta = MathF.Abs(rotations[3] - rotations[2]);
        return laterDelta > earlyDelta;
    }

    private static bool World_Engine_SymmetricEngines_ProduceNoNetRotation()
    {
        var world = new World(ShipKind.Custom, BuildSymmetricEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        UndockEngineTestShip(world, 1);
        SitAtEngineTestHelm(world, 1);

        for (var i = 0; i < 10; i++)
        {
            world.DebugSetHelmInput(1f, 0f, 0f);
            world.Step(RealtimeStep);
        }

        var snapshot = world.CreateSnapshot();
        // Net thrust is nonzero (both engines' reaction force adds along the same axis) but net
        // torque cancels exactly - the ship should accelerate in a straight line with no induced spin.
        return MathF.Abs(snapshot.ShipField.RotationDegrees) < 0.01f
            && (MathF.Abs(snapshot.ShipField.VelocityX) > 0.01f || MathF.Abs(snapshot.ShipField.VelocityY) > 0.01f);
    }

    // Direct user request ("хочу... возможность стабилизации корабля где двигатели начинают работаь в
    // том режиме чтобы компенсировать поворот") - re-engaging auto-stabilize (World.ShipField.cs's
    // EngageAutoStabilize) should bring the spin the off-center engine built up back toward a stop
    // within a reasonable number of ticks, whose angular-decay branch is
    // ShipAutoStabilizeAngularDecelerationPerSecond.
    private static bool World_Engine_Stabilize_DecaysInducedAngularVelocity()
    {
        var world = new World(ShipKind.Custom, BuildEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        UndockEngineTestShip(world, 1);
        SitAtEngineTestHelm(world, 1);
        for (var i = 0; i < 10; i++)
        {
            world.DebugSetHelmInput(1f, 0f, 0f);
            world.Step(RealtimeStep);
        }

        // There's no separate player-facing "stabilize" command any more - World.Autopilot.cs's own
        // StepAutopilot only re-engages auto-stabilize ONCE, at the moment a destination is
        // cancelled/arrived (World.Autopilot.cs's own CancelAutopilot doc comment - re-asserting it
        // every idle tick instead would stomp on DebugSetShipVelocity's "stays off until something
        // explicit re-engages it" contract elsewhere). There was never an active destination here to
        // begin with, so this call is a one-shot "player pressed stabilize" equivalent, not a real
        // cancellation.
        world.CancelAutopilot();
        for (var i = 0; i < 90; i++)
            world.Step(RealtimeStep);

        // A stopped angular velocity means a further tick barely moves RotationDegrees at all -
        // compare two consecutive post-stabilize readings rather than asserting an exact value,
        // since the flat _helmTurn-driven rate (unaffected by any of this) is also always at play.
        var beforeOneMore = world.CreateSnapshot().ShipField.RotationDegrees;
        world.Step(RealtimeStep);
        var afterOneMore = world.CreateSnapshot().ShipField.RotationDegrees;
        return MathF.Abs(afterOneMore - beforeOneMore) < 0.05f;
    }
}
