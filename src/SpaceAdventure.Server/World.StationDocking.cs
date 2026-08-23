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
        _shipFieldPosition = DockBerthPosition;
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

        // Otherwise the ship would just sit dead still exactly where it was (TryDockAtStation
        // zeroed velocity, and docking's own auto-stabilize hold - still true from that same call -
        // would instantly cancel out anything short of a real thruster burn). Releasing that hold
        // and giving one small push lets ordinary inertia carry it clear on its own. -X is screen
        // "left" in the same world/field frame GalaxyMapPanel draws directly (no flip) - the same
        // side the map's own docked-offset fix (GalaxyMapPanel.cs) never draws the station on, so
        // this always drifts away from wherever the station just appeared, not into it.
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

    private bool IsStationRoom(string roomId) => Station.Rooms.Any(r => r.Id == roomId);
}
