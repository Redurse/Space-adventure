using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Manual docking (game_design.md section 5/10 - stations are walkable, reached by physically
// docking): the ship is always hand-flown (World.ShipField.cs's own physics, now including station-
// hull collision), and the nearest station in the system is continuously tracked
// (World.Voyage.cs's UpdateNearestStation) rather than picked by clicking a destination - fly up to
// any station's berth slowly enough and a "Стыковка" button appears at the helm. Docking is that
// deliberate press, not an automatic capture: drifting into the berth by accident shouldn't dock
// you, and the button is what makes the whole approach readable rather than something that just
// happens.
//
// Once docked, walking through the ship's own outer airlock (the same door EVA already uses)
// leads directly onto the station and back - no suit needed, it's a sealed connector, not vacuum.
public sealed partial class World
{
    private const float DockCaptureRadius = 4f; // how close to the berth counts as "alongside"
    private const float DockMaxSpeed = 2f; // must be crawling, not ramming, for the button to arm
    private const float HullClearance = 0.1f; // shrinks the hull for the collision test, so mating flush isn't a crash
    // M48 follow-up - "при отстыковке корабль медленно и плавно немного уходил от станции влево":
    // a gentle one-time push-off, not a sustained thruster burn - this game's own ship physics has
    // no passive drag anywhere (World.ShipField.cs), so a small velocity here coasts at that same
    // slow speed indefinitely on its own until the pilot actually takes the stick.
    private const float UndockDriftSpeed = 0.6f;

    // Direct user report ("но у меня на корабле 2 шлюза") - a Ship-Editor-built ship can have a
    // real airlock two different ways now: a rect-based vacuum-facing Door (Door.LeadsToVacuum - a
    // compartment prefab like "Шлюз 1", or a hand-authored hull) or a plain door edge placed onto
    // open space via the Door/"Шлюз" palette tool (this session's earlier "дверь будет считаться
    // шлюзом" feature, TileGrid.CanPlaceDoorEdge). Docking/boarding only ever knew about the first
    // kind - this record + resolver is the one place that picks WHICHEVER kind actually exists, so
    // GetOrCreateStation/GetDockedLayout/PullCrewOffStation never have to special-case which system
    // built the ship. (These two are still genuinely separate representations - rect vs edge - not
    // unified by humble-soaring-cat.md's "убрать AirlockOuterDoor как отдельный тип", which only
    // removed the rect-based type's own now-redundant sibling.)
    private readonly record struct ShipAirlock(string Id, string RoomId, Vec2 Position, EdgeSide Side, bool Vertical, float Width, float Height);

    private ShipAirlock? ResolveShipAirlock()
    {
        if (Ship.VacuumDoors.Count > 0)
        {
            var door = Ship.VacuumDoors[0];
            var room = Ship.Rooms.First(r => r.Id == door.RoomAId);
            var side = Ship.InferAirlockSide(room, door.X, door.Y);
            return new ShipAirlock(door.Id, door.RoomAId, door.Position, side, door.IsVertical, door.Width, door.Height);
        }

        var edge = Ship.DoorEdges.FirstOrDefault(e => e.RoomAId is null || e.RoomBId is null);
        if (edge is null)
            return null;

        var roomId = edge.RoomAId ?? edge.RoomBId!;
        // Ship.DoorEdges' own Side is always East or South (TileGrid.CanonicalEdgeKey) regardless of
        // which of the two flanking tiles is the room - if the room is on the FAR side (RoomAId
        // null, meaning Coord itself is the vacuum tile), the room actually faces the OPPOSITE
        // compass direction from the canonical Side.
        var facingFromRoom = edge.RoomAId is null ? edge.Side.Opposite() : edge.Side;
        var edgeSide = facingFromRoom switch
        {
            TileSide.North => EdgeSide.Top,
            TileSide.South => EdgeSide.Bottom,
            TileSide.East => EdgeSide.Right,
            _ => EdgeSide.Left,
        };
        // The seam's own center, in the same continuous room-local coordinates Ship.Custom.cs's
        // doorEdges builder already used to resolve RoomAId/RoomBId in the first place.
        var position = edge.Side == TileSide.East
            ? new Vec2(edge.Coord.X + 1f, edge.Coord.Y + 0.5f)
            : new Vec2(edge.Coord.X + 0.5f, edge.Coord.Y + 1f);
        // A single tile-edge door has no separate span - both axes are exactly 1 unit, same
        // Door.FootprintRect-scale narrow door every hand-painted single-tile interior door already
        // uses (Ship.Custom.cs's BuildDoors, `doorDef.Wide ? ... : 1f`).
        return new ShipAirlock(edge.Id, roomId, position, edgeSide, Vertical: edge.Side == TileSide.East, Width: 1f, Height: 1f);
    }

    // Where the hull's centre has to end up for the ship's own outer airlock door to sit exactly on
    // top of the station's connector. Both structures are laid out in the same interior frame
    // (Station.Create's connectorAnchor), so mating them is a pure translation: park the hull here
    // with zero rotation and the two frames differ by exactly Station.WorldOffset - the ship's
    // interior, the station's interior and the field outside become one continuous coordinate
    // system, which is what removes the last hidden transition in the game.
    public Vec2 DockBerthPosition => Station.WorldOffset + GetHullLocalBounds().Center;

    // True while the ship is parked alongside the nearest station's berth slowly enough to mate
    // with it - what arms the helm's "Стыковка" button (the client mirrors this to decide whether
    // to draw it). A faction whose territory this is can refuse the ship outright at deep enough
    // hostility (World.Factions.cs) - flying up to the berth itself is still allowed, so nothing
    // strands the ship mid-flight, but the button never arms and the crew is left to fix things
    // elsewhere. Mid-fight the same station can instead be actively defending itself
    // (World.Voyage.cs's UpdateNearestStation) - docking is refused then too, not just once things
    // are calm enough to talk.
    public bool CanDockNow =>
        !IsDocked && !IsInBattle && _nearestStationPointId is { } stationId &&
        (DockBerthPosition - _shipFieldPosition).Length() < DockCaptureRadius &&
        _shipVelocity.Length() < DockMaxSpeed &&
        GetStanding(OwnerOf(stationId)) > FactionDefinitions.WarThreshold;

    // The deliberate press. Ignored unless actually alongside, so a mashed button can't dock the
    // ship from across the field. The capture radius is deliberately forgiving and the mating
    // itself exact: the clamps take over, straighten the ship out and pull it the last few metres
    // onto the berth, exactly like a real docking collar.
    private void TryDockAtStation()
    {
        if (!CanDockNow)
            return;

        _shipRotationDegrees = 0f;
        SetShipFieldPosition(DockBerthPosition);
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipAutoStabilize = true;
        EnterStation(_nearestStationPointId!);
    }

    // Same button either way (the helm's "Стыковка"/"Отстыковаться" toggle) - docks when alongside
    // the berth, undocks when already sitting docked, so there's no separate control to hunt for
    // just to leave. A mashed press outside either state (mid-approach, mid-flight) does nothing,
    // same as TryDockAtStation's own CanDockNow gate.
    private void HandleDockButtonPressed()
    {
        if (IsDocked)
            Undock();
        else
            TryDockAtStation();
    }

    // Leaves the berth - the ship stays sitting right where it was, free to fly wherever. Nothing
    // captures it back onto the station on its own; the next dock only happens on another
    // deliberate press once it's actually alongside a berth again.
    private void Undock()
    {
        PullCrewOffStation();
        _dockedPointId = null;
        _justCastOffStation = true; // World.ShipField.cs's StepShipFieldPhysics clears this itself

        // Otherwise the ship would just sit dead-on at the berth forever (TryDockAtStation zeroed
        // velocity, and docking's own auto-stabilize hold - still true from that same call - would
        // instantly cancel out anything short of a real thruster burn). Releasing that hold and
        // giving it one small push lets ordinary inertia carry it clear on its own. -X is screen
        // "left" in the same world/field frame GalaxyMapPanel draws directly (no flip) - the same
        // side the map's own docked-offset fix (GalaxyMapPanel.cs) never draws the station on, so
        // this always drifts away from wherever the station is drawn, not into it. Stations are
        // fixed now (M59), so no departure-velocity catch-up is needed - the ship's own position
        // hasn't gone stale while docked.
        _shipAutoStabilize = false;
        _shipVelocity = new Vec2(-UndockDriftSpeed, 0f);
    }

    // Casting off (either through this button or by walking away from the docked layout entirely)
    // takes the station's rooms out of the docked layout, so anyone still standing in them would be
    // left walking around geometry that no longer connects to anything - they get pulled back
    // through the connector into the airlock chamber instead.
    private void PullCrewOffStation()
    {
        foreach (var character in _characters.Values.Where(c => c.OnStation))
        {
            character.OnStation = false;
            // Falls back to any room at all only when the ship genuinely has no airlock of either
            // kind (ResolveShipAirlock) - a ship with no real airlock at all (relaxed
            // CustomShipValidator rule) has nowhere meaningful to pull this character back through,
            // but it must still land somewhere real.
            character.RoomId = ResolveShipAirlock()?.RoomId ?? Ship.Rooms[0].Id;
            character.Position = Ship.GetRoom(character.RoomId).Center;
        }
    }

    private bool HullTouchesStation(Vec2 candidateWorldCenter)
    {
        var (localCenter, halfExtents) = GetHullLocalBounds();
        var clear = new Vec2(halfExtents.X - HullClearance, halfExtents.Y - HullClearance);

        foreach (var (sx, sy) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
        {
            var corner = candidateWorldCenter + RotateLocalToWorld(new Vec2(clear.X * sx, clear.Y * sy), _shipRotationDegrees);
            if (Station.ContainsPoint(corner - Station.WorldOffset))
                return true;
        }
        return false;
    }

    // While docked the station's rooms sit in the same coordinate system as the ship's own, joined
    // by one shared doorway, so crossing over is an ordinary walk through a door handled by
    // RoomLayout - not a special-cased teleport between two structures. Rebuilt on demand rather
    // than cached, since either side can be replaced (a bought hull, a different station kind).
    private (IReadOnlyList<Room> Rooms, IReadOnlyList<Door> Doors) GetDockedLayout()
    {
        // A ship with no real airlock at all (relaxed CustomShipValidator rule, and no vacuum-facing
        // door edge either) has no door to mate with the station's own connector - the station
        // simply stays unreachable rather than crashing; still "docked" in every other sense (galaxy
        // position, trading, etc.).
        if (ResolveShipAirlock() is not { } airlock)
            return (Ship.Rooms, Ship.Doors);
        var rooms = Ship.Rooms.Concat(Station.Rooms).ToList();
        // Same id as the ship's own outer door/door edge, so it opens and closes with it - the
        // connector and that door are physically the same rectangle once mated. Vertical passed
        // explicitly rather than left to IsVertical's Width<=Height fallback - correct either way
        // for a rect-based vacuum-facing Door (always wider on its span axis) but load-bearing for a
        // door-edge-based airlock, whose Width and Height are both exactly 1.
        var connector = new Door(airlock.Id, airlock.RoomId, Station.DockRoomId,
            (float)airlock.Position.X, (float)airlock.Position.Y, airlock.Width, airlock.Height, Vertical: airlock.Vertical);
        // The rect-based case's own airlock.Id already sits in Ship.Doors (Ship.VacuumDoors is a
        // filter over that same list, not a separate source, since humble-soaring-cat.md's "убрать
        // AirlockOuterDoor как отдельный тип") - excluded here so `connector` doesn't end up listed
        // twice under the same id once mated.
        var doors = Ship.Doors.Where(d => d.Id != airlock.Id).Append(connector).Concat(Station.Doors).ToList();
        return (rooms, doors);
    }

    // Bug fix (humble-soaring-cat.md, "стены не имеют коллизии") - the tile-collision equivalent of
    // GetDockedLayout above, used by World.Movement.cs instead of the old RoomLayout system. Found
    // live: while docked, movement used to go through RoomLayout.MoveAlongAxis, whose walls are
    // still the OLD pre-M73 zero-thickness convention (clamped to the room's own rectangle edge,
    // Room.Top + CharacterRadius) - but M75's renderer has drawn every wall as a real, full 1-unit-
    // thick tile for a while now, one tile further INTO the room than that old clamp stops at. A
    // fresh campaign starts docked (World.cs's own constructor: "a fresh run starts docked"), so
    // this was the actual live movement path for a large share of ordinary play, not a corner case -
    // a character could stand anywhere from the room's old rectangle edge up to a full tile deeper,
    // reading as visibly standing inside the wall's own rendered plating. Ship.Tiles and Station.Tiles
    // already share one coordinate frame (Station.cs's own doc comment: "positioned so ShipConnector
    // lands exactly on the ship's outer airlock door") and never otherwise overlap, so a plain
    // Cells-dictionary union (Ship's own cell wins at the one shared connector coordinate - it alone
    // is kept synced to the live door-open state, via SyncShipTiles's vacuum-door loop;
    // Station.Tiles's separate copy of that same tile is never synced) is exact, with no region
    // recompute needed: TileMovement only ever calls CellAt/IsWalkable, never reads Regions, so
    // going through TileGrid's own SetFloor/SetWall mutators here would pay for BFS region-merging
    // work movement itself has no use for. Rebuilt on demand rather than cached, same "either side
    // can be replaced" reasoning GetDockedLayout above already gives for staying uncached.
    private TileGrid GetDockedTileGrid()
    {
        var merged = new TileGrid();
        foreach (var (coord, cell) in Ship.Tiles.Cells)
            merged.Cells[coord] = cell;
        foreach (var (coord, cell) in Station.Tiles.Cells)
            if (!merged.Cells.ContainsKey(coord))
                merged.Cells[coord] = cell;
        // M-doors-as-edges - same plain-union reasoning as Cells above: a door edge only ever sits
        // between 2 tiles of the SAME structure (the ship editor never lets one span a coordinate
        // the station also occupies), so there's no possible key collision to resolve here either.
        foreach (var (key, edge) in Ship.Tiles.DoorEdges)
            merged.DoorEdges[key] = edge;
        foreach (var (key, edge) in Station.Tiles.DoorEdges)
            merged.DoorEdges[key] = edge;
        return merged;
    }

    private bool IsStationRoom(string roomId) => Station.Rooms.Any(r => r.Id == roomId);
}
