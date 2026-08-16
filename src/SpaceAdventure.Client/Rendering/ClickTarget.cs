using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

public enum BlockKind { None, Reactor, Distribution, System, Navigation, Station, Wiring, Rack }

// Which block, if any, the player currently has "open" (game_design.md sections 1, 5, 10 — click
// a block to walk up to its terminal). System carries which of the 5 power systems it is.
public readonly record struct ClickTarget(BlockKind Kind, PowerSystemId System = default)
{
    public static readonly ClickTarget None = new(BlockKind.None);
    public static readonly ClickTarget Reactor = new(BlockKind.Reactor);
    public static readonly ClickTarget Distribution = new(BlockKind.Distribution);
    public static readonly ClickTarget Navigation = new(BlockKind.Navigation);
    public static readonly ClickTarget Station = new(BlockKind.Station);
    public static readonly ClickTarget Wiring = new(BlockKind.Wiring);
    public static readonly ClickTarget Rack = new(BlockKind.Rack);
    public static ClickTarget ForSystem(PowerSystemId system) => new(BlockKind.System, system);
}
