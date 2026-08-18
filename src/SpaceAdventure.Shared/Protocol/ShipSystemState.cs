using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// Per physical device now, not per system (M14) - Shields has two devices (ShipSystemDevice) that
// can be damaged independently of each other via their own drop wire, even though they share one
// system and one trunk. Damaged means "not currently receiving power": either this specific
// device's drop link is cut, or its system's trunk link is cut (see WireLinkState).
// RepairProgress/RepairTickPosition (World.SystemRepair.cs) are only meaningful while Damaged -
// a Barotrauma-style repair minigame: holding a wrench/screwdriver and pressing F starts a slow
// passive fill, and a well-timed extra press while the sweeping tick sits inside the already-filled
// part of the bar adds a bonus chunk instead of fixing it in one instant press.
public sealed record ShipSystemState(string DeviceId, PowerSystemId System, bool Damaged,
    float RepairProgress = 0f, float RepairTickPosition = 0f);
