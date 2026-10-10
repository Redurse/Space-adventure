namespace Anabiosis.Shared.Model;

// Why a junction door could not be placed - drives the editor's own explanatory toast.
public enum JunctionDoorFailure
{
    None,
    NotAWall,
    NotHalfBlock,
    TooThick,
    BadBoundary,
    NotEveryTileHalfBlock,
    NoSideWall,
    WallDevice,
}

// What a junction door would do: the half-block wall tiles it clears (each becomes plain floor) and
// the door edges that go across the opening.
public sealed record JunctionDoorPlan(
    IReadOnlyList<TileCoord> WallTiles,
    IReadOnlyList<(TileCoord Coord, TileSide Side)> Edges);

// A wall tile a junction door removed, kept so deleting the door can put it back exactly as it was.
public readonly record struct SavedJunctionWall(TileCoord Coord, TileSide OpenSide, WallMaterial Material, float Hp, bool FromCompartment);

// Direct user request - a single/double/triple door can be put on the junction of two compartments
// (or on a compartment's wall against open space), but only where the wall there is made of half-block
// walls, and only where the wall carries on past both sides of the door (a door is a gap IN a wall, it
// never stands on its own).
//
// Geometry ("вариант Б"): the wall layers across the passage are removed entirely and become floor, and
// a normal door edge (TileGrid.DoorEdges - the same barrier the plain door tools use) goes across the
// middle of the opening. Two compartments touching leave a 2-tile-thick double wall, so the opening is
// 2 tiles deep with the door in its middle seam; a single wall against open space gives a 1-tile
// opening with the door on its outer edge (an airlock - Ship.Custom.cs turns an edge onto open space
// into one by itself).
//
// Reusing the edge door means nothing downstream has to know this exists: the export, the live ship's
// movement/atmosphere/lighting and the door toggling already handle an edge door. The only extra state
// is the removed walls, kept (SavedJunctionWall) so that deleting the door restores them.
public static class JunctionDoor
{
    // passage is the direction a character walks through the door (East or South); the door's width
    // then runs along the other axis (down for East, right for South) from the anchor tile.
    public static JunctionDoorPlan? Plan(TileGrid grid, TileCoord anchor, TileSide passage, int span, out JunctionDoorFailure failure)
    {
        if (passage is not (TileSide.East or TileSide.South))
            throw new ArgumentOutOfRangeException(nameof(passage), "Passage must be East or South.");

        failure = JunctionDoorFailure.None;
        var back = passage.Opposite();
        var spanOffset = passage == TileSide.East ? new TileCoord(0, 1) : new TileCoord(1, 0);
        TileCoord Shift(TileCoord c, int steps) => new(c.X + spanOffset.X * steps, c.Y + spanOffset.Y * steps);

        bool IsSolid(TileCoord c) => grid.CellAt(c) is { Wall: TileWallKind.Solid };
        bool HasWallDevice(TileCoord c) => grid.CellAt(c) is { } cell && (cell.WallDeviceId is not null || cell.DeviceId is not null || cell.DeviceId2 is not null);
        bool IsHalfBlockWall(TileCoord c) => grid.CellAt(c) is { HasFloor: true, Wall: TileWallKind.Solid, WallOpenSide: not null } && !HasWallDevice(c);
        // Bare floor, no device - what a door edge needs on its flanking tiles.
        bool IsFree(TileCoord c) => grid.CellAt(c) is { HasFloor: true, Wall: TileWallKind.None, DeviceId: null, DeviceId2: null };
        // Any floor with no wall on it - a device standing in front of the doorway is allowed.
        bool IsFloorTile(TileCoord c) => grid.CellAt(c) is { HasFloor: true, Wall: TileWallKind.None };
        bool IsSpace(TileCoord c) => grid.CellAt(c) is null or { HasFloor: false, Wall: TileWallKind.None, DeviceId: null };

        if (!IsSolid(anchor))
        {
            failure = JunctionDoorFailure.NotAWall;
            return null;
        }
        if (!IsHalfBlockWall(anchor))
        {
            failure = HasWallDevice(anchor) ? JunctionDoorFailure.WallDevice : JunctionDoorFailure.NotHalfBlock;
            return null;
        }

        // The run of half-block wall layers the anchor sits in, measured along the passage.
        var start = anchor;
        var end = anchor;
        var layers = 1;
        while (IsHalfBlockWall(back.Offset(start)) && layers <= 2)
        {
            start = back.Offset(start);
            layers++;
        }
        while (IsHalfBlockWall(passage.Offset(end)) && layers <= 2)
        {
            end = passage.Offset(end);
            layers++;
        }
        if (layers > 2)
        {
            failure = JunctionDoorFailure.TooThick;
            return null;
        }

        var before = back.Offset(start);
        var after = passage.Offset(end);
        // What the run is bounded by: free floor (F), open space (S), or anything else (X).
        char Kind(TileCoord c) => IsFloorTile(c) ? 'F' : IsSpace(c) ? 'S' : 'X';
        var beforeKind = Kind(before);
        var afterKind = Kind(after);
        var boundaryOk = layers == 2
            ? beforeKind == 'F' && afterKind == 'F'
            : (beforeKind, afterKind) is ('F', 'F') or ('F', 'S') or ('S', 'F');
        if (!boundaryOk)
        {
            failure = JunctionDoorFailure.BadBoundary;
            return null;
        }

        // The run's own tiles, in passage order, relative to the anchor's run start.
        var runTiles = new List<TileCoord>();
        for (var t = start; ; t = passage.Offset(t))
        {
            runTiles.Add(t);
            if (t == end)
                break;
        }

        // Where the door edge goes, on the first span position: in the middle of a 2-deep opening, on
        // the outer edge of a 1-deep opening that borders space, otherwise on the far side of the
        // cleared tile. Expressed as (tile, side) so every other position is just the same shifted.
        var edgeAnchor = layers == 2 ? (start, passage)
            : beforeKind == 'S' ? (before, passage)
            : (start, passage);

        var wallTiles = new List<TileCoord>();
        var edges = new List<(TileCoord, TileSide)>();
        for (var i = 0; i < span; i++)
        {
            foreach (var tile in runTiles)
            {
                var shifted = Shift(tile, i);
                if (!IsHalfBlockWall(shifted))
                {
                    failure = HasWallDevice(shifted) ? JunctionDoorFailure.WallDevice : JunctionDoorFailure.NotEveryTileHalfBlock;
                    return null;
                }
                wallTiles.Add(shifted);
            }
            if (Kind(Shift(before, i)) != beforeKind || Kind(Shift(after, i)) != afterKind)
            {
                failure = JunctionDoorFailure.BadBoundary;
                return null;
            }
            // A 1-deep opening between two floors puts its door edge against the far tile, which must be
            // bare floor for the edge to fit.
            if (layers == 1 && beforeKind == 'F' && afterKind == 'F' && !IsFree(Shift(after, i)))
            {
                failure = JunctionDoorFailure.BadBoundary;
                return null;
            }
            edges.Add((Shift(edgeAnchor.Item1, i), edgeAnchor.Item2));
        }

        // The wall has to carry on past both ends of the door, in every layer it cuts through.
        foreach (var tile in runTiles)
            if (!IsSolid(Shift(tile, -1)) || !IsSolid(Shift(tile, span)))
            {
                failure = JunctionDoorFailure.NoSideWall;
                return null;
            }

        return new JunctionDoorPlan(wallTiles, edges);
    }

    // Clears the plan's wall tiles to floor and puts the door edges across the opening, all under one
    // id. Returns the removed walls (for Restore), or null - leaving the grid exactly as it was - if
    // the edges turned out not to fit.
    public static IReadOnlyList<SavedJunctionWall>? Apply(TileGrid grid, JunctionDoorPlan plan, string id)
    {
        var saved = plan.WallTiles
            .Select(c =>
            {
                var cell = grid.CellAt(c)!;
                return new SavedJunctionWall(c, cell.WallOpenSide ?? TileSide.North, cell.WallMaterial, cell.WallHp, cell.WallFromCompartment);
            })
            .ToList();

        foreach (var coord in plan.WallTiles)
            grid.SetWall(coord, TileWallKind.None);

        if (plan.Edges.Any(e => !grid.CanPlaceDoorEdge(e.Coord, e.Side)))
        {
            Restore(grid, saved);
            return null;
        }
        foreach (var (coord, side) in plan.Edges)
            grid.AddDoorEdge(coord, side, id);
        return saved;
    }

    // Puts removed walls back as half-block walls. A tile that has since been turned into something
    // else (a device on it, a different wall) is left alone.
    public static void Restore(TileGrid grid, IEnumerable<SavedJunctionWall> walls)
    {
        foreach (var wall in walls)
        {
            if (grid.CellAt(wall.Coord) is not { HasFloor: true, Wall: TileWallKind.None, DeviceId: null, DeviceId2: null, WallDeviceId: null })
                continue;
            grid.SetWall(wall.Coord, TileWallKind.Solid, wall.Hp, wall.Material, wall.FromCompartment);
            grid.SetWallOpenSide(wall.Coord, wall.OpenSide);
        }
    }
}
