namespace SpaceAdventure.Shared.Model;

// Every station the ship can dock at shares this same physical layout and NPC roster for now
// (game_design.md section 10 says stations differ by type/services, but that differentiation is a
// later refinement). Physical rooms/doors mirror Ship's own model exactly (game_design.md section
// 5 - "тоже модульные, как корабль, можно ходить по ним пешком после стыковки") so the client can
// reuse the exact same interior-rendering/movement-collision code path. Position is the station's
// fixed location in the docking-approach field space (it never moves or rotates, unlike the ship)
// and doubles as the docking target - line the ship up within DockCaptureRadius of it at low
// speed (World.StationDocking.cs) to dock.
public sealed partial class Station
{
    public IReadOnlyList<Room> Rooms { get; }
    public IReadOnlyList<Door> Doors { get; }
    // The umbilical back to the ship's own airlock chamber - same "one side is a real room, the
    // other isn't part of this structure's own Doors list" shape as Ship.AirlockOuterDoors, just
    // crossing into a different physical structure instead of into vacuum.
    public AirlockOuterDoor ShipConnector { get; }
    // Purely a target for the welder/cutter's aim-HP-bar UI (World.WallBlocks.cs's own comment on
    // FindAimedStationWallBlock explains why) - a station itself is never actually breachable, so
    // unlike Ship.WallBlocks this list never needs a mutable Hp dictionary behind it.
    public IReadOnlyList<WallBlock> WallBlocks { get; }
    public IReadOnlyList<StationNpc> Npcs { get; }
    // Station property that can be stolen (game_design.md section 10, World.StationCrime.cs).
    public IReadOnlyList<StationCrate> Crates { get; }
    public string DockRoomId { get; }

    // Rooms/doors/NPCs are laid out in the *docked* frame: the frame the ship's own interior uses,
    // positioned so ShipConnector lands exactly on the ship's outer airlock door. Docking snaps the
    // hull onto DockBerthPosition with zero rotation (World.StationDocking.cs), after which those
    // two frames differ by exactly WorldOffset - which is what lets one camera and one coordinate
    // system cover ship, station and open space with no jump anywhere in between. Settable (not
    // just constructor-assigned) because the same shared per-kind Station instance is repositioned
    // to whichever GalaxyPoint's own map position the ship is actually approaching right now
    // (RepositionTo, called from World.Voyage.cs's Arrive) - otherwise every station of the same
    // kind would physically sit at the one spot this class happened to be built at.
    public Vec2 WorldOffset { get; private set; }

    // Moves this station (and everything anchored to it - Position, DockingPortPosition, the
    // docked room layout) so its own Position lands exactly on worldCenter - the GalaxyPoint's real
    // map coordinate, not the fixed spot every station used to share regardless of which one was
    // actually being flown to.
    public void RepositionTo(Vec2 worldCenter) => WorldOffset = worldCenter - Center;

    public Vec2 Center { get; }
    public Vec2 HalfExtents { get; }

    // Station centre / berth mouth in the field's world space - what the exterior view, the radar
    // and the approach physics work in.
    public Vec2 Position => Center + WorldOffset;
    public Vec2 DockingPortPosition => ShipConnector.Position + WorldOffset;

    private readonly Dictionary<string, Room> _roomsById;

    public Station(IReadOnlyList<Room> rooms, IReadOnlyList<Door> doors, AirlockOuterDoor shipConnector,
        IReadOnlyList<StationNpc> npcs, IReadOnlyList<StationCrate> crates, Vec2 worldCenter, string dockRoomId)
    {
        Rooms = rooms;
        Doors = doors;
        ShipConnector = shipConnector;
        WallBlocks = BuildWallBlocks(rooms, doors, new[] { shipConnector });
        Npcs = npcs;
        Crates = crates;
        DockRoomId = dockRoomId;
        _roomsById = rooms.ToDictionary(r => r.Id);

        var minX = rooms.Min(r => r.Left);
        var maxX = rooms.Max(r => r.Right);
        var minY = rooms.Min(r => r.Top);
        var maxY = rooms.Max(r => r.Bottom);
        Center = new Vec2((minX + maxX) / 2, (minY + maxY) / 2);
        HalfExtents = new Vec2((maxX - minX) / 2, (maxY - minY) / 2);
        WorldOffset = worldCenter - Center;
    }

    // True for a point given in the station's own (docked) frame - the approach physics converts
    // the ship's world position into it before asking.
    public bool ContainsPoint(Vec2 point) => _roomsById.Values.Any(r => r.Contains(point));

    public Room GetRoom(string roomId) => _roomsById[roomId];

    public (Vec2 Position, string RoomId) MoveAlongAxis(Vec2 position, string roomId, Vec2 delta, Func<string, bool> isDoorOpen) =>
        RoomLayout.MoveAlongAxis(Rooms, Doors, position, roomId, delta, isDoorOpen);

}
