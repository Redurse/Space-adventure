using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // can hold squadrons (game_design.md section 12): FireBowTurretUntilEnemyDefeated gives up
    // after a fixed number of reload/repair cycles, which isn't always enough for three hulls in
    // a row, and stopping early would leave the caller mid-battle instead of victorious.
    private static void WinBattleAt(World world, string sectorId)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: sectorId));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        for (var round = 0; round < 8 && world.Phase == VoyagePhase.Battle; round++)
        {
            FireBowTurretUntilEnemyDefeated(world, 1);
            for (var i = 0; i < 30 && world.Phase == VoyagePhase.Battle && world.Enemy.Hp <= 0; i++)
                world.Step(RealtimeStep); // let StepVoyage resolve the kill and settle the standing
        }
    }

    // Keeps fighting a faction's ships until its standing actually drops past the hostile
    // threshold, putting in at the neutral home station between rounds.
    //
    // Both details are load-bearing. The retry is bounded rather than a fixed count because
    // FireBowTurretUntilEnemyDefeated gives up after a set number of reload/repair cycles, so one
    // battle can end with the enemy still alive - "exactly 3 kills" was flaky for that reason.
    // And the repair stop matters because damage accumulates across rounds (cut wire links, spent
    // fuel, breaches) until the ship simply can't fly itself to a dock any more, which made the
    // *final* docking flaky instead. Home is Independent, so calling there shifts nobody's
    // standing and doesn't disturb what's being measured.
    private static void GrindStandingHostile(World world, string sectorId, FactionId faction, int threshold = FactionDefinitions.HostileThreshold)
    {
        // Budget rather than a fixed count: a sector's squadron doesn't always cost the same
        // standing (how many hulls the fight actually gets through varies with how the fight goes),
        // so "fly until they hate us" has to be allowed to take a few more trips than the minimum.
        // A higher bar (e.g. FactionDefinitions.WarThreshold) just takes more of the same trips.
        for (var attempt = 0; attempt < 20 && world.GetStanding(faction) > threshold; attempt++)
        {
            WinBattleAt(world, sectorId);
            // Always put in for repairs, including after the final battle - the caller still has
            // to fly somewhere afterwards, and doing that on a freshly shot-up ship is exactly
            // what made the last docking flaky.
            world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "home-station"));
            DockAtStation(world);
        }
    }

    private static bool World_Faction_DestroyingShip_LowersOwnerRaisesRival()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WinBattleAt(world, "sector-alpha"); // FreeFleet space

        return world.GetStanding(FactionId.FreeFleet) == FactionDefinitions.StandingPerShipDestroyed
            && world.GetStanding(FactionId.Consortium) == FactionDefinitions.RivalStandingPerShipDestroyed;
    }

    private static bool World_Faction_QuestTurnIn_RaisesStanding()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Starts docked at the neutral home station - take its delivery job and run it to
        // whichever station it names. Reads that station's actual owner rather than assuming
        // Consortium: with the Miners' Guild's own base on the map too (GalaxyMap.cs), the random
        // pick is no longer guaranteed to land on one specific faction.
        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
        var quest = world.CreateSnapshot().ActiveQuest;
        if (quest is null)
            return false;

        var destinationFaction = world.GalaxyMap.GetPoint(quest.DestinationPointId).Faction;
        var before = world.GetStanding(destinationFaction);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: quest.DestinationPointId));
        DockAtStation(world);
        world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));

        return world.CreateSnapshot().ActiveQuest is null
            && world.GetStanding(destinationFaction) == before + FactionDefinitions.StandingPerQuestTurnIn;
    }

    // Working for one side costs a little goodwill with the other, the same ripple ship-kills
    // already have, just the opposite sign and much smaller (World.Factions.cs's FactionDefinitions).
    // Retried like GrindStandingHostile above: the delivery job's destination is picked at random
    // from every other station, and the Miners' Guild's own base (no Rival) is one of them - this
    // needs a run that happened to land on Consortium or FreeFleet to have anything to measure.
    private static bool World_Faction_QuestTurnIn_LowersRivalStanding()
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var world = new World();
            world.SpawnCharacter(1);

            world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
            var quest = world.CreateSnapshot().ActiveQuest;
            if (quest is null)
                return false;

            var destinationFaction = world.GalaxyMap.GetPoint(quest.DestinationPointId).Faction;
            if (FactionDefinitions.Rival(destinationFaction) is not { } rival)
                continue;

            var before = world.GetStanding(rival);
            world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: quest.DestinationPointId));
            DockAtStation(world);
            world.ApplyCommand(1, new ClientCommand(1, TurnInCargoQuestPressed: true));

            return world.CreateSnapshot().ActiveQuest is null
                && world.GetStanding(rival) == before + FactionDefinitions.RivalStandingPerQuestTurnIn;
        }

        return false; // never landed on a rival-having destination in 10 tries - setup problem
    }

    // Three kills in Consortium space put standing past the hostile threshold (-18 each vs. a -40
    // threshold), at which point their stations stop offering work at all.
    private static bool World_Faction_HostileStanding_BlocksQuestOffers()
    {
        var world = new World();
        world.SpawnCharacter(1);
        // Three fights pile up enough hull breaches to suffocate an unsuited character long before
        // the third one ends - and a dead character can't walk to the helm to dock afterwards.
        // Suiting up first makes the whole run deterministic (World.Atmosphere.cs skips suited
        // characters entirely).
        EquipSuit(world, 1);
        GrindStandingHostile(world, "sector-delta", FactionId.Consortium);

        if (world.GetStanding(FactionId.Consortium) > FactionDefinitions.HostileThreshold)
            return false; // didn't actually anger them enough - setup problem, not the behavior under test

        // trade-station, not outpost-gamma: it's Consortium-held *and* actually has an
        // Administrator to refuse you (outpost-gamma is a Shipyard - no quests there at all, so
        // it would pass for the wrong reason).
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "trade-station"));
        DockAtStation(world);
        if (world.Phase != VoyagePhase.Station)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true, AcceptQuestKind: QuestKind.Delivery));
        return world.CreateSnapshot().ActiveQuest is null;
    }

    private static bool World_Faction_HostileStanding_RaisesPrices()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EquipSuit(world, 1); // see World_Faction_HostileStanding_BlocksQuestOffers
        GrindStandingHostile(world, "sector-delta", FactionId.Consortium);
        if (world.GetStanding(FactionId.Consortium) > FactionDefinitions.HostileThreshold)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        DockAtStation(world);

        // Buy with a freshly spawned character: three battles leave player 1 with a full
        // inventory row (tools/crates picked up along the way), and a failed TryAdd is a silent
        // no-charge - which would look exactly like "the markup wasn't applied".
        world.SpawnCharacter(2);
        var good = TradeCatalog.Goods[0];
        var creditsBefore = world.Credits;
        world.ApplyCommand(2, new ClientCommand(2, BuyItemType: good.Item));

        // Paid more than the list price because they dislike the crew (World.Factions.cs's
        // PriceMultiplier) - the exact figure is FactionDefinitions' business, not this test's.
        var paid = creditsBefore - world.Credits;
        return paid > good.BuyPrice;
    }

    private static bool World_Faction_IndependentsNeverShift()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Independents own no hostile sectors, and finishing the home station's own job is
        // credited to whoever holds the destination - so nothing here can move their number.
        WinBattleAt(world, "sector-alpha");
        return world.GetStanding(FactionId.Independent) == 0;
    }

    // Unlike Independent, the Guild's own standing is meant to move (its station has real prices
    // to protect) - what it shares with Independent is only staying out of the Consortium/FreeFleet
    // war, i.e. no Rival to ripple to or from.
    private static bool World_Faction_MinersGuildNeverHasARival() =>
        FactionDefinitions.Rival(FactionId.MinersGuild) is null;

    // The Guild's own base is the one place ore is worth more than the standard list price
    // (World.Trade.cs's MiningStationOreSellBonus) - everywhere else, a freshly-spawned crew at
    // neutral standing sells at exactly list price, so any excess here has to be the bonus.
    private static bool World_Trade_SellMineralAtMiningStation_PaysBonusOverListPrice()
    {
        var world = new World();
        world.SpawnCharacter(1);
        MineOre(world, 1);

        var slotIndex = Array.IndexOf(world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), ItemType.Mineral);
        var creditsBefore = world.Credits;

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "mining-outpost"));
        DockAtStation(world);
        world.ApplyCommand(1, new ClientCommand(1, SellSlotIndex: slotIndex));

        var gained = world.Credits - creditsBefore;
        return gained > TradeCatalog.Find(ItemType.Mineral)!.SellPrice;
    }

    // Enough Consortium losses hand their own frontier station to FreeFleet outright
    // (World.Factions.cs's NudgeWarEffort/ContestedPointId) - not just a reputation number this
    // time, but who the map itself says owns outpost-gamma.
    private static bool World_Faction_War_ContestedStationFlipsAfterEnoughShipsDestroyed()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EquipSuit(world, 1); // see World_Faction_HostileStanding_BlocksQuestOffers

        // Kills, not standing, are what the war front counts - track them directly rather than
        // back-computing from GetStanding, so a future change to the standing-per-kill constant
        // can't silently break this test's arithmetic.
        // 5 kills: comfortably past what grinding to HostileThreshold or WarThreshold alone costs
        // (World.Factions.cs's WarEffortToFlipSector is set above both on purpose).
        const int killsNeeded = 5;
        var kills = 0;
        for (var attempt = 0; attempt < 20 && kills < killsNeeded; attempt++)
        {
            var before = world.GetStanding(FactionId.Consortium);
            WinBattleAt(world, "sector-delta");
            kills += (before - world.GetStanding(FactionId.Consortium)) / -FactionDefinitions.StandingPerShipDestroyed;
            world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "home-station"));
            DockAtStation(world);
        }

        if (kills < killsNeeded)
            return false; // never landed enough kills - setup problem, not the behavior under test

        return world.CreateSnapshot().GalaxyPoints.First(p => p.Id == "outpost-gamma").Faction == FactionId.FreeFleet;
    }

    // The same standing that raises prices also thickens the welcome party (World.Factions.cs's
    // SquadronSizeAdjustment) - a faction that already hates the crew throws one more hull at the
    // very sector that made it hate them.
    private static bool World_Faction_HostileStanding_SendsABiggerSquadron()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EquipSuit(world, 1); // see World_Faction_HostileStanding_BlocksQuestOffers

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-delta"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);
        var baselineSize = world.CreateSnapshot().EnemyShip.Ships.Count;

        GrindStandingHostile(world, "sector-delta", FactionId.Consortium);
        if (world.GetStanding(FactionId.Consortium) > FactionDefinitions.HostileThreshold)
            return false; // didn't actually anger them enough - setup problem, not the behavior under test

        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-delta"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        return world.CreateSnapshot().EnemyShip.Ships.Count == baselineSize + 1;
    }

    // Deep enough hostility closes the territory entirely (World.StationDocking.cs's CanDockNow) -
    // further than just losing the job board (World_Faction_HostileStanding_BlocksQuestOffers).
    // The ship can still fly right up to the berth; the button just never arms.
    private static bool World_Docking_AtWar_NeverArmsTheButton()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EquipSuit(world, 1); // see World_Faction_HostileStanding_BlocksQuestOffers
        GrindStandingHostile(world, "sector-delta", FactionId.Consortium, FactionDefinitions.WarThreshold);
        if (world.GetStanding(FactionId.Consortium) > FactionDefinitions.WarThreshold)
            return false; // didn't actually reach war - setup problem, not the behavior under test

        // trade-station, not outpost-gamma: grinding this deep can overshoot past
        // WarEffortToFlipSector too (a single hostility-enlarged squadron kill can swing standing
        // by more than one threshold at once) and hand outpost-gamma itself to FreeFleet - which
        // would make it dockable again for an unrelated reason. trade-station is never a war front,
        // so it stays Consortium's regardless of how far this grind overshoots.
        ApproachBerth(world, "trade-station");
        if (world.CanDockNow)
            return false; // armed despite being at war with whoever owns this berth

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        return world.Phase == VoyagePhase.StationApproach; // pressed anyway - still refused
    }

    // Trading a Frigate down to a Scout costs less than the trade-in is worth, so the yard pays
    // out - which is exactly the negative-cost case World.ShipPurchase.cs allows on purpose.
    private static bool World_Shipyard_BuyCheaperHull_SwapsShipAndRefunds()
    {
        var world = new World(); // starts docked at home-station as a Frigate
        world.SpawnCharacter(1);
        // Only a Shipyard-kind station sells hulls (game_design.md section 10) - the home outpost
        // has no Shipwright at all, so the trip is part of the mechanic, not test overhead.
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        DockAtStation(world);

        var creditsBefore = world.Credits;
        var expectedCost = world.GetShipSwapCost(ShipKind.Scout);
        world.ApplyCommand(1, new ClientCommand(1, PurchaseShipKind: ShipKind.Scout));

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return world.CurrentShipKind == ShipKind.Scout
            && world.Credits == creditsBefore - expectedCost
            && expectedCost < 0 // trading down really does pay out
            && world.Ship.Rooms.Count == Ship.Create(ShipKind.Scout).Rooms.Count
            && me.X == world.Ship.SpawnPoint.X; // moved onto the new hull's spawn point
    }

    private static bool World_Shipyard_Buy_FailsWithoutEnoughCredits()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma")); // the Shipyard station
        DockAtStation(world);

        // A Cruiser costs far more than the starting wallet even after trading in the Frigate.
        var creditsBefore = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, PurchaseShipKind: ShipKind.Cruiser));

        return world.CurrentShipKind == ShipKind.Frigate && world.Credits == creditsBefore;
    }

    private static bool World_Shipyard_Buy_FailsWhileNotDocked()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, PurchaseShipKind: ShipKind.Scout));
        return world.CurrentShipKind == ShipKind.Frigate;
    }

    // A new hull comes out of the yard intact, and the crew wallet/inventory carry across - only
    // the ship itself is replaced (World.ShipPurchase.cs).
    private static bool World_Shipyard_SwapKeepsCreditsAndClearsBreaches()
    {
        var world = new World();
        world.SpawnCharacter(1);

        // Take some damage first so there's something to be repaired by the swap, then head to
        // the one station that actually has a Shipwright.
        EquipSuit(world, 1);
        WinBattleAt(world, "sector-alpha");
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "outpost-gamma"));
        DockAtStation(world);

        var creditsBefore = world.Credits;
        var expectedCost = world.GetShipSwapCost(ShipKind.Scout);
        world.ApplyCommand(1, new ClientCommand(1, PurchaseShipKind: ShipKind.Scout));

        var snapshot = world.CreateSnapshot();
        return world.CurrentShipKind == ShipKind.Scout
            && world.Credits == creditsBefore - expectedCost
            && snapshot.WallBlockStates.All(s => !s.Breached)
            && snapshot.RoomOxygen.All(o => o.Oxygen >= 100f);
    }

}
