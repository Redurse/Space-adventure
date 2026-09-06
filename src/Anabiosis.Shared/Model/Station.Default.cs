namespace Anabiosis.Shared.Model;

// The one hand-authored bit left of the old fixed per-kind layout (Station.Procedural.cs does all
// the real work now) - a single fixed-seed station used only where tests need *a* station without
// caring what shape it is.
public sealed partial class Station
{
    // Fixed location in the docking-approach field space - the same spot for every station, since
    // only one is ever "the station you're approaching" at a time (World.StationDocking.cs).
    private static readonly Vec2 WorldCenter = new(150f, 150f);

    public static Station CreateDefault() => CreateProcedural("test-station", StationKind.Trade, Vec2.Zero);
}
