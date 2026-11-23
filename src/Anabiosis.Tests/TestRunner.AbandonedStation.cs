using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// The dead station place (World.AbandonedStations.cs): off the charts until found, then dockable; empty,
// airless, with salvage in its rooms that is remembered once taken.
internal static partial class TestRunner
{
    // A seeded world whose home system rolled a dead station (two systems in three do), undocked.
    private static (World World, PointOfInterest Station)? WorldWithAnAbandonedStation()
    {
        for (var seed = 1; seed <= 12; seed++)
        {
            var world = new World(worldSeed: seed);
            if (world.GalaxyMap.GetSystem("sol").Pois.FirstOrDefault(p => p.Kind == PoiKind.AbandonedStation) is not { } station)
                continue;
            world.SpawnCharacter(1);
            if (world.IsDocked)
            {
                world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
                world.Step(RealtimeStep);
            }
            return (world, station);
        }
        return null;
    }

    private static bool DockAtAbandonedStation(World world, PointOfInterest station)
    {
        world.DebugPlaceShip(station.Position + new Vec2(150, 0)); // in sensor range: found, and now the nearest station
        world.Step(RealtimeStep);
        world.Step(RealtimeStep);
        world.DebugPlaceShip(world.DockBerthPosition);
        world.Step(RealtimeStep);
        if (!world.CanDockNow)
            return false;
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        return world.IsDocked && world.DockedAtAbandonedStation;
    }

    private static bool World_AbandonedStation_IsOffTheChartsUntilFound_ThenDockable()
    {
        if (WorldWithAnAbandonedStation() is not var (world, station))
            return false;
        world.Step(RealtimeStep);
        var before = world.CreateSnapshot();
        var hidden = before.GalaxyPoints.All(p => p.Kind != GalaxyPointKind.AbandonedStation)
            && before.PointsOfInterest!.All(p => p.Id != station.Id);

        if (!DockAtAbandonedStation(world, station))
            return false;
        var docked = world.CreateSnapshot();
        return hidden
            && docked.GalaxyPoints.Any(p => p.Id == station.Id && p.Kind == GalaxyPointKind.AbandonedStation)
            && docked.PointsOfInterest!.Single(p => p.Id == station.Id).Kind == PoiKind.AbandonedStation
            && docked.Voyage.DockedPointId == station.Id
            && docked.Station.IsAbandoned && docked.Station.Npcs.Count == 0 && docked.Station.Crates.Count == 0
            && docked.Station.Residents is null
            && docked.DroppedItems.Count(d => d.Id.StartsWith("station-loot-")) >= 4;
    }

    // Nobody refuels a ship at a dead station: docking there leaves the reactor fuel where it was.
    private static bool World_AbandonedStation_DockingDoesNotResupply()
    {
        if (WorldWithAnAbandonedStation() is not var (world, station))
            return false;
        StepFor(world, 20 * 30); // burn a little fuel first
        var fuelBefore = world.CreateSnapshot().Reactor.Fuel;
        if (!DockAtAbandonedStation(world, station))
            return false;
        var fuelAfter = world.CreateSnapshot().Reactor.Fuel;
        return fuelBefore < world.CreateSnapshot().Reactor.MaxFuel && fuelAfter <= fuelBefore + 0.001f;
    }

    // No air in there: an unsuited visitor chokes. Salvage is picked up like anything on a floor, the save
    // remembers it, and coming back finds the rest still lying where it was - minus what was taken.
    private static bool World_AbandonedStation_IsAirless_AndItsSalvageIsTakenOnce()
    {
        if (WorldWithAnAbandonedStation() is not var (world, station) || !DockAtAbandonedStation(world, station))
            return false;
        var salvage = world.CreateSnapshot().DroppedItems.Where(d => d.Id.StartsWith("station-loot-")).ToList();
        var pick = salvage[0];

        world.DebugPlaceCharacterOnStation(1, pick.Position);
        var healthBefore = world.CreateSnapshot().Characters.Single().Health;
        StepFor(world, 30);
        var choking = world.CreateSnapshot().Characters.Single().Health < healthBefore;

        world.ApplyCommand(1, new ClientCommand(1, PickupDroppedItemId: pick.Id));
        world.Step(RealtimeStep);
        var taken = world.CreateSnapshot();
        var carried = taken.Characters.Single().Inventory!.MainSlots.Contains(pick.Item) && taken.DroppedItems.All(d => d.Id != pick.Id);
        var remembered = world.CreateSave().Exploration!.Single(e => e.SystemId == "sol").TakenLootIds!.Contains(pick.Id);

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // cast off: the rooms and what is in them go
        world.Step(RealtimeStep);
        var clearedOnUndock = !world.IsDocked && world.CreateSnapshot().DroppedItems.All(d => !d.Id.StartsWith("station-loot-"));

        if (!DockAtAbandonedStation(world, station))
            return false;
        var back = world.CreateSnapshot().DroppedItems.Where(d => d.Id.StartsWith("station-loot-")).Select(d => d.Id).ToList();
        return choking && carried && remembered && clearedOnUndock
            && back.Count == salvage.Count - 1 && !back.Contains(pick.Id);
    }

    // Everything that looks for stations must not see a dead one: it is its own kind of point.
    private static bool World_AbandonedStation_IsNotAStationForTheRestOfTheGame()
    {
        if (WorldWithAnAbandonedStation() is not var (world, station))
            return false;
        var point = world.GalaxyMap.GetPoint(station.Id);
        return point.Kind == GalaxyPointKind.AbandonedStation
            && world.GalaxyMap.GetSystem("sol").Points.Count(p => p.Kind == GalaxyPointKind.Station)
                == new World().GalaxyMap.GetSystem("sol").Points.Count(p => p.Kind == GalaxyPointKind.Station)
            && new World().GalaxyMap.GetSystem("sol").Points.All(p => p.Kind != GalaxyPointKind.AbandonedStation);
    }
}
