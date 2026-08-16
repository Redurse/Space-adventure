namespace SpaceAdventure.Shared.Model;

// The reactor's physical position/footprint (game_design.md section 1). A big, clickable block
// — clicking it "enters" it and shows its 4 fuel-rod slots (see ReactorState).
// SizeScale multiplies how big it's drawn: a class whose whole midsection is built around its
// reactor should look like it, and the alternative (one size for every hull) makes a purpose-built
// reactor hall read as a normal room with a normal box in it.
public sealed record ReactorBlock(string Id, string RoomId, float X, float Y, float SizeScale = 1f)
{
    public Vec2 Position => new(X, Y);
}
