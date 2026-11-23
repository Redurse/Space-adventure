namespace Anabiosis.Shared.Model;

// The revolver, after Barotrauma's: a single-handed sidearm with a six-round cylinder. It has its own socket (TankSockets) that takes RevolverRounds - one
// "charge" is one round - and every pull of the trigger spends exactly one. Rounds come in stacks of up to six (an item's charge is how many are in the
// stack, shown as "x6"); dragging a stack onto the revolver tops its cylinder up to six and leaves the rest in the stack. With the cylinder empty and
// a stack in your pockets, the next pull of the trigger reloads from it (World.WeaponFire.cs) before it fires.
public static class RevolverDefinitions
{
    public const float CylinderRounds = 6f;
    public const float RoundsPerShot = 1f;
}
