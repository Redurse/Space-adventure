using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Converts a point in AsteroidField world space into the ship's own local frame (the same frame
// Room/Door/WallBlock/etc. already use) - lets the client draw the ship interior and everything
// outside it (asteroids, ore, dropped items, EVA characters) through one shared camera instead of
// two separate renderers/scales (game_design.md: "one continuous space, no hidden transition").
// Mirrors World.GetHullLocalBounds/RotateWorldToLocal server-side exactly, just client-side since
// the server only ever needs this for its own physics, not for the client's camera.
public static class ShipLocalFrame
{
    public static Vec2 GetHullCenter(IReadOnlyList<Room> rooms)
    {
        var minX = rooms.Min(r => r.Left);
        var maxX = rooms.Max(r => r.Right);
        var minY = rooms.Min(r => r.Top);
        var maxY = rooms.Max(r => r.Bottom);
        return new Vec2((minX + maxX) / 2, (minY + maxY) / 2);
    }

    // Half-width/height of the ship's combined room footprint - used to place the engine glow at
    // the stern without needing the full per-room detail.
    public static Vec2 GetHullHalfExtents(IReadOnlyList<Room> rooms)
    {
        var minX = rooms.Min(r => r.Left);
        var maxX = rooms.Max(r => r.Right);
        var minY = rooms.Min(r => r.Top);
        var maxY = rooms.Max(r => r.Bottom);
        return new Vec2((maxX - minX) / 2, (maxY - minY) / 2);
    }

    // Directions, not points - no translation, only the rotation. Needed because everything the
    // player sees outside the hull is drawn in the ship's own unrotated frame, so their keyboard
    // and cursor speak that frame, while the server's EVA physics works in field/world space. Get
    // the conversion wrong and a rotated ship sends you off at an angle to wherever you aimed.
    public static Vec2 ToWorldDirection(Vec2 localDirection, float rotationDegrees)
    {
        var radians = rotationDegrees * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vec2(localDirection.X * cos - localDirection.Y * sin, localDirection.X * sin + localDirection.Y * cos);
    }

    public static Vec2 ToLocalDirection(Vec2 worldDirection, float rotationDegrees) =>
        ToWorldDirection(worldDirection, -rotationDegrees);

    public static Vec2 ToLocal(Vec2 worldPoint, ShipFieldState shipField, Vec2 hullCenter)
    {
        var radians = shipField.RotationDegrees * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var worldOffset = worldPoint - new Vec2(shipField.X, shipField.Y);
        // Inverse-rotate (world -> ship-local), same convention as World.Eva.cs's
        // RotateWorldToLocal: local-to-world would be [cos,-sin; sin,cos], so this is its inverse.
        var localOffset = new Vec2(worldOffset.X * cos + worldOffset.Y * sin, -worldOffset.X * sin + worldOffset.Y * cos);
        return hullCenter + localOffset;
    }

    // Whether this character's X/Y are AsteroidField world coordinates rather than this scene's own
    // ship-local ones. THE seam - World.cs's CreateSnapshot switches what CharacterState.X/Y mean
    // the instant IsOutside flips, and every consumer on this side (camera, sight cone, hit-tests,
    // suit lamp, both character draw passes) has to agree about it. Named once so it can't be
    // spelled differently in two places and drift.
    public static bool InFieldSpace(CharacterState character) => character.IsOutside;

    // A character's position in the ONE frame this whole scene is drawn in: the ship's own local,
    // unrotated frame. Indoors that is already what X/Y are; outside they are field coordinates -
    // on a ship out in a sector they run to thousands - and have to come back through the hull's
    // live position/rotation first (ToLocal above). Every site that used to skip this fold got the
    // same bug wearing a different hat: the camera looking at empty space, a door click that never
    // landed, an RCS plume eighty screens past the right-hand edge - this used to be a ternary
    // repeated at every one of those call sites instead of a single named function.
    public static Vec2 CharacterToLocal(CharacterState character, ShipFieldState shipField, Vec2 hullCenter) =>
        InFieldSpace(character)
            ? ToLocal(new Vec2(character.X, character.Y), shipField, hullCenter)
            : new Vec2(character.X, character.Y);
}
