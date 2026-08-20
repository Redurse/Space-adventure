namespace SpaceAdventure.Shared.Model;

// One 1x1 block per unit segment of a room's boundary that has no neighboring room on that side -
// the exact same rule Ship.Custom.cs's BuildWallBlocks/IsUnitCovered applies to a player-drawn
// hull, just re-derived here against Room instead of CustomRoomDef (the two share every field but
// aren't the same type, so the loop can't be shared directly without a generic rewrite - not worth
// it for one extra caller). Any block that lands on a Door or the ShipConnector's own footprint is
// dropped afterward, same as Ship.cs's constructor does for its own wall blocks.
public sealed partial class Station
{
    private static List<WallBlock> BuildWallBlocks(IReadOnlyList<Room> rooms, IReadOnlyList<Door> doors, AirlockOuterDoor shipConnector)
    {
        var blocks = new List<WallBlock>();
        foreach (var room in rooms)
        {
            var index = 0;
            for (var x = room.Left; x < room.Right; x += 1f)
                if (!IsUnitCovered(rooms, room, EdgeSide.Top, x))
                    blocks.Add(new WallBlock($"{room.Id}-wall-{index++}", room.Id, x + 0.5f, room.Top));
            for (var x = room.Left; x < room.Right; x += 1f)
                if (!IsUnitCovered(rooms, room, EdgeSide.Bottom, x))
                    blocks.Add(new WallBlock($"{room.Id}-wall-{index++}", room.Id, x + 0.5f, room.Bottom));
            for (var y = room.Top; y < room.Bottom; y += 1f)
                if (!IsUnitCovered(rooms, room, EdgeSide.Left, y))
                    blocks.Add(new WallBlock($"{room.Id}-wall-{index++}", room.Id, room.Left, y + 0.5f));
            for (var y = room.Top; y < room.Bottom; y += 1f)
                if (!IsUnitCovered(rooms, room, EdgeSide.Right, y))
                    blocks.Add(new WallBlock($"{room.Id}-wall-{index++}", room.Id, room.Right, y + 0.5f));
        }
        return blocks
            .Where(b => !doors.Any(d => d.Contains(b.Position)) && !shipConnector.Contains(b.Position))
            .ToList();
    }

    private static bool IsUnitCovered(IReadOnlyList<Room> rooms, Room room, EdgeSide side, float unitStart)
    {
        foreach (var other in rooms)
        {
            if (other.Id == room.Id)
                continue;
            var covers = side switch
            {
                EdgeSide.Top => other.Bottom == room.Top && other.Left <= unitStart && other.Right >= unitStart + 1f,
                EdgeSide.Bottom => other.Top == room.Bottom && other.Left <= unitStart && other.Right >= unitStart + 1f,
                EdgeSide.Left => other.Right == room.Left && other.Top <= unitStart && other.Bottom >= unitStart + 1f,
                EdgeSide.Right => other.Left == room.Right && other.Top <= unitStart && other.Bottom >= unitStart + 1f,
                _ => false,
            };
            if (covers)
                return true;
        }
        return false;
    }
}
