using Anabiosis.Server;
using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    // An L-shaped room: a 4-wide top arm (y in [0,2)) plus a 2-wide arm below its LEFT half (x in
    // [0,2), y in [2,4)) - bbox is 4x4, but the bottom-RIGHT 2x2 corner (x in [2,4), y in [2,4)) is
    // a genuine notch with no floor there at all.
    private static CustomRoomDef LShapedRoom(string id, string name, float originX, float originY) =>
        new(id, name, new[]
        {
            new RectF(originX, originY, 4, 2),
            new RectF(originX, originY + 2, 2, 2),
        });

    // A separate room placed entirely inside the L's own bounding-box notch must NOT be flagged as
    // overlapping it - the old bbox-only Overlaps check (pre-M89) would have falsely rejected this
    // as a real conflict, since it only ever compared the two rooms' bounding boxes.
    private static bool NonRectangularRoom_Validator_DoesNotFalselyFlagOverlapInsideItsOwnNotch()
    {
        var def = new CustomShipDefinition(
            "Тест",
            new[] { LShapedRoom("l", "Г-отсек", 0, 0), new CustomRoomDef("n", "Сосед", 2, 2, 2, 2) },
            Array.Empty<CustomDoorDef>(), Array.Empty<CustomAirlockDef>(), Array.Empty<CustomDeviceDef>(), 0f);
        var errors = CustomShipValidator.Validate(def);
        return !errors.Any(e => e.Contains("перекрываются"));
    }

    // A separate room that genuinely intersects one of the L's real subrects must still be flagged.
    private static bool NonRectangularRoom_Validator_StillRejectsGenuineOverlapWithASubrect()
    {
        var def = new CustomShipDefinition(
            "Тест",
            new[] { LShapedRoom("l", "Г-отсек", 0, 0), new CustomRoomDef("n", "Сосед", 1, 1, 2, 2) },
            Array.Empty<CustomDoorDef>(), Array.Empty<CustomAirlockDef>(), Array.Empty<CustomDeviceDef>(), 0f);
        var errors = CustomShipValidator.Validate(def);
        return errors.Any(e => e.Contains("перекрываются"));
    }

    // FindRoomPairOverlaps must connect the L's bottom arm to a real rectangular neighbor touching
    // its west side, without inventing a spurious touch from the bbox's own notch.
    private static bool NonRectangularRoom_FindRoomPairOverlaps_FindsRealTouchNotBoundingBoxGhost()
    {
        var lRoom = LShapedRoom("l", "Г-отсек", 0, 0);
        var neighbor = new CustomRoomDef("n", "Сосед", -2, 2, 2, 2); // touches the bottom arm's west side
        var overlaps = ShipLayoutGeometry.FindRoomPairOverlaps(new[] { lRoom, neighbor });
        return overlaps.Count == 1 && overlaps[0].OverlapLength == 2f;
    }

    // A "staircase" of 2 subrects both reaching the room's own top (Y=0) bbox edge - an airlock
    // authored on that side is geometrically ambiguous (which piece's edge does it actually sit on?)
    // and must be rejected.
    private static CustomRoomDef StaircaseRoom() => new("s", "Ступени", new[]
    {
        new RectF(0, 0, 2, 4),
        new RectF(2, 0, 2, 2),
    });

    private static bool NonRectangularRoom_Validator_RejectsAirlockOnAmbiguousMultiSubrectSide()
    {
        var def = new CustomShipDefinition(
            "Тест", new[] { StaircaseRoom() }, Array.Empty<CustomDoorDef>(),
            new[] { new CustomAirlockDef("s", EdgeSide.Top) }, Array.Empty<CustomDeviceDef>(), 0f);
        var errors = CustomShipValidator.Validate(def);
        return errors.Any(e => e.Contains("ровно один кусок"));
    }

    // The same staircase's Bottom side (Y=4) is unambiguous - only the tall 2x4 piece reaches it
    // (the short 2x2 piece stops at Y=2) - so an airlock there must NOT be rejected on that ground.
    private static bool NonRectangularRoom_Validator_AcceptsAirlockOnUnambiguousSide()
    {
        var def = new CustomShipDefinition(
            "Тест", new[] { StaircaseRoom() }, Array.Empty<CustomDoorDef>(),
            new[] { new CustomAirlockDef("s", EdgeSide.Bottom) }, Array.Empty<CustomDeviceDef>(), 0f);
        var errors = CustomShipValidator.Validate(def);
        return !errors.Any(e => e.Contains("ровно один кусок"));
    }

    // M90 (humble-soaring-cat.md) end-to-end integration test - builds a REAL, playable World from
    // an L-shaped custom ship, walks a character all the way into the far arm (through the door,
    // across the internal seam between the L's own two pieces, with no wall in the way), breaches an
    // unrelated wall elsewhere on the ship, and confirms the character's own room identity is still
    // reported correctly. This is exactly the failure class TileMovement.RoomIdAt's own doc comment
    // describes having been bitten by before (a region-based lookup silently breaking room identity
    // once enough interior walls got shot through and TileGrid merged two SealedRegions) - RoomIdAt
    // stays a pure Room.Contains rectangle-list scan, never touching SealedRegion at all, so that
    // failure class cannot reappear regardless of what elsewhere on the ship gets breached.
    private static CustomShipDefinition BuildLShapedCustomShipDefinition()
    {
        // Both pieces are 3+ tall/wide, and the far arm (bottomArm) reaches topArm's OWN right edge
        // (x=3) so the seam between them fully covers that column - keeping the "genuine notch" (no
        // floor below) on the OPPOSITE side (x=0) from where room "b"'s door attaches (x=4). Two
        // lessons learned by trial: (1) a room narrower than 3 in either dimension has every one of
        // its own tiles on its own wall ring (TileGridRasterizer.FromRooms walls a room's own
        // outermost floor tiles, not a separate ring outside them) with no interior tile left
        // walkable at all - a pre-existing fact of this game's tile model. (2) a corner tile that is
        // simultaneously "touches a neighbour room" (should stay open) AND "touches this room's own
        // genuine notch" (should wall) can't have both - keeping the notch and the neighbour on
        // different sides avoids ever needing one tile to satisfy both roles.
        var lRoom = new CustomRoomDef("a", "Г-отсек", new[]
        {
            new RectF(0, 0, 4, 3),
            new RectF(1, 3, 3, 3),
        });
        var bRoom = new CustomRoomDef("b", "Мостик", 4, 0, 4, 3);
        return new CustomShipDefinition(
            "Тестовый Г-корабль",
            new[] { lRoom, bRoom },
            new[] { new CustomDoorDef("a", "b") },
            new[] { new CustomAirlockDef("b", EdgeSide.Right) },
            new[]
            {
                new CustomDeviceDef(CustomDeviceKind.Helm, 6f, 1f),
                new CustomDeviceDef(CustomDeviceKind.Reactor, 4.5f, 0.3f),
                new CustomDeviceDef(CustomDeviceKind.Distribution, 5f, 0.3f),
                new CustomDeviceDef(CustomDeviceKind.Navigation, 5.5f, 0.3f),
                new CustomDeviceDef(CustomDeviceKind.Engine, 6f, 0.3f),
                new CustomDeviceDef(CustomDeviceKind.Oxygen, 6.5f, 0.3f),
                new CustomDeviceDef(CustomDeviceKind.SuitLocker, 7f, 0.3f),
                new CustomDeviceDef(CustomDeviceKind.StorageRack, 7.5f, 0.3f),
            },
            0f);
    }

    private static bool NonRectangularRoom_World_CharacterInFarArm_RoomIdStaysCorrectAfterUnrelatedBreach()
    {
        var world = new World(ShipKind.Custom, BuildLShapedCustomShipDefinition());
        world.SpawnCharacter(1);
        MoveCharacterTo(world, 1, targetX: 2f, targetY: 4f); // the L's far arm - through the door and across its own internal seam

        world.DebugBreachWallBlock("b"); // an unrelated wall breach elsewhere on the ship

        var character = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1);
        return world.Ship.Rooms.Single(r => r.Contains(new Vec2(character.X, character.Y))).Id == "a";
    }
}
