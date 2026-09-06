using System;
using System.Linq;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// A damaged system device (World.Wiring.cs's wire-based Damaged flag) no longer fixes with one F
// press, nor with the old skill-based sweeping-tick minigame (M57 - "нельзя было просто так
// починить") - it's a plain elapsed-time timer now: 12 in-game hours of a character standing in
// reach with a wrench/screwdriver in hand, deliberately long enough that it can't be soloed
// through in a moment and instead becomes real content for the "ускорение" transit mode's
// engineer window (M57 design conversation) - several broken devices, several engineers, each
// slowly ticking down over the course of the burn.
public sealed partial class World
{
    private const double SystemRepairDurationSeconds = 12.0 * 3600.0;

    private sealed class SystemRepairProgress
    {
        public double WorkedSeconds;
    }

    private readonly Dictionary<string, SystemRepairProgress> _systemRepairProgress = new();

    // M74 (humble-soaring-cat.md) - generalizes what used to be 5 separately hardcoded fields
    // (ReactorBlock/DistributionBlock/BatteryBlock/HelmConsole/NavigationConsole) into one small
    // per-kind lookup, reused by World.Interact.cs/World.EnemyAi.cs and this file. Devices.First(...)
    // rather than iterating every instance of a kind on purpose: each of these five kinds still only
    // ever has ONE tracked Broken flag for the whole ship, even on a hull with extra reactor/helm/
    // navigation rooms (Ship.cs's own ExtraReactorPositions doc comment) - letting every instance
    // report/repair the same shared flag would need PowerGrid itself to track per-instance state,
    // not just per-system-wide-flag, which stays a deliberately deferred follow-up, not something
    // this milestone invents.
    private static readonly DeviceKind[] RepairableBlockKinds =
    {
        DeviceKind.Reactor, DeviceKind.Distribution, DeviceKind.Battery, DeviceKind.Helm, DeviceKind.Navigation,
    };

    private ShipDevice RepairableBlock(DeviceKind kind) => Ship.Devices.First(d => d.Kind == kind);

    private bool IsBlockBroken(DeviceKind kind) => kind switch
    {
        DeviceKind.Reactor => PowerGrid.Reactor.Broken,
        DeviceKind.Distribution => PowerGrid.DistributionBroken,
        DeviceKind.Battery => PowerGrid.Battery.Broken,
        DeviceKind.Helm => HelmConsoleBroken,
        DeviceKind.Navigation => NavigationConsoleBroken,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private void SetBlockBroken(DeviceKind kind, bool broken)
    {
        switch (kind)
        {
            case DeviceKind.Reactor: PowerGrid.Reactor.Broken = broken; break;
            case DeviceKind.Distribution: PowerGrid.DistributionBroken = broken; break;
            case DeviceKind.Battery: PowerGrid.Battery.Broken = broken; break;
            case DeviceKind.Helm: HelmConsoleBroken = broken; break;
            case DeviceKind.Navigation: NavigationConsoleBroken = broken; break;
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    // Called every tick for every currently-damaged device, every currently-damaged Junction box
    // (game_design.md - "щитки" are their own breakable device too), and every destroyed door on
    // the player's own ship - all driven by the same elapsed-time timer, only advancing while at
    // least one character is actually in reach with the right tool in hand.
    private void StepSystemRepair(double deltaSeconds)
    {
        foreach (var device in Ship.SystemDevices)
            StepSystemRepairFor(device.Id, r => r == device.RoomId, device.Position, IsDeviceConnected(device.Id), deltaSeconds);

        // Hull cameras aren't ShipSystemDevices (WireGraphFactory's own comment explains why), but
        // they're wired into the same graph and damaged/repaired exactly the same way, so they need
        // the identical passive-trickle-plus-sweep treatment or a cut camera would just stay dark
        // forever with no way to work the repair bar at all.
        foreach (var camera in Ship.Cameras)
            StepSystemRepairFor(camera.Id, r => r == camera.RoomId, camera.InteriorPosition, IsDeviceConnected(camera.Id), deltaSeconds);

        foreach (var junction in _components.Where(c => c.Kind == ComponentKind.Junction))
            StepSystemRepairFor(junction.Id, r => r == junction.RoomId, junction.Position, !IsJunctionDamaged(junction.Id), deltaSeconds);

        foreach (var (doorId, connects, position) in AllShipDoors())
            StepSystemRepairFor(doorId, connects, position, !IsDoorDestroyed(doorId), deltaSeconds);

        // The reactor/distribution/battery "boxes" plus the helm and scanner console (enemy/weapon
        // overhaul - "реактор и коробки могли быть сломаны", "штурвал, сонар можно было сломать") -
        // each a single physical fixture with a plain bool Damaged state, same minigame as everything
        // else above. See RepairableBlockKinds's own doc comment for why this is "one instance per
        // kind" rather than looping every Devices entry of a repairable kind.
        foreach (var kind in RepairableBlockKinds)
        {
            var block = RepairableBlock(kind);
            StepSystemRepairFor(block.Id, r => r == block.RoomId, block.Position, !IsBlockBroken(kind), deltaSeconds);
        }

        // Cosmoteer-style marching engines (direct user request) - the Control tile's own seized-
        // throttle state repairs with the same wrench/screwdriver minigame, one instance per engine
        // (unlike RepairableBlockKinds above, there can be more than one of these on a hull).
        foreach (var engine in Ship.Engines)
            StepSystemRepairFor(engine.Id, r => r == engine.RoomId, engine.ControlPosition, !IsEngineControlBroken(engine.Id), deltaSeconds);
    }

    // matchesRoom is a predicate rather than a plain room id because a door (unlike a SystemDevice/
    // Junction, each pinned to exactly one room) connects two rooms at once - AllShipDoors already
    // hands each door the right test (Door.Connects for an interior door, a single-room check for
    // an outer airlock).
    private void StepSystemRepairFor(string id, Func<string, bool> matchesRoom, Vec2 position, bool connected, double deltaSeconds)
    {
        if (connected)
        {
            _systemRepairProgress.Remove(id);
            return;
        }

        if (!_systemRepairProgress.TryGetValue(id, out var progress))
            return;

        // M57 - the Engineer tab's own device list adds a second, remote way to work a repair
        // (Character.EngineerFocusDeviceId) alongside the original physical wrench/screwdriver
        // presence - either one alone is enough, and having both doesn't work it any faster.
        var beingWorked = _characters.Values.Any(c =>
            (matchesRoom(c.RoomId) && (position - c.Position).Length() < InteractionRadius &&
             (c.Inventory.IsHolding(ItemType.Wrench) || c.Inventory.IsHolding(ItemType.Screwdriver)))
            || c.EngineerFocusDeviceId == id);
        if (!beingWorked)
            return; // pauses exactly where it was rather than resetting - walking away costs no progress

        progress.WorkedSeconds += deltaSeconds;
        if (progress.WorkedSeconds >= SystemRepairDurationSeconds)
            FinishSystemRepair(id);
    }

    // The E-key attempt itself (World.Interact.cs) - only ever needs to START the timer; once the
    // entry exists, StepSystemRepairFor above advances it on its own every tick a character stays
    // in reach with the right tool, so a repeated press here is just a harmless no-op.
    private void AttemptSystemRepair(string deviceId)
    {
        if (!_systemRepairProgress.ContainsKey(deviceId))
            _systemRepairProgress[deviceId] = new SystemRepairProgress();
    }

    // A door id never collides with a wiring id (distinct naming conventions - "door-..." vs
    // "system-..."/"junction-..."), and _doorHp only ever gains an entry once DamageDoor has
    // actually run, so this is a reliable way to tell which kind of thing just finished repairing
    // without threading a separate "what is this" tag through the whole minigame.
    private void FinishSystemRepair(string id)
    {
        if (_doorHp.ContainsKey(id))
            _doorHp[id] = DoorMaxHp;
        else if (RepairableBlockKinds.Where(k => RepairableBlock(k).Id == id).Cast<DeviceKind?>().FirstOrDefault() is { } blockKind)
            SetBlockBroken(blockKind, false);
        else if (Ship.Engines.Any(e => e.Id == id))
            RepairEngineControl(id, EnginePartMaxHp); // instant full fix after the long timer, same as SetBlockBroken above
        else
            RepairDeviceWiring(id);
        _systemRepairProgress.Remove(id);
    }

    // M57 - the Engineer tab's own device list needs the reactor/distribution/battery/helm/
    // navigation "boxes" too, not just SystemDevices/Cameras/Junctions/Doors (those already ride
    // the wire in SystemStates/JunctionStates/DoorStates) - these five only ever had their Broken/
    // repair state read locally by World.Interact.cs's own proximity check before now, never
    // resent every tick. Reuses ShipSystemState's exact shape; System is never actually read for
    // these five client-side (the Engineer panel labels them by DeviceId, not by power system).
    private IReadOnlyList<ShipSystemState> CreateBlockRepairStates() => RepairableBlockKinds.Select(kind =>
    {
        var block = RepairableBlock(kind);
        var system = kind is DeviceKind.Helm or DeviceKind.Navigation ? PowerSystemId.Secondary : PowerSystemId.Engine;
        return new ShipSystemState(block.Id, system, IsBlockBroken(kind), GetSystemRepairDisplay(block.Id));
    }).Concat(Ship.Engines.Select(e =>
        new ShipSystemState(e.Id, PowerSystemId.Engine, IsEngineControlBroken(e.Id), GetSystemRepairDisplay(e.Id))))
    .ToList();

    // Test-only convenience, same convention as World.ShipField.cs's DebugPlaceShip: the 12-hour
    // duration above is deliberate real content (this file's own doc comment), not something a
    // unit test should actually sit through tick-by-tick - 1.3 million Step calls per repair test
    // would make the suite unusable. Only ever advances entries StepSystemRepairFor has already
    // started (AttemptSystemRepair) and only takes effect once that same function's own
    // "beingWorked" check next runs, so a test still has to genuinely hold the right tool in reach
    // for the repair to land - this just skips the WAIT, not the requirement.
    public void DebugFastForwardAllRepairs(double seconds)
    {
        foreach (var progress in _systemRepairProgress.Values)
            progress.WorkedSeconds += seconds;
    }

    private float GetSystemRepairDisplay(string deviceId) =>
        _systemRepairProgress.TryGetValue(deviceId, out var progress)
            ? (float)Math.Min(100.0, progress.WorkedSeconds / SystemRepairDurationSeconds * 100.0)
            : 0f;
}
