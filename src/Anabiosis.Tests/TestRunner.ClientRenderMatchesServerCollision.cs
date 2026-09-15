using System.Linq;
using Anabiosis.Client.Rendering;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Direct user bug report ("стены отображаются не на своих местах, а коллизии там же") - a Ship
// Editor-built hull's own post-rasterization corrections (TileShipBuilder.BuildDefinition's steps
// 3.5/3.6: a T-junction's residual wall, or a half-block notch sitting at a region's own edge) were
// applied only to the SERVER's own Ship.Tiles (Ship.Custom.cs), never networked to the client at
// all - the client re-rasterized its OWN copy purely from Rooms/Doors/AirlockOuterDoors, which can
// never represent these corrections (that's exactly why they exist as separate bolt-on lists rather
// than folded into Room.Rects). End result: the server's real collision was correct, but the
// client's rendering silently reverted to the wrong, naive geometry - "walls don't match where you
// actually bump into something."
internal static partial class TestRunner
{
    // Same half-block-notch shape as TileShipBuilder_HalfBlockNotchAtRegionEdge_
    // StaysOpenAndKeepsItsWallOpenSide, but built into a full, valid, playable custom ship (every
    // device CustomShipValidator requires) so the WHOLE pipeline runs end to end: TileShipBuilder ->
    // Ship.FromCustomDefinition (server) -> World.CreateSnapshot -> ClientTileGrid.Build (client).
    private static CustomShipDefinition BuildHalfBlockNotchShipDefinition()
    {
        var tiles = new TileGrid();
        for (var y = 0; y < 14; y++)
            for (var x = 0; x <= 6; x++)
                tiles.SetFloor(new TileCoord(x, y), true);

        tiles.SetWall(new TileCoord(0, 6), TileWallKind.Solid);
        tiles.SetWall(new TileCoord(0, 7), TileWallKind.Solid);
        tiles.SetWall(new TileCoord(6, 6), TileWallKind.Solid);
        tiles.SetWall(new TileCoord(6, 7), TileWallKind.Solid);
        tiles.SetWallOpenSide(new TileCoord(0, 6), TileSide.West);
        tiles.SetWallOpenSide(new TileCoord(0, 7), TileSide.West);
        tiles.SetWallOpenSide(new TileCoord(6, 6), TileSide.East);
        tiles.SetWallOpenSide(new TileCoord(6, 7), TileSide.East);

        // Airlock door, standalone, one tile outside the region's own North boundary - the only way
        // CustomShipValidator's own "needs an airlock" rule can be satisfied here.
        tiles.SetFloor(new TileCoord(3, -1), true);
        tiles.SetWall(new TileCoord(3, -1), TileWallKind.Door);

        var deviceKinds = new System.Collections.Generic.Dictionary<TileCoord, CustomDeviceKind>
        {
            [new TileCoord(1, 1)] = CustomDeviceKind.Reactor,
            [new TileCoord(2, 1)] = CustomDeviceKind.Distribution,
            [new TileCoord(3, 1)] = CustomDeviceKind.Helm,
            [new TileCoord(4, 1)] = CustomDeviceKind.Navigation,
            [new TileCoord(5, 1)] = CustomDeviceKind.Oxygen,
            [new TileCoord(1, 2)] = CustomDeviceKind.SuitLocker,
            [new TileCoord(2, 2)] = CustomDeviceKind.StorageRack,
        };
        foreach (var (coord, kind) in deviceKinds)
            tiles.PlaceDevice(coord, kind.ToString());

        var engineControl = new TileCoord(5, 2);
        tiles.PlaceDevice(engineControl, "engine");
        var engines = new System.Collections.Generic.Dictionary<TileCoord, TileShipBuilder.EngineSpec>
        {
            [engineControl] = new(TileSide.South, 10f),
        };

        var (definition, errors) = TileShipBuilder.BuildDefinition(tiles, deviceKinds, engines, "half-block-notch-ship", 0f);
        if (definition is null)
            throw new System.InvalidOperationException("setup problem: " + string.Join("; ", errors));
        return definition;
    }

    private static bool World_HalfBlockNotch_ClientRenderMatchesServerCollisionAtEveryNotchTile()
    {
        var definition = BuildHalfBlockNotchShipDefinition();
        var world = new World(ShipKind.Custom, definition);
        var snapshot = world.CreateSnapshot();
        var clientTiles = ClientTileGrid.Build(snapshot);

        var notchTiles = new[]
        {
            new TileCoord(0, 6), new TileCoord(0, 7), new TileCoord(6, 6), new TileCoord(6, 7),
        };
        foreach (var coord in notchTiles)
        {
            var server = world.Ship.Tiles.CellAt(coord);
            var client = clientTiles.CellAt(coord);
            // The bug: server correctly has Solid+WallOpenSide (real collision), client used to come
            // back None (no wall rendered at all) since it never learned about SupplementalWallTiles/
            // WallOpenSideOverrides.
            if (server is not { Wall: TileWallKind.Solid, WallOpenSide: not null })
                return false; // setup problem - server itself isn't right
            if (client is not { Wall: TileWallKind.Solid } || client.WallOpenSide != server.WallOpenSide)
                return false;
        }

        // And the floor immediately above/below each notch (rows 5 and 8) must read identically on
        // both sides too - not just the notch tiles themselves.
        foreach (var (x, y) in new[] { (0, 5), (0, 8), (6, 5), (6, 8) })
        {
            var coord = new TileCoord(x, y);
            if (world.Ship.Tiles.CellAt(coord)?.Wall != clientTiles.CellAt(coord)?.Wall)
                return false;
        }
        return true;
    }
}
