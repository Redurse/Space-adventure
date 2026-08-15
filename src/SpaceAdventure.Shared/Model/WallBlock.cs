namespace SpaceAdventure.Shared.Model;

// A single 1x1 segment of a room's OUTER hull (game_design.md sections 1-2 — block-based
// structure, continuous player movement). Only exterior edges get blocks — an interior wall
// shared with another pressurized room has no vacuum on the other side, so it isn't breachable.
// X/Y is the block's center point.
public sealed record WallBlock(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
