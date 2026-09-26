namespace Anabiosis.Shared.Model;

// Direct user bug report ("некоторые устройства в игре не отображаются а в редакторе они видны") -
// every CustomDeviceKind listed in Kinds below has no dedicated mechanic of its own (same "заготовка,
// функционал добавим поэтапно" status CustomDeviceKind.Junction already carried before JunctionBox
// gave it a real fixture - JunctionBox.cs's own doc comment) - one shared record for all of them,
// rather than copy-pasting a JunctionBox-style one-off record another twenty-odd times. Purely
// decorative: no on/off, no interaction, nothing but a position and which kind it is.
// HalfSide - meaningful only when CustomDeviceFootprint.IsHalfWidthKind(Kind) (Fabricator/
// Deconstructor, direct user request "по аналогии... полтора на 3") - see HelmConsole.cs's own doc
// comment for the exact reasoning (same shape, same convention, just reused for a device that isn't
// one of the two dedicated Ship-side fixtures).
public sealed record DecorativeDevice(string Id, string RoomId, CustomDeviceKind Kind, float X, float Y, bool Rotated = false, TileSide HalfSide = TileSide.East)
{
    public Vec2 Position => new(X, Y);

    // Every kind that reaches this record instead of its own dedicated Ship-side object. Engine
    // Small/Medium/Large/WarpEngine are unreachable from the current editor palette (replaced by the
    // real ShipEngine mechanic) but kept here so an old save round-trips instead of silently losing
    // them. TripleDoor/ConstructionBench/Fabricator/Deconstructor/WeaponWorkbench/Bed/ShuttleHangar
    // already have real DeviceSkin art (DeviceSkin.Face) - ShipRenderer picks that when it matches
    // this device's own Kind, falling back to the generic tinted-swatch look everything else here
    // still gets.
    public static readonly CustomDeviceKind[] Kinds =
    {
        CustomDeviceKind.EngineSmall, CustomDeviceKind.EngineMedium, CustomDeviceKind.EngineLarge, CustomDeviceKind.WarpEngine,
        CustomDeviceKind.ShuttleHangar, CustomDeviceKind.DroneHangar,
        CustomDeviceKind.SmallStorage, CustomDeviceKind.LargeStorage, CustomDeviceKind.Morgue, CustomDeviceKind.FuelRodStorage,
        CustomDeviceKind.ConstructionBench, CustomDeviceKind.Fabricator, CustomDeviceKind.Deconstructor, CustomDeviceKind.WeaponWorkbench,
        CustomDeviceKind.PowerConduit,
        CustomDeviceKind.Table, CustomDeviceKind.Chair, CustomDeviceKind.Sofa, CustomDeviceKind.Bed, CustomDeviceKind.Nightstand,
        CustomDeviceKind.Spotlight, CustomDeviceKind.Lamp, CustomDeviceKind.DecorativePlant,
        CustomDeviceKind.DefensiveTurret, CustomDeviceKind.ShieldGeneratorSmall, CustomDeviceKind.ShieldGeneratorLarge, CustomDeviceKind.WeaponPanel,
        CustomDeviceKind.TripleDoor,
    };
}
