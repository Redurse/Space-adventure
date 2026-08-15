using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Simulation root, split into topic partials: World.Movement.cs, World.Combat.cs,
// World.Interact.cs, World.Voyage.cs, World.EnemyAi.cs, World.Atmosphere.cs, World.Reactor.cs.
public sealed partial class World
{
    public long Tick { get; set; }
    public Ship Ship { get; } = Ship.CreateStarter();
    public PowerGrid PowerGrid { get; } = new();
    public ShieldSystem Shield { get; } = new();
    public EnemyShip Enemy { get; } = new(maxHp: 100f);
    public Station Station { get; } = Station.CreateDefault();
    public VoyagePhase Phase { get; private set; } = VoyagePhase.Station;

    private readonly Dictionary<int, Character> _characters = new();
    private readonly Dictionary<int, Vec2> _moveInput = new();
    private readonly Dictionary<string, TurretRuntime> _turretRuntimes;
    private readonly Dictionary<string, float> _turretAimInput = new();
    private readonly HashSet<string> _breachedWallBlockIds = new();

    public World()
    {
        _turretRuntimes = Ship.Turrets.ToDictionary(t => t.Id, t => new TurretRuntime(t));
        foreach (var room in Ship.Rooms)
            _roomOxygen[room.Id] = FullOxygen;

        var home = GalaxyMap.GetPoint(GalaxyMap.HomePointId);
        _shipMapPosition = home.Position;
        _dockedPointId = home.Id;
    }

    public void SpawnCharacter(int playerId) => _characters[playerId] = new Character(playerId, Ship.SpawnPoint, Ship.SpawnRoomId);

    public void ApplyCommand(int playerId, ClientCommand command)
    {
        _moveInput[playerId] = new Vec2(command.MoveX, command.MoveY);
        PowerGrid.ApplyInput(command.PowerSystemIndex, command.PowerDirection);

        var character = _characters[playerId];

        if (command.InteractPressed)
            HandleInteract(character);

        if (command.ToggleHoldSlotIndex >= 0)
            character.Inventory.ToggleHold(command.ToggleHoldSlotIndex);

        if (command.ToggleReactorSlotIndex >= 0)
            ToggleReactorSlot(character, command.ToggleReactorSlotIndex);

        if (command.TravelToPointId is not null)
            TryStartTravel(command.TravelToPointId);

        if (character.ManningTurretId is { } turretId)
        {
            _turretAimInput[turretId] = command.TurretAimDirection;
            if (command.FirePressed)
                TryFire(_turretRuntimes[turretId]);
        }
    }

    public void Step(double deltaSeconds)
    {
        StepCharacters(deltaSeconds);
        StepTurrets(deltaSeconds);
        StepVoyage(deltaSeconds);
        StepEnemyAi(deltaSeconds);
        StepAtmosphere(deltaSeconds);
        PowerGrid.Step(deltaSeconds);
        Shield.Step(deltaSeconds, PowerGrid.GetAllocation(PowerSystemId.Shields));
    }

    public WorldSnapshot CreateSnapshot() => new(
        Tick,
        Ship.Rooms,
        Ship.Doors,
        Ship.Turrets,
        _turretRuntimes.Values.Select(t => new TurretState(
            t.Definition.Id, t.AimDegrees, t.MannedByPlayerId, t.CooldownRemaining,
            t.AmmoRemaining, t.Definition.MagazineCapacity, t.Charge, t.Definition.MaxCharge, t.Damaged)).ToArray(),
        Ship.AmmoStorages,
        Ship.SuitLockers,
        Ship.ToolStations,
        Ship.SystemDevices,
        Ship.SystemDevices.Select(d => new ShipSystemState(d.System, PowerGrid.IsDamaged(d.System))).ToArray(),
        Ship.ReactorBlock,
        Ship.DistributionBlock,
        Ship.NavigationConsole,
        GalaxyMap.Points,
        Ship.AirlockConsole,
        Station.Npcs,
        new ReactorState(
            PowerGrid.Reactor.RodSlots.ToArray(),
            PowerGrid.Reactor.Fuel,
            PowerGrid.Reactor.MaxFuel,
            PowerGrid.Reactor.CurrentOutput,
            PowerGrid.Reactor.MaxOutput),
        new ShieldState(Shield.Points, ShieldSystem.MaxPoints),
        Ship.WallBlocks,
        Ship.WallBlocks.Select(b => new WallBlockState(b.Id, _breachedWallBlockIds.Contains(b.Id))).ToArray(),
        Ship.Rooms.Select(r => new RoomOxygenState(r.Id, _roomOxygen[r.Id])).ToArray(),
        new EnemyShipState(Enemy.Hp, Enemy.MaxHp, Enemy.IsRetreating),
        _characters.Values.Select(c => new CharacterState(
            c.PlayerId, c.Position.X, c.Position.Y, c.CarryingAmmoCrate, c.Health, c.WearingSuit, c.SuitActionRemaining,
            c.FacingDirection.X, c.FacingDirection.Y,
            new InventoryState(
                c.Inventory.MainSlots.ToArray(),
                new Dictionary<EquipSlot, ItemType?>(c.Inventory.Equipped),
                c.Inventory.HeldSlotIndices.ToArray()))).ToArray(),
        PowerGrid.CreateState(),
        new VoyageState(Phase, _shipMapPosition, _dockedPointId, _travelTargetPointId));
}
