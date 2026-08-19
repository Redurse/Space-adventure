namespace SpaceAdventure.Shared.Protocol;

// Covers both interior Doors and AirlockOuterDoors (game_design.md Phase 3, M16) — ids are unique
// across both lists, so one flat lookup by DoorId works for either. Interior doors default open
// (preserves the pre-M16 always-passable behavior); an airlock's outer door defaults closed
// (opening it to vacuum is something the crew has to deliberately choose).
//
// Hp/MaxHp parallel WallBlockState's own shape (World.Doors.cs) - only the player's own ship's
// doors are ever actually damaged (World.EnemyAi.cs's random attack roll), so a station/enemy-ship
// door id simply always resolves to full health and Destroyed stays false for it. RepairProgress/
// RepairTickPosition are only meaningful once Destroyed, driven by the same F-key minigame a
// SystemDevice/Junction already uses (World.SystemRepair.cs) rather than a separate mechanic.
public sealed record DoorState(string DoorId, bool IsOpen, float Hp, float MaxHp,
    float RepairProgress = 0f, float RepairTickPosition = 0f)
{
    public bool Destroyed => Hp <= 0f;
}
