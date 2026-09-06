using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    // M71 (humble-soaring-cat.md) - TileGridRasterizer.FromRooms is called additively from every
    // Ship/Station/EnemyShipLayout constructor already; these tests confirm that projection is
    // topologically faithful to the original Room/Door graph for every hand-authored hull, without
    // any of them yet reading `.Tiles` for real gameplay.
    //
    // Note on tile counts: a wall in the new model occupies a full 1-unit tile that is NOT itself
    // walkable, whereas WallBlock in the old model is a thin structural skin drawn ON a room's own
    // boundary - the room's rectangle already includes right up to that edge as walkable space
    // (RoomLayout.MoveAlongAxis clamps to the room rectangle, not to some smaller interior carved out
    // by wall blocks). So a rasterized region's tile COUNT is smaller than Width*Height by roughly its
    // perimeter ring - that's a real, deliberate consequence of walls becoming solid tiles, not a
    // rounding artifact to paper over. What must still hold exactly is the TOPOLOGY: one region per
    // room, with the room's own center reliably landing inside it.

    private static bool EachRoomCenterMapsToItsOwnDistinctRegion(IReadOnlyList<Room> rooms, TileGrid tiles)
    {
        var seenRegions = new HashSet<int>();
        foreach (var room in rooms)
        {
            var center = room.Center;
            var coord = new TileCoord((int)Math.Floor(center.X), (int)Math.Floor(center.Y));
            var regionId = tiles.RegionIdAt(coord);
            if (regionId is null)
                return false; // a room's own center must be open floor, not a wall/door tile
            if (!seenRegions.Add(regionId.Value))
                return false; // two different rooms must never collapse into the same region
        }
        return seenRegions.Count == rooms.Count && tiles.Regions.Count == rooms.Count;
    }

    private static bool TileGridRasterizer_HandAuthoredShipHulls_OneRegionPerRoom()
    {
        foreach (var kind in new[] { ShipKind.Scout, ShipKind.Frigate, ShipKind.Cruiser, ShipKind.Corvette })
        {
            var ship = Ship.Create(kind);
            if (!EachRoomCenterMapsToItsOwnDistinctRegion(ship.Rooms, ship.Tiles))
                return false;
        }
        return true;
    }

    private static bool TileGridRasterizer_EveryEnemyHull_OneRegionPerRoom()
    {
        foreach (var layout in EnemyShipLayout.All)
        {
            if (!EachRoomCenterMapsToItsOwnDistinctRegion(layout.Rooms, layout.Tiles))
                return false;
        }
        return true;
    }

    private static bool TileGridRasterizer_ProceduralStation_OneRegionPerRoom()
    {
        var station = Station.CreateDefault();
        return EachRoomCenterMapsToItsOwnDistinctRegion(station.Rooms, station.Tiles);
    }

    // A whole-integer hull (Frigate) rasterizes exactly, so this checks a concrete expected shape
    // instead of a generic formula. Cockpit is Room(0,0,5,6) with a neighbor (reactor) sharing its
    // whole Right edge and nothing on any other side: TileGridRasterizer walls a room's own
    // Left/Top unconditionally but only walls its own Right/Bottom when NOT covered by a neighbor
    // (leaving that job to the neighbor's own Left/Top pass, so the shared wall is one tile, not
    // two) - so cockpit loses column 0 (own Left) and row 5 (own Bottom, genuinely exterior) but
    // keeps column 4 open (its Right side is the reactor's problem, not its own), leaving open
    // columns {1,2,3,4} x rows {1,2,3,4} = 16 tiles, none of them carved away by the
    // cockpit-reactor door (which sits entirely on column 5, inside reactor's own rectangle).
    private static bool TileGridRasterizer_FrigateCockpit_ExactInteriorTileCount()
    {
        var ship = Ship.Create(ShipKind.Frigate);
        var cockpit = ship.Rooms.First(r => r.Id == "cockpit");
        var regionId = ship.Tiles.RegionIdAt(new TileCoord((int)cockpit.Center.X, (int)cockpit.Center.Y));
        if (regionId is null)
            return false;
        var region = ship.Tiles.Regions[regionId.Value];

        for (var x = 1; x <= 4; x++)
            for (var y = 1; y <= 4; y++)
                if (!region.Tiles.Contains(new TileCoord(x, y)))
                    return false;
        return region.Tiles.Count == 16;
    }
}
