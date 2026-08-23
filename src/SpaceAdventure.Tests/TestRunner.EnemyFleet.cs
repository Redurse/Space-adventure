using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // (Enemy.ApplyDamage) rather than through the turrets: FireBowTurretUntilEnemyDefeated loops
    // until the enemy's HP hits zero, which against a squadron seamlessly rolls on into the *next*
    // ship - useful for "just win the fight", useless for observing what happens between kills.
    private static void EngageSector(World world, string sectorId) => EnterBattle(world, sectorId: sectorId);

    private static void DestroyCurrentEnemyShip(World world)
    {
        world.Enemy.ApplyDamage(world.Enemy.Hp);
        world.Step(RealtimeStep); // StepVoyage resolves the kill on the next tick
    }

    // "You can always outrun them" (EnemyMaxSpeed's own doc comment) is a real, working escape now:
    // flying clear of the SECTOR (World.Voyage.cs's HasFledTheSector, measured from its own marker,
    // not from the actively-pursuing squadron - see that method's own reasoning) drops the fight
    // back to open space with no win recorded and the squadron still very much alive.
    private static bool World_Battle_FlyingClearOfTheSectorFleesTheFightWithoutAWin()
    {
        var world = new World();
        world.SpawnCharacter(1);
        // sector-delta sits well clear of every field edge even after M40's recentring - StartBattle
        // always parks the ship at rotation 0 for a fresh fight, so astern thrust always retreats
        // in -X regardless of which sector this is, and this one has the room for it.
        EngageSector(world, "sector-delta");
        if (!world.IsInBattle)
            return false;

        SitAtHelm(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f));
        for (var i = 0; i < 60; i++)
            world.Step(RealtimeStep);

        // BattleFleeDistance is 560 now (M48, ×2 alongside the field's own doubling) - reverse-
        // thrust tops out at the same ShipMaxSpeed(5) forward flight does, just slower to ramp up
        // (ShipReverseThrustFraction), so clearing it takes noticeably longer than the old
        // 280-unit version did; 200s is comfortably more than the ~112s minimum at full reverse speed.
        world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: -1f)); // straight astern, away from the marker
        for (var i = 0; i < 200 * 30 && world.IsInBattle; i++)
            world.Step(RealtimeStep);

        return !world.IsInBattle && world.CreateSnapshot().Enemy.RemainingShips > 0;
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
        if (!world.IsInBattle || afterFirst.Enemy.RemainingShips != 1)
            return false;
        if (afterFirst.Enemy.Hp < afterFirst.Enemy.MaxHp || afterFirst.EnemyShip.Crew.Any(c => !c.Alive))
            return false;

        DestroyCurrentEnemyShip(world);

        return !world.IsInBattle && world.CreateSnapshot().Enemy.RemainingShips == 0;
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

        return !world.IsInBattle;
    }

}
