namespace Anabiosis.Shared.Protocol;

// A live defender aboard the enemy ship (game_design.md Phase 3 - boarding). Position is in the
// enemy ship layout's own room coordinates, same convention as StationNpc.
public sealed record EnemyCrewState(string Id, string Name, string RoomId, float X, float Y, float Health, bool Alive);
