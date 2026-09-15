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
        return device is not null && !device.Rotated && MathF.Abs(device.X - 2.5f) < 0.01f && MathF.Abs(device.Y - 2f) < 0.01f;
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
        return device is not null && device.Rotated && MathF.Abs(device.X - 2f) < 0.01f && MathF.Abs(device.Y - 2.5f) < 0.01f;
    }

    // Direct user bug report ("в кокпите в самой игре устройства не повернуты как в редакторе") -
    // the CENTER-position math above was already correct, but CustomDeviceDef never carried the
    // Rotated flag itself onward at all - so a rotated Helm/Navigation always came back unrotated
    // once it reached a real Ship (HelmConsole/NavigationConsole had nowhere to store it, and
    // ShipRenderer always drew/hovered/clicked it at its base 3x2 footprint regardless). This is the
    // full pipeline this session's fix threads it through: TileShipBuilder.BuildDefinition ->
    // CustomDeviceDef.Rotated -> Ship.FromCustomDefinition -> HelmConsole/NavigationConsole.Rotated.
    private static bool Ship_FromCustomDefinition_RotatedHelmAndNavigation_KeepTheirOwnRotatedFlag()
    {
        var tiles = new TileGrid();
        for (var x = 0; x < 10; x++)
            for (var y = 0; y < 10; y++)
                tiles.SetFloor(new TileCoord(x, y), true);
        // A plain wall ring with one door standing in for the airlock CustomShipValidator requires
        // ("Нужен хотя бы один шлюзовой люк во внешний космос") - genuinely nothing beyond it, so
        // TileShipBuilder's own SideIsAirlock picks it up same as any hand-painted hull's would.
        for (var x = -1; x <= 10; x++)
        {
            tiles.SetFloor(new TileCoord(x, -1), true);
            tiles.SetWall(new TileCoord(x, -1), TileWallKind.Solid);
            tiles.SetFloor(new TileCoord(x, 10), true);
            tiles.SetWall(new TileCoord(x, 10), TileWallKind.Solid);
        }
        for (var y = -1; y <= 10; y++)
        {
            tiles.SetFloor(new TileCoord(-1, y), true);
            tiles.SetWall(new TileCoord(-1, y), TileWallKind.Solid);
            tiles.SetFloor(new TileCoord(10, y), true);
            tiles.SetWall(new TileCoord(10, y), TileWallKind.Solid);
        }
        tiles.SetWall(new TileCoord(10, 5), TileWallKind.Door);

        var helmAnchor = new TileCoord(0, 0);
        var navAnchor = new TileCoord(2, 0); // unrotated Helm is 3 wide - stay clear of it
        tiles.PlaceDevice(helmAnchor, "helm");
        tiles.PlaceDevice(new TileCoord(navAnchor.X, navAnchor.Y), "nav");
        var deviceKinds = new Dictionary<TileCoord, CustomDeviceKind>
        {
            [helmAnchor] = CustomDeviceKind.Helm,
            [navAnchor] = CustomDeviceKind.Navigation,
            [new TileCoord(4, 0)] = CustomDeviceKind.Reactor,
            [new TileCoord(4, 4)] = CustomDeviceKind.Distribution,
            [new TileCoord(5, 4)] = CustomDeviceKind.Engine,
            [new TileCoord(6, 4)] = CustomDeviceKind.Oxygen,
            [new TileCoord(7, 4)] = CustomDeviceKind.SuitLocker,
            [new TileCoord(8, 4)] = CustomDeviceKind.StorageRack,
        };
        foreach (var (anchor, kind) in deviceKinds)
        {
            if (anchor == helmAnchor || anchor == navAnchor)
                continue; // already placed above with a fixed id
            tiles.PlaceDevice(anchor, $"device-{anchor.X}-{anchor.Y}");
        }
        // Helm rotated (2x3), Navigation left unrotated (3x2) - proves the flag travels per-device,
        // not as some ship-wide toggle.
        var deviceRotations = new Dictionary<TileCoord, bool> { [helmAnchor] = true };

        var (definition, errors) = TileShipBuilder.BuildDefinition(
            tiles, deviceKinds, new Dictionary<TileCoord, TileShipBuilder.EngineSpec>(), "Тест", 0f,
            deviceRotations: deviceRotations);
        if (definition is null || errors.Count > 0)
            return false;

        var ship = Ship.FromCustomDefinition(definition);
        return ship.HelmConsole.Rotated && !ship.NavigationConsole.Rotated;
    }
}
