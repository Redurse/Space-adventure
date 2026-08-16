namespace SpaceAdventure.Shared.Model;

// Station property lying around in a room, there to be stolen (game_design.md section 10 — "на
// станции можно воровать вещи"). X/Y are in the station's own room coordinates, same convention
// as StationNpc. Taking one is the only way to get goods without paying the Trader.
public sealed record StationCrate(string Id, string RoomId, float X, float Y, ItemType Item)
{
    public Vec2 Position => new(X, Y);
}
