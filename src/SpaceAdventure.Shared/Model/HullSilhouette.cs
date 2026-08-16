namespace SpaceAdventure.Shared.Model;

// The hull's real outline: the union of its compartments, in the ship's own layout coordinates.
//
// Everything outside the ship used to work off the bounding box of all the rooms instead. On a hull
// that's one straight row of boxes the two are the same thing, so nobody noticed - but the moment a
// class isn't a rectangle (the Corvette is a U, with open space between its engine pylons) the box
// covers vacuum, and a crewman in magnetic boots walks across a gap with nothing under his feet.
public static class HullSilhouette
{
    public static bool Contains(IReadOnlyList<Room> rooms, Vec2 localPoint)
    {
        foreach (var room in rooms)
            if (localPoint.X >= room.Left && localPoint.X <= room.Right &&
                localPoint.Y >= room.Top && localPoint.Y <= room.Bottom)
                return true;
        return false;
    }

    // 0 anywhere on or under the plating, otherwise the distance out to the nearest compartment.
    public static float DistanceOutside(IReadOnlyList<Room> rooms, Vec2 localPoint)
    {
        if (Contains(rooms, localPoint))
            return 0f;

        var nearest = float.MaxValue;
        foreach (var room in rooms)
            nearest = Math.Min(nearest, (ClampToRoom(room, localPoint) - localPoint).Length());
        return nearest;
    }

    // Where a boot ends up: standing `clearance` clear of the plating, on the outside of it.
    public static Vec2 SnapToSurface(IReadOnlyList<Room> rooms, Vec2 localPoint, float clearance)
    {
        if (rooms.Count == 0)
            return localPoint;

        if (!Contains(rooms, localPoint))
        {
            // Outside already: walk in to the nearest bit of plating, then stand off it. Snapping
            // to the *nearest* compartment rather than the whole box is what carries someone around
            // an inside corner instead of across the gap it opens.
            var best = ClampToRoom(rooms[0], localPoint);
            var bestDistance = (best - localPoint).Length();
            for (var i = 1; i < rooms.Count; i++)
            {
                var candidate = ClampToRoom(rooms[i], localPoint);
                var distance = (candidate - localPoint).Length();
                if (distance < bestDistance)
                    (best, bestDistance) = (candidate, distance);
            }

            var outward = localPoint - best;
            return outward.Length() < 0.0001f ? best : best + outward.Normalized() * clearance;
        }

        // Under the plating (walked into the hull, or spawned inside it): leave through the nearest
        // face that actually opens onto space. A face shared with the next compartment is not a way
        // out - stepping through it would put the boots inside the ship.
        var exit = NearestExteriorExit(rooms, localPoint, clearance);
        return exit ?? localPoint;
    }

    private static Vec2? NearestExteriorExit(IReadOnlyList<Room> rooms, Vec2 localPoint, float clearance)
    {
        Vec2? best = null;
        var bestDistance = float.MaxValue;

        foreach (var room in rooms)
        {
            if (localPoint.X < room.Left || localPoint.X > room.Right ||
                localPoint.Y < room.Top || localPoint.Y > room.Bottom)
                continue;

            foreach (var (distance, candidate) in new[]
                     {
                         (localPoint.X - room.Left, new Vec2(room.Left - clearance, localPoint.Y)),
                         (room.Right - localPoint.X, new Vec2(room.Right + clearance, localPoint.Y)),
                         (localPoint.Y - room.Top, new Vec2(localPoint.X, room.Top - clearance)),
                         (room.Bottom - localPoint.Y, new Vec2(localPoint.X, room.Bottom + clearance)),
                     })
            {
                if (distance >= bestDistance || Contains(rooms, candidate))
                    continue;
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static Vec2 ClampToRoom(Room room, Vec2 point) =>
        new(Math.Clamp(point.X, room.Left, room.Right), Math.Clamp(point.Y, room.Top, room.Bottom));
}
