using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Direct user request ("нельзя было ходить сквозь все устройства... игрок спавнился не в устройствах")
// - ShipDeviceOccupancy stamps every tile-editor device onto Ship.Tiles, and the spawn point (the
// helm's own centre) is moved onto clear floor.
internal static partial class TestRunner
{
    // A 10x10 walled room built through the real editor -> TileShipBuilder -> Ship path.
    private static Ship BuildDeviceCollisionShip() => Ship.FromCustomDefinition(BuildDeviceCollisionDefinition());

    private static CustomShipDefinition BuildDeviceCollisionDefinition()
    {
        var tiles = new TileGrid();
        for (var x = -1; x <= 10; x++)
        for (var y = -1; y <= 10; y++)
        {
            tiles.SetFloor(new TileCoord(x, y), true);
            if (x is -1 or 10 || y is -1 or 10)
                tiles.SetWall(new TileCoord(x, y), TileWallKind.Solid);
        }
        tiles.SetWall(new TileCoord(10, 5), TileWallKind.Door);

        var deviceKinds = new Dictionary<TileCoord, CustomDeviceKind>
        {
            [new TileCoord(0, 0)] = CustomDeviceKind.Helm,
            [new TileCoord(2, 0)] = CustomDeviceKind.Navigation,
            [new TileCoord(5, 0)] = CustomDeviceKind.Reactor,
            [new TileCoord(4, 6)] = CustomDeviceKind.Distribution,
            [new TileCoord(5, 6)] = CustomDeviceKind.Engine,
            [new TileCoord(6, 6)] = CustomDeviceKind.Oxygen,
            [new TileCoord(7, 6)] = CustomDeviceKind.SuitLocker,
            [new TileCoord(8, 6)] = CustomDeviceKind.StorageRack,
            [new TileCoord(1, 8)] = CustomDeviceKind.CardTable,
        };
        foreach (var (anchor, _) in deviceKinds)
            tiles.PlaceDevice(anchor, $"device-{anchor.X}-{anchor.Y}");

        var (definition, errors) = TileShipBuilder.BuildDefinition(
            tiles, deviceKinds, new Dictionary<TileCoord, TileShipBuilder.EngineSpec>(), "Тест", 0f);
        if (definition is null || errors.Count > 0)
            throw new InvalidOperationException("test ship failed to build: " + string.Join("; ", errors));
        return definition;
    }

    private static Vec2 WalkStraight(Ship ship, Vec2 from, Vec2 delta)
    {
        var roomId = TileMovement.RoomIdAt(ship.Rooms, from) ?? ship.Rooms[0].Id;
        var position = from;
        const int steps = 40;
        for (var i = 0; i < steps; i++)
        {
            (position, roomId) = ship.MoveAlongAxis(position, roomId, new Vec2(delta.X / steps, 0), _ => true);
            (position, roomId) = ship.MoveAlongAxis(position, roomId, new Vec2(0, delta.Y / steps), _ => true);
        }
        return position;
    }

    private static bool DeviceCollision_OrdinaryDevice_StopsAWalker()
    {
        var ship = BuildDeviceCollisionShip();
        // Walk up the column of the storage rack (tile 8,6): must stop below its bottom face.
        var end = WalkStraight(ship, new Vec2(8.5, 8.5), new Vec2(0, -4));
        return ship.Tiles.CellAt(new TileCoord(8, 6))?.DeviceId is not null
            && end.Y >= 7f + TileMovement.CharacterRadius - 0.01f;
    }

    private static bool DeviceCollision_Reactor_ClaimsItsWholeFootprint()
    {
        var ship = BuildDeviceCollisionShip();
        for (var x = 5; x < 9; x++)
        for (var y = 0; y < 4; y++)
            if (ship.Tiles.CellAt(new TileCoord(x, y))?.DeviceId is null)
                return false;
        // and a walker crossing the room left-to-right under row 1 is stopped at its left face
        var end = WalkStraight(ship, new Vec2(4.5, 2.5), new Vec2(4, 0));
        return end.X <= 5f - TileMovement.CharacterRadius + 0.01f;
    }

    private static bool DeviceCollision_SpawnIsOnClearFloor_NotInsideTheHelm()
    {
        var ship = BuildDeviceCollisionShip();
        var tile = new TileCoord((int)Math.Floor(ship.SpawnPoint.X), (int)Math.Floor(ship.SpawnPoint.Y));
        return ship.Tiles.CellAt(tile) is { DeviceId: null, HasFloor: true }
            && ShipDeviceOccupancy.IsClearSpot(ship.Tiles, ship.SpawnPoint)
            && (ship.SpawnPoint - ship.HelmConsole.Position).Length() < 3.0;
    }

    // The frozen hand-authored hull (free-float positions, no editor anchors) is left exactly as it was.
    private static bool DeviceCollision_DefaultHull_IsUnchanged()
    {
        var ship = Ship.FromCustomDefinition(ShipDefaultHull.Definition);
        return ship.Tiles.Cells.Values.All(c => c.DeviceId is null)
            && ship.SpawnPoint == ship.HelmConsole.Position;
    }
}

internal static partial class TestRunner
{
    // Each role appears beside its own device, on clear floor, never inside it.
    private static bool DeviceCollision_RoleSpawn_BesideOwnDevice()
    {
        var ship = BuildDeviceCollisionShip();
        foreach (var (role, kind) in new[] { (CrewRole.Mechanic, DeviceKind.Reactor), (CrewRole.Captain, DeviceKind.Helm), (CrewRole.Engineer, DeviceKind.Distribution), (CrewRole.Scientist, DeviceKind.Navigation) })
        {
            var device = ship.Devices.First(d => d.Kind == kind);
            var (position, roomId) = ship.RoleSpawn(role);
            if (roomId != device.RoomId || ShipDeviceOccupancy.OverlapsDevice(ship.Tiles, position)
                || !ShipDeviceOccupancy.IsClearSpot(ship.Tiles, position) || (position - device.Position).Length() > 3.2)
                return false;
        }
        return true;
    }

    // A player's first role pick moves them from the shared spawn to that role's own spot.
    private static bool DeviceCollision_FirstRolePick_MovesPlayerToTheirPost()
    {
        var world = new World(ShipKind.Custom, BuildDeviceCollisionDefinition());
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, SetOwnRoleTo: CrewRole.Mechanic));
        world.Step(0.01);
        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var expected = world.Ship.RoleSpawn(CrewRole.Mechanic).Position;
        return (new Vec2(me.X, me.Y) - expected).Length() < 0.2 && expected != world.Ship.SpawnPoint;
    }
}

internal static partial class TestRunner
{
    // The reactor is solid now; standing against its edge must still reach it (its levers here).
    private static bool DeviceCollision_Reactor_UsableFromItsEdge()
    {
        var world = new World(ShipKind.Custom, BuildDeviceCollisionDefinition());
        world.SpawnCharacter(1);
        world.Step(0.01);
        var reactor = world.Ship.ReactorBlock.Position;               // (7, 2): the 4x4 machine spans x 5..9
        MoveCharacterTo(world, 1, 4.5f, (float)reactor.Y);
        var me = world.CreateSnapshot().Characters.Single();
        if (me.X > 5f - TileMovement.CharacterRadius + 0.01f)          // still stopped at the edge
            return false;
        var before = world.CreateSnapshot().ReactorLevers.LightsOn;
        world.ApplyCommand(1, new ClientCommand(1, ToggleLightsPressed: true));
        world.Step(0.01);
        return world.CreateSnapshot().ReactorLevers.LightsOn != before;
    }
}
