namespace SpaceAdventure.Shared.Protocol;

public sealed record ReactorState(
    IReadOnlyList<bool> RodSlots,
    float Fuel,
    float MaxFuel,
    float CurrentOutput,
    float MaxOutput);
