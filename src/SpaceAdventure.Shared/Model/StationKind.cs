namespace SpaceAdventure.Shared.Model;

// game_design.md section 10 — "у разных станций разный набор модулей/услуг — нет единого шаблона".
// Which services a station offers is exactly which NPCs it has: no Shipwright means no hulls for
// sale there, no Mechanic means no upgrades, and so on.
public enum StationKind
{
    Outpost,  // bare minimum: administrator + trader. The home station.
    Trade,    // adds a mechanic - the well-supplied commercial hub
    Shipyard, // the only kind that sells hulls
    Mining,   // the Miners' Guild's own base - its trader pays a premium for ore (World.Trade.cs)
}
