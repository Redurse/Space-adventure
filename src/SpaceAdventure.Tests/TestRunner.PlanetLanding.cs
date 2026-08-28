using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // A landable body in sol, and its fixed position (M59 - bodies don't move any more) - filtered
    // to CelestialBodyGenerator.IsLandable (Rocky/Moon) since landing needs a body with an actual
    // surface, not whichever tier sol's innermost planet happens to roll.
    private static (CelestialBody Body, Vec2 Position) SolLandableBody(World world)
    {
        var system = world.GalaxyMap.GetSystem("sol");
        var body = system.Bodies.First(b => b.ParentId is not null && CelestialBodyGenerator.IsLandable(b));
        return (body, CelestialBodyGenerator.PositionAt(body, system.BodiesById) + system.Field.Center);
    }

    // Places the ship touching a landable body's own surface, at rest (DebugPlaceShip always
    // zeroes velocity) - the shared starting point every test below needs.
    private static (CelestialBody Body, Vec2 Position) PlaceShipTouchingLandableBody(World world)
    {
        var (body, position) = SolLandableBody(world);
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }
        // A little inside the body's own radius, same reasoning HullOverlapsCelestialBody's own
        // nearest-point-on-hull-box test needs: touching by CENTRE distance alone would leave the
        // hull's own footprint still clear of the body depending on which corner faces it.
        world.DebugPlaceShip(position + new Vec2(1f, 0f) * (body.Radius - 1f));
        return (body, position);
    }

    private static bool World_PlanetLanding_CanLandNow_TrueWhenTouchingLandableBody_FalseWhenFar()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var (body, position) = SolLandableBody(world);
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }

        world.DebugPlaceShip(position + new Vec2(1f, 0f) * (body.Radius * 5f));
        var farAway = world.CanLandNow;

        world.DebugPlaceShip(position + new Vec2(1f, 0f) * (body.Radius - 1f));
        var touching = world.CanLandNow;

        return !farAway && touching;
    }

    private static bool World_PlanetLanding_TryLand_EntersSurfaceFieldWithNoGravity()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var (body, _) = PlaceShipTouchingLandableBody(world);

        world.ApplyCommand(1, new ClientCommand(1, ToggleLandingPressed: true));
        world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        var landedHere = snapshot.Voyage.LandedBodyId == body.Id;
        var withinSurfaceBounds = snapshot.ShipField.X >= 0 && snapshot.ShipField.X <= PlanetSurface.Width &&
            snapshot.ShipField.Y >= 0 && snapshot.ShipField.Y <= PlanetSurface.Height;

        // Sitting at rest, engines off, autostabilize on (TryLandOnPlanet's own setup) - a landed
        // ship should never drift, since there's no gravity anywhere in this game any more (M59).
        var before = new Vec2(snapshot.ShipField.X, snapshot.ShipField.Y);
        for (var i = 0; i < 2 * 30; i++)
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().ShipField;
        var stayedPut = (new Vec2(after.X, after.Y) - before).Length() < 0.01f;

        return landedHere && withinSurfaceBounds && stayedPut;
    }

    private static bool World_PlanetLanding_Obstacles_BlockShipMovement()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var (body, _) = PlaceShipTouchingLandableBody(world);
        world.ApplyCommand(1, new ClientCommand(1, ToggleLandingPressed: true));
        world.Step(RealtimeStep);

        var target = PlanetSurface.Generate(body.Id).OrderBy(o => (o.Position - PlanetSurface.Center).Length()).First();

        SitAtHelm(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        // Steer straight at the nearest rock and hold it - SteerToward (TestRunner.Core.cs) is the
        // same throttle/turn feedback every interplanetary flight test already uses, just aimed at
        // a local target instead of a system-scale one.
        for (var i = 0; i < 60 * 30; i++)
        {
            world.ApplyCommand(1, SteerToward(world, 1, target.Position));
            world.Step(RealtimeStep);
            var shipField = world.CreateSnapshot().ShipField;
            if ((new Vec2(shipField.X, shipField.Y) - target.Position).Length() < target.Radius + 10f)
                break; // close enough that a real collision would already have stopped it
        }
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 0f, HelmTurn: 0f));
        world.Step(RealtimeStep);

        var final = world.CreateSnapshot().ShipField;
        var distanceToRockCentre = (new Vec2(final.X, final.Y) - target.Position).Length();

        // Never actually reached the rock's own centre despite steering straight at it the whole
        // way - stopped by TryFindHullCollision (against ActiveObstacles, the surface's own rocks
        // while landed) well short of it.
        return distanceToRockCentre > target.Radius * 0.5f;
    }

    private static bool World_PlanetLanding_TakeOff_ReturnsToSystemFieldNearBodyPosition()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var (body, position) = PlaceShipTouchingLandableBody(world);
        world.ApplyCommand(1, new ClientCommand(1, ToggleLandingPressed: true));
        world.Step(RealtimeStep);

        for (var i = 0; i < 5 * 30; i++)
            world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, ToggleLandingPressed: true)); // same button, takes off
        world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        var shipPos = new Vec2(snapshot.ShipField.X, snapshot.ShipField.Y);

        // TakeOff() places the ship at body.Radius + a fixed clearance margin (World.PlanetLanding.
        // cs's own TakeOffClearanceMargin, 100 units) - that margin is now bigger than a small
        // moon's own radius (Cosmoteer-scale bodies, M59), so "near" has to be measured against the
        // actual placement formula rather than a multiple of the body's own radius.
        return snapshot.Voyage.LandedBodyId is null && (shipPos - position).Length() < body.Radius + 150f;
    }

    private static bool World_PlanetLanding_Eva_WalksOnSurfaceAndReturnsInside()
    {
        var world = new World();
        world.SpawnCharacter(1);
        PlaceShipTouchingLandableBody(world);
        world.ApplyCommand(1, new ClientCommand(1, ToggleLandingPressed: true));
        world.Step(RealtimeStep);

        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));

        // Same airlock-chamber coordinates every other EVA-exit test already walks to
        // (World_Eva_ExitSuited_SetsIsOutsideAndAttachesToShip) - landing never touches the ship's
        // own interior layout, only where the hull sits in field-space.
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // step through the open outer door onto the surface

        var outside = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var exitedOntoSurface = outside.IsOutside && outside.IsEvaAttached;

        // Walk back the way they came and through the same door - StepPlanetSurfaceWalk's own
        // door check (World.Eva.cs), the only way back inside from open ground.
        for (var i = 0; i < 10 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            if (!me.IsOutside)
                break;
            world.ApplyCommand(1, new ClientCommand(1, MoveX: -1f, MoveY: 0f));
            world.Step(RealtimeStep);
        }
        var backInside = !world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsOutside;

        return exitedOntoSurface && backInside;
    }
}
