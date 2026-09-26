using System;
using System.Linq;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // Two rooms side by side, sharing the wall at X=4: everything the Ship Editor's validator
    // requires (game_design.md's own fixed hulls all have the same handful of required systems),
    // one door across the shared wall, one airlock on room "b"'s free right side.
    private static CustomShipDefinition BuildSimpleCustomShipDefinition() => new(
        "Тестовый корабль",
        new[]
        {
            new CustomRoomDef("a", "Мостик", 0, 0, 4, 4),
            new CustomRoomDef("b", "Шлюз", 4, 0, 4, 4),
        },
        new[] { new CustomDoorDef(4, 2, true, true) }, // shared wall at X=4, centered on its Y=[0,4] span
        new[] { new CustomAirlockDef("b", EdgeSide.Right) },
        new[]
        {
            new CustomDeviceDef(CustomDeviceKind.Reactor, 1, 1),
            new CustomDeviceDef(CustomDeviceKind.Distribution, 2, 1),
            new CustomDeviceDef(CustomDeviceKind.Helm, 1, 2),
            new CustomDeviceDef(CustomDeviceKind.Navigation, 2, 2),
            new CustomDeviceDef(CustomDeviceKind.Engine, 1, 3),
            new CustomDeviceDef(CustomDeviceKind.Oxygen, 2, 3),
            new CustomDeviceDef(CustomDeviceKind.SuitLocker, 5, 1),
            new CustomDeviceDef(CustomDeviceKind.StorageRack, 5, 2),
        },
        0f);

    private static bool CustomShip_Validator_RejectsBlankDefinition() =>
        CustomShipValidator.Validate(CustomShipDefinition.Empty).Count > 0;

    private static bool CustomShip_Validator_AcceptsSimpleValidDefinition() =>
        CustomShipValidator.Validate(BuildSimpleCustomShipDefinition()).Count == 0;

    private static bool CustomShip_FromDefinition_BuildsRoomsDoorsAndAirlock()
    {
        var ship = Ship.FromCustomDefinition(BuildSimpleCustomShipDefinition());
        // Doors.Count == 2 now (humble-soaring-cat.md, "убрать AirlockOuterDoor как отдельный тип") -
        // the interior door between "a"/"b" plus the hull door, both live in the one list; VacuumDoors
        // is a filter over that same list, not a separate source.
        return ship.Rooms.Count == 2
            && ship.Doors.Count == 2
            && ship.Doors.Count(d => !d.LeadsToVacuum) == 1
            && ship.Doors.First(d => !d.LeadsToVacuum).Connects("a") && ship.Doors.First(d => !d.LeadsToVacuum).Connects("b")
            && ship.VacuumDoors.Count == 1
            && ship.VacuumDoors[0].RoomAId == "b"
            && ship.ReactorBlock.RoomId == "a"
            && ship.HelmConsole.RoomId == "a"
            && ship.SuitLockers.Single().RoomId == "b";
    }

    // The reactor now always draws its own fixed 4x4-tile texture (ShipRenderer.ReactorBlockSize) -
    // SizeScale must stay at its default 1f for a custom ship regardless of how big the room the
    // player drew around it is. Before this fix, SizeScale was derived from the room's own
    // dimensions (Min(Width,Height)*0.6/(40/48)) - a leftover from when the reactor was a small
    // icon meant to visually fill its room; on room "a" (4x4) that leftover formula gave ~2.88,
    // over 11 tiles wide once applied to the new fixed 4x4 texture - exactly the "huge glowing
    // reactor" bug reported when playing a Ship-Editor-built ship.
    private static bool CustomShip_FromDefinition_ReactorSizeScaleStaysDefault()
    {
        var ship = Ship.FromCustomDefinition(BuildSimpleCustomShipDefinition());
        return ship.ReactorBlock.SizeScale == 1f;
    }

    // Room "a"'s shared side with "b" (X=4) and "b"'s airlock side (right) must carry no OUTER
    // WallBlocks (Ship.Custom.cs's BuildWallBlocks skips exactly those) - only the three plain
    // exterior sides of each room (top/bottom/left of "a", top/bottom of "b") get one. The shared
    // "a"/"b" boundary itself isn't blockless any more, though (enemy/weapon overhaul - "внутренние
    // стены корабля также блокировали снаряды врага"): Ship.cs's GenerateInteriorWallBlocks now
    // covers it separately, tagged IsInterior so it still doesn't vent (World.Atmosphere.cs) - the
    // door cut into that boundary is filtered out of it exactly like an outer block would be.
    private static bool CustomShip_FromDefinition_SkipsWallBlocksOnInteriorAndAirlockSides()
    {
        var ship = Ship.FromCustomDefinition(BuildSimpleCustomShipDefinition());
        var outerBlocks = ship.WallBlocks.Where(w => !w.IsInterior).ToList();
        var expected = 4 + 4 + 4 // room a: top, bottom, left
            + 4 + 4;             // room b: top, bottom (no left - interior, no right - airlock)
        return outerBlocks.Count == expected
            && outerBlocks.All(w => w.X != 4f) // no OUTER block sits on the shared/airlock wall line
            && ship.WallBlocks.Any(w => w.IsInterior && w.X == 4f); // but the interior boundary itself is covered now
    }

    private static bool CustomShip_World_CharacterWalksThroughPlacedDoor()
    {
        var world = new World(ShipKind.Custom, BuildSimpleCustomShipDefinition());
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, 5f, 2f);
        var character = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return world.Ship.Rooms.Single(r => r.Contains(new Vec2(character.X, character.Y))).Id == "b";
    }

    private static bool CustomShip_World_SnapshotCarriesCustomKindAndForwardDegrees()
    {
        var definition = BuildSimpleCustomShipDefinition() with { ForwardDegrees = 90f };
        var world = new World(ShipKind.Custom, definition);
        var snapshot = world.CreateSnapshot();
        return snapshot.CurrentShipKind == ShipKind.Custom && snapshot.ShipForwardDegrees == 90f;
    }

    // Direct user bug report ("не могу зайти в игру на корабле cosmoteer1") - a real player-built
    // ship with zero CustomAirlockDef entries (allowed ever since CustomShipValidator's own airlock-
    // count check was removed, direct user request) used to crash the very first World construction:
    // every new World starts docked (this constructor's own "a fresh run starts docked" comment) and
    // immediately touches Station, whose GetOrCreateStation anchored itself on
    // Ship.VacuumDoors.First() - an empty-sequence exception the client swallowed silently
    // (Game1.Menu.cs's FinishPendingSessionIfReady), so the player just sat on the Ship Editor screen
    // forever with no error. World must now build, dock, and snapshot cleanly with zero airlocks.
    private static bool CustomShip_World_ZeroAirlocks_DoesNotCrashOnConstructionOrDocking()
    {
        var definition = BuildSimpleCustomShipDefinition() with { Airlocks = Array.Empty<CustomAirlockDef>() };
        var world = new World(ShipKind.Custom, definition);
        if (world.Ship.VacuumDoors.Count != 0)
            return false; // setup problem - this test only means anything with genuinely zero airlocks
        if (!world.IsDocked)
            return false; // every fresh World starts docked - this is what used to crash right here

        world.SpawnCharacter(1);
        world.Step(RealtimeStep); // exercises CreateSnapshot's own docked-layout/station rendering path
        return world.CreateSnapshot().Station.Rooms.Count > 0; // a station still generated, just unreachable
    }

    // Direct user bug report ("некоторые устройства в игре не отображаются а в редакторе они
    // видны") - every DecorativeDevice.Kinds entry used to have no Ship.FromCustomDefinition case at
    // all, so placing one in the editor produced nothing in the actual built Ship (and therefore
    // nothing in WorldSnapshot, nothing to render). Picks two representative kinds - one with real
    // DeviceSkin art (Bed) and one with only the generic tinted-swatch fallback (Table) - to cover
    // both DrawDecorativeDevice branches without enumerating all ~25.
    private static bool CustomShip_FromDefinition_BuildsDecorativeDevicesForOtherwiseUnhandledKinds()
    {
        var definition = BuildSimpleCustomShipDefinition() with
        {
            Devices = BuildSimpleCustomShipDefinition().Devices
                .Append(new CustomDeviceDef(CustomDeviceKind.Bed, 5f, 1f))
                .Append(new CustomDeviceDef(CustomDeviceKind.Table, 5f, 2f, Rotated: true))
                .ToList(),
        };
        var ship = Ship.FromCustomDefinition(definition);
        if (ship.DecorativeDevices.Count != 2)
            return false;
        var bed = ship.DecorativeDevices.SingleOrDefault(d => d.Kind == CustomDeviceKind.Bed);
        var table = ship.DecorativeDevices.SingleOrDefault(d => d.Kind == CustomDeviceKind.Table);
        if (bed is null || table is null)
            return false;
        if (bed.RoomId != "b" || table.RoomId != "b" || !table.Rotated)
            return false;

        // Round-trips back into CustomDeviceDef (Ship.Convert.cs's own ToDefinition), same "survives
        // a rebuild" guarantee every other device kind already has.
        var roundTripped = ship.ToDefinition();
        return roundTripped.Devices.Count(d => d.Kind == CustomDeviceKind.Bed) == 1
            && roundTripped.Devices.Count(d => d.Kind == CustomDeviceKind.Table && d.Rotated) == 1;
    }
}
