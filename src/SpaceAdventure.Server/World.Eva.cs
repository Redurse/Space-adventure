using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Stepping outside (game_design.md Phase 3, M17): from the airlock chamber, walking into an open
// AirlockOuterDoor while wearing a suit crosses into the same AsteroidField world space the ship
// itself occupies - not a separate scene. Outside, a character defaults to "magnetized" (moves
// rigidly with whatever it's attached to - the ship or an asteroid) until it deliberately pushes
// off toward the mouse cursor, becoming a free-floating body with jetpack fuel for correction.
// Run out of fuel and you drift forever on whatever velocity you had.
public sealed partial class World
{
    private const float EvaWalkSpeed = 3f; // matches interior MoveSpeed
    // How near a drifting character has to get before the boots grab on - contact, not proximity.
    // It used to be a couple of units of slack, which meant a jump was snatched out of the air and
    // snapped onto the plating while the suit was still visibly short of it. Only barely wider than
    // where the character ends up standing (HullWalkClearance), so grabbing on is a touch and the
    // snap that follows it moves you almost nowhere. Tunnelling isn't a risk at this width because
    // the whole step is sampled (TryAutoAttachAlong), not just its endpoint.
    private const float ShipAttachZoneMargin = 0.5f;
    private const float AsteroidAttachZoneMargin = 0.5f;
    // Half the character's own width, so a magnetized suit's boots touch the hull rather than
    // hovering off it: magnetized movement is constrained to the *surface*, not to a thick shell
    // around it (which used to let you wander a couple of metres off the plating, and even across
    // the middle of the hull's footprint, with nothing under your feet).
    private const float HullWalkClearance = 0.35f;
    // A shove off a handhold, not a leap: enough to cross a gap between rocks with a bit of
    // patience, slow enough that a misjudged jump is something you can watch happening and correct
    // with the jetpack rather than something that has already gone wrong by the time you see it.
    private const float PushOffSpeed = 2.4f;
    private const float EvaEntryNudge = 1.1f; // > the door's own 1-unit width, so it clears the rect entirely
    private const float JetpackAccelerationPerSecond = 1f; // gentle correction thrust, not a main engine
    private const float JetpackFuelPerSecond = 10f;
    // A push-off starts right at the edge of the very zone that re-attaches a drifting character,
    // so without something to hold it off the boots would grab again on the next tick and the push
    // would be a no-op. What holds them off is the identity of the thing pushed away from, not a
    // stretch of time: it stops catching you until you're this much clear of it, and nothing else
    // stops catching you at all.
    private const float PushOffClearMargin = 1f;

    // Absolute world position in the current AsteroidField - what the client actually renders,
    // regardless of what the character is attached to.
    public Vec2 GetEvaWorldPosition(Character character) => character.EvaAttachedTo switch
    {
        EvaAttachment.Ship => _shipFieldPosition + RotateLocalToWorld(character.EvaLocalOffset, _shipRotationDegrees),
        EvaAttachment.Asteroid => AsteroidField.Asteroids.First(a => a.Id == character.EvaAttachedAsteroidId).Position + character.EvaLocalOffset,
        _ => character.EvaLocalOffset, // None: this field just holds the world position directly
    };

    private static Vec2 RotateLocalToWorld(Vec2 local, float rotationDegrees)
    {
        var radians = rotationDegrees * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vec2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
    }

    private static Vec2 RotateWorldToLocal(Vec2 world, float rotationDegrees) => RotateLocalToWorld(world, -rotationDegrees);

    // Only reachable from inside the airlock chamber, moving into a currently-open outer door,
    // while wearing a suit (walking into vacuum unsuited would just be instant death for no
    // gameplay benefit, so it's gated out entirely rather than modeled). Attaches to the ship at
    // the door's own position so the character doesn't visually jump anywhere on the crossing.
    private bool TryCrossIntoVacuum(Character character, Vec2 moveDelta)
    {
        // Outside means anywhere the ship is actually out in the field, physically: an asteroid
        // field, a battle, approaching or transiting between points of interest (M31-M33's real,
        // physically-simulated flight) all put the ship's own hull right there to walk out onto.
        // Only docked at a station is different - the airlock leads onto the station's own
        // walkway there (Movement.cs's OnStation crossing), not into vacuum, so that's the one
        // phase this stays blocked for.
        if (!character.SuitSealed || Phase == VoyagePhase.Station)
            return false;

        // Whichever room the door is actually cut into, rather than one hardcoded chamber id: a
        // hull can carry its ports in any compartment (the Corvette puts one on each beam), and
        // keying this to a room name meant a crew on such a ship could never get outside at all.
        var next = character.Position + moveDelta;
        var outerDoor = Ship.AirlockOuterDoors.FirstOrDefault(d =>
            d.RoomId == character.RoomId && IsDoorOpen(d.Id) && d.Contains(next));
        if (outerDoor is null)
            return false;

        var (hullCenter, _) = GetHullLocalBounds();
        character.IsOutside = true;
        character.EvaAttachedTo = EvaAttachment.Ship;
        // Straight onto the plating beside the door, boots down - no nudge out into open space.
        // Standing back in the door's own rectangle is harmless now that going back inside also
        // requires actually walking *toward* the hull (StepShipAttachedWalk).
        // Snapped from the character's own crossing point (next), not the door's fixed center -
        // the door is a whole unit wide, so anchoring to its center always re-planted the character
        // there regardless of where across the doorway they actually walked through, reading as a
        // small teleport on every exit.
        character.EvaLocalOffset = SnapToHullSurface(next - hullCenter);
        character.EvaVelocity = Vec2.Zero;
        return true;
    }

    // Walking while attached to the ship: normally just slides the local offset tangentially
    // around the hull, clamped to stay in its zone - except walking back into the same open door
    // crosses back inside (you can't step through it mid-drift or from an asteroid; has to
    // physically walk there first, same as anything else magnetized).
    private void StepShipAttachedWalk(Character character, Vec2 moveDelta)
    {
        var (hullCenter, _) = GetHullLocalBounds();
        var localDelta = RotateWorldToLocal(moveDelta, _shipRotationDegrees);
        var candidateOffset = character.EvaLocalOffset + localDelta;
        var absoluteLocalPos = hullCenter + candidateOffset;

        // Standing on an open airlock leads back inside only when actually stepping toward the
        // hull. Boots on the plating put you flush against the door's own rectangle, so "am I
        // inside it" alone would drag you back in the moment you tried to walk away along the hull.
        // Measured as distance to the hull's *outline*, not to its centre: sliding along a face
        // toward the middle of the ship gets closer to the centre while never getting any closer
        // to the plating, and would otherwise read as stepping in.
        var steppingInward = HullSurfaceDistance(candidateOffset) <
                             HullSurfaceDistance(character.EvaLocalOffset) - 0.0001f;
        var outerDoor = steppingInward
            ? Ship.AirlockOuterDoors.FirstOrDefault(d => IsDoorOpen(d.Id) && d.Contains(absoluteLocalPos))
            : null;
        if (outerDoor is null)
        {
            character.EvaLocalOffset = SnapToHullSurface(candidateOffset);
            return;
        }

        // Nudged inward past the door rather than placed exactly on it - the door's own rectangle
        // is a full unit wide, so landing anywhere inside it (even while clearly walking further
        // in) would otherwise still satisfy TryCrossIntoVacuum's check on the very next tick and
        // immediately bounce back outside, mirroring the exit-side bug this same fix pattern
        // addresses in TryCrossIntoVacuum above.
        var towardHull = (hullCenter - outerDoor.Position).Normalized();
        character.IsOutside = false;
        character.RoomId = outerDoor.RoomId; // back into whichever compartment this port belongs to
        character.Position = absoluteLocalPos + towardHull * EvaEntryNudge;
        character.EvaAttachedTo = EvaAttachment.None;
        character.EvaLocalOffset = Vec2.Zero;
    }

    // Magnetized movement is movement *along the plating*: the offset is projected onto the hull's
    // outline (its footprint rectangle, pushed out by the character's own half-width) rather than
    // merely clamped inside a zone around it. Walking into the hull keeps you pinned to the face
    // you're standing on, walking along it slides you, and walking past a corner carries you around
    // onto the next face - which is what boots on a hull should feel like.
    // How far out from the plating a point is: 0 anywhere on or under it, positive out in space.
    // Measured against the union of the compartments (HullSilhouette), not the bounding box - on a
    // hull that isn't a rectangle those are different shapes, and the box includes open sky.
    private float HullSurfaceDistance(Vec2 localOffset)
    {
        var (hullCenter, _) = GetHullLocalBounds();
        return HullSilhouette.DistanceOutside(Ship.Rooms, hullCenter + localOffset);
    }

    private Vec2 SnapToHullSurface(Vec2 localOffset)
    {
        var (hullCenter, _) = GetHullLocalBounds();
        return HullSilhouette.SnapToSurface(Ship.Rooms, hullCenter + localOffset, HullWalkClearance) - hullCenter;
    }

    // Same rule on a rock, against its real jagged outline rather than the circle it used to be
    // approximated by (AsteroidShape): stand on the surface you can see, not on an invisible one.
    private static Vec2 SnapToAsteroidSurface(Asteroid asteroid, Vec2 localOffset) =>
        AsteroidShape.SurfacePoint(asteroid, asteroid.Position + localOffset, HullWalkClearance) - asteroid.Position;

    // moveInputDirection is Vec2.Zero when the player isn't holding a direction this tick - free
    // floating characters still need to be stepped every tick regardless (drifting on momentum),
    // unlike attached movement which is a no-op with no input.
    private void StepEvaCharacter(Character character, Vec2 moveInputDirection, double deltaSeconds)
    {
        if (character.EvaAttachedTo == EvaAttachment.None)
        {
            StepFreeFloating(character, moveInputDirection, deltaSeconds);
            return;
        }

        if (moveInputDirection == Vec2.Zero)
            return;

        var delta = moveInputDirection * EvaWalkSpeed * (float)deltaSeconds;

        if (character.EvaAttachedTo == EvaAttachment.Ship)
        {
            StepShipAttachedWalk(character, delta);
            return;
        }

        // Asteroid: no rotation, so the world-space input direction applies directly.
        var asteroid = AsteroidField.Asteroids.First(a => a.Id == character.EvaAttachedAsteroidId);
        var candidate = SnapToAsteroidSurface(asteroid, character.EvaLocalOffset + delta);

        // A rock lying against the hull used to be a way through the walls: walking round it
        // carried you into the ship's footprint and straight through its plating. The hull is
        // solid from the outside too, whatever you happen to be standing on.
        var (hullCenter, _) = GetHullLocalBounds();
        var inShipFrame = RotateWorldToLocal(asteroid.Position + candidate - _shipFieldPosition, _shipRotationDegrees);
        if (HullSilhouette.Contains(Ship.Rooms, hullCenter + inShipFrame))
            return;

        character.EvaLocalOffset = candidate;
    }

    private void StepFreeFloating(Character character, Vec2 moveInputDirection, double deltaSeconds)
    {
        if (moveInputDirection != Vec2.Zero && character.JetpackFuel > 0)
        {
            character.EvaVelocity += moveInputDirection * JetpackAccelerationPerSecond * (float)deltaSeconds;
            character.JetpackFuel = Math.Max(0, character.JetpackFuel - JetpackFuelPerSecond * (float)deltaSeconds);
        }

        var from = character.EvaLocalOffset;
        var worldPos = (from + character.EvaVelocity * (float)deltaSeconds)
            .Clamp(0, 0, AsteroidField.Width, AsteroidField.Height);
        character.EvaLocalOffset = worldPos;

        // Checked along the whole step, not just where it ended: a jump used to sail clean through
        // a rock whenever the tick happened to straddle it, and the drifter came out the far side
        // untouched. Sampling the segment means the boots catch whatever the flight actually
        // crossed, not whatever it happened to land on.
        TryAutoAttachAlong(character, from, worldPos);
    }

    // Touching the hull or a rock while drifting free re-magnetizes automatically ("зацепиться"
    // needs no deliberate action beyond getting close) - boots that grab on by proximity, not a
    // button you have to press to land.
    private void TryAutoAttachAlong(Character character, Vec2 from, Vec2 to)
    {
        var travelled = (to - from).Length();
        var samples = Math.Max(1, (int)MathF.Ceiling(travelled / 0.25f));
        for (var i = 1; i <= samples; i++)
        {
            if (TryAutoAttach(character, from + (to - from) * (i / (float)samples)))
                return;
        }
    }

    private bool TryAutoAttach(Character character, Vec2 worldPos)
    {
        // Already hull-center-relative, matching EvaLocalOffset's own convention for Ship
        // attachment (see GetEvaWorldPosition) - no further hullCenter offset belongs here, unlike
        // TryCrossIntoVacuum's conversion from an absolute ship-local point (a Door's position).
        var (hullCenter, _) = GetHullLocalBounds();
        var localToShip = RotateWorldToLocal(worldPos - _shipFieldPosition, _shipRotationDegrees);
        if (character.PushedOffFrom != PushOffOrigin.Ship &&
            HullSilhouette.DistanceOutside(Ship.Rooms, hullCenter + localToShip) <= ShipAttachZoneMargin)
        {
            character.EvaAttachedTo = EvaAttachment.Ship;
            // Grabbing on pulls you the last bit onto the plating, rather than leaving you frozen
            // wherever in the capture zone the boots happened to catch.
            character.EvaLocalOffset = SnapToHullSurface(localToShip);
            character.EvaVelocity = Vec2.Zero;
            character.PushedOffFrom = PushOffOrigin.None;
            return true;
        }

        foreach (var asteroid in AsteroidField.Asteroids)
        {
            if (character.PushedOffFrom == PushOffOrigin.Asteroid && character.PushedOffAsteroidId == asteroid.Id)
                continue;
            if (AsteroidShape.DistanceOutside(asteroid, worldPos) > AsteroidAttachZoneMargin)
                continue;

            character.EvaAttachedTo = EvaAttachment.Asteroid;
            character.EvaAttachedAsteroidId = asteroid.Id;
            character.EvaLocalOffset = SnapToAsteroidSurface(asteroid, worldPos - asteroid.Position);
            character.EvaVelocity = Vec2.Zero;
            character.PushedOffFrom = PushOffOrigin.None;
            return true;
        }

        ClearPushOffOriginOnceClear(character, worldPos, hullCenter);
        return false;
    }

    // The thing you just kicked off ignores you until you're properly clear of it, and then only
    // that one thing. A blanket few-seconds-of-immunity instead - which is what this used to be -
    // meant a jump passed straight through every rock and through your own ship for the whole
    // window, which is exactly the "flies through everything" complaint.
    private void ClearPushOffOriginOnceClear(Character character, Vec2 worldPos, Vec2 hullCenter)
    {
        switch (character.PushedOffFrom)
        {
            case PushOffOrigin.Ship:
                var localToShip = RotateWorldToLocal(worldPos - _shipFieldPosition, _shipRotationDegrees);
                if (HullSilhouette.DistanceOutside(Ship.Rooms, hullCenter + localToShip) > ShipAttachZoneMargin + PushOffClearMargin)
                    character.PushedOffFrom = PushOffOrigin.None;
                break;

            case PushOffOrigin.Asteroid:
                var rock = AsteroidField.Asteroids.FirstOrDefault(a => a.Id == character.PushedOffAsteroidId);
                if (rock is null || AsteroidShape.DistanceOutside(rock, worldPos) > AsteroidAttachZoneMargin + PushOffClearMargin)
                {
                    character.PushedOffFrom = PushOffOrigin.None;
                    character.PushedOffAsteroidId = null;
                }
                break;
        }
    }

    private void HandlePushOff(Character character, Vec2 direction)
    {
        if (!character.IsOutside || character.EvaAttachedTo == EvaAttachment.None || direction == Vec2.Zero)
            return;

        var worldPos = GetEvaWorldPosition(character);
        character.PushedOffFrom = character.EvaAttachedTo == EvaAttachment.Ship ? PushOffOrigin.Ship : PushOffOrigin.Asteroid;
        character.PushedOffAsteroidId = character.EvaAttachedAsteroidId;
        character.EvaAttachedTo = EvaAttachment.None;
        character.EvaAttachedAsteroidId = null;
        character.EvaLocalOffset = worldPos;
        character.EvaVelocity = direction.Normalized() * PushOffSpeed;
    }
}
