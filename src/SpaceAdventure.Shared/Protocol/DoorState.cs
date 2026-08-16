namespace SpaceAdventure.Shared.Protocol;

// Covers both interior Doors and AirlockOuterDoors (game_design.md Phase 3, M16) — ids are unique
// across both lists, so one flat lookup by DoorId works for either. Interior doors default open
// (preserves the pre-M16 always-passable behavior); an airlock's outer door defaults closed
// (opening it to vacuum is something the crew has to deliberately choose).
public sealed record DoorState(string DoorId, bool IsOpen);
