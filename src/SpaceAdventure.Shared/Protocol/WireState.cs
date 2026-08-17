namespace SpaceAdventure.Shared.Protocol;

// Replaces WireLinkState's 3-bool tuple. A "backup" is now just a second Wire between the same two
// pins (Component.cs's pin-cardinality rule), so the old PrimaryDamaged/HasBackup/BackupDamaged
// tri-state collapses to one bool per wire - whichever wire got cut, full stop.
public sealed record WireState(string WireId, bool Damaged);
