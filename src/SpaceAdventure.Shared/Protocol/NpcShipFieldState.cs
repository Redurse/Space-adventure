using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// Ambient traffic in the current system (World.NpcShips.cs, M43) - unlike EnemyShipFieldState's
// squadron, these exist whether or not the player has ever come near them, so the client just
// draws whatever this list happens to contain each tick, the same way it already draws the enemy
// squadron. A Military hull that has turned hostile and closed to combat range is converted into a
// real EnemyShipFieldState instead (World.NpcShips.cs's TryEngageHostileNpc) and drops out of this
// list for as long as that fight lasts - the two lists never describe the same hull at once.
public sealed record NpcShipFieldState(
    string Id,
    NpcShipKind Kind,
    FactionId FactionId,
    float X,
    float Y,
    float RotationDegrees);
