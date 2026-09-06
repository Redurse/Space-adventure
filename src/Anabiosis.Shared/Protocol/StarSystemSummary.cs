using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

// One entry in the inter-system map (World.StarSystems.cs) - just enough to draw a node and warp
// to it. A system's own points of interest only ever go over the wire for the system the ship is
// actually in (WorldSnapshot.GalaxyPoints) - there's no reason a client needs another system's
// asteroid layout before it's ever been there. Width/Height are that system's own field bounds
// (its AsteroidField, same units GalaxyPoint.X/Y use) - the client needs the CURRENT system's own
// bounds to know where a free-form map click is actually still inside the barrier. GalaxyX/Y are
// this system's own fixed node position on the GALACTIC map (StarSystem.GalaxyX/Y) - a completely
// separate coordinate space from Width/Height, used only by GalacticMapPanel to draw the system as
// a node and its corridors as lines that never cross.
public sealed record StarSystemSummary(string Id, string Name, float Width, float Height, float GalaxyX, float GalaxyY,
    // Who runs this system, if anyone (StarSystem.cs) - a controlled system reads calmer at a
    // glance on the galactic map than a contested one, same fact GalaxyMap.cs's own generation
    // already uses to decide how rough a procedural system turns out.
    FactionId? ControllingFaction = null);
