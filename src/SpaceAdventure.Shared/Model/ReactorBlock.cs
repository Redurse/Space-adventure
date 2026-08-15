namespace SpaceAdventure.Shared.Model;

// The reactor's physical position/footprint (game_design.md section 1). A big, clickable block
// — clicking it "enters" it and shows its 4 fuel-rod slots (see ReactorState).
public sealed record ReactorBlock(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
