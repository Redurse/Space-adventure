namespace SpaceAdventure.Shared.Model;

// Which plating a hull camera's optical head is bolted to (HullCameraMount turns this into a
// position and a fixed outward bearing) - the same four-direction vocabulary TurretMountSide
// already uses for guns, kept as its own enum rather than shared with turrets since a camera and
// a gun are unrelated hull fixtures that just happen to both bolt onto plating (M48 follow-up -
// "камеры как устройства корабля, а не отдельный виртуальный режим").
public enum CameraMountSide
{
    Aft,
    Fore,
    Port,
    Starboard,
}
