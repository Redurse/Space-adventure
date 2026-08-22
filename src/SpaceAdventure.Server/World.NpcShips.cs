using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Ambient traffic in the CURRENT star system only (game_design.md, M43) - cargo/military/scout
// hulls that fly the field on their own schedule, independent of whether the player has ever come
// near them, unlike the squadrons a hostile sector/station spawns on demand in response to the
// player (World.EnemyFleet.cs). Warping away simply stops simulating this system's traffic; flying
// back in repopulates it fresh - "persistent" means "always flying while you're here", not "the
// same individual hulls survive a trip to another system".
public sealed partial class World
{
    private const int NpcFleetMaxPerSystem = 8;
    private const float NpcCargoSpeed = 4f;
    private const float NpcMilitarySpeed = 3f;
    private const float NpcScoutSpeed = 5f;
    private const float NpcTurnDegreesPerSecond = 60f;
    private const float NpcWaypointArriveRadius = 15f;
    // Comfortably under EnemyWeaponRangeUnits(26) plus a real closing run - a hostile hull is seen
    // coming, not ambushing from directly on top of the player.
    private const float NpcAggroRadius = 60f;

    // Deliberately its own stream AND its own counter, not the shared _random/_seedCounter
    // (World.EnemyAi.cs) - that one is "the only randomness in the simulation" by design, and
    // several tests depend on exactly which sequence of rolls a given World produces from it
    // (quest targets, incidental-ambush damage). Incrementing the SAME counter for a second field
    // would still shift _random's own seed for every World built afterward (every other instance
    // now advances the shared counter twice instead of once), which is just as disruptive as
    // sharing the stream outright - so this needs a fully separate counter, not just a separate
    // Random instance drawing from a shared one.
    private static int _npcSeedCounter;
    private readonly Random _npcRandom = new(Interlocked.Increment(ref _npcSeedCounter) * 15485867);

    private readonly List<NpcShipRuntime> _npcShips = new();
    // Which system _npcShips currently represents - recomputed lazily on mismatch (StepNpcFleet)
    // the same way GalaxyMap.EnsureGenerated tops up lazily on read, rather than pushing a
    // notification through every place _currentSystemId can change (today just TryWarpTo).
    private string? _npcFleetSystemId;
    // Set instead of _battleSectorPointId when a persistent Military hull (not a GalaxyPoint
    // sector/station) is what triggered the current fight - the two are mutually exclusive
    // (TryEngageHostileNpc only runs when neither is already set) and IsInBattle (World.cs) is
    // true for either. Kept as a separate field rather than folded into _battleSectorPointId so
    // every existing sector/station code path (World.Voyage.cs) stays completely untouched.
    private string? _battleNpcShipId;
    // Where the ship was when an NPC fight started - HasFledTheNpcBattle's own reference point,
    // the same role a sector's fixed marker plays for HasFledTheSector (World.Voyage.cs), since a
    // roaming NPC has no marker of its own to measure from.
    private Vec2 _battleNpcShipEncounterPosition;

    private void StepNpcFleet(double deltaSeconds)
    {
        if (_npcFleetSystemId != _currentSystemId)
            RepopulateNpcFleet();

        var dt = (float)deltaSeconds;
        foreach (var npc in _npcShips)
            StepNpcShip(npc, dt);

        // Can't be jumped while safely parked at a berth - the same reason StepVoyage's own sector
        // check never runs while IsDocked.
        if (!IsDocked && _battleSectorPointId is null && _battleNpcShipId is null)
            TryEngageHostileNpc();
    }

    private void StepNpcShip(NpcShipRuntime npc, float dt)
    {
        var toWaypoint = npc.Waypoint - npc.Position;
        if (toWaypoint.Length() < NpcWaypointArriveRadius)
        {
            npc.Waypoint = NextWaypointFor(npc);
            toWaypoint = npc.Waypoint - npc.Position;
        }

        var speed = npc.Kind switch
        {
            NpcShipKind.Cargo => NpcCargoSpeed,
            NpcShipKind.Scout => NpcScoutSpeed,
            _ => NpcMilitarySpeed,
        };
        npc.Velocity = toWaypoint.Length() > 0.01f ? toWaypoint.Normalized() * speed : Vec2.Zero;
        npc.Position += npc.Velocity * dt;

        if (npc.Velocity.Length() > 0.1f)
        {
            var facing = MathF.Atan2(npc.Velocity.Y, npc.Velocity.X) * (180f / MathF.PI);
            npc.RotationDegrees = RotateToward(npc.RotationDegrees, facing, NpcTurnDegreesPerSecond * dt);
        }
    }

    // Cargo alternates between the two ends of its fixed run; Military/Scout just get a fresh
    // random patrol point every time they reach the last one.
    private Vec2 NextWaypointFor(NpcShipRuntime npc) =>
        npc.Kind == NpcShipKind.Cargo
            ? (npc.Waypoint - npc.RouteA).Length() < 1f ? npc.RouteB : npc.RouteA
            : RandomPointInField();

    private Vec2 RandomPointInField() =>
        new(_npcRandom.NextSingle() * AsteroidField.Width, _npcRandom.NextSingle() * AsteroidField.Height);

    // Rebuilds this system's whole ambient population from scratch - cheap enough (at most
    // NpcFleetMaxPerSystem hulls) that there's no need to preserve individual ships across a trip
    // to another system and back.
    private void RepopulateNpcFleet()
    {
        _npcFleetSystemId = _currentSystemId;
        _npcShips.Clear();

        var system = GalaxyMap.GetSystem(_currentSystemId);
        var stations = system.Points.Where(p => p.Kind == GalaxyPointKind.Station).ToArray();
        var index = 0;

        if (stations.Length >= 2)
        {
            // One run per station, to its own next neighbour in the list - a handful of stations
            // means a handful of overlapping shuttle routes, not a fully-connected mesh.
            for (var i = 0; i < stations.Length && _npcShips.Count < NpcFleetMaxPerSystem; i++)
            {
                var here = stations[i].Position;
                var next = stations[(i + 1) % stations.Length].Position;
                _npcShips.Add(new NpcShipRuntime($"npc-cargo-{index++}", NpcShipKind.Cargo, stations[i].Faction, here, here, next));
            }
        }
        else if (stations.Length == 1)
        {
            // Nowhere else in THIS system to shuttle to - a fixed point out past the warp zone
            // reads as "this run continues on to another system" rather than inventing a second
            // destination that doesn't exist here.
            var here = stations[0].Position;
            var away = AsteroidField.Center + new Vec2(GalaxyMap.WarpZoneRadius, 0f);
            _npcShips.Add(new NpcShipRuntime($"npc-cargo-{index++}", NpcShipKind.Cargo, stations[0].Faction, here, here, away));
        }

        // Whoever actually has a flag flying in this system (a point's own owner, or the system's
        // controlling faction if it has one) fields a patrol - Independent excluded, the same
        // "takes no side, keeps no navy" reading World.Factions.cs's rival-standing logic already
        // gives them.
        var militaryFactions = system.Points.Select(p => p.Faction)
            .Concat(system.ControllingFaction is { } cf ? new[] { cf } : Array.Empty<FactionId>())
            .Where(f => f != FactionId.Independent)
            .Distinct()
            .ToArray();
        foreach (var faction in militaryFactions)
        {
            if (_npcShips.Count >= NpcFleetMaxPerSystem)
                break;
            var start = RandomPointInField();
            _npcShips.Add(new NpcShipRuntime($"npc-military-{index++}", NpcShipKind.Military, faction, start, start, RandomPointInField()));
        }

        if (_npcShips.Count < NpcFleetMaxPerSystem)
        {
            // A scout still flies a side's colours when the system actually has one, purely for
            // flavor - it never fights regardless of whose it is (NpcShipKind's own doc comment).
            var scoutFaction = militaryFactions.Length > 0 ? militaryFactions[_npcRandom.Next(militaryFactions.Length)] : FactionId.Independent;
            var start = RandomPointInField();
            _npcShips.Add(new NpcShipRuntime($"npc-scout-{index++}", NpcShipKind.Scout, scoutFaction, start, start, RandomPointInField()));
        }
    }

    // A persistent Military hull turning a standing-driven grudge into a real fight the moment the
    // player's ship comes within sight of it - reuses the exact squadron/projectile/boarding
    // machinery a hostile sector already runs (World.EnemyFleet.cs) rather than inventing a second,
    // parallel combat model: the ambient hull converts into a single-ship EnemyShipRuntime
    // "squadron" and drops out of _npcShips for as long as the fight lasts.
    private void TryEngageHostileNpc()
    {
        var hostile = _npcShips.FirstOrDefault(npc =>
            npc.Kind == NpcShipKind.Military &&
            GetStanding(npc.FactionId) <= FactionDefinitions.HostileThreshold &&
            (npc.Position - _shipFieldPosition).Length() <= NpcAggroRadius);
        if (hostile is null)
            return;

        _npcShips.Remove(hostile);
        _battleFaction = hostile.FactionId;
        _battleNpcShipId = hostile.Id;
        _battleNpcShipEncounterPosition = _shipFieldPosition;
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipRotationDegrees = 0f;
        _shipAutoStabilize = true;
        SpawnEnemySquadron(1); // the ambient hull itself is one ship, not a sector's squadron
        ResetEnemyCrew();
        _crewShipId = BoardableEnemy?.Id;
    }

    // Same resolution ResolveEnemyLosses (World.Voyage.cs) gives a hostile sector's squadron,
    // scaled down to the one hull a Military NPC ever fields: no bounty, no war-effort bookkeeping
    // (that front is about sectors/stations, not a roaming patrol) - just the same per-kill
    // standing cost any other kill already carries.
    private void ResolveNpcBattleLosses()
    {
        if (_enemyShips.Any(e => e.Alive))
            return;
        if (_battleFaction is { } faction)
            RecordShipDestroyed(faction);
        _battleNpcShipId = null;
        _battleFaction = null;
        _projectiles.Clear();
    }

    // Measured from where the fight started, same reasoning HasFledTheSector's own doc comment
    // gives for not measuring against the enemy hull itself (its formation AI tracks the player
    // too closely for that to ever open up during a real retreat).
    private bool HasFledTheNpcBattle() =>
        _battleNpcShipId is not null &&
        (_battleNpcShipEncounterPosition - _shipFieldPosition).Length() > BattleFleeDistance;

    private void FleeNpcBattle()
    {
        _battleNpcShipId = null;
        _battleFaction = null;
        _projectiles.Clear();
        NudgeAwayFromFieldEdge();
    }

    private IReadOnlyList<NpcShipFieldState> CreateNpcShipStates() =>
        _npcShips.Select(n => new NpcShipFieldState(n.Id, n.Kind, n.FactionId, n.Position.X, n.Position.Y, n.RotationDegrees)).ToArray();
}
