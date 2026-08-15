namespace SpaceAdventure.Shared.Model;

// Physical block on the bridge (game_design.md section 5) — click it to bring up the galaxy map
// and pick where to fly next.
public sealed record NavigationConsole(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
