namespace SpaceAdventure.Shared.Protocol;

// RodCharges: one entry per reactor slot — null means the slot is empty, otherwise how much charge
// the rod in it has left, as a 0..1 fraction (0 = spent rod still sitting in the slot).
public sealed record ReactorState(
    IReadOnlyList<float?> RodCharges,
    float Fuel,
    float MaxFuel,
    float CurrentOutput,
    float MaxOutput);
