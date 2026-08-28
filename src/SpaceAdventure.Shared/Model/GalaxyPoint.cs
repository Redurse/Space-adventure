namespace SpaceAdventure.Shared.Model;

// A single destination on the galaxy map (game_design.md section 5 — "маршрут выбирает сам
// игрок"). X/Y are in map units, unrelated to the ship-interior coordinate system. Faction is who
// holds it (game_design.md section 12) — for a station that sets its prices and whether it'll give
// you work; for a hostile sector it's whose raider you'd be fighting there.
// X/Y are double, not float (M58 follow-up - same fix as ShipFieldState's own doc comment): a
// float32 position can't resolve two points closer than tens of thousands of units apart once the
// field itself grew large enough - a non-hosted point's own absolute coordinate (X = (float)
// someHugeDoubleValue.X, the pattern several call sites in GalaxyMap.cs still used) silently drifted
// from whatever real, double-precision position it was meant to echo (AsteroidField.ClusterCenter,
// in one confirmed case), stranding "the ship's own start point" and "the asteroids actually there"
// tens of thousands of units apart despite being defined as the exact same point.
public sealed record GalaxyPoint(string Id, string Name, double X, double Y, GalaxyPointKind Kind,
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
    // M59 - "убрать орбитальную механику, вернуть статичную карту в духе Cosmoteer": every point
    // (including a station) is a plain fixed coordinate now. Used to optionally ride along with a
    // host celestial body (HostBodyId/OrbitAngularSpeed, M50/M52) - both removed, along with the
    // RotatingOffset math and the PositionAt(bodiesById, totalSeconds, fieldCenter) method they drove.
    public Vec2 Position => new(X, Y);
}
