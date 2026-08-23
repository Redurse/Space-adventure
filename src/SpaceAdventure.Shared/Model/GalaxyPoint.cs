namespace SpaceAdventure.Shared.Model;

// A single destination on the galaxy map (game_design.md section 5 — "маршрут выбирает сам
// игрок"). X/Y are in map units, unrelated to the ship-interior coordinate system. Faction is who
// holds it (game_design.md section 12) — for a station that sets its prices and whether it'll give
// you work; for a hostile sector it's whose raider you'd be fighting there.
public sealed record GalaxyPoint(string Id, string Name, float X, float Y, GalaxyPointKind Kind,
    FactionId Faction = FactionId.Independent,
    // Only meaningful for Kind == Station: which services it offers (game_design.md section 10).
    StationKind StationKind = StationKind.Trade,
    // Only meaningful for Kind == HostileSector: how many ships defend it (game_design.md section
    // 12, "групповые вражеские встречи"). They engage one after another, not all at once.
    int SquadronSize = 1,
    // Which StarSystem this point belongs to. Left blank in the literals below - StarSystem's own
    // constructor stamps every point it's given with its own Id, so the point data doesn't have to
    // repeat its system on every single line.
    string SystemId = "",
    // How close the ship has to come for this point to catch it (World.Voyage.cs's generalized
    // arrival scan) - every point of interest has its own radius rather than one shared constant, so
    // a busier point (a station's berth) could later be made more forgiving than a bare warp marker
    // without touching anything else. Defaults to the radius every point used before this existed.
    float CaptureRadius = 8f)
{
    public Vec2 Position => new(X, Y);
}
