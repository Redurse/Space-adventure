namespace Anabiosis.Shared.Model;

// How far a shot gets through a deck before something solid stops it. A bullet goes in a straight line through
// open floor and open (or broken) doors; it stops in the first standing wall, at a closed door, or where the
// deck ends. Devices do not stop it. The same walk serves all three places a gun can be fired - the ship, a
// boarded hull and a station - since each of them is a TileGrid.
public static class ShotTrace
{
    public const float Step = 0.1f;

    public readonly record struct Result(float Distance, TileCoord? Wall);

    // Distance is how far the shot travelled before stopping (maxDistance if nothing stopped it); Wall is the
    // tile of the wall it stopped in, null when it was a door, the edge of the deck, or nothing at all.
    public static Result ToWall(TileGrid tiles, Vec2 from, Vec2 direction, float maxDistance)
    {
        var previous = Coord(from);
        for (var travelled = Step; travelled <= maxDistance; travelled += Step)
        {
            var point = from + direction * travelled;
            var coord = Coord(point);
            if (tiles.CellAt(coord) is not { } cell || (!cell.HasFloor && cell.Wall == TileWallKind.None))
                return new Result(travelled, null); // off the deck
            // A door that takes up a tile of its own stops the shot only while it is shut and standing; a wall, only
            // in the half of the tile it actually fills.
            if (cell.Wall == TileWallKind.Door && !cell.DoorOpen && cell.WallHp > 0)
                return new Result(travelled, null);
            if (cell.Wall == TileWallKind.Solid && cell.WallHp > 0
                && (cell.WallOpenSide is not { } openSide || TileGrid.IsInSolidHalf(coord, point, openSide)))
                return new Result(travelled, coord);
            if (coord != previous && !tiles.IsWalkableAcrossEdge(previous, coord))
                return new Result(travelled, null); // a closed door
            previous = coord;
        }
        return new Result(maxDistance, null);
    }

    private static TileCoord Coord(Vec2 point) => new((int)Math.Floor(point.X), (int)Math.Floor(point.Y));
}
