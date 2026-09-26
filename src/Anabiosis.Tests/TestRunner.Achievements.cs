using System;
using System.IO;
using System.Linq;
using Anabiosis.Client.Achievements;
using Anabiosis.Server;

internal static partial class TestRunner
{
    // Direct user request ("система достижений... максимум только 1 раз") - AchievementTracker is
    // checked purely against synthetic WorldSnapshot copies here (World.CreateSnapshot() + `with`),
    // not real gameplay - the point is to pin down the TRACKER's own unlock-once/persistence/toast-
    // queue behavior, not to re-derive real docking/combat/etc. physics that already have their own
    // test coverage elsewhere in this suite.
    private static string TempAchievementsPath() => Path.Combine(Path.GetTempPath(), $"achievements-test-{Guid.NewGuid():N}.json");

    private static bool Achievements_UnlockOnceAndPersistsAcrossTrackerInstances()
    {
        var path = TempAchievementsPath();
        try
        {
            var baseSnapshot = new World().CreateSnapshot();
            var tracker = new AchievementTracker(path);

            if (tracker.IsUnlocked("credits_1000"))
                return false; // starting credits (World.Trade.cs's own 300) must not already qualify

            var richSnapshot = baseSnapshot with { Credits = 5000 };
            tracker.Update(richSnapshot, myPlayerId: 1, deltaSeconds: 0.016);
            if (!tracker.IsUnlocked("credits_1000") || !tracker.IsUnlocked("credits_5000") || tracker.IsUnlocked("credits_20000"))
                return false;
            if (tracker.UnlockedAt("credits_1000") is not { } firstUnlockTime)
                return false;

            // Re-running the SAME qualifying snapshot must not re-timestamp (i.e. not re-unlock).
            tracker.Update(richSnapshot, myPlayerId: 1, deltaSeconds: 0.016);
            if (tracker.UnlockedAt("credits_1000") != firstUnlockTime)
                return false;

            // A fresh tracker instance pointed at the same file must see it as already unlocked -
            // the whole point of a player-level file rather than a save-level one.
            var reloaded = new AchievementTracker(path);
            return reloaded.IsUnlocked("credits_1000") && reloaded.IsUnlocked("credits_5000") && !reloaded.IsUnlocked("credits_20000");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool Achievements_ToastQueueShowsOneAtATime()
    {
        var path = TempAchievementsPath();
        try
        {
            var baseSnapshot = new World().CreateSnapshot();
            var tracker = new AchievementTracker(path);

            // Two thresholds cleared on the very same tick - both unlock at once, but only one
            // toast shows until its own timer runs out.
            var richSnapshot = baseSnapshot with { Credits = 5000 };
            tracker.Update(richSnapshot, myPlayerId: 1, deltaSeconds: 0.016);
            if (tracker.CurrentToast is not { } firstToast)
                return false;

            tracker.Update(richSnapshot, myPlayerId: 1, deltaSeconds: AchievementTracker.ToastDurationSeconds + 0.1);
            if (tracker.CurrentToast is not { } secondToast || secondToast.Id == firstToast.Id)
                return false;

            tracker.Update(richSnapshot, myPlayerId: 1, deltaSeconds: AchievementTracker.ToastDurationSeconds + 0.1);
            return tracker.CurrentToast is null;
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Direct near-miss caught in review, not a hypothetical - a fresh campaign STARTS already
    // docked at the home station (World.cs's own "a fresh run starts docked" doc comment), so a
    // bare "docked now" check would have unlocked this on tick 0 of any new game, never
    // representing a real accomplishment. HasReDocked requires having left first.
    private static bool Achievements_FirstDock_RequiresHavingLeftFirst()
    {
        var path = TempAchievementsPath();
        try
        {
            var baseSnapshot = new World().CreateSnapshot();
            if (baseSnapshot.Voyage.DockedPointId is null)
                return false; // the premise this test is guarding against no longer holds - investigate, don't just pass
            var tracker = new AchievementTracker(path);

            // Still docked from campaign start - must NOT unlock yet.
            tracker.Update(baseSnapshot, myPlayerId: 1, deltaSeconds: 0.016);
            if (tracker.IsUnlocked("first_dock"))
                return false;

            // Undock, fly around a while, then dock again elsewhere - only NOW should it unlock.
            var undocked = baseSnapshot with { Voyage = baseSnapshot.Voyage with { DockedPointId = null } };
            tracker.Update(undocked, myPlayerId: 1, deltaSeconds: 0.016);
            if (tracker.IsUnlocked("first_dock"))
                return false;

            var redocked = baseSnapshot with { Voyage = baseSnapshot.Voyage with { DockedPointId = "some-other-station" } };
            tracker.Update(redocked, myPlayerId: 1, deltaSeconds: 0.016);
            return tracker.IsUnlocked("first_dock");
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Same shape of near-miss as first_dock above - EnemyShip.Rooms is the enemy hull's own static
    // room TEMPLATE (World.cs's CreateSnapshot builds it unconditionally, every tick, regardless of
    // whether any boarding is actually in progress), never empty from tick 0 of any campaign.
    // CharacterState.OnEnemyShip is the real "standing over there right now" fact.
    private static bool Achievements_Boarding_IsNotAlwaysTrueFromTemplateRooms()
    {
        var path = TempAchievementsPath();
        try
        {
            var baseSnapshot = new World().CreateSnapshot();
            if (baseSnapshot.EnemyShip.Rooms.Count == 0)
                return false; // the premise this test is guarding against no longer holds - investigate, don't just pass
            var tracker = new AchievementTracker(path);

            tracker.Update(baseSnapshot, myPlayerId: 1, deltaSeconds: 0.016);
            return !tracker.IsUnlocked("boarding");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool Achievements_CatalogHasNoDuplicateIds()
    {
        var ids = AchievementCatalog.All.Select(a => a.Id).ToList();
        return ids.Count == ids.Distinct().Count() && ids.Count == 20;
    }
}
