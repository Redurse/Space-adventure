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
    // Same margin as the ship's own hull - a station's plating is no less solid, and boots that
    // grab a ship's own outer wall on contact should grab any other station's for the same reason.
    private const float StationAttachZoneMargin = 0.5f;
    // Same margin again, for the currently boardable enemy hull - it's just as solid a surface as
    // any of the others, the only difference is that it also moves and turns under you.
    private const float EnemyShipAttachZoneMargin = 0.5f;
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

    // Seconds a character survives in vacuum with no sealed suit. Deliberately generous enough
    // to turn round and step back through the door you came out of, and no more.
    private const double UnsuitedGraceSeconds = 3.0;
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
        // The station never rotates (WorldOffset is a pure translation, unlike the ship's own
        // _shipFieldPosition/_shipRotationDegrees pair), so its own local offset needs no rotation
        // step back out to world space.
        EvaAttachment.Station => Station.WorldOffset + character.EvaLocalOffset,
        // Same shape as Ship above, just against whichever enemy is currently boardable - if it's
        // gone (destroyed, or the fight ended) this degrades to treating the stale offset as an
        // absolute world position rather than throwing, same spirit as every other "structure
        // vanished out from under a character" edge case in this file.
        EvaAttachment.EnemyShip => BoardableEnemy is { } enemy
            ? enemy.Position + RotateLocalToWorld(character.EvaLocalOffset, enemy.RotationDegrees)
            : character.EvaLocalOffset,
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

    // Only reachable from inside the airlock chamber, moving into a currently-open outer door (or,
    // now, a breach wide enough to fit through - World.WallBlocks.cs's IsPassableBreach), while
    // wearing a suit (walking into vacuum unsuited would just be instant death for no gameplay
    // benefit, so it's gated out entirely rather than modeled). Attaches to the ship at the
    // crossing point's own wall so the character doesn't visually jump anywhere on the crossing.
    private bool TryCrossIntoVacuum(Character character, Vec2 moveDelta)
    {
        // Outside means anywhere the ship is actually out in the field, physically: an asteroid
        // field, a battle, approaching or transiting between points of interest (M31-M33's real,
        // physically-simulated flight) all put the ship's own hull right there to walk out onto.
        //
        // Docked at a station used to be blocked outright, on the grounds that the airlock leads
        // onto the station's walkway rather than into vacuum. That is true of exactly one door:
        // GetDockedLayout mates Ship.AirlockOuterDoors.First() to the station's dock room, and
        // that mated pair is one physical rectangle. Every other port on the hull is still a hole
        // in the hull with space on the far side of it - a Corvette carries one per beam - and so
        // is a breach. Blocking the whole phase therefore blocked the wrong thing: not "you cannot
        // get out while docked" but "this particular door goes to the walkway".
        // Going out unsuited is allowed, and survivable for exactly UnsuitedGraceSeconds - long
        // enough to grab something just outside the door and get back in, not long enough to go
        // anywhere. It used to be blocked outright on the grounds that it would be instant death
        // for no gameplay benefit; a few seconds of grace is what turns it from a death into a
        // decision. StepUnsuitedExposure below runs the clock and kills at the end of it.

        // The connector, and only while it actually is one. Null underway, so nothing is excluded
        // there and a single-port ship keeps its only way out.
        var connectorId = IsDocked && Ship.AirlockOuterDoors.Count > 0
            ? Ship.AirlockOuterDoors[0].Id
            : null;

        var room = Ship.Rooms.FirstOrDefault(r => r.Id == character.RoomId);
        if (room is null)
            return false;

        // Whichever room the door is actually cut into, rather than one hardcoded chamber id: a
        // hull can carry its ports in any compartment (the Corvette puts one on each beam), and
        // keying this to a room name meant a crew on such a ship could never get outside at all.
        var next = character.Position + moveDelta;
        var outerDoor = Ship.AirlockOuterDoors.FirstOrDefault(d =>
            d.RoomId == character.RoomId && d.Id != connectorId && IsDoorOpen(d.Id) && d.Contains(next));
        // No open door here - maybe there's a hole instead. A single broken block is still a
        // pinhole (nothing to see through so much as around); only a breach wide enough to
        // actually fit through (two broken blocks side by side) works as a way out. Interior
        // bulkheads are excluded here on purpose - a breach between two pressurized rooms is a
        // walk-through into the next compartment (RoomLayout.MoveAlongAxis, World.Movement.cs),
        // never a step into vacuum, regardless of how wide it's broken open.
        var breachBlock = outerDoor is null
            ? Ship.WallBlocks.FirstOrDefault(b => b.RoomId == character.RoomId && !b.IsInterior && IsPassableBreach(b) &&
                (b.Position - next).Length() <= RoomLayout.BreachCrossingRadius)
            : null;
        if (outerDoor is null && breachBlock is null)
            return false;

        var (hullCenter, _) = GetHullLocalBounds();
        character.IsOutside = true;
        // Straight onto the plating beside the door, boots down - no nudge out into open space.
        // Standing back in the door's own rectangle is harmless now that going back inside also
        // requires actually walking *toward* the hull (StepShipAttachedWalk).
        // Placed at the crossing point's own known wall (ExitPositionAt), not SnapToHullSurface's
        // generic "nearest exterior face" scan: that scan judges distance from wherever the
        // character's crossing point happens to land, and on a chamber that isn't roughly square
        // the nearest face by raw distance isn't always the face actually being crossed -
        // occasionally popping the character out through a different wall than the one they just
        // walked at, which read exactly like a teleport. The door/block's wall is fixed hull
        // layout, never a guess.
        var crossingAt = outerDoor?.Position ?? breachBlock!.Position;
        var exitLocalOffset = ExitPositionAt(room, crossingAt, next, HullWalkClearance) - hullCenter;
        // Boots off means boots off the instant you step through, same as everywhere else
        // (TryAutoAttach) - walking out doesn't grab you onto the hull for free; EvaLocalOffset's
        // meaning flips from a hull-local offset to an absolute world position the moment there's
        // nothing actually holding you to it (Character.cs's own doc comment on the field).
        if (character.MagneticBootsOn)
        {
            character.EvaAttachedTo = EvaAttachment.Ship;
            character.EvaLocalOffset = exitLocalOffset;
        }
        else
        {
            character.EvaAttachedTo = EvaAttachment.None;
            character.EvaLocalOffset = _shipFieldPosition + RotateLocalToWorld(exitLocalOffset, _shipRotationDegrees);
            // Standing right in the attach zone the instant they cross - without this, the very
            // next tick's TryAutoAttach would treat that as fresh contact and bounce them (with
            // zero velocity, so no visible effect, but still re-arming every following tick and
            // cancelling out any jetpack thrust before it can ever build up). BouncedOffFrom, not
            // PushedOffFrom - this must not block flicking the boots straight back on while still
            // standing right there.
            character.BouncedOffFrom = PushOffOrigin.Ship;
        }
        character.EvaVelocity = Vec2.Zero;
        return true;
    }

    // Which of the room's own four walls a point at its boundary actually sits on - fixed by the
    // hull's layout (compares the point to the room's bounds), never by wherever the character
    // crossing it happens to be standing. Shared by an airlock door and a wide-enough breach alike
    // (World.WallBlocks.cs) - both are just "a known point on the hull", geometrically.
    private static Vec2 OutwardDirectionAt(Room room, Vec2 position)
    {
        var faces = new (float Distance, Vec2 Direction)[]
        {
            (MathF.Abs(position.X - room.Left), new Vec2(-1, 0)),
            (MathF.Abs(position.X - room.Right), new Vec2(1, 0)),
            (MathF.Abs(position.Y - room.Top), new Vec2(0, -1)),
            (MathF.Abs(position.Y - room.Bottom), new Vec2(0, 1)),
        };
        var closest = faces[0];
        foreach (var face in faces)
            if (face.Distance < closest.Distance)
                closest = face;
        return closest.Direction;
    }

    // Where a character crossing at this known hull point ends up: pushed straight out past that
    // point's own wall by `clearance`, keeping whichever coordinate runs *along* the wall exactly
    // as they walked it - so the only thing that changes is "inside the plating" becoming "just
    // outside it", not where along the doorway/breach they crossed.
    private static Vec2 ExitPositionAt(Room room, Vec2 position, Vec2 crossingPoint, float clearance)
    {
        var direction = OutwardDirectionAt(room, position);
        return direction.X != 0
            ? new Vec2(direction.X > 0 ? room.Right + clearance : room.Left - clearance, crossingPoint.Y)
            : new Vec2(crossingPoint.X, direction.Y > 0 ? room.Bottom + clearance : room.Top - clearance);
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
        // No door underfoot - maybe a wide-enough breach is (same rule TryCrossIntoVacuum uses
        // going the other way): a hole big enough to fit through works exactly like an open
        // airlock for getting back in, too. Interior bulkheads excluded, same reasoning as
        // TryCrossIntoVacuum above - they were never a way out to begin with.
        var breachBlock = steppingInward && outerDoor is null
            ? Ship.WallBlocks.FirstOrDefault(b => !b.IsInterior && IsPassableBreach(b) && (b.Position - absoluteLocalPos).Length() <= RoomLayout.BreachCrossingRadius)
            : null;
        if (outerDoor is null && breachBlock is null)
        {
            character.EvaLocalOffset = SnapToHullSurface(candidateOffset);
            return;
        }

        // Nudged inward past the door/breach rather than placed exactly on it - the crossing
        // point's own footprint is a full unit wide, so landing anywhere inside it (even while
        // clearly walking further in) would otherwise still satisfy TryCrossIntoVacuum's check on
        // the very next tick and immediately bounce back outside, mirroring the exit-side bug this
        // same fix pattern addresses in TryCrossIntoVacuum above.
        var entryRoomId = outerDoor?.RoomId ?? breachBlock!.RoomId;
        var entryPosition = outerDoor?.Position ?? breachBlock!.Position;
        var towardHull = (hullCenter - entryPosition).Normalized();
        character.IsOutside = false;
        character.RoomId = entryRoomId; // back into whichever compartment this port/breach belongs to
        character.Position = absoluteLocalPos + towardHull * EvaEntryNudge;
        character.EvaAttachedTo = EvaAttachment.None;
        character.EvaLocalOffset = Vec2.Zero;
    }

    // Same as StepShipAttachedWalk above, just against the currently boardable enemy hull: sliding
    // along its plating, and crossing inside the moment you step toward a hatch or wall panel that's
    // actually been cut open (EnemyShipRuntime's own per-hull Hp) rather than merely open - these
    // hatches are locked, there's no handle to open one from outside, only a torch.
    private void StepEnemyShipAttachedWalk(Character character, Vec2 moveDelta)
    {
        if (BoardableEnemy is not { } enemy)
            return; // the hull it was attached to is gone - nothing left to walk on

        var localCenter = EnemyHullLocalCenter(enemy.Layout);
        var localDelta = RotateWorldToLocal(moveDelta, enemy.RotationDegrees);
        var candidateOffset = character.EvaLocalOffset + localDelta;
        var absoluteLocalPos = localCenter + candidateOffset;

        var steppingInward = EnemyHullSurfaceDistance(candidateOffset, enemy) <
                             EnemyHullSurfaceDistance(character.EvaLocalOffset, enemy) - 0.0001f;
        var outerDoor = steppingInward
            ? enemy.Layout.AirlockOuterDoors.FirstOrDefault(d => enemy.IsAirlockBreached(d.Id) && d.Contains(absoluteLocalPos))
            : null;
        var breachBlock = steppingInward && outerDoor is null
            ? enemy.Layout.WallBlocks.FirstOrDefault(b => !b.IsInterior && enemy.IsWallBlockBreached(b.Id) &&
                (b.Position - absoluteLocalPos).Length() <= RoomLayout.BreachCrossingRadius)
            : null;
        if (outerDoor is null && breachBlock is null)
        {
            character.EvaLocalOffset = SnapToEnemyHullSurface(candidateOffset, enemy);
            return;
        }

        var entryRoomId = outerDoor?.RoomId ?? breachBlock!.RoomId;
        var entryPosition = outerDoor?.Position ?? breachBlock!.Position;
        var towardHull = (localCenter - entryPosition).Normalized();
        character.IsOutside = false;
        character.OnEnemyShip = true;
        character.RoomId = entryRoomId;
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

    // Same rule again on the station's own hull - HullSilhouette works against any room list, not
    // just the ship's, so this is the exact same call SnapToHullSurface makes, just against
    // Station.Rooms and with no hullCenter/rotation step either side of it (see GetEvaWorldPosition).
    private Vec2 SnapToStationSurface(Vec2 localOffset) =>
        HullSilhouette.SnapToSurface(Station.Rooms, localOffset, HullWalkClearance);

    // Same rule again on the currently boardable enemy hull - hull-centre-relative, matching
    // EvaLocalOffset's own convention for EnemyShip attachment (see GetEvaWorldPosition).
    private static float EnemyHullSurfaceDistance(Vec2 localOffset, EnemyShipRuntime enemy) =>
        HullSilhouette.DistanceOutside(enemy.Layout.Rooms, EnemyHullLocalCenter(enemy.Layout) + localOffset);

    private static Vec2 SnapToEnemyHullSurface(Vec2 localOffset, EnemyShipRuntime enemy)
    {
        var center = EnemyHullLocalCenter(enemy.Layout);
        return HullSilhouette.SnapToSurface(enemy.Layout.Rooms, center + localOffset, HullWalkClearance) - center;
    }

    // moveInputDirection is Vec2.Zero when the player isn't holding a direction this tick - free
    // floating characters still need to be stepped every tick regardless (drifting on momentum),
    // unlike attached movement which is a no-op with no input.
    // Vacuum exposure with no sealed suit. Counted rather than applied as damage per second so
    // the limit is a time the player can actually learn - "three seconds" - instead of a rate
    // they have to infer from a health bar. Any working suit stops the clock and resets it: the
    // grace is per trip outside, not a budget spent across a shift.
    private void StepUnsuitedExposure(Character character, double deltaSeconds)
    {
        if (!character.IsOutside || character.SuitSealed)
        {
            character.UnsuitedVacuumSeconds = 0;
            return;
        }

        character.UnsuitedVacuumSeconds += deltaSeconds;
        if (character.UnsuitedVacuumSeconds >= UnsuitedGraceSeconds)
            character.Health = 0;
    }

    private void StepEvaCharacter(Character character, Vec2 moveInputDirection, double deltaSeconds)
    {
        StepUnsuitedExposure(character, deltaSeconds);
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

        if (character.EvaAttachedTo == EvaAttachment.EnemyShip)
        {
            StepEnemyShipAttachedWalk(character, delta);
            return;
        }

        if (character.EvaAttachedTo == EvaAttachment.Station)
        {
            // The station never rotates, so unlike StepShipAttachedWalk this needs no local/world
            // conversion either side of the snap - and it never has a return-to-somewhere-else
            // crossing to check for, since there is no equivalent of walking back aboard your own
            // ship: getting off the station's hull is always a deliberate push-off (HandlePushOff).
            character.EvaLocalOffset = SnapToStationSurface(character.EvaLocalOffset + delta);
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
        // The thrusters are part of the suit, so without one there is nothing to fire: an unsuited
        // character who pushes off is a body with momentum and no way to change it. That is the
        // whole risk of stepping out unsuited - not the timer on its own, but the timer plus not
        // being able to correct a bad push.
        if (moveInputDirection != Vec2.Zero && character.JetpackFuel > 0 && character.SuitSealed)
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
        // crossed, not whatever it happened to land on - this is what makes the station's own
        // plating (TryAutoAttach's own Station branch) grab magnetic boots on contact and bounce
        // a boots-off drifter straight back off it, the same as the ship's hull already does.
        TryAutoAttachAlong(character, from, worldPos);

        // Defensive only: the sampling above should always catch the crossing first (it's fine
        // enough - 0.25 units a sample - that a single tick blowing straight through the whole
        // attach margin between two samples shouldn't happen at any speed this game reaches), but
        // if it somehow still does, this is what stops a drifter dead inside the station's own
        // rooms instead of leaving it lodged there. Same undocked guard as the station branch
        // below: Station.Position/WorldOffset only tracks the nearest station while undocked
        // (World.Voyage.cs's UpdateNearestStation) - once docked it's frozen at the berth instead.
        if (character.EvaAttachedTo == EvaAttachment.None && !IsDocked &&
            Station.ContainsPoint(character.EvaLocalOffset - Station.WorldOffset))
        {
            character.EvaLocalOffset = from;
            character.EvaVelocity = Vec2.Zero;
        }
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

    // Bounced speed is half of whatever the character was actually flying at, reflected straight
    // back rather than off the surface normal - "отскочил обратно", not a billiard-ball carom.
    private const float BounceSpeedFactor = 0.5f;

    private bool TryAutoAttach(Character character, Vec2 worldPos)
    {
        // Already hull-center-relative, matching EvaLocalOffset's own convention for Ship
        // attachment (see GetEvaWorldPosition) - no further hullCenter offset belongs here, unlike
        // TryCrossIntoVacuum's conversion from an absolute ship-local point (a Door's position).
        var (hullCenter, _) = GetHullLocalBounds();
        var localToShip = RotateWorldToLocal(worldPos - _shipFieldPosition, _shipRotationDegrees);
        if (HullSilhouette.DistanceOutside(Ship.Rooms, hullCenter + localToShip) <= ShipAttachZoneMargin)
        {
            if (!character.MagneticBootsOn)
            {
                // BouncedOffFrom, not PushedOffFrom: this one only has to stop the bounce itself
                // from re-triggering every tick a boots-off character rests against the same
                // surface (which would otherwise flip an outward jetpack burn straight back
                // inward before it ever built up real escape speed) - it must not also block
                // flicking the boots back on and grabbing on right where they're already
                // touching, which is what sharing PushedOffFrom's own immunity would do.
                if (character.BouncedOffFrom == PushOffOrigin.Ship || character.PushedOffFrom == PushOffOrigin.Ship)
                    return false;

                // Left exactly at worldPos - the sample point along the travelled step where
                // contact was actually detected - rather than snapped to the boot-clearance
                // surface. Some position update is still needed (this sample can be short of the
                // step's own endpoint, which is what stops a fast jump from tunnelling through),
                // but snapping any closer than the flight itself reached is what grabbing on does;
                // bouncing off must never pull the character in on its own, or it reads as
                // sticking to the wall for an instant before flinging away from it.
                character.EvaLocalOffset = worldPos;
                character.EvaVelocity = character.EvaVelocity * -BounceSpeedFactor;
                character.BouncedOffFrom = PushOffOrigin.Ship;
                return true;
            }

            if (character.PushedOffFrom == PushOffOrigin.Ship)
                return false; // a deliberate push-off still isn't immediately undone once boots are back on

            character.EvaAttachedTo = EvaAttachment.Ship;
            // Grabbing on pulls you the last bit onto the plating, rather than leaving you frozen
            // wherever in the capture zone the boots happened to catch.
            character.EvaLocalOffset = SnapToHullSurface(localToShip);
            character.EvaVelocity = Vec2.Zero;
            character.PushedOffFrom = PushOffOrigin.None;
            character.BouncedOffFrom = PushOffOrigin.None;
            return true;
        }

        // The currently boardable enemy hull, exactly the same shape of check as the ship's own
        // just above - only meaningful during a battle, and against whichever ship is actually the
        // one you'd board (World.Boarding.cs's BoardableEnemy). Rotates with the hull it belongs to
        // instead of staying fixed, which is the whole reason this needs the hull's own
        // Position/RotationDegrees rather than the player's own _shipFieldPosition/_shipRotationDegrees.
        if (IsInBattle && BoardableEnemy is { } enemy)
        {
            var enemyLocalCenter = EnemyHullLocalCenter(enemy.Layout);
            var localToEnemy = RotateWorldToLocal(worldPos - enemy.Position, enemy.RotationDegrees);
            if (HullSilhouette.DistanceOutside(enemy.Layout.Rooms, enemyLocalCenter + localToEnemy) <= EnemyShipAttachZoneMargin)
            {
                if (!character.MagneticBootsOn)
                {
                    if (character.BouncedOffFrom == PushOffOrigin.EnemyShip || character.PushedOffFrom == PushOffOrigin.EnemyShip)
                        return false;

                    character.EvaLocalOffset = worldPos;
                    character.EvaVelocity = character.EvaVelocity * -BounceSpeedFactor;
                    character.BouncedOffFrom = PushOffOrigin.EnemyShip;
                    return true;
                }

                if (character.PushedOffFrom == PushOffOrigin.EnemyShip)
                    return false;

                character.EvaAttachedTo = EvaAttachment.EnemyShip;
                character.EvaLocalOffset = SnapToEnemyHullSurface(localToEnemy, enemy);
                character.EvaVelocity = Vec2.Zero;
                character.PushedOffFrom = PushOffOrigin.None;
                character.BouncedOffFrom = PushOffOrigin.None;
                return true;
            }
        }

        // The station's hull, exactly the same shape of check as the ship's own just above -
        // only meaningful while undocked, same guard as StepFreeFloating's own station check
        // above: Station.Position/WorldOffset only tracks the nearest station while undocked
        // (World.Voyage.cs's UpdateNearestStation), so testing it while docked would attach to
        // (or bounce off) the berth's own frozen coordinates instead.
        if (!IsDocked)
        {
            var localToStation = worldPos - Station.WorldOffset;
            if (HullSilhouette.DistanceOutside(Station.Rooms, localToStation) <= StationAttachZoneMargin)
            {
                if (!character.MagneticBootsOn)
                {
                    if (character.BouncedOffFrom == PushOffOrigin.Station || character.PushedOffFrom == PushOffOrigin.Station)
                        return false;

                    character.EvaLocalOffset = worldPos;
                    character.EvaVelocity = character.EvaVelocity * -BounceSpeedFactor;
                    character.BouncedOffFrom = PushOffOrigin.Station;
                    return true;
                }

                if (character.PushedOffFrom == PushOffOrigin.Station)
                    return false;

                character.EvaAttachedTo = EvaAttachment.Station;
                character.EvaLocalOffset = SnapToStationSurface(localToStation);
                character.EvaVelocity = Vec2.Zero;
                character.PushedOffFrom = PushOffOrigin.None;
                character.BouncedOffFrom = PushOffOrigin.None;
                return true;
            }
        }

        foreach (var asteroid in AsteroidField.Asteroids)
        {
            if (AsteroidShape.DistanceOutside(asteroid, worldPos) > AsteroidAttachZoneMargin)
                continue;

            if (!character.MagneticBootsOn)
            {
                if ((character.BouncedOffFrom == PushOffOrigin.Asteroid && character.BouncedOffAsteroidId == asteroid.Id) ||
                    (character.PushedOffFrom == PushOffOrigin.Asteroid && character.PushedOffAsteroidId == asteroid.Id))
                    continue;

                // Same reasoning as the ship branch above, immunity included: left at worldPos
                // itself, not snapped any closer to the rock's surface than the flight already
                // carried it, and marked as just-bounced so the very next tick doesn't re-bounce
                // the tiny velocity this one just left before it can build into an actual escape.
                character.EvaLocalOffset = worldPos;
                character.EvaVelocity = character.EvaVelocity * -BounceSpeedFactor;
                character.BouncedOffFrom = PushOffOrigin.Asteroid;
                character.BouncedOffAsteroidId = asteroid.Id;
                return true;
            }

            if (character.PushedOffFrom == PushOffOrigin.Asteroid && character.PushedOffAsteroidId == asteroid.Id)
                continue;

            character.EvaAttachedTo = EvaAttachment.Asteroid;
            character.EvaAttachedAsteroidId = asteroid.Id;
            character.EvaLocalOffset = SnapToAsteroidSurface(asteroid, worldPos - asteroid.Position);
            character.EvaVelocity = Vec2.Zero;
            character.PushedOffFrom = PushOffOrigin.None;
            character.BouncedOffFrom = PushOffOrigin.None;
            return true;
        }

        ClearPushOffOriginOnceClear(character, worldPos, hullCenter);
        return false;
    }

    // The thing you just kicked off (or bounced off) ignores you until you're properly clear of
    // it, and then only that one thing. A blanket few-seconds-of-immunity instead - which is what
    // this used to be - meant a jump passed straight through every rock and through your own ship
    // for the whole window, which is exactly the "flies through everything" complaint. Clears
    // PushedOffFrom and BouncedOffFrom independently (either, both, or neither can be set at once)
    // against the exact same distance test, since "far enough clear of the thing" means the same
    // distance regardless of which of the two reasons put it there.
    private void ClearPushOffOriginOnceClear(Character character, Vec2 worldPos, Vec2 hullCenter)
    {
        if (character.PushedOffFrom == PushOffOrigin.Ship || character.BouncedOffFrom == PushOffOrigin.Ship)
        {
            var localToShip = RotateWorldToLocal(worldPos - _shipFieldPosition, _shipRotationDegrees);
            if (HullSilhouette.DistanceOutside(Ship.Rooms, hullCenter + localToShip) > ShipAttachZoneMargin + PushOffClearMargin)
            {
                if (character.PushedOffFrom == PushOffOrigin.Ship)
                    character.PushedOffFrom = PushOffOrigin.None;
                if (character.BouncedOffFrom == PushOffOrigin.Ship)
                    character.BouncedOffFrom = PushOffOrigin.None;
            }
        }

        if (character.PushedOffFrom == PushOffOrigin.Station || character.BouncedOffFrom == PushOffOrigin.Station)
        {
            var localToStation = worldPos - Station.WorldOffset;
            if (HullSilhouette.DistanceOutside(Station.Rooms, localToStation) > StationAttachZoneMargin + PushOffClearMargin)
            {
                if (character.PushedOffFrom == PushOffOrigin.Station)
                    character.PushedOffFrom = PushOffOrigin.None;
                if (character.BouncedOffFrom == PushOffOrigin.Station)
                    character.BouncedOffFrom = PushOffOrigin.None;
            }
        }

        if (character.PushedOffFrom == PushOffOrigin.Asteroid || character.BouncedOffFrom == PushOffOrigin.Asteroid)
        {
            var rockId = character.PushedOffFrom == PushOffOrigin.Asteroid ? character.PushedOffAsteroidId : character.BouncedOffAsteroidId;
            var rock = AsteroidField.Asteroids.FirstOrDefault(a => a.Id == rockId);
            if (rock is null || AsteroidShape.DistanceOutside(rock, worldPos) > AsteroidAttachZoneMargin + PushOffClearMargin)
            {
                if (character.PushedOffFrom == PushOffOrigin.Asteroid)
                {
                    character.PushedOffFrom = PushOffOrigin.None;
                    character.PushedOffAsteroidId = null;
                }
                if (character.BouncedOffFrom == PushOffOrigin.Asteroid)
                {
                    character.BouncedOffFrom = PushOffOrigin.None;
                    character.BouncedOffAsteroidId = null;
                }
            }
        }
    }

    // Test-only teleport, same convention as World.ShipField.cs's DebugPlaceShip - drops an already-
    // suited character free-floating at an exact world position with zero velocity, instead of
    // making a test spend jetpack fuel (JetpackFuelPerSecond=10, only 10 seconds of thrust total)
    // and dozens of simulated seconds flying there for real. A test that's actually about EVA flight
    // itself still flies for real and never calls this.
    // attachToEnemyShip lets a test drop the character already magnetized to the currently
    // boardable hull (World.Eva.cs's own attach model, EvaLocalOffset relative to that hull's own
    // moving/turning frame) instead of free-floating nearby - boarding now only ever crosses in
    // while attached and walking (StepEnemyShipAttachedWalk), the same way it always has for the
    // player's own ship.
    public void DebugPlaceEvaCharacter(int playerId, Vec2 worldPosition, bool attachToEnemyShip = false)
    {
        var character = _characters[playerId];
        character.IsOutside = true;
        character.EvaAttachedAsteroidId = null;
        character.EvaVelocity = Vec2.Zero;
        if (attachToEnemyShip && BoardableEnemy is { } enemy)
        {
            character.EvaAttachedTo = EvaAttachment.EnemyShip;
            character.EvaLocalOffset = RotateWorldToLocal(worldPosition - enemy.Position, enemy.RotationDegrees);
        }
        else
        {
            character.EvaAttachedTo = EvaAttachment.None;
            character.EvaLocalOffset = worldPosition;
        }
    }

    private void HandlePushOff(Character character, Vec2 direction)
    {
        if (!character.IsOutside || character.EvaAttachedTo == EvaAttachment.None || direction == Vec2.Zero)
            return;

        var worldPos = GetEvaWorldPosition(character);
        character.PushedOffFrom = character.EvaAttachedTo switch
        {
            EvaAttachment.Ship => PushOffOrigin.Ship,
            EvaAttachment.Station => PushOffOrigin.Station,
            _ => PushOffOrigin.Asteroid,
        };
        character.PushedOffAsteroidId = character.EvaAttachedAsteroidId;
        character.EvaAttachedTo = EvaAttachment.None;
        character.EvaAttachedAsteroidId = null;
        character.EvaLocalOffset = worldPos;
        character.EvaVelocity = direction.Normalized() * PushOffSpeed;
    }
}
