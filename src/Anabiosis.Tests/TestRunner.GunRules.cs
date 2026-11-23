using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// The rules of gunfire taken from Barotrauma on top of its numbers (World.GunShots.cs): bullets that stop at
// walls and closed doors, friendly fire, armour and penetration, a stun that passes by itself, the weapons skill,
// and the other rounds a gun can be loaded with.
internal static partial class TestRunner
{
    // A bullet stops in a standing wall; a closed door stops it, the same door open lets it through.
    private static bool GunRules_ABulletStopsAtAWallAndAClosedDoor()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var tiles = world.Ship.Tiles;
        var me = world.CreateSnapshot().Characters.Single();
        var from = new Vec2(me.X, me.Y);
        foreach (var direction in new[] { new Vec2(1, 0), new Vec2(-1, 0), new Vec2(0, 1), new Vec2(0, -1) })
        {
            var stop = ShotTrace.ToWall(tiles, from, direction, 60f);
            if (stop.Distance >= 60f)
                return false; // nothing inside a hull is sixty tiles of open floor
        }

        // A door that is a tile of its own: come at it from a free floor tile next to it, with free floor beyond.
        foreach (var (coord, cell) in tiles.Cells)
        {
            if (cell.Wall != TileWallKind.Door)
                continue;
            foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
            {
                bool Free(TileCoord c) => tiles.CellAt(c) is { HasFloor: true, Wall: TileWallKind.None };
                var near = new TileCoord(coord.X - dx, coord.Y - dy);
                var far = new TileCoord(coord.X + dx, coord.Y + dy);
                if (!Free(near) || !Free(far))
                    continue;
                var start = new Vec2(near.X + 0.5, near.Y + 0.5);
                tiles.SetDoorOpen(coord, false);
                var closed = ShotTrace.ToWall(tiles, start, new Vec2(dx, dy), 2f).Distance;
                tiles.SetDoorOpen(coord, true);
                var open = ShotTrace.ToWall(tiles, start, new Vec2(dx, dy), 2f).Distance;
                return closed < 1.1f && open >= 1.9f;
            }
        }
        return false; // a hull with no door to try
    }

    // The first body on the line takes the round, whoever it belongs to.
    private static bool GunRules_FriendlyFireHitsACrewmateInTheLine()
    {
        var (world, _) = GunWorld(ItemType.Revolver);
        world.SpawnCharacter(2);
        var snapshot = world.CreateSnapshot();
        var me = snapshot.Characters.Single(c => c.PlayerId == 1);
        var mate = snapshot.Characters.Single(c => c.PlayerId == 2);
        var dx = (float)(mate.X - me.X);
        var dy = (float)(mate.Y - me.Y);
        var aimed = MathF.Abs(dx) + MathF.Abs(dy) > 0.01f;
        world.ApplyCommand(1, new ClientCommand(1, LookX: aimed ? dx : 1f, LookY: aimed ? dy : 0f, FirePressed: true));
        world.Step(RealtimeStep);
        world.Step(RealtimeStep);
        return world.DebugBody(2).Vitality < BodyHealth.MaxVitality && world.DebugBody(1).Vitality >= BodyHealth.MaxVitality - 0.01f;
    }

    // A vest cuts a torso wound to 30% and its bleeding to 20%; a round takes its penetration off that protection.
    private static bool GunRules_ArmourCutsTheWoundAndPenetrationCutsTheArmour()
    {
        static (float Trauma, float Bleeding) Hit(ItemType gun, BodyPart part, params ItemType[] worn)
        {
            var world = new World();
            world.SpawnCharacter(1);
            foreach (var item in worn)
                world.DebugEquip(1, item);
            world.DebugShootCharacter(1, gun, part);
            var hit = world.DebugBody(1)[part];
            return (hit.Trauma, hit.Bleeding);
        }
        static bool Near(float a, float b) => MathF.Abs(a - b) < 0.01f;

        var bare = Hit(ItemType.Revolver, BodyPart.Torso);
        var vest = Hit(ItemType.Revolver, BodyPart.Torso, ItemType.BodyArmor);      // penetration 0.1
        var rifle = Hit(ItemType.BoltRifle, BodyPart.Torso, ItemType.BodyArmor);    // penetration 0.6
        var helmet = Hit(ItemType.Revolver, BodyPart.Head, ItemType.BallisticHelmet);
        var arm = Hit(ItemType.Revolver, BodyPart.LeftArm, ItemType.BodyArmor, ItemType.BallisticHelmet);
        return Near(bare.Trauma, 24f) && Near(bare.Bleeding, 16f)
            && Near(vest.Trauma, 24f * 0.37f) && Near(vest.Bleeding, 16f * 0.28f)
            && Near(rifle.Trauma, 55f * 0.72f) && Near(rifle.Bleeding, 33f * 0.68f)
            && Near(helmet.Trauma, 24f * 0.28f) && Near(helmet.Bleeding, 16f * 0.19f)
            && Near(arm.Trauma, 24f) && Near(arm.Bleeding, 16f); // nothing covers a limb
    }

    // A stun is a few seconds on the floor, then back up - no need to recover any health first.
    private static bool GunRules_AStunPassesByItself()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugShootCharacter(1, ItemType.Handcannon, BodyPart.LeftLeg); // 1.5 seconds of stun
        var body = world.DebugBody(1);
        if (!body.IsDowned || !body.Stunned || body.Unconscious)
            return false;
        StepFor(world, 2 * 30);
        return !body.IsDowned && !body.Stunned;
    }

    // The spread sits between the gun's two cones by how far the skill is from what the gun asks for.
    private static bool GunRules_SkillSetsTheSpreadAndTheStartingSkillFollowsTheJob()
    {
        var revolver = GunDefinitions.Of(ItemType.Revolver)!; // asks for 40; cones 0.1 and 6
        static bool Near(float a, float b) => MathF.Abs(a - b) < 0.001f;
        return Near(revolver.SpreadFor(140f), 0.1f)                          // a hundred over: the tight cone
            && Near(revolver.SpreadFor(40f), 0.1f + 5.9f * 0.25f)            // exactly enough: a quarter of the way out
            && Near(revolver.SpreadFor(-60f), 6f)                            // a hundred under: the whole loose cone
            && WeaponSkills.Starting(CrewRole.Security) == 45f && WeaponSkills.Starting(CrewRole.Captain) == 30f
            && WeaponSkills.Starting(CrewRole.Engineer) == 20f
            && Near(WeaponSkills.GainFor(25f, 20f), 0.3125f);
    }

    // Hurting a hostile raises the skill of the one who did it.
    private static bool GunRules_HittingADefenderRaisesTheSkill()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugEnemyCrewHoldsFire = true;
        BoardEnemyShip(world, ItemType.Knife);
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip)
            return false;
        var defenderId = WalkBoarderToMeleeRangeOfNearestDefender(world);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: SlotHolding(world, ItemType.Knife)));
        world.DebugGiveItem(1, ItemType.Revolver);
        var slot = SlotHolding(world, ItemType.Revolver);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));
        LoadGun(world, ItemType.Revolver, slot);

        var before = world.DebugWeaponsSkill(1);
        for (var i = 0; i < 40; i++)
        {
            FireAtCrew(world, defenderId);
            world.Step(RealtimeStep);
        }
        return world.DebugWeaponsSkill(1) > before;
    }

    // A gun takes its ordinary round or a variant of it, remembers which is in it, and will not mix loose rounds.
    private static bool GunRules_AGunFiresTheKindOfRoundLoadedInIt()
    {
        var depleted = GunDefinitions.Loaded(GunDefinitions.Of(ItemType.Revolver)!, ItemType.RevolverRoundsDepleted);
        var smg = GunDefinitions.Loaded(GunDefinitions.Of(ItemType.Smg)!, ItemType.SmgMagazineDepleted);
        var slug = GunDefinitions.Loaded(GunDefinitions.Of(ItemType.Shotgun)!, ItemType.ShotgunSlugs);
        var wrong = GunDefinitions.Loaded(GunDefinitions.Of(ItemType.Revolver)!, ItemType.ShotgunSlugs);
        static bool Near(float a, float b) => MathF.Abs(a - b) < 0.001f;
        if (!(Near(depleted.HitWound, 35f) && Near(depleted.HitBleeding, 10f) && Near(depleted.Penetration, 0.25f)
            && Near(smg.HitWound, 19.5f) && Near(smg.Penetration, 0.4f)       // the SMG's own 0.15 on top of the round's 0.25
            && slug.Pellets == 1 && Near(slug.HitWound, 20f)
            && Near(wrong.HitWound, 24f)))                                    // a round the gun does not take changes nothing
            return false;

        var (world, slot) = GunWorld(ItemType.Revolver);
        InventoryState Inventory() => world.CreateSnapshot().Characters.Single().Inventory!;
        world.DebugGiveItem(1, ItemType.RevolverRoundsDepleted);
        var stack = SlotHolding(world, ItemType.RevolverRoundsDepleted);
        world.ApplyCommand(1, new ClientCommand(1, AttachTankFromSlot: stack, AttachTankToSlot: slot));
        if (Inventory().MainSlotTanks[slot] != 6f || Inventory().MainSlotTankTypes![slot] == ItemType.RevolverRoundsDepleted)
            return false; // six ordinary rounds are in the cylinder: the other kind stays out

        world.ApplyCommand(1, new ClientCommand(1, DetachTankSlot: slot));
        stack = SlotHolding(world, ItemType.RevolverRoundsDepleted);
        world.ApplyCommand(1, new ClientCommand(1, AttachTankFromSlot: stack, AttachTankToSlot: slot));
        var loaded = Inventory();
        if (loaded.MainSlotTanks[slot] != 6f || loaded.MainSlotTankTypes![slot] != ItemType.RevolverRoundsDepleted)
            return false;

        // Unloading gives back the kind that was in it.
        world.ApplyCommand(1, new ClientCommand(1, DetachTankSlot: slot));
        var after = Inventory();
        return after.MainSlotTanks[slot] is null && after.MainSlots.Count(s => s == ItemType.RevolverRoundsDepleted) == 1
            && after.MainSlots.Count(s => s == ItemType.RevolverRounds) == 1;
    }

    // Defenders work their guns: a rifleman gets off several rounds in the time the old fixed interval allowed one.
    private static bool GunRules_DefendersFireAtThePaceOfTheirGuns()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.DebugEnemyCrewHoldsFire = true; // no shooting on the walk up
        // Cut in where a fighter can actually be walked up to (the hull's devices wall some compartments off).
        BoardEnemyShip(world, ItemType.Knife, throughBlockId: HullBlockWithAWalkToAFighter(world));
        if (!world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnEnemyShip)
            return false;
        WalkBoarderToMeleeRangeOfNearestDefender(world, NearestReachableDefender(world, EnemyCrewRole.Fighter));
        world.DebugEnemyCrewHoldsFire = false;
        var seen = new HashSet<string>();
        for (var i = 0; i < 3 * 30; i++)
        {
            world.Step(RealtimeStep);
            foreach (var shot in world.CreateSnapshot().PersonalShots)
                if (shot.FromEnemy)
                    seen.Add(shot.Id);
        }
        return seen.Count >= 3;
    }
}

// Everyone on a station can be hurt, not only its guards (World.StationCrime.cs).
internal static partial class TestRunner
{
    // Player 1 on the docked station with a loaded assault rifle in hand.
    private static World ArmedOnStation()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var slot = TakeFromRack(world, ItemType.Rifle);
        world.ApplyCommand(1, new ClientCommand(1, ToggleHoldSlotIndex: slot));
        TakeTankFromRack(world, ItemType.Magazine);
        AttachTankTo(world, slot, ItemType.Magazine);
        WalkOntoStation(world);
        return world;
    }

    // Shooting the trader dead: the station goes on alert, the standing drops, and there is nobody left to trade with.
    private static bool GunRules_StationStaffCanBeShotDead()
    {
        var world = ArmedOnStation();
        var trader = world.Station.Npcs.FirstOrDefault(n => n.Kind == NpcKind.Trader);
        if (trader is null || !world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).OnStation)
            return false;
        // Unharmed staff are not in the list at all; only guards are.
        if (world.CreateSnapshot().Station.Guards.Any(g => g.NpcId == trader.Id))
            return false;
        var faction = world.CreateSnapshot().GalaxyPoints.First(p => p.Id == world.CreateSnapshot().Voyage.DockedPointId).Faction;
        var standingBefore = world.GetStanding(faction);
        var creditsBefore = world.Credits;

        WalkOnStationTo(world, trader.X, trader.Y);
        for (var i = 0; i < 20 * 30; i++)
        {
            if (world.CreateSnapshot().Station.Guards.FirstOrDefault(g => g.NpcId == trader.Id) is { Alive: false })
                break;
            world.ApplyCommand(1, new ClientCommand(1, FirePressed: true));
            world.Step(RealtimeStep);
        }
        var state = world.CreateSnapshot().Station.Guards.FirstOrDefault(g => g.NpcId == trader.Id);
        if (state is not { Alive: false } || !world.IsStationAlerted || world.GetStanding(faction) >= standingBefore)
            return false;

        // A dead trader sells nothing.
        world.DebugAddCredits(1000);
        var credits = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, BuyItemType: ItemType.MedKit));
        world.Step(RealtimeStep);
        return world.Credits == credits && creditsBefore <= credits;
    }

    // A resident walking about the station takes a round like anyone else.
    private static bool GunRules_StationResidentsCanBeShot()
    {
        var world = ArmedOnStation();
        for (var i = 0; i < 40 * 30; i++)
        {
            var snapshot = world.CreateSnapshot();
            var residents = snapshot.Station.Residents;
            if (residents is null || residents.Count == 0)
                return false;
            if (residents.Any(r => r.Health < r.MaxHealth))
                return world.IsStationAlerted;
            var me = snapshot.Characters.Single(c => c.PlayerId == 1);
            if (me.Health <= 0)
                return false;
            var target = residents.OrderBy(r => (r.X - me.X) * (r.X - me.X) + (r.Y - me.Y) * (r.Y - me.Y)).First();
            var dx = (float)(target.X - me.X);
            var dy = (float)(target.Y - me.Y);
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            // Walk up, then fire along the line to them.
            world.ApplyCommand(1, distance > 1.5f
                ? new ClientCommand(1, MoveX: dx / distance, MoveY: dy / distance)
                : new ClientCommand(1, LookX: dx, LookY: dy, WeaponFireHeld: true));
            world.Step(RealtimeStep);
        }
        return false;
    }
}

// The map's extrapolation of the ship (GalaxyMapPanel.cs) must not depend on the server's tick count agreeing with a client clock.
internal static partial class TestRunner
{
    private static bool MapClock_SnapshotAgeIsMeasuredFromWhenATickWasFirstSeen()
    {
        var age = new Anabiosis.Client.Rendering.SnapshotAge();
        static bool Near(float a, float b) => MathF.Abs(a - b) < 0.0001f;

        // A clock that is nowhere near tick / 30 - the state a second game in the same process used to start in.
        if (!Near(age.Seconds(tick: 5, now: 1000.00, maxSeconds: 0.1f), 0f))
            return false;                                                    // first sight of a tick: nothing to carry forward yet
        if (!Near(age.Seconds(5, 1000.04, 0.1f), 0.04f))
            return false;                                                    // the same tick on the next frame
        if (!Near(age.Seconds(5, 1003.00, 0.1f), 0.1f))
            return false;                                                    // a long wait for the next snapshot never runs further than the cap
        if (!Near(age.Seconds(6, 1003.03, 0.1f), 0f))
            return false;                                                    // a new tick starts over
        // The game ends and another begins: its tick count starts from zero and the clock may even run backwards.
        return Near(age.Seconds(0, 20.0, 0.1f), 0f) && Near(age.Seconds(0, 19.9, 0.1f), 0f);
    }
}

// A station's guards fire back with the rifle they carry (World.GuardFire.cs).
internal static partial class TestRunner
{
    // Everything that happens to player 1 on the station for up to `seconds` with the alert up and nobody doing anything:
    // how many rounds the guards fired (at the player, as the snapshot saw them) and what the player lost.
    private static (int Rounds, float HealthLost, bool TracerSeen) LetGuardsShoot(World world, int seconds)
    {
        world.DebugRaiseStationAlert();
        world.ApplyCommand(1, new ClientCommand(1));
        var before = world.DebugBody(1).Vitality;
        var seen = new HashSet<string>();
        var tracer = false;
        for (var i = 0; i < seconds * 30; i++)
        {
            world.Step(RealtimeStep);
            var snapshot = world.CreateSnapshot();
            foreach (var shot in snapshot.PersonalShots)
                if (shot.FromEnemy && shot.Scene == ShotScene.Station)
                    seen.Add(shot.Id);
            tracer |= snapshot.LaserBeams?.Any(b => b.Tracer && b.FromEnemy) == true;
            if (world.DebugBody(1).Dead)
                break;
        }
        return (seen.Count, before - world.DebugBody(1).Vitality, tracer);
    }

    // The patrolling guards come at the crew on alert and fire real rounds: a tracer, wounds on the player.
    private static bool GunRules_PatrollingGuardsFireBackAtTheCrew()
    {
        var world = ArmedOnStation();
        if (world.DebugResidentGuardCount() == 0)
            return false; // the home station has patrolling guards - this test is about them
        // The posted guards stand down, so only the patrolling ones can be what hurts.
        var posted = world.Station.Npcs.Where(n => n.Kind == NpcKind.Security).ToList();
        foreach (var guard in posted)
            world.DebugKillStationNpc(guard.Id);
        var (rounds, lost, tracer) = LetGuardsShoot(world, 25);
        return rounds >= 3 && lost > 5f && tracer;
    }

    // The guard posted in a room fires back too: with real rounds, not a damage number applied from nowhere.
    private static bool GunRules_PostedGuardsFireRealRoundsToo()
    {
        var world = ArmedOnStation();
        var guard = world.Station.Npcs.FirstOrDefault(n => n.Kind == NpcKind.Security);
        if (guard is null)
            return false;
        WalkOnStationTo(world, guard.X, guard.Y);
        var (rounds, lost, tracer) = LetGuardsShoot(world, 10);
        return rounds >= 3 && lost > 5f && tracer;
    }

    // A guard's rounds go through the people of the station: nobody who lives there is hurt by them.
    private static bool GunRules_GuardsDoNotHitTheirOwnPeople()
    {
        var world = ArmedOnStation();
        LetGuardsShoot(world, 20);
        var snapshot = world.CreateSnapshot();
        return snapshot.Station.Residents!.All(r => r.Health >= r.MaxHealth)
            && snapshot.Station.Guards.All(g => g.Health >= g.MaxHealth);
    }
}
