using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Achievements;

// Direct user request ("сделай чтобы... максимум только 1 раз... высвечивалось выполнение
// достижения... буквально как в Стиме") - owns the persisted unlocked-set (AchievementStore, a
// player-level file - see its own doc comment) and the bottom-right toast's own queue/timer, so
// Game1.cs only ever needs to call Update once a frame and read CurrentToast to draw it. Checked
// purely against the ordinary per-tick WorldSnapshot already flowing through the client - no new
// server/protocol surface at all.
public sealed class AchievementTracker
{
    // Direct user request ("буквально как в Стиме") - Steam's own toast sits for a few seconds
    // before fading; long enough to actually read the name+description, short enough not to nag
    // through a whole play session if several unlock back to back (queued, one at a time, below).
    public const float ToastDurationSeconds = 5f;

    private readonly string? _storePath;
    private readonly Dictionary<string, DateTime> _unlocked;
    private readonly Queue<AchievementDefinition> _pendingToasts = new();
    private string? _firstSeenSystemId;

    public AchievementTracker(string? storePath = null)
    {
        _storePath = storePath;
        _unlocked = AchievementStore.Load(storePath);
    }

    public bool IsUnlocked(string id) => _unlocked.ContainsKey(id);
    public DateTime? UnlockedAt(string id) => _unlocked.TryGetValue(id, out var at) ? at : null;

    public AchievementDefinition? CurrentToast { get; private set; }
    public float CurrentToastRemainingSeconds { get; private set; }

    // "traveler"'s own condition (AchievementCatalog.All) - remembers whichever system this
    // session first observed, entirely local/in-memory (not persisted - a fresh launch just
    // re-learns "home" from wherever the very next snapshot says, harmless either way).
    public bool HasChangedSystem(string currentSystemId)
    {
        _firstSeenSystemId ??= currentSystemId;
        return currentSystemId != _firstSeenSystemId;
    }

    // "first_dock"'s own condition - direct user bug near-miss caught in review: a fresh campaign
    // STARTS already docked at the home station (World.cs's own "A fresh run starts docked" doc
    // comment), so a bare `Voyage.DockedPointId is not null` check would unlock the instant any
    // session begins, never representing a real accomplishment. This requires having been undocked
    // at least once first - the same "remember one bit, in memory only" shape HasChangedSystem
    // above already uses.
    private bool _hasEverBeenUndocked;
    public bool HasReDocked(bool dockedNow)
    {
        if (!dockedNow)
        {
            _hasEverBeenUndocked = true;
            return false;
        }
        return _hasEverBeenUndocked;
    }

    public void Update(WorldSnapshot snapshot, int myPlayerId, double deltaSeconds)
    {
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == myPlayerId);
        var unlockedJustNow = false;
        foreach (var achievement in AchievementCatalog.All)
        {
            if (_unlocked.ContainsKey(achievement.Id))
                continue;
            if (!achievement.Condition(snapshot, me, this))
                continue;
            _unlocked[achievement.Id] = DateTime.UtcNow;
            _pendingToasts.Enqueue(achievement);
            unlockedJustNow = true;
        }
        if (unlockedJustNow)
            AchievementStore.Save(_unlocked, _storePath); // one write covers every achievement this tick, not one per unlock

        if (CurrentToast is not null)
        {
            CurrentToastRemainingSeconds -= (float)deltaSeconds;
            if (CurrentToastRemainingSeconds <= 0f)
                CurrentToast = null;
        }
        if (CurrentToast is null && _pendingToasts.Count > 0)
        {
            CurrentToast = _pendingToasts.Dequeue();
            CurrentToastRemainingSeconds = ToastDurationSeconds;
        }
    }
}
