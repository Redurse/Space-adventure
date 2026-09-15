using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Client.Rendering;
using Anabiosis.Server;
using Anabiosis.Shared.Model;

// Direct user request ("я хочу полностью переделать двери" - the narrow/1-tile door specifically,
// humble-soaring-cat.md) - the new door-as-an-EDGE-between-two-tiles primitive on TileGrid, tested
// directly the same way TestRunner.TileGrid.cs already tests Solid/Door TILE region topology.
internal static partial class TestRunner
{
    private static bool TileGrid_DoorEdge_RequiresBothFlankingTilesAlreadyFreeFloor()
    {
        var grid = new TileGrid();
        var a = new TileCoord(0, 0);
        var b = new TileCoord(1, 0);

        // Neither tile floored yet - can't place.
        if (grid.CanPlaceDoorEdge(a, TileSide.East))
            return false;

        grid.SetFloor(a, true);
        if (grid.CanPlaceDoorEdge(a, TileSide.East))
            return false; // b still has no floor

        grid.SetFloor(b, true);
        if (!grid.CanPlaceDoorEdge(a, TileSide.East))
            return false; // both free floor now - valid

        // A wall on either tile blocks it (this door never converts/steals a wall tile).
        grid.SetWall(b, TileWallKind.Solid);
        if (grid.CanPlaceDoorEdge(a, TileSide.East))
            return false;
        grid.SetWall(b, TileWallKind.None);

        // A device on either tile blocks it too.
        grid.PlaceDevice(b, "some-device");
        if (grid.CanPlaceDoorEdge(a, TileSide.East))
            return false;
        grid.RemoveDevice(b);

        grid.AddDoorEdge(a, TileSide.East, "door-1");
        // Already has an edge there now - placing a second one at the same seam should refuse.
        return !grid.CanPlaceDoorEdge(a, TileSide.East);
    }

    // Both flanking tiles STAY ordinary floor region members (unlike a wall/door TILE, which drops
    // out of the region graph entirely) - the door edge just keeps them from being in the SAME
    // region while it's intact, exactly the way an intact wall/door tile keeps its two neighbors
    // apart today (TileGrid_SolidWallSplitsRegionThenNoneMergesItBack's own mirror image).
    private static bool TileGrid_DoorEdge_SplitsRegionThenRemovalMergesItBack()
    {
        var grid = new TileGrid();
        var a = new TileCoord(0, 0);
        var b = new TileCoord(1, 0);
        grid.SetFloor(a, true);
        grid.SetFloor(b, true);
        if (grid.Regions.Count != 1)
            return false; // setup: one connected region before the edge exists

        grid.AddDoorEdge(a, TileSide.East, "door-1");
        if (grid.Regions.Count != 2)
            return false;
        // Unlike a wall tile, BOTH flanking tiles are still real region members.
        if (grid.RegionIdAt(a) is null || grid.RegionIdAt(b) is null)
            return false;
        if (grid.RegionIdAt(a) == grid.RegionIdAt(b))
            return false;

        grid.RemoveDoorEdge(a, TileSide.East);
        if (grid.Regions.Count != 1)
            return false;
        return grid.RegionIdAt(a) == grid.RegionIdAt(b) && grid.Regions[grid.RegionIdAt(a)!.Value].Tiles.Count == 2;
    }

    // Open/closed never changes region topology (mirrors TileGrid_OpenDoorNeverMergesRegionsButIsWalkable
    // for tile-based doors) - only breaching the edge (Hp<=0) merges the regions back, and repairing
    // it splits them again, exactly like DamageWall/RepairWall already do for a wall tile.
    private static bool TileGrid_DoorEdge_OpenOrClosedNeverChangesTopology_OnlyBreachDoes()
    {
        var grid = new TileGrid();
        var a = new TileCoord(0, 0);
        var b = new TileCoord(1, 0);
        grid.SetFloor(a, true);
        grid.SetFloor(b, true);
        grid.AddDoorEdge(a, TileSide.East, "door-1");

        grid.SetDoorEdgeOpen(a, TileSide.East, true);
        if (grid.Regions.Count != 2 || grid.RegionIdAt(a) == grid.RegionIdAt(b))
            return false; // opening it must NOT merge the regions
        if (!grid.IsWalkableAcrossEdge(a, b))
            return false; // but it must be walkable now that it's open

        grid.SetDoorEdgeOpen(a, TileSide.East, false);
        if (grid.IsWalkableAcrossEdge(a, b))
            return false; // closed again - blocked

        grid.DamageDoorEdge(a, TileSide.East, 1000f);
        if (grid.Regions.Count != 1 || grid.RegionIdAt(a) != grid.RegionIdAt(b))
            return false; // breached - merges regardless of the (closed) open flag
        if (!grid.IsWalkableAcrossEdge(a, b))
            return false; // breached also means walkable regardless of open flag

        grid.RepairDoorEdge(a, TileSide.East, 1000f);
        return grid.Regions.Count == 2 && grid.RegionIdAt(a) != grid.RegionIdAt(b);
    }

    // If there's another path around a newly-placed edge (a loop), adding the edge must NOT split
    // the region - SplitRegionIfDisconnected's own "still one connected piece" case, exercised here
    // through the door-edge entry point rather than a removed wall tile.
    private static bool TileGrid_DoorEdge_LoopWithAnotherPathAround_DoesNotSplitRegion()
    {
        var grid = new TileGrid();
        // A 2x2 ring of floor: (0,0)-(1,0)-(1,1)-(0,1)-(0,0).
        var coords = new[] { new TileCoord(0, 0), new TileCoord(1, 0), new TileCoord(1, 1), new TileCoord(0, 1) };
        foreach (var c in coords)
            grid.SetFloor(c, true);
        if (grid.Regions.Count != 1)
            return false;

        grid.AddDoorEdge(new TileCoord(0, 0), TileSide.East, "door-1");
        // Still one region - (0,0) can still reach (1,0) the long way around via (0,1)/(1,1).
        return grid.Regions.Count == 1 && grid.RegionIdAt(new TileCoord(0, 0)) == grid.RegionIdAt(new TileCoord(1, 0));
    }

    // The other flanking direction/order must resolve to the exact same edge (CanonicalEdgeKey) -
    // asking from either side, or via North/South instead of South/North, must never create or see
    // two independent edges for what is physically one seam.
    private static bool TileGrid_DoorEdge_CanonicalizesRegardlessOfWhichSideItsAskedFrom()
    {
        var grid = new TileGrid();
        var a = new TileCoord(0, 0);
        var b = new TileCoord(0, 1);
        grid.SetFloor(a, true);
        grid.SetFloor(b, true);
        grid.AddDoorEdge(a, TileSide.South, "door-1");

        if (grid.DoorEdgeAt(b, TileSide.North) is not { } edgeFromFar || edgeFromFar.Id != "door-1")
            return false;
        // Toggling from the far side must affect the SAME edge.
        grid.SetDoorEdgeOpen(b, TileSide.North, true);
        return grid.DoorEdgeAt(a, TileSide.South)!.Open;
    }

    private static bool TileMovement_BlocksMovementAcrossClosedDoorEdge_AllowsWhenOpenOrBreached()
    {
        var grid = new TileGrid();
        var a = new TileCoord(0, 0);
        var b = new TileCoord(1, 0);
        grid.SetFloor(a, true);
        grid.SetFloor(b, true);
        grid.AddDoorEdge(a, TileSide.East, "door-1");

        // Standing well inside tile a, moving east should be stopped right at the seam (x=1) while closed.
        var start = new Vec2(0.5, 0.5);
        var afterClosed = TileMovement.MoveAlongAxis(grid, start, new Vec2(0.6, 0));
        if (afterClosed.X >= 1.0 - TileMovement.CharacterRadius + 0.01)
            return false; // must not have crossed into tile b

        grid.SetDoorEdgeOpen(a, TileSide.East, true);
        var afterOpen = TileMovement.MoveAlongAxis(grid, start, new Vec2(0.6, 0));
        if (afterOpen.X < 1.0)
            return false; // open now - must actually cross

        grid.SetDoorEdgeOpen(a, TileSide.East, false);
        grid.DamageDoorEdge(a, TileSide.East, 1000f);
        var afterBreached = TileMovement.MoveAlongAxis(grid, start, new Vec2(0.6, 0));
        return afterBreached.X >= 1.0; // breached - walkable regardless of the (closed) open flag
    }

    // Erasing a flanking tile's floor entirely must take its door edge down with it - the edge lives
    // in a SEPARATE dictionary, not on the TileCell being removed, so without an explicit cleanup it
    // would otherwise dangle, pointing at a coordinate with no floor at all.
    private static bool TileGrid_DoorEdge_RemovedWhenFlankingFloorIsErased()
    {
        var grid = new TileGrid();
        var a = new TileCoord(0, 0);
        var b = new TileCoord(1, 0);
        grid.SetFloor(a, true);
        grid.SetFloor(b, true);
        grid.AddDoorEdge(a, TileSide.East, "door-1");

        grid.SetFloor(a, false);
        return grid.DoorEdgeAt(b, TileSide.West) is null && grid.Regions.Count == 1
            && grid.RegionIdAt(b) is { } && grid.Regions[grid.RegionIdAt(b)!.Value].Tiles.Count == 1;
    }

    // Painting an actual wall/door TILE directly onto one of an edge's own flanking tiles must take
    // the edge down with it too - same reasoning as the floor-erasure case above, just via SetWall
    // instead of SetFloor(false).
    private static bool TileGrid_DoorEdge_RemovedWhenFlankingTileBecomesAWall()
    {
        var grid = new TileGrid();
        var a = new TileCoord(0, 0);
        var b = new TileCoord(1, 0);
        grid.SetFloor(a, true);
        grid.SetFloor(b, true);
        grid.AddDoorEdge(a, TileSide.East, "door-1");

        grid.SetWall(b, TileWallKind.Solid);
        return grid.DoorEdgeAt(a, TileSide.East) is null;
    }

    // Same shape as TestRunner.ClientRenderMatchesServerCollision.cs's own
    // BuildHalfBlockNotchShipDefinition (a minimal but fully valid, playable custom hull so the WHOLE
    // pipeline runs end to end) - here exercising a door edge instead of a half-block notch: does it
    // survive TileShipBuilder -> Ship.FromCustomDefinition (server) -> World.CreateSnapshot ->
    // ClientTileGrid.Build (client) with server and client agreeing on collision AND occlusion?
    private static CustomShipDefinition BuildDoorEdgeShipDefinition()
    {
        var tiles = new TileGrid();
        for (var y = 0; y < 10; y++)
            for (var x = 0; x <= 6; x++)
                tiles.SetFloor(new TileCoord(x, y), true);

        tiles.SetFloor(new TileCoord(3, -1), true);
        tiles.SetWall(new TileCoord(3, -1), TileWallKind.Door);

        var deviceKinds = new Dictionary<TileCoord, CustomDeviceKind>
        {
            [new TileCoord(1, 1)] = CustomDeviceKind.Reactor,
            [new TileCoord(2, 1)] = CustomDeviceKind.Distribution,
            [new TileCoord(3, 1)] = CustomDeviceKind.Helm,
            [new TileCoord(4, 1)] = CustomDeviceKind.Navigation,
            [new TileCoord(5, 1)] = CustomDeviceKind.Oxygen,
            [new TileCoord(1, 2)] = CustomDeviceKind.SuitLocker,
            [new TileCoord(2, 2)] = CustomDeviceKind.StorageRack,
        };
        foreach (var (coord, kind) in deviceKinds)
            tiles.PlaceDevice(coord, kind.ToString());

        var engineControl = new TileCoord(5, 2);
        tiles.PlaceDevice(engineControl, "engine");
        var engines = new Dictionary<TileCoord, TileShipBuilder.EngineSpec> { [engineControl] = new(TileSide.South, 10f) };

        // The door edge itself - between two ordinary floor tiles well away from every device above.
        tiles.AddDoorEdge(new TileCoord(3, 6), TileSide.South, "test-door-edge");

        var (definition, errors) = TileShipBuilder.BuildDefinition(tiles, deviceKinds, engines, "door-edge-ship", 0f);
        if (definition is null)
            throw new InvalidOperationException("setup problem: " + string.Join("; ", errors));
        return definition;
    }

    private static bool World_DoorEdge_ServerAndClientAgreeOnCollisionAndOcclusion()
    {
        var definition = BuildDoorEdgeShipDefinition();
        var world = new World(ShipKind.Custom, definition);
        var edgeCoord = new TileCoord(3, 6);
        const TileSide edgeSide = TileSide.South;

        if (world.Ship.DoorEdges.Count != 1)
            return false;
        var edge = world.Ship.DoorEdges[0];
        if (edge.Coord != edgeCoord || edge.Side != edgeSide || edge.RoomAId != edge.RoomBId)
            return false; // both flanking tiles are the same single room in this simple rectangular hull

        // One tick so World.TileSync.cs's SyncShipTiles reconciles _doorEdgeOpen/_doorEdgeHp onto
        // Ship.Tiles.DoorEdges - exactly what always happens before any real snapshot goes out
        // (GameServer.cs calls Step every tick, ahead of every per-client CreateSnapshot).
        world.Step(0.01);

        // Interior door edges default OPEN (InitializeShipState's own "preserves the pre-M16
        // always-passable behavior") - open must never block movement or sight.
        if (world.Ship.Tiles.DoorEdgeAt(edgeCoord, edgeSide) is not { Open: true, Hp: > 0 })
            return false;
        if (!world.Ship.Tiles.IsWalkableAcrossEdge(edgeCoord, edgeSide.Offset(edgeCoord)))
            return false;

        var openSnapshot = world.CreateSnapshot();
        if (openSnapshot.DoorEdges is not { Count: 1 } || openSnapshot.DoorEdgeStates is not { Count: 1 })
            return false;
        var openClientTiles = ClientTileGrid.Build(openSnapshot);
        if (openClientTiles.DoorEdgeAt(edgeCoord, edgeSide) is not { Open: true })
            return false;
        // Open - no occluding segment on the seam line at all (same "no wall here" treatment an open
        // door tile already gets, TileOccluders.IsOccluding's own doc comment).
        var openSegments = TileOccluders.Build(openClientTiles, Array.Empty<SightGap>());
        var seam = new WallSegment(edgeCoord.X, edgeCoord.Y + 1, edgeCoord.X + 1, edgeCoord.Y + 1);
        if (openSegments.Contains(seam))
            return false;

        // Now close it (ToggleDoor reuses the SAME id space ClientCommand.DoorToggleId already
        // carries, no protocol change) and re-run the whole pipeline from scratch.
        world.ToggleDoor(edge.Id);
        world.Step(0.01);

        if (world.Ship.Tiles.DoorEdgeAt(edgeCoord, edgeSide) is not { Open: false, Hp: > 0 })
            return false;
        if (world.Ship.Tiles.IsWalkableAcrossEdge(edgeCoord, edgeSide.Offset(edgeCoord)))
            return false; // must now block movement

        var closedSnapshot = world.CreateSnapshot();
        var closedClientTiles = ClientTileGrid.Build(closedSnapshot);
        if (closedClientTiles.DoorEdgeAt(edgeCoord, edgeSide) is not { Open: false })
            return false;
        var closedSegments = TileOccluders.Build(closedClientTiles, Array.Empty<SightGap>());
        return closedSegments.Contains(seam); // closed - must occlude sight along the full seam
    }

    // Direct user request ("сделай по аналогии дверь 1 на 2 т е дверь занимающую 4 клетки и назови
    // ее широкой дверью") - a "wide" edge door is just 2 PARALLEL narrow edges sharing one Id
    // (Game1.ShipEditor.cs's own PlaceEdgeDoor/DoorEdgeGroupAt) - nothing new at the TileGrid
    // level at all, so this proves the region only actually splits once BOTH stand, exactly like a
    // single edge would with "another path around" (TileGrid_DoorEdge_LoopWithAnotherPathAround_
    // DoesNotSplitRegion) - the second parallel edge closing the LAST remaining path.
    private static bool TileGrid_DoorEdge_WideDoor_BothParallelEdgesNeededToSplitRegion()
    {
        var grid = new TileGrid();
        // A 2x2 free-floor block: column A = (0,0)/(0,1), column B = (1,0)/(1,1).
        var a1 = new TileCoord(0, 0);
        var a2 = new TileCoord(0, 1);
        var b1 = new TileCoord(1, 0);
        foreach (var c in new[] { a1, a2, b1, new TileCoord(1, 1) })
            grid.SetFloor(c, true);
        if (grid.Regions.Count != 1)
            return false;

        grid.AddDoorEdge(a1, TileSide.East, "wide-door-1");
        // Still one region - the a2/b2 crossing is still wide open.
        if (grid.Regions.Count != 1 || grid.RegionIdAt(a1) != grid.RegionIdAt(b1))
            return false;

        grid.AddDoorEdge(a2, TileSide.East, "wide-door-1");
        // Both crossings closed now - the region must actually split.
        return grid.Regions.Count == 2 && grid.RegionIdAt(a1) != grid.RegionIdAt(b1);
    }

    // Same 2x2 shape, exercised through the real Ship/World pipeline this time - 2 CustomDoorEdgeDef
    // entries sharing one Id (exactly what TileShipBuilder's export would produce for a wide door
    // the editor placed, since it just copies TileGrid.DoorEdges verbatim). Confirms the "one id,
    // several physical edges" trick actually holds together end to end: one CreateDoorEdgeStates
    // entry (not two) for the shared id, and toggling that one id moves BOTH physical edges at once.
    private static bool World_DoorEdge_WideDoor_TwoSegmentsShareIdAndToggleTogether()
    {
        var tiles = new TileGrid();
        for (var y = 0; y < 10; y++)
            for (var x = 0; x <= 7; x++)
                tiles.SetFloor(new TileCoord(x, y), true);

        tiles.SetFloor(new TileCoord(3, -1), true);
        tiles.SetWall(new TileCoord(3, -1), TileWallKind.Door);

        var deviceKinds = new Dictionary<TileCoord, CustomDeviceKind>
        {
            [new TileCoord(1, 1)] = CustomDeviceKind.Reactor,
            [new TileCoord(2, 1)] = CustomDeviceKind.Distribution,
            [new TileCoord(3, 1)] = CustomDeviceKind.Helm,
            [new TileCoord(4, 1)] = CustomDeviceKind.Navigation,
            [new TileCoord(5, 1)] = CustomDeviceKind.Oxygen,
            [new TileCoord(1, 2)] = CustomDeviceKind.SuitLocker,
            [new TileCoord(2, 2)] = CustomDeviceKind.StorageRack,
        };
        foreach (var (coord, kind) in deviceKinds)
            tiles.PlaceDevice(coord, kind.ToString());

        var engineControl = new TileCoord(5, 2);
        tiles.PlaceDevice(engineControl, "engine");
        var engines = new Dictionary<TileCoord, TileShipBuilder.EngineSpec> { [engineControl] = new(TileSide.South, 10f) };

        // The wide door itself - 2 parallel edges sharing one id, well away from every device above.
        tiles.AddDoorEdge(new TileCoord(3, 6), TileSide.East, "wide-door-1");
        tiles.AddDoorEdge(new TileCoord(3, 7), TileSide.East, "wide-door-1");

        var (definition, errors) = TileShipBuilder.BuildDefinition(tiles, deviceKinds, engines, "wide-door-ship", 0f);
        if (definition is null)
            throw new InvalidOperationException("setup problem: " + string.Join("; ", errors));
        if (definition.DoorEdges.Count != 2 || definition.DoorEdges.Any(e => e.Id != "wide-door-1"))
            return false; // setup problem - export didn't carry both segments through with the shared id

        var world = new World(ShipKind.Custom, definition);
        if (world.Ship.DoorEdges.Count != 2 || world.Ship.DoorEdges.Any(e => e.Id != "wide-door-1"))
            return false;
        world.Step(0.01);

        var seg1 = new TileCoord(3, 6);
        var seg2 = new TileCoord(3, 7);
        // Interior edges default open (same as a narrow one) - both segments together, from one id.
        if (world.Ship.Tiles.DoorEdgeAt(seg1, TileSide.East) is not { Open: true })
            return false;
        if (world.Ship.Tiles.DoorEdgeAt(seg2, TileSide.East) is not { Open: true })
            return false;

        var openStates = world.CreateSnapshot().DoorEdgeStates;
        // One shared id -> ONE state entry, not two - _doorEdgeOpen is keyed by Id, exactly the same
        // "one door, several segments" collapse the client-side rendering/hitbox loops rely on.
        if (openStates is not { Count: 1 } || openStates[0].Id != "wide-door-1" || !openStates[0].IsOpen)
            return false;

        world.ToggleDoor("wide-door-1");
        world.Step(0.01);
        // BOTH segments must have moved together from the single toggle - a half-closed wide door
        // (one segment open, the other still shut) would let a corner-sampled character partially
        // clip through where TileMovement.IsClear checks the two crossings independently.
        return world.Ship.Tiles.DoorEdgeAt(seg1, TileSide.East) is { Open: false }
            && world.Ship.Tiles.DoorEdgeAt(seg2, TileSide.East) is { Open: false };
    }

    // Direct user request ("сделай тоже самое с тройной дверью, чтобы она занимала 2 на 3 тайла") -
    // same "several parallel edges sharing one Id" trick the wide door already proved out, just 3
    // segments instead of 2 (PlaceEdgeDoor's own doc comment - the count is fully generic). Confirms
    // the region only actually splits once ALL THREE stand, same reasoning as the wide door's own
    // test - each still-open crossing keeps the two sides connected until the very last one closes.
    private static bool TileGrid_DoorEdge_TripleDoor_AllThreeParallelEdgesNeededToSplitRegion()
    {
        var grid = new TileGrid();
        // A 2x3 free-floor block: column A = (0,0)/(0,1)/(0,2), column B = (1,0)/(1,1)/(1,2).
        var a1 = new TileCoord(0, 0);
        var a2 = new TileCoord(0, 1);
        var a3 = new TileCoord(0, 2);
        var b1 = new TileCoord(1, 0);
        foreach (var c in new[] { a1, a2, a3, b1, new TileCoord(1, 1), new TileCoord(1, 2) })
            grid.SetFloor(c, true);
        if (grid.Regions.Count != 1)
            return false;

        grid.AddDoorEdge(a1, TileSide.East, "triple-door-1");
        grid.AddDoorEdge(a2, TileSide.East, "triple-door-1");
        // Still one region - the a3/b3 crossing is still wide open.
        if (grid.Regions.Count != 1 || grid.RegionIdAt(a1) != grid.RegionIdAt(b1))
            return false;

        grid.AddDoorEdge(a3, TileSide.East, "triple-door-1");
        // All three crossings closed now - the region must actually split.
        return grid.Regions.Count == 2 && grid.RegionIdAt(a1) != grid.RegionIdAt(b1);
    }

    // Same 2x3 shape, exercised through the real Ship/World pipeline - 3 CustomDoorEdgeDef entries
    // sharing one Id (exactly what TileShipBuilder's export would produce for a triple door the
    // editor placed). Mirrors World_DoorEdge_WideDoor_TwoSegmentsShareIdAndToggleTogether exactly,
    // just with a 3rd segment - one CreateDoorEdgeStates entry for the shared id regardless of how
    // many physical edges carry it, and toggling that one id moves all three at once.
    private static bool World_DoorEdge_TripleDoor_ThreeSegmentsShareIdAndToggleTogether()
    {
        var tiles = new TileGrid();
        for (var y = 0; y < 11; y++)
            for (var x = 0; x <= 7; x++)
                tiles.SetFloor(new TileCoord(x, y), true);

        tiles.SetFloor(new TileCoord(3, -1), true);
        tiles.SetWall(new TileCoord(3, -1), TileWallKind.Door);

        var deviceKinds = new Dictionary<TileCoord, CustomDeviceKind>
        {
            [new TileCoord(1, 1)] = CustomDeviceKind.Reactor,
            [new TileCoord(2, 1)] = CustomDeviceKind.Distribution,
            [new TileCoord(3, 1)] = CustomDeviceKind.Helm,
            [new TileCoord(4, 1)] = CustomDeviceKind.Navigation,
            [new TileCoord(5, 1)] = CustomDeviceKind.Oxygen,
            [new TileCoord(1, 2)] = CustomDeviceKind.SuitLocker,
            [new TileCoord(2, 2)] = CustomDeviceKind.StorageRack,
        };
        foreach (var (coord, kind) in deviceKinds)
            tiles.PlaceDevice(coord, kind.ToString());

        var engineControl = new TileCoord(5, 2);
        tiles.PlaceDevice(engineControl, "engine");
        var engines = new Dictionary<TileCoord, TileShipBuilder.EngineSpec> { [engineControl] = new(TileSide.South, 10f) };

        // The triple door itself - 3 parallel edges sharing one id, well away from every device above.
        tiles.AddDoorEdge(new TileCoord(3, 6), TileSide.East, "triple-door-1");
        tiles.AddDoorEdge(new TileCoord(3, 7), TileSide.East, "triple-door-1");
        tiles.AddDoorEdge(new TileCoord(3, 8), TileSide.East, "triple-door-1");

        var (definition, errors) = TileShipBuilder.BuildDefinition(tiles, deviceKinds, engines, "triple-door-ship", 0f);
        if (definition is null)
            throw new InvalidOperationException("setup problem: " + string.Join("; ", errors));
        if (definition.DoorEdges.Count != 3 || definition.DoorEdges.Any(e => e.Id != "triple-door-1"))
            return false; // setup problem - export didn't carry all three segments through with the shared id

        var world = new World(ShipKind.Custom, definition);
        if (world.Ship.DoorEdges.Count != 3 || world.Ship.DoorEdges.Any(e => e.Id != "triple-door-1"))
            return false;
        world.Step(0.01);

        var seg1 = new TileCoord(3, 6);
        var seg2 = new TileCoord(3, 7);
        var seg3 = new TileCoord(3, 8);
        if (world.Ship.Tiles.DoorEdgeAt(seg1, TileSide.East) is not { Open: true })
            return false;
        if (world.Ship.Tiles.DoorEdgeAt(seg2, TileSide.East) is not { Open: true })
            return false;
        if (world.Ship.Tiles.DoorEdgeAt(seg3, TileSide.East) is not { Open: true })
            return false;

        var openStates = world.CreateSnapshot().DoorEdgeStates;
        if (openStates is not { Count: 1 } || openStates[0].Id != "triple-door-1" || !openStates[0].IsOpen)
            return false;

        world.ToggleDoor("triple-door-1");
        world.Step(0.01);
        return world.Ship.Tiles.DoorEdgeAt(seg1, TileSide.East) is { Open: false }
            && world.Ship.Tiles.DoorEdgeAt(seg2, TileSide.East) is { Open: false }
            && world.Ship.Tiles.DoorEdgeAt(seg3, TileSide.East) is { Open: false };
    }
}
