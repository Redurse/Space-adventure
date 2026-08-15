namespace SpaceAdventure.Shared.Model;

// Every station the ship can dock at shares this same NPC roster for now (game_design.md
// section 10 says stations differ by type/services, but that differentiation is a later
// refinement — Phase 2 MVP just needs an Administrator and a Trader to exist somewhere).
public sealed class Station
{
    public IReadOnlyList<StationNpc> Npcs { get; }

    public Station(IReadOnlyList<StationNpc> npcs) => Npcs = npcs;

    public static Station CreateDefault()
    {
        var npcs = new[]
        {
            new StationNpc("npc-administrator", "Администратор станции", NpcKind.Administrator, X: 20f, Y: 20f),
            new StationNpc("npc-trader", "Торговец", NpcKind.Trader, X: 60f, Y: 20f),
        };
        return new Station(npcs);
    }
}
