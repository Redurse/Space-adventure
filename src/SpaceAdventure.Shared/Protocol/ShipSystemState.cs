using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

public sealed record ShipSystemState(PowerSystemId System, bool Damaged);
