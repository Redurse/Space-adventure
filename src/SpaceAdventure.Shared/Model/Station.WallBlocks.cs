namespace SpaceAdventure.Shared.Model;

// One 1x1 block per unit segment of a room's boundary that has no neighboring room on that side -
// the exact same rule Ship.Custom.cs's BuildWallBlocks/IsUnitCovered applies to a player-drawn
// hull, just re-derived here against Room instead of CustomRoomDef (the two share every field but
// aren't the same type, so the loop can't be shared directly without a generic rewrite - not worth
// it for one extra caller). Any block that lands on a Door or the ShipConnector's own footprint is
// dropped afterward, same as Ship.cs's constructor does for its own wall blocks.
public sealed partial class Station
{
    // internal rather than private: EnemyShipLayout.cs reuses this verbatim for its own hull's
    // cuttable wall blocks - same "derive purely from room geometry" rule, just a different
    // structure's Rooms/Doors/connectors triple.
    // Generalized (humble-soaring-cat.md M90) to walk each room's own subrects independently -
    // byte-identical to the old per-room walk whenever every room has exactly one rect (every
    // station/enemy hull today, since neither builds multi-rect rooms yet) since room.Rects then
    // has exactly one element equal to the bbox.
    internal static List<WallBlock> BuildWallBlocks(IReadOnlyList<Room> rooms, IReadOnlyList<Door> doors, IReadOnlyList<AirlockOuterDoor> shipConnectors)
    {
        var blocks = new List<WallBlock>();
        foreach (var room in rooms)
        {
            var index = 0;
            foreach (var rect in room.Rects)
            {
                for (var x = rect.X; x < rect.Right; x += 1f)
                    if (!IsUnitCovered(rooms, room, rect, EdgeSide.Top, x))
                        blocks.Add(new WallBlock($"{room.Id}-wall-{index++}", room.Id, x + 0.5f, rect.Y));
                for (var x = rect.X; x < rect.Right; x += 1f)
                    if (!IsUnitCovered(rooms, room, rect, EdgeSide.Bottom, x))
                        blocks.Add(new WallBlock($"{room.Id}-wall-{index++}", room.Id, x + 0.5f, rect.Bottom));
                for (var y = rect.Y; y < rect.Bottom; y += 1f)
                    if (!IsUnitCovered(rooms, room, rect, EdgeSide.Left, y))
                        blocks.Add(new WallBlock($"{room.Id}-wall-{index++}", room.Id, rect.X, y + 0.5f));
                for (var y = rect.Y; y < rect.Bottom; y += 1f)
                    if (!IsUnitCovered(rooms, room, rect, EdgeSide.Right, y))
                        blocks.Add(new WallBlock($"{room.Id}-wall-{index++}", room.Id, rect.Right, y + 0.5f));
            }
        }
        return blocks
            .Where(b => !doors.Any(d => d.Contains(b.Position)) && !shipConnectors.Any(c => c.Contains(b.Position)))
            .ToList();
    }

    // True when this 1-unit segment (at `unitStart` along `side` of `rect`, one of `room`'s own
    // subrects) is covered by SOME other piece of floor - either one of `room`'s OWN other subrects
    // (an internal seam) or a different room's subrect (a genuine shared interior boundary). Used
    // symmetrically on all 4 sides here (a covered segment on EITHER side of a boundary just gets no
    // WallBlock object at all, on either room - this function only decides whether a hull-cutting
    // WallBlock exists, unlike TileGridRasterizer's own wall-TILE pass below, which needs exactly
    // one physical tile per boundary and so treats leading/trailing edges asymmetrically).
    internal static bool IsUnitCovered(IReadOnlyList<Room> rooms, Room room, RectF rect, EdgeSide side, float unitStart) =>
        IsUnitCoveredBySameRoom(room, rect, side, unitStart) || IsUnitCoveredByOtherRoom(rooms, room, rect, side, unitStart);

    internal static bool IsUnitCoveredBySameRoom(Room room, RectF rect, EdgeSide side, float unitStart)
    {
        foreach (var other in room.Rects)
            if (other != rect && CoversUnit(other, rect, side, unitStart))
                return true;
        return false;
    }

    internal static bool IsUnitCoveredByOtherRoom(IReadOnlyList<Room> rooms, Room room, RectF rect, EdgeSide side, float unitStart)
    {
        foreach (var other in rooms)
        {
            if (other.Id == room.Id)
                continue;
            foreach (var otherRect in other.Rects)
                if (CoversUnit(otherRect, rect, side, unitStart))
                    return true;
        }
        return false;
    }

    private static bool CoversUnit(RectF other, RectF rect, EdgeSide side, float unitStart) => side switch
    {
        EdgeSide.Top => other.Bottom == rect.Y && other.X <= unitStart && other.Right >= unitStart + 1f,
        EdgeSide.Bottom => other.Y == rect.Bottom && other.X <= unitStart && other.Right >= unitStart + 1f,
        EdgeSide.Left => other.Right == rect.X && other.Y <= unitStart && other.Bottom >= unitStart + 1f,
        EdgeSide.Right => other.X == rect.Right && other.Y <= unitStart && other.Bottom >= unitStart + 1f,
        _ => false,
    };
}
