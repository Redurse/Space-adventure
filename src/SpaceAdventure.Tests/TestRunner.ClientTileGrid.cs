using System.Collections.Generic;
using System.Linq;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

// Regression guard for a real bug found live (humble-soaring-cat.md, "из одного отсека если дверь
// открыта то игрок может видеть что в другом отсеке, а не как сейчас"): ClientTileGrid.Build used to
// never overlay live door-open state onto the TileGrid it hands back (M75's own original doc comment
// argued the renderer didn't need it), which was fine for M75's rendering-only consumer but silently
// broke M78's TileOccluders consumer (Game1.Lighting.cs) - every door tile came back permanently
// "closed" here, so opening a door in the live game never actually removed the wall segment blocking
// sight through the doorway. See ClientTileGrid.cs's own doc comment for the full mechanism.
internal static partial class TestRunner
{
    private static bool ClientTileGrid_Build_OverlaysLiveDoorOpenState()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var door = world.Ship.Doors.First(d => d.Id == "door-cockpit-reactor");

        var openSnapshot = world.CreateSnapshot();
        var doorCoords = TileGridRasterizer.DoorTileCoords(openSnapshot.Rooms, door.X, door.Y, door.Width, door.Height).ToList();
        var openTiles = ClientTileGrid.Build(openSnapshot);
        // Doors start open (World_ToggleDoor_ViaClientCommand_FlipsState's own "before" assumption) -
        // the exact regression: this used to come back DoorOpen=false here regardless of live state.
        if (!doorCoords.All(c => openTiles.CellAt(c) is { Wall: TileWallKind.Door, DoorOpen: true }))
            return false;

        world.ApplyCommand(1, new ClientCommand(1, DoorToggleId: door.Id));
        var closedTiles = ClientTileGrid.Build(world.CreateSnapshot());
        return doorCoords.All(c => closedTiles.CellAt(c) is { Wall: TileWallKind.Door, DoorOpen: false });
    }

    // The true end-to-end proof: feeding a live, open-door snapshot through the exact same
    // ClientTileGrid.Build + TileOccluders.Build + SightGap pipeline Game1.Lighting.cs uses must
    // leave NO wall segment covering the doorway - on either the tile's near (outer) face or its far
    // (inner) face, the specific asymmetric-footprint mismatch that used to leave one face uncut even
    // when the gap-cutting fallback ran (see ClientTileGrid.cs's own doc comment).
    private static bool ClientTileGrid_OpenDoor_LeavesNoWallSegmentForTileOccludersToBlockSightWith()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var door = world.Ship.Doors.First(d => d.Id == "door-cockpit-reactor");
        var snapshot = world.CreateSnapshot();

        var gaps = new List<SightGap> { Occluders.ToGap(door) }; // same construction Game1.Lighting.cs uses for an open door
        var segments = TileOccluders.Build(ClientTileGrid.Build(snapshot), gaps);

        // door-cockpit-reactor sits at the reactor room's own leading (x=5) edge - its tile's West
        // face is at x=5, its East face at x=6 (TileOccluders.Build's own edge convention).
        return !AnyVerticalCovers(segments, 5f, door.Y) && !AnyVerticalCovers(segments, 6f, door.Y);
    }

    private static bool AnyVerticalCovers(IReadOnlyList<WallSegment> segments, float x, float y) =>
        segments.Any(s => System.MathF.Abs(s.Ax - x) < TileOccludersEpsilon && System.MathF.Abs(s.Bx - x) < TileOccludersEpsilon &&
            s.Ay - TileOccludersEpsilon <= y && y <= s.By + TileOccludersEpsilon);

    // The FIRST fix only overlaid live door state onto the player's own ship's tiles
    // (ClientTileGrid.Build) - Game1.Lighting.cs's docked case rasterizes the STATION's own layout
    // through a completely separate TileGridRasterizer.FromRooms call, which never got the same
    // treatment, so a station door (or the ship<->station connector) stayed permanently "closed" to
    // TileOccluders even while genuinely open - the identical bug, missed on the other structure,
    // caught live by the user after the first fix ("баг с видимостью через двери не исправлен").
    private static bool ClientTileGrid_ApplyLiveDoorState_AlsoWorksForStationTiles()
    {
        var world = new World();
        if (!world.IsDocked)
            return false; // a fresh campaign always starts docked - this test needs that to hold
        world.SpawnCharacter(1);
        var door = world.Station.Doors.FirstOrDefault();
        if (door is null)
            return true; // nothing to prove if this station layout has no interior doors at all

        var snapshot = world.CreateSnapshot();
        var doorCoords = TileGridRasterizer.DoorTileCoords(snapshot.Station.Rooms, door.X, door.Y, door.Width, door.Height).ToList();
        var stationTiles = TileGridRasterizer.FromRooms(snapshot.Station.Rooms, snapshot.Station.Doors, new[] { snapshot.Station.ShipConnector });
        ClientTileGrid.ApplyLiveDoorState(stationTiles, snapshot.Station.Rooms, snapshot.Station.Doors,
            new[] { snapshot.Station.ShipConnector }, snapshot.DoorStates);

        // Station doors default open the same way a ship's own regular Door does (ClientTileGrid's
        // own ?? true fallback) - this must come back DoorOpen=true, not the pre-fix always-false.
        return doorCoords.All(c => stationTiles.CellAt(c) is { Wall: TileWallKind.Door, DoorOpen: true });
    }
}
