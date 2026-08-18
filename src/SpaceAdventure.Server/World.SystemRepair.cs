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

    // Called every tick for every currently-damaged device: the passive trickle and the sweeping
    // tick only advance while at least one character is actually in reach with the right tool in
    // hand - walk away mid-repair and it just pauses exactly where it was, rather than resetting.
    private void StepSystemRepair(double deltaSeconds)
    {
        foreach (var device in Ship.SystemDevices)
        {
            if (IsDeviceConnected(device.Id))
            {
                _systemRepairProgress.Remove(device.Id);
                continue;
            }

            if (!_systemRepairProgress.TryGetValue(device.Id, out var progress))
                continue;

            var beingWorked = _characters.Values.Any(c =>
                c.RoomId == device.RoomId && (device.Position - c.Position).Length() < InteractionRadius &&
                (c.Inventory.IsHolding(ItemType.Wrench) || c.Inventory.IsHolding(ItemType.Screwdriver)));
            if (!beingWorked)
                continue;

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
                FinishSystemRepair(device.Id);
        }
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

    private void FinishSystemRepair(string deviceId)
    {
        RepairDeviceWiring(deviceId);
        _systemRepairProgress.Remove(deviceId);
    }

    private (float Percent, float TickPosition) GetSystemRepairDisplay(string deviceId) =>
        _systemRepairProgress.TryGetValue(deviceId, out var progress) ? (progress.Percent, progress.TickPosition) : (0f, 0f);
}
