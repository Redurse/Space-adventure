using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // weapon, suit up, and get aboard - a precondition for tests about what happens once boarded,
    // not about the journey there (World_Boarding_MagneticBootsAttachToEnemyHull/
    // CuttingEnemyHullDamagesIt/CrossingACutHullBreachBoardsIntoThatRoom cover that mechanism
    // itself), so this uses the debug precondition setters (DebugBreachEnemyWallBlock,
    // DebugPlaceEvaCharacter's attachToEnemyShip) to get there directly rather than simulating a
    // real cutting job and flight in - lands in EnemyShipLayout.BoardingRoomId, same compartment the
    // old fixed-hatch entry always used, so every existing test built against that room still holds.
    // withCutter/withWelder: grabbed from the rack and tanked up alongside the weapon, while still
    // indoors on the player's own ship - TakeFromRack walks there via WalkAcrossShipTo, which only
    // makes sense before crossing over, so a test that wants to cut/weld aboard the enemy hull has
    // to ask for the tool here rather than trying to fetch it afterward.
    private static void BoardEnemyShip(World world, ItemType weapon, bool withCutter = false, bool withWelder = false)
    {
        EnterBattle(world);

        var slot = TakeFromRack(world, weapon);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));
        EquipSuit(world, 1);

        if (withCutter)
        {
            var cutterSlot = TakeFromRack(world, ItemType.Cutter);
            world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: cutterSlot));
            TakeTankFromRack(world);
            AttachTankTo(world, cutterSlot);
        }
        if (withWelder)
        {
            var welderSlot = TakeFromRack(world, ItemType.WeldingTool);
            world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: welderSlot));
            TakeTankFromRack(world, ItemType.WeldingTank);
            AttachTankTo(world, welderSlot, ItemType.WeldingTank);
        }

        var localCenter = world.EnemyShipLayout.GetLocalBounds().Center;
        var block = world.EnemyShipLayout.WallBlocks.First(b => b.RoomId == world.EnemyShipLayout.BoardingRoomId);
        world.DebugBreachEnemyWallBlock(block.Id);
        var localOffset = block.Position - localCenter;
        world.DebugPlaceEvaCharacter(1, EnemyHullBlockWorldPosition(world, localOffset), attachToEnemyShip: true);

        // Walking straight toward the hull's own centre from right at the breach is "stepping
        // inward" by definition (World.Eva.cs's StepEnemyShipAttachedWalk) - re-rotated to world
        // space every tick since the hull keeps turning under the character's boots.
        var inwardLocalDir = (localCenter - block.Position).Normalized();
        for (var i = 0; i < 30 && !world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip; i++)
        {
            var enemy = world.CreateSnapshot().EnemyShip.Ships.First(s => s.IsBoardable);
            var worldDir = RotateLocalToWorld(inwardLocalDir, enemy.RotationDegrees);
            world.ApplyCommand(1, new ClientCommand(1, MoveX: (float)worldDir.X, MoveY: (float)worldDir.Y));
            world.Step(RealtimeStep);
        }

        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0)); // see WalkFixedDirection's own note
    }

    // Same rotation World.Eva.cs's RotateLocalToWorld/ShipLocalFrame.ToWorldDirection use, kept local
    // to the test file since it's server-internal there and there's no third caller to share it with.
    private static Vec2 RotateLocalToWorld(Vec2 local, float rotationDegrees)
    {
        var radians = rotationDegrees * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vec2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
    }

    // The boardable enemy's current world position of a WallBlock that started life at localOffset
    // from the hull's own centre - re-read every tick since the hull moves and turns mid-fight
    // (World.EnemyFleet.cs), exactly mirroring the maths World.Cutting.cs/World.Boarding.cs use
    // server-side to test the same aim.
    private static Vec2 EnemyHullBlockWorldPosition(World world, Vec2 localOffset)
    {
        var enemy = world.CreateSnapshot().EnemyShip.Ships.First(s => s.IsBoardable);
        return new Vec2(enemy.X, enemy.Y) + RotateLocalToWorld(localOffset, enemy.RotationDegrees);
    }

    // The always-open fixed hatch (World.Boarding.cs's BoardingReachRadius=6, measured from the
    // hull's own centre) already covers most of a small hull - Gunship's own farthest corner is
    // only ~8.75 out, not enough clearance for an approach to reliably reach it without boarding via
    // the old path first by accident. Frigate is the one class with real margin (~11.45 at its far
    // corners) and, being 5 rooms rather than a single breach compartment, also has a far corner
    // that ISN'T the boarding room - the only way these two tests can actually tell "boarded via the
    // new cut hole" apart from "boarded via the always-open hatch and just happened to land nearby".
    // Cutting an enemy hull works exactly like the player's own ship (World.Cutting.cs reuses the
    // same reach/rate/samples): suit up with a cutter instead of a weapon, get right up on a wall
    // block instead of the fixed hatch, and hold the flame on it. Forces Frigate (World.
    // DebugForceEnemyClass) rather than relying on whatever a sector's own id happens to hash to -
    // it's the one class with enough clearance beyond the always-open fixed hatch's
    // BoardingReachRadius to prove this is really the new cut-hole path and not the old one.
    // Teleports there (World.DebugPlaceEvaCharacter) instead of flying for real: the target orbits
    // fast enough (EnemyOrbitDegreesPerSecond) that a realistic approach chews through the whole
    // 10-second jetpack fuel budget (JetpackFuelPerSecond) just correcting course, which is a fuel-
    // management problem this test isn't about.
    private static bool World_Boarding_CuttingEnemyHullDamagesIt()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugForceEnemyClass(EnemyShipClass.Frigate);
        EnterBattle(world);
        ExitShipIntoVacuum(world);

        var localCenter = world.EnemyShipLayout.GetLocalBounds().Center;
        var boardingRoomId = world.EnemyShipLayout.BoardingRoomId;
        var block = world.EnemyShipLayout.WallBlocks.Where(b => b.RoomId != boardingRoomId)
            .OrderByDescending(b => (b.Position - localCenter).Length()).First();
        var localOffset = block.Position - localCenter;
        world.DebugPlaceEvaCharacter(1, EnemyHullBlockWorldPosition(world, localOffset));

        // Re-aims every tick (the hull keeps drifting/turning under it) but never moves - aiming is
        // free, thrust costs jetpack fuel, and the block starts at zero distance so a few ticks of
        // gradual drift is nowhere near enough to carry it out of the flame's reach. Standing right
        // on top of one block's exact position also puts its immediate neighbours within the same
        // sampled reach, so this checks whether cutting damaged *any* of the hull's blocks rather
        // than insisting on this exact one - which specific panel the flame catches first is the
        // aiming algorithm's own tie-break, not something this test needs to dictate.
        var healthBefore = world.CreateSnapshot().EnemyShip.WallBlockStates.Sum(s => s.Hp);
        for (var i = 0; i < 10; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var toBlock = EnemyHullBlockWorldPosition(world, localOffset) - new Vec2(me.X, me.Y);
            var dir = toBlock.Length() > 0.001f ? toBlock.Normalized() : new Vec2(1f, 0f);
            world.ApplyCommand(1, new ClientCommand(1, CutHeld: true, LookX: (float)dir.X, LookY: (float)dir.Y));
            world.Step(RealtimeStep);
        }

        var healthAfter = world.CreateSnapshot().EnemyShip.WallBlockStates.Sum(s => s.Hp);
        return healthAfter < healthBefore;
    }

    // The other half of the same feature: once a wall is actually breached, walking through it while
    // magnetized to the hull (World.Eva.cs's StepEnemyShipAttachedWalk) boards the player into the
    // room right behind THAT block, not the hull's fixed hatch. DebugBreachEnemyWallBlock is the
    // test-only precondition setter, same convention as the player's own World.DebugBreachWallBlock,
    // standing in for a finished cutting job; DebugPlaceEvaCharacter's attachToEnemyShip drops the
    // character already magnetized right at the hole instead of needing a real flight there first
    // (boarding only ever crosses in while attached and walking now, the same as the player's own
    // ship always has).
    private static bool World_Boarding_CrossingACutHullBreachBoardsIntoThatRoom()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugForceEnemyClass(EnemyShipClass.Frigate);
        EnterBattle(world);
        ExitShipIntoVacuum(world);

        var localCenter = world.EnemyShipLayout.GetLocalBounds().Center;
        var boardingRoomId = world.EnemyShipLayout.BoardingRoomId;
        var block = world.EnemyShipLayout.WallBlocks.Where(b => b.RoomId != boardingRoomId)
            .OrderByDescending(b => (b.Position - localCenter).Length()).First();
        var localOffset = block.Position - localCenter;
        if (!world.DebugBreachEnemyWallBlock(block.Id))
            return false;
        world.DebugPlaceEvaCharacter(1, EnemyHullBlockWorldPosition(world, localOffset), attachToEnemyShip: true);

        // Walking straight toward the hull's own centre from right at the breach is "stepping
        // inward" by definition - re-rotated out to world space every tick since the hull keeps
        // turning under the character's boots.
        var inwardLocalDir = (localCenter - block.Position).Normalized();
        for (var i = 0; i < 30 && !world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip; i++)
        {
            var enemy = world.CreateSnapshot().EnemyShip.Ships.First(s => s.IsBoardable);
            var worldDir = RotateLocalToWorld(inwardLocalDir, enemy.RotationDegrees);
            world.ApplyCommand(1, new ClientCommand(1, MoveX: (float)worldDir.X, MoveY: (float)worldDir.Y));
            world.Step(RealtimeStep);
        }

        var final = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var finalRoom = world.EnemyShipLayout.Rooms.FirstOrDefault(r => r.Contains(new Vec2(final.X, final.Y)));
        return final.OnEnemyShip && finalRoom?.Id == block.RoomId;
    }

    // Step 1 of the new flow ("игрок примагничивается к вражескому кораблю при помощи ботинок"):
    // drifting into the hull with boots on grabs on (World.Eva.cs's TryAutoAttach, EnemyShip branch)
    // exactly like it always has for the player's own ship - IsEvaAttached is the one bit the
    // snapshot exposes for "attached to something", which is enough to prove the branch fired
    // without needing to distinguish which structure from outside the server.
    private static bool World_Boarding_MagneticBootsAttachToEnemyHull()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugForceEnemyClass(EnemyShipClass.Frigate);
        EnterBattle(world);
        // Suits up while still indoors (EquipSuit's suit locker is at ship-interior coordinates,
        // same order BoardEnemyShip uses) - DebugPlaceEvaCharacter below does the actual "step
        // outside" itself, so there's no real ExitShipIntoVacuum crossing to sequence around.
        EquipSuit(world, 1);

        // The nearest-to-centre block, not the farthest corner: a raider's hull turns fast
        // (EnemyTurnDegreesPerSecond=120 in World.EnemyFleet.cs) and a point far from the rotation
        // axis sweeps many units per second under it - a free-floating chase of a distant corner
        // would need to out-fly that sweep with only the jetpack's own weak thrust, the same trap
        // World_Boarding_CuttingEnemyHullDamagesIt's own comment warns about. TryAutoAttach reacts
        // to proximity every tick regardless of movement input (StepFreeFloating runs
        // unconditionally), so placing the character already just inside the attach margin
        // (EnemyShipAttachZoneMargin=0.5) and taking a single step is enough to prove the magnetic-
        // boots hookup itself without needing a realistic approach flight.
        var localCenter = world.EnemyShipLayout.GetLocalBounds().Center;
        var block = world.EnemyShipLayout.WallBlocks.OrderBy(b => (b.Position - localCenter).Length()).First();
        var localOffset = block.Position - localCenter;
        var outward = localOffset.Normalized();
        world.DebugPlaceEvaCharacter(1, EnemyHullBlockWorldPosition(world, localOffset + outward * 0.2f));
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // boots on
        world.Step(RealtimeStep);

        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsEvaAttached;
    }

    // "резак и сварка работали корректно внутри вражеского корабля" - once aboard, the cutter has
    // to reach the enemy's own interior fittings (FindAimedEnemyIndoorTarget/
    // CutIndoorAlongFlameOnEnemyShip in World.Cutting.cs), not just the outer hull it cut through
    // to get in. Every hull class has exactly one interior door off its boarding room (confirmed by
    // inspection across all four EnemyShipLayout.Classes.cs hulls), so this doesn't need to force a
    // specific class the way the outer-hull tests do.
    private static bool World_Boarding_IndoorCuttingDamagesEnemyDoor()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife, withCutter: true);

        var door = world.EnemyShipLayout.Doors.First(d => d.Connects(world.EnemyShipLayout.BoardingRoomId));
        MoveCharacterTo(world, 1, (float)door.Position.X, (float)door.Position.Y);

        // WallCutDamagePerSecond=34 against DoorMaxHp=100 takes just under 3 real seconds of
        // continuous flame - well under 100 ticks at RealtimeStep, with margin for the cut not
        // landing every single tick.
        for (var i = 0; i < 120 && !world.IsDoorDestroyed(door.Id); i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var toDoor = door.Position - new Vec2(me.X, me.Y);
            var dir = toDoor.Length() > 0.001f ? toDoor.Normalized() : new Vec2(1f, 0f);
            world.ApplyCommand(1, new ClientCommand(1, CutHeld: true, LookX: (float)dir.X, LookY: (float)dir.Y));
            world.Step(RealtimeStep);
        }

        return world.IsDoorDestroyed(door.Id);
    }

    // The welder's own counterpart, sealing shut the very hole the boarder just cut through
    // (WeldIndoorAlongFlameOnEnemyShip) - BoardEnemyShip's entry block is already breached to zero
    // Hp by the time this lands inside, exactly the "already-damaged panel" the welder needs to
    // prove it can repair from the corridor side, same as it already does on the player's own ship.
    private static bool World_Boarding_IndoorWeldingRepairsBreachedEnemyWallBlock()
    {
        var world = new World();
        world.SpawnCharacter(1);
        BoardEnemyShip(world, ItemType.Knife, withWelder: true);

        var boardingRoomId = world.EnemyShipLayout.BoardingRoomId;
        var entryBlock = world.EnemyShipLayout.WallBlocks.First(b => b.RoomId == boardingRoomId);
        // No MoveCharacterTo here: the entry block is the breach itself, still passable while
        // unwelded, so bang-bang homing straight at its exact centre would just walk the character
        // back out through the hole into vacuum. BoardEnemyShip already stops the character right
        // beside it (the inward walk halts the instant OnEnemyShip flips true), well within
        // WelderReachUnits - close the hole from right where boarding actually lands.
        var hpBefore = world.CreateSnapshot().EnemyShip.WallBlockStates.First(s => s.Id == entryBlock.Id).Hp;

        for (var i = 0; i < 20; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            var toBlock = entryBlock.Position - new Vec2(me.X, me.Y);
            var dir = toBlock.Length() > 0.001f ? toBlock.Normalized() : new Vec2(1f, 0f);
            world.ApplyCommand(1, new ClientCommand(1, WeldHeld: true, LookX: (float)dir.X, LookY: (float)dir.Y));
            world.Step(RealtimeStep);
        }

        var hpAfter = world.CreateSnapshot().EnemyShip.WallBlockStates.First(s => s.Id == entryBlock.Id).Hp;
        return hpBefore <= 0f && hpAfter > hpBefore;
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
                world.ApplyCommand(1, new ClientCommand(1, MoveX: (float)dir.X, MoveY: (float)dir.Y));
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
        var doorIds = layouts.SelectMany(l => l.Doors.Select(d => d.Id).Concat(l.AirlockOuterDoors.Select(d => d.Id))).ToList();
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
