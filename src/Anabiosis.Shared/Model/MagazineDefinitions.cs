namespace Anabiosis.Shared.Model;

// The rifle's magazine (TankSockets): it plugs into the rifle the way a gas tank plugs into a cutter,
// and the rifle will not fire without one that still has rounds. Every round of a burst takes one
// "charge" out of it (World.PersonalShots.cs), so an empty magazine means an empty rifle until a fresh
// one goes in.
public static class MagazineDefinitions
{
    public const float FullCharge = 30f;   // rounds
    public const float RoundsPerBullet = 1f;
}
