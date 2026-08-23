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
    // Two locked hatches, same "one side is a real room, the other is outside this structure" shape
    // as Ship.AirlockOuterDoors - closed by default, same as a real airlock, not a standing-open
    // hole. Getting in means cutting one open (World.Cutting.cs) or cutting straight through a
    // WallBlock instead; either way, the compartment behind whichever one gave way is in vacuum and
    // stays that way.
    public IReadOnlyList<AirlockOuterDoor> AirlockOuterDoors { get; }
    public IReadOnlyList<EnemyCrewSpawn> CrewSpawns { get; }
    // Which compartment a boarding party is nominally headed for - AirlockOuterDoors[0]'s own room.
    // With two real hatches (plus any wall panel) there's no single fixed way in any more; this only
    // still matters for generic tests/atmosphere checks that just need *a* valid interior room.
    public string BoardingRoomId { get; }
    // The hull's own fixed turret loadout (e.g. Frigate's 2 magnetic + 1 laser), or null to keep the
    // older behavior of one weapon per hull picked by squadron slot (World.EnemyFleet.cs's
    // EnemyWeaponFor) - a class only needs this when its weapons are a defining trait of the hull
    // itself rather than whatever the squadron formation happens to hand it.
    public IReadOnlyList<TurretWeaponType>? WeaponLoadout { get; }
    // The hull's own cuttable exterior, derived purely from Rooms/Doors the same way a station's is
    // (Station.WallBlocks.cs's BuildWallBlocks, reused verbatim) - so a raider is a real boardable
    // structure with a real hull, not just a hatch you fly up to: any exterior wall can be cut open
    // from EVA (World.Cutting.cs) exactly like the player's own ship, and climbed through once open.
    public IReadOnlyList<WallBlock> WallBlocks { get; }

    public EnemyShipLayout(EnemyShipClass kind, string name, IReadOnlyList<Room> rooms, IReadOnlyList<Door> doors,
        IReadOnlyList<AirlockOuterDoor> airlockOuterDoors, IReadOnlyList<EnemyCrewSpawn> crewSpawns, string boardingRoomId,
        IReadOnlyList<TurretWeaponType>? weaponLoadout = null)
    {
        Kind = kind;
        Name = name;
        Rooms = rooms;
        Doors = doors;
        AirlockOuterDoors = airlockOuterDoors;
        CrewSpawns = crewSpawns;
        BoardingRoomId = boardingRoomId;
        WeaponLoadout = weaponLoadout;
        WallBlocks = Station.BuildWallBlocks(rooms, doors, airlockOuterDoors);
    }

    // Bounding box of the hull's own Rooms in its local frame - the same "centre + rotate" anchor
    // World.GetHullLocalBounds/ShipLocalFrame.GetHullCenter already use for the player's own ship,
    // so a WallBlock's local position can be turned into a world position via
    // EnemyShipRuntime.Position/RotationDegrees the identical way.
    public (Vec2 Center, Vec2 HalfExtents) GetLocalBounds()
    {
        var minX = Rooms.Min(r => r.Left);
        var maxX = Rooms.Max(r => r.Right);
        var minY = Rooms.Min(r => r.Top);
        var maxY = Rooms.Max(r => r.Bottom);
        return (new Vec2((minX + maxX) / 2, (minY + maxY) / 2), new Vec2((maxX - minX) / 2, (maxY - minY) / 2));
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
        Create(EnemyShipClass.Frigate),
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
