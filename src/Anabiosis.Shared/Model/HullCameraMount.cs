namespace Anabiosis.Shared.Model;

// Where a hull camera's optical head actually sits: outside the plating, facing straight out along
// the mount's own fixed normal - a bolted-down security camera, not something a player can slew
// (M48 follow-up - "статичный вид... сектор камеры широкий сам по себе"). Mirrors TurretMount's own
// split between an interior crew station and an exterior physical point exactly, right down to the
// per-side geometry, since a camera dome and a gun mount are both just "a fixture bolted to this
// hull's plating" - only the fixed OutwardDegrees matters here, there's no muzzle/lead offset to
// aim since nothing about a camera's facing ever changes after it's installed.
public readonly record struct HullCameraMount(Vec2 Position, float OutwardDegrees)
{
    private const float HullStandoff = 0.3f; // a camera housing sits almost flush, unlike a turret's barrel ring

    public static HullCameraMount For(IReadOnlyList<Room> rooms, IReadOnlyList<HullCamera> allCameras, HullCamera camera)
    {
        var room = rooms.FirstOrDefault(r => r.Contains(camera.InteriorPosition)) ?? rooms[0];

        switch (camera.MountSide)
        {
            case CameraMountSide.Port:
                return new HullCameraMount(new Vec2(room.Left - HullStandoff, camera.Y), 180f);
            case CameraMountSide.Starboard:
                return new HullCameraMount(new Vec2(room.Right + HullStandoff, camera.Y), 0f);
        }

        var minY = rooms.Min(r => r.Top);
        var maxY = rooms.Max(r => r.Bottom);
        var sharingTheFace = allCameras.Where(c => c.MountSide == camera.MountSide).ToList();
        var slot = sharingTheFace.FindIndex(c => c.Id == camera.Id);
        if (slot < 0)
            slot = 0;

        // Evenly spaced across the plating, same rule TurretMount uses for Fore/Aft guns: one
        // camera sits dead centre, two sit at a third and two thirds of the way down, and so on.
        var y = minY + (maxY - minY) * (slot + 1f) / (sharingTheFace.Count + 1f);

        return camera.MountSide == CameraMountSide.Fore
            ? new HullCameraMount(new Vec2(rooms.Min(r => r.Left) - HullStandoff, y), 180f)
            : new HullCameraMount(new Vec2(rooms.Max(r => r.Right) + HullStandoff, y), 0f);
    }
}
