using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Jobs from the station's Administrator NPC (game_design.md section 7). Started as delivery-only
// (M11); bounty and mining were added once the mechanics they depend on existed - a hostile sector
// with a ship to kill, and ore that can be cut and carried. Both accept and turn-in require being
// docked, same gate as the Trader (World.Trade.cs), and a faction you've angered won't offer work
// at all (World.Factions.cs).
public sealed partial class World
{
    private const int DeliveryQuestReward = 100;
    private const int BountyQuestReward = 180; // riskier than a delivery run, pays accordingly
    private const int MiningQuestReward = 140;
    private const int MiningQuestOreRequired = 2;

    public Quest? ActiveQuest { get; private set; }

    // Takes a job from the board. The Administrator offers one of each kind it can currently
    // support, so `preferred` picks which - an unset (or unavailable) kind falls back to a random
    // pick, which is what the "just give me work" path does. No-ops if one is already active,
    // we're not docked, the station has no Administrator, or its faction has been angered.
    private void TryAcceptQuest(QuestKind? preferred = null)
    {
        if (Phase != VoyagePhase.Station || ActiveQuest is not null || _dockedPointId is not { } dockedId)
            return;

        if (Station.Npcs.All(n => n.Kind != NpcKind.Administrator))
            return; // not every station kind staffs one (game_design.md section 10)

        if (IsHostileHere)
            return;

        var dockedName = GalaxyMap.GetPoint(dockedId).Name;

        // Pick from whatever kinds this station can actually offer right now - a map with no other
        // station can't issue a delivery, one with no hostile sector can't issue a bounty.
        var candidates = new List<Quest>();

        var otherStations = GalaxyMap.Points
            .Where(p => p.Kind == GalaxyPointKind.Station && p.Id != dockedId)
            .ToList();
        if (otherStations.Count > 0)
        {
            var destination = otherStations[_random.Next(otherStations.Count)];
            candidates.Add(new Quest(QuestKind.Delivery, destination.Id, destination.Name, DeliveryQuestReward, dockedId));
        }

        var sectors = GalaxyMap.Points.Where(p => p.Kind == GalaxyPointKind.HostileSector).ToList();
        if (sectors.Count > 0)
        {
            var target = sectors[_random.Next(sectors.Count)];
            candidates.Add(new Quest(QuestKind.Bounty, target.Id, target.Name, BountyQuestReward, dockedId));
        }

        if (AsteroidField.OreDeposits.Count > 0)
            candidates.Add(new Quest(QuestKind.Mining, dockedId, dockedName, MiningQuestReward, dockedId, MiningQuestOreRequired));

        if (candidates.Count == 0)
            return;

        ActiveQuest = candidates.FirstOrDefault(q => q.Kind == preferred)
            ?? candidates[_random.Next(candidates.Count)];
    }

    // Marks a bounty done the moment its target sector's ship is destroyed - the reward is still
    // collected in person, back at the station that issued it.
    private void NoteBountyTargetDestroyed(string sectorPointId)
    {
        if (ActiveQuest is { Kind: QuestKind.Bounty } quest && quest.DestinationPointId == sectorPointId)
            ActiveQuest = quest with { ObjectiveComplete = true };
    }

    private void TryTurnInQuest(Character character)
    {
        if (Phase != VoyagePhase.Station || ActiveQuest is not { } quest)
            return;

        switch (quest.Kind)
        {
            case QuestKind.Delivery:
                if (quest.DestinationPointId != _dockedPointId)
                    return;
                break;

            case QuestKind.Bounty:
                // Collected wherever it was issued, and only once the kill actually happened.
                if (!quest.ObjectiveComplete || quest.IssuedByPointId != _dockedPointId)
                    return;
                break;

            case QuestKind.Mining:
                if (quest.IssuedByPointId != _dockedPointId)
                    return;
                // The ore is real inventory, not an abstract flag - hand over the actual items.
                var oreSlots = Enumerable.Range(0, Inventory.MainSlotCount)
                    .Where(i => character.Inventory.ItemAt(i) == ItemType.Mineral)
                    .Take(quest.RequiredAmount)
                    .ToList();
                if (oreSlots.Count < quest.RequiredAmount)
                    return;
                foreach (var slot in oreSlots)
                    character.Inventory.TryRemoveAt(slot);
                break;
        }

        Credits += quest.RewardCredits;
        AdjustStanding(DockedFaction, FactionDefinitions.StandingPerQuestTurnIn);
        ActiveQuest = null;
    }
}
