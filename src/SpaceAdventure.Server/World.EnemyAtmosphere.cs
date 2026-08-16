using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Air aboard the ship you are boarding. Until now the enemy hull had no atmosphere at all, which
// made a breached warship the one pressurised place in the game - and made boarding a pure shooting
// gallery, where the only question was who fired first.
//
// The model is the player's own (World.Atmosphere.cs) with everything the enemy doesn't have taken
// out: no generator, no repairable breaches, no welding. What is left is the part that matters here.
// The compartment behind the boarding breach is open to space and stays that way, every interior
// door starts *closed* (a crew being boarded buttons up), and opening one bleeds the room behind it
// into the vacuum next door. So there are two ways to take a hull: clear it room by room, or open
// the doors and let it suffocate - and which one works depends on whether that crew wears suits,
// which is what separates a freighter from a gunship (EnemyShipClass).
public sealed partial class World
{
    // Faster than the player's ship loses air through a single punctured wall block: this is a hole
    // the size of a hatch, not a crack, and waiting out a ship should be measured in tens of
    // seconds rather than minutes.
    private const float EnemyVentRatePerSecond = 0.9f;

    private readonly Dictionary<string, float> _enemyRoomOxygen = new();
    // Which hull's compartments the levels above describe. Every class has its own room ids, so
    // reading one hull's air out of another's dictionary isn't a wrong number - it's a missing key.
    private EnemyShipClass? _enemyAtmosphereKind;

    private void ResetEnemyAtmosphere()
    {
        var layout = EnemyShipLayout;
        _enemyRoomOxygen.Clear();
        foreach (var room in layout.Rooms)
            _enemyRoomOxygen[room.Id] = FullOxygen;
        // The breach is a hole in the hull, so the compartment behind it has already lost its air
        // by the time anyone climbs through.
        _enemyRoomOxygen[layout.BoardingRoomId] = 0f;
        _enemyAtmosphereKind = layout.Kind;
    }

    private void StepEnemyAtmosphere(double deltaSeconds)
    {
        var layout = EnemyShipLayout;
        // The hull in front of the guns changes the moment one dies, and this runs every tick -
        // including the tick between the kill and the squadron bookkeeping that swaps the crew.
        if (_enemyAtmosphereKind != layout.Kind)
            ResetEnemyAtmosphere();

        // Same batched-delta diffusion as the player's ship: computed against the pre-diffusion
        // levels so the order the doors happen to be listed in can't bias which side loses air.
        var deltas = new Dictionary<string, float>();
        foreach (var door in layout.Doors)
        {
            if (!IsDoorOpen(door.Id))
                continue;
            var flow = OxygenDiffusionRatePerSecond * (_enemyRoomOxygen[door.RoomAId] - _enemyRoomOxygen[door.RoomBId]) * (float)deltaSeconds;
            deltas[door.RoomAId] = deltas.GetValueOrDefault(door.RoomAId) - flow;
            deltas[door.RoomBId] = deltas.GetValueOrDefault(door.RoomBId) + flow;
        }

        foreach (var (roomId, delta) in deltas)
            _enemyRoomOxygen[roomId] += delta;

        // The breach itself: whatever drifts into that compartment goes straight back out.
        var breached = layout.BoardingRoomId;
        _enemyRoomOxygen[breached] -= EnemyVentRatePerSecond * FullOxygen * (float)deltaSeconds;

        foreach (var room in layout.Rooms)
            _enemyRoomOxygen[room.Id] = Math.Clamp(_enemyRoomOxygen[room.Id], 0f, FullOxygen);

        SuffocateEnemyCrew(deltaSeconds);
        SuffocateBoarders(deltaSeconds);
    }

    // A defender in a vented compartment is on a clock, unless it is wearing a suit. This is the
    // whole payoff of the mechanic: air is a weapon that costs time instead of ammunition, and the
    // crews that can ignore it are exactly the ones meant to be fought head on.
    private void SuffocateEnemyCrew(double deltaSeconds)
    {
        foreach (var crew in _enemyCrew.Values)
        {
            if (!crew.Alive || crew.Spawn.Suited)
                continue;
            if (!_enemyRoomOxygen.TryGetValue(crew.Spawn.RoomId, out var oxygen) || oxygen >= OxygenSafeThreshold)
                continue;

            var damage = MaxSuffocationDamagePerSecond * (OxygenSafeThreshold - oxygen) / OxygenSafeThreshold;
            crew.Health = Math.Max(0, crew.Health - damage * (float)deltaSeconds);
        }

        // Suffocating the last defender takes the hull exactly like shooting the last one does
        // (TryFirePersonalWeapon) - otherwise venting a ship would clear it of crew and still leave
        // it flying and shooting.
        if (_enemyCrew.Count > 0 && _enemyCrew.Values.All(c => !c.Alive) && Enemy.Hp > 0)
            Enemy.ApplyDamage(Enemy.Hp);
    }

    // A boarding party crosses vacuum to get there, so it is suited by definition - but the rule is
    // written out rather than assumed, because losing a suit aboard is the sort of thing a later
    // mechanic will do, and silently making the boarder immortal would be the wrong default.
    private void SuffocateBoarders(double deltaSeconds)
    {
        foreach (var character in _characters.Values)
        {
            if (!character.OnEnemyShip || character.SuitSealed)
                continue;
            if (!_enemyRoomOxygen.TryGetValue(character.RoomId, out var oxygen) || oxygen >= OxygenSafeThreshold)
                continue;

            var damage = MaxSuffocationDamagePerSecond * (OxygenSafeThreshold - oxygen) / OxygenSafeThreshold;
            character.Health = Math.Max(0, character.Health - damage * (float)deltaSeconds);
        }
    }

    private IReadOnlyList<RoomOxygenState> CreateEnemyRoomOxygenStates() =>
        EnemyShipLayout.Rooms
            .Select(r => new RoomOxygenState(r.Id, _enemyRoomOxygen.GetValueOrDefault(r.Id, FullOxygen)))
            .ToArray();
}
