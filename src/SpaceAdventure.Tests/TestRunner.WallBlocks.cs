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
            world.ApplyCommand(1, new ClientCommand(1, CutHeld: true, LookX: aim.X, LookY: aim.Y));
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
    // crossing during VoyagePhase.Station - it leads onto the station's own walkway there, not
    // vacuum), so this has to actually be out in the field first for the breach check itself to be
    // what's on trial.
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

    // Two adjacent fully-broken blocks (the corridor's top wall tiles in exact 1-unit steps, so
    // 11.5 and 12.5 sit right next to each other) are wide enough to fit through - works exactly
    // like walking out an open airlock (World.Eva.cs's IsPassableBreach).
    private static bool World_Eva_TwoAdjacentBrokenBlocks_ArePassable()
    {
        var world = new World();
        world.SpawnCharacter(1); // corridor
        EnterAsteroidFieldStationary(world);
        EquipCutterWithTank(world);

        WalkAcrossShipTo(world, 11.5f, 0.5f);
        if (CutWallBlockAt(world, 11.5f, 0f, new Vec2(0, -1), 4 * 30) >= 4 * 30)
            return false;
        MoveCharacterTo(world, 1, 12.5f, 0.5f); // same room, no door to cross
        if (CutWallBlockAt(world, 12.5f, 0f, new Vec2(0, -1), 4 * 30) >= 4 * 30)
            return false;

        EquipSuit(world, 1);
        WalkAcrossShipTo(world, 11.5f, 0.5f);
        WalkFixedDirection(world, 1, 0f, -1f);

        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsOutside;
    }
}
