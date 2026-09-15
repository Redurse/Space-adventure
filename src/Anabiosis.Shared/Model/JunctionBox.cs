namespace Anabiosis.Shared.Model;

// Direct user bug report ("сделай чтобы щитки отображались в игре а не была просто пустота") - the
// Ship Editor's "Щиток" (CustomDeviceKind.Junction) palette item was placeable and rendered fine in
// the editor's own preview, but had no case at all in Ship.FromCustomDefinition - so a real played
// ship simply had nothing there, an empty tile where the editor showed a device. Purely decorative,
// same "no on/off state of its own" shape WallLamp already uses - not tied to the wiring system's
// OWN, unrelated "junction box" concept (World.Wiring.cs's per-power-system trunk breaker,
// WorldSnapshot.JunctionStates), which is entirely auto-derived from SystemDevices/DistributionBlock
// and has nothing to do with where the player happened to paint this cosmetic fixture.
public sealed record JunctionBox(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
