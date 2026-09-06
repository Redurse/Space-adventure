using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// One ambient hull flying the current system on its own, independent of the player
// (World.NpcShips.cs, M43). Deliberately not an EnemyShipRuntime and not a subclass of one - it
// has no HP of its own until a Military hull actually turns hostile and closes to combat range, at
// which point World.NpcShips.cs spawns a real EnemyShipRuntime for it and removes this one instead
// of the two ever coexisting.
public sealed class NpcShipRuntime
{
    public string Id { get; }
    public NpcShipKind Kind { get; }
    public FactionId FactionId { get; }
    public Vec2 Position { get; set; }
    public Vec2 Velocity { get; set; }
    public float RotationDegrees { get; set; }
    // Where this hull is currently headed - re-picked on arrival (World.NpcShips.cs's
    // StepNpcShip). For Cargo this alternates between RouteA/RouteB; Military/Scout just get a
    // fresh random patrol point each time and leave RouteA/RouteB at their starting value, unused.
    // Waypoint itself is a STATIC snapshot, only used for the "which end am I at" comparison in
    // NextWaypointFor - the actual navigation target is resolved live from WaypointStationId
    // whenever that's set (World.NpcShips.cs's own StepNpcShip), not from this frozen value.
    public Vec2 Waypoint { get; set; }
    // Which GalaxyPoint the current Waypoint actually is, if any (M58 follow-up - "перевести
    // стыковку на относительный кадр"): a hosted station's own real Kepler orbital speed can carry
    // it far from wherever `Waypoint` was snapshotted the moment this got picked, so StepNpcShip
    // re-resolves the LIVE position through this id every tick instead of navigating toward the
    // stale Vec2 directly. Null for Military/Scout's own random patrol points and the single-
    // station "away" synthetic point, none of which correspond to a real GalaxyPoint.
    public string? WaypointStationId { get; set; }
    // The two ends of a Cargo hull's fixed shuttle run (its departure and destination station, or
    // a synthetic point standing in for "off to another system" when this system only has one
    // station) - meaningless for Military/Scout, which patrol randomly instead. RouteA/RouteBStationId
    // are each real GalaxyPoint ids when that end actually is a station, null for the single-station
    // case's own synthetic "away" point.
    public Vec2 RouteA { get; }
    public string? RouteAStationId { get; }
    public Vec2 RouteB { get; }
    public string? RouteBStationId { get; }

    public NpcShipRuntime(string id, NpcShipKind kind, FactionId factionId, Vec2 position,
        Vec2 routeA, string? routeAStationId, Vec2 routeB, string? routeBStationId)
    {
        Id = id;
        Kind = kind;
        FactionId = factionId;
        Position = position;
        RouteA = routeA;
        RouteAStationId = routeAStationId;
        RouteB = routeB;
        RouteBStationId = routeBStationId;
        Waypoint = routeB;
        WaypointStationId = routeBStationId;
    }
}
