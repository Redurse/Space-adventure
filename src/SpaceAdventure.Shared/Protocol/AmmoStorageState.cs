namespace SpaceAdventure.Shared.Protocol;

// Runtime stock for an AmmoStorage (finite now, see World.Ammo.cs) - separate from the static
// AmmoStorage position the same way DoorState sits next to the static Door.
public sealed record AmmoStorageState(string StorageId, int Remaining, int Capacity);
