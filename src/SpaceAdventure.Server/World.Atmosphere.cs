using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Barotrauma-style room atmosphere: each room has an oxygen level (0-100) instead of a flat
// breached/not-breached flag. The Oxygen generator only feeds its own room directly — every
// other room only gets oxygen by it physically diffusing there through doors, room by room, so
// a room far from the generator fills up slower than one right next to it. Every open hull
// breach leaks oxygen back out locally. One breach and a modestly powered generator roughly
// balance out (once the room is actually reached); several breaches drain faster than supply.
public sealed partial class World
{
    private const float FullOxygen = 100f;
    private const float OxygenLeakPerBreachPerSecond = 3f;
    private const float OxygenGenerationPerPowerUnitPerSecond = 0.3f; // 10 power ~= offsets 1 breach
    private const float OxygenDiffusionRatePerSecond = 0.5f; // fraction of a door's level gap equalized per second
    private const float OxygenSafeThreshold = 50f; // characters are fine at/above this level
    private const float MaxSuffocationDamagePerSecond = 10f; // reached only at Oxygen == 0

    private readonly Dictionary<string, float> _roomOxygen = new();

    private void StepAtmosphere(double deltaSeconds)
    {
        var oxygenPower = PowerGrid.GetAllocation(PowerSystemId.Oxygen);
        var generatorRoomId = Ship.SystemDevices.First(d => d.System == PowerSystemId.Oxygen).RoomId;
        _roomOxygen[generatorRoomId] = Math.Min(FullOxygen,
            _roomOxygen[generatorRoomId] + OxygenGenerationPerPowerUnitPerSecond * oxygenPower * (float)deltaSeconds);

        // Diffuse across every door, room to room, based on the level gap — computed as a batch
        // of deltas from the pre-diffusion snapshot so the order doors happen to be processed in
        // doesn't bias the result toward one side of the ship.
        var deltas = new Dictionary<string, float>();
        foreach (var door in Ship.Doors)
        {
            var flow = OxygenDiffusionRatePerSecond * (_roomOxygen[door.RoomAId] - _roomOxygen[door.RoomBId]) * (float)deltaSeconds;
            deltas[door.RoomAId] = deltas.GetValueOrDefault(door.RoomAId) - flow;
            deltas[door.RoomBId] = deltas.GetValueOrDefault(door.RoomBId) + flow;
        }
        foreach (var (roomId, delta) in deltas)
            _roomOxygen[roomId] += delta;

        foreach (var room in Ship.Rooms)
        {
            var breachCount = Ship.WallBlocks.Count(b => b.RoomId == room.Id && _breachedWallBlockIds.Contains(b.Id));
            var oxygen = _roomOxygen[room.Id] - OxygenLeakPerBreachPerSecond * breachCount * (float)deltaSeconds;
            _roomOxygen[room.Id] = Math.Clamp(oxygen, 0f, FullOxygen);
        }

        foreach (var character in _characters.Values)
        {
            if (character.WearingSuit)
                continue;

            var oxygen = _roomOxygen[character.RoomId];
            if (oxygen >= OxygenSafeThreshold)
                continue;

            var damage = MaxSuffocationDamagePerSecond * (OxygenSafeThreshold - oxygen) / OxygenSafeThreshold;
            character.Health = Math.Max(0, character.Health - damage * (float)deltaSeconds);
        }
    }
}
