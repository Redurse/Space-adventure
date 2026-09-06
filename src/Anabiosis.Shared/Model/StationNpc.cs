namespace Anabiosis.Shared.Model;

// X/Y are physical coordinates in the station's own room layout (Station.Rooms), the same
// convention Room/Door/etc. use - an NPC is just a character-shaped fixture standing in a room,
// walk up and click it (game_design.md section 10).
public sealed record StationNpc(string Id, string Name, NpcKind Kind, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
