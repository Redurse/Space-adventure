using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Manual docking (game_design.md section 5/10 - stations are walkable, reached by physically
// docking): arriving at a Station-kind galaxy point no longer teleports the player straight into
// the station menu. Instead it drops the ship into VoyagePhase.StationApproach, a small manual
// flight using the exact same helm/thrust physics as the asteroid field (World.ShipField.cs's
// IntegrateShipFieldMotion) - bring the ship alongside the station's docking port slowly, and a
// "Стыковка" button appears at the helm. Docking is that deliberate press, not an automatic
// capture: drifting into the berth by accident shouldn't dock you, and the button is what makes
// the whole approach readable rather than something that just happens.
//
// Once docked, walking through the ship's own outer airlock (the same door EVA already uses)
// leads directly onto the station and back - no suit needed, it's a sealed connector, not vacuum.
public sealed partial class World
{
    private const float DockCaptureRadius = 4f; // how close to the berth counts as "alongside"
    private const float DockMaxSpeed = 2f; // must be crawling, not ramming, for the button to arm
    private const float StationApproachStartDistance = 20f; // fixed starting distance, straight down +X toward the station
    private const float HullClearance = 0.1f; // shrinks the hull for the collision test, so mating flush isn't a crash

    // Where the hull's centre has to end up for the ship's own outer airlock door to sit exactly on
    // top of the station's connector. Both structures are laid out in the same interior frame
    // (Station.Create's connectorAnchor), so mating them is a pure translation: park the hull here
    // with zero rotation and the two frames differ by exactly Station.WorldOffset - the ship's
    // interior, the station's interior and the field outside become one continuous coordinate
    // system, which is what removes the last hidden transition in the game.
    public Vec2 DockBerthPosition => Station.WorldOffset + GetHullLocalBounds().Center;

    private void EnterStationApproach()
    {
        Phase = VoyagePhase.StationApproach;
        _shipFieldPosition = DockBerthPosition - new Vec2(StationApproachStartDistance, 0);
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        // Bow already pointing at the station, whichever way this hull's nose sits in its own
        // layout - a forgiving line-up that doesn't start the approach with a turn.
        _shipRotationDegrees = -Ship.ForwardDegrees;
        _shipAutoStabilize = true;
    }

    // True while the ship is parked alongside the berth slowly enough to mate with it - what arms
    // the helm's "Стыковка" button (the client mirrors this to decide whether to draw it). A
    // faction whose territory this is can refuse the ship outright at deep enough hostility
    // (World.Factions.cs) - the approach itself is still allowed, so nothing strands the ship
    // mid-flight, but the button never arms and the crew is left to fix things elsewhere.
    public bool CanDockNow =>
        Phase == VoyagePhase.StationApproach &&
        (DockBerthPosition - _shipFieldPosition).Length() < DockCaptureRadius &&
        _shipVelocity.Length() < DockMaxSpeed &&
        GetStanding(OwnerOf(_travelTargetPointId!)) > FactionDefinitions.WarThreshold;

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
        // _travelTargetPointId was never cleared on arrival for a station (World.Voyage.cs's
        // Arrive) - it's still the point we were heading to the whole time we were maneuvering.
        EnterStation(_travelTargetPointId!);
    }

    private void StepStationApproachPhysics(double deltaSeconds)
    {
        var candidatePosition = IntegrateShipFieldMotion(deltaSeconds)
            .Clamp(0, 0, AsteroidField.Width, AsteroidField.Height);

        // The station's own compartments are solid: shoulder into them and the ship stops dead
        // rather than passing through or taking damage (its hull is sturdier than a lone asteroid,
        // and this isn't combat). Tested against the real room footprint rather than a circle, now
        // that the station is drawn as the shape it actually is - and against the hull's four
        // corners rather than its centre, so a long ship can't slide its nose through a wall.
        if (HullTouchesStation(candidatePosition))
        {
            _shipVelocity = Vec2.Zero;
            return;
        }

        _shipFieldPosition = candidatePosition;
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
