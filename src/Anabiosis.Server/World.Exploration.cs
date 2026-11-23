using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Exploring a system (direct user request - fog of war and hidden places in the spirit of Cosmoteer):
//   * every system has its own fog (FogGrid.cs). The ship's sensors clear it in a circle around the hull
//     as it flies; a sonar ping (World.Scanner.cs) clears its whole cone or ring at once;
//   * the system's hidden places (StarSystem.Pois, rolled from the world seed) are unknown until then. A
//     sonar ping that sweeps one marks it as an unknown signal; coming within sensor range identifies it;
//   * the loot finds (pod / abandoned ship / graveyard - the old salvage points, World.Salvage.cs, folded
//     in here) are still taken by flying into them, once. Credits go to the crew, hyperium to the reserve.
// All of it is saved (SaveGame.WorldSeed / Exploration), so a system stays the way it was left.
public sealed partial class World
{
    public const float ShipSensorRadius = ExplorationConstants.ShipSensorRadius;
    private const double ExplorationNoticeSeconds = 6.0;

    private sealed class SystemExploration
    {
        public required FogGrid Fog { get; init; }
        // The copy handed to snapshots: replaced only when the fog actually changes.
        public required byte[] FogBits { get; set; }
        public HashSet<string> Signals { get; } = new();
        public HashSet<string> Identified { get; } = new();
        public HashSet<string> Looted { get; } = new();
        // Salvage already carried off a wreck (World.Wrecks.cs), by loot id.
        public HashSet<string> TakenLoot { get; } = new();
    }

    private readonly Dictionary<string, SystemExploration> _exploration = new();
    private string? _explorationNotice;
    private double _explorationNoticeSecondsLeft;

    public int WorldSeed => GalaxyMap.WorldSeed;
    public string? SalvageNotice => _explorationNoticeSecondsLeft > 0 ? _explorationNotice : null;
    public IReadOnlyCollection<string> LootedPoiIds => ExplorationOf(_currentSystemId).Looted;
    public IReadOnlyCollection<string> IdentifiedPoiIds => ExplorationOf(_currentSystemId).Identified;
    public IReadOnlyCollection<string> SignalPoiIds => ExplorationOf(_currentSystemId).Signals;
    public bool IsFogRevealedAt(Vec2 position) => ExplorationOf(_currentSystemId).Fog.IsRevealed(position);

    private SystemExploration ExplorationOf(string systemId)
    {
        if (_exploration.TryGetValue(systemId, out var existing))
            return existing;
        var fog = new FogGrid(GalaxyMap.GetSystem(systemId).Field.Width);
        return _exploration[systemId] = new SystemExploration { Fog = fog, FogBits = fog.ToBits() };
    }

    private void Notify(string text)
    {
        _explorationNotice = text;
        _explorationNoticeSecondsLeft = ExplorationNoticeSeconds;
    }

    private void StepExploration(double deltaSeconds)
    {
        if (_explorationNoticeSecondsLeft > 0)
            _explorationNoticeSecondsLeft -= deltaSeconds;

        var system = GalaxyMap.GetSystem(_currentSystemId);
        var exploration = ExplorationOf(_currentSystemId);
        // Where the ship is on the system map - NOT _shipFieldPosition, which while docked or landed is not a
        // field coordinate at all (ShipMapPosition's own doc comment): the fog would clear somewhere else.
        var ship = ShipMapPosition;
        if (exploration.Fog.RevealCircle(ship, ShipSensorRadius))
            exploration.FogBits = exploration.Fog.ToBits();

        foreach (var poi in system.Pois)
        {
            var distance = (poi.Position - ship).Length();
            if (distance <= ShipSensorRadius + poi.Radius && exploration.Identified.Add(poi.Id))
            {
                exploration.Signals.Remove(poi.Id);
                Notify($"Обнаружено: {PoiCatalog.NameFor(poi.Kind)}");
            }

            if (!PoiCatalog.IsLootOnApproach(poi.Kind) || distance > poi.Radius || IsDocked || IsLandedOnPlanet
                || !exploration.Looted.Add(poi.Id))
                continue;
            var loot = PoiCatalog.LootFor(poi);
            Credits += loot.Credits;
            _hyperiumReserve += loot.Hyperium;
            Notify(loot.Hyperium > 0
                ? $"{PoiCatalog.NameFor(poi.Kind)}: +{loot.Credits} кредитов, +{loot.Hyperium} гиперия"
                : $"{PoiCatalog.NameFor(poi.Kind)}: +{loot.Credits} кредитов");
        }
    }

    // A sonar ping (World.Scanner.cs's FireScannerPing): clears the fog over everything it swept and turns
    // every hidden place inside it into an unknown signal. bearingDegrees null = the all-round mode.
    private void RevealBySonar(float? bearingDegrees, float halfAngleDegrees, float range)
    {
        var exploration = ExplorationOf(_currentSystemId);
        var ship = ShipMapPosition;
        var changed = bearingDegrees is { } bearing
            ? exploration.Fog.RevealCone(ship, bearing, halfAngleDegrees, range)
            : exploration.Fog.RevealCircle(ship, range);
        if (changed)
            exploration.FogBits = exploration.Fog.ToBits();

        var found = 0;
        foreach (var poi in GalaxyMap.GetSystem(_currentSystemId).Pois)
        {
            if (exploration.Identified.Contains(poi.Id) || exploration.Signals.Contains(poi.Id))
                continue;
            var toPoi = poi.Position - ship;
            if (toPoi.Length() > range + poi.Radius)
                continue;
            if (bearingDegrees is { } sweep)
            {
                var at = MathF.Atan2((float)toPoi.Y, (float)toPoi.X) * (180f / MathF.PI);
                // widened by the angle the place itself subtends, so a cluster is caught by its edge too
                var slack = MathF.Atan2(poi.Radius, (float)Math.Max(1.0, toPoi.Length())) * (180f / MathF.PI);
                if (MathF.Abs(ShortestAngle(at - sweep)) > halfAngleDegrees + slack)
                    continue;
            }
            exploration.Signals.Add(poi.Id);
            found++;
        }
        if (found > 0)
            Notify(found == 1 ? "Сонар: неизвестный сигнал" : $"Сонар: неизвестных сигналов - {found}");
    }

    // Cheat panel: the whole of this system's map is explored at once - every fog cell cleared and every hidden place found.
    public void DebugRevealSystemMap()
    {
        var exploration = ExplorationOf(_currentSystemId);
        var size = exploration.Fog.FieldSize;
        if (exploration.Fog.RevealCircle(new Vec2(size / 2f, size / 2f), size))
            exploration.FogBits = exploration.Fog.ToBits();

        var found = 0;
        foreach (var poi in GalaxyMap.GetSystem(_currentSystemId).Pois)
        {
            exploration.Signals.Remove(poi.Id);
            if (exploration.Identified.Add(poi.Id))
                found++;
        }
        Notify(found > 0 ? $"Карта системы раскрыта: найдено мест - {found}" : "Карта системы раскрыта");
    }

    private IReadOnlyList<PoiState> CreatePoiStates()
    {
        var exploration = ExplorationOf(_currentSystemId);
        var states = new List<PoiState>();
        foreach (var poi in GalaxyMap.GetSystem(_currentSystemId).Pois)
        {
            var identified = exploration.Identified.Contains(poi.Id);
            if (!identified && !exploration.Signals.Contains(poi.Id))
                continue;
            states.Add(new PoiState(poi.Id, identified ? poi.Kind : null, poi.X, poi.Y, poi.Radius, exploration.Looted.Contains(poi.Id)));
        }
        return states;
    }

    private FogOfWarState CreateFogState()
    {
        var exploration = ExplorationOf(_currentSystemId);
        return new FogOfWarState(exploration.Fog.FieldSize, exploration.FogBits, ShipSensorRadius);
    }

    private IReadOnlyList<SystemExplorationSave> CreateExplorationSave() =>
        _exploration.Select(kv => new SystemExplorationSave(kv.Key, kv.Value.Fog.ToBits(),
            kv.Value.Signals.ToArray(), kv.Value.Identified.ToArray(), kv.Value.Looted.ToArray(), kv.Value.TakenLoot.ToArray())).ToArray();

    private void LoadExploration(IReadOnlyList<SystemExplorationSave>? saved)
    {
        _exploration.Clear();
        foreach (var entry in saved ?? Array.Empty<SystemExplorationSave>())
        {
            // A system the saved galaxy no longer has (it only grows, so this is a hand-edited save).
            if (GalaxyMap.Systems.All(s => s.Id != entry.SystemId))
                continue;
            var fog = new FogGrid(GalaxyMap.GetSystem(entry.SystemId).Field.Width, entry.FogBits);
            var exploration = new SystemExploration { Fog = fog, FogBits = fog.ToBits() };
            exploration.Signals.UnionWith(entry.SignalIds);
            exploration.Identified.UnionWith(entry.IdentifiedIds);
            exploration.Looted.UnionWith(entry.LootedIds);
            exploration.TakenLoot.UnionWith(entry.TakenLootIds ?? Array.Empty<string>());
            _exploration[entry.SystemId] = exploration;
        }
    }
}
