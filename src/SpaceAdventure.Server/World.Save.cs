using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Capturing and restoring a run (game_design.md section 5 - "игра сохраняется при каждой
// стыковке"). See SaveGame's own doc comment for why only campaign progress is stored and not the
// physical simulation.
public sealed partial class World
{
    // Set on every dock, cleared by whoever writes the file (GameServer). A flag rather than an
    // event so the World stays free of any notion of files or I/O - it only reports that a
    // save-worthy moment happened.
    public bool AutosavePending { get; private set; }

    public SaveGame CreateSave()
    {
        // Solo play has exactly one character; a crew save would need this per-player, which is
        // deferred along with everything else multiplayer.
        var inventory = _characters.Values.FirstOrDefault()?.Inventory.MainSlots
            .OfType<ItemType>()
            .ToArray() ?? Array.Empty<ItemType>();

        return new SaveGame(
            SaveGame.CurrentVersion,
            CurrentShipKind,
            Credits,
            _dockedPointId ?? GalaxyMap.HomePointId,
            new Dictionary<FactionId, int>(_factionStanding),
            new Dictionary<ShipUpgradeTrack, int>(_upgradeLevels),
            inventory,
            ActiveQuest,
            RackSlots.ToArray(),
            _customShipDefinition,
            _campaignStage,
            _storyLog.ToArray(),
            GalaxyMap.GeneratedProceduralCount,
            _manualScannerMarkers.ToArray());
    }

    public void ClearAutosavePending() => AutosavePending = false;

    // Restores a saved run onto a freshly constructed World. Expects to be called before anyone
    // has played (right after construction) - it re-docks the ship at the saved station rather
    // than trying to unwind whatever was happening.
    public void ApplySave(SaveGame save)
    {
        if (save.ShipKind != CurrentShipKind || save.ShipKind == ShipKind.Custom)
        {
            CurrentShipKind = save.ShipKind;
            _customShipDefinition = save.ShipKind == ShipKind.Custom ? save.CustomShip : null;
            Ship = save.ShipKind == ShipKind.Custom ? Ship.FromCustomDefinition(save.CustomShip!) : Ship.Create(save.ShipKind);
            _turretRuntimes.Clear();
            foreach (var turret in Ship.Turrets)
                _turretRuntimes[turret.Id] = new TurretRuntime(turret);
            InitializeShipState();
        }

        Credits = save.Credits;

        foreach (var (faction, standing) in save.FactionStandings)
            _factionStanding[faction] = standing;

        foreach (var (track, level) in save.UpgradeLevels)
            _upgradeLevels[track] = level;
        ApplyUpgradeEffects();

        ActiveQuest = save.ActiveQuest;
        if (save.RackSlots is { } rackSlots)
            LoadRackSlots(rackSlots);

        _campaignStage = save.Campaign;
        _storyLog.Clear();
        if (save.StoryLog is { } storyLog)
            _storyLog.AddRange(storyLog);

        if (save.GeneratedProceduralSystemCount is { } proceduralCount)
            GalaxyMap.EnsureAtLeast(proceduralCount);

        _manualScannerMarkers.Clear();
        if (save.ManualScannerMarkers is { } markers)
            _manualScannerMarkers.AddRange(markers);

        // Docked at the saved station, exactly as if the ship had just finished its approach.
        var point = GalaxyMap.Points.FirstOrDefault(p => p.Id == save.DockedPointId)
            ?? GalaxyMap.GetPoint(GalaxyMap.HomePointId);
        // _currentSystemId isn't restored yet (M35) - a save/load after warping away will still
        // put the crew back in whichever system they started in this process. Contained, tracked.
        EnterStation(point.Id);
        AutosavePending = false; // loading isn't itself a save-worthy moment

        foreach (var character in _characters.Values)
        {
            character.Position = Ship.SpawnPoint;
            character.RoomId = Ship.SpawnRoomId;
            character.OnStation = false;
            character.OnEnemyShip = false;
            character.IsOutside = false;
            character.IsAtHelm = false;
            character.ManningTurretId = null;

            foreach (var item in save.Inventory)
                character.Inventory.TryAdd(item);
        }
    }
}
