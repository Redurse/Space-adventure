using Anabiosis.Server;
using Anabiosis.Shared.Model;

// Structural checks on Station.Procedural.cs's generator, independent of World/docking - these
// exercise the generator directly against a handful of synthetic point ids per kind, so a
// regression here is caught without needing a running World at all.
internal static partial class TestRunner
{
    private static readonly StationKind[] AllStationKinds =
    {
        StationKind.Trade, StationKind.Military, StationKind.Mining, StationKind.Shipyard, StationKind.Research,
    };

    private static readonly string[] SamplePointIds = { "sample-a", "sample-b", "sample-c", "sample-d" };

    private static bool Station_Procedural_RoomCountStaysInTheAgreedBand()
    {
        foreach (var kind in AllStationKinds)
            foreach (var pointId in SamplePointIds)
            {
                var station = Station.CreateProcedural(pointId + "-" + kind, kind, Vec2.Zero);
                if (station.Rooms.Count < 10 || station.Rooms.Count > 20)
                    return false;
            }
        return true;
    }

    // The 7 modules that must exist regardless of kind (World.Recruiting.cs/World.StationCrime.cs
    // hard-require Recruiter/Security specifically to exist anywhere on the station), plus
    // Shipyard's own always-present Shipwright.
    private static bool Station_Procedural_AlwaysHasTheMandatoryModules()
    {
        foreach (var kind in AllStationKinds)
            foreach (var pointId in SamplePointIds)
            {
                var station = Station.CreateProcedural(pointId + "-" + kind, kind, Vec2.Zero);
                var kinds = station.Npcs.Select(n => n.Kind).ToHashSet();
                if (!kinds.Contains(NpcKind.Trader) || !kinds.Contains(NpcKind.Administrator) ||
                    !kinds.Contains(NpcKind.Mechanic) || !kinds.Contains(NpcKind.Security) ||
                    !kinds.Contains(NpcKind.Recruiter))
                    return false;
                if (kind == StationKind.Shipyard && !kinds.Contains(NpcKind.Shipwright))
                    return false;
                if (station.Crates.Count == 0) // Storage's own mandatory crate, at minimum
                    return false;
            }
        return true;
    }

    // The whole point of the ring: every room reachable from the dock by crossing doors, no
    // isolated compartment left stranded (TestRunner.RoomGraph.cs's ReachableRoomIds).
    private static bool Station_Procedural_EveryRoomIsReachableFromTheDock()
    {
        foreach (var kind in AllStationKinds)
            foreach (var pointId in SamplePointIds)
            {
                var station = Station.CreateProcedural(pointId + "-" + kind, kind, Vec2.Zero);
                var reachable = ReachableRoomIds(station.Doors, station.DockRoomId);
                if (reachable.Count != station.Rooms.Count)
                    return false;
            }
        return true;
    }

    private static bool RoomsOverlap(Room a, Room b) =>
        a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;

    private static bool Station_Procedural_NoTwoRoomsOverlap()
    {
        foreach (var kind in AllStationKinds)
            foreach (var pointId in SamplePointIds)
            {
                var station = Station.CreateProcedural(pointId + "-" + kind, kind, Vec2.Zero);
                for (var i = 0; i < station.Rooms.Count; i++)
                    for (var j = i + 1; j < station.Rooms.Count; j++)
                        if (RoomsOverlap(station.Rooms[i], station.Rooms[j]))
                            return false;
            }
        return true;
    }

    // The same point id must always regenerate byte-identical geometry - no separate seed is ever
    // saved (World.Save.cs), so this is the one property the whole per-instance-station feature
    // depends on.
    private static bool Station_Procedural_SamePointIdAlwaysRegeneratesIdentically()
    {
        foreach (var kind in AllStationKinds)
        {
            var a = Station.CreateProcedural("repeat-check", kind, new Vec2(3f, -7f));
            var b = Station.CreateProcedural("repeat-check", kind, new Vec2(3f, -7f));
            if (a.Rooms.Count != b.Rooms.Count)
                return false;
            for (var i = 0; i < a.Rooms.Count; i++)
                if (a.Rooms[i] != b.Rooms[i])
                    return false;
        }
        return true;
    }

    // Different point ids of the SAME kind must not collapse onto the same shape - the entire
    // point of moving off the old "one shared instance per kind" model.
    private static bool Station_Procedural_DifferentPointIdsGiveDifferentShapes()
    {
        var a = Station.CreateProcedural("shape-a", StationKind.Trade, Vec2.Zero);
        var b = Station.CreateProcedural("shape-b", StationKind.Trade, Vec2.Zero);
        return a.Rooms.Count != b.Rooms.Count || !a.Rooms.Select(r => (r.Width, r.Height)).SequenceEqual(b.Rooms.Select(r => (r.Width, r.Height))) ||
            !a.Npcs.Select(n => n.Kind).SequenceEqual(b.Npcs.Select(n => n.Kind));
    }

    // A free-tile-editor-built ship can put its airlock on ANY of the 4 walls (CustomShipDefinition's
    // EdgeSide), unlike every hand-authored hull (always Right/east) - regression test for the
    // station-orientation fix (World.cs now computes the real side via Ship.Convert.cs's
    // InferAirlockSide and passes it through). A synthetic 10x6 "hull" rect stands in for the ship;
    // for each side, the airlock sits in the middle of that wall, and the generated station must
    // never land any room on top of the hull rect - the whole point of orienting away from it.
    private static bool Station_Procedural_OrientsAwayFromShipHullOnEverySide()
    {
        var hull = new Room("hull", "Hull", 0f, 0f, 10f, 6f);
        var casesBySide = new (EdgeSide Side, Vec2 Anchor)[]
        {
            (EdgeSide.Right, new Vec2(10f, 3f)),
            (EdgeSide.Left, new Vec2(0f, 3f)),
            (EdgeSide.Top, new Vec2(5f, 0f)),
            (EdgeSide.Bottom, new Vec2(5f, 6f)),
        };
        foreach (var kind in AllStationKinds)
            foreach (var (side, anchor) in casesBySide)
            {
                var station = Station.CreateProcedural($"orient-{side}-{kind}", kind, anchor, side);
                foreach (var room in station.Rooms)
                    if (RoomsOverlap(hull, room))
                        return false;
            }
        return true;
    }

    // The dock's own connector must land exactly on connectorAnchor regardless of kind - the exact
    // contract World.cs's GetOrCreateStation relies on to keep a docked ship's airlock lined up.
    private static bool Station_Procedural_ConnectorLandsExactlyOnTheAnchor()
    {
        var anchor = new Vec2(23f, 3f);
        foreach (var kind in AllStationKinds)
        {
            var station = Station.CreateProcedural("anchor-check-" + kind, kind, anchor);
            if (Math.Abs(station.ShipConnector.Position.X - anchor.X) > 0.001f ||
                Math.Abs(station.ShipConnector.Position.Y - anchor.Y) > 0.001f)
                return false;
        }
        return true;
    }
}
