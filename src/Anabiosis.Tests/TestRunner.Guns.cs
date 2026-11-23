using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// The firearms after Barotrauma (GunDefinitions.cs): its numbers for every gun, a round per shot at the pace of
// the gun, pellets, loose rounds and magazines, the wound a round leaves.
internal static partial class TestRunner
{
    private static int SlotHolding(World world, ItemType item) =>
        Array.IndexOf(world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!.MainSlots.ToArray(), item);

    // Player 1 with this gun in hand, loaded from one item of its ammunition unless told otherwise.
    private static (World World, int Slot) GunWorld(ItemType gun, bool loaded = true)
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugGiveItem(1, gun);
        var slot = SlotHolding(world, gun);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));
        if (loaded)
            LoadGun(world, gun, slot);
        return (world, slot);
    }

    private static void LoadGun(World world, ItemType gun, int slot)
    {
        var ammo = GunDefinitions.Of(gun)!.Ammo;
        world.DebugGiveItem(1, ammo);
        world.ApplyCommand(1, new ClientCommand(1, AttachTankFromSlot: SlotHolding(world, ammo), AttachTankToSlot: slot));
    }

    private static bool Guns_EveryGunIsAWeaponWithItsOwnAmmoNameAndPrice()
    {
        foreach (var gun in GunDefinitions.Guns)
        {
            var ok = WeaponDefinitions.IsWeapon(gun.Weapon) && WeaponDefinitions.IsFirearm(gun.Weapon)
                && TankSockets.AcceptedTank(gun.Weapon) == gun.Ammo && TankSockets.IsTank(gun.Ammo)
                && TankSockets.CapacityOf(gun.Weapon) == gun.Capacity
                && ItemDefinitions.HandsRequired(gun.Weapon) == gun.Hands && ItemDefinitions.HandsRequired(gun.Ammo) == 1
                && ItemDefinitions.DisplayName(gun.Weapon) != gun.Weapon.ToString()
                && ItemDefinitions.DisplayName(gun.Ammo) != gun.Ammo.ToString()
                && ItemDefinitions.ShortLabel(gun.Weapon) != "?" && ItemDefinitions.ShortLabel(gun.Ammo) != "?"
                && TradeCatalog.Goods.Any(g => g.Item == gun.Weapon && TradeCatalog.IsForSale(g))
                && TradeCatalog.Goods.Any(g => g.Item == gun.Ammo && TradeCatalog.IsForSale(g));
            if (!ok)
                return false;
        }
        return GunDefinitions.Guns.Count == 10;
    }

    // The numbers Barotrauma gives each gun and its round.
    private static bool Guns_CarryTheBarotraumaNumbers()
    {
        static bool Is(ItemType gun, float reload, float wound, float bleeding, float stun, int capacity, int pellets = 1)
        {
            var g = GunDefinitions.Of(gun)!;
            return Math.Abs(g.ReloadSeconds - reload) < 0.0001f && Math.Abs(g.HitWound - wound) < 0.0001f
                && Math.Abs(g.HitBleeding - bleeding) < 0.0001f && Math.Abs(g.StunSeconds - stun) < 0.0001f
                && g.Capacity == capacity && g.Pellets == pellets;
        }
        return Is(ItemType.Revolver, 0.5f, 24f, 16f, 0.3f, 6)
            && Is(ItemType.Handcannon, 1.0f, 70f, 50f, 1.5f, 6)
            && Is(ItemType.MachinePistol, 0.175f, 10f, 10f, 0.2f, 21)     // the SMG round at 0.8; its stun lands one hit in ten
            && Is(ItemType.Smg, 0.19f, 16.25f, 16.25f, 0.2f, 21)          // the same round at 1.3
            && Is(ItemType.Shotgun, 1.0f, 8f, 6f, 0.1f, 6, pellets: 8)
            && Is(ItemType.AutoShotgun, 0.45f, 7.2f, 5.4f, 0.1f, 24, pellets: 8)
            && Is(ItemType.Rifle, 0.24f, 25f, 15f, 0.25f, 30)
            && Is(ItemType.BoltRifle, 2.0f, 55f, 33f, 0.6f, 6)
            && Is(ItemType.Hmg, 0.1f, 12f, 10f, 0.1f, 100)
            && Is(ItemType.HarpoonGun, 1.75f, 35f, 35f, 0.5f, 6);
    }

    private static bool Guns_ShotgunThrowsEightPelletsAndLoadsSixShellsFromAStackOfTwelve()
    {
        var (world, slot) = GunWorld(ItemType.Shotgun);
        var inventory = world.CreateSnapshot().Characters.Single().Inventory!;
        var stack = SlotHolding(world, ItemType.ShotgunShells);
        if (inventory.MainSlotTanks[slot] != 6f || stack < 0 || inventory.MainSlotTanks[stack] != 6f)
            return false; // six shells went in, six stayed in the stack

        world.ApplyCommand(1, new ClientCommand(1, LookX: 1f, LookY: 0f, FirePressed: true));
        world.Step(RealtimeStep);
        var snapshot = world.CreateSnapshot();
        var pellets = snapshot.PersonalShots;
        var fanned = pellets.Select(p => MathF.Atan2(p.VelocityY, p.VelocityX)).Distinct().Count() == 8;
        return pellets.Count == 8 && fanned && pellets.All(p => p.Weapon == ItemType.Shotgun)
            && snapshot.Characters.Single().Inventory!.MainSlotTanks[slot] == 5f;
    }

    private static bool Guns_SmgAndHmgFireAtTheirOwnPace()
    {
        static bool Paced(ItemType gun, float seconds)
        {
            var (world, slot) = GunWorld(gun);
            var fired = FireAndCollectBullets(world, (int)(seconds * 30), held: true).Count;
            var expected = (int)(seconds / GunDefinitions.Of(gun)!.ReloadSeconds) + 1;
            var left = world.CreateSnapshot().Characters.Single().Inventory!.MainSlotTanks[slot];
            return Math.Abs(fired - expected) <= 1 && left == GunDefinitions.Of(gun)!.Capacity - fired;
        }
        return Paced(ItemType.Smg, 2f) && Paced(ItemType.Hmg, 2f) && Paced(ItemType.MachinePistol, 2f);
    }

    private static bool Guns_AFreshMagazineSwapsWithTheOneInTheGun()
    {
        var (world, slot) = GunWorld(ItemType.Smg);
        FireAndCollectBullets(world, 30, held: true);
        var left = world.CreateSnapshot().Characters.Single().Inventory!.MainSlotTanks[slot];
        if (left is not (> 0f and < 21f))
            return false;

        world.DebugGiveItem(1, ItemType.SmgMagazine);
        var fresh = SlotHolding(world, ItemType.SmgMagazine);
        world.ApplyCommand(1, new ClientCommand(1, AttachTankFromSlot: fresh, AttachTankToSlot: slot));
        var inventory = world.CreateSnapshot().Characters.Single().Inventory!;
        // The full one is in the gun; the part-used one is where the full one was carried.
        return inventory.MainSlotTanks[slot] == 21f && inventory.MainSlots[fresh] == ItemType.SmgMagazine && inventory.MainSlotTanks[fresh] == left;
    }

    // A hit is the wound of that round and nothing else: the part takes its trauma and its bleeding, and the stun
    // of the round puts the target down.
    private static bool Guns_ARoundLeavesExactlyItsWoundBleedingAndStun()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugShootCharacter(1, ItemType.Handcannon, BodyPart.LeftLeg);
        var body = world.DebugBody(1);
        var cannon = body[BodyPart.LeftLeg].Trauma == 70f && body[BodyPart.LeftLeg].Bleeding == 50f && body.StunSeconds == 1.5f;

        var other = new World();
        other.SpawnCharacter(1);
        other.DebugShootCharacter(1, ItemType.Smg, BodyPart.RightArm);
        other.DebugShootCharacter(1, ItemType.Smg, BodyPart.RightArm);
        var arm = other.DebugBody(1)[BodyPart.RightArm];
        var smg = Math.Abs(arm.Trauma - 32.5f) < 0.001f && Math.Abs(arm.Bleeding - 32.5f) < 0.001f && other.DebugBody(1).StunSeconds == 0f;

        world.Step(RealtimeStep);
        var down = world.DebugBody(1).IsDowned;
        return cannon && smg && down;
    }

    // The bolt rifle: two seconds between shots for a trained hand, five for anybody else.
    private static bool Guns_BoltRifleCyclesSlowlyInUntrainedHands()
    {
        var spec = GunDefinitions.Of(ItemType.BoltRifle)!;
        if (spec.ReloadFor(40f) != 2f || spec.ReloadFor(0f) != 5f || spec.ReloadFor(20f) != 3.5f)
            return false;

        var (world, slot) = GunWorld(ItemType.BoltRifle);
        float? Loaded() => world.CreateSnapshot().Characters.Single().Inventory!.MainSlotTanks[slot];
        void Pull()
        {
            world.ApplyCommand(1, new ClientCommand(1, LookX: 1f, LookY: 0f, FirePressed: true));
            world.Step(RealtimeStep);
        }
        Pull();
        if (Loaded() != 5f)
            return false;
        StepFor(world, 3 * 30); // three seconds: enough for a marksman, not for this character
        Pull();
        if (Loaded() != 5f)
            return false;
        StepFor(world, 2 * 30 + 5);
        Pull();
        return Loaded() == 4f;
    }

    private static bool Guns_AutoShotgunHoldsTwoStacksOfShells()
    {
        var (world, slot) = GunWorld(ItemType.AutoShotgun);
        float? Loaded() => world.CreateSnapshot().Characters.Single().Inventory!.MainSlotTanks[slot];
        if (Loaded() != 12f || SlotHolding(world, ItemType.ShotgunShells) >= 0)
            return false; // the whole first stack went in
        LoadGun(world, ItemType.AutoShotgun, slot);
        if (Loaded() != 24f)
            return false;
        LoadGun(world, ItemType.AutoShotgun, slot); // full: a third stack stays in the pocket
        var spare = SlotHolding(world, ItemType.ShotgunShells);
        if (Loaded() != 24f || spare < 0)
            return false;

        // Unloading gives a stack back and leaves the other in the gun.
        world.ApplyCommand(1, new ClientCommand(1, DetachTankSlot: slot));
        var inventory = world.CreateSnapshot().Characters.Single().Inventory!;
        return inventory.MainSlotTanks[slot] == 12f && inventory.MainSlots.Count(s => s == ItemType.ShotgunShells) == 2;
    }

    // An untrained hand sprays: the rounds of an SMG leave all over its 16-degree cone, never outside it.
    private static bool Guns_UntrainedFireSpraysInsideTheSpreadCone()
    {
        var spec = GunDefinitions.Of(ItemType.Smg)!;
        if (spec.SpreadFor(100f) >= spec.SpreadFor(0f))
            return false;

        var (world, _) = GunWorld(ItemType.Smg);
        var angles = new Dictionary<string, float>();
        for (var i = 0; i < 3 * 30; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, WeaponFireHeld: true, LookX: 1f, LookY: 0f));
            world.Step(RealtimeStep);
            foreach (var shot in world.CreateSnapshot().PersonalShots)
                angles[shot.Id] = MathF.Atan2(shot.VelocityY, shot.VelocityX) * 180f / MathF.PI;
        }
        var half = spec.UnskilledSpreadDegrees / 2f;
        return angles.Count >= 10 && angles.Values.All(a => Math.Abs(a) <= half + 0.01f)
            && angles.Values.Max() - angles.Values.Min() > half * 0.5f;
    }

    // Aboard a hostile ship: a round from each of the guns that reaches a defender hurts them.
    private static bool Guns_EveryGunHurtsADefenderItHits()
    {
        foreach (var gun in GunDefinitions.Guns)
        {
            var world = new World();
            world.SpawnCharacter(1);
            BoardEnemyShip(world, ItemType.Knife);
            if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip)
                return false;
            var defenderId = WalkBoarderToMeleeRangeOfNearestDefender(world);
            world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: SlotHolding(world, ItemType.Knife))); // the knife goes away
            world.DebugGiveItem(1, gun.Weapon);
            var slot = SlotHolding(world, gun.Weapon);
            world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));
            LoadGun(world, gun.Weapon, slot);

            var before = world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == defenderId).Health;
            for (var i = 0; i < 20; i++)
            {
                FireAtCrew(world, defenderId);
                world.Step(RealtimeStep);
            }
            var after = world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == defenderId).Health;
            if (!(after < before))
                return false;
        }
        return true;
    }
}
