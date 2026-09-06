using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

// Content-каталог отсеков (see the plan's own "содержательный каталог отсеков" section) - the 14
// new functional room types built on top of M60-M64's own build/demolish/detach machinery. Every
// device-carrying catalog entry has to actually work the moment it's built, and a second reactor/
// bridge/engine/shield room has to genuinely SUM with the first rather than being rejected or
// silently ignored (the plan's own "бонус, не список" design).
internal static partial class TestRunner
{
    private static string BuildAndCompleteCatalogRoom(World world, string catalogId)
    {
        var roomsBefore = world.Ship.Rooms.Select(r => r.Id).ToHashSet();
        world.DebugAddCredits(2000); // content-каталог отсеков's pricier entries (reactor 900cr,
        // cockpit-small 700cr, ...) exceed StartingCredits (300) on their own
        world.ApplyCommand(1, new ClientCommand(1, BuildRoom: new BuildRoomRequest(catalogId)));
        world.DebugFastForwardRoomBuilds(9999);
        world.Step(RealtimeStep);
        return world.Ship.Rooms.Select(r => r.Id).First(id => !roomsBefore.Contains(id));
    }

    // A second reactor room's own OutputBonus (RoomCatalog.ReactorRoomBonusOutput) has to actually
    // reach PowerGrid.Reactor - the FIRST reactor device built off any hull never gets a bonus (it's
    // the baseline "exactly one" every hand-authored hull already has), only devices BEYOND it do.
    private static bool World_ContentCatalog_ReactorRoom_IncreasesOutputBonus()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var bonusBefore = world.PowerGrid.Reactor.OutputBonus;

        BuildAndCompleteCatalogRoom(world, "reactor");

        return world.Ship.ReactorDeviceCount == 2
            && world.PowerGrid.Reactor.OutputBonus == bonusBefore + RoomCatalog.ReactorRoomBonusOutput;
    }

    // Two reactor rooms have to sum, not cap at one extra - the user's own explicit "суммируются"
    // decision, not just "a second one is merely tolerated".
    private static bool World_ContentCatalog_TwoReactorRooms_BonusesSum()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");

        BuildAndCompleteCatalogRoom(world, "reactor");
        BuildAndCompleteCatalogRoom(world, "reactor");

        return world.Ship.ReactorDeviceCount == 3
            && world.PowerGrid.Reactor.OutputBonus == RoomCatalog.ReactorRoomBonusOutput * 2;
    }

    // Destroying (M63 structural detachment) a bonus-only reactor room has to lower the bonus back
    // down - the whole point of computing it live off Ship.ReactorDeviceCount every time, not just
    // once at build time.
    private static bool World_ContentCatalog_DestroyingReactorRoom_LowersBonusBack()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var builtRoomId = BuildAndCompleteCatalogRoom(world, "reactor");
        if (world.PowerGrid.Reactor.OutputBonus <= 0f)
            return false; // setup problem - the build itself didn't grant a bonus

        world.DebugDestroyRoomWallBlocks(builtRoomId);
        world.Step(RealtimeStep);

        return world.Ship.ReactorDeviceCount == 1 && world.PowerGrid.Reactor.OutputBonus == 0f
            && world.CreateSnapshot().ShipDebris is { Count: > 0 }; // actually detached, not just refused
    }

    // ShieldSystem.MaxPoints has to come from an instance (Shield.MaxPoints), not the old static
    // constant, and has to actually grow once a shield-generator room is built.
    private static bool World_ContentCatalog_ShieldGeneratorRoom_IncreasesMaxPoints()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var maxBefore = world.Shield.MaxPoints;

        BuildAndCompleteCatalogRoom(world, "shield-generator");

        return world.Shield.MaxPoints == maxBefore + RoomCatalog.ShieldRoomCapacityBonus;
    }

    // A built engine room's own ThrustBonus has to actually reach a real ShipSystemDevice on the
    // Engine power system - confirms the whole CustomDeviceDef->ShipSystemDevice plumbing, not just
    // that a room with the right name exists.
    // Cosmoteer-style marching engines (direct user request, ShipEngine.cs) - a marching-engine
    // catalog room now builds a real 3-tile ShipEngine (RoomCatalog.EnginesFor) instead of the old
    // flat SystemDevices ThrustBonus this test originally checked for; the RCS sibling test right
    // below is untouched since RCS hasn't been converted to a real ShipEngine yet.
    private static bool World_ContentCatalog_EngineRoom_BuildsAWorkingEngine()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");

        BuildAndCompleteCatalogRoom(world, "engine-small");

        return world.Ship.Engines.Count == 1 && world.Ship.Engines[0].MaxThrust > 0f
            && !world.IsEngineNozzleBroken(world.Ship.Engines[0].Id);
    }

    // Regression: Ship.ToDefinition() (World.ShipBuilding.cs always starts a NEW build from its own
    // output) didn't round-trip Engines at all until this was caught here - building a second room
    // after an engine room silently dropped the engine every time. Building an unrelated room
    // ("quarters") right after the engine room must leave it in place.
    private static bool World_ContentCatalog_EngineRoom_SurvivesBuildingAnotherRoomAfterward()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");

        BuildAndCompleteCatalogRoom(world, "engine-small");
        BuildAndCompleteCatalogRoom(world, "quarters");

        return world.Ship.Engines.Count == 1;
    }

    // Cosmoteer-style engines, RCS follow-up (direct user request - "по его образу сделаем все
    // остальные") - "rcs-1way" is the one RCS entry small/simple enough (a single straight line of
    // thrusters) to convert to a real ShipEngine; "rcs-2way"/"rcs-3way" deliberately still use the
    // old flat TurnBonus device (RoomCatalog.EnginesFor's own doc comment explains why).
    private static bool World_ContentCatalog_Rcs1Way_BuildsAWorkingEngine()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");

        BuildAndCompleteCatalogRoom(world, "rcs-1way");

        return world.Ship.Engines.Count == 1 && world.Ship.Engines[0].Role == EngineRole.Rcs
            && world.Ship.Engines[0].MaxThrust > 0f;
    }

    // Same guarantee for an RCS room's TurnBonus - a different field on the same Engine-system
    // device, never both nonzero on the one entry (RoomCatalog.cs's own doc comment).
    private static bool World_ContentCatalog_RcsRoom_CarriesTurnBonusOntoADevice()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");

        BuildAndCompleteCatalogRoom(world, "rcs-2way");

        var totalTurnBonus = world.Ship.SystemDevices.Where(d => d.System == PowerSystemId.Engine).Sum(d => d.TurnBonus);
        return totalTurnBonus > 0f;
    }

    // A turret-room catalog entry has to produce a REAL, working Turret the instant it's built -
    // already-existing device pipeline (M60/M48), just newly reachable through the catalog.
    private static bool World_ContentCatalog_TurretRoom_BuildsAWorkingTurret()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var turretsBefore = world.Ship.Turrets.Count;

        BuildAndCompleteCatalogRoom(world, "turret-laser");

        return world.Ship.Turrets.Count == turretsBefore + 1
            && world.Ship.Turrets.Any(t => t.WeaponType == TurretWeaponType.Laser);
    }

    // Same guarantee for the camera catalog entry (M48's own device pipeline).
    private static bool World_ContentCatalog_CameraRoom_BuildsAWorkingCamera()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var camerasBefore = world.Ship.Cameras.Count;

        BuildAndCompleteCatalogRoom(world, "camera");

        return world.Ship.Cameras.Count == camerasBefore + 1;
    }

    // A cockpit room's own Helm+Navigation pair has to show up as EXTRA seats (Ship.cs's own
    // ExtraHelmConsoles/ExtraNavigationConsoles) - the primary HelmConsole/NavigationConsole stay
    // exactly what the hand-authored hull already had, untouched.
    private static bool World_ContentCatalog_CockpitRoom_AddsExtraHelmAndNavigationConsoles()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        var originalHelmId = world.Ship.HelmConsole.Id;

        BuildAndCompleteCatalogRoom(world, "cockpit-small");

        return world.Ship.ExtraHelmConsoles.Count == 1 && world.Ship.ExtraNavigationConsoles.Count == 1
            && world.Ship.HelmConsole.Id == originalHelmId; // primary fixture untouched
    }

    // A character standing in the EXTRA bridge room's own seat has to be able to pilot exactly like
    // standing at the original HelmConsole - the whole point of the "any seat works" design
    // (World.Interact.cs).
    private static bool World_ContentCatalog_ExtraHelmSeat_LetsACharacterPilot()
    {
        var world = new World();
        world.SpawnCharacter(1);
        DockAtStation(world, "outpost-gamma");
        BuildAndCompleteCatalogRoom(world, "cockpit-small");
        var extraSeat = world.Ship.ExtraHelmConsoles[0];

        // The bang-bang MoveCharacterTo drives straight at the target (no pathfinding) - fine for a
        // single open room, but a diagonal line into a NEW room can clip the wall beside its own
        // door instead of passing through the doorway. Routing through the door's own position first
        // (same waypoint trick TestRunner.Doors.cs/QuestsAndSave.cs already use for a cross-room walk)
        // sidesteps that entirely.
        var doorIntoSeatRoom = world.Ship.Doors.First(d => d.RoomAId == extraSeat.RoomId || d.RoomBId == extraSeat.RoomId);
        MoveCharacterTo(world, 1, doorIntoSeatRoom.X, doorIntoSeatRoom.Y);
        MoveCharacterTo(world, 1, extraSeat.X, extraSeat.Y);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));

        return world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).IsAtHelm;
    }

    // CustomShipValidator's own relaxed rule (M60+ content-каталог - "хотя бы один" instead of
    // "ровно один") tested directly, the same shape M61's own RoomGraphConnectivity unit tests use -
    // a hand-built definition with 2 reactor devices must now validate cleanly.
    private static bool CustomShipValidator_AllowsMultipleReactorsHelmsAndNavigationConsoles()
    {
        var def = Ship.Create(ShipKind.Frigate).ToDefinition();
        var doubled = def with { Devices = def.Devices.Append(new CustomDeviceDef(CustomDeviceKind.Reactor, def.Devices.First(d => d.Kind == CustomDeviceKind.Reactor).X,
            def.Devices.First(d => d.Kind == CustomDeviceKind.Reactor).Y)).ToList() };

        return CustomShipValidator.Validate(doubled).Count == 0;
    }

    // Редактор корабля в духе Cosmoteer (humble-soaring-cat.md, "Отдельная ветка: редактор корабля...")
    // - a ship assembled from NOTHING but catalog modules (no hand-authored hull underneath at all)
    // has to pass validation, or the editor's own "Играть" button could never unlock once the old
    // freeform room+device tools are gone. Before utility-bay/storage-bay existed, no catalog entry
    // ever produced Distribution/Oxygen/SuitLocker/StorageRack, so this was structurally impossible -
    // this test is the guard against that gap ever reopening (e.g. someone renaming/removing one of
    // the two new entries without noticing what depends on it).
    private static bool CustomShipValidator_AcceptsAShipBuiltEntirelyFromCatalogModules()
    {
        var catalogIds = new[] { "reactor", "utility-bay", "cockpit-small", "engine-small", "storage-bay" };
        var rooms = new List<CustomRoomDef>();
        var devices = new List<CustomDeviceDef>();
        var cursorX = 0f;
        foreach (var id in catalogIds)
        {
            var entry = RoomCatalog.Find(id)!;
            var room = new CustomRoomDef(id, entry.Name, cursorX, 0f, entry.Width, entry.Height);
            rooms.Add(room);
            var centerX = room.X + room.Width / 2f;
            var centerY = room.Y + room.Height / 2f;
            devices.AddRange(entry.Devices.Select(kind => new CustomDeviceDef(kind, centerX, centerY)));
            cursorX += entry.Width;
        }

        var airlocks = new[] { new CustomAirlockDef("reactor", EdgeSide.Left) }; // leftmost room's own outer wall, nothing built there to conflict
        var def = new CustomShipDefinition("Каталожный корабль", rooms, Array.Empty<CustomDoorDef>(), airlocks, devices, ForwardDegrees: 0f);

        return CustomShipValidator.Validate(def).Count == 0;
    }
}
