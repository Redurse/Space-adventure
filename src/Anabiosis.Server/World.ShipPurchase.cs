using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Buying a different hull from a station's Shipwright (game_design.md section 9 - "все классы
// доступны с самого начала, но дороже/дешевле"). Until now the three ShipKinds could only be
// picked once, on the pre-game select screen; this makes them a real economic choice mid-run.
//
// Swapping hulls rebuilds every piece of state keyed off the ship layout - turret runtimes, room
// oxygen, door states, hull breaches - and moves the crew to the new ship's spawn point. Inventory
// and credits are personal/crew-wide, so they survive the swap untouched, as do upgrades bought
// from the Mechanic (they're tracked per-track on the crew, not per-hull).
public sealed partial class World
{
    // Shared with the constructor so a bought hull is initialized exactly like a starting one -
    // no chance of the two paths drifting apart.
    private void InitializeShipState()
    {
        // Station layouts hang off this hull's airlock door position, so they have to follow it.
        RebuildStationLayouts();
        InitializeWiring();
        InitializeComponentMounts();
        InitializeRackSlots();
        InitializeSuitLockers();
        InitializeWallBlocks();
        InitializeEngines();
        RestockAmmoStorages();
        RestockHullPlating();
        RecomputeDeviceBonuses(); // content-каталог отсеков - a bought/starting hull's own bonus is 0, but this is the shared recompute point every caller (constructor/purchase/save/detach) goes through

        _roomOxygen.Clear();
        foreach (var room in Ship.Rooms)
            _roomOxygen[room.Id] = FullOxygen;

        foreach (var door in Ship.Doors)
        {
            _doorOpen[door.Id] = true; // preserves the pre-M16 always-passable behavior
            _doorHp[door.Id] = DoorMaxHp;
        }
        foreach (var outerDoor in Ship.AirlockOuterDoors)
        {
            _doorOpen[outerDoor.Id] = false; // opening to vacuum is always a deliberate choice
            _doorHp[outerDoor.Id] = DoorMaxHp;
        }

        _systemRepairProgress.Clear(); // a new/swapped hull's devices start undamaged anyway
    }

    // Net price: the new hull's list price minus what the yard gives back for the current one.
    // Can be negative when trading down (a Cruiser for a Scout pays out), which is deliberate.
    public int GetShipSwapCost(ShipKind kind) =>
        ShipCatalog.Price(kind) - ShipCatalog.TradeInValue(CurrentShipKind);

    private void TryPurchaseShip(ShipKind kind)
    {
        if (!IsDocked || kind == CurrentShipKind)
            return;

        // Only stations that actually have a Shipwright sell hulls (game_design.md section 10 -
        // "у разных станций разный набор модулей/услуг").
        if (Station.Npcs.All(n => n.Kind != NpcKind.Shipwright))
            return;

        var cost = GetShipSwapCost(kind);
        if (Credits < cost)
            return;

        Credits -= cost;
        CurrentShipKind = kind;
        _customShipDefinition = null; // only Ship.Create's fixed kinds are ever sold here
        Ship = Ship.Create(kind);

        _turretRuntimes.Clear();
        foreach (var turret in Ship.Turrets)
            _turretRuntimes[turret.Id] = new TurretRuntime(turret);
        _turretAimInput.Clear();
        // The old hull's CardTable (and everyone about to be moved off it below) is gone with it.
        _cardGame = null;

        InitializeShipState();

        // Everyone aboard steps off onto the yard and back onto the new hull - including anyone
        // who happened to be manning a turret or the helm of a ship that no longer exists.
        foreach (var character in _characters.Values)
        {
            character.ManningTurretId = null;
            character.IsAtHelm = false;
            character.IsOutside = false;
            character.OnEnemyShip = false;
            character.OnStation = false;
            character.EvaAttachedTo = EvaAttachment.None;
            character.EvaAttachedAsteroidId = null;
            character.EvaVelocity = Vec2.Zero;
            character.Position = Ship.SpawnPoint;
            character.RoomId = Ship.SpawnRoomId;
        }
    }
}
