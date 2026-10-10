using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// M63 - "структурное отделение" (see C:\Users\Andrey\.claude\plans\humble-soaring-cat.md's own M63
// design): a room whose own exterior wall is fully breached is destroyed outright, and whatever
// that leaves unreachable from the reactor - not just the destroyed room itself - splits off as one
// free-flying debris fragment. No new physics: a fragment is pure inertia (position += velocity*dt),
// the exact same integrator the ship/asteroids/EVA already use (this project has had no gravity
// since M59). Nothing inside a fragment is simulated (no walking around on it) - the same
// simplified treatment already given to every other non-interactive field object.
//
// M64 - consequences of a detachment, on top of the structure itself: anyone actually standing in a
// detaching room is ejected into free EVA at the split point (DestroyRoomAndDetach's own doc comment
// below) rather than left orphaned for ApplyShipDefinition's generic "no such room any more, fall
// back to spawn" cleanup to silently teleport them to safety inside the remaining ship - that
// fallback is right for M61's voluntary, docked demolition but wrong for a combat kill. Any item
// dropped in a detaching room is removed outright ("flies off with the debris" - the plan's own
// wording for why this is simpler than scattering it into vacuum as a separate free-floating pickup,
// and consistent with a fragment's interior never being walked on in the first place). The plan's
// other two M64 items need no new code here: a detached reactor/distribution block already can't
// happen (DestroyRoomAndDetach's own reactor-room guard), and power loss in the remaining ship from
// losing OTHER devices already falls out of World.Wiring.cs's existing IsPinPowered reachability
// check for free, same as it already does for a cut wire.
public sealed partial class World
{
    private sealed class ShipDebrisFragment
    {
        public required string Id;
        public Vec2 Position;
        public required Vec2 Velocity;
        public required float RotationDegrees;
        // Degrees per second - a piece torn off by an explosion tumbles instead of gliding along rigidly.
        public float AngularVelocity;
        // Hit points: a torn-off piece can be shot to pieces (TryHitDebris).
        public float Hp;
        // Stored relative to the FRAGMENT's own pivot (its footprint's centre at the moment of
        // detachment), not the old ship's hull centre - so Position alone is enough to place every
        // room correctly from here on, the same "one transform, not two" shape Ship.Rooms itself
        // already has relative to _shipFieldPosition.
        public required IReadOnlyList<Room> Rooms;
    }

    private readonly List<ShipDebrisFragment> _shipDebris = new();
    private int _nextDebrisId;

    // (Which compartment dies, and when, is World.RoomHp.cs; what an explosion does is World.ShipBlasts.cs.
    // A compartment is no longer lost the moment ALL its walls are breached - a third of them is enough.)

    // The structural computation itself lives in Shared (ShipDetachment) so hostile ships can use it too.
    private bool TryComputeRoomDetachment(string roomId, out CustomShipDefinition shrunk, out IReadOnlyList<CustomRoomDef> detachedRooms) =>
        ShipDetachment.TryCompute(Ship, roomId, out shrunk, out detachedRooms);

    // M64 - everyone actually aboard a room that's about to detach becomes a free EVA body at their
    // own exact position, the moment before the room stops existing - same state-reset shape
    // World.Boarding.cs's own EjectFromEnemyShip uses for "the structure you were standing in is
    // gone", just computing the ejection point from THIS ship's own hull transform instead of
    // BoardableEnemy's. Doing this FIRST (before ApplyShipDefinition runs) matters: it flips
    // IsOutside to true, which is exactly the flag ApplyShipDefinition's own generic orphaned-
    // character fallback checks to decide whether to step in - once it's already true, that
    // fallback correctly leaves the ejection alone instead of overwriting it.
    private void EjectCrewFromDetachingRooms(HashSet<string> detachedRoomIds)
    {
        var (hullCenter, _) = GetHullLocalBounds();
        foreach (var character in _characters.Values.Where(c => !c.OnStation && !c.IsOutside && detachedRoomIds.Contains(c.RoomId)).ToList())
        {
            character.IsOutside = true;
            character.EvaAttachedTo = EvaAttachment.None;
            character.EvaAttachedAsteroidId = null;
            // EvaLocalOffset's meaning flips from a hull-local offset to an absolute world position
            // the instant nothing is actually holding the character to a structure any more
            // (Character.cs's own doc comment on the field) - exactly this instant.
            character.EvaLocalOffset = _shipFieldPosition + RotateLocalToWorld(character.Position - hullCenter, _shipRotationDegrees);
            // Keeps moving with the ship's own last velocity rather than snapping to absolute rest -
            // the same "pure pursuit isn't enough, hand over the origin's own live velocity too"
            // fix World.Eva.cs's HandlePushOff already needed this session for the ordinary push-off
            // case; a structural kill deserves the same courtesy; the debris fragment itself is
            // launched with the identical velocity (SpawnDebrisFragment), so ejected crew and the
            // wreck they were just standing in drift apart smoothly rather than one snapping still.
            character.EvaVelocity = _shipVelocity;
            character.PushedOffFrom = PushOffOrigin.None;
            character.BouncedOffFrom = PushOffOrigin.None;
            character.ManningTurretId = null;
            character.IsAtHelm = false;
            character.RoomId = Ship.SpawnRoomId; // meaningless while outside, valid for the trip home -
                                                  // same convention EjectFromEnemyShip already uses
        }
    }

    // How hard an explosion shoves a piece it tears off, away from the blast (field units per second), and how
    // fast it ends up turning.
    private const float DebrisHpPerTile = 6f; // a 4x4 room: 96 hp, about thirty cannon hits
    private const float BlastKickSpeed = 2.2f;
    private const float MinBlastSpinDegreesPerSecond = 8f;
    private const float MaxBlastSpinDegreesPerSecond = 26f;

    private void SpawnDebrisFragment(IReadOnlyList<CustomRoomDef> detachedRooms, Vec2 blastCenter)
    {
        var (ownHullCenter, _) = GetHullLocalBounds();
        SpawnDebrisFragment(detachedRooms, blastCenter, ownHullCenter, _shipFieldPosition, _shipRotationDegrees, _shipVelocity);
    }

    // The same, for a piece torn off ANY ship: its hull centre in the ship-local frame, and where that ship is in the field.
    private void SpawnDebrisFragment(IReadOnlyList<CustomRoomDef> detachedRooms, Vec2 blastCenter, Vec2 hullCenter, Vec2 fieldPosition,
        float rotationDegrees, Vec2 shipVelocity)
    {
        // The detached group's own footprint centre becomes its new pivot - simple bounding-box
        // centre rather than an area-weighted one, plenty accurate for a first cut (nothing about
        // gameplay depends on the pivot being the exact centre of mass).
        var minX = detachedRooms.Min(r => r.X);
        var maxX = detachedRooms.Max(r => r.X + r.Width);
        var minY = detachedRooms.Min(r => r.Y);
        var maxY = detachedRooms.Max(r => r.Y + r.Height);
        var pivot = new Vec2((minX + maxX) / 2.0, (minY + maxY) / 2.0);

        var worldPosition = fieldPosition + RotateLocalToWorld(pivot - hullCenter, rotationDegrees);

        _shipDebris.Add(new ShipDebrisFragment
        {
            Id = $"debris-{_nextDebrisId++}",
            Position = worldPosition,
            // Inherits the ship's velocity at the moment of separation, plus a shove away from the blast and a
            // spin - the way a piece of a ship torn off by an explosion behaves in Cosmoteer.
            Velocity = shipVelocity + KickAwayFrom(blastCenter, pivot, rotationDegrees),
            RotationDegrees = rotationDegrees,
            AngularVelocity = SpinFrom(blastCenter, pivot),
            Hp = detachedRooms.Sum(r => r.Width * r.Height) * DebrisHpPerTile,
            Rooms = detachedRooms.Select(r => new Room(r.Id, r.Name, (float)(r.X - pivot.X), (float)(r.Y - pivot.Y), r.Width, r.Height)).ToArray(),
        });
    }

    // Pure inertia, ticked from World.Step() - the exact same integrator every other field object
    // already uses (no gravity since M59, so this is genuinely the whole physics model).
    private void StepShipDebris(double deltaSeconds)
    {
        foreach (var fragment in _shipDebris)
        {
            fragment.Position += fragment.Velocity * deltaSeconds;
            fragment.RotationDegrees += fragment.AngularVelocity * (float)deltaSeconds;
        }
    }

    // Direct user request ("Ð¾ÑÐ¾ÑÐ²Ð°Ð½Ð½ÑÐµ ÑÐ°ÑÑÐ¸ ... ÐºÐ°Ðº Ð² Cosmoteer"): a shot from the player's guns that reaches a
    // torn-off piece hurts it, and a piece that runs out of hit points blows up. The segment is carried into the
    // fragment's own frame (its rooms are stored relative to its pivot) and sampled for a point inside any room.
    private bool TryHitDebris(Vec2 from, Vec2 to, float damage)
    {
        foreach (var fragment in _shipDebris.ToList())
        {
            var a = RotateWorldToLocal(from - fragment.Position, fragment.RotationDegrees);
            var b = RotateWorldToLocal(to - fragment.Position, fragment.RotationDegrees);
            var length = (b - a).Length();
            var samples = Math.Max(1, (int)Math.Ceiling(length / 0.2));
            for (var i = 0; i <= samples; i++)
            {
                var p = a + (b - a) * (i / (double)samples);
                if (fragment.Rooms.Any(r => r.Contains(p)))
                {
                    DamageDebris(fragment, damage);
                    return true;
                }
            }
        }
        return false;
    }

    private void DamageDebris(ShipDebrisFragment fragment, float damage)
    {
        fragment.Hp -= damage;
        if (fragment.Hp > 0f)
            return;
        var area = fragment.Rooms.Sum(r => r.Width * r.Height);
        AddBlast(fragment.Position, Math.Clamp(MathF.Sqrt(area) * 0.8f + 1.5f, 3f, 7f), ShipBlastKind.Compartment, inField: true);
        _shipDebris.Remove(fragment);
    }

    // Test-only: shoot the first piece at its own centre / along an arbitrary segment.
    public void DebugDamageDebris(string fragmentId, float damage)
    {
        if (_shipDebris.FirstOrDefault(f => f.Id == fragmentId) is { } fragment)
            DamageDebris(fragment, damage);
    }

    public bool DebugShootSegmentAtDebris(Vec2 from, Vec2 to, float damage) => TryHitDebris(from, to, damage);

    // The push on a piece centred at `pivot` from a blast at `center` (both in the ship-local frame): straight
    // away from the blast, carried into field space by the hull's current attitude.
    private Vec2 KickAwayFrom(Vec2 center, Vec2 pivot, float rotationDegrees)
    {
        var away = pivot - center;
        if (away.Length() < 0.01)
            away = new Vec2(1, 0);
        away = away * (1.0 / away.Length());
        return RotateLocalToWorld(away, rotationDegrees) * BlastKickSpeed;
    }

    // Which way and how fast it turns: the side of the blast it was thrown to decides the direction, a bit of
    // randomness the speed.
    private float SpinFrom(Vec2 center, Vec2 pivot)
    {
        var offset = pivot - center;
        var sign = offset.X * 0.31 + offset.Y * -0.77 >= 0 ? 1f : -1f; // an arbitrary but stable handedness per direction
        var speed = MinBlastSpinDegreesPerSecond + (float)_random.NextDouble() * (MaxBlastSpinDegreesPerSecond - MinBlastSpinDegreesPerSecond);
        return sign * speed;
    }

    private IReadOnlyList<ShipDebrisState> CreateShipDebrisStates() =>
        _shipDebris.Select(f => new ShipDebrisState(f.Id, (float)f.Position.X, (float)f.Position.Y, f.RotationDegrees, f.Rooms)).ToArray();

    // Test-only precondition setter, same convention as DebugBreachWallBlock - fully breaches EVERY
    // wall block a room has (DebugBreachWallBlock itself only ever touches the first one it finds),
    // going through the real private DamageWallBlock so CheckRoomStructuralFailure's own hook fires
    // exactly the way a real cutter/enemy-fire/asteroid-impact sequence gradually working through
    // every block on a room's exterior would - a test that calls this is exercising the actual
    // trigger path, not a shortcut around it.
    public void DebugDestroyRoomWallBlocks(string roomId)
    {
        foreach (var block in Ship.WallBlocks.Where(b => b.RoomId == roomId).ToList())
            DamageWallBlock(block.Id, MaxHpFor(block));
    }
}
