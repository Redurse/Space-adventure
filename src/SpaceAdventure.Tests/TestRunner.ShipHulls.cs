using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    private static bool Ship_Corvette_HasSideGunsTwoPortsAndSameWireDeviceIds()
    {
        var ship = Ship.Create(ShipKind.Corvette);
        if (ship.Rooms.Count != 5 || ship.AirlockOuterDoors.Count != 2)
            return false;
        // Suits are stored at the ways out, one locker per port, not somewhere across the ship.
        if (!ship.AirlockOuterDoors.All(d => ship.SuitLockers.Any(l => l.RoomId == d.RoomId)))
            return false;
        if (!ship.SystemDevices.Select(d => d.Id).OrderBy(x => x).SequenceEqual(ExpectedSystemDeviceIds.OrderBy(x => x)))
            return false;

        // The broadside: both barrels leave the gun deck's own walls, pointing opposite ways.
        var armory = ship.GetRoom("armory");
        var starboard = TurretMount.For(ship.Rooms, ship.Turrets, ship.Turrets.First(t => t.Id == "turret-starboard"));
        var port = TurretMount.For(ship.Rooms, ship.Turrets, ship.Turrets.First(t => t.Id == "turret-port"));

        return starboard.Position.X > armory.Right && starboard.OutwardDegrees == 0f
            && port.Position.X < armory.Left && port.OutwardDegrees == 180f;
    }

    // The hull runs bow-to-stern down the screen, so its spine doors are horizontal slots - the
    // first ones in the game. Walking the whole ship proves RoomLayout crosses them as happily as
    // the vertical ones every other class uses.
    // A hull laid out down the screen has to lead with its nose. Rotation aligns the ship's own
    // forward axis with its velocity, so a Corvette flying +X ends up rotated +90: its bow (local
    // -Y) is what's pointing along the course, not its flank.
    private static bool World_ShipField_CorvetteFliesNoseFirst()
    {
        var world = new World(ShipKind.Corvette);
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);

        world.ApplyCommand(1, new ClientCommand(1, PowerSystemIndex: 1, PowerDirection: 1f)); // Engine
        StepFor(world, 60);

        MoveCharacterTo(world, 1, 6.75f, 0.9f); // helm console
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        // 2s of full burn, not the full 8s this used to run for: the asteroid belt around
        // "asteroid-field-epsilon" now genuinely orbits (M58 follow-up - World.Tick actually
        // advancing means asteroid positions aren't frozen at their spawn instant any more), and a
        // rock happens to drift into this straight-line course by ~second 3, trapping the ship in a
        // repeated hull-collision stall well before the check below ever runs. The dot product this
        // test cares about already converges to ~0.9999 by well under 2s (verified via a scratch
        // trace), so a shorter, obstacle-free burn checks the exact same thing without depending on
        // where a real, moving asteroid happens to be by second 8.
        for (var i = 0; i < 2 * 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, HelmThrottle: 1f));
            world.Step(RealtimeStep);
        }

        var field = world.CreateSnapshot().ShipField;
        var forward = TurretMount.FromDegrees(field.RotationDegrees + world.Ship.ForwardDegrees);
        var course = new Vec2(field.VelocityX, field.VelocityY).Normalized();

        // The nose and the course agree to within a few degrees (dot product ~1).
        return forward.X * course.X + forward.Y * course.Y > 0.99f;
    }

    // Going EVA used to be keyed to a room literally named "airlock-chamber", so a hull that puts
    // its ports in ordinary compartments (one on each beam here) locked its crew inside for good.
    private static bool World_Eva_CorvetteCrewGoesOutThroughABeamPort()
    {
        var world = new World(ShipKind.Corvette);
        world.SpawnCharacter(1);
        EnterAsteroidFieldStationary(world);

        MoveCharacterTo(world, 1, 6.75f, 11f);  // down the spine into the reactor hall
        MoveCharacterTo(world, 1, 12.3f, 11f);  // across through the door, still on its row
        MoveCharacterTo(world, 1, 12.3f, 8.5f); // up to the starboard bay's suit locker
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true)); // suit up
        StepFor(world, 90);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).WearingSuit)
            return false;

        // Tanks live in the starter rack stock now (World.Storage.cs's InitializeRackSlots) - a
        // suit with an empty socket won't get anyone through the port, so grab one before crossing
        // back to the starboard bay's airlock. Walked here by hand rather than through
        // TakeTankFromRack/WalkAcrossShipTo: that helper's "doorRow=3" shortcut assumes every door
        // sits at the same height, true on the row-laid-out hulls it was written for but not on the
        // Corvette's own spine-and-bays layout, where it would just walk into a wall.
        var rackSlots = world.CreateSnapshot().RackSlots;
        var rackSlotIndex = rackSlots.ToList().IndexOf(ItemType.OxygenTank);
        var rack = world.Ship.StorageRacks[rackSlotIndex / StorageRack.Capacity];
        MoveCharacterTo(world, 1, 12.3f, 11f); // back down to the door row
        MoveCharacterTo(world, 1, 9.5f, 11f);  // through the door into the reactor hall
        MoveCharacterTo(world, 1, rack.X, 11f); // along the hall to the rack's own column
        MoveCharacterTo(world, 1, rack.X, rack.Y); // up to the rack itself
        var freeMainSlot = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToList().IndexOf(null);
        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Rack, rackSlotIndex), MoveItemTo: new SlotRef(ItemSlotKind.Main, freeMainSlot)));
        AttachTankTo(world, WornSuitSlotIndex);

        MoveCharacterTo(world, 1, 12.3f, 11f); // back down to the door row
        MoveCharacterTo(world, 1, 9.5f, 11f);  // through the door, back into the reactor hall
        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: "door-airlock-vacuum"));
        MoveCharacterTo(world, 1, 9f, 11f); // reactor side, lined up with the door to life-support
        MoveCharacterTo(world, 1, 12.5f, 9.5f); // through the door, line up with the port
        WalkFixedDirection(world, 1, 1f, 0f);

        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsOutside;
    }

    private static bool Ship_Corvette_CrewWalksTheSpineAndOutToBothBays()
    {
        var world = new World(ShipKind.Corvette);
        world.SpawnCharacter(1);

        MoveCharacterTo(world, 1, 6.75f, 11f); // down the spine, cockpit -> gun deck -> reactor hall
        var inReactorHall = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        if (inReactorHall.Y < 8f)
            return false; // never made it through the horizontal doors

        MoveCharacterTo(world, 1, 2f, 11f); // out to the shield bay
        var toPort = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

        MoveCharacterTo(world, 1, 6.75f, 11f);
        MoveCharacterTo(world, 1, 11.5f, 11f); // and across to life support
        var toStarboard = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

        return toPort.X < 4f && toStarboard.X > 9.5f;
    }

    private static bool Ship_Scout_HasAirlockChamberAndSameWireDeviceIds()
    {
        var ship = Ship.CreateScout();
        return ship.Rooms.Any(r => r.Id == "airlock-chamber") &&
               ship.AirlockOuterDoors.Count == 1 &&
               ship.SystemDevices.Select(d => d.Id).OrderBy(x => x).SequenceEqual(ExpectedSystemDeviceIds.OrderBy(x => x));
    }

    private static bool Ship_Cruiser_HasAirlockChamberAndThreeTurrets()
    {
        var ship = Ship.CreateCruiser();
        return ship.Rooms.Any(r => r.Id == "airlock-chamber") &&
               ship.Turrets.Count == 3 &&
               ship.SystemDevices.Select(d => d.Id).OrderBy(x => x).SequenceEqual(ExpectedSystemDeviceIds.OrderBy(x => x));
    }

    private static bool World_ShipKindScout_SpawnsAndSteps()
    {
        var world = new World(ShipKind.Scout);
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);

        var character = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return character.X > world.Ship.SpawnPoint.X;
    }

    private static bool World_ShipKindCruiser_SpawnsAndSteps()
    {
        var world = new World(ShipKind.Cruiser);
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, MoveX: 1, MoveY: 0));
        for (var i = 0; i < 30; i++)
            world.Step(RealtimeStep);

        var character = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return character.X > world.Ship.SpawnPoint.X;
    }

    private static bool RoomLayout_MoveAlongAxis_BlocksAtWallWithoutDoor()
    {
        var station = Station.CreateDefault();
        var dockRoomId = station.DockRoomId;
        // Reads the dock room's real bounds rather than a hardcoded corner (M49 - the dock room no
        // longer sits at a fixed (0,0) origin, its size/position come from the procedural
        // generator) - the top wall is guaranteed door-free regardless: the dock room's only two
        // ring neighbours are to its right and below it (Station.Procedural.cs's own doc comment on
        // why Dock always lands on the perimeter's top-left corner).
        var room = station.GetRoom(dockRoomId);
        // room.Top+1f (not +2f) used to be clear of the wall, back when a wall was a zero-width line
        // sitting exactly on the room's own Top edge; now the room's own Top row is itself a real,
        // solid wall tile (TileGridRasterizer.FromRooms - every room's own leading Left/Top edge is
        // walled unconditionally), so the start position has to sit a full tile further in to avoid
        // starting inside that wall tile.
        var start = new Vec2(room.Center.X, room.Top + 2f);
        var (pos, roomId) = station.MoveAlongAxis(start, dockRoomId, new Vec2(0, -1f), _ => true);
        // Clamped CharacterRadius short of the top hull wall's face - one tile deeper into the room
        // than the old zero-width-wall model, since the wall now consumes the room's own Top row.
        return roomId == dockRoomId && Math.Abs(pos.Y - (room.Top + 1f + RoomLayout.CharacterRadius)) < 0.01f;
    }

}
