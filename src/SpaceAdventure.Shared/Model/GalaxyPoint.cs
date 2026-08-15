namespace SpaceAdventure.Shared.Model;

// A single destination on the galaxy map (game_design.md section 5 — "маршрут выбирает сам
// игрок"). X/Y are in map units, unrelated to the ship-interior coordinate system.
public sealed record GalaxyPoint(string Id, string Name, float X, float Y, GalaxyPointKind Kind)
{
    public Vec2 Position => new(X, Y);
}
