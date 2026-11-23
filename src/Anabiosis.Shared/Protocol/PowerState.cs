using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

public sealed record PowerState(
    float ReactorMaxOutput,
    float ReactorOutput,
    float ReactorFuel,
    float ReactorMaxFuel,
    float BatteryCharge,
    float BatteryCapacity,
    IReadOnlyDictionary<PowerSystemId, float> Allocated,
    // W3 of wiring-plan.md - the fusion reactor in auto mode: what its grid asks of it, what it
    // actually gives (the panel's two graph lines), its start/stop button, and plasma temperature
    // in millions of kelvin. ReactorOutput above is where its plasma currently sits.
    float ReactorLoad = 0f,
    float ReactorDelivered = 0f,
    bool ReactorRunning = true,
    float PlasmaTemperature = 0f);
