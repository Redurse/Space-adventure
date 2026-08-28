using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// M63 - a free-flying chunk of hull that structurally detached from the ship (World.ShipDebris.cs's
// own doc comment has the full design). X/Y/RotationDegrees are the fragment's own live world-space
// transform (independent of the player ship's); Rooms are the detached rooms' footprints, stored
// relative to the fragment's own pivot (its footprint's centre at the moment of detachment) the same
// way Ship.Rooms is relative to the player ship's own hull centre - FieldRenderer places each one by
// rotating that offset out to world space and adding X/Y, the same transform already used for an
// enemy hull's own wall blocks.
public sealed record ShipDebrisState(string Id, float X, float Y, float RotationDegrees, IReadOnlyList<Room> Rooms);
