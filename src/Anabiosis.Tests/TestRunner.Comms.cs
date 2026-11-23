using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// The comms link with a station (World.Comms.cs): hail it from range, then trade, hire and take jobs without docking.
internal static partial class TestRunner
{
    // Undocked, a little way off the home station - inside the hailing range, outside the docking one.
    private static (World World, string HomeId) CommsWorld(float offsetUnits = 300f)
    {
        var world = new World();
        world.SpawnCharacter(1);
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }
        var home = world.GalaxyMap.GetPoint(world.GalaxyMap.HomePointId);
        world.DebugPlaceShip(home.Position + new Vec2(offsetUnits, 0));
        world.Step(RealtimeStep);
        return (world, home.Id);
    }

    private static CharacterState Me(World world) => world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

    // In range, the hail goes through, the line shows in the snapshot, and the trader sells over it.
    private static bool World_Comms_HailInRange_OpensALineAndTheTraderSells()
    {
        var (world, homeId) = CommsWorld();
        if (world.IsDocked || world.IsInBattle)
            return false; // setup
        world.ApplyCommand(1, new ClientCommand(1, HailPointId: homeId));
        world.Step(RealtimeStep);
        var comms = world.CreateSnapshot().Comms;
        if (comms is not { LinkedPointId: { } linked } || linked != homeId || !comms.HasTrader)
            return false;

        var creditsBefore = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Bandage));
        world.Step(RealtimeStep);
        return world.Credits < creditsBefore && Me(world).Inventory!.MainSlots.Contains(ItemType.Bandage);
    }

    // Without a line nothing is sold: the same purchase does nothing from outside the station.
    private static bool World_Comms_WithoutALine_NothingIsSold()
    {
        var (world, _) = CommsWorld();
        var creditsBefore = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.Bandage));
        world.Step(RealtimeStep);
        return world.Credits == creditsBefore && !Me(world).Inventory!.MainSlots.Contains(ItemType.Bandage);
    }

    // Far out of range the hail is refused, with a word of why.
    private static bool World_Comms_HailOutOfRange_IsRefused()
    {
        var (world, homeId) = CommsWorld(offsetUnits: World.CommsRangeUnits + 600f);
        if (world.IsInBattle)
            return false; // setup: an engagement out there would refuse for another reason
        world.ApplyCommand(1, new ClientCommand(1, HailPointId: homeId));
        world.Step(RealtimeStep);
        var comms = world.CreateSnapshot().Comms;
        return comms is { LinkedPointId: null, Notice: not null };
    }

    // The line drops when the ship flies out of range, and with it the services.
    private static bool World_Comms_LineDropsWhenTheShipDriftsOutOfRange()
    {
        var (world, homeId) = CommsWorld();
        world.ApplyCommand(1, new ClientCommand(1, HailPointId: homeId));
        world.Step(RealtimeStep);
        if (world.CreateSnapshot().Comms?.LinkedPointId != homeId)
            return false;

        var home = world.GalaxyMap.GetPoint(homeId);
        world.DebugPlaceShip(home.Position + new Vec2(World.CommsRangeUnits * 2f, 0));
        world.Step(RealtimeStep);
        return world.CreateSnapshot().Comms?.LinkedPointId is null && world.CommsPointId is null;
    }

    // Hanging up ends the line.
    private static bool World_Comms_HangingUp_EndsTheLine()
    {
        var (world, homeId) = CommsWorld();
        world.ApplyCommand(1, new ClientCommand(1, HailPointId: homeId));
        world.Step(RealtimeStep);
        world.ApplyCommand(1, new ClientCommand(1, EndCommsPressed: true));
        world.Step(RealtimeStep);
        return world.CreateSnapshot().Comms?.LinkedPointId is null;
    }

    // Crew can be hired over the line.
    private static bool World_Comms_HiringWorksOverTheLine()
    {
        var (world, homeId) = CommsWorld();
        world.DebugAddCredits(2000);
        world.ApplyCommand(1, new ClientCommand(1, HailPointId: homeId));
        world.Step(RealtimeStep);
        var snapshot = world.CreateSnapshot();
        if (snapshot.Comms is not { HasRecruiter: true } || snapshot.RecruitCandidates.Count == 0)
            return false; // setup: the home station should have a recruiter and a board
        var botsBefore = snapshot.Characters.Count(c => c.IsBot);
        var creditsBefore = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, HireCandidateId: snapshot.RecruitCandidates[0].Id));
        world.Step(RealtimeStep);
        var after = world.CreateSnapshot();
        return after.Characters.Count(c => c.IsBot) == botsBefore + 1 && world.Credits < creditsBefore;
    }

    // The job board answers over the line, too: a job taken there is the active one.
    private static bool World_Comms_AcceptingAJobWorksOverTheLine()
    {
        var (world, homeId) = CommsWorld();
        world.ApplyCommand(1, new ClientCommand(1, HailPointId: homeId));
        world.Step(RealtimeStep);
        if (world.CreateSnapshot().Comms is not { HasAdministrator: true })
            return false; // setup
        world.ApplyCommand(1, new ClientCommand(1, AcceptCargoQuestPressed: true));
        world.Step(RealtimeStep);
        return world.CreateSnapshot().ActiveQuest is not null;
    }
}
