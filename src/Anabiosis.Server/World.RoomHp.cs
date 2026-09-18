using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Direct user request ("скрытое число хп" per compartment, test value 1000, "при 0 - отсек
// взрывается и физически перестаёт существовать, а на его месте будут обломки") - a shadow damage
// pool per Room, invisible to the player until the new "Отсеки" helm tab reveals it, fed by every
// EXISTING source of damage to something inside that room (there's no new weapon/hazard here, just
// an aggregate of what already breaks today) rather than any new, independent damage source.
public sealed partial class World
{
    public const float RoomMaxHp = 1000f;

    private readonly Dictionary<string, float> _roomHp = new();

    // Called from InitializeShipState (constructor + every hull swap), same convention as
    // InitializeWallBlocks/InitializeEngines - every room starts at full health.
    private void InitializeRoomHp()
    {
        _roomHp.Clear();
        foreach (var room in Ship.Rooms)
            _roomHp[room.Id] = RoomMaxHp;
    }

    private float RoomHp(string roomId) => _roomHp.TryGetValue(roomId, out var hp) ? hp : RoomMaxHp;

    // The one entry point every existing damage call site below feeds into - never a source of
    // damage on its own. Silently ignored for a roomId this dictionary doesn't know about (a stale
    // id from a room that already exploded, or a station/enemy-hull room this pool was never meant
    // to track) rather than throwing, the same "quiet, best-effort" shape World.Atmosphere.cs's own
    // room-oxygen lookups already use for an unrecognized id.
    private void DamageRoom(string roomId, float amount)
    {
        if (!_roomHp.ContainsKey(roomId))
            return;
        var next = Math.Max(0f, RoomHp(roomId) - amount);
        _roomHp[roomId] = next;
        if (next <= 0f)
            ExplodeRoom(roomId);
    }

    // Direct user request ("отсек физически перестает существовать на корабле полностью, он
    // удаляется везде где он используется, а на месте взорванного отсека будут всякие обломки") -
    // reuses World.ShipDebris.cs's own TryComputeRoomDetachment (the exact same "remove roomId, and
    // detach anything ELSE that becomes unreachable from the reactor as a result" computation
    // DestroyRoomAndDetach already does for a combat wall-breach), differing only in what happens to
    // roomId's own footprint once it's gone: DestroyRoomAndDetach flies the whole detached group away
    // together (a wall genuinely failing structurally is a believable "it broke off"); an exploding
    // compartment instead leaves a stationary wreck sitting right where it was, still attached to and
    // moving with the rest of the ship - ANY other room that becomes unreachable purely as a side
    // effect of this one disappearing still detaches and flies off exactly as before (the hull really
    // has split into two pieces at that point, same as a wall-breach detachment would produce).
    private void ExplodeRoom(string roomId)
    {
        if (!TryComputeRoomDetachment(roomId, out var shrunk, out var detachedRooms))
            return;

        var detachedRoomIds = detachedRooms.Select(r => r.Id).ToHashSet();
        EjectCrewFromDetachingRooms(detachedRoomIds);
        _droppedItems.RemoveAll(item => item.RoomId is not null && detachedRoomIds.Contains(item.RoomId));

        // The exploded room itself stays put as a permanent wreck decoration (a new entry in
        // Ship.WreckPatches, explicitly round-tripped by Ship.ToDefinition() so it survives every
        // later build/demolish/explosion - CustomShipDefinition.WreckPatches' own doc comment); any
        // OTHER room dragged down with it (the hull genuinely split) still flies off as a real,
        // independent debris fragment, exactly as DestroyRoomAndDetach already does for a wall
        // breach - only spawn one if there's actually something left in it.
        var explodedRoom = detachedRooms.First(r => r.Id == roomId);
        var otherDetached = detachedRooms.Where(r => r.Id != roomId).ToList();
        if (otherDetached.Count > 0)
            SpawnDebrisFragment(otherDetached);

        var patch = new RectF(explodedRoom.X, explodedRoom.Y, explodedRoom.Width, explodedRoom.Height);
        var withWreck = shrunk with { WreckPatches = shrunk.WreckPatches.Append(patch).ToList() };

        // ApplyShipDefinition's own room-id reconcile (World.ShipBuilding.cs) already drops every
        // stale _roomHp entry (this room's and any co-detached one's) as part of applying `withWreck`.
        ApplyShipDefinition(withWreck);
    }

    // A door borders one or two rooms rather than sitting inside a single one (Door.RoomAId/RoomBId,
    // or AirlockOuterDoor's single RoomId with vacuum on the other side) - damages whichever of them
    // actually exist rather than requiring callers (DamageDoor/ChopDoor, World.Doors.cs) to know
    // which shape of door they're dealing with.
    private void DamageRoomsForDoor(string doorId, float amount)
    {
        if (Ship.Doors.FirstOrDefault(d => d.Id == doorId) is { } door)
        {
            DamageRoom(door.RoomAId, amount);
            DamageRoom(door.RoomBId, amount);
        }
        else if (Ship.AirlockOuterDoors.FirstOrDefault(d => d.Id == doorId) is { } airlock)
        {
            DamageRoom(airlock.RoomId, amount);
        }
    }

    private IReadOnlyList<RoomHpState> CreateRoomHpStates() =>
        Ship.Rooms.Select(r => new RoomHpState(r.Id, RoomHp(r.Id), RoomMaxHp)).ToArray();
}
