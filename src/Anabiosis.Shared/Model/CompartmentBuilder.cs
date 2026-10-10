namespace Anabiosis.Shared.Model;

// In-game building from the compartment catalog (the Shipwright), on the same tile grid and with the same stamping rules as the
// Ship Editor: a catalog compartment is placed by a tile anchor and a quarter-turn rotation, must land on completely free ground
// (CompartmentPlacer.Stamp refuses any overlap), and must touch the existing hull. A valid placement is turned into a FRAGMENT of
// a ship definition (the compartment's room, devices, engines, airlock and the tile corrections it needs), which Merge then
// appends to the live ship's definition - the same conversion the editor's "Играть" uses (TileShipBuilder), applied to just the
// new compartment so everything already on the ship is left exactly as it is.
public sealed record CompartmentPlan(
    CompartmentCatalogEntry Entry,
    TileCoord Anchor,
    int Rotation,
    CustomShipDefinition Fragment,
    IReadOnlyList<TileCoord> Tiles,
    RectF Bounds);

public static class CompartmentBuilder
{
    private const string FragmentInstance = "compartment-new";

    // The tiles the compartment would occupy (floor and wall ring), absolute.
    public static IReadOnlyList<TileCoord> FootprintTiles(CompartmentCatalogEntry entry, TileCoord anchor, int rotation)
    {
        var rotated = CompartmentPlacer.Rotate(entry, rotation);
        var tiles = new List<TileCoord>();
        foreach (var rect in rotated.FootprintRects)
            for (var x = (int)rect.X; x < (int)rect.Right; x++)
                for (var y = (int)rect.Y; y < (int)rect.Bottom; y++)
                    tiles.Add(new TileCoord(anchor.X + x, anchor.Y + y));
        return tiles;
    }

    // The anchor that puts the cursor tile at the middle of the footprint (what the editor does).
    public static TileCoord AnchorForCursor(CompartmentCatalogEntry entry, int rotation, TileCoord cursor)
    {
        var rotated = CompartmentPlacer.Rotate(entry, rotation);
        return new TileCoord(cursor.X - rotated.Width / 2, cursor.Y - rotated.Height / 2);
    }

    // `liveTiles` is the ship's current tile grid; `reserved` are tiles claimed by builds still under way.
    public static (CompartmentPlan? Plan, string? Error) Plan(TileGrid liveTiles, IReadOnlySet<TileCoord> reserved,
        CompartmentCatalogEntry entry, TileCoord anchor, int rotation)
    {
        var tiles = FootprintTiles(entry, anchor, rotation);
        var own = tiles.ToHashSet();
        if (tiles.Any(reserved.Contains))
            return (null, "Место занято строящимся отсеком.");

        var stamped = CompartmentPlacer.Stamp(liveTiles.Clone(), entry, anchor, rotation, FragmentInstance);
        if (!stamped.Success)
            return (null, "Место занято.");

        // It must touch the existing hull (a compartment floating on its own is no part of the ship): some footprint tile sits
        // right beside a tile that already has floor.
        var touches = tiles.Any(t => Neighbours(t).Any(n => !own.Contains(n) && liveTiles.CellAt(n) is { HasFloor: true }));
        if (!touches)
            return (null, "Отсек должен примыкать к корпусу.");

        var rotated = CompartmentPlacer.Rotate(entry, rotation);
        var scratch = new TileGrid();
        var result = CompartmentPlacer.Stamp(scratch, entry, anchor, rotation, FragmentInstance);
        if (!result.Success)
            return (null, result.Error ?? "Нельзя поставить отсек.");

        var kinds = result.Devices.ToDictionary(d => d.Coord, d => d.Kind);
        var rotations = result.Devices.Where(d => d.Rotated).ToDictionary(d => d.Coord, _ => true);
        var halfSides = result.Devices.Where(d => d.HalfSide is not null).ToDictionary(d => d.Coord, d => d.HalfSide!.Value);
        // Two engines sharing one control tile (a double engine) are exported as a pair.
        var engineGroups = result.Engines.GroupBy(e => e.ControlCoord).ToList();
        var engines = engineGroups.Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => new TileShipBuilder.EngineSpec(g.First().Facing, g.First().MaxThrust));
        var doubleEngines = engineGroups.Where(g => g.Count() == 2)
            .ToDictionary(g => g.Key, g => (new TileShipBuilder.EngineSpec(g.First().Facing, g.First().MaxThrust),
                new TileShipBuilder.EngineSpec(g.Last().Facing, g.Last().MaxThrust)));
        var rects = rotated.FootprintRects.Select(r => new RectF(r.X + anchor.X, r.Y + anchor.Y, r.Width, r.Height)).ToList();
        var compartments = new Dictionary<string, (IReadOnlySet<TileCoord> Tiles, IReadOnlyList<RectF> Rects, string DisplayName)>
        {
            [FragmentInstance] = (own, rects, entry.DisplayName),
        };

        var (fragment, errors) = TileShipBuilder.BuildDefinition(scratch, kinds, engines, entry.DisplayName, 0f, null, rotations, halfSides, doubleEngines, compartments);
        if (fragment is null || errors.Count > 0 || fragment.Rooms.Count != 1)
            return (null, errors.FirstOrDefault() ?? "Не удалось построить отсек.");

        var bounds = new RectF(rects.Min(r => r.Left), rects.Min(r => r.Top),
            rects.Max(r => r.Right) - rects.Min(r => r.Left), rects.Max(r => r.Bottom) - rects.Min(r => r.Top));
        return (new CompartmentPlan(entry, anchor, rotation, fragment, tiles, bounds), null);
    }

    // The ship's definition with the compartment appended (under a fresh room id), or null if the result is not a valid ship.
    public static CustomShipDefinition? Merge(CustomShipDefinition def, CompartmentPlan plan, string roomId)
    {
        var fragment = plan.Fragment;
        var room = fragment.Rooms[0] with { Id = roomId };
        var airlocks = fragment.Airlocks.Select(a => a with { RoomId = roomId });
        var merged = def with
        {
            Rooms = def.Rooms.Append(room).ToList(),
            Doors = def.Doors.Concat(fragment.Doors).ToList(),
            Airlocks = def.Airlocks.Concat(airlocks).ToList(),
            Devices = def.Devices.Concat(fragment.Devices).ToList(),
            Engines = def.Engines.Concat(fragment.Engines).ToList(),
            WallMaterials = def.WallMaterials.Concat(fragment.WallMaterials).ToList(),
            SupplementalWallTiles = def.SupplementalWallTiles.Concat(fragment.SupplementalWallTiles).ToList(),
            ForcedFloorTiles = def.ForcedFloorTiles.Concat(fragment.ForcedFloorTiles).ToList(),
            WallOpenSides = def.WallOpenSides.Concat(fragment.WallOpenSides).ToList(),
            DoorEdges = def.DoorEdges.Concat(fragment.DoorEdges).ToList(),
        };
        return CustomShipValidator.Validate(merged).Count > 0 ? null : merged;
    }

    public static string NextRoomId(IReadOnlyList<CustomRoomDef> rooms)
    {
        var max = 0;
        foreach (var room in rooms)
            if (room.Id.StartsWith("room-") && int.TryParse(room.Id.AsSpan(5), out var n) && n > max)
                max = n;
        return $"room-{max + 1}";
    }

    private static IEnumerable<TileCoord> Neighbours(TileCoord t)
    {
        yield return new TileCoord(t.X + 1, t.Y);
        yield return new TileCoord(t.X - 1, t.Y);
        yield return new TileCoord(t.X, t.Y + 1);
        yield return new TileCoord(t.X, t.Y - 1);
    }
}

// What a compartment costs (credits) and how much hull plating it eats. The catalog itself carries no economy; this derives it
// from the compartment's type and size, and sorts the compartments into the shipwright's category tabs.
public static class CompartmentPricing
{
    public static int Price(CompartmentCatalogEntry entry)
    {
        var perTile = entry.Type switch
        {
            CompartmentType.Reactor => 14,
            CompartmentType.Engine => 9,
            CompartmentType.Weapons => 10,
            CompartmentType.Cockpit => 11,
            CompartmentType.Docking => 9,
            CompartmentType.Distribution => 8,
            CompartmentType.LifeSupport => 7,
            CompartmentType.Engineering => 6,
            CompartmentType.Medical => 6,
            _ => 5,
        };
        return TileArea(entry) * perTile;
    }

    public static int PlatingCost(CompartmentCatalogEntry entry) => Math.Max(1, (int)Math.Ceiling(TileArea(entry) / 12.0));

    // Demolishing returns half of what the compartment cost.
    public static int Refund(CompartmentCatalogEntry entry) => Price(entry) / 2;

    public static RoomCategory CategoryOf(CompartmentType type) => type switch
    {
        CompartmentType.Reactor or CompartmentType.Distribution or CompartmentType.Engineering => RoomCategory.Power,
        CompartmentType.Engine => RoomCategory.Propulsion,
        CompartmentType.Weapons => RoomCategory.Weapons,
        CompartmentType.LifeSupport or CompartmentType.Medical or CompartmentType.CrewQuarters => RoomCategory.Crew,
        CompartmentType.Cockpit => RoomCategory.Sensors,
        _ => RoomCategory.Structural, // Docking
    };

    // The catalog entry a built room came from, by the name the builder gave it.
    public static CompartmentCatalogEntry? EntryForRoomName(string roomName) =>
        CompartmentCatalog.Entries.FirstOrDefault(e => e.DisplayName == roomName);

    private static int TileArea(CompartmentCatalogEntry entry) =>
        entry.FootprintRects.Sum(r => (int)r.Width * (int)r.Height);
}
