using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The hostile ship drawn like the player's own: the server sends the hull's definition (now and then) and which wired things
// are dead; this rebuilds the whole Ship from it - every device, turret, engine and wall - and wraps it into a snapshot the
// ordinary ShipRenderer.Draw can paint. The enemy's own state (air, wall damage, which things are dark) replaces the player's.
public sealed class EnemyShipView
{
    private int _version = -1;
    private Ship? _ship;

    // Null until the hull's definition has arrived at least once.
    public WorldSnapshot? Build(WorldSnapshot snapshot)
    {
        var enemy = snapshot.EnemyShip;
        if (enemy.Definition is { } definition && (_ship is null || enemy.LayoutVersion != _version))
        {
            _ship = Ship.FromCustomDefinition(definition);
            _version = enemy.LayoutVersion;
        }
        if (_ship is not { } ship)
            return null;

        var dark = enemy.NotWorkingIds is { Count: > 0 } ids ? ids.ToHashSet() : new HashSet<string>();
        var power = snapshot.Power with
        {
            ReactorOutput = enemy.ReactorBroken ? 0f : snapshot.Power.ReactorMaxOutput,
            BatteryCharge = 0f,
        };

        return snapshot with
        {
            Rooms = ship.Rooms,
            Doors = Array.Empty<Door>(),
            DoorStates = Array.Empty<DoorState>(),
            Turrets = ship.Turrets,
            TurretStates = ship.Turrets.Select(t => new TurretState(t.Id, 0f, null, 0f, 0, 0, Damaged: dark.Contains(t.Id))).ToArray(),
            AmmoStorages = ship.AmmoStorages,
            AmmoStorageStates = Array.Empty<AmmoStorageState>(),
            SuitLockers = ship.SuitLockers,
            SuitLockerStates = ship.SuitLockers.Select(l => new SuitLockerState(l.Id, true)).ToArray(),
            SystemDevices = ship.SystemDevices,
            SystemStates = ship.SystemDevices.Select(d => new ShipSystemState(d.Id, d.System, dark.Contains(d.Id))).ToArray(),
            JunctionStates = Array.Empty<ShipSystemState>(),
            ReactorBlock = ship.ReactorBlock,
            DistributionBlock = ship.DistributionBlock,
            BatteryBlock = ship.BatteryBlock,
            NavigationConsole = ship.NavigationConsole,
            HelmConsole = ship.HelmConsole,
            StorageRacks = ship.StorageRacks,
            RackSlots = Array.Empty<ItemType?>(),
            WallBlocks = ship.WallBlocks,
            WallBlockStates = enemy.WallBlockStates,
            RoomOxygen = enemy.RoomOxygen,
            Characters = Array.Empty<CharacterState>(),
            Power = power,
            // The player's wires and junctions must not appear on the enemy.
            Wiring = new WiringSnapshot(Array.Empty<Component>(), Array.Empty<ComponentState>(), Array.Empty<Wire>(),
                Array.Empty<WireState>(), Array.Empty<ComponentMount>(), Array.Empty<ComponentMountState>()),
            CardTable = ship.CardTable,
            CardGame = null,
            FrontsGame = null,
            Jukebox = null,
            CurrentShipKind = ShipKind.Custom,
            ShipForwardDegrees = ship.ForwardDegrees,
            Cameras = ship.Cameras,
            Terminals = ship.Terminals.Select(t => new TerminalState(t, true)).ToArray(),
            WallLamps = ship.WallLamps,
            SupplementalWallTiles = ship.SupplementalWallTiles,
            ForcedFloorTiles = ship.ForcedFloorTiles,
            WallOpenSideOverrides = ship.WallOpenSideOverrides,
            WallMaterialOverrides = ship.WallMaterialOverrides,
            JunctionBoxes = ship.JunctionBoxes,
            DoorEdges = ship.DoorEdges,
            DoorEdgeStates = null,
            RoomHp = null,
            WreckPatches = ship.WreckPatches,
            DecorativeDevices = ship.DecorativeDevices,
            ShipStatusMonitors = ship.ShipStatusMonitors,
            CommsConsoles = ship.CommsConsoles,
            ExtraNavigationConsoles = ship.ExtraNavigationConsoles,
            ExtraHelmConsoles = ship.ExtraHelmConsoles,
            PendingRoomBuilds = null,
            EngineStates = null,
            ShipDebris = null,
            ShipBlasts = null,
            Autopilot = null,
        };
    }
}
