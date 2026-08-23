namespace SpaceAdventure.Shared.Model;

// game_design.md section 10 — "у разных станций разный набор модулей/услуг — нет единого шаблона".
// Which services a station offers follows from its kind (Station.Procedural.cs's per-kind secondary
// module pool), same as which NPCs it has: no Shipwright means no hulls for sale there, no Mechanic
// means no upgrades, and so on.
public enum StationKind
{
    Trade,      // the well-supplied commercial hub - biggest trade/storage-flavored secondary pool
    Military,   // armed outposts - barracks/armory/command secondary pool, biggest total room count
    Mining,     // the Miners' Guild's own base - its trader pays a premium for ore (World.Trade.cs)
    Shipyard,   // the only kind that sells hulls (always gets a Shipwright's office)
    Research,   // small science outposts - laboratory/observatory secondary pool
}
