using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // weapon, suit up, EVA out and fly across to the enemy hull. Reuses M18's exact
    // fly-toward-a-target pattern - boarding is the same "drift to a point in field space" move,
    // just aimed at the enemy ship instead of an ore deposit.
    private static void BoardEnemyShip(World world, ItemType weapon)
    {
        EnterBattle(world);

        var slot = TakeFromRack(world, weapon);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));

        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // exit into vacuum, boots off by default so not attached yet
        // Boots on and one settling step, so the push-off below actually has something to push
        // off from (World.Eva.cs's HandlePushOff is a no-op while not attached).
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);

        var target = world.CreateSnapshot().EnemyShip.Position;
        var exitPos = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var pushDirection = new Vec2(target.X - exitPos.X, target.Y - exitPos.Y).Normalized();
        world.ApplyCommand(1, new ClientCommand(1, PushOffPressed: true, PushOffDirectionX: pushDirection.X, PushOffDirectionY: pushDirection.Y));
        world.Step(RealtimeStep);

        // The target is re-read every tick: enemy hulls fly now (World.EnemyFleet.cs), so steering
        // at where one was when the boarder left the airlock would only ever reach empty space.
        for (var i = 0; i < 60 * 30; i++)
        {
            var snapshot = world.CreateSnapshot();
            var me = snapshot.Characters.Single(c => c.PlayerId == 1);
            if (me.OnEnemyShip)
                break;
            var current = snapshot.EnemyShip.Position;
            var dir = new Vec2(current.X - me.X, current.Y - me.Y).Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: dir.Y));
            world.Step(RealtimeStep);
        }

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0)); // see WalkFixedDirection's own note
    }

    private static bool World_Boarding_EvaDuringBattle_ReachesEnemyShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return me.OnEnemyShip && !me.IsOutside;
    }

    // Not every class holds its boarding room (Gunship's breach is empty, unlike Raider/Freighter),
    // so this commits to whichever living defender is nearest by door-graph hops (not recomputed
    // every tick - re-picking "nearest" by straight-line distance while crossing rooms can thrash
    // between two defenders that are each briefly closer mid-walk) and walks the door path to them
    // one waypoint at a time, each leg with its own generous timeout. Returns that defender's id.
    private static string WalkBoarderToMeleeRangeOfNearestDefender(World world)
    {
        var me0 = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var myRoom0 = world.EnemyShipLayout.Rooms.FirstOrDefault(r => r.Contains(new Vec2(me0.X, me0.Y)));
        var target = world.CreateSnapshot().EnemyShip.Crew.Where(c => c.Alive)
            .OrderBy(c => myRoom0 is null ? 0 : FindDoorPath(world.EnemyShipLayout.Doors, myRoom0.Id, c.RoomId).Count)
            .First();

        var waypoints = myRoom0 is null ? new List<Vec2>() : FindDoorPath(world.EnemyShipLayout.Doors, myRoom0.Id, target.RoomId);
        waypoints.Add(new Vec2(target.X, target.Y));

        foreach (var waypoint in waypoints)
        {
            for (var i = 0; i < 10 * 30; i++)
            {
                var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
                var toWaypoint = waypoint - new Vec2(me.X, me.Y);
                if (toWaypoint.Length() <= 0.6f)
                    break;

                foreach (var door in world.EnemyShipLayout.Doors)
                    if (!world.IsDoorOpen(door.Id) && (door.Position - new Vec2(me.X, me.Y)).Length() < 1.5f)
                        world.ToggleDoor(door.Id);

                var dir = toWaypoint.Normalized();
                world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: dir.Y));
                world.Step(RealtimeStep);
            }
        }

        return target.Id;
    }

    private static bool World_Boarding_FireWeaponDamagesCrewInSameRoom()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife); // knife is melee-only - has to close in

        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip)
            return false;

        var defenderId = WalkBoarderToMeleeRangeOfNearestDefender(world);
        var healthBefore = world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == defenderId).Health;

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0, FirePressed: true));
        world.Step(RealtimeStep);

        var after = world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == defenderId);
        return after.Health < healthBefore;
    }

    private static bool World_Boarding_WithoutWeaponHeld_DoesNothing()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife);

        // Drop the knife out of hand - unarmed, Space must do nothing at all.
        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        var knifeSlot = Array.IndexOf(inventory.MainSlots.ToArray(), ItemType.Knife);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: knifeSlot)); // un-hold

        var defenderId = WalkBoarderToMeleeRangeOfNearestDefender(world);

        var healthBefore = world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == defenderId).Health;
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0, FirePressed: true));
        world.Step(RealtimeStep);

        return world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == defenderId).Health == healthBefore;
    }

    // Clearing every defender captures the ship outright - an alternative win condition to
    // shelling it down from the turrets (game_design.md Phase 3).
    private static bool World_Boarding_KillingAllCrew_DestroysEnemyShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.LaserRifle); // longest range - can clear a room without closing to melee

        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip)
            return false;

        // Work through the ship one defender at a time: walk to melee range of the nearest one
        // (WalkBoarderToMeleeRangeOfNearestDefender's own door-graph BFS, robust to any hull shape -
        // Frigate's spine runs the other way from the older classes, like the player's own Corvette),
        // then hose it down with the rifle - well within its actual firing range by the time you're
        // that close - before moving on to whoever's nearest next.
        for (var round = 0; round < world.EnemyShipLayout.CrewSpawns.Count && world.CreateSnapshot().EnemyShip.Crew.Any(c => c.Alive); round++)
        {
            if (world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health <= 0)
                return false; // died boarding - not what this test is checking

            WalkBoarderToMeleeRangeOfNearestDefender(world);
            for (var i = 0; i < 3 * 30; i++)
            {
                world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0, FirePressed: true));
                world.Step(RealtimeStep);
            }
        }

        return world.CreateSnapshot().EnemyShip.Crew.All(c => !c.Alive) && world.CreateSnapshot().Enemy.Hp <= 0;
    }

    // Every hull class is a distinct structure, and nothing about it may collide with another's:
    // door state and the room a character stands in are flat dictionaries shared by every structure
    // in the game, so two classes reusing an id would be the same door and the same room.
    private static bool EnemyShipClasses_AreDistinctStructures()
    {
        var layouts = EnemyShipLayout.All;
        if (layouts.Count < 3 || layouts.Select(l => l.Kind).Distinct().Count() != layouts.Count)
            return false;

        var roomIds = layouts.SelectMany(l => l.Rooms.Select(r => r.Id)).ToList();
        var doorIds = layouts.SelectMany(l => l.Doors.Select(d => d.Id).Append(l.BoardingHatch.Id)).ToList();
        var crewIds = layouts.SelectMany(l => l.CrewSpawns.Select(c => c.Id)).ToList();

        // Every class also has to be walkable end to end: a breach compartment that is actually one
        // of its rooms, and every defender standing in a room that exists.
        foreach (var layout in layouts)
        {
            if (layout.Rooms.All(r => r.Id != layout.BoardingRoomId))
                return false;
            if (layout.CrewSpawns.Any(c => layout.Rooms.All(r => r.Id != c.RoomId)))
                return false;
        }

        return roomIds.Distinct().Count() == roomIds.Count
               && doorIds.Distinct().Count() == doorIds.Count
               && crewIds.Distinct().Count() == crewIds.Count;
    }

    // The Frigate's whole point is matching the player's own Corvette footprint (Ship.Corvette.cs:
    // x 0..13.5, y 0..18.5) while fielding a fixed 2-magnetic/1-laser loadout no other class carries.
    private static bool EnemyShipClasses_FrigateMatchesCorvetteFootprintAndCarriesItsFixedGuns()
    {
        var frigate = EnemyShipLayout.Of(EnemyShipClass.Frigate);
        var left = frigate.Rooms.Min(r => r.X);
        var top = frigate.Rooms.Min(r => r.Y);
        var right = frigate.Rooms.Max(r => r.X + r.Width);
        var bottom = frigate.Rooms.Max(r => r.Y + r.Height);
        if (left != 0 || top != 0 || right != 13.5f || bottom != 18.5f)
            return false;

        return frigate.WeaponLoadout is { Count: 3 } loadout
               && loadout.Count(w => w == TurretWeaponType.Magnetic) == 2
               && loadout.Count(w => w == TurretWeaponType.Laser) == 1
               // Every other class keeps the older behavior: whichever single weapon the squadron
               // formation hands it, not a loadout of its own.
               && EnemyShipLayout.All.Where(l => l.Kind != EnemyShipClass.Frigate).All(l => l.WeaponLoadout is null);
    }

    // Which hull defends a sector is fixed by the sector, not rolled fresh: run from a fight, come
    // back, and it has to be the same opposition waiting - otherwise retreating would be a way to
    // reroll a gunship into a freighter.
    private static bool World_Boarding_SectorAlwaysFieldsTheSameHull()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EngageSector(world, "sector-beta");
        var first = world.CreateSnapshot().EnemyShip.ClassName;

        var again = new World();
        again.SpawnCharacter(1);
        EngageSector(again, "sector-beta");

        var elsewhere = new World();
        elsewhere.SpawnCharacter(1);
        EngageSector(elsewhere, "sector-alpha");

        // Same sector, same hull. (The two sectors are allowed to match - what matters is that the
        // answer is a property of the sector, which the repeat run is what proves.)
        return first == again.CreateSnapshot().EnemyShip.ClassName
               && EnemyShipLayout.All.Any(l => l.Name == elsewhere.CreateSnapshot().EnemyShip.ClassName);
    }

    // Air as a weapon (World.EnemyAtmosphere.cs): a boarded hull is buttoned up, so its compartments
    // hold their air until someone opens a door onto the breach - and then whoever is inside without
    // a suit is on a clock, while a crew that fights in suits doesn't care.
    private static bool World_Boarding_OpeningDoors_VentsTheHullAndSuffocatesUnsuitedCrew()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EngageSector(world, "sector-alpha");

        var layout = world.EnemyShipLayout;
        var deepRoom = layout.Rooms.Last(r => r.Id != layout.BoardingRoomId);
        float Oxygen(string roomId) =>
            world.CreateSnapshot().EnemyShip.RoomOxygen.First(o => o.RoomId == roomId).Oxygen;

        // Sealed: the breach vents its own compartment and nothing else, however long it stands.
        for (var i = 0; i < 10 * 30; i++)
            world.Step(RealtimeStep);
        if (Oxygen(layout.BoardingRoomId) > 1f || Oxygen(deepRoom.Id) < 99f)
            return false;

        foreach (var door in layout.Doors)
            world.ToggleDoor(door.Id);
        for (var i = 0; i < 40 * 30; i++)
            world.Step(RealtimeStep);

        bool Alive(string crewId) => world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == crewId).Alive;
        var unsuitedGone = layout.CrewSpawns.Where(s => !s.Suited).All(s => !Alive(s.Id));
        var suitedHolding = layout.CrewSpawns.Where(s => s.Suited).All(s => Alive(s.Id));

        return Oxygen(deepRoom.Id) < OxygenSafeThresholdForTests && unsuitedGone && suitedHolding;
    }

    private const float OxygenSafeThresholdForTests = 50f; // mirrors World.Atmosphere.cs's own threshold

    // Losing the hull you are standing in throws you out of it. The next ship of the squadron is a
    // different floor plan, so staying "aboard" would leave the character in a compartment that no
    // longer exists anywhere.
    private static bool World_Boarding_HullDestroyedUnderneath_EjectsTheBoardingParty()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip)
            return false;

        world.Enemy.ApplyDamage(world.Enemy.Hp); // a turret finishes the hull off while they're inside
        world.Step(RealtimeStep);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return !me.OnEnemyShip && me.IsOutside;
    }

    private static bool World_Boarding_CrewFightsBack_DamagesBoarder()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife);

        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip)
            return false;

        WalkBoarderToMeleeRangeOfNearestDefender(world);

        var healthBefore = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0));
        for (var i = 0; i < 5 * 30; i++) // outlast the defenders' attack interval, taking no action
            world.Step(RealtimeStep);

        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health < healthBefore;
    }

    // Fly to a hostile sector and shell its ship down - the standing-moving event
    // (World.Factions.cs's RecordShipDestroyed) fires on the transition back out of the fight.
    // Clears a whole sector, however many ships defend it. The retry loop matters now that sectors
}
