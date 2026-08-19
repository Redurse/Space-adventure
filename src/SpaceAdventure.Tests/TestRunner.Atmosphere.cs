using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // individual outer-hull wall blocks (spread unevenly across all rooms) — 600 simulated
    // seconds gives enough draws that every room ends up with at least one breach with very low
    // residual flake risk.
    private static void BreachEveryRoom(World world)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 600 * 30; i++)
            world.Step(RealtimeStep);
    }

    // Stops the moment the given room gets its first breach, rather than running the full
    // BreachEveryRoom sweep (which deliberately keeps going long enough to hit every room at least
    // once) - a character standing in the target room the whole time would otherwise rack up
    // several minutes of unsuited decompression exposure and could die of it before a test ever
    // gets to use the breach for anything.
    private static void BreachRoom(World world, string roomId)
    {
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 600 * 30 && !RoomHasBreach(world.CreateSnapshot(), roomId); i++)
            world.Step(RealtimeStep);
    }

    private static bool RoomHasBreach(WorldSnapshot snapshot, string roomId) =>
        snapshot.WallBlockStates.Any(s => s.Breached && snapshot.WallBlocks.First(b => b.Id == s.Id).RoomId == roomId);

    private static int CountBreaches(WorldSnapshot snapshot, string roomId) =>
        snapshot.WallBlockStates.Count(s => s.Breached && snapshot.WallBlocks.First(b => b.Id == s.Id).RoomId == roomId);

    private static bool World_EnemyAi_EventuallyBreachesEveryRoom()
    {
        var world = new World();
        world.SpawnCharacter(1); // position doesn't matter for this test

        BreachEveryRoom(world);

        var snapshot = world.CreateSnapshot();
        return world.Ship.Rooms.All(r => RoomHasBreach(snapshot, r.Id));
    }

    private static bool World_Decompression_DrainsHealthInBreachedRoom()
    {
        var world = new World();
        world.SpawnCharacter(1); // pilot — only sends commands, its health is never checked

        // Enemy AI only attacks once in Battle — get there first via the galaxy map.
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        // A single breach only leaks oxygen slowly — wait for an actual breach, then keep
        // stepping until oxygen has actually dropped clearly (not just barely, which could
        // flicker back above 50 for a tick from diffusion) under the safe threshold. This search
        // can take a long time in the worst case, so nobody should be sitting in the room while
        // it runs (see below).
        for (var i = 0; i < 600 * 30 && !RoomHasBreach(world.CreateSnapshot(), "corridor"); i++)
            world.Step(RealtimeStep);

        for (var i = 0; i < 300 * 30; i++)
        {
            world.Step(RealtimeStep);
            if (world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "corridor").Oxygen < 40f)
                break;
        }

        // Spawn a fresh, full-health character straight into the now-dangerous corridor (that's
        // the ship's spawn point) right before measuring. A character present for the whole
        // search above would keep taking damage the entire time oxygen sits under the 50
        // threshold — which the 300s search budget is easily long enough to do, bottoming out at
        // 0 well before the measurement window and making "after < before" fail on bad luck.
        world.SpawnCharacter(2);
        var before = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 2).Health;
        for (var i = 0; i < 30; i++) // 1 more second while oxygen is critically low
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 2).Health;

        return after < before;
    }

    // The generator physically sits in the corridor (Ship.cs: "system-oxygen" is in "corridor")
    // and only produces oxygen there in proportion to power routed to it (World.Atmosphere.cs).
    // Waiting for a corridor-specific breach via the normal long random fight (like
    // World_Decompression_DrainsHealthInBreachedRoom does) doesn't work as a "stays healthy when
    // powered" check: by the time corridor takes its own hit, other rooms have likely also taken
    // several unrelated breaches and sit far below FullOxygen — and since only the corridor has a
    // generator, heavily depleted neighbors can diffusion-drain it faster than one generator can
    // keep up, independent of whether the power question this test cares about is even true. So
    // instead: retry fresh, short (single attack-cycle) encounters until one lands exactly one
    // ship-wide breach in the corridor while every other room is still untouched (fresh spawn, so
    // everything else is still at FullOxygen) — an isolated scenario where full power should
    // trivially keep up with just its own room's single 3/sec leak. A single attack has only
    // ~7% odds of landing exactly this (most of its own outcomes are turret/system damage or a
    // wall breach elsewhere on the ship), so the retry budget needs a wide enough margin that
    // exhausting it is negligible, not just "usually enough".
    private static bool World_Oxygen_GeneratorRestoresRoomOxygenWhenPowered()
    {
        for (var attempt = 0; attempt < 300; attempt++)
        {
            var world = new World();
            world.SpawnCharacter(1); // pilot — only sends commands

            // PowerSystemId order: Oxygen(0), Engine, Shields, WeaponCharger, Secondary.
            world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 0, PowerDirection: 1f, TravelToPointId: "sector-alpha"));
            for (var i = 0; i < 120 * 30 && world.Phase != VoyagePhase.Battle; i++)
                world.Step(RealtimeStep);

            for (var i = 0; i < 7 * 30; i++) // just past the first 6s attack-cooldown tick
                world.Step(RealtimeStep);

            var snapshot = world.CreateSnapshot();
            var totalBreaches = snapshot.WallBlockStates.Count(s => s.Breached);
            if (totalBreaches != 1 || !RoomHasBreach(snapshot, "corridor"))
                continue; // this attempt's single attack didn't land the isolated scenario we want

            for (var i = 0; i < 10 * 30; i++) // let it settle under full power
                world.Step(RealtimeStep);

            return world.CreateSnapshot().RoomOxygen.First(o => o.RoomId == "corridor").Oxygen > 70f;
        }

        return false; // never landed the isolated single-breach scenario within the attempt budget
    }

    private static bool World_RepairBreach_ClearsItViaInteract()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor
        EquipSuit(world, 1); // survives however long it takes the corridor to actually take a hit, and the walk to it once it does

        var weldingToolSlot = TakeFromRack(world, ItemType.WeldingTool);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: weldingToolSlot)); // hold it (two hands)

        TakeTankFromRack(world, ItemType.WeldingTank);
        AttachTankTo(world, Array.IndexOf(
            world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), ItemType.WeldingTool),
            ItemType.WeldingTank);

        BreachRoom(world, "corridor"); // stop the moment the corridor takes its first hit

        // A room can hold several independent breaches now — the welder is a held, aimed flame
        // (World.Welding.cs) rather than an F-press, so aim it at whichever breach is nearest and
        // hold it lit for a bit; assert the count drops by exactly one rather than "cleared". Which
        // wall block actually takes the hit is random (World.EnemyAi.cs) - walk to whichever one it
        // really was rather than a fixed pre-chosen spot, so the welder is in range regardless of
        // which wall of the room got hit.
        var breachCountBefore = CountBreaches(world.CreateSnapshot(), "corridor");
        if (breachCountBefore == 0)
            return false;

        var breachedSnapshot = world.CreateSnapshot();
        var breachedBlock = breachedSnapshot.WallBlocks.First(b =>
            b.RoomId == "corridor" && breachedSnapshot.WallBlockStates.First(s => s.Id == b.Id).Breached);
        WalkAcrossShipTo(world, breachedBlock.X, breachedBlock.Y);

        for (var i = 0; i < 5 * 30 && CountBreaches(world.CreateSnapshot(), "corridor") == breachCountBefore; i++)
        {
            var snapshot = world.CreateSnapshot();
            var me = snapshot.Characters.Single(c => c.PlayerId == 1);
            var target = snapshot.WallBlocks
                .Where(b => b.RoomId == "corridor" && snapshot.WallBlockStates.First(s => s.Id == b.Id).Breached)
                .OrderBy(b => (new Vec2(b.X, b.Y) - new Vec2(me.X, me.Y)).Length())
                .First();
            var aim = new Vec2(target.X - me.X, target.Y - me.Y);
            aim = aim.Length() > 0.01f ? aim.Normalized() : new Vec2(0f, -1f);
            world.ApplyCommand(1, new ClientCommand(1, WeldHeld: true, LookX: aim.X, LookY: aim.Y));
            world.Step(RealtimeStep);
        }

        return CountBreaches(world.CreateSnapshot(), "corridor") == breachCountBefore - 1;
    }

    // The enemy AI can now damage a turret instead of breaching a room; a single F press on a
    // damaged turret repairs it rather than manning it, so a longer battle may need 2 presses.
    private static void EnsureManning(World world, int playerId, string turretId)
    {
        for (var i = 0; i < 3; i++)
        {
            if (world.CreateSnapshot().TurretStates.Any(t => t.Id == turretId && t.MannedByPlayerId == playerId))
                return;
            world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true));
        }
    }

    // Fires the bow turret until the enemy is defeated, reacting to whatever the enemy AI
    // throws at that turret along the way (reload trips, wrench repairs) instead of assuming a
    // fixed number of clean magazines — the AI's chance to damage a turret makes that assumption
    // flaky over a long fight.
    private static void FireBowTurretUntilEnemyDefeated(World world, int playerId)
    {
        const string turretId = "turret-bow";

        // Grab and hold a wrench up front so a mid-fight turret-damage attack can be repaired —
        // repair now requires the tool actually held in hand, not just F near the turret.
        var wrenchSlot = TakeFromRack(world, ItemType.Wrench);
        world.ApplyCommand(playerId, new ClientCommand(playerId, ToggleHoldSlotIndex: wrenchSlot)); // hold it

        // A sector's whole squadron is in the field at once now (World.EnemyFleet.cs), so clearing
        // one means shooting three hulls down rather than one - and shells that miss a wingman
        // sitting off the firing line cost iterations too.
        // The budget is generous on purpose: a run where the raiders keep disabling the gun spends
        // most of its iterations repairing and reloading rather than shooting, and running out mid
        // fight makes whatever test called this fail for a reason that has nothing to do with what
        // it was checking. Now that the roll sequence is seeded (World.EnemyAi.cs) an unlucky run is
        // reproducible rather than occasional - so it has to be survivable rather than rare.
        for (var iteration = 0; iteration < 400 && world.CreateSnapshot().Enemy.Hp > 0; iteration++)
        {
            var state = world.CreateSnapshot().TurretStates.Single(t => t.Id == turretId);

            if (state.Damaged)
            {
                MoveCharacterTo(world, playerId, 1.5f, 3f);
                if (world.CreateSnapshot().TurretStates.Single(t => t.Id == turretId).MannedByPlayerId == playerId)
                    world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // stand up first
                world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // repair
                continue;
            }

            if (state.AmmoRemaining <= 0)
            {
                if (state.MannedByPlayerId == playerId)
                    world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // stand up
                MoveCharacterTo(world, playerId, 15f, 3f);
                world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // pick up a crate
                MoveCharacterTo(world, playerId, 1.5f, 3f);
                world.ApplyCommand(playerId, new ClientCommand(playerId, InteractPressed: true)); // reload
                continue;
            }

            MoveCharacterTo(world, playerId, 1.5f, 3f);
            EnsureManning(world, playerId, turretId);
            world.ApplyCommand(playerId, new ClientCommand(playerId, FirePressed: true));
            for (var i = 0; i < 20; i++) // outlast the 0.5s cooldown before the next shot
                world.Step(RealtimeStep);
        }
    }

}
