using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // A world with player 1 holding a rifle (optionally with a magazine plugged in), standing in the
    // spawn room looking down +X.
    private static (World World, int RifleSlot) RifleWorld(bool withMagazine)
    {
        var world = new World();
        world.SpawnCharacter(1);
        var slot = TakeFromRack(world, ItemType.Rifle);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));
        if (withMagazine)
        {
            TakeTankFromRack(world, ItemType.Magazine);
            AttachTankTo(world, slot, ItemType.Magazine);
        }
        return (world, slot);
    }

    private static float? MagazineRounds(World world, int rifleSlot) =>
        world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlotTanks[rifleSlot];

    // Steps `ticks` ticks holding the trigger the given way, returning every distinct bullet id seen in flight.
    private static HashSet<string> FireAndCollectBullets(World world, int ticks, bool held, bool pressedOnlyFirstTick = false)
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < ticks; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: !held && (!pressedOnlyFirstTick || i == 0),
                WeaponFireHeld: held, LookX: 1f, LookY: 0f));
            world.Step(RealtimeStep);
            foreach (var shot in world.CreateSnapshot().PersonalShots)
                seen.Add(shot.Id);
        }
        return seen;
    }

    private static bool World_Rifle_MagazineSocketsLikeATankAndStartsFull()
    {
        var (world, slot) = RifleWorld(withMagazine: true);
        return MagazineRounds(world, slot) == MagazineDefinitions.FullCharge;
    }

    private static bool World_Rifle_OneBurstReleasesSixBulletsAndWearsSixRoundsOffTheMagazine()
    {
        var (world, slot) = RifleWorld(withMagazine: true);
        var bullets = FireAndCollectBullets(world, 40, held: false, pressedOnlyFirstTick: true);
        return bullets.Count == WeaponDefinitions.BurstRounds
            && MagazineRounds(world, slot) == MagazineDefinitions.FullCharge - WeaponDefinitions.BurstRounds;
    }

    private static bool World_Rifle_BurstBulletsComeOutOneAfterAnotherNotAllAtOnce()
    {
        var (world, _) = RifleWorld(withMagazine: true);
        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true, LookX: 1f, LookY: 0f));
        world.Step(RealtimeStep);
        var afterFirstTick = world.CreateSnapshot().PersonalShots.Count;
        return afterFirstTick >= 1 && afterFirstTick < WeaponDefinitions.BurstRounds;
    }

    private static bool World_Rifle_WithoutAMagazineCannotFire()
    {
        var (world, slot) = RifleWorld(withMagazine: false);
        var bullets = FireAndCollectBullets(world, 60, held: true);
        return bullets.Count == 0;
    }

    private static bool World_Rifle_HoldingTheTriggerEmptiesTheMagazineThenStops()
    {
        var (world, slot) = RifleWorld(withMagazine: true);
        var bullets = FireAndCollectBullets(world, 10 * 30, held: true); // ten seconds of trigger
        return bullets.Count == (int)MagazineDefinitions.FullCharge && MagazineRounds(world, slot) == 0f;
    }

    private static bool World_Rifle_FiresFromRightMouseHeldAloneNoSpaceNeeded()
    {
        var (world, _) = RifleWorld(withMagazine: true);
        return FireAndCollectBullets(world, 30, held: true).Count > 0;
    }

    private static bool World_Rifle_FreshMagazineAfterRunningDryCanFireAgain()
    {
        var (world, slot) = RifleWorld(withMagazine: true);
        FireAndCollectBullets(world, 10 * 30, held: true);
        if (MagazineRounds(world, slot) != 0f)
            return false;

        // Swap in a fresh one: take the empty one out, plug a full one in.
        world.ApplyCommand(1, new ClientCommand(1, DetachTankSlot: slot));
        TakeTankFromRack(world, ItemType.Magazine);
        var slots = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        var fresh = Enumerable.Range(0, slots.MainSlots.Count).First(i => slots.MainSlots[i] == ItemType.Magazine && (slots.MainSlotTanks[i] is null || slots.MainSlotTanks[i] == MagazineDefinitions.FullCharge)); // a magazine fresh off the rack carries no charge value yet - it plugs in full
        world.ApplyCommand(1, new ClientCommand(1, AttachTankFromSlot: fresh, AttachTankToSlot: slot));
        return MagazineRounds(world, slot) == MagazineDefinitions.FullCharge && FireAndCollectBullets(world, 30, held: true).Count > 0;
    }

    private static (World World, int Slot) LaserWorld()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var slot = TakeFromRack(world, ItemType.LaserRifle);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));
        return (world, slot);
    }

    private static bool World_Laser_FiresABeamWithoutAMagazine()
    {
        var (world, _) = LaserWorld();
        world.ApplyCommand(1, new ClientCommand(1, WeaponFireHeld: true, LookX: 1f, LookY: 0f));
        world.Step(RealtimeStep);
        return world.CreateSnapshot().LaserBeams is { Count: > 0 };
    }

    private static bool World_Laser_BeamEndsAtTheWallOfTheRoomItWasFiredIn()
    {
        var (world, _) = LaserWorld();
        var snapshot = world.CreateSnapshot();
        var me = snapshot.Characters.Single(c => c.PlayerId == 1);
        var room = snapshot.Rooms.First(r => r.Contains(new Vec2(me.X, me.Y)));

        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true, LookX: 1f, LookY: 0f));
        world.Step(RealtimeStep);
        var beam = world.CreateSnapshot().LaserBeams!.Single();

        var startsAtMuzzle = Math.Abs(beam.StartX - me.X) < 0.3 && Math.Abs(beam.StartY - me.Y) < 0.3;
        var runsRight = beam.EndX > beam.StartX + 0.5f;
        var stopsAtTheWall = beam.EndX <= room.Right + 0.01f && beam.EndX >= room.Right - 0.5f; // within one step of the bulkhead
        var stayedOnTheLine = Math.Abs(beam.EndY - beam.StartY) < 0.01f;
        return startsAtMuzzle && runsRight && stopsAtTheWall && stayedOnTheLine && beam.Life > 0.5f;
    }

    private static bool World_Laser_BeamFadesAndDisappears()
    {
        var (world, _) = LaserWorld();
        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true, LookX: 1f, LookY: 0f));
        world.Step(RealtimeStep);
        if (world.CreateSnapshot().LaserBeams!.Count != 1)
            return false;
        for (var i = 0; i < 15; i++) // half a second, past the 0.2 s life
            world.Step(RealtimeStep);
        return world.CreateSnapshot().LaserBeams!.Count == 0;
    }

    private static bool World_Laser_BeamFollowsTheAimDirection()
    {
        var (world, _) = LaserWorld();
        world.ApplyCommand(1, new ClientCommand(1, FirePressed: true, LookX: 0f, LookY: 1f));
        world.Step(RealtimeStep);
        var beam = world.CreateSnapshot().LaserBeams!.Single();
        return beam.EndY > beam.StartY + 0.5f && Math.Abs(beam.EndX - beam.StartX) < 0.01f;
    }
}
