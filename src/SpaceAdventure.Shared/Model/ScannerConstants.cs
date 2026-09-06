namespace SpaceAdventure.Shared.Model;

// The scanner console's own geometry/timing (World.Scanner.cs is the actual authority on what gets
// detected and when the next pulse is allowed - these values only drive the client's decorative
// cone/ring and cooldown display, GalaxyMapPanel.Scanner.cs's own doc comments), shared here for the
// same reason InteractionConstants is: two independently hand-typed "must match" literals is how
// one of them quietly drifts.
public static class ScannerConstants
{
    public const float RangeUnits = 1080f;
    public const float SweepHalfAngleDegrees = 12f;
    public const float PingCooldownSeconds = 15f;
}
