namespace SpaceAdventure.Shared.Model;

// X/Y are positions in the wiring panel's own abstract schematic layout (game_design.md section 1)
// — like StationNpc/GalaxyPoint, unrelated to ship-interior coordinates. Device nodes reuse the
// underlying ShipSystemDevice's Id directly, so a WireLink's ToNodeId can be matched straight
// back to the physical block it powers without an extra lookup table.
public sealed record WireNode(string Id, WireNodeKind Kind, string Label, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
