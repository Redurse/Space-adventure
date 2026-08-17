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
        world.ApplyCommand(1, new ClientCommand(1, TravelToPointId: "sector-alpha"));
        for (var i = 0; i < 5 * 30 && world.Phase != VoyagePhase.Battle; i++)
            world.Step(RealtimeStep);

        var slot = TakeFromRack(world, weapon);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));

        EquipSuit(world, 1);
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f); // exit into vacuum, attached to the hull

        var target = world.CreateSnapshot().EnemyShipPosition;
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
            var current = snapshot.EnemyShipPosition;
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

    private static bool World_Boarding_FireWeaponDamagesCrewInSameRoom()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife); // knife is melee-only - has to close in

        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip)
            return false;

        var defender = world.CreateSnapshot().EnemyCrew.First(c => c.RoomId == world.EnemyShipLayout.BoardingRoomId);
        var healthBefore = defender.Health;

        // Walk right up to the defender in the boarding room, then swing.
        for (var i = 0; i < 5 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var toTarget = new Vec2(defender.X - me.X, defender.Y - me.Y);
            if (toTarget.Length() <= 0.6f)
                break;
            var dir = toTarget.Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: dir.Y));
            world.Step(RealtimeStep);
        }

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0, FirePressed: true));
        world.Step(RealtimeStep);

        var after = world.CreateSnapshot().EnemyCrew.First(c => c.Id == defender.Id);
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

        var defender = world.CreateSnapshot().EnemyCrew.First(c => c.RoomId == world.EnemyShipLayout.BoardingRoomId);
        for (var i = 0; i < 5 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var toTarget = new Vec2(defender.X - me.X, defender.Y - me.Y);
            if (toTarget.Length() <= 0.6f)
                break;
            var dir = toTarget.Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: dir.Y));
            world.Step(RealtimeStep);
        }

        var healthBefore = world.CreateSnapshot().EnemyCrew.First(c => c.Id == defender.Id).Health;
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0, FirePressed: true));
        world.Step(RealtimeStep);

        return world.CreateSnapshot().EnemyCrew.First(c => c.Id == defender.Id).Health == healthBefore;
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

        // Work through the ship room by room, walking toward the nearest living defender and
        // firing whenever one is in range.
        for (var i = 0; i < 120 * 30 && world.CreateSnapshot().EnemyCrew.Any(c => c.Alive); i++)
        {
            var snapshot = world.CreateSnapshot();
            var me = snapshot.Characters.Single(c => c.PlayerId == 1);
            if (me.Health <= 0)
                return false; // died boarding - not what this test is checking

            var target = snapshot.EnemyCrew.Where(c => c.Alive)
                .OrderBy(c => (new Vec2(c.X, c.Y) - new Vec2(me.X, me.Y)).Length())
                .First();
            var toTarget = new Vec2(target.X - me.X, target.Y - me.Y);
            var dir = toTarget.Length() > 0.001f ? toTarget.Normalized() : Vec2.Zero;

            // A boarded hull is buttoned up (World.cs registers its doors closed), so advancing
            // means opening the one in front of you - the same click a player makes, done here as
            // soon as the boarder is within arm's reach of it.
            foreach (var door in world.EnemyShipLayout.Doors)
                if (!world.IsDoorOpen(door.Id) && (door.Position - new Vec2(me.X, me.Y)).Length() < 1.5f)
                    world.ToggleDoor(door.Id);

            // Doors sit at the rooms' shared mid-height, so approach along that row first.
            var moveY = Math.Abs(me.Y - 3f) > 0.2f ? Math.Sign(3f - me.Y) : dir.Y;
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: moveY, FirePressed: true));
            world.Step(RealtimeStep);
        }

        return world.CreateSnapshot().EnemyCrew.All(c => !c.Alive) && world.CreateSnapshot().Enemy.Hp <= 0;
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

    // Which hull defends a sector is fixed by the sector, not rolled fresh: run from a fight, come
    // back, and it has to be the same opposition waiting - otherwise retreating would be a way to
    // reroll a gunship into a freighter.
    private static bool World_Boarding_SectorAlwaysFieldsTheSameHull()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EngageSector(world, "sector-beta");
        var first = world.CreateSnapshot().EnemyShipClassName;

        var again = new World();
        again.SpawnCharacter(1);
        EngageSector(again, "sector-beta");

        var elsewhere = new World();
        elsewhere.SpawnCharacter(1);
        EngageSector(elsewhere, "sector-alpha");

        // Same sector, same hull. (The two sectors are allowed to match - what matters is that the
        // answer is a property of the sector, which the repeat run is what proves.)
        return first == again.CreateSnapshot().EnemyShipClassName
               && EnemyShipLayout.All.Any(l => l.Name == elsewhere.CreateSnapshot().EnemyShipClassName);
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
            world.CreateSnapshot().EnemyRoomOxygen.First(o => o.RoomId == roomId).Oxygen;

        // Sealed: the breach vents its own compartment and nothing else, however long it stands.
        for (var i = 0; i < 10 * 30; i++)
            world.Step(RealtimeStep);
        if (Oxygen(layout.BoardingRoomId) > 1f || Oxygen(deepRoom.Id) < 99f)
            return false;

        foreach (var door in layout.Doors)
            world.ToggleDoor(door.Id);
        for (var i = 0; i < 40 * 30; i++)
            world.Step(RealtimeStep);

        bool Alive(string crewId) => world.CreateSnapshot().EnemyCrew.First(c => c.Id == crewId).Alive;
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

        var defender = world.CreateSnapshot().EnemyCrew.First(c => c.RoomId == world.EnemyShipLayout.BoardingRoomId);
        for (var i = 0; i < 5 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var toTarget = new Vec2(defender.X - me.X, defender.Y - me.Y);
            if (toTarget.Length() <= 0.6f)
                break;
            var dir = toTarget.Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: dir.X, MoveY: dir.Y));
            world.Step(RealtimeStep);
        }

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
