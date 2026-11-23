using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// Ships are solid (World.Solidity.cs): an ambient or hostile hull does not sit inside a station, and two hulls do not sit inside each other.
internal static partial class TestRunner
{
    // The station's rooms as rectangles in field space, from what a client sees.
    private static List<RectF> StationFieldRects(World world)
    {
        var station = world.CreateSnapshot().Station;
        return station.Rooms
            .SelectMany(r => r.Rects)
            .Select(r => new RectF(r.X + (float)station.WorldOffset.X, r.Y + (float)station.WorldOffset.Y, r.Width, r.Height))
            .ToList();
    }

    private static float DistanceToRects(Vec2 point, IEnumerable<RectF> rects) =>
        rects.Min(r => (float)new Vec2(point.X - Math.Clamp(point.X, r.Left, r.Right), point.Y - Math.Clamp(point.Y, r.Top, r.Bottom)).Length());

    // An ambient ship dropped right on a station's centre is pushed out to its surface, not left sitting inside it.
    private static bool World_Solidity_AmbientShipIsPushedOutOfAStation()
    {
        var world = NpcHullWorld(out var id, out _);
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        var rects = StationFieldRects(world);
        var station = world.CreateSnapshot().Station;
        var centre = station.Position + station.WorldOffset;
        // Bound far away, so it does not turn back towards the station while the test watches.
        world.DebugPlaceNpc(id, centre, Vec2.Zero, centre + new Vec2(0, 2000));
        world.DebugPlaceShip(centre + new Vec2(120, 0)); // near enough for a built hull, far enough not to be a fight
        for (var i = 0; i < 5; i++)
            world.Step(RealtimeStep);
        var npc = world.CreateSnapshot().NpcShips.First(n => n.Id == id);
        if (npc.Rooms is null)
            return false;
        return DistanceToRects(new Vec2(npc.X, npc.Y), rects) > 1f;
    }

    // Two ambient ships put on the very same spot are shoved apart, not left as one overlapping shape.
    private static bool World_Solidity_TwoAmbientShipsDoNotOverlap()
    {
        var world = NpcHullWorld(out var first, out var start);
        var second = world.CreateSnapshot().NpcShips.First(n => n.Id != first).Id;
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        var at = new Vec2(start.X, start.Y);
        world.DebugPlaceShip(at + new Vec2(300, 0));
        world.DebugPlaceNpc(first, at, Vec2.Zero, at + new Vec2(0, 5000));
        world.DebugPlaceNpc(second, at + new Vec2(1, 0), Vec2.Zero, at + new Vec2(0, 5000));
        for (var i = 0; i < 5; i++)
            world.Step(RealtimeStep);
        var ships = world.CreateSnapshot().NpcShips;
        var a = ships.First(n => n.Id == first);
        var b = ships.First(n => n.Id == second);
        if (a.Rooms is null || b.Rooms is null)
            return false;
        var minX = a.Rooms.SelectMany(r => r.Rects).Min(r => r.Left);
        var maxX = a.Rooms.SelectMany(r => r.Rects).Max(r => r.Right);
        var minY = a.Rooms.SelectMany(r => r.Rects).Min(r => r.Top);
        var maxY = a.Rooms.SelectMany(r => r.Rects).Max(r => r.Bottom);
        var radius = Math.Sqrt(Math.Pow((maxX - minX) / 2, 2) + Math.Pow((maxY - minY) / 2, 2));
        var distance = (new Vec2(a.X, a.Y) - new Vec2(b.X, b.Y)).Length();
        return distance >= 2 * radius - 0.5;
    }
}
