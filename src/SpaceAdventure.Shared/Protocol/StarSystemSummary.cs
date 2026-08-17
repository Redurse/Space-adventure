namespace SpaceAdventure.Shared.Protocol;

// One entry in the inter-system map (World.StarSystems.cs) - just enough to draw a node and warp
// to it. A system's own points of interest only ever go over the wire for the system the ship is
// actually in (WorldSnapshot.GalaxyPoints) - there's no reason a client needs another system's
// asteroid layout before it's ever been there.
public sealed record StarSystemSummary(string Id, string Name);
