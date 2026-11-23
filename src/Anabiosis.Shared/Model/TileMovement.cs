namespace Anabiosis.Shared.Model;

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

    // What one character remembers between moves: whether they are in the middle of squeezing through a gap. In that state a spot where the
    // full clearance box does not fit is a slot they are standing IN, not a wall they are stuck inside - the old "stuck in a wall, let them out in
    // any direction" rule must not fire there (it let them walk straight through the slot's walls).
    public sealed class MoveMemory
    {
        public bool Squeezed;
    }

    public static Vec2 MoveAlongAxis(TileGrid tiles, Vec2 position, Vec2 delta, IReadOnlyList<RoomLayout.RoomObstacle>? obstacles = null,
        MoveMemory? memory = null)
    {
        var next = position + delta;
        if (IsClear(tiles, position, next) && !BlocksPosition(obstacles, next))
        {
            if (memory is not null)
                memory.Squeezed = false;
            return next;
        }

        // Not through the front door: a person can turn sideways. The free half of a half-block wall (and any other gap about half a tile
        // wide) is narrower than the clearance box, but not narrower than a body seen edge-on - see TrySqueeze.
        if (TrySqueeze(tiles, position, delta, obstacles) is { } squeezed)
        {
            if (memory is not null)
                memory.Squeezed = true;
            return squeezed;
        }

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
        // (A spot that is only clear sideways - the character is in the middle of squeezing through a gap - is not "inside the wall".)
        // Narrow in EITHER direction counts: in a slot only half a tile wide the character is clear sideways along the slot, and pressing the
        // other way must find them blocked by its walls - not "stuck inside a wall", which is let out in any direction (that was walking through textures).
        if (!IsClear(tiles, position, position))
        {
            // Mid-squeeze, in a slot: not stuck in a wall - the slot's own walls still hold, and the way out is the squeeze above.
            var inSlot = memory is { Squeezed: true }
                && (IsClear(tiles, position, position, (SqueezeCrossRadius, CharacterRadius)) || IsClear(tiles, position, position, (CharacterRadius, SqueezeCrossRadius)));
            if (!inSlot)
                return next;
        }

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
            if (IsClear(tiles, position, candidate) && !BlocksPosition(obstacles, candidate))
                lo = mid;
            else
                hi = mid;
        }
        return position + delta * lo;
    }

    // Every tile the character's own clearance box could possibly overlap - at most a 2x2 tile
    // area, since CharacterRadius*2 (0.7) is less than one tile unit, so sampling all four corners
    // of that box is guaranteed to touch every tile the box actually intersects. `anchor` is the
    // start-of-tick position this whole move began from (the SAME fixed point across the entire
    // binary search in MoveAlongAxis, never a previous candidate) - direct user request ("я хочу
    // полностью переделать двери", the edge-based narrow door): a door edge sits between two tiles
    // rather than occupying one, so per-tile walkability alone can never see it - for each sampled
    // corner, if moving from `anchor` to `position` actually crosses into a different tile, that
    // specific crossing must also clear TileGrid.IsWalkableAcrossEdge. Passing anchor == position
    // (both "is my current spot clear" checks in MoveAlongAxis do this) naturally skips the edge
    // check entirely - nothing crosses if nothing moved.
    private static bool IsClear(TileGrid tiles, Vec2 anchor, Vec2 position) => IsClear(tiles, anchor, position, (CharacterRadius, CharacterRadius));

    // Sideways: the box is as deep as ever along the way of travel, but only SqueezeRadius wide across it.
    private const float SqueezeCrossRadius = 0.2f;     // 0.4 across: fits the 0.5 free half of a half-block wall
    private const float SqueezeMaxNudge = 0.32f;       // how far off the line of travel an opening may lie and still be found
    private const float SqueezeNudgeStep = 0.05f;      // how far one move may slide toward it

    private static (float X, float Y) SqueezeRadius(Vec2 delta, bool narrow) =>
        !narrow ? (CharacterRadius, CharacterRadius)
        : Math.Abs(delta.X) >= Math.Abs(delta.Y) ? (CharacterRadius, SqueezeCrossRadius) : (SqueezeCrossRadius, CharacterRadius);

    // The move that goes through a gap too narrow for the clearance box but wide enough for a body turned sideways: straight on if the
    // narrow box is clear, or - when the gap lies a little to one side - a small slide toward it, so that holding the key against the
    // edge of a half-block wall brings the character to the opening instead of leaving them stuck on its corner. Null when there is no gap.
    private static Vec2? TrySqueeze(TileGrid tiles, Vec2 position, Vec2 delta, IReadOnlyList<RoomLayout.RoomObstacle>? obstacles)
    {
        if (delta.X == 0 && delta.Y == 0)
            return null;
        var radius = SqueezeRadius(delta, narrow: true);
        var next = position + delta;
        if (IsClear(tiles, position, next, radius) && !BlocksPosition(obstacles, next))
            return next;

        // Only from a spot that is itself clear (full or sideways): a character stuck inside a wall is for MoveAlongAxis to free.
        if (!IsClear(tiles, position, position, radius))
            return null;

        var across = Math.Abs(delta.X) >= Math.Abs(delta.Y) ? new Vec2(0, 1) : new Vec2(1, 0);
        for (var offset = SqueezeNudgeStep; offset <= SqueezeMaxNudge + 1e-4f; offset += SqueezeNudgeStep)
            foreach (var sign in new[] { 1f, -1f })
            {
                var target = next + across * (sign * offset);
                if (!IsClear(tiles, position, target, radius) || BlocksPosition(obstacles, target))
                    continue;
                var slide = position + across * (sign * Math.Min(offset, SqueezeNudgeStep));
                if (IsClear(tiles, position, slide, radius) && !BlocksPosition(obstacles, slide))
                    return slide;
            }
        return null;
    }

    private static bool IsClear(TileGrid tiles, Vec2 anchor, Vec2 position, (float X, float Y) radius)
    {
        foreach (var (sx, sy) in Samples)
        {
            var dx = sx * radius.X;
            var dy = sy * radius.Y;
            var cx = position.X + dx;
            var cy = position.Y + dy;
            var coord = new TileCoord((int)Math.Floor(cx), (int)Math.Floor(cy));
            if (tiles.CellAt(coord) is not { } cell || !TileGrid.IsWalkable(cell, coord, new Vec2(cx, cy)))
                return false;

            var ax = anchor.X + dx;
            var ay = anchor.Y + dy;
            var anchorCoord = new TileCoord((int)Math.Floor(ax), (int)Math.Floor(ay));
            if (anchorCoord != coord && !tiles.IsWalkableAcrossEdge(anchorCoord, coord))
                return false;
        }
        return true;
    }

    // Corners alone are NOT enough (direct user bug report - "через малую сторону полублочной стены
    // можно пройти внутрь"): a half-block wall's solid half (or a half-width device's) is only 0.5
    // units thick, thinner than the 0.7-wide clearance box, so walking head-on into its narrow end
    // with the box centred on it put one corner column either side of the strip and none inside it.
    // A 3x3 grid (corners + edge midpoints + centre) spaces samples 0.35 apart on each axis - less
    // than the thinnest blocked region (0.5) - so any blocked region the box overlaps always
    // contains at least one sample.
    // Unit offsets of the 3x3 samples, scaled by the box's half-width and half-depth.
    private static readonly (float, float)[] Samples =
    {
        (-1f, -1f), (0f, -1f), (1f, -1f),
        (-1f, 0f), (0f, 0f), (1f, 0f),
        (-1f, 1f), (0f, 1f), (1f, 1f),
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
