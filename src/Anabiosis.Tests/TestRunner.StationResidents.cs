using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Station residents: TilePathfinder (A* on tiles) and StationResidentSim (people who walk around a
// docked station). Pure TileGrid / Station tests - no World needed except the one snapshot check.
internal static partial class TestRunner
{
    // A w x h block of open floor.
    private static TileGrid PathfindingRoom(int width, int height)
    {
        var g = new TileGrid();
        for (var x = 0; x < width; x++)
            for (var y = 0; y < height; y++)
                g.SetFloor(new TileCoord(x, y), true);
        return g;
    }

    private static bool TilePathfinder_OpenFloor_FindsAShortStraightPath()
    {
        var g = PathfindingRoom(8, 3);
        var path = TilePathfinder.FindPath(g, new TileCoord(0, 1), new TileCoord(7, 1));
        return path is { Count: 7 } && path[^1] == new TileCoord(7, 1) && path.All(c => c.Y == 1);
    }

    private static bool TilePathfinder_GoesAroundAWall()
    {
        var g = PathfindingRoom(7, 5);
        for (var y = 0; y < 4; y++)
            g.SetWall(new TileCoord(3, y), TileWallKind.Solid); // a wall with its only gap at y=4
        var path = TilePathfinder.FindPath(g, new TileCoord(1, 1), new TileCoord(5, 1));
        return path is not null && path.Any(c => c == new TileCoord(3, 4)) && path.All(c => TilePathfinder.IsPassable(g, c));
    }

    private static bool TilePathfinder_NoRoute_ReturnsNull()
    {
        var g = PathfindingRoom(7, 3);
        for (var y = 0; y < 3; y++)
            g.SetWall(new TileCoord(3, y), TileWallKind.Solid); // sealed off
        return TilePathfinder.FindPath(g, new TileCoord(1, 1), new TileCoord(5, 1)) is null
            && TilePathfinder.FindPath(g, new TileCoord(1, 1), new TileCoord(3, 1)) is null; // goal inside a wall
    }

    private static bool TilePathfinder_NeverCutsAWallCorner()
    {
        // Two wall tiles meeting only at a corner: the diagonal between them must not be used.
        var g = PathfindingRoom(4, 4);
        g.SetWall(new TileCoord(1, 2), TileWallKind.Solid);
        g.SetWall(new TileCoord(2, 1), TileWallKind.Solid);
        var path = TilePathfinder.FindPath(g, new TileCoord(1, 1), new TileCoord(2, 2));
        if (path is null)
            return false;
        var previous = new TileCoord(1, 1);
        foreach (var step in path)
        {
            if (step.X != previous.X && step.Y != previous.Y &&
                (!TilePathfinder.IsPassable(g, new TileCoord(step.X, previous.Y)) || !TilePathfinder.IsPassable(g, new TileCoord(previous.X, step.Y))))
                return false;
            previous = step;
        }
        return true;
    }

    private static bool TilePathfinder_WalksThroughADoorTile_AndRespectsForbiddenTiles()
    {
        var g = PathfindingRoom(7, 3);
        for (var y = 0; y < 3; y++)
            g.SetWall(new TileCoord(3, y), y == 1 ? TileWallKind.Door : TileWallKind.Solid);
        if (TilePathfinder.FindPath(g, new TileCoord(1, 1), new TileCoord(5, 1)) is not { } through || !through.Contains(new TileCoord(3, 1)))
            return false;
        // Fence the door off and the only route is gone.
        var forbidden = new HashSet<TileCoord> { new(3, 1) };
        return TilePathfinder.FindPath(g, new TileCoord(1, 1), new TileCoord(5, 1), forbidden) is null;
    }

    // ---- the residents ----

    private static IEnumerable<(string Name, Station Station)> SampleStations()
    {
        foreach (var kind in AllStationKinds)
            foreach (var pointId in SamplePointIds)
            {
                var id = pointId + "-" + kind;
                yield return (id, Station.CreateProcedural(id, kind, Vec2.Zero));
            }
    }

    private static bool StationResidents_CrowdSizeScalesWithTheStationAndStaysBounded()
    {
        if (StationResidentSim.ResidentCountFor(3) < 3 || StationResidentSim.ResidentCountFor(200) > 22)
            return false;
        foreach (var (name, station) in SampleStations())
        {
            var sim = new StationResidentSim(station, name);
            if (sim.Count != StationResidentSim.ResidentCountFor(station.Rooms.Count) || sim.GuardCount < 1)
                return false;
        }
        return true;
    }

    private static bool StationResidents_SameStationGetsTheSameCrowd()
    {
        var (name, station) = SampleStations().First();
        var a = new StationResidentSim(station, name).States;
        var b = new StationResidentSim(station, name).States;
        return a.Count == b.Count && a.Zip(b).All(p => p.First == p.Second);
    }

    private static bool StationResidents_StartOnWalkableFloor_NeverOnTheShipConnector()
    {
        foreach (var (name, station) in SampleStations())
        {
            var sim = new StationResidentSim(station, name);
            foreach (var r in sim.States)
            {
                var tile = TilePathfinder.TileAt(new Vec2(r.X, r.Y));
                if (!TilePathfinder.IsPassable(station.Tiles, tile) || sim.IsForbidden(tile))
                    return false;
            }
        }
        return true;
    }

    private static bool StationResidents_WalkAroundForMinutes_StayInsideTheStation()
    {
        foreach (var (name, station) in SampleStations().Take(8))
        {
            var sim = new StationResidentSim(station, name);
            var start = sim.States.ToDictionary(s => s.Id, s => new Vec2(s.X, s.Y));
            var travelled = sim.States.ToDictionary(s => s.Id, _ => 0.0);
            var last = new Dictionary<string, Vec2>(start);
            for (var i = 0; i < 30 * 240; i++) // four minutes
            {
                sim.Step(1.0 / 30.0, alerted: false, Array.Empty<Vec2>());
                if (i % 5 != 0)
                    continue;
                foreach (var r in sim.States)
                {
                    var at = new Vec2(r.X, r.Y);
                    var tile = TilePathfinder.TileAt(at);
                    if (!TilePathfinder.IsPassable(station.Tiles, tile) || sim.IsForbidden(tile))
                        return false;
                    travelled[r.Id] += (at - last[r.Id]).Length();
                    last[r.Id] = at;
                }
            }
            // Most of them actually went somewhere.
            if (travelled.Values.Count(d => d > 6.0) < travelled.Count * 0.7)
                return false;
        }
        return true;
    }

    private static bool StationResidents_GuardsPatrolThroughSeveralRooms()
    {
        var (name, station) = SampleStations().First();
        var sim = new StationResidentSim(station, name);
        var guardIds = sim.States.Where(s => s.Role == ResidentRole.Guard).Select(s => s.Id).ToHashSet();
        var roomsSeen = guardIds.ToDictionary(id => id, _ => new HashSet<string>());
        for (var i = 0; i < 30 * 300; i++)
        {
            sim.Step(1.0 / 30.0, alerted: false, Array.Empty<Vec2>());
            if (i % 10 != 0)
                continue;
            foreach (var r in sim.States.Where(s => guardIds.Contains(s.Id)))
                if (station.Rooms.FirstOrDefault(room => room.Contains(new Vec2(r.X, r.Y))) is { } room)
                    roomsSeen[r.Id].Add(room.Id);
        }
        return roomsSeen.Values.All(rooms => rooms.Count >= 3);
    }

    private static bool StationResidents_WhenAlerted_CiviliansFreezeAndGuardsCloseIn()
    {
        var (name, station) = SampleStations().First();
        var sim = new StationResidentSim(station, name);
        for (var i = 0; i < 30 * 10; i++)
            sim.Step(1.0 / 30.0, alerted: false, Array.Empty<Vec2>());

        // The "crew member" stands somewhere on the station's walkable floor.
        var target = sim.States.First(s => s.Role == ResidentRole.Civilian);
        var crew = new[] { new Vec2(target.X, target.Y) };
        var civiliansBefore = sim.States.Where(s => s.Role == ResidentRole.Civilian).ToDictionary(s => s.Id, s => (s.X, s.Y));

        for (var i = 0; i < 30 * 40; i++)
            sim.Step(1.0 / 30.0, alerted: true, crew);

        var frozen = sim.States.Where(s => s.Role == ResidentRole.Civilian).All(s => civiliansBefore[s.Id] == (s.X, s.Y));
        var guardsClose = sim.States.Where(s => s.Role == ResidentRole.Guard)
            .All(g => (new Vec2(g.X, g.Y) - crew[0]).Length() < 3.5);
        return frozen && guardsClose;
    }

    private static bool World_StationResidents_AppearInTheSnapshotOnlyWhileDocked()
    {
        var world = new World();
        world.SpawnCharacter(1);
        // Not docked yet: nothing to send.
        if (world.IsDocked)
            DockAtStation(world, world.GalaxyMap.HomePointId); // make the check below meaningful either way
        var docked = world.CreateSnapshot().Station.Residents;
        if (docked is not { Count: > 0 })
            return false;

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true)); // undock
        world.Step(RealtimeStep);
        return !world.IsDocked && world.CreateSnapshot().Station.Residents is null;
    }

    // The crowd has to survive the trip over the wire (JSON, same path a joined player's client reads).
    private static bool World_StationResidents_SurviveTheWireRoundTrip()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var sent = world.CreateSnapshot().Station.Residents;
        if (sent is not { Count: > 0 })
            return false;
        var restored = Anabiosis.Shared.Networking.Wire.Deserialize<WorldSnapshot>(Anabiosis.Shared.Networking.Wire.Serialize(world.CreateSnapshot()));
        return restored.Station.Residents is { } got && got.Count == sent.Count && got.Zip(sent).All(p => p.First == p.Second);
    }
}
