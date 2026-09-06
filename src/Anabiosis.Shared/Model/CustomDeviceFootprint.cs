namespace Anabiosis.Shared.Model;

// How many tiles a catalog device occupies in the tile-model ship editor/builder - 1x1 for most
// kinds, a real 4x4-tile footprint for the Reactor (ShipRenderer.ReactorBlockSize), and a real 3x2
// (3 tiles long, 2 wide) footprint for Helm/Navigation (direct user request - the console reads as
// a genuine console, not a single point, without needing device rotation to place it). Shared
// between Game1.ShipEditor.cs (the editor's own placement/removal) and TileShipBuilder.cs
// (converting a saved tile grid into a CustomShipDefinition) - both used to keep their own
// identical copy of this one mapping, the same "must match" drift risk InteractionConstants/
// ScannerConstants already fixed elsewhere.
public static class CustomDeviceFootprint
{
    public static (int Width, int Height) Size(CustomDeviceKind kind) => kind switch
    {
        CustomDeviceKind.Reactor => (4, 4),
        CustomDeviceKind.Helm => (3, 2),
        CustomDeviceKind.Navigation => (3, 2),
        _ => (1, 1),
    };
}
