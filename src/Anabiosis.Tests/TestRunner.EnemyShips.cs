using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// A hostile ship as a real simulated ship (World.EnemyDamage.cs / World.EnemyCrew.cs): it is hit where the shell actually
// crosses its hull, loses compartments and the reactor, and its people walk their posts doing their roles' jobs.
internal static partial class TestRunner
{
    private static World EngagedWorld()
    {
        var world = new World();
        world.SpawnCharacter(1);
        EnterBattle(world);
        return world;
    }

    private static float WallHpTotal(World world) =>
        world.CreateSnapshot().EnemyShip.WallBlockStates.Sum(s => s.Hp);

    // A shell that crosses the hull hurts the first wall it meets, and only that one.
    private static bool World_EnemyShip_ShellHurtsOnlyTheFirstWallItCrosses()
    {
        var world = EngagedWorld();
        var before = WallHpTotal(world);

        // The airlock compartment's east wall: nothing but plating out there.
        if (!world.DebugShootEnemyLocal(new Vec2(34, 4.5), new Vec2(30, 4.5), 40f))
            return false;

        var after = WallHpTotal(world);
        var hurt = world.CreateSnapshot().EnemyShip.WallBlockStates.Count(s => s.Hp < s.MaxHp);
        return Math.Abs(before - after - 40f) < 0.01f && hurt == 1;
    }

    // A shell fired at empty space does nothing.
    private static bool World_EnemyShip_ShellThroughEmptySpaceHitsNothing()
    {
        var world = EngagedWorld();
        return !world.DebugShootEnemyLocal(new Vec2(60, 60), new Vec2(50, 50), 40f);
    }

    // Losing a third of a compartment's walls destroys it: it leaves the hull, and the ship's health readout drops.
    private static bool World_EnemyShip_CompartmentThatLosesItsWalls_IsDestroyed()
    {
        var world = EngagedWorld();
        var roomsBefore = world.EnemyShipLayout.Rooms.Count;
        var hpBefore = world.Enemy.Hp;
        var victim = world.EnemyShipLayout.Rooms.First(r => r.Name.Contains("Двигатель") && r.Left < 5f);

        world.DebugDestroyEnemyRoomWallBlocks(victim.Id);
        for (var i = 0; i < 5 * 30; i++)
            world.Step(RealtimeStep);

        var layout = world.EnemyShipLayout;
        return layout.Rooms.Count < roomsBefore && layout.Rooms.All(r => r.Id != victim.Id)
            && world.Enemy.Hp < hpBefore && world.Enemy.Hp > 0;
    }

    // The reactor's compartment going takes the whole ship: it explodes and is lost.
    private static bool World_EnemyShip_ReactorCompartmentDestroyed_TheShipExplodes()
    {
        var world = EngagedWorld();
        var reactorRoomId = world.EnemyShipLayout.Ship.Devices.First(d => d.Kind == DeviceKind.Reactor).RoomId;

        world.DebugDestroyEnemyRoomWallBlocks(reactorRoomId);
        world.Step(RealtimeStep);
        var sawReactorBlast = world.CreateSnapshot().ShipBlasts?.Any(b => b.Kind == ShipBlastKind.Reactor) ?? false;
        var aliveMidway = world.Enemy.Hp > 0;

        for (var i = 0; i < 8 * 30; i++)
            world.Step(RealtimeStep);

        return sawReactorBlast && aliveMidway && world.Enemy.Hp <= 0;
    }

    // Shooting the reactor itself is as final.
    private static bool World_EnemyShip_ReactorShotToPieces_TheShipExplodes()
    {
        var world = EngagedWorld();
        var reactor = world.EnemyShipLayout.Ship.Devices.First(d => d.Kind == DeviceKind.Reactor);
        // The shell starts right beside the reactor, so only the reactor is crossed.
        for (var i = 0; i < 20; i++)
            world.DebugShootEnemyLocal(reactor.Position + new Vec2(-0.5, 0), reactor.Position + new Vec2(0.5, 0), 100f);
        for (var i = 0; i < 8 * 30; i++)
            world.Step(RealtimeStep);

        return world.Enemy.Hp <= 0;
    }

    // Without working engines the ship cannot manoeuvre: it stops dead in the water.
    private static bool World_EnemyShip_WithoutEngines_StopsMoving()
    {
        var world = EngagedWorld();
        world.DebugKillEnemyCrewWithRole(EnemyCrewRole.Engineer); // or they would simply repair them
        for (var i = 0; i < 6 * 30; i++)
            world.Step(RealtimeStep); // let it get underway

        world.DebugBreakEnemyEngines();
        for (var i = 0; i < 20 * 30; i++)
            world.Step(RealtimeStep);
        var a = world.CreateSnapshot().EnemyShip.Ships.First();
        for (var i = 0; i < 4 * 30; i++)
            world.Step(RealtimeStep);
        var b = world.CreateSnapshot().EnemyShip.Ships.First();

        var drift = Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        return drift < 1.0;
    }

    private static int EnemyShotsInFlightOver(World world, int seconds)
    {
        var seen = new HashSet<string>();
        for (var i = 0; i < seconds * 30; i++)
        {
            world.Step(RealtimeStep);
            foreach (var shot in world.CreateSnapshot().Projectiles.Where(p => p.FromEnemy))
                seen.Add(shot.Id);
        }
        return seen.Count;
    }

    // A gun fires only while its scientist is at the periscope.
    private static bool World_EnemyShip_GunsNeedTheirScientists()
    {
        var manned = EngagedWorld();
        var firedManned = EnemyShotsInFlightOver(manned, 40);

        var unmanned = EngagedWorld();
        unmanned.DebugKillEnemyCrewWithRole(EnemyCrewRole.Scientist);
        var firedUnmanned = EnemyShotsInFlightOver(unmanned, 40);

        return firedManned > 0 && firedUnmanned == 0;
    }

    // An engineer walks to a hole in the hull and welds it shut.
    private static bool World_EnemyShip_EngineerPatchesABreachedWall()
    {
        var world = EngagedWorld();
        var block = world.EnemyShipLayout.WallBlocks.First(b => !b.IsInterior && b.RoomId == world.EnemyShipLayout.BoardingRoomId);
        world.DebugBreachEnemyWallBlock(block.Id);

        for (var i = 0; i < 90 * 30; i++)
            world.Step(RealtimeStep);

        return world.CreateSnapshot().EnemyShip.WallBlockStates.First(s => s.Id == block.Id).Hp >= World.WallBlockMaxHp - 0.5f;
    }

    // A scientist walks to a wounded crewmate and heals them.
    private static bool World_EnemyShip_ScientistHealsTheWounded()
    {
        var world = EngagedWorld();
        var patient = world.CreateSnapshot().EnemyShip.Crew.First(c => c.Role == EnemyCrewRole.Fighter);
        world.DebugDamageEnemyCrew(patient.Id, 40f);
        var hurt = world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == patient.Id).Health;

        for (var i = 0; i < 40 * 30; i++)
            world.Step(RealtimeStep);

        return world.CreateSnapshot().EnemyShip.Crew.First(c => c.Id == patient.Id).Health > hurt + 15f;
    }

    // Everyone starts at their post, and the people are real walkers on the hull.
    private static bool World_EnemyShip_CrewAreEightPeopleWithRoles()
    {
        var world = EngagedWorld();
        var crew = world.CreateSnapshot().EnemyShip.Crew;
        return crew.Count is >= 6 and <= 8
            && crew.Count(c => c.Role == EnemyCrewRole.Captain) == 1
            && crew.Any(c => c.Role == EnemyCrewRole.Scientist)
            && crew.Any(c => c.Role == EnemyCrewRole.Engineer);
    }

    // However many fight, never more than two hulls are in the field at once - a bigger squadron arrives in waves.
    private static bool World_EnemyShip_NeverMoreThanTwoInTheField()
    {
        var world = EngagedWorld();
        for (var i = 0; i < 6; i++)
            world.ApplyCommand(1, new ClientCommand(1, DebugSpawnEnemyPressed: true));
        world.Step(RealtimeStep);
        return world.DebugEnemyShipsInField <= 2;
    }

    // ---- the hostile ship's electrics ----

    private static void ShootDevice(World world, Vec2 position) =>
        world.DebugShootEnemyLocal(position + new Vec2(-0.5, 0), position + new Vec2(0.5, 0), 5f);

    // A healthy hostile ship has every gun and engine powered.
    private static bool World_EnemyPower_HealthyShipRunsEverything()
    {
        var world = EngagedWorld();
        var power = world.DebugEnemyPower();
        var layout = world.EnemyShipLayout;
        return !power.ReactorBroken && power.CutWires == 0
            && power.GunsFiring == layout.Ship.Turrets.Count && power.EnginesRunning == layout.Ship.Engines.Count;
    }

    // Hitting the reactor knocks it out: no power anywhere, until an engineer brings it back.
    private static bool World_EnemyPower_ReactorHit_DarkensTheShip_ThenEngineersRestoreIt()
    {
        var world = EngagedWorld();
        var reactor = world.EnemyShipLayout.Ship.Devices.First(d => d.Kind == DeviceKind.Reactor);
        ShootDevice(world, reactor.Position);
        // The battery covers for a few seconds before the power really goes (the same as on the player's ship).
        for (var i = 0; i < 5 * 30; i++)
            world.Step(RealtimeStep);
        var dark = world.DebugEnemyPower();
        if (!dark.ReactorBroken || dark.GunsFiring != 0 || dark.EnginesRunning != 0)
            return false;

        for (var i = 0; i < 90 * 30; i++)
            world.Step(RealtimeStep);
        var back = world.DebugEnemyPower();
        return !back.ReactorBroken && back.GunsFiring > 0 && back.EnginesRunning > 0;
    }

    // Shooting a gun cuts its lead: that gun goes silent until the wire is repaired, the other keeps firing.
    private static bool World_EnemyPower_ShotGunLosesItsLead_ThenIsRewired()
    {
        var world = EngagedWorld();
        var layout = world.EnemyShipLayout;
        var turret = layout.Ship.Turrets.First();
        ShootDevice(world, TurretMount.For(layout.Rooms, layout.Ship.Turrets, turret).Position);
        var hit = world.DebugEnemyPower();
        if (hit.CutWires != 1 || hit.GunsFiring != layout.Ship.Turrets.Count - 1)
            return false;

        for (var i = 0; i < 90 * 30; i++)
            world.Step(RealtimeStep);
        var fixedUp = world.DebugEnemyPower();
        return fixedUp.CutWires == 0 && fixedUp.GunsFiring == layout.Ship.Turrets.Count;
    }

    // A junction box (щиток) hit cuts the trunk to everything on its system.
    private static bool World_EnemyPower_JunctionBoxHit_CutsItsSystem()
    {
        var world = EngagedWorld();
        var before = world.DebugEnemyPower();
        var anyDrop = false;
        foreach (var box in world.EnemyShipLayout.Ship.JunctionBoxes)
        {
            ShootDevice(world, new Vec2(box.X, box.Y));
            var now = world.DebugEnemyPower();
            if (now.CutWires > before.CutWires && (now.GunsFiring < before.GunsFiring || now.EnginesRunning < before.EnginesRunning))
            {
                anyDrop = true;
                break;
            }
        }
        return anyDrop;
    }

    // After a compartment is lost the hull is rebuilt, but the guns still fire and the crew still man them.
    private static bool World_EnemyPower_LosingACompartment_KeepsGunsAndWiresConsistent()
    {
        var world = EngagedWorld();
        var victim = world.EnemyShipLayout.Rooms.First(r => r.Name.Contains("Двигатель") && r.Left < 5f);
        world.DebugDestroyEnemyRoomWallBlocks(victim.Id);
        for (var i = 0; i < 3 * 30; i++)
            world.Step(RealtimeStep);
        if (world.EnemyShipLayout.Rooms.Any(r => r.Id == victim.Id))
            return false;

        var power = world.DebugEnemyPower();
        var guns = world.EnemyShipLayout.Ship.Turrets.Count;
        return guns > 0 && power.GunsFiring == guns && power.EnginesRunning == world.EnemyShipLayout.Ship.Engines.Count;
    }

    // The client rebuilds the hostile ship from the definition the snapshot carries: it must survive the wire intact.
    private static bool Wire_EnemyShipDefinition_RoundTripsAndRebuildsTheSameShip()
    {
        var world = EngagedWorld();
        while (world.CreateSnapshot().EnemyShip.Definition is null)
            world.Step(RealtimeStep);

        var sent = world.CreateSnapshot();
        var back = Anabiosis.Shared.Networking.Wire.Deserialize<WorldSnapshot>(Anabiosis.Shared.Networking.Wire.Serialize(sent));
        if (back.EnemyShip.Definition is not { } definition)
            return false;
        var rebuilt = Ship.FromCustomDefinition(definition);
        var original = world.EnemyShipLayout.Ship;
        return rebuilt.Rooms.Count == original.Rooms.Count
            && rebuilt.Turrets.Count == original.Turrets.Count
            && rebuilt.Engines.Count == original.Engines.Count
            && rebuilt.JunctionBoxes.Count == original.JunctionBoxes.Count
            && rebuilt.DoorEdges.Count == original.DoorEdges.Count
            && rebuilt.Devices.Count == original.Devices.Count;
    }
}
