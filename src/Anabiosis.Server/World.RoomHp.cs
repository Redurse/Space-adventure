using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Each compartment's own health (direct user request, "у каждого отсека своё хп... как в Cosmoteer, но по
// отсекам"). A compartment's health is not a separate number that is taken off by hits: it is READ OFF the
// state of its own walls, so it behaves exactly as the player describes without any extra bookkeeping:
//
//   * every hit on one of the compartment's walls (wall block or engine bulkhead/nozzle - a block that is
//     merely damaged, not yet breached, counts for what it has lost) lowers it;
//   * it reaches zero when a THIRD of the compartment's total wall hit points is gone - in practice about a
//     third of its wall blocks breached;
//   * welding a breach (or any damaged wall) gives back exactly what that wall had taken, because the number
//     is recomputed from the wall's hit points every time it is asked for.
//
// At zero the compartment is destroyed (World.ShipBlasts.cs): a large explosion in its place that hurts
// whatever is around it, nothing left where it stood (open space), and whatever that leaves cut off from the reactor
// breaking away as its own tumbling piece (World.ShipDebris.cs). If it is the reactor compartment the whole
// ship goes.
//
// The old shadow pool (_roomHp) survives only as a second, non-wall term - damage that is not to a wall at all
// (the sun's heat, World.SunZone.cs) cannot be welded away, so it stays a separate multiplier.
public sealed partial class World
{
    public const float RoomMaxHp = 1000f;

    // A compartment is destroyed once this share of its total wall hit points is gone.
    public const float RoomDestructionWallShare = 1f / 3f;

    // Non-wall damage only (sun heat) - 1000 = untouched. Walls are not in here (see the header).
    private readonly Dictionary<string, float> _roomHp = new();

    // Called from InitializeShipState (constructor + every hull swap), same convention as
    // InitializeWallBlocks/InitializeEngines - every room starts at full health.
    private void InitializeRoomHp()
    {
        _roomHp.Clear();
        foreach (var room in Ship.Rooms)
            _roomHp[room.Id] = RoomMaxHp;
    }

    private float RoomExtraHp(string roomId) => _roomHp.TryGetValue(roomId, out var hp) ? hp : RoomMaxHp;

    // (total, lost) hit points of the compartment's walls: its wall blocks, and the bulkhead and nozzle of any
    // engine standing in it (they hold pressure and take hits exactly like wall blocks do).
    private (float Total, float Lost) RoomWallHitPoints(string roomId)
    {
        var total = 0f;
        var lost = 0f;
        foreach (var block in Ship.WallBlocks)
        {
            if (block.RoomId != roomId)
                continue;
            var max = MaxHpFor(block);
            total += max;
            lost += max - Math.Clamp(WallBlockHp(block.Id), 0f, max);
        }
        foreach (var engine in Ship.Engines)
        {
            if (engine.RoomId != roomId)
                continue;
            total += 2f * EnginePartMaxHp;
            lost += EnginePartMaxHp - Math.Clamp(EngineBulkheadHp(engine.Id), 0f, EnginePartMaxHp);
            lost += EnginePartMaxHp - Math.Clamp(EngineNozzleHp(engine.Id), 0f, EnginePartMaxHp);
        }
        return (total, lost);
    }

    // 1 = every wall intact, 0 = destroyed. The walls' part reaches zero at RoomDestructionWallShare of the
    // wall hit points lost; a compartment with no walls of its own (fully interior) has nothing to lose that
    // way, only non-wall damage can destroy it.
    public float RoomIntegrity(string roomId)
    {
        var (total, lost) = RoomWallHitPoints(roomId);
        var wallPart = total <= 0f ? 1f : Math.Clamp(1f - lost / (total * RoomDestructionWallShare), 0f, 1f);
        return wallPart * Math.Clamp(RoomExtraHp(roomId) / RoomMaxHp, 0f, 1f);
    }

    // What the helm's "Отсеки" tab and the on-room damage look read: integrity on the old 0..RoomMaxHp scale.
    private float RoomHp(string roomId) => RoomIntegrity(roomId) * RoomMaxHp;

    // Non-wall damage to a room (the sun's heat). Wall damage does NOT go through here - walls lose their own
    // hit points, and the room's health follows from them (RoomIntegrity).
    private void DamageRoom(string roomId, float amount)
    {
        if (!_roomHp.ContainsKey(roomId))
            return;
        _roomHp[roomId] = Math.Max(0f, RoomExtraHp(roomId) - amount);
        CheckRoomDestroyed(roomId);
    }

    // Called after anything that can lower a compartment's health: if it just ran out, the compartment is
    // destroyed. Quietly ignores a room that no longer exists (a stale id) or that is already on its way out.
    // Test-only: some tests simulate many minutes of undefended enemy fire just to get a breach or a drained room, and
    // would now see the ship blow up along the way - they switch destruction off and test their own thing.
    public bool DebugRoomsIndestructible { get; set; }

    private void CheckRoomDestroyed(string roomId)
    {
        if (DebugRoomsIndestructible || !_roomHp.ContainsKey(roomId) || _doomedRooms.Contains(roomId))
            return;
        if (RoomIntegrity(roomId) > 0f)
            return;
        DestroyRoom(roomId);
    }

    // A compartment that has lost a third of its walls disappears from the ship entirely (direct user request) - reuses World.ShipDebris.cs's own
    // TryComputeRoomDetachment (the exact same "remove roomId, and detach anything ELSE that becomes
    // unreachable from the reactor as a result" computation), differing only in what happens to roomId's own
    // footprint once it's gone: an exploding compartment leaves nothing at all - its place is open space -
    // while ANY other room that becomes unreachable purely as
    // a side effect of this one disappearing still detaches and flies off as its own tumbling piece (the hull
    // really has split in two). Returns whether the ship actually changed.
    private bool ExplodeRoom(string roomId, Vec2 blastCenter)
    {
        if (!TryComputeRoomDetachment(roomId, out var shrunk, out var detachedRooms))
            return false;

        var detachedRoomIds = detachedRooms.Select(r => r.Id).ToHashSet();
        EjectCrewFromDetachingRooms(detachedRoomIds);
        _droppedItems.RemoveAll(item => item.RoomId is not null && detachedRoomIds.Contains(item.RoomId));

        // The exploded room itself is simply gone (direct user request: it vanishes as a compartment, nothing of it
        // is left behind and its place is open space - nobody can walk there any more); any OTHER room dragged down
        // with it flies off as a real, independent fragment - only spawn one if there's actually something left in it.
        var otherDetached = detachedRooms.Where(r => r.Id != roomId).ToList();
        if (otherDetached.Count > 0)
            SpawnDebrisFragment(otherDetached, blastCenter);

        // ApplyShipDefinition reconciles every per-room dictionary (this room's and any co-detached one's
        // entries are dropped) and, for a structural change that touches devices, now keeps every surviving
        // wall's damage instead of healing the whole hull (World.ShipBuilding.cs).
        ApplyShipDefinition(shrunk);
        return true;
    }

    private IReadOnlyList<RoomHpState> CreateRoomHpStates() =>
        Ship.Rooms.Select(r => new RoomHpState(r.Id, RoomHp(r.Id), RoomMaxHp)).ToArray();
}
