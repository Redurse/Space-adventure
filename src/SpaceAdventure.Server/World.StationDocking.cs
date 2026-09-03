using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

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
            character.RoomId = Ship.AirlockOuterDoors.First().RoomId;
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
        var outerDoor = Ship.AirlockOuterDoors.First();
        var rooms = Ship.Rooms.Concat(Station.Rooms).ToList();
        // Same id as the ship's own outer door, so it opens and closes with it - the connector and
        // that door are physically the same rectangle once mated.
        var connector = new Door(outerDoor.Id, outerDoor.RoomId, Station.DockRoomId,
            outerDoor.X, outerDoor.Y, outerDoor.Width, outerDoor.Height);
        var doors = Ship.Doors.Append(connector).Concat(Station.Doors).ToList();
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
    // is kept synced to the live door-open state, via SyncShipTiles's AirlockOuterDoors loop;
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
        return merged;
    }

    private bool IsStationRoom(string roomId) => Station.Rooms.Any(r => r.Id == roomId);
}
