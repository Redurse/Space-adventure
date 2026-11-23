using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Ships are solid. A ship does not fly into a station, and two ships do not fly into each other (direct user report: hulls drawn one inside another,
// ambient ships sitting inside stations):
//  * the player's hull against a station is tested as the shapes they are - the hull's rooms, turned as the hull is turned, against the station's rooms -
//    instead of by the four corners of the hull's bounding box (an arm of the hull could pass through a station between the corners);
//  * every simulated hull (raiders and the ambient ships that have been built, World.NpcHulls.cs) is pushed out of every station of the system, and out of each
//    other, the way it already was out of the player's ship (SeparateEnemy);
//  * the player's hull stops against any of those hulls, not only the raiders.
// A station is its rooms (their rectangles, in the docked frame, moved to where the point is in the field).
public sealed partial class World
{
    private const float StationSolidityActivationRadius = NpcHullSimRadius + 200f;
    private long _stationRectsTick = -1;
    private readonly List<RectF> _stationRects = new();
    private readonly List<(Vec2 Center, float Radius)> _stationBounds = new();
    private readonly List<string> _stationPointIds = new();

    // Every station of the current system as rectangles in field space (the same space the ships are in), rebuilt once a tick.
    private IReadOnlyList<RectF> FieldStationRects()
    {
        if (_stationRectsTick == Tick)
            return _stationRects;
        _stationRects.Clear();
        _stationBounds.Clear();
        _stationPointIds.Clear();
        foreach (var point in GalaxyMap.GetSystem(_currentSystemId).Points)
        {
            if (point.Kind != GalaxyPointKind.Station)
                continue;
            // A station that has not been built yet is built only once a hull that could touch it is near (building one costs tens of milliseconds -
            // every station of the system at once on the first tick was a visible hitch): simulated hulls stay within NpcHullSimRadius of the player.
            if (!_stationsByPointId.ContainsKey(point.Id) && (ResolveGalaxyPointPosition(point) - _shipFieldPosition).Length() > StationSolidityActivationRadius)
                continue;
            var station = GetOrCreateStation(point.Id);
            // A station's rooms are in the docked frame; its WorldOffset puts that frame where the point is (Station.RepositionTo).
            var offset = station.WorldOffset;
            var centre = station.Position;
            var radius = 0f;
            foreach (var room in station.Rooms)
                foreach (var rect in room.Rects)
                {
                    var moved = new RectF(rect.X + (float)offset.X, rect.Y + (float)offset.Y, rect.Width, rect.Height);
                    _stationRects.Add(moved);
                    var far = new Vec2(Math.Max(Math.Abs(moved.Left - centre.X), Math.Abs(moved.Right - centre.X)),
                        Math.Max(Math.Abs(moved.Top - centre.Y), Math.Abs(moved.Bottom - centre.Y)));
                    radius = Math.Max(radius, (float)far.Length());
                }
            _stationBounds.Add((centre, radius));
            _stationPointIds.Add(point.Id);
        }
        _stationRectsTick = Tick;
        return _stationRects;
    }

    // How far from its centre a station's rooms reach - a ship has "arrived" at it a little beyond that.
    private float StationReachOf(string pointId)
    {
        FieldStationRects();
        var index = _stationPointIds.IndexOf(pointId);
        // A station not built yet (none of the hulls is near it): a typical outpost's reach, so far-off ships stop about where they will have to anyway.
        return index < 0 ? 40f : _stationBounds[index].Radius;
    }

    // Moves a round hull (a hostile or ambient ship, by its bounding circle) out of every station's rooms.
    private void PushCircleOutOfStations(ref Vec2 position, float radius)
    {
        var rects = FieldStationRects();
        if (rects.Count == 0)
            return;
        for (var pass = 0; pass < 3; pass++)
        {
            var moved = false;
            for (var i = 0; i < rects.Count; i++)
            {
                var rect = rects[i];
                var closestX = Math.Clamp(position.X, rect.Left, rect.Right);
                var closestY = Math.Clamp(position.Y, rect.Top, rect.Bottom);
                var away = new Vec2(position.X - closestX, position.Y - closestY);
                var gap = (float)away.Length();
                if (gap >= radius)
                    continue;
                Vec2 normal;
                float push;
                if (gap > 0.001f)
                {
                    normal = away.Normalized();
                    push = radius - gap;
                }
                else
                {
                    // The centre is inside the rectangle: out through the nearest face.
                    var toLeft = position.X - rect.Left;
                    var toRight = rect.Right - position.X;
                    var toTop = position.Y - rect.Top;
                    var toBottom = rect.Bottom - position.Y;
                    var nearest = Math.Min(Math.Min(toLeft, toRight), Math.Min(toTop, toBottom));
                    normal = nearest == toLeft ? new Vec2(-1, 0) : nearest == toRight ? new Vec2(1, 0) : nearest == toTop ? new Vec2(0, -1) : new Vec2(0, 1);
                    push = (float)nearest + radius;
                }
                position += normal * push;
                moved = true;
            }
            if (!moved)
                break;
        }
    }

    // Does the player's hull, parked at `candidateCenter` and turned as it is now, overlap any station? Rooms against rooms, shrunk by HullClearance so
    // a hull lying flush against a berth does not count.
    private bool HullOverlapsStationShape(Vec2 candidateCenter)
    {
        var stations = FieldStationRects();
        if (stations.Count == 0)
            return false;
        var (localCenter, halfExtents) = GetHullLocalBounds();
        // Far from every station: the hull's bounding circle cannot reach any of them.
        var hullRadius = (float)halfExtents.Length();
        var any = false;
        foreach (var (center, radius) in _stationBounds)
            if ((center - candidateCenter).Length() < radius + hullRadius + 2f)
            {
                any = true;
                break;
            }
        if (!any)
            return false;

        var axisX = RotateLocalToWorld(new Vec2(1, 0), _shipRotationDegrees);
        var axisY = RotateLocalToWorld(new Vec2(0, 1), _shipRotationDegrees);
        Span<Vec2> corners = stackalloc Vec2[4];
        foreach (var room in Ship.Rooms)
            foreach (var rect in room.Rects)
            {
                var left = rect.Left + HullClearance;
                var right = rect.Right - HullClearance;
                var top = rect.Top + HullClearance;
                var bottom = rect.Bottom - HullClearance;
                if (right <= left || bottom <= top)
                    continue;
                corners[0] = candidateCenter + RotateLocalToWorld(new Vec2(left, top) - localCenter, _shipRotationDegrees);
                corners[1] = candidateCenter + RotateLocalToWorld(new Vec2(right, top) - localCenter, _shipRotationDegrees);
                corners[2] = candidateCenter + RotateLocalToWorld(new Vec2(right, bottom) - localCenter, _shipRotationDegrees);
                corners[3] = candidateCenter + RotateLocalToWorld(new Vec2(left, bottom) - localCenter, _shipRotationDegrees);
                var minX = Math.Min(Math.Min(corners[0].X, corners[1].X), Math.Min(corners[2].X, corners[3].X));
                var maxX = Math.Max(Math.Max(corners[0].X, corners[1].X), Math.Max(corners[2].X, corners[3].X));
                var minY = Math.Min(Math.Min(corners[0].Y, corners[1].Y), Math.Min(corners[2].Y, corners[3].Y));
                var maxY = Math.Max(Math.Max(corners[0].Y, corners[1].Y), Math.Max(corners[2].Y, corners[3].Y));

                for (var s = 0; s < stations.Count; s++)
                {
                    var station = stations[s];
                    if (maxX <= station.Left || station.Right <= minX || maxY <= station.Top || station.Bottom <= minY)
                        continue; // the boxes round the two do not even meet
                    // The two edge directions of the turned room are the other separating axes (the station rectangle brings the two screen ones).
                    if (!SeparatedAlong(axisX, corners, station) && !SeparatedAlong(axisY, corners, station))
                        return true;
                }
            }
        return false;
    }

    private static bool SeparatedAlong(Vec2 axis, ReadOnlySpan<Vec2> roomCorners, RectF station)
    {
        var roomMin = double.MaxValue;
        var roomMax = double.MinValue;
        foreach (var corner in roomCorners)
        {
            var projection = corner.X * axis.X + corner.Y * axis.Y;
            roomMin = Math.Min(roomMin, projection);
            roomMax = Math.Max(roomMax, projection);
        }
        var stationMin = double.MaxValue;
        var stationMax = double.MinValue;
        foreach (var (x, y) in new[] { (station.Left, station.Top), (station.Right, station.Top), (station.Right, station.Bottom), (station.Left, station.Bottom) })
        {
            var projection = x * axis.X + y * axis.Y;
            stationMin = Math.Min(stationMin, projection);
            stationMax = Math.Max(stationMax, projection);
        }
        return roomMax <= stationMin || stationMax <= roomMin;
    }
}
