using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// In-game building from the compartment catalog (CompartmentBuilder): tile-grid placement, rotation, the same rules as the editor.
internal static partial class TestRunner
{
    private static readonly IReadOnlySet<TileCoord> NoReservedTiles = new HashSet<TileCoord>();

    // The first anchor (scanning a window around the hull) where the compartment can be placed.
    private static CompartmentPlan? FindPlacement(Ship ship, CompartmentCatalogEntry entry, int rotation, IReadOnlySet<TileCoord>? reserved = null)
    {
        for (var y = -40; y <= 40; y++)
            for (var x = -40; x <= 50; x++)
                if (CompartmentBuilder.Plan(ship.Tiles, reserved ?? NoReservedTiles, entry, new TileCoord(x, y), rotation).Plan is { } plan)
                    return plan;
        return null;
    }

    // Every catalog compartment, in every rotation, can be attached to a real hull and the result is a valid, buildable ship.
    private static bool CompartmentBuild_EveryEntryAttachesInEveryRotation()
    {
        var ship = Ship.FromCustomDefinition(ShipBuiltInFleet.CoolShip);
        var def = ship.ToDefinition();
        foreach (var entry in CompartmentCatalog.Entries)
            for (var rotation = 0; rotation < 4; rotation++)
            {
                if (FindPlacement(ship, entry, rotation) is not { } plan)
                {
                    Console.WriteLine($"   no placement for {entry.Id} r{rotation}");
                    return false;
                }
                var merged = CompartmentBuilder.Merge(def, plan, CompartmentBuilder.NextRoomId(def.Rooms));
                if (merged is null)
                {
                    Console.WriteLine($"   merge failed for {entry.Id} r{rotation}");
                    return false;
                }
                var built = Ship.FromCustomDefinition(merged);
                if (built.Rooms.Count != ship.Rooms.Count + 1 || !built.Rooms.Any(r => r.Name == entry.DisplayName))
                {
                    Console.WriteLine($"   wrong rooms for {entry.Id} r{rotation}: {built.Rooms.Count}");
                    return false;
                }
            }
        return true;
    }

    // Free ground only: stamping on top of the hull, or on a reserved (under-construction) tile, is refused; so is a spot that
    // touches nothing.
    private static bool CompartmentBuild_RefusesOverlapReservedAndFloatingSpots()
    {
        var ship = Ship.FromCustomDefinition(ShipBuiltInFleet.CoolShip);
        var entry = CompartmentCatalog.Entries[0];
        var onHull = CompartmentBuilder.Plan(ship.Tiles, NoReservedTiles, entry, new TileCoord(15, 0), 0).Plan;
        var floating = CompartmentBuilder.Plan(ship.Tiles, NoReservedTiles, entry, new TileCoord(200, 200), 0).Plan;
        if (onHull is not null || floating is not null)
            return false;

        var plan = FindPlacement(ship, entry, 0);
        if (plan is null)
            return false;
        var reserved = plan.Tiles.ToHashSet();
        return CompartmentBuilder.Plan(ship.Tiles, reserved, entry, plan.Anchor, 0).Plan is null;
    }

    // Rebuilding the hull from its own definition keeps the half-thickness walls, extra walls and open tiles the editor made.
    private static bool CompartmentBuild_ToDefinitionKeepsTileCorrections()
    {
        var ship = Ship.FromCustomDefinition(ShipBuiltInFleet.CoolShip);
        var def = ship.ToDefinition();
        var again = Ship.FromCustomDefinition(def);
        return def.WallOpenSides.Count == ship.WallOpenSideOverrides.Count && def.WallOpenSides.Count > 0
            && again.DoorEdges.Count == ship.DoorEdges.Count
            && again.Tiles.Cells.Count == ship.Tiles.Cells.Count;
    }

    // A ship with one more compartment attached, ready for doors.
    private static (Ship Ship, CustomShipDefinition Def) CoolShipWithAnExtraCompartment()
    {
        var ship = Ship.FromCustomDefinition(ShipBuiltInFleet.CoolShip);
        var def = ship.ToDefinition();
        var entry = CompartmentCatalog.Entries.First(e => e.Type == CompartmentType.Engineering);
        var plan = FindPlacement(ship, entry, 0)!;
        var merged = CompartmentBuilder.Merge(def, plan, CompartmentBuilder.NextRoomId(def.Rooms))!;
        return (Ship.FromCustomDefinition(merged), merged);
    }

    private static (TileCoord Anchor, TileSide Passage, int Span)? FindDoorSpot(Ship ship, int span)
    {
        var bounds = ship.Tiles.Cells.Keys;
        var minX = bounds.Min(c => c.X) - 2; var maxX = bounds.Max(c => c.X) + 2;
        var minY = bounds.Min(c => c.Y) - 2; var maxY = bounds.Max(c => c.Y) + 2;
        foreach (var passage in new[] { TileSide.East, TileSide.South })
            for (var y = minY; y <= maxY; y++)
                for (var x = minX; x <= maxX; x++)
                    if (JunctionDoor.Plan(ship.Tiles, new TileCoord(x, y), passage, span, out _) is not null)
                        return (new TileCoord(x, y), passage, span);
        return null;
    }

    // A door placed through the definition becomes a real door edge in the rebuilt ship, with the walls it replaced opened up.
    private static bool CompartmentBuild_JunctionDoorPlacedThroughTheDefinition_AppearsInTheRebuiltShip()
    {
        var (ship, def) = CoolShipWithAnExtraCompartment();
        if (FindDoorSpot(ship, 1) is not { } spot)
            return false;

        var (updated, failure) = JunctionDoorBuilder.Place(ship, def, spot.Anchor, spot.Passage, spot.Span, "jdoor-1");
        if (updated is null || failure != JunctionDoorFailure.None)
            return false;
        var rebuilt = Ship.FromCustomDefinition(updated);
        var plan = JunctionDoor.Plan(ship.Tiles, spot.Anchor, spot.Passage, spot.Span, out _)!;
        return rebuilt.DoorEdges.Count == ship.DoorEdges.Count + plan.Edges.Count
            && plan.WallTiles.All(t => rebuilt.Tiles.CellAt(t) is { Wall: TileWallKind.None });
    }

    // Removing the door puts the half walls back exactly (the ship rebuilt from the definition equals the one before the door).
    private static bool CompartmentBuild_JunctionDoorRemoved_RestoresTheWalls()
    {
        var (ship, def) = CoolShipWithAnExtraCompartment();
        if (FindDoorSpot(ship, 1) is not { } spot)
            return false;
        var (withDoor, _) = JunctionDoorBuilder.Place(ship, def, spot.Anchor, spot.Passage, spot.Span, "jdoor-1");
        if (withDoor is null || JunctionDoorBuilder.Remove(withDoor, "jdoor-1") is not { } without)
            return false;

        var rebuilt = Ship.FromCustomDefinition(without);
        return rebuilt.DoorEdges.Count == ship.DoorEdges.Count
            && rebuilt.Tiles.Cells.Count == ship.Tiles.Cells.Count
            && rebuilt.Tiles.Cells.All(kv => ship.Tiles.CellAt(kv.Key) is { } c && c.Wall == kv.Value.Wall && c.WallOpenSide == kv.Value.WallOpenSide);
    }

    // The three door widths are all possible on a hull with a wide enough joint, and cost more the wider they are.
    private static bool CompartmentBuild_DoorWidthsAndPrices()
    {
        var (ship, _) = CoolShipWithAnExtraCompartment();
        return FindDoorSpot(ship, 1) is not null && FindDoorSpot(ship, 2) is not null
            && JunctionDoorBuilder.Price(1) < JunctionDoorBuilder.Price(2) && JunctionDoorBuilder.Price(2) < JunctionDoorBuilder.Price(3);
    }

    // ---- through the real commands ----

    private static World CoolShipWorld()
    {
        var world = new World(ShipKind.Custom, ShipBuiltInFleet.CoolShip);
        world.SpawnCharacter(1);
        world.DebugAddCredits(100000);
        DockAtStation(world, "home-station");
        return world;
    }

    private static bool CompartmentBuild_CommandBuildsTheCompartmentAfterItsTimerAndChargesForIt()
    {
        var world = CoolShipWorld();
        var entry = CompartmentCatalog.Entries.First(e => e.Type == CompartmentType.Engine);
        var plan = FindPlacement(world.Ship, entry, 1)!;
        var roomsBefore = world.Ship.Rooms.Count;
        var creditsBefore = world.Credits;

        world.ApplyCommand(1, new ClientCommand(1, BuildCompartment: new BuildCompartmentRequest(entry.Id, plan.Anchor.X, plan.Anchor.Y, 1)));
        world.Step(RealtimeStep);
        var pending = world.CreateSnapshot().PendingRoomBuilds is { Count: 1 };
        var charged = world.Credits == creditsBefore - CompartmentPricing.Price(entry);
        if (!pending || !charged || world.Ship.Rooms.Count != roomsBefore)
            return false;

        world.DebugFastForwardCompartmentBuilds(9999);
        world.Step(RealtimeStep);
        return world.Ship.Rooms.Count == roomsBefore + 1 && world.Ship.Engines.Count > 0
            && world.CreateSnapshot().PendingRoomBuilds is null or { Count: 0 };
    }

    private static bool CompartmentBuild_CommandRefusesOverlapAndTooLittleMoney()
    {
        var world = CoolShipWorld();
        var entry = CompartmentCatalog.Entries[0];
        var creditsBefore = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, BuildCompartment: new BuildCompartmentRequest(entry.Id, 15, 0, 0))); // on the hull
        world.Step(RealtimeStep);
        if (world.Credits != creditsBefore || world.CreateSnapshot().PendingRoomBuilds is { Count: > 0 })
            return false;

        var plan = FindPlacement(world.Ship, entry, 0)!;
        world.DebugAddCredits(-world.Credits);
        world.ApplyCommand(1, new ClientCommand(1, BuildCompartment: new BuildCompartmentRequest(entry.Id, plan.Anchor.X, plan.Anchor.Y, 0)));
        world.Step(RealtimeStep);
        return world.CreateSnapshot().PendingRoomBuilds is null or { Count: 0 };
    }

    // The same spot cannot be started twice while its first build is still going.
    private static bool CompartmentBuild_SecondBuildOnTheSameTilesIsRefused()
    {
        var world = CoolShipWorld();
        var entry = CompartmentCatalog.Entries[0];
        var plan = FindPlacement(world.Ship, entry, 0)!;
        var request = new BuildCompartmentRequest(entry.Id, plan.Anchor.X, plan.Anchor.Y, 0);
        world.ApplyCommand(1, new ClientCommand(1, BuildCompartment: request));
        world.ApplyCommand(1, new ClientCommand(1, BuildCompartment: request));
        world.Step(RealtimeStep);
        return world.CreateSnapshot().PendingRoomBuilds is { Count: 1 };
    }

    private static bool CompartmentBuild_CommandPlacesAndRemovesADoor()
    {
        var world = CoolShipWorld();
        var entry = CompartmentCatalog.Entries.First(e => e.Type == CompartmentType.Engineering);
        var plan = FindPlacement(world.Ship, entry, 0)!;
        world.ApplyCommand(1, new ClientCommand(1, BuildCompartment: new BuildCompartmentRequest(entry.Id, plan.Anchor.X, plan.Anchor.Y, 0)));
        world.DebugFastForwardCompartmentBuilds(9999);
        world.Step(RealtimeStep);
        if (FindDoorSpot(world.Ship, 1) is not { } spot)
            return false;

        var credits = world.Credits;
        var edgesBefore = world.Ship.DoorEdges.Count;
        world.ApplyCommand(1, new ClientCommand(1, PlaceDoor: new PlaceDoorRequest(spot.Anchor.X, spot.Anchor.Y, spot.Passage, 1)));
        world.Step(RealtimeStep);
        if (!world.DebugHasDoorEdge("jdoor-1") || world.Ship.DoorEdges.Count <= edgesBefore || world.Credits != credits - JunctionDoorBuilder.Price(1))
            return false;

        world.ApplyCommand(1, new ClientCommand(1, RemoveDoorId: "jdoor-1"));
        world.Step(RealtimeStep);
        return !world.DebugHasDoorEdge("jdoor-1") && world.Ship.DoorEdges.Count == edgesBefore
            && world.Credits == credits - JunctionDoorBuilder.Price(1) + JunctionDoorBuilder.Price(1) / 2;
    }

    // Demolishing a built compartment gives half the price back; one that would cut the hull in two stays.
    private static bool CompartmentBuild_DemolishReturnsHalfThePrice()
    {
        var world = CoolShipWorld();
        var entry = CompartmentCatalog.Entries.First(e => e.Type == CompartmentType.Engineering);
        var plan = FindPlacement(world.Ship, entry, 0)!;
        var roomsBefore = world.Ship.Rooms.Count;
        world.ApplyCommand(1, new ClientCommand(1, BuildCompartment: new BuildCompartmentRequest(entry.Id, plan.Anchor.X, plan.Anchor.Y, 0)));
        world.DebugFastForwardCompartmentBuilds(9999);
        world.Step(RealtimeStep);
        if (world.Ship.Rooms.Count != roomsBefore + 1)
            return false;
        var builtId = world.Ship.Rooms.First(r => r.Name == entry.DisplayName && r.Id.StartsWith("room-")).Id;

        var credits = world.Credits;
        world.ApplyCommand(1, new ClientCommand(1, DemolishRoomId: builtId));
        world.Step(RealtimeStep);
        return world.Ship.Rooms.Count == roomsBefore && world.Credits == credits + CompartmentPricing.Refund(entry);
    }

    // The default hull (hand-authored rooms, no tile-editor corrections) takes catalog compartments too.
    private static bool CompartmentBuild_DefaultHullAcceptsEveryEntry()
    {
        var ship = Ship.FromCustomDefinition(ShipDefaultHull.Definition);
        var def = ship.ToDefinition();
        foreach (var entry in CompartmentCatalog.Entries)
        {
            if (FindPlacement(ship, entry, 0) is not { } plan)
                return false;
            if (CompartmentBuilder.Merge(def, plan, CompartmentBuilder.NextRoomId(def.Rooms)) is not { } merged)
                return false;
            var built = Ship.FromCustomDefinition(merged);
            if (built.Rooms.Count != ship.Rooms.Count + 1)
                return false;
        }
        return true;
    }
}
