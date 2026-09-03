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

        // M72 (humble-soaring-cat.md) - leak now reads Ship.Tiles instead of trusting the
        // WallBlock.IsInterior flag: a block "borders vacuum" (leaks) exactly when at least one of
        // its own tile's four neighbors has no floor at all (true open space), and "doesn't"
        // (interior bulkhead, both sides already pressurized) when every neighbor is itself part of
        // the hull - a direct, geometric re-derivation of the same distinction IsInterior used to
        // hard-code at generation time, kept in sync with live damage via World.TileSync.cs. Oxygen
        // storage/diffusion above is untouched (still keyed by Room.Id, still walks Ship.Doors
        // directly) - Room.Id stays the authoritative room identity everywhere else in World until
        // M73, and Ship.Doors already encodes exactly the right room pairs, so migrating that half
        // too would be pure churn with no behavior difference for today's rectangular hulls.
        foreach (var room in Ship.Rooms)
        {
            var leak = 0f;
            foreach (var block in Ship.WallBlocks.Where(b => b.RoomId == room.Id))
            {
                var coord = TileGridRasterizer.WallBlockTileCoord(block, Ship.Rooms, room);
                if (Ship.Tiles.CellAt(coord) is not { Wall: TileWallKind.Solid } cell)
                    continue;
                var bordersVacuum = TileSideExtensions.All.Any(side => Ship.Tiles.CellAt(side.Offset(coord)) is not { HasFloor: true });
                if (!bordersVacuum)
                    continue;
                leak += OxygenLeakPerBreachPerSecond * (1f - cell.WallHp / WallMaterialDefaults.MaxHp(block.Material));
            }
            // Cosmoteer-style marching engines (direct user request) - a breached Bulkhead tile
            // leaks exactly like a breached WallBlock (World.Engines.cs's own TotalEngineLeakInRoom).
            leak += TotalEngineLeakInRoom(room.Id);
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
