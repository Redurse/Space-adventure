using Anabiosis.Shared.Model;

// Direct user bug report ("они вообще никак не отображаются ни в редакторе ни в игре") - a shaped-
// footprint kind's own anchor tile can be a deliberately EMPTY corner of its shape (the turret
// kinds' new (4, 3) shape never occupies its own top-left corner at all - CustomDeviceFootprint.
// ShapedFootprint's own doc comment). TileShipBuilder.BuildDefinition used to require that SAME
// coordinate to also carry a TileCell.DeviceId before it would export a device at all - true for
// every ordinary rectangular footprint (whose anchor is always part of its own footprint), but
// false here, so the turret silently never made it into CustomShipDefinition.Devices at all. Fixed
// by iterating `deviceKinds` directly instead of piggy-backing on a Cells+DeviceId coincidence -
// this pins that fix down at the one layer testable without a real GraphicsDevice (the editor's own
// mirror of this bug, Game1.ShipEditor.Draw.cs's DrawEditorTiles, isn't separately unit tested,
// same as every other editor-level render path in this project).
internal static partial class TestRunner
{
    // Builds a tile grid with bare floor under every tile the turret's own shape actually touches
    // (CanPlaceHalfWidthDevice's own bare-floor precondition), places it exactly the way
    // Game1.ShipEditor.cs's BuildFootprintPlan would (anchor at (0, 0), unrotated), and confirms the
    // export finds it - the anchor tile itself (an empty corner of the shape) deliberately gets NO
    // floor at all, to prove the fix doesn't depend on it existing.
    private static bool TileShipBuilder_ShapedFootprintKind_AnchorNotPartOfOwnShape_StillExports()
    {
        var tiles = new TileGrid();
        var anchor = new TileCoord(0, 0);
        var shape = CustomDeviceFootprint.ShapedFootprint(CustomDeviceKind.TurretBallistic)!;
        foreach (var t in shape)
            tiles.SetFloor(new TileCoord(anchor.X + t.Offset.X, anchor.Y + t.Offset.Y), true);
        foreach (var t in shape)
        {
            var tile = new TileCoord(anchor.X + t.Offset.X, anchor.Y + t.Offset.Y);
            if (t.HalfSide is { } side)
                tiles.PlaceHalfWidthDevice(tile, side, "turret-1");
            else
                tiles.PlaceDevice(tile, "turret-1");
        }

        var deviceKinds = new Dictionary<TileCoord, CustomDeviceKind> { [anchor] = CustomDeviceKind.TurretBallistic };
        var (definition, errors) = TileShipBuilder.BuildDefinition(
            tiles, deviceKinds, new Dictionary<TileCoord, TileShipBuilder.EngineSpec>(), "Test Ship", forwardDegrees: 0f);

        if (definition is null || errors.Count > 0)
            return false;
        if (definition.Devices.Count != 1)
            return false;
        var device = definition.Devices[0];
        // Center of the (4, 3) bounding box relative to the anchor - (0,0) + (2, 1.5).
        return device.Kind == CustomDeviceKind.TurretBallistic && device.X == 2f && device.Y == 1.5f;
    }
}
