namespace SpaceAdventure.Shared.Protocol;

// Hp/MaxHp describe the ship currently being fought. RemainingShips counts the whole squadron
// still defending the sector, including this one (game_design.md section 12) - it's 1 for a lone
// raider and 0 once the sector is cleared.
public sealed record EnemyShipState(float Hp, float MaxHp, bool IsRetreating, int RemainingShips = 1);
