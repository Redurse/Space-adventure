namespace Anabiosis.Server;

// Who is mending something this tick (a damaged device with a wrench or screwdriver in hand, a broken door, the engineer's remote repair): the snapshot tells every
// client, which plays the repair melody for them (Audio/ShipAmbience.Characters.cs). Rebuilt every step by the repair code.
public sealed partial class World
{
    private readonly HashSet<int> _repairingNow = new();
}
