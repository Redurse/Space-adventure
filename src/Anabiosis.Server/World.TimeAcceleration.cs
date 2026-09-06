namespace Anabiosis.Server;

// M57 - "режим ускорения времени": a captain-tab control that runs the simulation itself faster,
// separate from CruiseEngaged (World.ShipField.cs, M53's own "режим ускорения" - a THRUST
// multiplier, not a time one; the two happen to share the Russian word but are unrelated
// mechanics). Deliberately NOT implemented as a deltaSeconds multiplier - project history
// (continue.md's old M33/M34 TimeCompressionFactor=8) already hit exactly that trap: a
// fixed-degrees-per-tick turn rate multiplied by 8 overshot its own convergence threshold every
// tick and oscillated forever. Instead GameServer.Tick() runs this many ordinary, unscaled 1/30s
// physics steps per real tick - every existing threshold/rate constant keeps seeing the same small
// deltaSeconds it was always tuned against, just more times per real second.
public sealed partial class World
{
    private static readonly int[] ValidTimeAccelerationLevels = { 1, 10, 100, 1000 };

    public int TimeAccelerationLevel { get; private set; } = 1;

    // Only a helm-seated character may change it (World.cs's ApplyCommand gates the call the same
    // way as ToggleCruisePressed) - silently ignores an unrecognized level rather than throwing,
    // the same "trust the client but don't crash on garbage" stance ToggleCruise's own gate takes.
    public void SetTimeAccelerationLevel(int level)
    {
        if (Array.IndexOf(ValidTimeAccelerationLevels, level) >= 0)
            TimeAccelerationLevel = level;
    }

    // Called from World.Interact.cs the moment a character stands up from the helm - if nobody is
    // left actually watching the console, the simulation shouldn't keep racing ahead unsupervised.
    public void ResetTimeAccelerationIfNobodyAtHelm()
    {
        if (!_characters.Values.Any(c => c.IsAtHelm))
            TimeAccelerationLevel = 1;
    }
}
