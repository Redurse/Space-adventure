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
        // Every interior ship door now starts closed by default (direct user request, "сделай
        // чтобы все двери на корабле изначально были закрыты") - opened up front, same as
        // MoveCharacterTo already does for its own callers below (TakeFromRack/EquipSuit), so the
        // walk to the weapon rack and suit locker isn't blocked by the new default. Safe to call
        // this early and this often specifically because DebugOpenAllDoors now excludes the
        // player's own airlock (see its own doc comment, World.Doors.cs) - EnterBattle undocks the
        // ship, and opening the REAL vacuum door while undocked would otherwise bleed the whole
        // ship's oxygen out for the rest of this setup, which none of these tests need at all
        // (DebugPlaceEvaCharacter below teleports straight onto the enemy hull instead).
        world.DebugOpenAllDoors();

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

    // Walks the boarder, tile by tile along the hull's own A* path, to melee reach of the nearest living defender, and
    // returns that defender's id. The defender is picked once (the nearest at the start) and kept - re-picking "nearest"
    // every tick while the crew are walking about thrashes between whoever is briefly closer. The crew move too (fighters
    // come for a boarder), so the path is re-planned every tick toward wherever the target is now.
    private static string WalkBoarderToMeleeRangeOfNearestDefender(World world)
    {
        string? targetId = null;
        for (var i = 0; i < 25 * 30; i++)
        {
            var snapshot = world.CreateSnapshot();
            var me = snapshot.Characters.Single(c => c.PlayerId == 1);
            var mePos = new Vec2(me.X, me.Y);
            var alive = snapshot.EnemyShip.Crew.Where(c => c.Alive).ToList();
            var target = alive.FirstOrDefault(c => c.Id == targetId)
                ?? alive.OrderBy(c => (new Vec2(c.X, c.Y) - mePos).Length()).First();
            targetId = target.Id;
            var targetPos = new Vec2(target.X, target.Y);
            if ((targetPos - mePos).Length() <= 0.9f)
                break;

            var path = TilePathfinder.FindPath(world.EnemyShipLayout.Tiles, TilePathfinder.TileAt(mePos), TilePathfinder.TileAt(targetPos));
            var waypoint = path is { Count: > 0 } ? TilePathfinder.CenterOf(path[0]) : targetPos;
            // The last tile before the target: walk straight at it rather than at the tile centre.
            if (path is { Count: <= 1 })
                waypoint = targetPos;
            var dir = (waypoint - mePos).Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: (float)dir.X, MoveY: (float)dir.Y));
            world.Step(RealtimeStep);
        }

        return targetId!;
    }

    // Pulls the trigger aimed straight at one defender (the aim is the cursor, not the walking direction).
    private static void FireAtCrew(World world, string crewId)
    {
        var snapshot = world.CreateSnapshot();
        var me = snapshot.Characters.Single(c => c.PlayerId == 1);
        var target = snapshot.EnemyShip.Crew.First(c => c.Id == crewId);
        var dir = (new Vec2(target.X, target.Y) - new Vec2(me.X, me.Y)).Normalized();
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 0, MoveY: 0, LookX: (float)dir.X, LookY: (float)dir.Y, FirePressed: true));
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

        // The round leaves on one tick and lands on the next.
        for (var i = 0; i < 3; i++)
        {
            FireAtCrew(world, defenderId);
            world.Step(RealtimeStep);
        }

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
        for (var i = 0; i < 3; i++)
        {
            FireAtCrew(world, defenderId);
            world.Step(RealtimeStep);
        }

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

        // Eight armed people are not something a lone boarder clears with a rifle, so the rest are put down directly and the
        // last one is really hunted: walk to them and shoot. The kill that empties the ship is what takes it.
        var last = world.EnemyShipLayout.CrewSpawns.Last().Id;
        foreach (var spawn in world.EnemyShipLayout.CrewSpawns.Where(s => s.Id != last))
            world.DebugDamageEnemyCrew(spawn.Id, EnemyCrewHealthForTests);

        var targetId = WalkBoarderToMeleeRangeOfNearestDefender(world);
        for (var i = 0; i < 6 * 30 && (world.CreateSnapshot().EnemyShip.Crew.FirstOrDefault(c => c.Id == targetId)?.Alive ?? false); i++)
        {
            FireAtCrew(world, targetId);
            world.Step(RealtimeStep);
        }

        return world.CreateSnapshot().EnemyShip.Crew.All(c => !c.Alive) && world.CreateSnapshot().Enemy.Hp <= 0;
    }


    // Air as a weapon (World.EnemyAtmosphere.cs): a sealed hull holds its air; a breach in one compartment's outer wall
    // drains it and, through the open doors, everything connected to it - and whoever is inside without a suit is on a
    // clock (fleeing to thinner and thinner rooms doesn't save them), while a crew that fights in suits doesn't care.
    private static bool World_Boarding_BreachVentsTheHullAndSuffocatesUnsuitedCrew()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EngageSector(world, "sector-alpha");

        var layout = world.EnemyShipLayout;
        float Oxygen(string roomId) =>
            world.CreateSnapshot().EnemyShip.RoomOxygen.First(o => o.RoomId == roomId).Oxygen;

        // Sealed: nothing leaks, however long it stands.
        for (var i = 0; i < 10 * 30; i++)
            world.Step(RealtimeStep);
        if (layout.Rooms.Any(r => Oxygen(r.Id) < 99f))
            return false;

        // The engineers would weld the hole shut (World.EnemyCrew.cs) - this is about what an unrepaired breach does.
        world.DebugKillEnemyCrewWithRole(EnemyCrewRole.Engineer);
        var block = layout.WallBlocks.First(b => b.RoomId == layout.BoardingRoomId && !b.IsInterior);
        world.DebugBreachEnemyWallBlock(block.Id);
        for (var i = 0; i < 20 * 30; i++)
            world.Step(RealtimeStep);
        if (Oxygen(layout.BoardingRoomId) > 5f)
            return false;

        for (var i = 0; i < 100 * 30; i++)
            world.Step(RealtimeStep);

        bool Alive(string crewId) => world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == crewId).Alive;
        var anyAir = layout.Rooms.Any(r => Oxygen(r.Id) >= OxygenSafeThresholdForTests);
        var unsuitedGone = layout.CrewSpawns.Where(s => !s.Suited).All(s => !Alive(s.Id));
        var suitedHolding = layout.CrewSpawns.Where(s => s.Suited && s.Role != EnemyCrewRole.Engineer).All(s => Alive(s.Id)); // the engineers were killed above
        return !anyAir && unsuitedGone && suitedHolding;
    }

    private const float EnemyCrewHealthForTests = 1000f; // more than any crew member has

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
