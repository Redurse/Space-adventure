using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

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
    public Vec2 Waypoint { get; set; }
    // The two ends of a Cargo hull's fixed shuttle run (its departure and destination station, or
    // a synthetic point standing in for "off to another system" when this system only has one
    // station) - meaningless for Military/Scout, which patrol randomly instead.
    public Vec2 RouteA { get; }
    public Vec2 RouteB { get; }

    public NpcShipRuntime(string id, NpcShipKind kind, FactionId factionId, Vec2 position, Vec2 routeA, Vec2 routeB)
    {
        Id = id;
        Kind = kind;
        FactionId = factionId;
        Position = position;
        RouteA = routeA;
        RouteB = routeB;
        Waypoint = routeB;
    }
}
