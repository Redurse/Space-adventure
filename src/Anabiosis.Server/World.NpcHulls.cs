using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Ambient ships (cargo, patrol, scout - World.NpcShips.cs) as real ships rather than flying triangles (direct user request:
// "корабли всех фракций ... нормальные корабли с экипажами ... получали урон от игрока и от мира ... облетали астероиды").
//
// Each one carries the same simulated hull a raider is (EnemyShipRuntime: rooms, walls, wiring, atmosphere, a crew at their posts,
// engineers who weld breaches), built the first time the player comes within NpcHullActivationRadius or a shot lands - far from
// anyone it is still only a point, so a system's ten ambient ships cost nothing until somebody is near. A materialised hull is
// then stepped by the very same systems as a raider's (crews, atmosphere, compartments blowing up), so everything that can hurt
// or be done to one of those works here unchanged.
//
//  * Damage from the player: a shell is traced through the hull (TryHitEnemyShips) and hurts the first intact thing it meets;
//    the ship it hit is Provoked - it stops its run, flies at the player and, once it has closed, becomes an ordinary raider
//    (AdoptNpcAsEnemy: the same hull, wounds and crew carried over into the fight).
//  * Damage from the world: scraping an asteroid rips its plating, flying through the star's heat cooks it.
//  * Flying: it steers round asteroids and the star's heat ring instead of through them (NpcAvoidObstacles).
public sealed partial class World
{
    // A hull is built once the player is this close (comfortably more than the client's detail distance, so it is always there to draw).
    private const float NpcHullActivationRadius = 450f;
    // Crews, air and compartments of a built hull are only stepped while it is this near; further off it is frozen as it was.
    private const float NpcHullSimRadius = 900f;
    // How close a ship the player fired on has to come before it turns the exchange into a real fight.
    private const float NpcProvokedEngageRadius = 160f;
    private const float NpcAccelerationPerSecond = 2.2f;
    private const float NpcAvoidMargin = 8f;
    // Plating lost per unit of speed lost in a scrape with an asteroid.
    private const float NpcCollisionWallDamagePerSpeed = 16f;
    private const float NpcCollisionContactReach = 3.5f;
    // Provoked ships hold their fire a beat after the fight opens, like any raider (EnemyOpeningDelaySeconds), only shorter.
    private const float NpcProvokedOpeningDelaySeconds = 1.5f;

    // Every ambient ship is the same hull, so one circle encloses them all.
    private static readonly Lazy<float> NpcBroadHullRadius = new(() =>
    {
        var (_, half) = NewEnemyLayout().GetLocalBounds();
        return (float)Math.Sqrt(half.X * half.X + half.Y * half.Y) + 0.5f;
    });

    private EnemyShipRuntime EnsureNpcHull(NpcShipRuntime npc)
    {
        if (npc.Hull is { } existing)
            return existing;
        var hull = new EnemyShipRuntime(npc.Id, EnemyMaxHp, npc.Position, NewEnemyLayout())
        {
            RotationDegrees = npc.RotationDegrees,
            // Own random phases from the NPC stream, so building one never shifts the shared simulation RNG.
            DodgePhaseSeed = _npcRandom.NextSingle() * 1000f,
            OrbitDirection = _npcRandom.Next(2) == 0 ? 1f : -1f,
        };
        npc.AttachHull(hull);
        return hull;
    }

    // The ambient hulls that are built, alive and near enough to be simulated this tick (a copy: dying removes them from the fleet).
    private List<EnemyShipRuntime> NpcSimulatedHulls() =>
        _npcShips
            .Select(n => n.Hull)
            .Where(h => h is { Alive: true } && (h.Position - _shipFieldPosition).Length() <= NpcHullSimRadius)
            .Select(h => h!)
            .ToList();

    // Raiders plus the ambient hulls that are being simulated - what the shared hull systems (crews, air, compartments) walk.
    private IEnumerable<EnemyShipRuntime> SimulatedHulls() => _enemyShips.Concat(NpcSimulatedHulls());

    private void StepNpcHulls(float dt)
    {
        foreach (var npc in _npcShips.ToList())
        {
            if (npc.Hull is null && (npc.Position - _shipFieldPosition).Length() <= NpcHullActivationRadius)
                EnsureNpcHull(npc);
            if (npc.Hull is not { } hull)
                continue;

            if (!hull.Alive)
            {
                // Lost with all hands. Only a ship the player shot down costs its owner standing; a scrape or the sun is no one's doing.
                _npcShips.Remove(npc);
                if (npc.Provoked)
                    RecordShipDestroyed(npc.FactionId);
                continue;
            }
            if (hull.IsExploding || hull.Derelict)
                continue;

            ApplyNpcWorldHazards(npc, hull, dt);
            SeparateEnemy(hull); // hulls are solid: never slides through the player's ship or a raider
        }
    }

    // ---- damage from the world ------------------------------------------------------------------------------------

    private void ApplyNpcWorldHazards(NpcShipRuntime npc, EnemyShipRuntime hull, float dt)
    {
        var radius = hull.HullRadius;
        // The plating is not a perfect disc: touch the rock a little inside the broad circle.
        var bodyReach = radius * 0.7f;

        foreach (var asteroid in AsteroidField.Asteroids)
        {
            var away = hull.Position - asteroid.Position;
            var distance = (float)away.Length();
            var contact = bodyReach + asteroid.Radius;
            if (distance >= contact)
                continue;

            var normal = distance > 0.001f ? away.Normalized() : new Vec2(1f, 0f);
            hull.Position = asteroid.Position + normal * contact;
            var closing = -(float)(hull.Velocity.X * normal.X + hull.Velocity.Y * normal.Y);
            if (closing <= 0.2f)
                continue;

            // Where the rock bit into the hull, in the hull's own frame: the plates around that spot take the blow.
            var local = EnemyWorldToLocal(hull, asteroid.Position + normal * asteroid.Radius);
            // The plates nearest that spot (the hull is not a solid disc, so the nearest may be a little way in).
            var nearest = hull.Layout.WallBlocks.Min(b => (b.Position - local).Length());
            foreach (var block in hull.Layout.WallBlocks)
                if ((block.Position - local).Length() <= nearest + NpcCollisionContactReach)
                    hull.DamageWallBlock(block.Id, closing * NpcCollisionWallDamagePerSpeed);
            hull.Velocity -= normal * (closing * 1.4f); // stops dead against the rock and bounces a little
        }

        // Heat: the whole ring round the star cooks the plating, deeper in means faster.
        var system = GalaxyMap.GetSystem(_currentSystemId);
        var star = system.Bodies.First(b => b.ParentId is null);
        var intensity = CelestialBodyGenerator.SunZoneIntensity(star, (float)(hull.Position - system.Field.Center).Length());
        if (intensity > 0f)
        {
            var heat = SunZoneMaxDamagePerSecond * intensity * dt;
            foreach (var block in hull.Layout.WallBlocks)
                hull.DamageWallBlock(block.Id, heat);
        }
    }

    // ---- steering round things ----------------------------------------------------------------------------------

    // The heading to actually fly: the wanted one bent away from any asteroid (or the star's heat ring) that lies across the path
    // within a few seconds' flying, and pushed straight out of one it is already too close to.
    private Vec2 NpcAvoidObstacles(NpcShipRuntime npc, Vec2 direction, float speed)
    {
        var radius = NpcBroadHullRadius.Value;
        var look = radius * 2.5f + speed * 6f;
        var perp = new Vec2(-direction.Y, direction.X);
        var steer = Vec2.Zero;
        var coinFlip = (npc.Id[^1] & 1) == 0 ? 1f : -1f; // which way to go round something dead ahead

        void Consider(Vec2 center, float obstacleRadius)
        {
            var clearance = obstacleRadius + radius + NpcAvoidMargin;
            var to = center - npc.Position;
            var distance = (float)to.Length();
            if (distance > look + clearance)
                return;
            if (distance < clearance)
            {
                steer += (npc.Position - center).Normalized() * 3f;
                return;
            }
            var along = (float)(to.X * direction.X + to.Y * direction.Y);
            if (along < 0f || along > look + clearance)
                return;
            var side = (float)(to.X * perp.X + to.Y * perp.Y); // + means it is on the perp side
            if (Math.Abs(side) >= clearance)
                return;
            var sign = Math.Abs(side) < 0.5f ? coinFlip : side > 0f ? -1f : 1f;
            var weight = (1f - Math.Abs(side) / clearance) * (1f - 0.5f * along / (look + clearance));
            steer += perp * (sign * weight * 2.5f);
        }

        foreach (var asteroid in AsteroidField.Asteroids)
            Consider(asteroid.Position, asteroid.Radius * AsteroidShape.MaxRadiusFactor);

        var system = GalaxyMap.GetSystem(_currentSystemId);
        var star = system.Bodies.First(b => b.ParentId is null);
        Consider(system.Field.Center, CelestialBodyGenerator.SunZoneRadius(star));

        var heading = direction + steer;
        return heading.Length() > 0.01f ? heading.Normalized() : direction;
    }

    // ---- test hooks ---------------------------------------------------------------------------------------------

    // Test-only: puts an ambient ship at a place, optionally already moving and bound for a given waypoint (World.DebugPlaceShip's convention).
    public void DebugPlaceNpc(string id, Vec2 position, Vec2? velocity = null, Vec2? waypoint = null)
    {
        var npc = _npcShips.First(n => n.Id == id);
        npc.Position = position;
        npc.Velocity = velocity ?? Vec2.Zero;
        if (waypoint is { } target)
        {
            npc.Waypoint = target;
            npc.WaypointStationId = null;
        }
    }

    // Test-only: how much plating an ambient ship has lost, in wall hit points (0 for one that has no hull built yet).
    public double DebugNpcWallDamage(string id) =>
        _npcShips.FirstOrDefault(n => n.Id == id)?.Hull is { } hull
            ? hull.Layout.WallBlocks.Sum(b => (double)(WallBlockMaxHp - hull.GetWallBlockHp(b.Id)))
            : 0.0;

    // Test-only: a player's shell flying from one point to another (what ResolveProjectileHit does for a shot that is not an enemy's).
    public bool DebugShootAtShips(Vec2 from, Vec2 to, float damage) => TryHitEnemyShips(from, to, damage);

    // ---- turning into a fight --------------------------------------------------------------------------------------

    // The ambient hull itself becomes the raider, with every wound and every crew member it already had.
    private void AdoptNpcAsEnemy(NpcShipRuntime npc)
    {
        var hull = EnsureNpcHull(npc);
        _enemyShips.Clear();
        hull.OrbitAngleDegrees = BearingDegrees(hull.Position - _shipFieldPosition);
        var opening = npc.Provoked ? NpcProvokedOpeningDelaySeconds : EnemyOpeningDelaySeconds;
        foreach (var turret in hull.Layout.Ship.Turrets)
            hull.TurretCooldowns[turret.Id] = opening;
        _enemyShips.Add(hull);
        _enemyReinforcements = 0;
        _remainingEnemyShips = 1;
    }
}
