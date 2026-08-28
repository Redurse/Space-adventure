using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    private static bool World_Crime_StealCrate_AddsItemAndMarksLooted()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WalkOntoStation(world);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false;

        var crate = world.Station.Crates.First();
        WalkOnStationTo(world, crate.X, crate.Y);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return world.IsCrateLooted(crate.Id)
            && world.GetStolenItemCount(1) == 1
            && me.Inventory!.MainSlots.Contains(crate.Item);
    }

    // Caught red-handed: fine, confiscation and a reputation hit, all three (game_design.md §10).
    private static bool World_Crime_CaughtByGuard_FinesConfiscatesAndLowersStanding()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WalkOntoStation(world);

        var crate = world.Station.Crates.First();
        WalkOnStationTo(world, crate.X, crate.Y);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        if (world.GetStolenItemCount(1) != 1)
            return false;

        var creditsBefore = world.Credits;
        var standingBefore = world.GetStanding(world.CreateSnapshot().GalaxyPoints
            .First(p => p.Id == world.CreateSnapshot().Voyage.DockedPointId).Faction);

        // Walk right up to the guard and wait out the patrol check.
        var guard = world.Station.Npcs.First(n => n.Kind == NpcKind.Security);
        WalkOnStationTo(world, guard.X, guard.Y);
        for (var i = 0; i < 5 * 30 && world.GetStolenItemCount(1) > 0; i++)
            world.Step(RealtimeStep);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var dockedFaction = world.CreateSnapshot().GalaxyPoints
            .First(p => p.Id == world.CreateSnapshot().Voyage.DockedPointId).Faction;

        return world.GetStolenItemCount(1) == 0 // confiscated
            && world.Credits < creditsBefore // fined
            && !me.Inventory!.MainSlots.Contains(crate.Item) // goods gone
            && world.GetStanding(dockedFaction) < standingBefore; // and they remember it
    }

    private static bool World_Crime_UnseenTheft_GoesUnpunished()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WalkOntoStation(world);

        var crate = world.Station.Crates.First(); // first service room - several rooms from the guard
        WalkOnStationTo(world, crate.X, crate.Y);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        var creditsBefore = world.Credits;
        for (var i = 0; i < 10 * 30; i++) // loiter well past several patrol checks, out of sight
            world.Step(RealtimeStep);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return world.GetStolenItemCount(1) == 1 && world.Credits == creditsBefore && me.Inventory!.MainSlots.Contains(crate.Item);
    }

    // Shared setup for the two "resist arrest" tests: get onto the station armed, walk up to the
    // guard, and open fire.
    private static StationNpc ArmAndConfrontGuard(World world)
    {
        var slot = TakeFromRack(world, ItemType.Rifle);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));

        WalkOntoStation(world);
        var guard = world.Station.Npcs.First(n => n.Kind == NpcKind.Security);
        WalkOnStationTo(world, guard.X, guard.Y);
        return guard;
    }

    private static bool World_Crime_ShootingGuard_AlertsStationAndGuardFightsBack()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var guard = ArmAndConfrontGuard(world);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false;

        if (world.IsStationAlerted)
            return false; // shouldn't be alerted before a shot is fired

        // Aimed and given time to arrive: a shot is a body crossing the room now
        // (World.PersonalShots.cs), not damage applied the instant the button goes down.
        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var aim = new Vec2(guard.X - me.X, guard.Y - me.Y).Normalized();
        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true, LookX: (float)aim.X, LookY: (float)aim.Y));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);

        var guardAfterShot = world.CreateSnapshot().Station.Guards.First(g => g.NpcId == guard.Id);
        if (!world.IsStationAlerted || guardAfterShot.Health >= guardAfterShot.MaxHealth)
            return false;

        var healthBefore = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0));
        for (var i = 0; i < 5 * 30; i++) // stand there and take it
            world.Step(RealtimeStep);

        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health < healthBefore;
    }

    private static bool World_Crime_KillingGuard_CostsHeavyStanding()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var guard = ArmAndConfrontGuard(world);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false;

        var dockedFaction = world.CreateSnapshot().GalaxyPoints
            .First(p => p.Id == world.CreateSnapshot().Voyage.DockedPointId).Faction;
        var standingBefore = world.GetStanding(dockedFaction);

        for (var i = 0; i < 60 * 30 && world.CreateSnapshot().Station.Guards.First(g => g.NpcId == guard.Id).Alive; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
            world.Step(RealtimeStep);
            if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health <= 0)
                return false; // lost the shootout - not what this test measures
        }

        return !world.CreateSnapshot().Station.Guards.First(g => g.NpcId == guard.Id).Alive
            && world.GetStanding(dockedFaction) < standingBefore;
    }

    private static bool World_Crime_RedockingRestocksCrates()
    {
        var world = new World();
        world.SpawnCharacter(1);
        WalkOntoStation(world);

        var crate = world.Station.Crates.First();
        WalkOnStationTo(world, crate.X, crate.Y);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        if (!world.IsCrateLooted(crate.Id))
            return false;

        // Back aboard, fly somewhere and return - the station shouldn't stay stripped forever.
        WalkOnStationTo(world, 0.5f, 3f);
        for (var i = 0; i < 5 * 30 && world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: -1, MoveY: 0));
            world.Step(RealtimeStep);
        }
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0));
        // Shut the outer door behind us - it was opened to reach the station, and leaving it open
        // vents the ship to vacuum (World.Atmosphere.cs), which kills the unsuited character on
        // the long walk forward to the helm that DockAtStation needs.
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));

        DockAtStation(world, "trade-station");

        return world.IsDocked && !world.IsCrateLooted(crate.Id) && world.GetStolenItemCount(1) == 0;
    }

    // Flies to a sector and puts the ship there into a battle. Damage is then applied directly
}
