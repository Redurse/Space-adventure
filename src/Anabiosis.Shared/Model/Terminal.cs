namespace Anabiosis.Shared.Model;

// A wall terminal's physical position - many independent instances per hull now (direct user
// request, "их будет много"), each with its own on/off (World.Terminals.cs), no track/volume state.
// FacingSide (direct user request, "занимал половину блока и визуально выглядел в соответствии с
// полублоком") - which side of its own (X,Y) tile the terminal's half-block visual sits on,
// whether recessed in a half-thick wall's own free half or protruding from an ordinary wall onto
// the neighboring floor tile (TileGrid.PlaceRecessedWallDevice/PlaceWallDevice). Collision is
// uniform either way - a full 1x1 obstacle at (X,Y), see Ship.cs's own WallDeviceObstacles.
public sealed record Terminal(string Id, string RoomId, float X, float Y, TileSide FacingSide)
{
    public Vec2 Position => new(X, Y);
}
