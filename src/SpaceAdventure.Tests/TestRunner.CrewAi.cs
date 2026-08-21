using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

// Hiring bot crew from the station Recruiter (game_design.md section 10) and what each hired role
// actually does on its own every tick (World.CrewAi.cs). The home station is an Outpost, which now
// staffs a Recruiter (Station.Default.cs), so every test here can hire right from World's own
// starting dock without traveling anywhere first.
internal static partial class TestRunner
{
    private static bool World_Recruiting_RosterOffersCandidatesAtHomeStation()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var roster = world.CreateSnapshot().RecruitCandidates;
        return roster.Count > 0 && roster.All(c => c.Cost > 0);
    }

    private static bool World_Recruiting_HireDeductsCreditsAndAddsBotToCrew()
    {
        var world = new World();
        world.SpawnCharacter(1);
        // Funded generously rather than relying on the default starting credits covering whichever
        // candidate the RNG happens to roll first - the static seed counter shifts with every test
        // registered earlier in the array, so a candidate cost that fit by coincidence today can
        // stop fitting the moment an unrelated test is added upstream of this one.
        FundGenerously(world);
        var before = world.CreateSnapshot();
        var candidate = before.RecruitCandidates[0];

        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: candidate.Id));

        var after = world.CreateSnapshot();
        var bot = after.Characters.FirstOrDefault(c => c.IsBot);
        return after.Credits == before.Credits - candidate.Cost &&
            bot is not null && bot.BotName == candidate.Name && bot.Role == candidate.Role &&
            after.RecruitCandidates.All(c => c.Id != candidate.Id) &&
            after.Characters.Count(c => c.IsBot) == 1;
    }

    // An id that isn't on the current board at all is the simplest way to fail the gate - covers
    // typos/stale clicks (a candidate hired or re-rolled between the click and the command landing).
    private static bool World_Recruiting_HireFailsForUnknownCandidateId()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var before = world.CreateSnapshot();

        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: "not-a-real-candidate"));

        var after = world.CreateSnapshot();
        return after.Credits == before.Credits && after.Characters.All(c => !c.IsBot);
    }

    // Hires from the initial 3-candidate roster, cheapest first, until credits can't cover what's
    // left - StartingCredits (300) against any 3 distinct roles' base costs (every combination
    // totals well over 300) always runs out within that same roster, so this never needs a re-roll
    // or gets anywhere near the 4-bot cap.
    private static bool World_Recruiting_HireFailsWithoutEnoughCredits()
    {
        var world = new World();
        world.SpawnCharacter(1);

        while (world.CreateSnapshot().RecruitCandidates.Count > 0)
        {
            var cheapest = world.CreateSnapshot().RecruitCandidates.MinBy(c => c.Cost)!;
            if (world.CreateSnapshot().Credits < cheapest.Cost)
                break;
            world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: cheapest.Id));
        }

        var broke = world.CreateSnapshot();
        if (broke.RecruitCandidates.Count == 0)
            return false; // ran out of names before running out of money - the setup didn't do its job

        var unaffordable = broke.RecruitCandidates.MinBy(c => c.Cost)!;
        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: unaffordable.Id));
        var after = world.CreateSnapshot();
        return after.Credits == broke.Credits && after.RecruitCandidates.Any(c => c.Id == unaffordable.Id);
    }

    private static bool World_Recruiting_HireFailsWhileNotDocked()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var candidate = world.CreateSnapshot().RecruitCandidates[0];

        EngageSector(world, "sector-alpha"); // now Phase == Battle, undocked
        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: candidate.Id));

        return world.CreateSnapshot().Characters.All(c => !c.IsBot);
    }

    // Free re-roll (no quest, no reward) - just enough to refresh the board (World.Voyage.cs's
    // EnterStation calls RegenerateRecruitRoster) when a test needs a *different* set of names.
    // DestroyCurrentEnemyShip (ApplyDamage straight to the hull, the same shortcut World_Squadron's
    // own tests use) stands in for actually winning the fight, since what these tests need out of
    // the round trip is a fresh board, not the fight itself. Docking is still a deliberate manual
    // approach and button press (World.StationDocking.cs) - DockAtStation (used throughout this
    // file already) is what actually flies it in and calls EnterStation for real; without it the
    // ship just sits outside forever and the roster never rerolls at all.
    private static void DockAgainForFreshRoster(World world)
    {
        EngageSector(world, "sector-alpha");
        DestroyCurrentEnemyShip(world);
        DockAtStation(world, world.GalaxyMap.HomePointId);
    }

    // Everything below this point is about what a hired bot *does*, not about the economy - so it
    // funds itself lavishly through the save/load path (SaveGame.Credits, World.Save.cs) rather
    // than earning it the way a real game would (accepting and completing station jobs), which
    // would tangle these tests up in the faction-standing system for no reason: a rival's standing
    // can, in principle, sour enough from repeated combat to start refusing new work, and that's
    // a real mechanic worth its own test, not a hazard this file should have to route around.
    private static void FundGenerously(World world)
    {
        var save = new SaveGame(SaveGame.CurrentVersion, world.CurrentShipKind, Credits: 10_000,
            world.GalaxyMap.HomePointId, new Dictionary<FactionId, int>(), new Dictionary<ShipUpgradeTrack, int>(),
            Array.Empty<ItemType>(), ActiveQuest: null);
        world.ApplySave(save); // re-docks at the same station and rerolls the roster fresh (EnterStation)
    }

    private static BotCandidate? CheapestOnOffer(World world) => world.CreateSnapshot().RecruitCandidates.MinBy(c => c.Cost);

    // Only the 3 of 5 roles that happen to land on the board this visit are on offer at all - a
    // test that cares about a *specific* role rerolls (free) until it shows up, same as a player
    // would just keep checking back. Affordability is never in question once funded.
    private static BotCandidate RerollUntilRoleOffered(World world, CrewRole role)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (world.CreateSnapshot().RecruitCandidates.FirstOrDefault(c => c.Role == role) is { } found)
                return found;
            DockAgainForFreshRoster(world);
        }
        throw new InvalidOperationException($"роль {role} не появилась за 20 попыток реролла");
    }

    // Funded generously and hired past the cap, then confirms one more hire - fully paid for - is
    // still refused purely by the headcount limit, not by money.
    private static bool World_Recruiting_RespectsMaxHiredBotsCap()
    {
        var world = new World();
        world.SpawnCharacter(1);
        FundGenerously(world);

        for (var hired = 0; hired < World.MaxHiredBots; hired++)
        {
            if (world.CreateSnapshot().RecruitCandidates.Count == 0)
                DockAgainForFreshRoster(world); // the 3-candidate board ran dry before the 4-bot cap did
            world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: CheapestOnOffer(world)!.Id));
        }

        if (world.CreateSnapshot().Characters.Count(c => c.IsBot) != World.MaxHiredBots)
            return false; // didn't actually reach the cap - not what this checks

        if (world.CreateSnapshot().RecruitCandidates.Count == 0)
            DockAgainForFreshRoster(world);
        var creditsBefore = world.CreateSnapshot().Credits;
        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: CheapestOnOffer(world)!.Id));

        var after = world.CreateSnapshot();
        return after.Characters.Count(c => c.IsBot) == World.MaxHiredBots && after.Credits == creditsBefore;
    }

    private static bool World_Recruiting_SecurityBotAutoMansAndFiresAtEnemy()
    {
        var world = new World();
        world.SpawnCharacter(1);
        FundGenerously(world);
        var securityCandidate = RerollUntilRoleOffered(world, CrewRole.Security);
        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: securityCandidate.Id));

        var bot = world.CreateSnapshot().Characters.Single(c => c.IsBot);
        if (world.CreateSnapshot().TurretStates.All(t => t.MannedByPlayerId != bot.PlayerId))
            return false;

        EngageSector(world, "sector-alpha");
        var startHp = world.CreateSnapshot().Enemy.Hp;

        for (var i = 0; i < 400 && world.CreateSnapshot().Enemy.Hp >= startHp; i++)
            world.Step(RealtimeStep);

        return world.CreateSnapshot().Enemy.Hp < startHp;
    }

    private static bool World_Recruiting_MechanicBotRefuelsDrainedReactor()
    {
        var world = new World();
        world.SpawnCharacter(1);
        FundGenerously(world);
        var mechanicCandidate = RerollUntilRoleOffered(world, CrewRole.Mechanic);
        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: mechanicCandidate.Id));

        world.PowerGrid.Reactor.RemoveRod(0);
        if (world.PowerGrid.Reactor.IsRodLoaded(0))
            return false;

        for (var i = 0; i < 6 * 30 && !world.PowerGrid.Reactor.IsRodLoaded(0); i++)
            world.Step(RealtimeStep);

        return world.PowerGrid.Reactor.IsRodLoaded(0);
    }

    // Wounding a character for real means decompressing a room over hundreds of ticks (the
    // technique the existing bleeding-threshold tests use) - out of proportion for checking one
    // bot's heal loop. This instead checks the narrower, still-real contract: a Scientist bot
    // never pushes a healthy crew above MaxHealth or otherwise disturbs it.
    private static bool World_Recruiting_ScientistBotLeavesHealthyCrewAlone()
    {
        var world = new World();
        world.SpawnCharacter(1);
        FundGenerously(world);
        var scientistCandidate = RerollUntilRoleOffered(world, CrewRole.Scientist);
        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: scientistCandidate.Id));

        for (var i = 0; i < 3 * 30; i++)
            world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        return snapshot.Characters.All(c => c.Health == Character.MaxHealth);
    }

    private static bool World_Recruiting_EngineerBotFeedsUnpoweredSystems()
    {
        var world = new World();
        world.SpawnCharacter(1);
        FundGenerously(world);

        var engineerCandidate = RerollUntilRoleOffered(world, CrewRole.Engineer);

        // RerollUntilRoleOffered may have flown the ship through a sector-alpha fight and back
        // (DockAgainForFreshRoster) to get a fresh board, which can leave Engine holding the whole
        // reactor budget. Drain it back down here, before hiring, so there's real headroom left for
        // the bot's Oxygen nudge to be measurable once it exists, regardless of how many rerolls it
        // took this run.
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: (int)PowerSystemId.Engine, PowerDirection: -1f));
        for (var i = 0; i < 5 * 30; i++)
            world.Step(RealtimeStep);

        var before = world.CreateSnapshot().Power.Allocated[PowerSystemId.Oxygen];
        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: engineerCandidate.Id));

        for (var i = 0; i < 6 * 30; i++)
            world.Step(RealtimeStep);

        return world.CreateSnapshot().Power.Allocated[PowerSystemId.Oxygen] > before;
    }

    private static bool World_Recruiting_CaptainBotStabilizesAbandonedHelm()
    {
        var world = new World();
        world.SpawnCharacter(1);
        FundGenerously(world);
        var captainCandidate = RerollUntilRoleOffered(world, CrewRole.Captain);
        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: captainCandidate.Id));

        EnterAsteroidFieldAndManHelm(world);
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // stand up - hands off the helm entirely
        // A handful of ticks, not the full brake - at ShipMaxSpeed(5)/ShipAutoStabilizeDecelerationPerSecond(6)
        // the captain-bot's own brake (engaged the instant nobody's left at helm) fully zeroes the
        // ship's momentum in under a second, so checking too long after standing up would just as
        // often catch it already stopped - which would say nothing about whether it was ever moving.
        for (var i = 0; i < 5; i++)
            world.Step(RealtimeStep);

        var moving = world.CreateSnapshot().ShipField;
        if (moving.VelocityX == 0f && moving.VelocityY == 0f)
            return false; // needs to actually be drifting for the test to mean anything

        for (var i = 0; i < 10 * 30; i++)
            world.Step(RealtimeStep);

        var stopped = world.CreateSnapshot().ShipField;
        return Math.Abs(stopped.VelocityX) < 0.05f && Math.Abs(stopped.VelocityY) < 0.05f;
    }
}
