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
        var oxygenPower = GetEffectivePower(PowerSystemId.Oxygen);
        var generatorRoomId = Ship.SystemDevices.First(d => d.System == PowerSystemId.Oxygen).RoomId;
        _roomOxygen[generatorRoomId] = Math.Min(FullOxygen,
            _roomOxygen[generatorRoomId] + OxygenGenerationPerPowerUnitPerSecond * oxygenPower * (float)deltaSeconds);

        // Diffuse across every OPEN door, room to room, based on the level gap — computed as a
        // batch of deltas from the pre-diffusion snapshot so the order doors happen to be
        // processed in doesn't bias the result toward one side of the ship. A closed door blocks
        // this entirely (game_design.md Phase 3, M16 - airtight compartments).
        var deltas = new Dictionary<string, float>();
        foreach (var door in Ship.Doors)
        {
            if (!IsDoorOpen(door.Id))
                continue;
            var flow = OxygenDiffusionRatePerSecond * (_roomOxygen[door.RoomAId] - _roomOxygen[door.RoomBId]) * (float)deltaSeconds;
            deltas[door.RoomAId] = deltas.GetValueOrDefault(door.RoomAId) - flow;
            deltas[door.RoomBId] = deltas.GetValueOrDefault(door.RoomBId) + flow;
        }

        // An open AirlockOuterDoor exposes its chamber directly to vacuum - same diffusion
        // formula as an interior door, just with the far side pinned at 0 instead of another
        // room's level. A door standing wide open to space drains far faster than any single
        // hull breach, which is exactly the point of it being a deliberate, undoable choice.
        // ...unless the ship is docked, in which case that same door opens onto the station's own
        // pressurized dock chamber rather than onto space (World.StationDocking.cs) - walking
        // ashore is a normal thing to do and must not vent the ship on the way.
        foreach (var outerDoor in Ship.AirlockOuterDoors)
        {
            if (!IsDoorOpen(outerDoor.Id) || IsDocked)
                continue;
            var flow = OxygenDiffusionRatePerSecond * _roomOxygen[outerDoor.RoomId] * (float)deltaSeconds;
            deltas[outerDoor.RoomId] = deltas.GetValueOrDefault(outerDoor.RoomId) - flow;
        }

        foreach (var (roomId, delta) in deltas)
            _roomOxygen[roomId] += delta;

        foreach (var room in Ship.Rooms)
        {
            // Scales with how badly each block is actually hurt, not just whether it's fully
            // breached - a wall dented but not yet through leaks a little, one punched clean
            // through leaks the full rate, and everything in between is a straight ramp.
            var leak = Ship.WallBlocks.Where(b => b.RoomId == room.Id)
                .Sum(b => OxygenLeakPerBreachPerSecond * (1f - WallBlockHp(b.Id) / WallBlockMaxHp));
            var oxygen = _roomOxygen[room.Id] - leak * (float)deltaSeconds;
            _roomOxygen[room.Id] = Math.Clamp(oxygen, 0f, FullOxygen);
        }

        foreach (var character in _characters.Values)
        {
            // Station and enemy-ship rooms aren't part of _roomOxygen at all (no atmosphere/breach
            // simulation in either structure) - and a boarding party is necessarily suited anyway,
            // since it crossed vacuum to get there.
            if (character.SuitSealed || character.OnStation || character.OnEnemyShip)
                continue;

            var oxygen = _roomOxygen[character.RoomId];
            if (oxygen >= OxygenSafeThreshold)
                continue;

            var damage = MaxSuffocationDamagePerSecond * (OxygenSafeThreshold - oxygen) / OxygenSafeThreshold;
            character.Health = Math.Max(0, character.Health - damage * (float)deltaSeconds);
        }
    }
}
