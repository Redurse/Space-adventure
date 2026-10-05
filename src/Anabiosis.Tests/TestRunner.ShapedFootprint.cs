using Anabiosis.Shared.Model;

// Direct user request (turret kinds' new (4, 3) shaped footprint, replacing the former plain 1x3
// column + separately-stamped wall "skirt", TurretMountSkirt.cs, since deleted) - pure data-level
// coverage for CustomDeviceFootprint.ShapedFootprint/RotateShapedFootprint, the one piece of this
// feature that doesn't need a real GraphicsDevice (Game1.ShipEditor.cs's own BuildFootprintPlan/
// CanPlaceDeviceFootprint/PlaceDeviceFootprint wrappers are exercised the same way every other
// editor-level method in this project already is - not separately unit tested, per
// CustomDeviceFootprint.cs's own rulebook comment).
internal static partial class TestRunner
{
    // The exact shape the user specified via their own legend (0 empty, 1 half-right(East),
    // 2 half-top(North), 3 half-left(West), 4 half-bottom(South), 5 full), decoded as "0440/1553/
    // 1003" over a 4-wide, 3-tall bounding box - pinned down explicitly so a future change to the
    // shape shows up as a clear, readable diff here instead of silently changing collision shape.
    private static bool CustomDeviceFootprint_ShapedFootprint_TurretUnrotated_MatchesUserSpecifiedShape()
    {
        var shape = CustomDeviceFootprint.ShapedFootprint(CustomDeviceKind.TurretBallistic);
        if (shape is null || shape.Count != 8)
            return false;

        bool Has(int x, int y, TileSide? halfSide) =>
            shape.Any(t => t.Offset == new TileCoord(x, y) && t.HalfSide == halfSide);

        return Has(1, 0, TileSide.South) && Has(2, 0, TileSide.South)
            && Has(0, 1, TileSide.East) && Has(1, 1, null) && Has(2, 1, null) && Has(3, 1, TileSide.West)
            && Has(0, 2, TileSide.East) && Has(3, 2, TileSide.West);
    }

    // Every turret kind shares the exact same shape (they all opted into IsShapedFootprintKind
    // together) - a non-turret, non-shaped kind gets null instead (the ordinary rectangle model).
    private static bool CustomDeviceFootprint_ShapedFootprint_AllFourTurretKindsShareTheSameShape()
    {
        var reference = CustomDeviceFootprint.ShapedFootprint(CustomDeviceKind.TurretBallistic);
        return CustomDeviceFootprint.ShapedFootprint(CustomDeviceKind.TurretLaser) is { } laser && laser.SequenceEqual(reference!)
            && CustomDeviceFootprint.ShapedFootprint(CustomDeviceKind.TurretMachineGun) is { } mg && mg.SequenceEqual(reference!)
            && CustomDeviceFootprint.ShapedFootprint(CustomDeviceKind.DefensiveTurret) is { } def && def.SequenceEqual(reference!)
            && CustomDeviceFootprint.ShapedFootprint(CustomDeviceKind.Reactor) is null;
    }

    // Rotating transposes every offset (dx, dy) -> (dy, dx) and relabels each HalfSide the same way
    // a compass direction's own (x, y) unit vector would transpose (West<->North, East<->South) -
    // matching TurretMountSkirt.cs's own former hand-written rotated branch for this same device
    // family, just derived generically here instead of as a second hand-authored list.
    private static bool CustomDeviceFootprint_RotateShapedFootprint_TransposesOffsetsAndRelabelsSides()
    {
        var unrotated = CustomDeviceFootprint.ShapedFootprint(CustomDeviceKind.TurretBallistic)!;
        var rotated = CustomDeviceFootprint.RotateShapedFootprint(unrotated);
        if (rotated.Count != 8)
            return false;

        bool Has(IReadOnlyList<CustomDeviceFootprint.ShapedFootprintTile> tiles, int x, int y, TileSide? halfSide) =>
            tiles.Any(t => t.Offset == new TileCoord(x, y) && t.HalfSide == halfSide);

        // Bounding box swaps too (Size(kind) is (4, 3) unrotated - DeviceFootprintSize's own (3, 4)
        // rotated convention), so every rotated offset must fit inside a 3-wide, 4-tall box.
        var fitsRotatedBox = rotated.All(t => t.Offset.X is >= 0 and < 3 && t.Offset.Y is >= 0 and < 4);

        return fitsRotatedBox
            && Has(rotated, 1, 0, TileSide.South) && Has(rotated, 2, 0, TileSide.South)
            && Has(rotated, 0, 1, TileSide.East) && Has(rotated, 1, 1, null)
            && Has(rotated, 0, 2, TileSide.East) && Has(rotated, 1, 2, null)
            && Has(rotated, 1, 3, TileSide.North) && Has(rotated, 2, 3, TileSide.North);
    }

    // Direct consequence of the transpose being its own inverse (unlike a true 4-state 90-degree
    // rotation) - turrets only ever have 2 orientations in this editor (a plain bool toggle, not a
    // 4-way cycle like the half-width kinds' own HalfSide), so rotating twice must land back on
    // exactly the original shape, not some 3rd/4th distinct orientation.
    private static bool CustomDeviceFootprint_RotateShapedFootprint_AppliedTwice_ReturnsToOriginal()
    {
        var unrotated = CustomDeviceFootprint.ShapedFootprint(CustomDeviceKind.TurretBallistic)!;
        var twiceRotated = CustomDeviceFootprint.RotateShapedFootprint(CustomDeviceFootprint.RotateShapedFootprint(unrotated));
        return twiceRotated.SequenceEqual(unrotated);
    }
}

internal static partial class TestRunner
{
    private static HashSet<(int X, int Y, TileSide? Side)> FacingCells(CustomDeviceKind kind, TileSide facing) =>
        CustomDeviceFootprint.ShapedFootprint(kind, facing)!.Select(t => (t.Offset.X, t.Offset.Y, t.HalfSide)).ToHashSet();

    private static bool CustomDeviceFootprint_ShapedFacing_NorthIsTheAuthoredShapeAndFourQuarterTurnsComeBack()
    {
        var kind = CustomDeviceKind.TurretLaser;
        var authored = CustomDeviceFootprint.ShapedFootprint(kind)!.Select(t => (t.Offset.X, t.Offset.Y, t.HalfSide)).ToHashSet();
        if (!FacingCells(kind, TileSide.North).SetEquals(authored))
            return false;

        // Turned a quarter: the box is 3 wide and 4 tall, no cell falls outside it, same number of cells.
        foreach (var facing in new[] { TileSide.East, TileSide.West })
        {
            var cells = FacingCells(kind, facing);
            if (cells.Count != authored.Count || cells.Any(c => c.X < 0 || c.X > 2 || c.Y < 0 || c.Y > 3))
                return false;
        }
        var south = FacingCells(kind, TileSide.South);
        return south.Count == authored.Count && south.All(c => c.X is >= 0 and <= 3 && c.Y is >= 0 and <= 2) && !south.SetEquals(authored);
    }

    private static bool CustomDeviceFootprint_ShapedFacing_WestMatchesTheOlderRotatedFlagForTheSymmetricTurretShape()
    {
        // A save from before four facings existed only says "rotated": that must still load as the same picture.
        var shape = CustomDeviceFootprint.ShapedFootprint(CustomDeviceKind.TurretBallistic)!;
        var transposed = CustomDeviceFootprint.RotateShapedFootprint(shape).Select(t => (t.Offset.X, t.Offset.Y, t.HalfSide)).ToHashSet();
        return CustomDeviceFootprint.ShapedFacing(rotated: true, stored: null) == TileSide.West
            && CustomDeviceFootprint.ShapedFacing(rotated: false, stored: null) == TileSide.North
            && FacingCells(CustomDeviceKind.TurretBallistic, TileSide.West).SetEquals(transposed);
    }
}
