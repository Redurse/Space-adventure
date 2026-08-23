using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// One hostile hull in the field: the abstract HP that combat already used, plus the place it
// occupies and the state its gunner needs. Kept separate from EnemyShip so the HP model stays the
// small testable thing it was.
public sealed class EnemyShipRuntime
{
    public string Id { get; }
    public EnemyShip Ship { get; }
    // Which hull this is, and therefore what a boarding party finds inside it (EnemyShipClass).
    public EnemyShipLayout Layout { get; }
    // Which turret weapons this hull is packing (enemy/weapon overhaul - "И враги, и игрок"
    // applies weapons to both sides) - a flavor/behavior list for TryEnemyFire (World.EnemyFleet.cs),
    // not full TurretRuntimes: raiders don't track ammo or heat, each entry just shoots at a rate and
    // bolt style that matches whichever weapon it is. Almost always one entry (whichever single
    // weapon the squadron formation handed this hull); a hull with its own fixed
    // EnemyShipLayout.WeaponLoadout (e.g. Frigate's 2 magnetic + 1 laser) carries all of them here so
    // each fires independently on its own cooldown.
    public IReadOnlyList<TurretWeaponType> WeaponLoadout { get; }
    // One cooldown per entry in WeaponLoadout, same indices - a multi-turret hull's guns don't share
    // a single reload clock, each fires on its own schedule. Starts at 0 for every slot; the opening
    // delay (EnemyOpeningDelaySeconds) is applied to all of them right after construction.
    public float[] TurretFireCooldowns { get; }
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
    // fixed quadrant - "не стояли на одном месте а летали вокруг корабля". Initialized from the
    // ship's actual spawn bearing so it starts exactly where the raider already is.
    public float OrbitAngleDegrees { get; set; }
    // Which way around the circle this raider orbits (+1/-1) - picked once per ship so a squadron
    // doesn't all sweep the same direction in lockstep.
    public float OrbitDirection { get; init; } = 1f;

    public EnemyShipRuntime(string id, float maxHp, Vec2 position, EnemyShipLayout layout, IReadOnlyList<TurretWeaponType> weaponLoadout)
    {
        Id = id;
        Ship = new EnemyShip(maxHp);
        Position = position;
        Layout = layout;
        WeaponLoadout = weaponLoadout;
        TurretFireCooldowns = new float[weaponLoadout.Count];
    }
}
