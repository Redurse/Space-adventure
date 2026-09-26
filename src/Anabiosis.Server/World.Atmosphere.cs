using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

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
        // One shared local function for both Ship.Doors and Ship.DoorEdges below (humble-soaring-
        // cat.md, "убрать AirlockOuterDoor как отдельный тип") - used to be 3 near-identical loops
        // (interior Door, vacuum-facing AirlockOuterDoor, DoorEdge branching internally to
        // reproduce the AirlockOuterDoor formula). A null far side pins the flow at "leak straight
        // out to vacuum" instead of "equalize with a real neighbor" - UNLESS the ship is docked, in
        // which case that same opening leads onto the station's own pressurized dock chamber
        // instead of space (World.StationDocking.cs), so an ordinary walk ashore must not vent the
        // ship. That guard is scoped to just the vacuum branch, not the whole function - an
        // interior door/edge (both rooms real) still has to keep diffusing while docked.
        void Diffuse(string id, string? roomA, string? roomB)
        {
            if (!IsDoorOpen(id))
                return;
            if (roomA is null || roomB is null)
            {
                if (IsDocked)
                    return;
                if ((roomA ?? roomB) is not { } vacuumRoomId)
                    return;
                var leak = OxygenDiffusionRatePerSecond * _roomOxygen[vacuumRoomId] * (float)deltaSeconds;
                deltas[vacuumRoomId] = deltas.GetValueOrDefault(vacuumRoomId) - leak;
                return;
            }
            var flow = OxygenDiffusionRatePerSecond * (_roomOxygen[roomA] - _roomOxygen[roomB]) * (float)deltaSeconds;
            deltas[roomA] = deltas.GetValueOrDefault(roomA) - flow;
            deltas[roomB] = deltas.GetValueOrDefault(roomB) + flow;
        }
        foreach (var door in Ship.Doors)
            Diffuse(door.Id, door.RoomAId, door.RoomBId);

        // M-doors-as-edges - same Diffuse function above, just against Ship.DoorEdges' own
        // RoomAId/RoomBId (looked up once in Ship.Custom.cs) instead of Doors'.
        foreach (var edge in Ship.DoorEdges)
            Diffuse(edge.Id, edge.RoomAId, edge.RoomBId);
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
            // since it crossed vacuum to get there. IsFullyExposedToVacuum (World.Eva.cs) covers its
            // own, separate, much faster ramp for a character who is outside or in a room with an
            // opening big enough to be full vacuum - this loop is deliberately excluded from those
            // cases now (humble-soaring-cat.md direct user request), both to avoid double-applying
            // damage on top of StepUnsuitedExposure and because an outside character's own RoomId is
            // stale (World.Eva.cs's own doc comment on the field) and was never a safe key into
            // _roomOxygen to begin with.
            if (character.SuitSealed || character.OnStation || character.OnEnemyShip || IsFullyExposedToVacuum(character))
                continue;

            var oxygen = _roomOxygen[character.RoomId];
            if (oxygen >= OxygenSafeThreshold)
                continue;

            var damage = MaxSuffocationDamagePerSecond * (OxygenSafeThreshold - oxygen) / OxygenSafeThreshold;
            character.Health = Math.Max(0, character.Health - damage * (float)deltaSeconds);
        }
    }
}
