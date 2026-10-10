using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// What a destroyed compartment does (World.ShipBlasts.cs): the explosion, the damage it deals around it, the
// chain it can start, the reactor taking the whole ship, and the pieces that break away and tumble.
internal static partial class TestRunner
{
    // A hull with a device-free dead-end room built flush against it, then a second (camera) room flush against
    // THAT - the second connects to the reactor only through the first, so losing the first cuts it loose.
    private static (Room Inner, Room Outer)? BuildChainedRooms(World world)
    {
        var innerEntry = world.GetBuildableRoomCatalog().First(e => e.Id == "empty-small");
        var hullAnchor = world.Ship.Rooms[0];
        var innerX = hullAnchor.X;
        var innerY = hullAnchor.Y + hullAnchor.Height;
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(innerEntry.Id, innerX, innerY)));
        CompletePendingRoomBuilds(world);
        var inner = world.Ship.Rooms.FirstOrDefault(r => r.X == innerX && r.Y == innerY);
        if (inner is null)
            return null;

        var outerEntry = world.GetBuildableRoomCatalog().First(e => e.Id == "camera");
        var outerX = innerX;
        var outerY = innerY + inner.Height;
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(outerEntry.Id, outerX, outerY)));
        CompletePendingRoomBuilds(world);
        var outer = world.Ship.Rooms.FirstOrDefault(r => r.X == outerX && r.Y == outerY);
        return outer is null ? null : (inner, outer);
    }

    // Breaks walls of a room one by one until it is one short of the destruction threshold.
    private static void DamageRoomJustShortOfDestruction(World world, string roomId)
    {
        var total = world.Ship.WallBlocks.Where(b => b.RoomId == roomId).Sum(b => World.WallBlockMaxHp)
            + 2f * World.EnginePartMaxHp * world.Ship.Engines.Count(e => e.RoomId == roomId);
        var threshold = total * World.RoomDestructionWallShare;
        var blocks = world.Ship.WallBlocks.Where(b => b.RoomId == roomId).ToList();
        var lost = 0f;
        foreach (var block in blocks)
        {
            if (lost + World.WallBlockMaxHp >= threshold)
                break;
            world.DebugBreachWallBlockById(block.Id);
            lost += World.WallBlockMaxHp;
        }
    }

    // The explosion shows up in the snapshot, hurts the neighbour's walls and the people near it, and does not
    // kill someone standing a few units away.
    private static bool World_ShipBlasts_DestroyedCompartmentExplodes_HurtsNeighbourWallsAndCrew()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 7f, 2f); // in "b", right beside the dead end "c"
        var bBefore = world.CreateSnapshot().RoomHp!.Single(s => s.RoomId == "b").Hp;

        world.DebugDestroyRoomWallBlocks("c");
        world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        var blast = snapshot.ShipBlasts?.FirstOrDefault(b => b.Kind == ShipBlastKind.Compartment);
        var me = snapshot.Characters.Single(c => c.PlayerId == 1);
        var bAfter = snapshot.RoomHp!.Single(s => s.RoomId == "b").Hp;
        return blast is not null && Math.Abs(blast.X - 10f) < 0.6f && Math.Abs(blast.Y - 2f) < 0.6f
            && world.Ship.Rooms.All(r => r.Id != "c")
            && bAfter < bBefore                       // its walls next to the blast were dented
            && me.Health > 0f && me.Health < Character.MaxHealth; // hurt, not killed
    }

    // A neighbour that was already close to the edge is finished by the blast, and goes up a moment later - a chain,
    // not one instant flash.
    private static bool World_ShipBlasts_ABlastCanFinishAWeakenedNeighbour_AChainReaction()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        DamageRoomJustShortOfDestruction(world, "b");
        if (world.Ship.Rooms.All(r => r.Id != "b"))
            return false; // setup problem - b should still be standing, just barely

        world.DebugDestroyRoomWallBlocks("c");
        world.Step(RealtimeStep);
        var bStillThereRightAway = world.Ship.Rooms.Any(r => r.Id == "b"); // the chain is staggered, not instant

        for (var i = 0; i < 30; i++) // one second - well past the chain delay
            world.Step(RealtimeStep);

        var compartmentBlasts = world.CreateSnapshot().ShipBlasts?.Count(b => b.Kind == ShipBlastKind.Compartment) ?? 0;
        return bStillThereRightAway && world.Ship.Rooms.All(r => r.Id != "b") && compartmentBlasts >= 2
            && world.Ship.Rooms.Any(r => r.Id == "a") && !world.IsShipExploding; // the reactor room survived the second blast
    }

    // The reactor going takes the whole ship: every room blows in turn, the crew dies with them, and after the
    // show the ship is back at the last dock, repaired, with the crew alive - the checkpoint the autosave uses.
    private static bool World_ShipBlasts_ReactorRoomDestroyed_BlowsUpEverythingThenRestoresTheShip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var roomsBefore = world.Ship.Rooms.Count;

        world.DebugDestroyRoomWallBlocks("reactor");
        world.Step(RealtimeStep);
        if (!world.IsShipExploding || world.CreateSnapshot().ShipBlasts?.Any(b => b.Kind == ShipBlastKind.Reactor) != true)
            return false;

        for (var i = 0; i < 75; i++) // 2.5 seconds in: every room has gone up (each blast stays visible 2.6 s) and the crew with it
            world.Step(RealtimeStep);
        var midway = world.CreateSnapshot();
        var everyRoomBlew = (midway.ShipBlasts?.Count(b => b.Kind == ShipBlastKind.Compartment) ?? 0) >= roomsBefore;
        var crewDied = midway.Characters.Single(c => c.PlayerId == 1).Health <= 0f;
        if (!everyRoomBlew || !crewDied)
            return false;

        for (var i = 0; i < 75; i++) // and the rest of the sequence (it all lasts 4.5 s)
            world.Step(RealtimeStep);
        var after = world.CreateSnapshot();
        var me = after.Characters.Single(c => c.PlayerId == 1);
        return !world.IsShipExploding && world.Ship.Rooms.Count == roomsBefore && world.IsDocked
            && me.Health == Character.MaxHealth && !me.IsOutside;
    }

    // Losing a compartment that held a device used to take a full reset that healed every OTHER wall on the ship.
    // Now the damage of the survivors is kept, and the crew standing in them is not thrown back to the cockpit.
    private static bool World_ShipBlasts_LosingACompartmentWithADevice_KeepsTheRestOfTheHullsDamage()
    {
        var world = new World(ShipKind.Custom, BuildThreeRoomEngineCustomShipDefinition());
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 1.5f, 3f); // in "a"
        var block = world.Ship.WallBlocks.Where(b => b.RoomId == "a").OrderBy(b => b.Position.X).First(); // the west wall, far from "b"
        world.DebugDamageWallBlockById(block.Id, 40f);
        var positionBefore = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);

        world.DebugDestroyRoomWallBlocks("b"); // "b" holds the engine: a device-graph change
        world.Step(RealtimeStep);

        var snapshot = world.CreateSnapshot();
        var stillHurt = snapshot.WallBlockStates.Single(s => s.Id == block.Id).Hp <= 60f + 0.01f;
        var me = snapshot.Characters.Single(c => c.PlayerId == 1);
        var staysPut = Math.Abs(me.X - positionBefore.X) < 0.5f && Math.Abs(me.Y - positionBefore.Y) < 0.5f;
        return stillHurt && staysPut && !me.IsOutside;
    }

    // A piece torn off by the explosion is shoved away from the blast and tumbles - it does not just glide along
    // rigidly with the ship's own velocity the way it used to.
    private static bool World_ShipBlasts_TornOffPieceIsShovedAndTumbles()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        if (BuildChainedRooms(world) is not { } rooms)
            return false;

        var shipVelocity = new Vec2(37, -19);
        world.DebugSetShipVelocity(shipVelocity);
        world.DebugDestroyRoomWallBlocks(rooms.Inner.Id);
        world.Step(RealtimeStep);

        var fragment = world.CreateSnapshot().ShipDebris?.FirstOrDefault();
        if (fragment is null)
            return false; // setup problem - the outer room did not break away
        var start = new Vec2(fragment.X, fragment.Y);
        var startRotation = fragment.RotationDegrees;

        const int steps = 30;
        for (var i = 0; i < steps; i++)
            world.Step(RealtimeStep);
        var later = world.CreateSnapshot().ShipDebris!.First(f => f.Id == fragment.Id);
        var seconds = RealtimeStep * steps;

        var drift = (new Vec2(later.X, later.Y) - start) * (1.0 / seconds) - shipVelocity; // what is left after the ship's own motion
        var spin = Math.Abs(later.RotationDegrees - startRotation) / seconds;
        return drift.Length() > 1.5 && drift.Length() < 3.0   // shoved by about the blast's kick
            && spin > 6.0 && spin < 28.0;                      // and turning at a plausible rate
    }

    // The player's guns can reach a torn-off piece: a shot through it hurts it, a miss does nothing, and a piece
    // shot to nothing blows up where it floats (a blast in field space).
    private static bool World_ShipBlasts_TornOffPieceCanBeShotToPieces()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        if (BuildChainedRooms(world) is not { } rooms)
            return false;
        world.DebugDestroyRoomWallBlocks(rooms.Inner.Id);
        world.Step(RealtimeStep);

        var fragment = world.CreateSnapshot().ShipDebris?.FirstOrDefault();
        if (fragment is null)
            return false;
        var centre = new Vec2(fragment.X, fragment.Y);

        var miss = world.DebugShootSegmentAtDebris(centre + new Vec2(-60, 40), centre + new Vec2(-50, 40), 10f);
        var hit = world.DebugShootSegmentAtDebris(centre + new Vec2(-1, 0), centre + new Vec2(1, 0), 10f);
        if (miss || !hit)
            return false;

        for (var i = 0; i < 40; i++) // far more than it can take
            world.DebugDamageDebris(fragment.Id, 10f);
        world.Step(RealtimeStep);

        var after = world.CreateSnapshot();
        return after.ShipDebris?.All(f => f.Id != fragment.Id) == true
            && after.ShipBlasts?.Any(b => b.InField) == true;
    }
}
