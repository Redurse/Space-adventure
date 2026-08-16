namespace SpaceAdventure.Shared.Protocol;

// Runtime state of one WireLink. PrimaryDamaged is the original wiring being cut (what the enemy
// AI's system-damage roll now targets); HasBackup/BackupDamaged track a player-laid redundant
// wire (game_design.md section 1 - "резерв на случай повреждения магистрали"). IsConnected is
// true as long as either the primary or an intact backup is carrying power.
public sealed record WireLinkState(string LinkId, bool PrimaryDamaged, bool HasBackup, bool BackupDamaged)
{
    public bool IsConnected => !PrimaryDamaged || (HasBackup && !BackupDamaged);
}
