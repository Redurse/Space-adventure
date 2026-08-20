using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// The docked station's own interior/property/crew state, split out of WorldSnapshot's own flat
// field list into its own group - everything here is read together by StationRenderer/
// StationPanel/the radar blip and nowhere else needs it piecemeal.
//
// Rooms/Doors/Npcs/Crates/WallBlocks are all in the *docked* frame - the ship's own interior
// coordinates - so a docked station needs no conversion to draw. Add WorldOffset to get field/
// world coordinates instead, which is what the exterior view and the radar plot in.
public sealed record StationSnapshot(
    IReadOnlyList<StationNpc> Npcs,
    IReadOnlyList<StationCrate> Crates,
    IReadOnlyList<StationCrateState> CrateStates,
    IReadOnlyList<StationGuardState> Guards,
    IReadOnlyList<Room> Rooms,
    IReadOnlyList<Door> Doors,
    AirlockOuterDoor ShipConnector,
    Vec2 Position,
    Vec2 WorldOffset,
    Vec2 DockingPortPosition,
    IReadOnlyList<WallBlock> WallBlocks,
    IReadOnlyList<WallBlockState> WallBlockStates);
