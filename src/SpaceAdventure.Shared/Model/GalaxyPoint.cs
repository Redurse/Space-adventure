namespace SpaceAdventure.Shared.Model;

// A single destination on the galaxy map (game_design.md section 5 — "маршрут выбирает сам
// игрок"). X/Y are in map units, unrelated to the ship-interior coordinate system. Faction is who
// holds it (game_design.md section 12) — for a station that sets its prices and whether it'll give
// you work; for a hostile sector it's whose raider you'd be fighting there.
public sealed record GalaxyPoint(string Id, string Name, float X, float Y, GalaxyPointKind Kind,
    FactionId Faction = FactionId.Independent,
    // Only meaningful for Kind == Station: which services it offers (game_design.md section 10).
    StationKind StationKind = StationKind.Outpost,
    // Only meaningful for Kind == HostileSector: how many ships defend it (game_design.md section
    // 12, "групповые вражеские встречи"). They engage one after another, not all at once.
    int SquadronSize = 1)
{
    public Vec2 Position => new(X, Y);
}
