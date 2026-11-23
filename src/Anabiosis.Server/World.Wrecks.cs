using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// The "blown-up ship" place (PoiKind.Wreck; direct user request - "взорванный корабль, представлен в виде
// разрушенного корабля, на который можно проникнуть"). It is a real hull - the same simulated ship a
// hostile cruiser is (EnemyShipRuntime: rooms, walls, wiring, atmosphere) - with nobody aboard, no power,
// no air, holes blown in its plating, and salvage lying on its decks. Because it IS one of those ships,
// everything a boarding party can already do to one works here unchanged: fly up in a suit, go in through
// a hole (or cut a new one), walk the compartments.
//
// A wreck only exists as a ship while the player's ship is near its place (it is materialized on approach
// and put away again when the crew flies off), so a system can hold several without simulating any of them.
// What it looks like is rolled from its place's id, so it is the same wreck every time; what has been taken
// from it is remembered (SystemExploration.TakenLoot, saved).
public sealed partial class World
{
    private const float WreckActivationRadius = 420f;
    private const float WreckDeactivationRadius = 650f;

    private EnemyShipRuntime? _wreck;
    private string? _wreckPoiId;

    public bool WreckInField => _wreck is not null;

    private void StepWrecks()
    {
        // A fight takes the board over (SpawnEnemySquadron clears it): whatever wreck was out is gone
        // with it, and comes back once the fight is over and the ship is still near.
        if (_wreck is not null && !_enemyShips.Contains(_wreck))
        {
            _wreck = null;
            _wreckPoiId = null;
        }
        if (IsInBattle)
            return;

        var ship = ShipMapPosition;
        var exploration = ExplorationOf(_currentSystemId);

        if (_wreck is not null)
        {
            if (!_wreck.Alive)
            {
                // Shot or cut to pieces: the place is spent.
                exploration.Looted.Add(_wreckPoiId!);
                RemoveWreck();
                return;
            }
            var anyoneAboard = _characters.Values.Any(c => c.OnEnemyShip);
            if (!anyoneAboard && (_wreck.Position - ship).Length() > WreckDeactivationRadius)
                RemoveWreck();
            return;
        }

        if (_enemyShips.Any(e => e.Alive))
            return;
        var poi = GalaxyMap.GetSystem(_currentSystemId).Pois.FirstOrDefault(p =>
            p.Kind == PoiKind.Wreck && !exploration.Looted.Contains(p.Id) && (p.Position - ship).Length() <= WreckActivationRadius);
        if (poi is not null)
            MaterializeWreck(poi, exploration);
    }

    private void RemoveWreck()
    {
        if (_wreck is not null)
            _enemyShips.Remove(_wreck);
        _wreck = null;
        _wreckPoiId = null;
    }

    private void MaterializeWreck(PointOfInterest poi, SystemExploration exploration)
    {
        var random = new Random(StableSectorSeed(poi.Id + "-wreck"));
        var layout = NewEnemyLayout();
        var wreck = new EnemyShipRuntime($"wreck-{poi.Id}", EnemyMaxHp, poi.Position, layout)
        {
            Derelict = true,
            RotationDegrees = (float)(random.NextDouble() * 360.0),
        };

        // Nobody aboard, nothing running, no air.
        wreck.Crew.Clear();
        wreck.Grid.Reactor.Broken = true;
        wreck.Grid.DistributionBroken = true;
        wreck.HelmBroken = true;
        foreach (var roomId in wreck.RoomOxygen.Keys.ToList())
            wreck.RoomOxygen[roomId] = 0f;
        foreach (var turret in layout.Ship.Turrets)
            wreck.DamageFixture(turret.Id, EnemyShipRuntime.TurretMaxHp, EnemyShipRuntime.TurretMaxHp);

        // Holes: every third outer plate or so is gone (so there is always a way in), and a few bulkheads.
        var outer = layout.WallBlocks.Where(b => !b.IsInterior).ToList();
        var holed = 0;
        foreach (var block in outer)
            if (random.NextDouble() < 0.3)
            {
                wreck.DamageWallBlock(block.Id, WallBlockMaxHp);
                holed++;
            }
        if (holed == 0 && outer.Count > 0)
            wreck.DamageWallBlock(outer[random.Next(outer.Count)].Id, WallBlockMaxHp);
        foreach (var block in layout.WallBlocks.Where(b => b.IsInterior))
            if (random.NextDouble() < 0.2)
                wreck.DamageWallBlock(block.Id, WallBlockMaxHp);

        // Salvage on the decks: a handful of things per wreck, on free floor, minus whatever was already taken.
        var count = random.Next(6, 11);
        for (var i = 0; i < count; i++)
        {
            var item = RollWreckLoot(random);
            var room = layout.Rooms[random.Next(layout.Rooms.Count)];
            Vec2? spot = null;
            for (var attempt = 0; attempt < 20 && spot is null; attempt++)
            {
                var tile = new TileCoord((int)Math.Floor(room.X + random.NextDouble() * room.Width), (int)Math.Floor(room.Y + random.NextDouble() * room.Height));
                var center = TilePathfinder.CenterOf(tile);
                if (TilePathfinder.IsPassable(layout.Tiles, tile) && room.Contains(center)
                    && wreck.Loot.All(l => (l.Position - center).Length() > 0.9))
                    spot = center;
            }
            var id = $"wreck-loot-{poi.Id}-{i}";
            if (spot is { } at && !exploration.TakenLoot.Contains(id))
                wreck.Loot.Add(new WreckLootState(id, item, at.X, at.Y, room.Id));
        }

        _enemyShips.Add(wreck);
        _wreck = wreck;
        _wreckPoiId = poi.Id;
    }

    private static readonly (ItemType Item, float Weight)[] WreckLootTable =
    {
        (ItemType.SteelPlate, 10f), (ItemType.CopperCable, 8f), (ItemType.Plastic, 7f), (ItemType.Hyperium, 7f),
        (ItemType.HullPlating, 6f), (ItemType.WireSpool, 5f), (ItemType.OxygenTank, 5f), (ItemType.WeldingTank, 4f),
        (ItemType.Bandage, 4f), (ItemType.MedKit, 3f), (ItemType.TitaniumPlate, 3f), (ItemType.Magazine, 3f),
        (ItemType.FuelRod, 2f), (ItemType.Plastalloy, 1.5f), (ItemType.Rifle, 1f),
        // guns and rounds somebody left behind (GunDefinitions.cs)
        (ItemType.RevolverRounds, 2f), (ItemType.ShotgunShells, 2f), (ItemType.SmgMagazine, 2f), (ItemType.RifleRounds, 1f),
        (ItemType.BodyArmor, 0.6f), (ItemType.BallisticHelmet, 0.6f),
        (ItemType.RevolverRoundsDepleted, 0.5f), (ItemType.SmgMagazineDepleted, 0.5f), (ItemType.ShotgunShellsRubber, 0.5f),
        (ItemType.ShotgunSlugs, 0.4f), (ItemType.HarpoonsHeavy, 0.4f), (ItemType.HarpoonsExplosive, 0.3f),
        (ItemType.Revolver, 1f), (ItemType.Shotgun, 0.7f), (ItemType.Smg, 0.7f), (ItemType.MachinePistol, 0.6f), (ItemType.HarpoonGun, 0.6f),
        (ItemType.BoltRifle, 0.4f), (ItemType.Handcannon, 0.3f), (ItemType.HandcannonRounds, 0.6f), (ItemType.Harpoons, 0.8f),
    };

    private static ItemType RollWreckLoot(Random random)
    {
        var roll = (float)random.NextDouble() * WreckLootTable.Sum(t => t.Weight);
        foreach (var (item, weight) in WreckLootTable)
        {
            roll -= weight;
            if (roll <= 0f)
                return item;
        }
        return WreckLootTable[0].Item;
    }

    // Picks a piece of salvage off the wreck's deck: the named one (a click), or the nearest in reach (the
    // interact key). True if something was taken.
    private bool TryTakeWreckLoot(Character character, string? lootId)
    {
        if (!character.OnEnemyShip || BoardableEnemy is not { Derelict: true } wreck)
            return false;
        var loot = wreck.Loot
            .Where(l => (lootId is null || l.Id == lootId) && (l.Position - character.Position).Length() < InteractionConstants.PickupRadius)
            .OrderBy(l => (l.Position - character.Position).Length())
            .FirstOrDefault();
        if (loot is null || !character.Inventory.TryAdd(loot.Item))
            return false;
        wreck.Loot.Remove(loot);
        ExplorationOf(_currentSystemId).TakenLoot.Add(loot.Id);
        return true;
    }

    private IReadOnlyList<WreckLootState>? CreateWreckLootStates() =>
        BoardableEnemy is { Derelict: true } wreck ? wreck.Loot.ToArray() : null;

    // Test hook: the wreck's own ship, once it is out.
    public (IReadOnlyList<WreckLootState> Loot, int BreachedOuterBlocks, int Crew, bool ReactorBroken)? DebugWreck() =>
        _wreck is null
            ? null
            : (_wreck.Loot.ToArray(), _wreck.Layout.WallBlocks.Count(b => !b.IsInterior && _wreck.IsWallBlockBreached(b.Id)),
                _wreck.Crew.Count, _wreck.Grid.Reactor.Broken);
}
