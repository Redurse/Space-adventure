namespace SpaceAdventure.Shared.Model;

// Where a turret's gun actually sits: outside the hull, with the barrel pointing away from the
// ship. The periscope (Turret.PeriscopePosition) is the crew station *inside* a room - the thing
// you walk up to and man - and the two are deliberately different places, which is what lets a
// shot leave the ship through a muzzle instead of materialising out of a console.
//
// Every gun sits on the aft plating, spread evenly across it. That's the quarter this hull fights
// over: raiders close from astern (World.EnemyFleet.cs) and the airlock that puts a boarding party
// outside is back there too, so the guns, the enemy and the way out all face the same way. A
// +-45 arc anywhere else would spend the fight pointing at empty space.
//
// Derived from the hull's own bounds rather than stored per turret, so a new ship class gets its
// mounts for free and they can't drift out of sync with a layout change (game_design.md section 2).
public readonly record struct TurretMount(Vec2 Position, float OutwardDegrees)
{
    public const float BarrelLength = 1.3f;
    private const float HullStandoff = 0.5f; // how far off the plating the mount ring sits

    public static TurretMount For(IReadOnlyList<Room> rooms, IReadOnlyList<Turret> allTurrets, Turret turret)
    {
        // Flank guns sit on the wall of the room they're crewed from, so a broadside comes out of
        // the gun deck's own plating rather than from somewhere else along the hull. Fore/aft guns
        // use the hull's end plating and share it, spread evenly, since several of them face the
        // same way down a hull that's mostly one long row.
        var room = rooms.FirstOrDefault(r => r.Contains(turret.PeriscopePosition)) ?? rooms[0];

        switch (turret.MountSide)
        {
            case TurretMountSide.Port:
                return new TurretMount(new Vec2(room.Left - HullStandoff, turret.PeriscopeY), 180f);
            case TurretMountSide.Starboard:
                return new TurretMount(new Vec2(room.Right + HullStandoff, turret.PeriscopeY), 0f);
        }

        var minY = rooms.Min(r => r.Top);
        var maxY = rooms.Max(r => r.Bottom);
        var sharingTheFace = allTurrets.Where(t => t.MountSide == turret.MountSide).ToList();
        var slot = sharingTheFace.FindIndex(t => t.Id == turret.Id);
        if (slot < 0)
            slot = 0;

        // Evenly spaced across the plating: one gun sits dead centre, two sit at a third and two
        // thirds of the way down, and so on.
        var y = minY + (maxY - minY) * (slot + 1f) / (sharingTheFace.Count + 1f);

        return turret.MountSide == TurretMountSide.Fore
            ? new TurretMount(new Vec2(rooms.Min(r => r.Left) - HullStandoff, y), 180f)
            : new TurretMount(new Vec2(rooms.Max(r => r.Right) + HullStandoff, y), 0f);
    }

    // Aim is relative to straight out of the mount, so the same -45..+45 arc means "45 degrees off
    // the plating's normal" on every ship regardless of which end the gun ended up on.
    public float FireDegrees(float aimDegrees) => OutwardDegrees + aimDegrees;

    public Vec2 FireDirection(float aimDegrees) => FromDegrees(FireDegrees(aimDegrees));

    // Where the shell leaves the barrel - clear of the plating, so a shot never spawns inside the
    // ship that fired it.
    public Vec2 Muzzle(float aimDegrees) => Position + FireDirection(aimDegrees) * BarrelLength;

    public static Vec2 FromDegrees(float degrees)
    {
        var radians = degrees * (MathF.PI / 180f);
        return new Vec2(MathF.Cos(radians), MathF.Sin(radians));
    }
}
