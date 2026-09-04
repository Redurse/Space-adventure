using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

public enum BlockKind { None, Reactor, Distribution, Battery, System, Navigation, Station, Rack, Connections, SuitLocker, Jukebox, CardTable }

// Which block, if any, the player currently has "open" (game_design.md sections 1, 5, 10 — click
// a block to walk up to its terminal). System carries which of the 5 power systems it is;
// Connections (screwdriver-only, World.Wiring.cs's component graph) carries which component's pins
// to list instead; Rack carries which physical shelf (a hull carries two, game_design.md section 13);
// SuitLocker carries which locker (SuitLockerPanel - read-only, the actual take/put is still F).
public readonly record struct ClickTarget(BlockKind Kind, PowerSystemId System = default, string? TargetComponentId = null)
{
    public static readonly ClickTarget None = new(BlockKind.None);
    public static readonly ClickTarget Reactor = new(BlockKind.Reactor);
    public static readonly ClickTarget Distribution = new(BlockKind.Distribution);
    public static readonly ClickTarget Battery = new(BlockKind.Battery);
    public static readonly ClickTarget Navigation = new(BlockKind.Navigation);
    public static readonly ClickTarget Station = new(BlockKind.Station);
    public static readonly ClickTarget Jukebox = new(BlockKind.Jukebox);
    public static readonly ClickTarget CardTable = new(BlockKind.CardTable);
    public static ClickTarget ForSystem(PowerSystemId system) => new(BlockKind.System, system);
    public static ClickTarget ForConnections(string componentId) => new(BlockKind.Connections, TargetComponentId: componentId);
    public static ClickTarget ForRack(string rackId) => new(BlockKind.Rack, TargetComponentId: rackId);
    public static ClickTarget ForSuitLocker(string lockerId) => new(BlockKind.SuitLocker, TargetComponentId: lockerId);
}
