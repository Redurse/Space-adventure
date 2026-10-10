using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Air aboard a hostile ship. The model is the player's own (World.Atmosphere.cs) with what the enemy doesn't have taken
// out: no generator, no repairable-by-hand seals. Air spreads between compartments through the hull's doors (which a
// hostile crew keeps open) and drains out through every breached outer wall block, so cutting or shooting a hole in a
// compartment slowly empties it and everything connected to it. Unsuited crew in thin air are on a clock; a suited
// crew (EnemyCrewSpawn.Suited) and a boarder in a sealed suit are not - which is what separates ships that have to be
// cleared room by room from ones that can simply be vented.
public sealed partial class World
{
    // Faster than the player's ship loses air through a single punctured wall block: this is a hole in a warship's
    // plating, not a crack, and waiting out a ship should be measured in tens of seconds rather than minutes.
    private const float EnemyVentRatePerSecond = 0.9f;
    // However many holes a compartment has, the drain stops growing at this many.
    private const int EnemyMaxVentingBreaches = 3;

    private void StepEnemyAtmospheres(double deltaSeconds)
    {
        foreach (var enemy in _enemyShips)
            if (enemy.Alive)
                StepEnemyAtmosphere(enemy, deltaSeconds);

        SuffocateBoarders(deltaSeconds);
    }

    private void StepEnemyAtmosphere(EnemyShipRuntime enemy, double deltaSeconds)
    {
        var layout = enemy.Layout;
        var oxygen = enemy.RoomOxygen;

        // Batched against the pre-diffusion levels so the order doors happen to be listed in can't bias which side
        // of a door loses air.
        var deltas = new Dictionary<string, float>();
        foreach (var edge in layout.Ship.DoorEdges)
        {
            if (edge.RoomAId is not { } a || edge.RoomBId is not { } b || !oxygen.ContainsKey(a) || !oxygen.ContainsKey(b))
                continue;
            var flow = OxygenDiffusionRatePerSecond * (oxygen[a] - oxygen[b]) * (float)deltaSeconds;
            deltas[a] = deltas.GetValueOrDefault(a) - flow;
            deltas[b] = deltas.GetValueOrDefault(b) + flow;
        }
        foreach (var (roomId, delta) in deltas)
            oxygen[roomId] += delta;

        // Whatever drifts into a compartment with a hole in its outer wall goes straight back out.
        foreach (var room in layout.Rooms)
        {
            var breaches = layout.WallBlocks.Count(b => b.RoomId == room.Id && !b.IsInterior && enemy.IsWallBlockBreached(b.Id));
            if (breaches > 0)
                oxygen[room.Id] -= EnemyVentRatePerSecond * Math.Min(breaches, EnemyMaxVentingBreaches) * FullOxygen
                    * (float)deltaSeconds;
            oxygen[room.Id] = Math.Clamp(oxygen[room.Id], 0f, FullOxygen);
        }

        SuffocateEnemyCrew(enemy, deltaSeconds);
    }

    // A defender in a vented compartment is on a clock, unless it is wearing a suit. This is the whole payoff of the
    // mechanic: air is a weapon that costs time instead of ammunition, and the crews that can ignore it are exactly the
    // ones meant to be fought head on.
    private void SuffocateEnemyCrew(EnemyShipRuntime enemy, double deltaSeconds)
    {
        foreach (var crew in enemy.Crew)
        {
            if (!crew.Alive || crew.Spawn.Suited)
                continue;
            if (!enemy.RoomOxygen.TryGetValue(crew.RoomId, out var oxygen) || oxygen >= OxygenSafeThreshold)
                continue;

            var damage = MaxSuffocationDamagePerSecond * (OxygenSafeThreshold - oxygen) / OxygenSafeThreshold;
            crew.Health = Math.Max(0, crew.Health - damage * (float)deltaSeconds);
        }

        CaptureIfCrewWiped(enemy);
    }

    // The last defender falling takes the hull however it happened - shot, or suffocated - otherwise venting a ship
    // would clear it of crew and still leave it flying and shooting.
    private static void CaptureIfCrewWiped(EnemyShipRuntime enemy)
    {
        if (enemy.Crew.Count > 0 && enemy.Crew.All(c => !c.Alive) && enemy.Ship.Hp > 0)
            enemy.Ship.ApplyDamage(enemy.Ship.Hp);
    }

    // A boarding party crosses vacuum to get there, so it is suited by definition - but the rule is written out rather
    // than assumed, because losing a suit aboard is the sort of thing a later mechanic will do, and silently making the
    // boarder immortal would be the wrong default.
    private void SuffocateBoarders(double deltaSeconds)
    {
        if (BoardableEnemy is not { } enemy)
            return;
        foreach (var character in _characters.Values)
        {
            if (!character.OnEnemyShip || character.SuitSealed)
                continue;
            if (!enemy.RoomOxygen.TryGetValue(character.RoomId, out var oxygen) || oxygen >= OxygenSafeThreshold)
                continue;

            var damage = MaxSuffocationDamagePerSecond * (OxygenSafeThreshold - oxygen) / OxygenSafeThreshold;
            character.Health = Math.Max(0, character.Health - damage * (float)deltaSeconds);
        }
    }

    private IReadOnlyList<RoomOxygenState> CreateEnemyRoomOxygenStates() =>
        BoardableEnemy is { } enemy
            ? enemy.Layout.Rooms.Select(r => new RoomOxygenState(r.Id, enemy.RoomOxygen.GetValueOrDefault(r.Id, FullOxygen))).ToArray()
            : Array.Empty<RoomOxygenState>();
}
