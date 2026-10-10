using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Server;
using Anabiosis.Shared.Model;

internal static partial class TestRunner
{
    // Direct user request: doors on the junction of two compartments (or a compartment wall against
    // open space), only where the wall is half-block and carries on past both sides of the door. See
    // JunctionDoor.cs. Pure TileGrid tests - two 5x5 compartments side by side (A = x0..4, B = x5..9,
    // y0..4), each with its own full wall ring, so they touch in a 2-tile-thick double wall at x=4/x=5.
    private static TileGrid JunctionTwoCompartments(bool halfBlock = true)
    {
        var g = new TileGrid();
        for (var x = 0; x < 10; x++)
            for (var y = 0; y < 5; y++)
                g.SetFloor(new TileCoord(x, y), true);
        for (var x = 0; x < 10; x++)
            for (var y = 0; y < 5; y++)
            {
                var lx = x < 5 ? x : x - 5;
                if (lx == 0 || lx == 4 || y == 0 || y == 4)
                    g.SetWall(new TileCoord(x, y), TileWallKind.Solid);
            }
        if (halfBlock)
            for (var y = 1; y <= 3; y++)
            {
                g.SetWallOpenSide(new TileCoord(4, y), TileSide.West);
                g.SetWallOpenSide(new TileCoord(5, y), TileSide.East);
            }
        return g;
    }

    private static bool IsOpenFloor(TileGrid g, int x, int y) =>
        g.CellAt(new TileCoord(x, y)) is { HasFloor: true, Wall: TileWallKind.None };

    private static bool JunctionDoor_AcrossDoubleHalfBlockWall_ClearsBothLayersAndDoorsTheMiddle()
    {
        var g = JunctionTwoCompartments();
        var plan = JunctionDoor.Plan(g, new TileCoord(4, 2), TileSide.East, 1, out _);
        if (plan is null || plan.WallTiles.Count != 2 || plan.Edges.Count != 1)
            return false;
        if (JunctionDoor.Apply(g, plan, "jd-1") is null)
            return false;
        // Both layers are floor now, the door edge sits between them, the rest of the wall is intact.
        return IsOpenFloor(g, 4, 2) && IsOpenFloor(g, 5, 2)
            && g.DoorEdgeAt(new TileCoord(4, 2), TileSide.East)?.Id == "jd-1"
            && g.CellAt(new TileCoord(4, 1)) is { Wall: TileWallKind.Solid }
            && g.CellAt(new TileCoord(5, 3)) is { Wall: TileWallKind.Solid };
    }

    private static bool JunctionDoor_WideAndTriple_CoverTheirWholeSpan()
    {
        var wide = JunctionTwoCompartments();
        var widePlan = JunctionDoor.Plan(wide, new TileCoord(4, 2), TileSide.East, 2, out _);
        var triple = JunctionTwoCompartments();
        var triplePlan = JunctionDoor.Plan(triple, new TileCoord(5, 1), TileSide.East, 3, out _);
        if (widePlan is null || widePlan.WallTiles.Count != 4 || widePlan.Edges.Count != 2)
            return false;
        if (triplePlan is null || triplePlan.WallTiles.Count != 6 || triplePlan.Edges.Count != 3)
            return false;
        JunctionDoor.Apply(triple, triplePlan, "jd-3");
        return Enumerable.Range(1, 3).All(y => IsOpenFloor(triple, 4, y) && IsOpenFloor(triple, 5, y)
            && triple.DoorEdgeAt(new TileCoord(4, y), TileSide.East) is not null);
    }

    private static bool JunctionDoor_FullThicknessWall_IsRefused()
    {
        var g = JunctionTwoCompartments(halfBlock: false);
        var plan = JunctionDoor.Plan(g, new TileCoord(4, 2), TileSide.East, 1, out var failure);
        return plan is null && failure == JunctionDoorFailure.NotHalfBlock;
    }

    private static bool JunctionDoor_SpanReachingAFullWallTile_IsRefused()
    {
        // Rows 1-4 would take in row 4, the full-thickness corner of the ring.
        var g = JunctionTwoCompartments();
        var plan = JunctionDoor.Plan(g, new TileCoord(4, 1), TileSide.East, 4, out var failure);
        return plan is null && failure == JunctionDoorFailure.NotEveryTileHalfBlock;
    }

    private static bool JunctionDoor_WithoutWallOnBothSides_IsRefused()
    {
        // A door is a gap in a wall, never freestanding: knock out the wall right above the span.
        var g = JunctionTwoCompartments();
        g.SetWall(new TileCoord(4, 1), TileWallKind.None);
        g.SetWall(new TileCoord(5, 1), TileWallKind.None);
        var plan = JunctionDoor.Plan(g, new TileCoord(4, 2), TileSide.East, 1, out var failure);
        return plan is null && failure == JunctionDoorFailure.NoSideWall;
    }

    private static bool JunctionDoor_WrongOrientation_IsRefused()
    {
        // Passage South runs along the wall instead of through it: a long run of half-block tiles.
        var g = JunctionTwoCompartments();
        return JunctionDoor.Plan(g, new TileCoord(4, 2), TileSide.South, 1, out var failure) is null
            && failure != JunctionDoorFailure.None;
    }

    private static bool JunctionDoor_AWallDeviceOnTheWall_IsRefused()
    {
        var g = JunctionTwoCompartments();
        g.CellAt(new TileCoord(4, 2))!.WallDeviceId = "lamp-1";
        var plan = JunctionDoor.Plan(g, new TileCoord(4, 2), TileSide.East, 1, out var failure);
        return plan is null && failure == JunctionDoorFailure.WallDevice;
    }

    private static bool JunctionDoor_AgainstOpenSpace_ReplacesTheOneWallTileAndDoorsItsOuterEdge()
    {
        // One 5x5 compartment, half-block east wall, nothing beyond it.
        var g = new TileGrid();
        for (var x = 0; x < 5; x++)
            for (var y = 0; y < 5; y++)
                g.SetFloor(new TileCoord(x, y), true);
        for (var x = 0; x < 5; x++)
            for (var y = 0; y < 5; y++)
                if (x == 0 || x == 4 || y == 0 || y == 4)
                    g.SetWall(new TileCoord(x, y), TileWallKind.Solid);
        for (var y = 1; y <= 3; y++)
            g.SetWallOpenSide(new TileCoord(4, y), TileSide.West);

        var plan = JunctionDoor.Plan(g, new TileCoord(4, 2), TileSide.East, 1, out _);
        if (plan is null || plan.WallTiles.Count != 1)
            return false;
        JunctionDoor.Apply(g, plan, "jd-air");
        return IsOpenFloor(g, 4, 2) && g.DoorEdgeAt(new TileCoord(4, 2), TileSide.East)?.Id == "jd-air"
            && g.CellAt(new TileCoord(5, 2)) is null;
    }

    private static bool JunctionDoor_RemovingIt_PutsTheHalfBlockWallsBack()
    {
        var g = JunctionTwoCompartments();
        var plan = JunctionDoor.Plan(g, new TileCoord(4, 2), TileSide.East, 2, out _)!;
        var saved = JunctionDoor.Apply(g, plan, "jd-r")!;
        foreach (var (coord, side) in plan.Edges)
            g.RemoveDoorEdge(coord, side);
        JunctionDoor.Restore(g, saved);

        return new[] { 4, 5 }.SelectMany(x => new[] { 2, 3 }.Select(y => (x, y))).All(t =>
            g.CellAt(new TileCoord(t.x, t.y)) is { Wall: TileWallKind.Solid, WallOpenSide: { } open }
            && open == (t.x == 4 ? TileSide.West : TileSide.East))
            && g.DoorEdges.Count == 0;
    }

    private static bool JunctionDoor_ExportsAsTwoRoomsJoinedByOneDoor()
    {
        var g = JunctionTwoCompartments();
        var plan = JunctionDoor.Plan(g, new TileCoord(4, 2), TileSide.East, 2, out _)!;
        JunctionDoor.Apply(g, plan, "jd-x");
        var (definition, errors) = TileShipBuilder.BuildDefinition(
            g, new Dictionary<TileCoord, CustomDeviceKind>(), new Dictionary<TileCoord, TileShipBuilder.EngineSpec>(), "Test Ship", 0f);
        return definition is not null && errors.Count == 0 && definition.Rooms.Count == 2
            && definition.DoorEdges.Count == 2 && definition.DoorEdges.All(e => e.Id == "jd-x");
    }

    // The runtime half: the exported ship must really have an opening where the walls were - walkable
    // from one compartment into the other once the door is open, blocked while it is closed.
    private static bool World_JunctionDoor_TwoCompartmentsConnectThroughTheDoorWhenOpen()
    {
        var tiles = JunctionTwoCompartments();
        var deviceKinds = new Dictionary<TileCoord, CustomDeviceKind>
        {
            [new TileCoord(1, 1)] = CustomDeviceKind.Reactor,
            [new TileCoord(2, 1)] = CustomDeviceKind.Distribution,
            [new TileCoord(3, 1)] = CustomDeviceKind.Helm,
            [new TileCoord(1, 2)] = CustomDeviceKind.Navigation,
            [new TileCoord(2, 2)] = CustomDeviceKind.Oxygen,
            [new TileCoord(3, 2)] = CustomDeviceKind.SuitLocker,
            [new TileCoord(2, 3)] = CustomDeviceKind.StorageRack,
        };
        foreach (var (coord, kind) in deviceKinds)
            tiles.PlaceDevice(coord, kind.ToString());
        var engineControl = new TileCoord(1, 3);
        tiles.PlaceDevice(engineControl, "engine");
        var engines = new Dictionary<TileCoord, TileShipBuilder.EngineSpec> { [engineControl] = new(TileSide.West, 10f) };

        var plan = JunctionDoor.Plan(tiles, new TileCoord(4, 2), TileSide.East, 2, out _)!;
        JunctionDoor.Apply(tiles, plan, "jd-live");

        var (definition, errors) = TileShipBuilder.BuildDefinition(tiles, deviceKinds, engines, "junction-ship", 0f);
        if (definition is null)
            throw new InvalidOperationException("setup problem: " + string.Join("; ", errors));

        var world = new World(ShipKind.Custom, definition);
        world.Step(0.01);
        var left = new TileCoord(4, 2);
        var right = new TileCoord(5, 2);
        if (world.Ship.Tiles.CellAt(left) is not { HasFloor: true, Wall: TileWallKind.None } ||
            world.Ship.Tiles.CellAt(right) is not { HasFloor: true, Wall: TileWallKind.None })
            return false; // the opening must be real floor in the live ship, not a wall the room rasterizer put back
        if (world.Ship.Tiles.DoorEdgeAt(left, TileSide.East) is not { Open: false })
            return false; // closed to begin with
        if (world.Ship.Tiles.IsWalkableAcrossEdge(left, right))
            return false;

        world.ToggleDoor("jd-live");
        world.Step(0.01);
        return world.Ship.Tiles.IsWalkableAcrossEdge(left, right)
            && world.Ship.Tiles.IsWalkableAcrossEdge(new TileCoord(4, 3), new TileCoord(5, 3));
    }
}
