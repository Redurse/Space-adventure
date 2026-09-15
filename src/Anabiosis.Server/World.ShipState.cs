namespace Anabiosis.Server;

// Shared ship-state initialization - runs after the constructor builds Ship, and after ApplySave/
// World.ShipBuilding.cs's own full-rebuild path rebuild it, so a fresh, loaded, or edited hull all
// end up in the identical state (turret runtimes, room oxygen, door open/hp, wiring, wall blocks...).
// Direct user request ("удали все текущие корабли... полностью удалить из кода") - this used to also
// host the mid-run Shipwright hull-swap feature (GetShipSwapCost/TryPurchaseShip), which only ever
// sold the 6 fixed ShipKinds that request removed; nothing is left to buy once every ship is Custom,
// so that feature is gone too (StationPanel.PurchasableShipKinds/ClientCommand.PurchaseShipKind and
// their own client-side UI went with it).
public sealed partial class World
{
    private void InitializeShipState()
    {
        // Station layouts hang off this hull's airlock door position, so they have to follow it.
        RebuildStationLayouts();
        InitializeWiring();
        InitializeComponentMounts();
        InitializeRackSlots();
        InitializeSuitLockers();
        InitializeTerminals();
        InitializeWallBlocks();
        InitializeEngines();
        RestockAmmoStorages();
        RestockHullPlating();
        RecomputeDeviceBonuses(); // content-каталог отсеков - a bought/starting hull's own bonus is 0, but this is the shared recompute point every caller (constructor/save/detach) goes through

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

        // M-doors-as-edges - the direct replacement for the old narrow interior Door, so it keeps
        // that same "preserves the pre-M16 always-passable behavior" default (open), not the
        // airlock's deliberate-choice default. Same "never cleared across a hull swap" convention
        // as _doorOpen/_doorHp just above (stale ids from a previous hull are simply never read
        // again, since every lookup here goes through this hull's own Ship.DoorEdges).
        foreach (var edge in Ship.DoorEdges)
        {
            _doorEdgeOpen[edge.Id] = true;
            _doorEdgeHp[edge.Id] = DoorMaxHp;
        }

        _systemRepairProgress.Clear(); // a new/swapped hull's devices start undamaged anyway
    }
}
