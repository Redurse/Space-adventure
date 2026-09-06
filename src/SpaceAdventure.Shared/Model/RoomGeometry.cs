namespace SpaceAdventure.Shared.Model;

// Shared helpers for multi-rect rooms (humble-soaring-cat.md M86) - every geometry consumer that
// used to iterate IReadOnlyList<Room>/IReadOnlyList<CustomRoomDef> assuming one rectangle per room
// (ShipLayoutGeometry's door/airlock adjacency, CustomShipValidator's overlap check, HullSkin's
// plate/corner-cut rendering, HullSilhouette's EVA geometry) is rewritten against the flattened
// RoomRect list these produce instead.
public static class RoomGeometry
{
    public static IReadOnlyList<RoomRect> Flatten(IReadOnlyList<Room> rooms) =>
        rooms.SelectMany(r => r.Rects.Select(rect => new RoomRect(r.Id, rect))).ToList();

    public static IReadOnlyList<RoomRect> Flatten(IReadOnlyList<CustomRoomDef> rooms) =>
        rooms.SelectMany(r => r.Rects.Select(rect => new RoomRect(r.Id, rect))).ToList();

    // The room's true centroid (area-weighted across every constituent rect) rather than its
    // bounding-box center, which can land outside an L/plus-shaped room entirely (in the notch).
    public static Vec2 AreaWeightedCentroid(IReadOnlyList<RectF> rects)
    {
        var totalArea = 0f;
        var sum = new Vec2(0, 0);
        foreach (var rect in rects)
        {
            var area = rect.Area;
            totalArea += area;
            sum = new Vec2(sum.X + rect.Center.X * area, sum.Y + rect.Center.Y * area);
        }
        return totalArea > 0f ? new Vec2(sum.X / totalArea, sum.Y / totalArea) : new Vec2(0, 0);
    }
}
