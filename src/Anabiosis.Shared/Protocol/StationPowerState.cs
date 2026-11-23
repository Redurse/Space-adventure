namespace Anabiosis.Shared.Protocol;

// The docked station's own power plant (World.StationPower.cs): the reactor the author drew into it can feed the ship's grid ("shore power") so the
// ship's reactor can idle and its battery charges at the berth. Null in the snapshot for a station with no reactor.
// Available - docked at a living station that has a reactor; Connected - the player has plugged the ship in; Capacity - what the station can supply;
// Delivered - what it actually gave the ship last tick.
public sealed record StationPowerState(bool Available, bool Connected, float Capacity, float Delivered);
