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
        // Direct user request - real footprints for the "производство" tab's own workbenches
        // (previously all (1,1), same placeholder every other not-yet-functional kind still has).
        CustomDeviceKind.ConstructionBench => (2, 3),
        CustomDeviceKind.Fabricator => (3, 3),
        CustomDeviceKind.Deconstructor => (3, 3),
        CustomDeviceKind.WeaponWorkbench => (2, 4),
        // Direct user request - every turret is a real 3x3 mount, not a single point.
        CustomDeviceKind.TurretBallistic => (3, 3),
        CustomDeviceKind.TurretLaser => (3, 3),
        CustomDeviceKind.TurretMachineGun => (3, 3),
        CustomDeviceKind.DefensiveTurret => (3, 3),
        // Direct user request - a bed reads as furniture you lie down IN, not a single tile.
        CustomDeviceKind.Bed => (1, 2),
        // Direct user request - a shuttle hangar is a genuinely large bay.
        CustomDeviceKind.ShuttleHangar => (5, 6),
        // Direct user request ("тройная дверь") - spans 3 tiles like a real wide doorway; rotation
        // (R, already free for every device kind) gives the 3x1 orientation with no extra code.
        CustomDeviceKind.TripleDoor => (1, 3),
        _ => (1, 1),
    };
}
