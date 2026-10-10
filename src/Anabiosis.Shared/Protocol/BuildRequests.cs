using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

// Place the catalog compartment CatalogId with its top-left tile at (X, Y), turned by Rotation quarter-turns (0-3).
public sealed record BuildCompartmentRequest(string CatalogId, int X, int Y, int Rotation);

// Put a junction door (JunctionDoor.cs) with its anchor tile at (X, Y): Passage is the direction a character walks through it
// (East or South), Span its width in tiles (1-3).
public sealed record PlaceDoorRequest(int X, int Y, TileSide Passage, int Span);
