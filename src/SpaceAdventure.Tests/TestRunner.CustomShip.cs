using System.Linq;
using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

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
        new[] { new CustomDoorDef("a", "b") },
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
        return ship.Rooms.Count == 2
            && ship.Doors.Count == 1
            && ship.Doors[0].Connects("a") && ship.Doors[0].Connects("b")
            && ship.AirlockOuterDoors.Count == 1
            && ship.AirlockOuterDoors[0].RoomId == "b"
            && ship.ReactorBlock.RoomId == "a"
            && ship.HelmConsole.RoomId == "a"
            && ship.SuitLockers.Single().RoomId == "b";
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
}
