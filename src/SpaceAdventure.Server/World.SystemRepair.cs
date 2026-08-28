using System;
using System.Linq;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

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

        // The reactor and its two sibling "boxes" (enemy/weapon overhaul - "реактор и коробки
        // могли быть сломаны") - each a single physical fixture with a plain bool Damaged state
        // (Reactor.Broken/PowerGrid.DistributionBroken/Battery.Broken), same minigame as everything
        // else above.
        StepSystemRepairFor(Ship.ReactorBlock.Id, r => r == Ship.ReactorBlock.RoomId, Ship.ReactorBlock.Position,
            !PowerGrid.Reactor.Broken, deltaSeconds);
        StepSystemRepairFor(Ship.DistributionBlock.Id, r => r == Ship.DistributionBlock.RoomId, Ship.DistributionBlock.Position,
            !PowerGrid.DistributionBroken, deltaSeconds);
        StepSystemRepairFor(Ship.BatteryBlock.Id, r => r == Ship.BatteryBlock.RoomId, Ship.BatteryBlock.Position,
            !PowerGrid.Battery.Broken, deltaSeconds);

        // The helm and the scanner console (enemy/weapon overhaul - "штурвал, сонар можно было
        // сломать") - same plain bool Damaged state, same minigame.
        StepSystemRepairFor(Ship.HelmConsole.Id, r => r == Ship.HelmConsole.RoomId, Ship.HelmConsole.Position,
            !HelmConsoleBroken, deltaSeconds);
        StepSystemRepairFor(Ship.NavigationConsole.Id, r => r == Ship.NavigationConsole.RoomId, Ship.NavigationConsole.Position,
            !NavigationConsoleBroken, deltaSeconds);
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
        else if (id == Ship.ReactorBlock.Id)
            PowerGrid.Reactor.Broken = false;
        else if (id == Ship.DistributionBlock.Id)
            PowerGrid.DistributionBroken = false;
        else if (id == Ship.BatteryBlock.Id)
            PowerGrid.Battery.Broken = false;
        else if (id == Ship.HelmConsole.Id)
            HelmConsoleBroken = false;
        else if (id == Ship.NavigationConsole.Id)
            NavigationConsoleBroken = false;
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
    private IReadOnlyList<ShipSystemState> CreateBlockRepairStates() => new[]
    {
        new ShipSystemState(Ship.ReactorBlock.Id, PowerSystemId.Engine, PowerGrid.Reactor.Broken, GetSystemRepairDisplay(Ship.ReactorBlock.Id)),
        new ShipSystemState(Ship.DistributionBlock.Id, PowerSystemId.Engine, PowerGrid.DistributionBroken, GetSystemRepairDisplay(Ship.DistributionBlock.Id)),
        new ShipSystemState(Ship.BatteryBlock.Id, PowerSystemId.Engine, PowerGrid.Battery.Broken, GetSystemRepairDisplay(Ship.BatteryBlock.Id)),
        new ShipSystemState(Ship.HelmConsole.Id, PowerSystemId.Secondary, HelmConsoleBroken, GetSystemRepairDisplay(Ship.HelmConsole.Id)),
        new ShipSystemState(Ship.NavigationConsole.Id, PowerSystemId.Secondary, NavigationConsoleBroken, GetSystemRepairDisplay(Ship.NavigationConsole.Id)),
    };

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
