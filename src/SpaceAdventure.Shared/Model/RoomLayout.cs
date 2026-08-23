namespace SpaceAdventure.Shared.Model;

// Shared room/door collision algorithm - used by both Ship and Station, which each have their own
// independent Rooms/Doors list but need the exact same "stay inside the room, cross only through
// an open aligned door" movement rule (see Ship.MoveAlongAxis's own doc comment for the rule
// itself). Extracted here once both types needed it, rather than duplicating it.
public static class RoomLayout
{
    // Half the character's own drawn footprint (matches Client ShipRenderer.CharacterDiameter/2).
    // A bare point-position let the character's center reach a room's exact geometric edge, which
    // is the wall's own centerline (ShipRenderer.DrawWallBand draws the wall straddling that same
    // line) - so the character's actual body, wider than that clearance, sank into the wall band
    // and poked out the other side into whatever room was drawn next door. Collision now stops the
    // character's near edge at the wall's face instead of letting its center touch the centerline.
    public const float CharacterRadius = 0.35f;

    // How close to a passable breach's own centre counts as "walked at it" - roughly a door's own
    // half-width, matching World.Eva.cs's identical convention for the exterior-breach-to-vacuum
    // crossing (a big-enough hole is as easy to walk through as an actual door, not a pinhole you
    // have to line up on exactly).
    public const float BreachCrossingRadius = 0.6f;

    public static Room GetRoom(IReadOnlyList<Room> rooms, string roomId) => rooms.First(r => r.Id == roomId);

    // wallBlocks/isPassableBreach are optional (default null = no breach crossing at all, exactly
    // today's behaviour) so every existing caller - Station's own wrapper (a station is never
    // actually breachable), EnemyShipLayout, and every test that only cares about doors - keeps
    // compiling and behaving unchanged. Only Ship's own wrapper, wired from World.Movement.cs, ever
    // passes real ones.
    public static (Vec2 Position, string RoomId) MoveAlongAxis(
        IReadOnlyList<Room> rooms, IReadOnlyList<Door> doors, Vec2 position, string roomId, Vec2 delta, Func<string, bool> isDoorOpen,
        IReadOnlyList<WallBlock>? wallBlocks = null, Func<WallBlock, bool>? isPassableBreach = null)
    {
        var room = GetRoom(rooms, roomId);
        var next = position + delta;

        if (ContainsWithClearance(room, next))
            return (next, roomId);

        // Door crossing is checked against the door's own rectangle, not the clearance-shrunk room
        // - otherwise a character could never get close enough to a door to actually reach it.
        var door = doors.FirstOrDefault(d => d.Connects(roomId) && d.Contains(next) && isDoorOpen(d.Id));
        if (door is not null)
            return (next, door.OtherRoom(roomId));

        // A passable breach in an interior bulkhead (World.WallBlocks.cs's IsPassableBreach) works
        // like a permanently-open door between the two rooms it separates - never a way out to
        // space, which is exactly what WallBlock.IsInterior exists to rule out (World.Eva.cs's own
        // breach-to-vacuum crossing explicitly excludes these blocks).
        if (wallBlocks is not null && isPassableBreach is not null)
        {
            var breach = wallBlocks.FirstOrDefault(w => w.IsInterior && w.OtherRoomId is not null &&
                (w.RoomId == roomId || w.OtherRoomId == roomId) &&
                (w.Position - next).Length() <= BreachCrossingRadius && isPassableBreach(w));
            if (breach is not null)
                return (next, breach.RoomId == roomId ? breach.OtherRoomId! : breach.RoomId);
        }

        return (new Vec2(
            Math.Clamp(next.X, room.Left + CharacterRadius, room.Right - CharacterRadius),
            Math.Clamp(next.Y, room.Top + CharacterRadius, room.Bottom - CharacterRadius)), roomId);
    }

    private static bool ContainsWithClearance(Room room, Vec2 p) =>
        p.X >= room.Left + CharacterRadius && p.X <= room.Right - CharacterRadius &&
        p.Y >= room.Top + CharacterRadius && p.Y <= room.Bottom - CharacterRadius;
}
