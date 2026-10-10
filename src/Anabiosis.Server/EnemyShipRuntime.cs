using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// One hostile ship in the field: where it is and how it flies, plus the simulated hull it is made of (Layout - real rooms,
// walls, turrets and engines, the same kind of structure the player's own ship is) and the people walking about it
// (EnemyShipRuntime.Interior.cs). Its overall health readout (Ship) is derived from that hull (World.EnemyDamage.cs).
public sealed partial class EnemyShipRuntime
{
    public string Id { get; }
    public EnemyShip Ship { get; }
    // The hull as it is NOW: it loses compartments as they are destroyed (World.EnemyDamage.cs).
    public EnemyShipLayout Layout { get; private set; }
    // One cooldown per turret (by turret id) - a multi-gun hull's guns don't share a single reload clock, each fires on its
    // own schedule. A turret with no entry has not fired yet.
    public Dictionary<string, float> TurretCooldowns { get; } = new();
    public Vec2 Position { get; set; }
    public Vec2 Velocity { get; set; }
    public float RotationDegrees { get; set; }
    public bool Alive => Ship.Hp > 0;
    // Which priority target (World.EnemyFleet.cs's EnemyTargetPriority) this raider is currently
    // committed to - null only before its first tick. Kept sticky there rather than re-picked every
    // shot: ResolveEnemyTarget only moves off it once it's actually disabled/unreachable.
    public EnemyTargetPriority? TargetPriority { get; set; }
    // Random per-ship phase offset for the dodge weave (World.EnemyFleet.cs's SteerEnemy) so a whole
    // squadron doesn't jink from side to side in lockstep.
    public float DodgePhaseSeed { get; init; }
    // Bearing (world degrees, atan2 convention) this raider currently sits at around the ship's own
    // centre, continuously advancing (World.EnemyFleet.cs's SteerEnemy) rather than settling on one
    // fixed quadrant. Initialized from the ship's actual spawn bearing so it starts exactly where the raider already is.
    public float OrbitAngleDegrees { get; set; }
    // Which way around the circle this raider orbits (+1/-1) - picked once per ship so a squadron
    // doesn't all sweep the same direction in lockstep.
    public float OrbitDirection { get; init; } = 1f;

    // This hull's own wall hit points, one entry per Layout.WallBlocks id. Own instance per ship so two raiders don't share
    // a wall's damage (World.Cutting.cs damages these once cut, World.EnemyDamage.cs when shot, World.Eva.cs's
    // StepMagnetizedWalk lets a boarder climb through a breached one same as the player's own hull). Keyed by id, so the
    // damage of every wall that survives losing a compartment carries over to the rebuilt hull.
    private readonly Dictionary<string, float> _wallBlockHp = new();

    public float GetWallBlockHp(string blockId) => _wallBlockHp.GetValueOrDefault(blockId, World.WallBlockMaxHp);
    public bool IsWallBlockBreached(string blockId) => GetWallBlockHp(blockId) <= 0f;
    public void DamageWallBlock(string blockId, float amount) =>
        _wallBlockHp[blockId] = Math.Max(0f, GetWallBlockHp(blockId) - amount);
    public void RepairWallBlock(string blockId, float amount) =>
        _wallBlockHp[blockId] = Math.Min(World.WallBlockMaxHp, GetWallBlockHp(blockId) + amount);

    public EnemyShipRuntime(string id, float maxHp, Vec2 position, EnemyShipLayout layout)
    {
        Id = id;
        Ship = new EnemyShip(maxHp);
        Position = position;
        Layout = layout;
        InitialRoomCount = layout.Rooms.Count;
        InitialThrust = layout.Ship.Engines.Where(e => e.Role == EngineRole.Marching).Sum(e => e.MaxThrust);
        InitializeInterior();
        BuildPowerGraph();
    }
}
