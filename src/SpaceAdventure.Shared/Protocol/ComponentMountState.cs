namespace SpaceAdventure.Shared.Protocol;

// Parallels DoorState's id+flag shape - which component (if any) is currently plugged into this
// mount, so the client can render an empty socket versus an installed part.
public sealed record ComponentMountState(string MountId, string? InstalledComponentId);
