using System;
using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Stepping outside (game_design.md Phase 3, M17): from the airlock chamber, walking into an open
// vacuum-facing door while wearing a suit crosses into the same AsteroidField world space the ship
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
        EvaAttachment.Asteroid => ActiveObstacles.First(a => a.Id == character.EvaAttachedAsteroidId).Position + character.EvaLocalOffset,
        // The ground never moves or rotates, same as Station below - EvaLocalOffset already IS
        // the absolute PlanetSurface-local position (M55 - see EvaAttachment.cs's own comment).
        EvaAttachment.Planet => character.EvaLocalOffset,
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

    // Where a character physically is, in whichever frame is the authoritative one for them right
    // now. Character.Position and EvaLocalOffset/EvaAttachedTo are never both valid at once -
    // IsOutside is the sole discriminant, and whichever one is losing goes stale rather than being
    // kept in sync (Character.cs's own doc comment on the field) - so every caller that needs
    // "where is this person" without caring which side of the hull they're on asks here instead of
    // re-deriving the same ternary. Callers that are structurally outside-only (e.g. StepCutting's
    // own aim-ray sampling, gated on IsOutside before it ever calls GetEvaWorldPosition) still call
    // that directly: for them the other arm is unreachable, and spelling it would only suggest it isn't.
    public Vec2 GetCharacterWorldPosition(Character character) =>
        character.IsOutside ? GetEvaWorldPosition(character) : character.Position;

    private static Vec2 RotateLocalToWorld(Vec2 local, float rotationDegrees)
    {
        var radians = rotationDegrees * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vec2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
    }

    private static Vec2 RotateWorldToLocal(Vec2 world, float rotationDegrees) => RotateLocalToWorld(world, -rotationDegrees);

    // A structure a magnetized suit can stand on. Every one of them is the same thing geometrically -
    // a union-of-compartments silhouette (HullSilhouette works against any room list), an anchor
    // point its offsets are measured from, and a live orientation in the field - so the walk, the
    // snap, and the grab-on-contact/bounce are one piece of code parameterized by this, not three
    // near-copies of it (Ship/EnemyShip/Station). The station is the degenerate case (never rotates,
    // offsets already absolute in its own frame): LocalCenter is Vec2.Zero and RotationDegrees is 0,
    // exactly what SnapToStationSurface/EnemyHullSurfaceDistance's own station-shaped siblings used
    // to hard-code separately. An asteroid is NOT one of these - different shape family
    // (AsteroidShape, not HullSilhouette) and its own per-rock push-off immunity id, see
    // TryContactAsteroid below.
    private readonly record struct EvaSurface(
        IReadOnlyList<Room> Rooms,
        Vec2 LocalCenter,   // hull-local anchor EvaLocalOffset is measured from
        Vec2 FieldPosition, // where that anchor sits in AsteroidField world space
        float RotationDegrees,
        float AttachZoneMargin,
        EvaAttachment Attachment,
        PushOffOrigin Origin)
    {
        public float DistanceOutside(Vec2 localOffset) =>
            HullSilhouette.DistanceOutside(Rooms, LocalCenter + localOffset);

        public Vec2 Snap(Vec2 localOffset) =>
            HullSilhouette.SnapToSurface(Rooms, LocalCenter + localOffset, HullWalkClearance) - LocalCenter;

        // The zero-rotation arm isn't an optimization: it keeps the station's own arithmetic
        // literally the subtraction it always was (`worldPos - Station.WorldOffset`), so this
        // extraction can't move a station EVA result by even one ulp through a cos/sin round trip.
        public Vec2 ToLocalOffset(Vec2 worldPoint) => RotationDegrees == 0f
            ? worldPoint - FieldPosition
            : RotateWorldToLocal(worldPoint - FieldPosition, RotationDegrees);

        public Vec2 ToLocalDelta(Vec2 worldDelta) => RotationDegrees == 0f
            ? worldDelta
            : RotateWorldToLocal(worldDelta, RotationDegrees);
    }

    private EvaSurface ShipSurface()
    {
        var (hullCenter, _) = GetHullLocalBounds();
        return new EvaSurface(Ship.Rooms, hullCenter, _shipFieldPosition, _shipRotationDegrees,
            ShipAttachZoneMargin, EvaAttachment.Ship, PushOffOrigin.Ship);
    }

    private static EvaSurface EnemySurface(EnemyShipRuntime enemy) =>
        new(enemy.Layout.Rooms, EnemyHullLocalCenter(enemy.Layout), enemy.Position, enemy.RotationDegrees,
            EnemyShipAttachZoneMargin, EvaAttachment.EnemyShip, PushOffOrigin.EnemyShip);

    private EvaSurface StationSurface() =>
        new(Station.Rooms, Vec2.Zero, Station.WorldOffset, 0f,
            StationAttachZoneMargin, EvaAttachment.Station, PushOffOrigin.Station);

    // Empty door/breach lists for a surface with no way in from out here at all - the station's own
    // plating, which is solid all the way round as far as a spacewalker is concerned: getting off it
    // is always a deliberate push-off (HandlePushOff), never a walk-through (StepEvaCharacter's own
    // old station-only comment, now folded into StepMagnetizedWalk's doc comment below).
    private static readonly IReadOnlyList<Door> NoDoors = Array.Empty<Door>();
    private static readonly IReadOnlyList<WallBlock> NoWallBlocks = Array.Empty<WallBlock>();
    private static readonly Func<string, bool> NeverPassableDoor = _ => false;
    private static readonly Func<WallBlock, bool> NeverPassableBlock = _ => false;

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
        // GetDockedLayout mates Ship.VacuumDoors.First() to the station's dock room, and
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
        var connectorId = IsDocked && Ship.VacuumDoors.Count > 0
            ? Ship.VacuumDoors[0].Id
            : null;

        var room = Ship.Rooms.FirstOrDefault(r => r.Id == character.RoomId);
        if (room is null)
            return false;

        // Whichever room the door is actually cut into, rather than one hardcoded chamber id: a
        // hull can carry its ports in any compartment (the Corvette puts one on each beam), and
        // keying this to a room name meant a crew on such a ship could never get outside at all.
        // NEVER Ship.Doors here - only VacuumDoors (humble-soaring-cat.md, "убрать AirlockOuterDoor
        // как отдельный тип"), or a crew member could step into space through an ordinary interior
        // doorway.
        var next = character.Position + moveDelta;
        var outerDoor = Ship.VacuumDoors.FirstOrDefault(d =>
            d.RoomAId == character.RoomId && d.Id != connectorId && IsDoorOpen(d.Id) && d.Contains(next));
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
        // requires actually walking *toward* the hull (StepMagnetizedWalk).
        // Placed at the crossing point's own known wall (ExitPositionAt), not EvaSurface.Snap's
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
        if (_landedBodyId is not null)
        {
            // M55 - landed on a planet: real gravity holds a suited crew member to the ground the
            // instant they cross, boots on or off (there's no ship hull to magnetize to out here,
            // and no zero-g to drift in either) - straight onto EvaAttachment.Planet's own walking
            // model (StepPlanetSurfaceWalk), never TryAutoAttach's "drift until you touch it" one.
            character.EvaAttachedTo = EvaAttachment.Planet;
            character.EvaLocalOffset = _shipFieldPosition + RotateLocalToWorld(exitLocalOffset, _shipRotationDegrees);
        }
        else if (character.MagneticBootsOn)
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
            (MathF.Abs((float)(position.X - room.Left)), new Vec2(-1, 0)),
            (MathF.Abs((float)(position.X - room.Right)), new Vec2(1, 0)),
            (MathF.Abs((float)(position.Y - room.Top)), new Vec2(0, -1)),
            (MathF.Abs((float)(position.Y - room.Bottom)), new Vec2(0, 1)),
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

    // Magnetized movement is movement *along the plating*: the offset is projected onto the hull's
    // outline (its footprint rectangle, pushed out by the character's own half-width) rather than
    // merely clamped inside a zone around it. Walking into the hull keeps you pinned to the face
    // you're standing on, walking along it slides you, and walking past a corner carries you around
    // onto the next face - which is what boots on a hull should feel like - except walking back
    // toward an open door/hatch or a wide-enough breach crosses back inside (you can't step through
    // it mid-drift or from an asteroid; has to physically walk there first, same as anything else
    // magnetized). Replaces StepShipAttachedWalk/StepEnemyShipAttachedWalk (used to be two near-
    // identical copies of this, differing only in which surface/doors/breaches they read) plus the
    // station-only inline branch StepEvaCharacter used to have - the station passes empty door/block
    // lists (NoDoors/NoWallBlocks) so it always falls through to the plain surface.Snap(...) below,
    // same as its own old dedicated SnapToStationSurface call: getting off the station is always a
    // deliberate push-off (HandlePushOff), never a walk-through.
    // outerDoors must ALWAYS be a vacuum-facing subset (Ship.VacuumDoors / enemy.Layout.OuterHatches),
    // NEVER a structure's full Doors list - humble-soaring-cat.md, "убрать AirlockOuterDoor как
    // отдельный тип" - or a crew member could step outside through an ordinary interior doorway.
    private void StepMagnetizedWalk(Character character, Vec2 moveDelta, in EvaSurface surface,
        IReadOnlyList<Door> outerDoors, Func<string, bool> isDoorPassable,
        IReadOnlyList<WallBlock> wallBlocks, Func<WallBlock, bool> isBlockPassable,
        bool crossesOntoEnemyShip)
    {
        var candidateOffset = character.EvaLocalOffset + surface.ToLocalDelta(moveDelta);
        var absoluteLocalPos = surface.LocalCenter + candidateOffset;

        // Standing on an open airlock leads back inside only when actually stepping toward the
        // hull. Boots on the plating put you flush against the door's own rectangle, so "am I
        // inside it" alone would drag you back in the moment you tried to walk away along the hull.
        // Measured as distance to the hull's *outline*, not to its centre: sliding along a face
        // toward the middle of the ship gets closer to the centre while never getting any closer
        // to the plating, and would otherwise read as stepping in.
        var steppingInward = surface.DistanceOutside(candidateOffset) <
                             surface.DistanceOutside(character.EvaLocalOffset) - 0.0001f;
        var outerDoor = steppingInward
            ? outerDoors.FirstOrDefault(d => isDoorPassable(d.Id) && d.Contains(absoluteLocalPos))
            : null;
        // No door underfoot - maybe a wide-enough breach is (same rule TryCrossIntoVacuum uses
        // going the other way): a hole big enough to fit through works exactly like an open
        // airlock for getting back in, too. Interior bulkheads excluded, same reasoning as
        // TryCrossIntoVacuum above - they were never a way out to begin with.
        var breachBlock = steppingInward && outerDoor is null
            ? wallBlocks.FirstOrDefault(b => !b.IsInterior && isBlockPassable(b) && (b.Position - absoluteLocalPos).Length() <= RoomLayout.BreachCrossingRadius)
            : null;
        if (outerDoor is null && breachBlock is null)
        {
            character.EvaLocalOffset = surface.Snap(candidateOffset);
            return;
        }

        // Nudged inward past the door/breach rather than placed exactly on it - the crossing
        // point's own footprint is a full unit wide, so landing anywhere inside it (even while
        // clearly walking further in) would otherwise still satisfy TryCrossIntoVacuum's check on
        // the very next tick and immediately bounce back outside, mirroring the exit-side bug this
        // same fix pattern addresses in TryCrossIntoVacuum above.
        var entryRoomId = outerDoor?.RoomAId ?? breachBlock!.RoomId;
        var entryPosition = outerDoor?.Position ?? breachBlock!.Position;
        var towardHull = (surface.LocalCenter - entryPosition).Normalized();
        character.IsOutside = false;
        if (crossesOntoEnemyShip)
            character.OnEnemyShip = true;
        character.RoomId = entryRoomId; // back into whichever compartment this port/breach belongs to
        character.Position = absoluteLocalPos + towardHull * EvaEntryNudge;
        character.EvaAttachedTo = EvaAttachment.None;
        character.EvaLocalOffset = Vec2.Zero;
    }

    // Same rule on a rock, against its real jagged outline rather than the circle it used to be
    // approximated by (AsteroidShape): stand on the surface you can see, not on an invisible one.
    private static Vec2 SnapToAsteroidSurface(Asteroid asteroid, Vec2 localOffset) =>
        AsteroidShape.SurfacePoint(asteroid, asteroid.Position + localOffset, HullWalkClearance) - asteroid.Position;

    // M55 - walking on a landed planet's own ground (EvaAttachment.Planet): real gravity, not a
    // "grab on contact" magnetize model, so this is a plain clamped translation blocked by whatever
    // rocks (ActiveObstacles) happen to be in the way - no sliding needed, unlike the ship's own
    // hull-box collision, since a person can simply choose to walk around a rock rather than being
    // steered along it. Also the only way back inside from out here: the ship sits on this exact
    // same small surface field while landed, so walking onto its still-open airlock (in the ship's
    // own local frame, since it can still turn on the ground) crosses back in, mirroring
    // StepMagnetizedWalk's own door check but without that method's hull-silhouette "stepping
    // inward" gate - there's no silhouette to be magnetized to out here to begin with.
    private void StepPlanetSurfaceWalk(Character character, Vec2 delta)
    {
        var candidate = (character.EvaLocalOffset + delta).Clamp(0, 0, PlanetSurface.Width, PlanetSurface.Height);
        if (ActiveObstacles.Any(o => AsteroidShape.Contains(o, candidate)))
            return;

        var (hullCenter, _) = GetHullLocalBounds();
        var inShipFrame = RotateWorldToLocal(candidate - _shipFieldPosition, _shipRotationDegrees);
        var absoluteLocalPos = hullCenter + inShipFrame;
        var outerDoor = Ship.VacuumDoors.FirstOrDefault(d => IsDoorOpen(d.Id) && d.Contains(absoluteLocalPos));
        if (outerDoor is not null)
        {
            var towardHull = (hullCenter - outerDoor.Position).Normalized();
            character.IsOutside = false;
            character.RoomId = outerDoor.RoomAId;
            character.Position = absoluteLocalPos + towardHull * EvaEntryNudge;
            character.EvaAttachedTo = EvaAttachment.None;
            character.EvaLocalOffset = Vec2.Zero;
            return;
        }

        character.EvaLocalOffset = candidate;
    }

    // Direct user request ("я хочу сделать чтобы когда игрок находился не в герметичных помещениях
    // и пробоина была достаточно большая то игрок через 3 секунды мгновенно погибал") - a character
    // is in FULL vacuum (not a slow leak) when they're physically outside, OR standing in a room
    // with an opening big enough that the room might as well be open space: an open vacuum-facing
    // door, or a breach wide enough to actually climb through (the same IsPassableBreach a character
    // could already WALK OUT through - World.WallBlocks.cs). A smaller pinhole leak keeps using
    // StepAtmosphere's own separate, slower oxygen-level-based ramp instead (its own gate excludes
    // this case so the two mechanisms never double up on the same character).
    // Real bug found live (door-default regression testing) - the vacuum-door branch was missing
    // the same !IsDocked guard StepAtmosphere's own Diffuse already has for the identical case: an
    // open outer door leads onto the station's pressurized dock chamber while docked, not space
    // (World.StationDocking.cs), so it must not be treated as an opening to vacuum either. Without
    // it, an unsuited character who merely lingers near their own open airlock while safely
    // docked - looting a station crate, say - was dying to "vacuum exposure" that was never real.
    private bool IsFullyExposedToVacuum(Character character) =>
        character.IsOutside ||
        (!IsDocked && Ship.VacuumDoors.Any(d => d.RoomAId == character.RoomId && IsDoorOpen(d.Id))) ||
        Ship.WallBlocks.Any(b => b.RoomId == character.RoomId && !b.IsInterior && IsPassableBreach(b));

    // Direct user request ("при выходе начинается дамаг игрока и через 3 секунды игрок мгновенно
    // умирает") - a linear ramp, not the old flat "counter, then snap Health to 0 on the final tick"
    // model: visible, gradual damage every tick (Character.Health already reaches the client every
    // snapshot), the rate chosen so it reaches exactly 0 at UnsuitedGraceSeconds.
    private const float UnsuitedDamagePerSecond = (float)(Character.MaxHealth / UnsuitedGraceSeconds);

    // moveInputDirection is Vec2.Zero when the player isn't holding a direction this tick - free
    // floating characters still need to be stepped every tick regardless (drifting on momentum),
    // unlike attached movement which is a no-op with no input.
    // Any working suit stops the clock and resets it: the grace is per trip into full vacuum, not a
    // budget spent across a shift. UnsuitedVacuumSeconds itself no longer gates anything (Health
    // reaching 0 is now the death signal on its own) - kept as plain "how long has this been going
    // on" bookkeeping, same meaning its own doc comment on Character.cs already describes.
    private void StepUnsuitedExposure(Character character, double deltaSeconds)
    {
        if (character.SuitSealed || !IsFullyExposedToVacuum(character))
        {
            character.UnsuitedVacuumSeconds = 0;
            return;
        }

        character.UnsuitedVacuumSeconds += deltaSeconds;
        character.Health = Math.Max(0, character.Health - UnsuitedDamagePerSecond * (float)deltaSeconds);
    }

    private void StepEvaCharacter(Character character, Vec2 moveInputDirection, double deltaSeconds)
    {
        // StepUnsuitedExposure now runs once per character per tick from World.Movement.cs's own
        // StepCharacters, unconditionally - not just for an outside character reaching this method,
        // since a large enough breach can put an INDOOR character in full vacuum too (humble-
        // soaring-cat.md direct user request) and this method is never reached for them at all.
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
            StepMagnetizedWalk(character, delta, ShipSurface(),
                Ship.VacuumDoors, IsDoorOpen, Ship.WallBlocks, IsPassableBreach,
                crossesOntoEnemyShip: false);
            return;
        }

        if (character.EvaAttachedTo == EvaAttachment.EnemyShip)
        {
            if (BoardableEnemy is not { } enemy)
                return; // the hull it was attached to is gone - nothing left to walk on
            StepMagnetizedWalk(character, delta, EnemySurface(enemy),
                enemy.Layout.OuterHatches, enemy.IsWallBlockBreached,
                enemy.Layout.WallBlocks, b => enemy.IsWallBlockBreached(b.Id),
                crossesOntoEnemyShip: true);
            return;
        }

        if (character.EvaAttachedTo == EvaAttachment.Station)
        {
            // The station never rotates (EvaSurface's own zero-rotation fast path), and it never
            // has a return-to-somewhere-else crossing to check for, since there is no equivalent of
            // walking back aboard your own ship - getting off the station's hull is always a
            // deliberate push-off (HandlePushOff), never a walk-through. NoDoors/NoWallBlocks make
            // StepMagnetizedWalk fall straight through to its own plain surface.Snap(...) call.
            StepMagnetizedWalk(character, delta, StationSurface(),
                NoDoors, NeverPassableDoor, NoWallBlocks, NeverPassableBlock,
                crossesOntoEnemyShip: false);
            return;
        }

        if (character.EvaAttachedTo == EvaAttachment.Planet)
        {
            StepPlanetSurfaceWalk(character, delta);
            return;
        }

        // Asteroid: no rotation, so the world-space input direction applies directly.
        var asteroid = ActiveObstacles.First(a => a.Id == character.EvaAttachedAsteroidId);
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
            .Clamp(0, 0, ActiveFieldWidth, ActiveFieldHeight);

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
        // Landed (M55) excluded for the same reason - Station.WorldOffset is frozen/stale then too
        // (StepVoyage's own early return skips UpdateNearestStation while landed), and points to an
        // unrelated, system-field-scale coordinate that has nothing to do with this small surface.
        //
        // Only fires on a genuine CROSSING into that state this step (was outside a moment ago, is
        // inside now) - not merely "currently reads as inside", which a docked ship's own airlock
        // exit point can already satisfy on the very first step a fresh EVA character takes (the
        // ship parks right up against the station now that M59 made the map static, so the two
        // structures' local coordinate frames routinely overlap at the berth). Without the "wasn't
        // already inside" half of this check, that ordinary starting position read as an in-flight
        // tunnel every single tick, permanently zeroing EvaVelocity the instant a character pushed
        // off at all near a station - the character never moved again for the rest of the session.
        if (character.EvaAttachedTo == EvaAttachment.None && !IsDocked && _landedBodyId is null &&
            !Station.ContainsPoint(from - Station.WorldOffset) &&
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
        var samples = Math.Max(1, (int)MathF.Ceiling((float)travelled / 0.25f));
        for (var i = 1; i <= samples; i++)
        {
            if (TryAutoAttach(character, from + (to - from) * (i / (float)samples)))
                return;
        }
    }

    // Bounced speed is half of whatever the character was actually flying at, reflected straight
    // back rather than off the surface normal - "отскочил обратно", not a billiard-ball carom.
    private const float BounceSpeedFactor = 0.5f;

    // Three outcomes, not two: null means far away (keep looking at the next surface), false means
    // in contact but immune (just pushed or bounced off this exact thing, see the comment inside),
    // true means grabbed on or bounced. Replaces the Ship/EnemyShip/Station branches TryAutoAttach
    // used to repeat 3 times (same distance-to-silhouette test, same bounce-or-grab body, differing
    // only in which surface/PushOffOrigin/EvaAttachment value). Critical to preserve: once inside a
    // surface's own zone, the caller must stop looking entirely (never fall through to the next
    // surface) even on the immune `false` case - that's exactly what the 3-state result lets the
    // caller do with `is { } hit => return hit`.
    private static bool? TryContactHull(Character character, Vec2 worldPos, in EvaSurface surface)
    {
        var localOffset = surface.ToLocalOffset(worldPos);
        if (surface.DistanceOutside(localOffset) > surface.AttachZoneMargin)
            return null;

        if (!character.MagneticBootsOn)
        {
            // BouncedOffFrom, not PushedOffFrom: this one only has to stop the bounce itself from
            // re-triggering every tick a boots-off character rests against the same surface (which
            // would otherwise flip an outward jetpack burn straight back inward before it ever
            // built up real escape speed) - it must not also block flicking the boots back on and
            // grabbing on right where they're already touching, which is what sharing
            // PushedOffFrom's own immunity would do.
            if (character.BouncedOffFrom == surface.Origin || character.PushedOffFrom == surface.Origin)
                return false;

            // Left exactly at worldPos - the sample point along the travelled step where contact
            // was actually detected - rather than snapped to the boot-clearance surface. Some
            // position update is still needed (this sample can be short of the step's own endpoint,
            // which is what stops a fast jump from tunnelling through), but snapping any closer
            // than the flight itself reached is what grabbing on does; bouncing off must never pull
            // the character in on its own, or it reads as sticking to the wall for an instant
            // before flinging away from it.
            character.EvaLocalOffset = worldPos;
            character.EvaVelocity = character.EvaVelocity * -BounceSpeedFactor;
            character.BouncedOffFrom = surface.Origin;
            return true;
        }

        if (character.PushedOffFrom == surface.Origin)
            return false; // a deliberate push-off still isn't immediately undone once boots are back on

        character.EvaAttachedTo = surface.Attachment;
        // Grabbing on pulls you the last bit onto the plating, rather than leaving you frozen
        // wherever in the capture zone the boots happened to catch.
        character.EvaLocalOffset = surface.Snap(localOffset);
        character.EvaVelocity = Vec2.Zero;
        character.PushedOffFrom = PushOffOrigin.None;
        character.BouncedOffFrom = PushOffOrigin.None;
        return true;
    }

    // Same 3-state shape as TryContactHull above, but against a rock rather than a hull silhouette -
    // deliberately NOT folded into EvaSurface: a different shape family (AsteroidShape, not
    // HullSilhouette), its own per-rock id in the immunity test (BouncedOffAsteroidId/
    // PushedOffAsteroidId, not just an enum value), and it sets EvaAttachedAsteroidId on grab.
    private static bool? TryContactAsteroid(Character character, Vec2 worldPos, Asteroid asteroid)
    {
        if (AsteroidShape.DistanceOutside(asteroid, worldPos) > AsteroidAttachZoneMargin)
            return null;

        if (!character.MagneticBootsOn)
        {
            if ((character.BouncedOffFrom == PushOffOrigin.Asteroid && character.BouncedOffAsteroidId == asteroid.Id) ||
                (character.PushedOffFrom == PushOffOrigin.Asteroid && character.PushedOffAsteroidId == asteroid.Id))
                return null; // immune to THIS rock specifically - the caller's foreach still checks the next one

            // Same reasoning as TryContactHull's own bounce, immunity included: left at worldPos
            // itself, not snapped any closer to the rock's surface than the flight already carried
            // it, and marked as just-bounced so the very next tick doesn't re-bounce the tiny
            // velocity this one just left before it can build into an actual escape.
            character.EvaLocalOffset = worldPos;
            character.EvaVelocity = character.EvaVelocity * -BounceSpeedFactor;
            character.BouncedOffFrom = PushOffOrigin.Asteroid;
            character.BouncedOffAsteroidId = asteroid.Id;
            return true;
        }

        if (character.PushedOffFrom == PushOffOrigin.Asteroid && character.PushedOffAsteroidId == asteroid.Id)
            return null;

        character.EvaAttachedTo = EvaAttachment.Asteroid;
        character.EvaAttachedAsteroidId = asteroid.Id;
        character.EvaLocalOffset = SnapToAsteroidSurface(asteroid, worldPos - asteroid.Position);
        character.EvaVelocity = Vec2.Zero;
        character.PushedOffFrom = PushOffOrigin.None;
        character.BouncedOffFrom = PushOffOrigin.None;
        return true;
    }

    private bool TryAutoAttach(Character character, Vec2 worldPos)
    {
        var ship = ShipSurface();
        if (TryContactHull(character, worldPos, ship) is { } hitShip)
            return hitShip;

        // The currently boardable enemy hull, exactly the same shape of check as the ship's own
        // just above - only meaningful during a battle, and against whichever ship is actually the
        // one you'd board (World.Boarding.cs's BoardableEnemy). Rotates with the hull it belongs to
        // instead of staying fixed, which is the whole reason EnemySurface needs the hull's own
        // Position/RotationDegrees rather than the player's own _shipFieldPosition/_shipRotationDegrees.
        if (IsInBattle && BoardableEnemy is { } enemy &&
            TryContactHull(character, worldPos, EnemySurface(enemy)) is { } hitEnemy)
            return hitEnemy;

        // The station's hull, exactly the same shape of check as the ship's own just above -
        // only meaningful while undocked, same guard as StepFreeFloating's own station check
        // above: Station.Position/WorldOffset only tracks the nearest station while undocked
        // (World.Voyage.cs's UpdateNearestStation), so testing it while docked would attach to
        // (or bounce off) the berth's own frozen coordinates instead.
        // Landed excluded too (M55) - Station.WorldOffset only tracks the nearest SYSTEM-field
        // station while undocked, which is frozen/stale and numerically meaningless against this
        // small local surface field's own coordinates once landed (StepVoyage's own early return
        // skips UpdateNearestStation while landed, the same as it already does while docked).
        if (!IsDocked && _landedBodyId is null &&
            TryContactHull(character, worldPos, StationSurface()) is { } hitStation)
            return hitStation;

        foreach (var asteroid in ActiveObstacles)
            if (TryContactAsteroid(character, worldPos, asteroid) is { } hitRock)
                return hitRock;

        ClearPushOffOriginOnceClear(character, worldPos, ship.LocalCenter);
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
            if (_landedBodyId is not null || HullSilhouette.DistanceOutside(Station.Rooms, localToStation) > StationAttachZoneMargin + PushOffClearMargin)
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
            var rock = ActiveObstacles.FirstOrDefault(a => a.Id == rockId);
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
    // while attached and walking (StepMagnetizedWalk), the same way it always has for the
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
        // Planet excluded (M55) - there's nothing to kick off FROM on open ground, just real
        // gravity holding a suited character to it; "pushing off" flat ground isn't a thing the
        // way it is off a wall, a hull, or a rock.
        if (!character.IsOutside || character.EvaAttachedTo is EvaAttachment.None or EvaAttachment.Planet || direction == Vec2.Zero)
            return;

        var worldPos = GetEvaWorldPosition(character);
        character.PushedOffFrom = character.EvaAttachedTo switch
        {
            EvaAttachment.Ship => PushOffOrigin.Ship,
            EvaAttachment.Station => PushOffOrigin.Station,
            _ => PushOffOrigin.Asteroid,
        };
        // A character standing magnetically attached co-moves with whatever they're attached to -
        // only the ship's own hull actually moves (stations and asteroids are fixed, M59), so only
        // that case needs its current velocity folded into the push-off kick.
        var originVelocity = character.EvaAttachedTo == EvaAttachment.Ship ? _shipVelocity : Vec2.Zero;
        character.PushedOffAsteroidId = character.EvaAttachedAsteroidId;
        character.EvaAttachedTo = EvaAttachment.None;
        character.EvaAttachedAsteroidId = null;
        character.EvaLocalOffset = worldPos;
        character.EvaVelocity = direction.Normalized() * PushOffSpeed + originVelocity;
    }
}
