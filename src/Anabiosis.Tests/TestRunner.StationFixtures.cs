using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// The devices the author drew into the start outpost (Station.Fixtures.cs): carried over from the drawing, turned with the station, solid, shown
// in the snapshot while docked, and the workshop ones work.
internal static partial class TestRunner
{
    private static bool StationFixtures_EveryDrawnDeviceIsThereOnEverySide()
    {
        foreach (var side in new[] { EdgeSide.Right, EdgeSide.Left, EdgeSide.Top, EdgeSide.Bottom })
        {
            var station = StartStationAt(new Vec2(30, 20), side);
            var f = station.Fixtures;
            // The counts are the drawing's own (the start station is a copy of the saved design "аванпост1", whatever it holds now).
            var drawn = Ship.FromCustomDefinition((CustomShipDefinition)typeof(Station)
                .GetProperty("OutpostStartDefinition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!, validate: false);
            if (f.WallLamps.Count != drawn.WallLamps.Count || f.Terminals.Count != drawn.Terminals.Count || f.JunctionBoxes.Count != drawn.JunctionBoxes.Count
                || f.DecorativeDevices.Count != drawn.DecorativeDevices.Count || f.CommsConsoles.Count != drawn.CommsConsoles.Count
                || f.Reactors.Count != 1 + drawn.ExtraReactorBlocks.Count || f.Batteries.Count != 1 + drawn.ExtraBatteryBlocks.Count
                || f.Distributions.Count != 1 + drawn.ExtraDistributionBlocks.Count || f.Navigations.Count != 1 + drawn.ExtraNavigationConsoles.Count
                || f.Helms.Count != 1 + drawn.ExtraHelmConsoles.Count || f.WallLamps.Count == 0 || f.DecorativeDevices.Count == 0)
                return false;
            // Every fixture sits in (or against the wall of) a room of the station - none was left behind in the drawing's own frame.
            bool InStation(string roomId, Vec2 p) =>
                station.Rooms.FirstOrDefault(r => r.Id == roomId) is { } room &&
                room.Rects.Any(q => p.X >= q.Left - 1.5 && p.X <= q.Right + 1.5 && p.Y >= q.Top - 1.5 && p.Y <= q.Bottom + 1.5);
            if (!f.WallLamps.All(l => InStation(l.RoomId, l.Position)) || !f.Terminals.All(t => InStation(t.RoomId, t.Position))
                || !f.DecorativeDevices.All(d => InStation(d.RoomId, d.Position)) || !f.Reactors.All(r => InStation(r.RoomId, r.Position))
                || !f.Helms.All(h => InStation(h.RoomId, h.Position)))
                return false;
        }
        return true;
    }

    // Solid: nobody walks through the reactor or the fabricator.
    private static bool StationFixtures_AreSolid()
    {
        var station = StartStationAt(new Vec2(30, 20), EdgeSide.Right);
        var fabricator = station.Fixtures.DecorativeDevices.First(d => d.Kind == CustomDeviceKind.Fabricator);
        return ShipDeviceOccupancy.OverlapsDevice(station.Tiles, fabricator.Position)
            && ShipDeviceOccupancy.OverlapsDevice(station.Tiles, station.Fixtures.Reactors[0].Position);
    }

    // They join the ship's lists in the snapshot while docked, and go again once the ship casts off.
    private static bool StationFixtures_AreInTheSnapshotOnlyWhileDocked()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var docked = world.CreateSnapshot();
        var hasWhileDocked = docked.DecorativeDevices!.Any(d => d.Kind == CustomDeviceKind.Fabricator && d.Id.StartsWith("home-station"))
            && docked.WallLamps!.Any(l => l.Id.StartsWith("home-station")) && docked.Terminals!.Any(t => t.Block.Id.StartsWith("home-station"));
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        var away = world.CreateSnapshot();
        return world.IsDocked == false && hasWhileDocked
            && away.DecorativeDevices!.All(d => !d.Id.StartsWith("home-station")) && away.WallLamps!.All(l => !l.Id.StartsWith("home-station"));
    }

    // A fabricator drawn into the station is a working fabricator.
    private static bool StationFixtures_FabricatorOnTheStationCrafts()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var fabricator = world.CreateSnapshot().DecorativeDevices!.First(d => d.Kind == CustomDeviceKind.Fabricator && d.Id.StartsWith("home-station"));
        world.DebugGiveItem(1, ItemType.IronOre);
        world.DebugGiveItem(1, ItemType.IronOre);
        world.DebugGiveItem(1, ItemType.AluminumOre);
        world.DebugPlaceCharacterOnStation(1, fabricator.Position);
        world.ApplyCommand(1, new ClientCommand(1, FabricatorCraftRecipeId: "steel-plate"));
        StepPastProductionTimer(world);
        var inventory = world.CreateSnapshot().Characters.Single(c => c.PlayerId == 1).Inventory!;
        return inventory.MainSlots.Count(s => s == ItemType.SteelPlate) == 1 && inventory.MainSlots.Count(s => s == ItemType.IronOre) == 0;
    }
}

internal static partial class TestRunner
{
    // The half-block walls and partitions of the drawing are all on the station, whichever way it is turned (a wall shared by two compartments lands
    // on a different tile once the rasterizer sees the station turned - the station's tiles are corrected to the drawn grid itself).
    private static bool StationFixtures_HalfWallsMatchTheDrawingOnEverySide()
    {
        var def = (CustomShipDefinition)typeof(Station)
            .GetProperty("OutpostStartDefinition", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null)!;
        var drawn = Ship.FromCustomDefinition(def, validate: false).Tiles.Cells.Values;
        var drawnHalf = drawn.Count(c => c.Wall == TileWallKind.Solid && c.WallOpenSide is not null);
        var drawnSolid = drawn.Count(c => c.Wall == TileWallKind.Solid);
        if (drawnHalf < 20)
            return false; // setup problem - the drawing has plenty of half-block walls
        foreach (var side in new[] { EdgeSide.Right, EdgeSide.Left, EdgeSide.Top, EdgeSide.Bottom })
        {
            var cells = StartStationAt(new Vec2(30, 20), side).Tiles.Cells.Values;
            var half = cells.Count(c => c.Wall == TileWallKind.Solid && c.WallOpenSide is not null);
            var solid = cells.Count(c => c.Wall == TileWallKind.Solid);
            // Every drawn half-block wall is there; the full walls differ only at the doorways (the ports not in use are walled).
            if (half != drawnHalf || Math.Abs(solid - drawnSolid) > 30)
                return false;
        }
        return true;
    }
}
