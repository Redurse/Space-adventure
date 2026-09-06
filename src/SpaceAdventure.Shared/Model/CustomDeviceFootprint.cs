namespace SpaceAdventure.Shared.Model;

// How many tiles square a catalog device occupies in the tile-model ship editor/builder - 1x1 for
// every kind except the Reactor, which is a real 4x4-tile footprint everywhere in the game
// (ShipRenderer.ReactorBlockSize). Shared between Game1.ShipEditor.cs (the editor's own placement/
// removal) and TileShipBuilder.cs (converting a saved tile grid into a CustomShipDefinition) - both
// used to keep their own identical copy of this one-line mapping, the same "must match" drift risk
// InteractionConstants/ScannerConstants already fixed elsewhere.
public static class CustomDeviceFootprint
{
    public static int Size(CustomDeviceKind kind) => kind == CustomDeviceKind.Reactor ? 4 : 1;
}
