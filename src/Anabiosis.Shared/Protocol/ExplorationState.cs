using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

// What the crew knows of a hidden place (PointOfInterest.cs). Only places that have been at least picked
// up by the sonar are ever sent - an unfound one is simply absent, so a client cannot peek.
// Kind is null while the place is only a sonar signal ("Неизвестный сигнал"); it is filled in once the
// ship has been close enough to see what it is.
public sealed record PoiState(string Id, PoiKind? Kind, double X, double Y, float Radius, bool Looted)
{
    public Vec2 Position => new(X, Y);
    public bool Identified => Kind is not null;
    public string Name => Kind is { } kind ? PoiCatalog.NameFor(kind) : PoiCatalog.UnknownSignalName;
}

// The current system's fog of war (FogGrid.cs): FogGrid.Size x FogGrid.Size cells over a square field of
// FieldSize units, one bit per cell (set = explored). Bits is replaced, never mutated, so an unchanged
// fog is the same array tick after tick and SnapshotDelta leaves it out of the frame.
public sealed record FogOfWarState(float FieldSize, byte[] Bits, float SensorRadius);

// A piece of salvage lying on a wreck deck (World.Wrecks.cs), in the wreck hull local frame.
public sealed record WreckLootState(string Id, ItemType Item, double X, double Y, string RoomId)
{
    public Vec2 Position => new(X, Y);
}
