using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// Per physical device now, not per system (M14) - Shields has two devices (ShipSystemDevice) that
// can be damaged independently of each other via their own drop wire, even though they share one
// system and one trunk. Damaged means "not currently receiving power": either this specific
// device's drop link is cut, or its system's trunk link is cut (see WireLinkState).
// RepairProgress (World.SystemRepair.cs) is only meaningful while Damaged - a plain elapsed-time
// timer, not a skill minigame: holding a wrench/screwdriver in reach for 12 in-game hours (M57)
// fixes it, regardless of how many times F gets pressed.
public sealed record ShipSystemState(string DeviceId, PowerSystemId System, bool Damaged,
    float RepairProgress = 0f);
