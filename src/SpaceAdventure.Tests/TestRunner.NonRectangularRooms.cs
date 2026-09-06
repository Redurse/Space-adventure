using SpaceAdventure.Shared.Model;

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
}
