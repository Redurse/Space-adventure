using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

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
    Door ShipConnector,
    Vec2 Position,
    Vec2 WorldOffset,
    Vec2 DockingPortPosition,
    IReadOnlyList<WallBlock> WallBlocks,
    IReadOnlyList<WallBlockState> WallBlockStates,
    // The people walking around the docked station (World.StationResidents.cs) - null while undocked and
    // in every older save/test that never knew about them.
    IReadOnlyList<StationResidentState>? Residents = null,
    // A dead station (World.AbandonedStations.cs): dark, airless, nobody aboard.
    bool IsAbandoned = false,
    // The drawing's wall corrections (half-block walls, extra wall tiles) the client replays on its own copy of the tiles - null for a station with none.
    StationTileFixes? TileFixes = null,
    // The station's power plant and the shore connection to the ship - null for a station without a reactor.
    StationPowerState? Power = null);
