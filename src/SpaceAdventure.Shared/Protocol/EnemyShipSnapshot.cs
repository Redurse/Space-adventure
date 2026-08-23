using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// The boardable enemy hull's own interior/crew/position, grouped the same way Station/AsteroidField
// are - BoardingRenderer and the boarding-related tests always want the whole thing together.
// Deliberately does NOT include WorldSnapshot's own top-level Enemy (hp/shields) or Projectiles/
// PersonalShots fields - "Enemy" alone is too overloaded a name (World.Enemy is a distinct
// server-side property) to fold in safely alongside this group without real risk of confusing the
// two, and Projectiles/PersonalShots are general combat state, not specifically about this hull's
// own structure.
public sealed record EnemyShipSnapshot(
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Door> Doors,
    AirlockOuterDoor BoardingHatch,
    string ClassName,
    IReadOnlyList<RoomOxygenState> RoomOxygen,
    Vec2 Position,
    IReadOnlyList<EnemyShipFieldState> Ships,
    IReadOnlyList<EnemyCrewState> Crew,
    // The boardable hull's own cuttable exterior - positions come from EnemyShipLayout.WallBlocks
    // (a pure function of ClassName/the boardable ship's Kind), only the per-instance Hp/Breached
    // state actually needs to cross the wire, same split Station/Ship's own wall blocks already use.
    IReadOnlyList<WallBlock> WallBlocks,
    IReadOnlyList<WallBlockState> WallBlockStates);
