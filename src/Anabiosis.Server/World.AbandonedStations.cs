using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// The dead station place (PoiKind.AbandonedStation; direct user request, from the list of places for the
// Cosmoteer-style system map): a station in every structural sense - the ship docks at it and the crew
// walks its rooms through the connector, the way they do at a live one - with nothing alive in it.
// It is off the charts until found (World.Exploration.cs); once identified the ship can dock.
//   * no people, no services, no resupply: docking does not refuel, repair or restock anything;
//   * no air: its rooms are open to vacuum, so anyone without a sealed suit suffocates in them;
//   * salvage lies in its rooms (ordinary things on the floor, picked up the ordinary way). What has been
//     carried off is remembered (SystemExploration.TakenLoot), so a station is stripped only once.
public sealed partial class World
{
    private const string StationLootPrefix = "station-loot-";

    private bool IsAbandonedStationPoint(string pointId) =>
        GalaxyMap.GetPoint(pointId).Kind == GalaxyPointKind.AbandonedStation;

    /// Docked at a dead station right now.
    public bool DockedAtAbandonedStation => _dockedPointId is { } id && IsAbandonedStationPoint(id);

    // Lays the station's salvage out in its rooms as the ship mates with it. Rolled from the station's id, so
    // it is the same salvage on every visit, minus what was already taken.
    private void PlaceStationSalvage(string pointId)
    {
        ClearStationSalvage();
        var station = Station;
        var taken = ExplorationOf(_currentSystemId).TakenLoot;
        var random = new Random(StableSectorSeed(pointId + "-salvage"));
        // The dock room is where everyone arrives - the finds are further in.
        var rooms = station.Rooms.Where(r => r.Id != station.DockRoomId).ToList();
        if (rooms.Count == 0)
            rooms = station.Rooms.ToList();
        var count = Math.Clamp(rooms.Count * 2, 6, 16);
        var placed = new List<Vec2>();
        for (var i = 0; i < count; i++)
        {
            var item = RollWreckLoot(random);
            var room = rooms[random.Next(rooms.Count)];
            Vec2? spot = null;
            for (var attempt = 0; attempt < 20 && spot is null; attempt++)
            {
                var tile = new TileCoord((int)Math.Floor(room.X + random.NextDouble() * room.Width), (int)Math.Floor(room.Y + random.NextDouble() * room.Height));
                var center = TilePathfinder.CenterOf(tile);
                if (TilePathfinder.IsPassable(station.Tiles, tile) && room.Contains(center) && placed.All(p => (p - center).Length() > 0.9))
                    spot = center;
            }
            var id = $"{StationLootPrefix}{pointId}-{i}";
            if (spot is not { } at)
                continue;
            placed.Add(at);
            if (!taken.Contains(id))
                _droppedItems.Add(new DroppedItem(id, item, at.X, at.Y, room.Id));
        }
    }

    // Casting off takes the station's rooms away - whatever salvage was left there goes with them.
    private void ClearStationSalvage() => _droppedItems.RemoveAll(d => d.Id.StartsWith(StationLootPrefix));

    private void RememberStationSalvageTaken(string droppedItemId)
    {
        if (droppedItemId.StartsWith(StationLootPrefix))
            ExplorationOf(_currentSystemId).TakenLoot.Add(droppedItemId);
    }
}

public sealed partial class World
{
    // Test hook: puts a character straight into a room of the docked station.
    public void DebugPlaceCharacterOnStation(int playerId, Vec2 position)
    {
        var character = _characters[playerId];
        character.Position = position;
        character.RoomId = Station.Rooms.First(r => r.Contains(position)).Id;
        character.OnStation = true;
        character.IsAtHelm = false;
    }
}
