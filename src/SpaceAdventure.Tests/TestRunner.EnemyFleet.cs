using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // (Enemy.ApplyDamage) rather than through the turrets: FireBowTurretUntilEnemyDefeated loops
    // until the enemy's HP hits zero, which against a squadron seamlessly rolls on into the *next*
    // ship - useful for "just win the fight", useless for observing what happens between kills.
    private static void EngageSector(World world, string sectorId)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: sectorId));
        for (var i = 0; i < 10 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);
    }

    private static void DestroyCurrentEnemyShip(World world)
    {
        world.Enemy.ApplyDamage(world.Enemy.Hp);
        world.Step(RealtimeStep); // StepVoyage resolves the kill on the next tick
    }

    // A defended sector sends its ships in one after another (game_design.md section 12) - killing
    // one doesn't end the fight until the last is gone.
    private static bool World_Squadron_NextShipEngagesAfterEachKill()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EngageSector(world, "sector-beta"); // squadron of 2
        if (world.CreateSnapshot().Enemy.RemainingShips != 2)
            return false;

        DestroyCurrentEnemyShip(world);

        // The fight continues, against a fresh full-health hull with its own intact crew to board.
        var afterFirst = world.CreateSnapshot();
        if (world.Phase != VoyagePhase.Battle || afterFirst.Enemy.RemainingShips != 1)
            return false;
        if (afterFirst.Enemy.Hp < afterFirst.Enemy.MaxHp || afterFirst.EnemyCrew.Any(c => !c.Alive))
            return false;

        DestroyCurrentEnemyShip(world);

        return world.Phase == VoyagePhase.Traveling && world.CreateSnapshot().Enemy.RemainingShips == 0;
    }

    private static bool World_Squadron_EveryKillCostsOwnerStanding()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EngageSector(world, "sector-beta"); // FreeFleet, squadron of 2

        DestroyCurrentEnemyShip(world);
        if (world.GetStanding(FactionId.FreeFleet) != FactionDefinitions.StandingPerShipDestroyed)
            return false; // the first kill alone should already have registered

        DestroyCurrentEnemyShip(world);

        // Both kills counted, not just the one that ended the engagement.
        return world.GetStanding(FactionId.FreeFleet) == FactionDefinitions.StandingPerShipDestroyed * 2;
    }

    private static bool World_Squadron_BountyCompletesOnlyWhenSectorCleared()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Bounty));
        var quest = world.CreateSnapshot().ActiveQuest;
        if (quest is not { Kind: QuestKind.Bounty })
            return false;

        EngageSector(world, quest.DestinationPointId);
        var squadronSize = world.CreateSnapshot().Enemy.RemainingShips;

        for (var ship = 0; ship < squadronSize; ship++)
        {
            DestroyCurrentEnemyShip(world);

            var complete = world.CreateSnapshot().ActiveQuest?.ObjectiveComplete ?? false;
            var lastShip = ship == squadronSize - 1;
            if (complete != lastShip)
                return false; // a squadron isn't beaten until its last ship is
        }

        return world.Phase == VoyagePhase.Traveling;
    }

}
