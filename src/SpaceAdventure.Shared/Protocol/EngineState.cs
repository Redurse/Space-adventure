using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// Cosmoteer-style marching engine (direct user request, ShipEngine.cs's own doc comment) - each of
// the 3 tiles gets its own Hp/MaxHp pair, the same shape WallBlockState already uses for a single
// wall panel. IsThrusting is purely cosmetic (drives the client's animated exhaust flame/glow) -
// true whenever the nozzle is intact and the engine's own effective throttle is actually nonzero.
public sealed record EngineState(
    string Id, float X, float Y, TileSide Facing,
    float ControlHp, float BulkheadHp, float NozzleHp, float MaxHp,
    bool IsThrusting)
{
    public bool ControlBroken => ControlHp <= 0f;
    public bool BulkheadBroken => BulkheadHp <= 0f;
    public bool NozzleBroken => NozzleHp <= 0f;
}
