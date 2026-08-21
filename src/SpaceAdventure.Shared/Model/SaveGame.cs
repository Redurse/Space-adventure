namespace SpaceAdventure.Shared.Model;

// A saved run (game_design.md section 5 — "игра сохраняется при каждой стыковке к аванпосту").
//
// Deliberately holds only *campaign* progress, not the physical simulation: which hull the crew
// flies, what they own, who likes them, what job they're on, and where on the map they are. Room
// positions, EVA state, hull breaches, ore already cut out of an asteroid and mid-battle state are
// all left out on purpose — a save is only ever taken while docked, which is a clean, well-defined
// situation, so restoring one just puts the crew back at that station rather than trying to
// reconstruct an arbitrary moment.
public sealed record SaveGame(
    int Version,
    ShipKind ShipKind,
    int Credits,
    string DockedPointId,
    IReadOnlyDictionary<FactionId, int> FactionStandings,
    IReadOnlyDictionary<ShipUpgradeTrack, int> UpgradeLevels,
    IReadOnlyList<ItemType> Inventory,
    Quest? ActiveQuest,
    // What's on the ship's storage rack, slot by slot (nulls preserved, since which shelf a thing
    // sits on is the player's own arrangement). Absent in version 1 files - restores as empty.
    IReadOnlyList<ItemType?>? RackSlots = null,
    // The player's own hull layout (Ship Editor), present only when ShipKind is Custom - absent in
    // pre-M?? files and whenever flying a fixed class, restoring as null either way.
    CustomShipDefinition? CustomShip = null,
    // How far the scripted intro campaign has gotten, and the narrative lines reached so far
    // (World.Campaign.cs) - both absent in older files, restoring as NotStarted/empty, the same
    // "additive, no version bump needed" pattern RackSlots/CustomShip above already established.
    CampaignStage Campaign = CampaignStage.NotStarted,
    IReadOnlyList<string>? StoryLog = null,
    // How far the procedural galaxy tail had already grown (GalaxyMap.cs's EnsureGenerated/
    // GeneratedProceduralCount) - absent in older files, restoring as null, which just leaves the
    // galaxy at whatever CreateStarter's own small initial seed already generated.
    int? GeneratedProceduralSystemCount = null)
{
    // Bumped whenever the shape changes incompatibly; SaveStore refuses anything it doesn't know,
    // so an old file fails to load cleanly instead of half-restoring into a broken run.
    public const int CurrentVersion = 2;
}
