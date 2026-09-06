using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

public sealed record PowerState(
    float ReactorMaxOutput,
    float ReactorOutput,
    float ReactorFuel,
    float ReactorMaxFuel,
    float BatteryCharge,
    float BatteryCapacity,
    IReadOnlyDictionary<PowerSystemId, float> Allocated);
