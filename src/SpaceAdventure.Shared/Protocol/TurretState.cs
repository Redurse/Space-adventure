namespace SpaceAdventure.Shared.Protocol;

public sealed record TurretState(
    string Id,
    float AimDegrees,
    int? MannedByPlayerId,
    float CooldownRemaining,
    int AmmoRemaining,
    int MagazineCapacity,
    float Charge = 0f,
    float MaxCharge = 0f,
    bool Damaged = false);
