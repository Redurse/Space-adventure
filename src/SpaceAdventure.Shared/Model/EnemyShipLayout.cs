namespace SpaceAdventure.Shared.Model;

// The enemy ship as a physical, boardable structure (game_design.md section 12, Phase 3 -
// "абордаж: пробоина/вражеский корабль как путь внутрь, бой отсек за отсеком"). Same shape as
// Station: its own Rooms/Doors plus one AirlockOuterDoor-style hatch that crosses in from EVA,
// walked with the shared RoomLayout collision. It has no power grid and no repairable systems -
// only crew to fight and, since the hull it was breached through leaks, air to lose.
//
// Which hull you board depends on the ship (EnemyShipClass, laid out in EnemyShipLayout.Classes.cs)
// rather than there being one plan for every enemy in the game.
public sealed partial class EnemyShipLayout
{
    public EnemyShipClass Kind { get; }
    public string Name { get; }
    public IReadOnlyList<Room> Rooms { get; }
    public IReadOnlyList<Door> Doors { get; }
    // The hull breach the boarding party climbs through - the same "one side is a real room, the
    // other is outside this structure" shape as Ship.AirlockOuterDoors and Station.ShipConnector.
    // It is also a hole: the compartment behind it is in vacuum and stays that way.
    public AirlockOuterDoor BoardingHatch { get; }
    public IReadOnlyList<EnemyCrewSpawn> CrewSpawns { get; }
    public string BoardingRoomId { get; }

    public EnemyShipLayout(EnemyShipClass kind, string name, IReadOnlyList<Room> rooms, IReadOnlyList<Door> doors,
        AirlockOuterDoor boardingHatch, IReadOnlyList<EnemyCrewSpawn> crewSpawns, string boardingRoomId)
    {
        Kind = kind;
        Name = name;
        Rooms = rooms;
        Doors = doors;
        BoardingHatch = boardingHatch;
        CrewSpawns = crewSpawns;
        BoardingRoomId = boardingRoomId;
    }

    public (Vec2 Position, string RoomId) MoveAlongAxis(Vec2 position, string roomId, Vec2 delta, Func<string, bool> isDoorOpen) =>
        RoomLayout.MoveAlongAxis(Rooms, Doors, position, roomId, delta, isDoorOpen);

    // Every class, built once and shared - the server registers all of their doors up front, the
    // same way it does for every station kind, because door state is one flat dictionary across
    // every structure in the game and which hull is in front of you changes mid-fight.
    public static IReadOnlyList<EnemyShipLayout> All { get; } = new[]
    {
        Create(EnemyShipClass.Raider),
        Create(EnemyShipClass.Freighter),
        Create(EnemyShipClass.Gunship),
    };

    public static EnemyShipLayout Of(EnemyShipClass kind) => All.First(l => l.Kind == kind);

    public static EnemyShipLayout CreateDefault() => Of(EnemyShipClass.Raider);
}

// Where a defender starts, what it fights with, and whether it is wearing a suit - runtime health
// lives server-side (World.Boarding.cs), same split as Turret/TurretRuntime. A suited defender goes
// on fighting in a vented compartment; an unsuited one is on a clock the moment its air goes.
public sealed record EnemyCrewSpawn(string Id, string Name, string RoomId, float X, float Y, ItemType Weapon, bool Suited = false)
{
    public Vec2 Position => new(X, Y);
}
