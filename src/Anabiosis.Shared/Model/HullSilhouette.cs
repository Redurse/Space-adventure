namespace Anabiosis.Shared.Model;

// The hull's real outline: the union of its compartments, in the ship's own layout coordinates.
//
// Everything outside the ship used to work off the bounding box of all the rooms instead. On a hull
// that's one straight row of boxes the two are the same thing, so nobody noticed - but the moment a
// class isn't a rectangle (the Corvette is a U, with open space between its engine pylons) the box
// covers vacuum, and a crewman in magnetic boots walks across a gap with nothing under his feet.
//
// M92 (humble-soaring-cat.md, non-rectangular compartments) - flattens every room to its own
// constituent RectF pieces first (a room can now be a non-rectangular UNION of several - Room.cs's
// own M86), rather than one rectangle per room. RoomId isn't needed anywhere in this file (pure EVA/
// boot-snapping geometry), so the flattened form here is just a plain RectF list. Byte-identical to
// the old per-room math whenever every room has exactly one rect (every hand-authored hull/station,
// forever) - genuinely carries someone around an inside corner of a multi-rect room's own notch too,
// exactly the same way it already does for a multi-room hull's own concave silhouette.
public static class HullSilhouette
{
    private static IEnumerable<RectF> Flatten(IReadOnlyList<Room> rooms) => rooms.SelectMany(r => r.Rects);

    public static bool Contains(IReadOnlyList<Room> rooms, Vec2 localPoint)
    {
        foreach (var rect in Flatten(rooms))
            if (localPoint.X >= rect.Left && localPoint.X <= rect.Right &&
                localPoint.Y >= rect.Top && localPoint.Y <= rect.Bottom)
                return true;
        return false;
    }

    // 0 anywhere on or under the plating, otherwise the distance out to the nearest compartment.
    public static float DistanceOutside(IReadOnlyList<Room> rooms, Vec2 localPoint)
    {
        if (Contains(rooms, localPoint))
            return 0f;

        var nearest = float.MaxValue;
        foreach (var rect in Flatten(rooms))
            nearest = (float)Math.Min(nearest, (ClampToRect(rect, localPoint) - localPoint).Length());
        return nearest;
    }

    // Where a boot ends up: standing `clearance` clear of the plating, on the outside of it.
    public static Vec2 SnapToSurface(IReadOnlyList<Room> rooms, Vec2 localPoint, float clearance)
    {
        var rects = Flatten(rooms).ToList();
        if (rects.Count == 0)
            return localPoint;

        if (!Contains(rooms, localPoint))
        {
            // Outside already: walk in to the nearest bit of plating, then stand off it. Snapping
            // to the *nearest* piece rather than the whole box is what carries someone around
            // an inside corner instead of across the gap it opens.
            var best = ClampToRect(rects[0], localPoint);
            var bestDistance = (best - localPoint).Length();
            for (var i = 1; i < rects.Count; i++)
            {
                var candidate = ClampToRect(rects[i], localPoint);
                var distance = (candidate - localPoint).Length();
                if (distance < bestDistance)
                    (best, bestDistance) = (candidate, distance);
            }

            var outward = localPoint - best;
            return outward.Length() < 0.0001f ? best : best + outward.Normalized() * clearance;
        }

        // Under the plating (walked into the hull, or spawned inside it): leave through the nearest
        // face that actually opens onto space. A face shared with the next piece is not a way out -
        // stepping through it would put the boots inside the ship.
        var exit = NearestExteriorExit(rects, rooms, localPoint, clearance);
        return exit ?? localPoint;
    }

    private static Vec2? NearestExteriorExit(IReadOnlyList<RectF> rects, IReadOnlyList<Room> rooms, Vec2 localPoint, float clearance)
    {
        Vec2? best = null;
        var bestDistance = float.MaxValue;

        foreach (var rect in rects)
        {
            if (localPoint.X < rect.Left || localPoint.X > rect.Right ||
                localPoint.Y < rect.Top || localPoint.Y > rect.Bottom)
                continue;

            foreach (var (distance, candidate) in new[]
                     {
                         (localPoint.X - rect.Left, new Vec2(rect.Left - clearance, localPoint.Y)),
                         (rect.Right - localPoint.X, new Vec2(rect.Right + clearance, localPoint.Y)),
                         (localPoint.Y - rect.Top, new Vec2(localPoint.X, rect.Top - clearance)),
                         (rect.Bottom - localPoint.Y, new Vec2(localPoint.X, rect.Bottom + clearance)),
                     })
            {
                if (distance >= bestDistance || Contains(rooms, candidate))
                    continue;
                best = candidate;
                bestDistance = (float)distance;
            }
        }

        return best;
    }

    private static Vec2 ClampToRect(RectF rect, Vec2 point) =>
        new(Math.Clamp(point.X, rect.Left, rect.Right), Math.Clamp(point.Y, rect.Top, rect.Bottom));
}
