using System;
using System.Linq;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Barotrauma-style repair minigame for a damaged system device (World.Wiring.cs's wire-based
// Damaged flag) - replaces the old instant "press F once, fixed" with a slow passive fill plus a
// sweeping tick: landing an extra F press while the tick sits inside the already-filled part of
// the bar adds a bonus chunk of progress and starts the sweep over, instead of the whole repair
// being one single press.
public sealed partial class World
{
    private const float SystemRepairPassivePercentPerSecond = 4f; // ~25s alone, with no bonus hits
    private const float SystemRepairHitBonusPercent = 15f;
    private const float SystemRepairTickSweepPerSecond = 0.6f; // full sweep of the bar in ~1.7s

    private sealed class SystemRepairProgress
    {
        public float Percent;
        public float TickPosition;
        public int TickDirection = 1;
    }

    private readonly Dictionary<string, SystemRepairProgress> _systemRepairProgress = new();

    // Called every tick for every currently-damaged device, every currently-damaged Junction box
    // (game_design.md - "щитки" are their own breakable device now, same minigame), and every
    // destroyed door on the player's own ship (same minigame again) - the passive trickle and the
    // sweeping tick only advance while at least one character is actually in reach with the right
    // tool in hand - walk away mid-repair and it just pauses exactly where it was, rather than
    // resetting.
    private void StepSystemRepair(double deltaSeconds)
    {
        foreach (var device in Ship.SystemDevices)
            StepSystemRepairFor(device.Id, r => r == device.RoomId, device.Position, IsDeviceConnected(device.Id), deltaSeconds);

        foreach (var junction in _components.Where(c => c.Kind == ComponentKind.Junction))
            StepSystemRepairFor(junction.Id, r => r == junction.RoomId, junction.Position, !IsJunctionDamaged(junction.Id), deltaSeconds);

        foreach (var (doorId, connects, position) in AllShipDoors())
            StepSystemRepairFor(doorId, connects, position, !IsDoorDestroyed(doorId), deltaSeconds);
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

        var beingWorked = _characters.Values.Any(c =>
            matchesRoom(c.RoomId) && (position - c.Position).Length() < InteractionRadius &&
            (c.Inventory.IsHolding(ItemType.Wrench) || c.Inventory.IsHolding(ItemType.Screwdriver)));
        if (!beingWorked)
            return;

        progress.TickPosition += progress.TickDirection * SystemRepairTickSweepPerSecond * (float)deltaSeconds;
        if (progress.TickPosition >= 1f)
        {
            progress.TickPosition = 1f;
            progress.TickDirection = -1;
        }
        else if (progress.TickPosition <= 0f)
        {
            progress.TickPosition = 0f;
            progress.TickDirection = 1;
        }

        progress.Percent = Math.Min(100f, progress.Percent + SystemRepairPassivePercentPerSecond * (float)deltaSeconds);
        if (progress.Percent >= 100f)
            FinishSystemRepair(id);
    }

    // The F-key attempt itself (World.Interact.cs) - starts the bar the first time (nothing to hit
    // yet, so it's just a no-op start), and every press after that checks the sweep: landing it
    // inside the already-filled part is a hit, worth a bonus chunk of progress and a fresh sweep
    // from the start; landing outside it does nothing this time - no penalty, just no bonus.
    private void AttemptSystemRepair(string deviceId)
    {
        if (!_systemRepairProgress.TryGetValue(deviceId, out var progress))
        {
            progress = new SystemRepairProgress();
            _systemRepairProgress[deviceId] = progress;
        }

        if (progress.TickPosition > progress.Percent / 100f)
            return; // missed - the sweep keeps going, nothing lost

        progress.Percent = Math.Min(100f, progress.Percent + SystemRepairHitBonusPercent);
        progress.TickPosition = 0f;
        progress.TickDirection = 1;
        if (progress.Percent >= 100f)
            FinishSystemRepair(deviceId);
    }

    // A door id never collides with a wiring id (distinct naming conventions - "door-..." vs
    // "system-..."/"junction-..."), and _doorHp only ever gains an entry once DamageDoor has
    // actually run, so this is a reliable way to tell which kind of thing just finished repairing
    // without threading a separate "what is this" tag through the whole minigame.
    private void FinishSystemRepair(string id)
    {
        if (_doorHp.ContainsKey(id))
            _doorHp[id] = DoorMaxHp;
        else
            RepairDeviceWiring(id);
        _systemRepairProgress.Remove(id);
    }

    private (float Percent, float TickPosition) GetSystemRepairDisplay(string deviceId) =>
        _systemRepairProgress.TryGetValue(deviceId, out var progress) ? (progress.Percent, progress.TickPosition) : (0f, 0f);
}
