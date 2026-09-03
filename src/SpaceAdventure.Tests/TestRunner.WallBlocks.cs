using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // Cutter in hand, tank attached, ready to point at whatever's nearby - the ship-interior half
    // of ExitShipIntoVacuum's own cutter setup (TestRunner.Mining.cs), minus the suit/airlock part
    // since cutting a wall from inside needs neither.
    private static void EquipCutterWithTank(World world)
    {
        var cutterSlot = TakeFromRack(world, ItemType.Cutter);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: cutterSlot));
        TakeTankFromRack(world);
        AttachTankTo(world, Array.IndexOf(
            world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), ItemType.Cutter));
    }

    // Holds the cutter's flame on whichever wall block sits at (blockX, blockY) until it breaches
    // or the budget runs out - mirrors CutBlock's own "cut it or time out" shape (TestRunner.
    // Mining.cs), just aimed at a wall instead of an ore vein.
    private static int CutWallBlockAt(World world, float blockX, float blockY, Vec2 aim, int maxTicks)
    {
        for (var i = 0; i < maxTicks; i++)
        {
            var snapshot = world.CreateSnapshot();
            var block = snapshot.WallBlocks.First(b => Math.Abs(b.X - blockX) < 0.1f && Math.Abs(b.Y - blockY) < 0.1f);
            if (snapshot.WallBlockStates.First(s => s.Id == block.Id).Breached)
                return i;
            world.ApplyCommand(1, new ClientCommand(1, CutHeld: true, LookX: (float)aim.X, LookY: (float)aim.Y));
            world.Step(RealtimeStep);
        }
        return maxTicks;
    }

    // The cutter used to only exist for ore out in the field - it now also works a ship's own
    // walls from inside, and gradually (World.WallBlocks.cs's WallCutDamagePerSecond), not as an
    // instant break.
    private static bool World_WallCutter_DamagesGraduallyBeforeBreaching()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor
        EquipCutterWithTank(world);
        WalkAcrossShipTo(world, 11.5f, 0.5f); // corridor's top wall block sits at (11.5, 0)

        for (var i = 0; i < 30; i++) // 1 second - well short of the ~3s a full block takes
        {
            world.ApplyCommand(1, new ClientCommand(1, CutHeld: true, LookX: 0f, LookY: -1f));
            world.Step(RealtimeStep);
        }

        var partial = world.CreateSnapshot();
        var partialBlock = partial.WallBlocks.First(b => Math.Abs(b.X - 11.5f) < 0.1f && b.Y == 0f);
        var partialState = partial.WallBlockStates.First(s => s.Id == partialBlock.Id);
        if (partialState.Breached || partialState.Hp >= partialState.MaxHp)
            return false; // must be damaged by now, but not yet fully broken

        var ticksToBreak = CutWallBlockAt(world, 11.5f, 0f, new Vec2(0, -1), 4 * 30);
        return ticksToBreak < 4 * 30 && RoomHasBreach(world.CreateSnapshot(), "corridor");
    }

    // The cutter used to only ever bite into ore once outside (CutAlongFlame) - it now falls back
    // to the player's own hull out there too, breaching it from the outside in exactly like it
    // already breaches it from the inside out, and the same way the welder already patches a
    // breach from either side (World.Welding.cs's WeldAlongFlame).
    private static bool World_Cut_BreachesOwnHullFromOutside()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor
        EnterAsteroidFieldStationary(world);
        ExitShipIntoVacuum(world); // boots on, standing right on the plating beside the airlock

        // The airlock chamber's own *outward* wall (X=26, where the door itself sits) was built
        // with no wall blocks on it at all (Ship.cs's CreateStarter: GenerateOuterWallBlocks(...,
        // right: false)) - that whole face is the airlock door assembly, not separately weldable
        // plating. The nearest real block is on the *bottom* wall instead, which means walking
        // there magnetized (StepShipAttachedWalk) has to actually round the corner rather than
        // aim straight at it: a straight line from the door to a point on a *different* face of a
        // convex room cuts through the room's own interior, which StepShipAttachedWalk reads as
        // stepping back inward and, next to a still-open door, walks the character straight back
        // through it. Two fixed-direction legs instead - straight down the right face past the
        // corner, then straight along the bottom face - keep every step genuinely tangential, the
        // same way a player rounding a corner would hug the hull face by face rather than cut it;
        // SnapToHullSurface's own nearest-point projection is what actually carries the character
        // around the corner once the raw input pushes past it, matching its own doc comment.
        var block = world.Ship.WallBlocks.First(b => b.RoomId == "airlock-chamber" && b.Position.Y == 6f && b.Position.X == 25.5f);
        var hullCenterLocal = new Vec2(
            (world.Ship.Rooms.Min(r => r.Left) + world.Ship.Rooms.Max(r => r.Right)) / 2f,
            (world.Ship.Rooms.Min(r => r.Top) + world.Ship.Rooms.Max(r => r.Bottom)) / 2f);

        for (var i = 0; i < 2 * 30; i++) // down the right face, just past the bottom-right corner
        {
            world.ApplyCommand(1, new ClientCommand(1, MoveX: 0f, MoveY: 1f));
            world.Step(RealtimeStep);
        }
        // Left along the bottom face, now distance-checked rather than a fixed duration: the
        // block sits barely past the corner (the whole room is only 3 units wide), and a fixed
        // walk long enough to be safe on a wider hull overshot it by a wide margin here, sailing
        // straight past it down the *rest* of the ship's own bottom hull (every room shares the
        // same Y-bounds, so the bottom edge runs continuously the whole length of the ship) into
        // the corridor's own wall, units away from where it needed to stop.
        var shipFieldForWalk = world.CreateSnapshot().ShipField;
        var walkTarget = new Vec2(
            shipFieldForWalk.X + (block.Position.X - hullCenterLocal.X),
            shipFieldForWalk.Y + (block.Position.Y - hullCenterLocal.Y));
        for (var i = 0; i < 10 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
            if (new Vec2(walkTarget.X - me.X, walkTarget.Y - me.Y).Length() <= 1f)
                break;
            world.ApplyCommand(1, new ClientCommand(1, MoveX: -1f, MoveY: 0f));
            world.Step(RealtimeStep);
        }

        var afterWalk = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (!afterWalk.IsOutside || !afterWalk.IsEvaAttached)
            return false; // let go of the hull somewhere along the way

        var finalShipField = world.CreateSnapshot().ShipField;
        var finalTarget = new Vec2(
            finalShipField.X + (block.Position.X - hullCenterLocal.X),
            finalShipField.Y + (block.Position.Y - hullCenterLocal.Y));
        var aim = new Vec2(finalTarget.X - afterWalk.X, finalTarget.Y - afterWalk.Y).Normalized();
        for (var i = 0; i < 4 * 30; i++) // comfortably past the ~3s a full block takes
        {
            world.ApplyCommand(1, new ClientCommand(1, CutHeld: true, LookX: (float)aim.X, LookY: (float)aim.Y));
            world.Step(RealtimeStep);
        }

        return world.CreateSnapshot().WallBlockStates.First(s => s.Id == block.Id).Breached;
    }

    // Aiming a lit welder at a wall that's already at full health used to be impossible to
    // distinguish from aiming at nothing at all - now it reveals the block (WallToolTargetBlockId,
    // what the client's health bar keys off) without changing anything about it.
    private static bool World_WeldHealthyWall_RevealsHpWithoutChangingIt()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor, every wall starts at full health

        var weldingToolSlot = TakeFromRack(world, ItemType.WeldingTool);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: weldingToolSlot));
        TakeTankFromRack(world, ItemType.WeldingTank);
        AttachTankTo(world, Array.IndexOf(
            world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), ItemType.WeldingTool),
            ItemType.WeldingTank);

        WalkAcrossShipTo(world, 11.5f, 0.5f);
        world.ApplyCommand(1, new ClientCommand(1, WeldHeld: true, LookX: 0f, LookY: -1f));
        world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        var me = snapshot.Characters.Single(c => c.PlayerId == 1);
        if (me.WallToolTargetBlockId is not { } targetId)
            return false; // should reveal a target even though there's nothing to repair

        var state = snapshot.WallBlockStates.First(s => s.Id == targetId);
        return state.Hp >= state.MaxHp; // untouched - it was already full
    }

    // A single broken block is a leak to see space through, not a way out. Docked at the home
    // station the airlock itself wouldn't let anyone through either (TryCrossIntoVacuum blocks all
    // crossing while IsDocked - it leads onto the station's own walkway there, not vacuum), so
    // this has to actually be out in the field first for the breach check itself to be what's on
    // trial.
    private static bool World_Eva_SingleBrokenBlock_IsNotPassable()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor
        EnterAsteroidFieldStationary(world);
        EquipCutterWithTank(world);
        WalkAcrossShipTo(world, 11.5f, 0.5f);
        if (CutWallBlockAt(world, 11.5f, 0f, new Vec2(0, -1), 4 * 30) >= 4 * 30)
            return false; // never actually broke it

        EquipSuit(world, 1); // ends at the engine-room locker
        WalkAcrossShipTo(world, 11.5f, 0.5f); // back to the breach
        WalkFixedDirection(world, 1, 0f, -1f);

        return !world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsOutside;
    }

    // Same "reveals the target without touching it" contract as
    // World_WeldHealthyWall_RevealsHpWithoutChangingIt, but for a station wall - a station has
    // nothing of the player's own ship to actually weld (World.Welding.cs's own comment explains
    // why), so unlike the ship's own walls this one must NEVER move off full health no matter how
    // long the torch stays lit, not just "not yet damaged".
    private static bool World_StationWall_WeldRevealsHpWithoutChangingIt()
    {
        var world = new World();
        world.SpawnCharacter(1); // starts already docked at home-station

        var weldingToolSlot = TakeFromRack(world, ItemType.WeldingTool);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: weldingToolSlot));
        TakeTankFromRack(world, ItemType.WeldingTank);
        AttachTankTo(world, Array.IndexOf(
            world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), ItemType.WeldingTool),
            ItemType.WeldingTank);

        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 23f, 3f);
        WalkFixedDirection(world, 1, 1f, 0f);

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (!me.OnStation)
            return false; // didn't make it onto the station

        var room = world.CreateSnapshot().Station.Rooms.First(r => r.Contains(new Vec2(me.X, me.Y)));
        // Bug fix follow-up (humble-soaring-cat.md, docked-movement tile collision) - excludes a
        // block sitting at one of the room's own corners: reaching it now means clearing TWO
        // perpendicular walls at once (both real, one-unit-thick tiles now, not the old zero-
        // thickness lines), not just the one this block's own edge is on - the plain single-axis
        // "in from the wall" stand position below only ever clears the one it's aiming at.
        var block = world.CreateSnapshot().Station.WallBlocks.First(b => b.RoomId == room.Id &&
            (b.Y == room.Top || b.Y == room.Bottom ? b.X > room.Left + 0.6f && b.X < room.Right - 0.6f
                : b.Y > room.Top + 0.6f && b.Y < room.Bottom - 0.6f));

        // Stand 1.5 in from whichever edge this block sits on (was 0.5 under the old zero-thickness
        // wall model) and aim straight at it - the same "walk up to the known block, look the one
        // way that hits it" shape every ship wall test above uses, just derived from the room's own
        // bounds instead of a hand-picked literal (a station's room coordinates shift with wherever
        // the ship's own airlock door happens to be, unlike the ship's own fixed layout). 1.5 rather
        // than 0.5 because the wall this block sits on is now a genuine one-unit-thick tile
        // (TileGridRasterizer), one tile further in than the old model's zero-width boundary line.
        var (standX, standY, aimX, aimY) =
            block.Y == room.Top ? (block.X, block.Y + 1.5f, 0f, -1f) :
            block.Y == room.Bottom ? (block.X, block.Y - 1.5f, 0f, 1f) :
            block.X == room.Left ? (block.X + 1.5f, block.Y, -1f, 0f) :
            (block.X - 1.5f, block.Y, 1f, 0f);
        MoveCharacterTo(world, 1, standX, standY);

        for (var i = 0; i < 60; i++) // 2 seconds - long enough real damage/repair would show if it existed
        {
            world.ApplyCommand(1, new ClientCommand(1, WeldHeld: true, LookX: aimX, LookY: aimY));
            world.Step(RealtimeStep);
        }

        var snapshot = world.CreateSnapshot();
        var meNow = snapshot.Characters.Single(c => c.PlayerId == 1);
        if (meNow.WallToolTargetBlockId != block.Id)
            return false; // should reveal exactly this station wall block

        var state = snapshot.Station.WallBlockStates.First(s => s.Id == block.Id);
        return state.Hp >= state.MaxHp; // untouched - stations are never actually breachable/repairable
    }

    // Two adjacent fully-broken blocks (the corridor's top wall tiles in exact 1-unit steps, so
    // 11.5 and 12.5 sit right next to each other) are wide enough to fit through - works exactly
    // like walking out an open airlock (World.Eva.cs's IsPassableBreach).
    // M73 (humble-soaring-cat.md) turned a wall from the old model's zero-thickness boundary line
    // into a genuine full 1-unit-thick tile, so the row directly under the top wall is only walkable
    // where a block has already been breached - this test's original "walk right up against the wall
    // and slide along it from one block to the next" approach gets physically stopped at the column
    // boundary of the still-solid second block. The fix (matching the interior-bulkhead case in
    // World_Eva_PassableInteriorBreach_WalksIntoAdjacentRoomNotVacuum) is to cut each block from
    // safely inside the room instead, never touching the wall row until the final walk-out.
    //
    // That alone still isn't enough for the SECOND block, though (confirmed live via a temporary
    // FindAimedCutTarget diagnostic): corridor's right edge (X=13) carries its own interior bulkhead
    // to "quarters" with a wall segment at (13, 1.5), and FindAimedCutTarget returns the FIRST sample
    // point that lands within WallCutPointRadius (0.85) of ANY wall block - a straight-up aim from
    // right under the second block (X~12.5) puts an early sample point close enough to that unrelated
    // bulkhead segment (distance ~0.66-0.98 depending on exact Y) to latch onto it instead, burning
    // the whole cut budget on a wall that was never the target. Standing at X=12.0 instead keeps every
    // sample point comfortably clear of the bulkhead (never closer than 1.0 in X alone) while still
    // catching the second block by its own third sample (~0.71 away, safely under the 0.85 radius).
    private static bool World_Eva_TwoAdjacentBrokenBlocks_ArePassable()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor
        EnterAsteroidFieldStationary(world);
        EquipCutterWithTank(world);

        WalkAcrossShipTo(world, 11.5f, 0.5f); // clamps to ~Y=1.35 - the wall row itself isn't walkable yet
        if (CutWallBlockAt(world, 11.5f, 0f, new Vec2(0, -1), 4 * 30) >= 4 * 30)
            return false;
        // Not directly under the second block (12.5) - that's exactly equidistant from the FIRST
        // block too (11.5, already breached but still a live FindAimedCutTarget candidate) and close
        // enough to the corridor/quarters interior bulkhead at (13, 1.5) for a straight-up aim's own
        // early samples to latch onto one of those instead. A diagonal aim from off to the side
        // avoids all three: every early sample stays clearly outside every wrong target's own
        // cutting radius, only converging onto the second block itself partway along the ray.
        MoveCharacterTo(world, 1, 11.9f, 1.35f);
        if (CutWallBlockAt(world, 12.5f, 0f, new Vec2(0.6, -1.35).Normalized(), 4 * 30) >= 4 * 30)
            return false;

        EquipSuit(world, 1);
        WalkAcrossShipTo(world, 11.5f, 0.5f);
        WalkFixedDirection(world, 1, 0f, -1f);

        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsOutside;
    }

    // The cockpit/reactor bulkhead (Ship.GenerateInteriorWallBlocks) shares its full Y0-6 boundary
    // at X=5, but door-cockpit-reactor only covers Y2-4 - leaving Y0-2 as genuine IsInterior wall,
    // with no door anywhere nearby. Breaking two adjacent blocks there (0.5 and 1.5) is the ONLY
    // way across at that height, so a walk-through proves RoomLayout.MoveAlongAxis's new breach
    // crossing actually works, not just that a door happened to also be in reach.
    private static bool World_Eva_PassableInteriorBreach_WalksIntoAdjacentRoomNotVacuum()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor
        EquipCutterWithTank(world);

        // Y=1.0, not 0.5: standing exactly level with the first block put it within the cutter's
        // own pointRadius of cockpit's UNRELATED top hull block at (4.5,0) too, which won a
        // same-tick tie against the intended target often enough to stall it at 0 damage forever -
        // Y=1.0 clears both target blocks (0.5 away from each) while staying well clear (1.0 away)
        // of that top wall.
        // Bug fix follow-up (humble-soaring-cat.md, docked-movement tile collision) - Y=1.0 itself
        // is no longer reachable (cockpit's own top wall, row 0, is a real tile now - clearance
        // stops at 1+CharacterRadius); WalkAcrossShipTo actually lands at ~1.35. That leaves this
        // spot only 0.35 from the SECOND block (row 1, y=1.5) but 0.85 from the intended FIRST one
        // (row 0, y=0.5) - a straight horizontal aim used to land almost exactly between both blocks
        // (both ~0.5 away under the old zero-thickness model) but now favors the wrong one, cutting
        // block 2 by accident and leaving block 1 untouched. Aiming up-and-over instead (same
        // diagonal-aim fix the second cut in World_Eva_TwoAdjacentBrokenBlocks_ArePassable already
        // needed for its own equivalent case) keeps every early sample closer to block 1 than to
        // block 2 or the top wall.
        WalkAcrossShipTo(world, 4.5f, 1.0f); // cockpit, right up against the interior bulkhead
        if (CutWallBlockAt(world, 5f, 0.5f, new Vec2(0.6, -1f).Normalized(), 4 * 30) >= 4 * 30)
            return false;
        MoveCharacterTo(world, 1, 4.5f, 1.5f); // same room, no door to cross
        if (CutWallBlockAt(world, 5f, 1.5f, new Vec2(1, 0), 4 * 30) >= 4 * 30)
            return false;

        WalkFixedDirection(world, 1, 1f, 0f); // straight at the two-block gap just opened

        var me = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        var room = world.CreateSnapshot().Rooms.FirstOrDefault(r => r.Contains(new Vec2(me.X, me.Y)));
        return room?.Id == "reactor" && !me.IsOutside;
    }
}
