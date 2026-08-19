namespace SpaceAdventure.Shared.Protocol;

// The reactor's 3 physical levers (game_design.md - drawn on the reactor block itself, next to
// its fuel-rod panel). All 3 default to the ship's normal operating state (World.cs), so a crew
// that never touches them sees no behavior change.
public sealed record ReactorLeverState(bool LightsOn, bool EmergencyShutdown, bool DoorsLocked);
