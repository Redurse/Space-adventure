using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // M72 (humble-soaring-cat.md) - World.TileSync.cs mirrors the real door/wall state onto
    // Ship.Tiles every Step(); these confirm the mirror actually reflects live gameplay actions
    // (ToggleDoor via ClientCommand, a real breach), not just its own initial construction-time
    // snapshot.

    private static bool World_TileSync_ToggleDoor_UpdatesShipTiles()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var door = world.Ship.Doors.First(d => d.Id == "door-cockpit-reactor");
        var coords = TileGridRasterizer.DoorTileCoords(world.Ship.Rooms, door.X, door.Y, door.Width, door.Height).ToList();
        if (coords.Count == 0)
            return false;

        world.Step(RealtimeStep); // let the very first sync run before checking the starting state
        if (coords.Any(c => world.Ship.Tiles.CellAt(c) is not { DoorOpen: true }))
            return false; // doors start open (World.ShipPurchase.cs's InitializeShipState)

        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-cockpit-reactor"));
        world.Step(RealtimeStep);
        return coords.All(c => world.Ship.Tiles.CellAt(c) is { DoorOpen: false });
    }

    private static bool World_TileSync_DestroyedDoor_ForcesTileOpenAndZeroesTileHp()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var door = world.Ship.Doors.First(d => d.Id == "door-cockpit-reactor");
        var coords = TileGridRasterizer.DoorTileCoords(world.Ship.Rooms, door.X, door.Y, door.Width, door.Height).ToList();

        world.DamageDoor("door-cockpit-reactor");
        world.Step(RealtimeStep);

        return coords.All(c => world.Ship.Tiles.CellAt(c) is { DoorOpen: true, WallHp: <= 0f });
    }

    private static bool World_TileSync_BreachedWallBlock_ZeroesTileHpAndRejoinsRegion()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var block = world.Ship.WallBlocks.First(b => b.RoomId == "corridor");
        var room = world.Ship.GetRoom("corridor");
        var coord = TileGridRasterizer.WallBlockTileCoord(block, world.Ship.Rooms, room);

        world.Step(RealtimeStep);
        var beforeRegion = world.Ship.Tiles.RegionIdAt(coord); // a solid wall tile is never a region member

        world.DebugBreachWallBlock("corridor");
        world.Step(RealtimeStep);

        var cell = world.Ship.Tiles.CellAt(coord);
        var afterRegion = world.Ship.Tiles.RegionIdAt(coord);
        // A breached wall tile stops blocking region topology (TileGrid.IsBlockingForRegion), so it
        // rejoins whichever region its now-open neighbors belong to.
        return beforeRegion is null && cell is { WallHp: <= 0f } && afterRegion is not null;
    }
}
