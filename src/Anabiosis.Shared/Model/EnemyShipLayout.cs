namespace Anabiosis.Shared.Model;

// The hostile ship as a real ship (direct user request: enemies are simulated ships made of compartments, like the
// player's, with people moving about it). Every enemy is the same hull - the frozen "крутой корабль"
// (ShipBuiltInFleet.CoolShip) - built into a full Ship, so it has the same rooms, devices, turrets, engines, wall
// blocks and tile grid the player's own ship has. This class is the boardable-structure view of that ship that the
// older boarding/cutting/EVA code was written against (Rooms, WallBlocks, Tiles, CrewSpawns...), kept so that code
// keeps working over the new data; there is no separate hand-drawn plan per enemy class any more.
//
// Each enemy gets its OWN layout instance (EnemyShipRuntime.Layout): compartments of a hostile ship can be destroyed,
// which rebuilds its Ship without them.
public sealed class EnemyShipLayout
{
    public string Name { get; }
    // What the ship was built from - sent to clients so they can draw the interior with the same renderer as the player's ship.
    public CustomShipDefinition Definition { get; }
    // The real ship: rooms, devices, turrets, engines, wall blocks and tiles.
    public Ship Ship { get; }
    public IReadOnlyList<Room> Rooms => Ship.Rooms;
    // The saved hull has no rectangular doors, only door edges (Ship.DoorEdges, living on Ship.Tiles) - this list stays for
    // the code that still asks for rectangular doors and is simply empty.
    public IReadOnlyList<Door> Doors => Ship.Doors.Where(d => !d.LeadsToVacuum).ToList();
    // Boarders cut straight through the hull plating (WallBlocks); there are no separate locked hatches.
    public IReadOnlyList<Door> OuterHatches { get; } = Array.Empty<Door>();
    public IReadOnlyList<WallBlock> WallBlocks => Ship.WallBlocks;
    public TileGrid Tiles => Ship.Tiles;
    public IReadOnlyList<EnemyCrewSpawn> CrewSpawns { get; }
    // Which compartment a boarding party is nominally headed for.
    public string BoardingRoomId { get; }
    // What the hull's own turrets fire (one entry per turret).
    public IReadOnlyList<TurretWeaponType> WeaponLoadout => Ship.Turrets.Select(t => t.WeaponType).ToList();

    private EnemyShipLayout(Ship ship, string name, CustomShipDefinition definition)
    {
        Definition = definition;
        Ship = ship;
        Name = name;
        BoardingRoomId = ship.Rooms[0].Id;
        CrewSpawns = EnemyCrewPlanner.Plan(ship);
        // A hostile crew doesn't shut doors on itself: every door edge of the hull stays open, so walking
        // (theirs and a boarder's) and the air between compartments are decided by walls and breaches alone.
        foreach (var (coord, side) in ship.Tiles.DoorEdges.Keys.ToList())
            ship.Tiles.SetDoorEdgeOpen(coord, side, true);
    }

    // A fresh, undamaged enemy hull.
    public static EnemyShipLayout Create() =>
        new(Ship.FromCustomDefinition(ShipBuiltInFleet.CoolShip), "Крейсер", ShipBuiltInFleet.CoolShip);

    // The same hull after losing compartments (its definition shrunk by World.RoomHp-style destruction).
    public static EnemyShipLayout FromDefinition(CustomShipDefinition definition, string name = "Крейсер") =>
        new(Ship.FromCustomDefinition(definition), name, definition);

    // Shared undamaged instance for places that just need *a* valid enemy structure outside a fight (snapshot fallback,
    // door registration). Never mutated.
    public static EnemyShipLayout Default { get; } = Create();

    // Bounding box of the hull's own Rooms in its local frame - the same "centre + rotate" anchor
    // World.GetHullLocalBounds/ShipLocalFrame.GetHullCenter already use for the player's own ship.
    public (Vec2 Center, Vec2 HalfExtents) GetLocalBounds()
    {
        var minX = Rooms.Min(r => r.Left);
        var maxX = Rooms.Max(r => r.Right);
        var minY = Rooms.Min(r => r.Top);
        var maxY = Rooms.Max(r => r.Bottom);
        return (new Vec2((minX + maxX) / 2, (minY + maxY) / 2), new Vec2((maxX - minX) / 2, (maxY - minY) / 2));
    }

    public (Vec2 Position, string RoomId) MoveAlongAxis(Vec2 position, string roomId, Vec2 delta, Func<string, bool> isDoorOpen)
    {
        var next = TileMovement.MoveAlongAxis(Tiles, position, delta);
        return (next, TileMovement.RoomIdAt(Rooms, next) ?? roomId);
    }
}
