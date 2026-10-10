namespace Anabiosis.Shared.Protocol;

// A person walking around the docked station (World.StationResidents.cs). Civilians wander between
// places; guards patrol and close in on trouble. Purely scenery with a pulse - they carry no services
// (the station's job NPCs, StationNpc, keep those). Look picks the clothes/colours client-side; X/Y are
// in the station's docked frame like StationNpc's own.
public enum ResidentRole
{
    Civilian,
    Guard,
}

public sealed record StationResidentState(string Id, string Name, int Look, ResidentRole Role, float X, float Y);
