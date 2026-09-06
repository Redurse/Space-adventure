using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    // Direct user request ("навигационная панель и сканер были размерами в 3 на 2 тайла... его
    // можно поворачивать") - Helm/Navigation are now a real 3x2 (not 1x1 or square) footprint, and
    // TileShipBuilder.BuildDefinition must export the device's CENTER using whichever of Width/
    // Height actually applies once a placed instance's own rotation flag is taken into account.
    private static bool CustomDeviceFootprint_HelmAndNavigation_AreThreeByTwo()
    {
        var (helmWidth, helmHeight) = CustomDeviceFootprint.Size(CustomDeviceKind.Helm);
        var (navWidth, navHeight) = CustomDeviceFootprint.Size(CustomDeviceKind.Navigation);
        return helmWidth == 3 && helmHeight == 2 && navWidth == 3 && navHeight == 2;
    }

    private static TileGrid BuildSmallRoomWithOneDeviceTile(TileCoord deviceAnchor)
    {
        var tiles = new TileGrid();
        for (var x = 0; x < 6; x++)
            for (var y = 0; y < 6; y++)
                tiles.SetFloor(new TileCoord(x, y), true);
        tiles.PlaceDevice(deviceAnchor, "device-0");
        return tiles;
    }

    private static bool TileShipBuilder_UnrotatedHelm_ExportsCenterUsingAuthoredWidthHeight()
    {
        var anchor = new TileCoord(1, 1);
        var tiles = BuildSmallRoomWithOneDeviceTile(anchor);
        var deviceKinds = new Dictionary<TileCoord, CustomDeviceKind> { [anchor] = CustomDeviceKind.Helm };
        var (definition, errors) = TileShipBuilder.BuildDefinition(
            tiles, deviceKinds, new Dictionary<TileCoord, TileShipBuilder.EngineSpec>(), "Тест", 0f);
        if (definition is null || errors.Count > 0)
            return false;
        var device = definition.Devices.SingleOrDefault(d => d.Kind == CustomDeviceKind.Helm);
        // Unrotated 3x2: center = anchor + (1.5, 1.0).
        return device is not null && MathF.Abs(device.X - 2.5f) < 0.01f && MathF.Abs(device.Y - 2f) < 0.01f;
    }

    private static bool TileShipBuilder_RotatedHelm_ExportsCenterUsingSwappedWidthHeight()
    {
        var anchor = new TileCoord(1, 1);
        var tiles = BuildSmallRoomWithOneDeviceTile(anchor);
        var deviceKinds = new Dictionary<TileCoord, CustomDeviceKind> { [anchor] = CustomDeviceKind.Helm };
        var deviceRotations = new Dictionary<TileCoord, bool> { [anchor] = true };
        var (definition, errors) = TileShipBuilder.BuildDefinition(
            tiles, deviceKinds, new Dictionary<TileCoord, TileShipBuilder.EngineSpec>(), "Тест", 0f,
            deviceRotations: deviceRotations);
        if (definition is null || errors.Count > 0)
            return false;
        var device = definition.Devices.SingleOrDefault(d => d.Kind == CustomDeviceKind.Helm);
        // Rotated (swapped to 2x3): center = anchor + (1.0, 1.5).
        return device is not null && MathF.Abs(device.X - 2f) < 0.01f && MathF.Abs(device.Y - 2.5f) < 0.01f;
    }
}
