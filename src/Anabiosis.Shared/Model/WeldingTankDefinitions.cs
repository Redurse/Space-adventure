namespace Anabiosis.Shared.Model;

// The welding tool's own tank (TankSockets) - the torch doesn't light without a charged one in
// its socket, same story as the cutter and its oxygen tank (OxygenTankDefinitions), just its own
// separate consumable so patching a hull doesn't quietly spend the same air a suit breathes.
public static class WeldingTankDefinitions
{
    public const float FullCharge = 100f;

    // The flame burns continuously while aimed at a breach, same as the cutter burns on ore -
    // about half a minute of steady welding per tank.
    public const float DrainPerSecond = 3.2f;
}
