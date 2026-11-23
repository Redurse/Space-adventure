using System.Linq;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // A bridge and, through a door, a hangar room with a shuttle bay (5 x 6 tiles): the crew works on the bridge, which keeps its air
    // when the bay doors open next door.
    private static CustomShipDefinition BuildShuttleShipDefinition() => new(
        "Тестовый корабль с ангаром",
        new[]
        {
            new CustomRoomDef("a", "Мостик", 0, 0, 8, 8),
            new CustomRoomDef("b", "Ангар", 8, 0, 8, 12),
        },
        new[] { new CustomDoorDef(8, 4, true, true) },
        new[] { new CustomAirlockDef("b", EdgeSide.Right) },
        new[]
        {
            new CustomDeviceDef(CustomDeviceKind.Reactor, 2, 2),
            new CustomDeviceDef(CustomDeviceKind.Distribution, 2, 4),
            new CustomDeviceDef(CustomDeviceKind.Helm, 4, 2),
            new CustomDeviceDef(CustomDeviceKind.Navigation, 4, 4),
            new CustomDeviceDef(CustomDeviceKind.Oxygen, 1, 1),
            new CustomDeviceDef(CustomDeviceKind.SuitLocker, 7, 1),
            new CustomDeviceDef(CustomDeviceKind.StorageRack, 7, 2),
            new CustomDeviceDef(CustomDeviceKind.ShuttleHangar, 12, 6),
        },
        0f,
        EnginesRaw: new[] { new CustomEngineDef(2f, 5f, TileSide.West, 20f) });

    // The same, in one room: whoever spawns on the bridge is in the hangar room too.
    private static CustomShipDefinition BuildShuttleOneRoomShipDefinition() => new(
        "Тестовый корабль с ангаром (один отсек)",
        new[] { new CustomRoomDef("a", "Ангар", 0, 0, 16, 12) },
        System.Array.Empty<CustomDoorDef>(),
        new[] { new CustomAirlockDef("a", EdgeSide.Right) },
        new[]
        {
            new CustomDeviceDef(CustomDeviceKind.Reactor, 2, 2),
            new CustomDeviceDef(CustomDeviceKind.Distribution, 2, 4),
            new CustomDeviceDef(CustomDeviceKind.Helm, 4, 2),
            new CustomDeviceDef(CustomDeviceKind.Navigation, 4, 4),
            new CustomDeviceDef(CustomDeviceKind.Oxygen, 1, 1),
            new CustomDeviceDef(CustomDeviceKind.SuitLocker, 15, 1),
            new CustomDeviceDef(CustomDeviceKind.StorageRack, 15, 2),
            new CustomDeviceDef(CustomDeviceKind.ShuttleHangar, 10, 6),
        },
        0f,
        EnginesRaw: new[] { new CustomEngineDef(2f, 5f, TileSide.West, 20f) });

    private static World NewShuttleWorld(bool oneRoom = false)
    {
        var world = new World(ShipKind.Custom, oneRoom ? BuildShuttleOneRoomShipDefinition() : BuildShuttleShipDefinition());
        world.SpawnCharacter(1);
        // The game starts moored to a station; a shuttle only launches from a ship that is free.
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            StepSeconds(world, 1.0);
        }
        return world;
    }

    private static ShuttleState TheShuttle(World world) => world.CreateSnapshot().Shuttles!.Single();

    private static string ShuttleBayId(World world) => TheShuttle(world).BayId;

    private static void ShuttleCommand(World world, ShuttleRequest request) =>
        world.ApplyCommand(1, new ClientCommand(1, Shuttle: request));

    // Sits down at the helm (the spawn point is its own seat), from where a shuttle can be ordered about from anywhere on the ship.
    private static bool SitAtTheHelm(World world)
    {
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);
        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsAtHelm;
    }

    private static bool World_Shuttle_EveryBayHoldsOneShuttle_DockedAtFirst()
    {
        var world = NewShuttleWorld();
        world.Step(RealtimeStep);
        var shuttle = TheShuttle(world);
        return shuttle.Phase == ShuttlePhase.Docked && !shuttle.BayDoorOpen && world.AllShuttlesHome && shuttle.Hp == 100;
    }

    private static bool World_Shuttle_Launch_OpensTheDoorsThenSendsItOut()
    {
        var world = NewShuttleWorld();
        if (!SitAtTheHelm(world))
            return false;
        ShuttleCommand(world, new ShuttleRequest(ShuttleBayId(world), ShuttleOrder.Launch));
        world.Step(RealtimeStep);
        var opening = TheShuttle(world);
        if (opening.Phase != ShuttlePhase.Opening || !opening.BayDoorOpen)
            return false;
        StepSeconds(world, 5.0);
        return TheShuttle(world).Phase == ShuttlePhase.Flying && !world.AllShuttlesHome;
    }

    private static bool World_Shuttle_LaunchNeedsSomeoneAtTheBayOrTheHelm()
    {
        var world = NewShuttleWorld();
        // On the bridge, not seated: neither at the bay nor at the helm.
        ShuttleCommand(world, new ShuttleRequest(ShuttleBayId(world), ShuttleOrder.Launch));
        world.Step(RealtimeStep);
        return TheShuttle(world).Phase == ShuttlePhase.Docked;
    }

    private static bool World_Shuttle_OpenDoors_VentTheRoom_AndHurtTheUnsuited()
    {
        var world = NewShuttleWorld(oneRoom: true);
        // The spawn point is within reach of the bay in this one-room ship, and the room is the one that is about to be opened.
        var before = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        ShuttleCommand(world, new ShuttleRequest(ShuttleBayId(world), ShuttleOrder.Launch));
        StepSeconds(world, 7.0); // the doors open at 4 s and stay open until the shuttle is clear of the bay, a few seconds more
        var after = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Health;
        return after < before;
    }

    private static bool World_Shuttle_MoveTo_FliesThereAndStopsAtThePoint()
    {
        var world = NewShuttleWorld();
        if (!SitAtTheHelm(world))
            return false;
        ShuttleCommand(world, new ShuttleRequest(ShuttleBayId(world), ShuttleOrder.Launch));
        StepSeconds(world, 7.0);
        var start = TheShuttle(world);
        var targetX = (float)start.X + 140f;
        var targetY = (float)start.Y;
        ShuttleCommand(world, new ShuttleRequest(ShuttleBayId(world), ShuttleOrder.MoveTo, targetX, targetY));
        StepSeconds(world, 25.0);
        var end = TheShuttle(world);
        var distance = System.Math.Sqrt((end.X - targetX) * (end.X - targetX) + (end.Y - targetY) * (end.Y - targetY));
        var speed = System.Math.Sqrt(end.VelocityX * end.VelocityX + end.VelocityY * end.VelocityY);
        return end.Phase == ShuttlePhase.Flying && distance < 8.0 && speed < 2.0 && !end.HasDestination;
    }

    private static bool World_Shuttle_Recall_BringsItHomeAndShutsTheDoors()
    {
        var world = NewShuttleWorld();
        if (!SitAtTheHelm(world))
            return false;
        ShuttleCommand(world, new ShuttleRequest(ShuttleBayId(world), ShuttleOrder.Launch));
        StepSeconds(world, 7.0);
        var start = TheShuttle(world);
        if (start.Phase != ShuttlePhase.Flying)
            return false;
        ShuttleCommand(world, new ShuttleRequest(ShuttleBayId(world), ShuttleOrder.MoveTo, (float)start.X + 120f, (float)start.Y));
        StepSeconds(world, 12.0);
        ShuttleCommand(world, new ShuttleRequest(ShuttleBayId(world), ShuttleOrder.Recall));
        StepSeconds(world, 30.0);
        var home = TheShuttle(world);
        return home.Phase == ShuttlePhase.Docked && !home.BayDoorOpen && world.AllShuttlesHome;
    }

    private static bool World_Shuttle_WhileOut_TheShipCannotDockLandOrJump()
    {
        var world = NewShuttleWorld();
        if (!SitAtTheHelm(world))
            return false;
        ShuttleCommand(world, new ShuttleRequest(ShuttleBayId(world), ShuttleOrder.Launch));
        StepSeconds(world, 5.0);
        return !world.AllShuttlesHome && !world.CanWarpNow && !world.CanDockNow && !world.CanLandNow;
    }
}
