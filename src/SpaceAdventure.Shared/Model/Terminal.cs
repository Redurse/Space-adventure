namespace SpaceAdventure.Shared.Model;

// A wall terminal's physical position - same shape as CardTable/Jukebox. Purely an on/off screen
// (World.cs's TerminalOn), no track/volume state - the simplest of the three optional devices.
public sealed record Terminal(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
