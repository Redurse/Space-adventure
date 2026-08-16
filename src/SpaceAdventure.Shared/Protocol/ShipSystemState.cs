using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// Per physical device now, not per system (M14) - Shields has two devices (ShipSystemDevice) that
// can be damaged independently of each other via their own drop wire, even though they share one
// system and one trunk. Damaged means "not currently receiving power": either this specific
// device's drop link is cut, or its system's trunk link is cut (see WireLinkState).
public sealed record ShipSystemState(string DeviceId, PowerSystemId System, bool Damaged);
