namespace SpaceAdventure.Shared.Model;

// M73 (humble-soaring-cat.md) - replaces RoomLayout.MoveAlongAxis's room-rectangle clamp with a
// direct per-tile walkability check against a TileGrid (M70/TileGrid.cs). Movement no longer needs
// to know which "room" a character is in at all to decide whether a move is legal - the character's
// absolute position plus the tile grid is enough, since TileGrid.IsWalkable already folds in
// open/closed doors and breached walls (a fully breached wall or door tile is walkable regardless of
// its own open/closed flag - a deliberate simplification over the old model's separate "two adjacent
// broken WallBlocks" passable-breach rule, see World.WallBlocks.cs's IsPassableBreach: with tiles
// already granular at 1 unit, a single breached tile IS a full 1-unit-wide gap, no adjacency pairing
// needed).
//
// `RoomId` itself is NOT going away in this milestone - it's still a plain Room.Id string, read
// directly (not just via movement) by oxygen, AI targeting, boarding, save/load and hull-swap/
// purchase code (World.Movement.cs's own callers found ~10 such sites). So this file also exposes
// RoomIdAt: a straight point -> region -> room lookup (matching a room's own center tile against a
// candidate region, the same technique M71's round-trip tests already use) for whoever needs to
// convert a moved-to position back into the Room.Id everything else still expects. Deliberately NOT
// cached per-structure: a fully breached interior wall merges two rooms into one SealedRegion (see
// TileGrid's own region-merge logic), so which region a room's center tile belongs to can genuinely
// change as the ship takes damage - recomputing per call keeps this correct without needing to
// invalidate a cache on every wall hit.
public static class TileMovement
{
    public const float CharacterRadius = RoomLayout.CharacterRadius;

    public static Vec2 MoveAlongAxis(TileGrid tiles, Vec2 position, Vec2 delta, IReadOnlyList<RoomLayout.RoomObstacle>? obstacles = null)
    {
        var next = position + delta;
        if (IsClear(tiles, next) && !BlocksPosition(obstacles, next))
            return next;

        // A wall is now a genuine, full 1-unit-thick tile (not the old model's zero-width line at
        // the room's own rectangle edge), so a position the OLD system considered perfectly legal -
        // as close as CharacterRadius to a room's boundary - can land INSIDE the new wall tile
        // instead. That's not hypothetical: it's been hit live, via the docked-merged movement path
        // (World.Movement.cs's MoveInCurrentStructure), which deliberately still runs the old
        // RoomLayout system and can leave a character sitting at exactly that old clamp value the
        // moment they undock and this tile-based path takes over. If the CURRENT position already
        // fails the clearance check, don't also refuse to move away from it - that would trap the
        // character in the wall forever, since every subsequent move would fail the same check for
        // the same reason. Only enforce "stay clear" once the character is actually in a clear spot.
        if (!IsClear(tiles, position))
            return next;

        // Slide as close to the wall as this move can get, rather than freezing at the pre-move
        // position outright - binary search the farthest fraction of `delta` that stays clear, the
        // same "stop right at the wall's face" precision the old room-rectangle clamp had (down to
        // the same CharacterRadius clearance), just found by search instead of solving the room-edge
        // arithmetic directly - this needs no per-direction case analysis and works identically for
        // every obstacle shape this function checks (tile walls, RoomObstacle rectangles alike).
        var lo = 0f;
        var hi = 1f;
        for (var i = 0; i < 20; i++) // far finer than any per-tick delta needs to be resolved to
        {
            var mid = (lo + hi) / 2f;
            var candidate = position + delta * mid;
            if (IsClear(tiles, candidate) && !BlocksPosition(obstacles, candidate))
                lo = mid;
            else
                hi = mid;
        }
        return position + delta * lo;
    }

    // Every tile the character's own clearance box could possibly overlap - at most a 2x2 tile
    // area, since CharacterRadius*2 (0.7) is less than one tile unit, so sampling all four corners
    // of that box is guaranteed to touch every tile the box actually intersects.
    private static bool IsClear(TileGrid tiles, Vec2 position)
    {
        foreach (var (dx, dy) in Corners)
        {
            var coord = new TileCoord((int)Math.Floor(position.X + dx), (int)Math.Floor(position.Y + dy));
            if (tiles.CellAt(coord) is not { } cell || !TileGrid.IsWalkable(cell))
                return false;
        }
        return true;
    }

    private static readonly (float, float)[] Corners =
    {
        (-CharacterRadius, -CharacterRadius), (CharacterRadius, -CharacterRadius),
        (-CharacterRadius, CharacterRadius), (CharacterRadius, CharacterRadius),
    };

    // Ship.DeviceObstacles (the reactor console in catalog-built rooms) isn't represented as a
    // Device tile yet - that lands with M74's device ECS. Kept alive here, unscoped from any
    // particular room (an obstacle's own absolute bounds never coincide with a different room's
    // anyway), so that collision doesn't silently regress in the gap between M73 and M74.
    private static bool BlocksPosition(IReadOnlyList<RoomLayout.RoomObstacle>? obstacles, Vec2 p) =>
        obstacles is not null && obstacles.Any(o =>
            p.X >= o.Center.X - o.HalfExtents.X - CharacterRadius && p.X <= o.Center.X + o.HalfExtents.X + CharacterRadius &&
            p.Y >= o.Center.Y - o.HalfExtents.Y - CharacterRadius && p.Y <= o.Center.Y + o.HalfExtents.Y + CharacterRadius);

    // Deliberately NOT region-based: two rooms whose shared interior wall gets fully breached down
    // to 0 HP merge into one SealedRegion (by design - see TileGrid's own region-merge logic), which
    // would make "which room's center tile shares my region" ambiguous the moment combat damages an
    // interior bulkhead - every room fused into that one region would answer the lookup identically,
    // regardless of which one the character is actually standing in (found live: a character who'd
    // walked clear across the ship to "quarters" kept reporting RoomId "cockpit" once enough interior
    // walls had been shot through during a long fight, silently failing every RoomId-gated
    // interaction - turret manning, oxygen, AI targeting - from then on). Rooms are still simple,
    // non-overlapping rectangles at this milestone (M76 is what makes that stop being true), so a
    // direct Room.Contains check is both simpler and immune to this - it never needs the tile grid at
    // all for this particular question.
    public static string? RoomIdAt(IReadOnlyList<Room> rooms, Vec2 position)
    {
        foreach (var room in rooms)
            if (room.Contains(position))
                return room.Id;
        return null;
    }
}
