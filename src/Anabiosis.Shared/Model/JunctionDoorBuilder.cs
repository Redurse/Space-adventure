namespace Anabiosis.Shared.Model;

// Placing and removing a junction door (single/double/triple, JunctionDoor.cs) on a LIVE ship, expressed as changes to its
// definition: the door's edges join DoorEdges, the half-block wall tiles it clears are forced open (ForcedFloorTiles, and dropped
// from WallOpenSides) and remembered (JunctionWalls) so that removing the door restores them. Rebuilding the ship from the new
// definition (Ship.FromCustomDefinition) then yields exactly what JunctionDoor.Apply does to a tile grid.
public static class JunctionDoorBuilder
{
    // The door's price by width.
    public static int Price(int span) => span switch { 1 => 40, 2 => 70, _ => 100 };

    public static (CustomShipDefinition? Definition, JunctionDoorFailure Failure) Place(Ship ship, CustomShipDefinition def,
        TileCoord anchor, TileSide passage, int span, string doorId)
    {
        var plan = JunctionDoor.Plan(ship.Tiles, anchor, passage, span, out var failure);
        if (plan is null)
            return (null, failure);

        var cleared = plan.WallTiles.ToHashSet();
        var saved = plan.WallTiles
            .Select(c => new CustomJunctionWallDef(c.X, c.Y, ship.Tiles.CellAt(c)?.WallOpenSide ?? TileSide.North, doorId))
            .ToList();
        var updated = def with
        {
            DoorEdges = def.DoorEdges.Concat(plan.Edges.Select(e => new CustomDoorEdgeDef(e.Coord.X, e.Coord.Y, e.Side, doorId))).ToList(),
            ForcedFloorTiles = def.ForcedFloorTiles.Concat(plan.WallTiles).ToList(),
            WallOpenSides = def.WallOpenSides.Where(o => !cleared.Contains(new TileCoord(o.X, o.Y))).ToList(),
            JunctionWalls = def.JunctionWalls.Concat(saved).ToList(),
        };
        return (updated, JunctionDoorFailure.None);
    }

    // Takes the door out and puts its walls back. Only doors placed through Place can be removed (they are the ones with
    // remembered walls); null otherwise.
    public static CustomShipDefinition? Remove(CustomShipDefinition def, string doorId)
    {
        var walls = def.JunctionWalls.Where(w => w.DoorId == doorId).ToList();
        if (walls.Count == 0 || def.DoorEdges.All(e => e.Id != doorId))
            return null;

        var coords = walls.Select(w => new TileCoord(w.X, w.Y)).ToHashSet();
        return def with
        {
            DoorEdges = def.DoorEdges.Where(e => e.Id != doorId).ToList(),
            ForcedFloorTiles = def.ForcedFloorTiles.Where(t => !coords.Contains(t)).ToList(),
            WallOpenSides = def.WallOpenSides.Concat(walls.Select(w => new CustomWallOpenSideDef(w.X, w.Y, w.OpenSide))).ToList(),
            JunctionWalls = def.JunctionWalls.Where(w => w.DoorId != doorId).ToList(),
        };
    }

    public static string NextDoorId(CustomShipDefinition def)
    {
        var max = 0;
        foreach (var edge in def.DoorEdges)
            if (edge.Id.StartsWith("jdoor-") && int.TryParse(edge.Id.AsSpan(6), out var n) && n > max)
                max = n;
        return $"jdoor-{max + 1}";
    }
}
