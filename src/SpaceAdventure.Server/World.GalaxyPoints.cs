using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed partial class World
{
    // The one place server code turns a GalaxyPoint into an actual field-space position. Every
    // point (stations included) is a plain fixed coordinate now (M59 - "убрать орбитальную
    // механику, вернуть статичную карту в духе Cosmoteer"), so this is just point.Position - kept
    // as its own method rather than inlined everywhere so a future change to how points are
    // resolved only has one call site to touch.
    public Vec2 ResolveGalaxyPointPosition(GalaxyPoint point) => point.Position;
}
