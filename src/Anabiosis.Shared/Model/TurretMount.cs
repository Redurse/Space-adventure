namespace Anabiosis.Shared.Model;

// Where a turret's gun actually sits: on top of the ship, directly above the turret device it is
// crewed from (direct user request - the gun is drawn over the hull at the device's own spot and
// turns a full 360 degrees; it is only ever seen from outside the ship, never from inside a
// compartment). The periscope (Turret.PeriscopePosition) is the crew station *inside* a room - the
// thing you walk up to and man - and the gun is the visual/muzzle half that sits right above it.
//
// MountSide no longer picks a plating position or limits a firing arc; it only says which way the
// barrel rests (aim 0) before anyone has swung it. Aim is relative to that rest direction and wraps
// around instead of clamping.
public readonly record struct TurretMount(Vec2 Position, float OutwardDegrees)
{
    // How much bigger the gun is drawn than its baked sprite (TurretSkin) - it has to read as a gun
    // sitting on a 4x3-tile device, not as a marker. The barrel length scales with it so the shell
    // leaves the drawn muzzle.
    public const float VisualScale = 1.6f;
    public const float BakedBarrelLength = 1.3f;
    public const float BarrelLength = BakedBarrelLength * VisualScale;

    public static TurretMount For(IReadOnlyList<Room> rooms, IReadOnlyList<Turret> allTurrets, Turret turret) =>
        new(turret.PeriscopePosition, turret.MountSide switch
        {
            TurretMountSide.Fore or TurretMountSide.Port => 180f,
            _ => 0f,
        });

    // Aim is relative to the rest direction (OutwardDegrees) and wraps: any angle is reachable.
    public float FireDegrees(float aimDegrees) => OutwardDegrees + aimDegrees;

    public Vec2 FireDirection(float aimDegrees) => FromDegrees(FireDegrees(aimDegrees));

    // Where the shell leaves the barrel - the tip of the drawn barrel, ahead of the turret centre.
    public Vec2 Muzzle(float aimDegrees) => Position + FireDirection(aimDegrees) * BarrelLength;

    // Wraps any angle into (-180, 180].
    public static float WrapDegrees(float degrees)
    {
        var wrapped = ((degrees + 180f) % 360f + 360f) % 360f - 180f;
        return wrapped == -180f ? 180f : wrapped;
    }

    public static Vec2 FromDegrees(float degrees)
    {
        var radians = degrees * (MathF.PI / 180f);
        return new Vec2(MathF.Cos(radians), MathF.Sin(radians));
    }
}
