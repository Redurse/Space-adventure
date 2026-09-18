namespace Anabiosis.Shared.Protocol;

// Direct user request ("скрытое число хп" отсека, показать в терминале управления кораблем) -
// World.RoomHp.cs's own per-room shadow-damage pool, mirroring RoomOxygenState's exact shape.
public sealed record RoomHpState(string RoomId, float Hp, float MaxHp);
