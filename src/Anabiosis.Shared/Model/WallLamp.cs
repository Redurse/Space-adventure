namespace Anabiosis.Shared.Model;

// A wall-mounted lamp (direct user request - "на стену размером с полублок можно крепить только
// терминал и настенную лампу... когда она установлена, она излучает свет") - purely passive, no
// on/off of its own: it just lights up whenever the ship's own lamps do (Game1.Lighting.cs reads
// the same ReactorLevers.LightsOn/power-fraction mood every room lamp already uses), so unlike
// Terminal there's no server-side toggle state to track for it at all. Same shape as Terminal
// otherwise - many independent instances per hull, FacingSide is which side of its own (X,Y) tile
// the half-block visual sits on (see Terminal's own doc comment for the recessed/protruding modes).
public sealed record WallLamp(string Id, string RoomId, float X, float Y, TileSide FacingSide)
{
    public Vec2 Position => new(X, Y);
}
