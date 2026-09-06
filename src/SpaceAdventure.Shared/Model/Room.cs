using System.Text.Json.Serialization;

namespace SpaceAdventure.Shared.Model;

// Footprint in world units (meters) - a UNION of 1+ axis-aligned RectF pieces (humble-soaring-
// cat.md M86: non-rectangular compartments). Kept to a single RoomId no matter how many pieces the
// footprint has - every gameplay system that gates on "RoomId == RoomId" (oxygen, sensors,
// interaction, AI targeting - see World.Atmosphere.cs/World.ComponentLogic.cs/World.Boarding.cs and
// ~20 more) stays completely unaffected by a room having more than one piece, since it's still
// exactly one id per compartment; only geometry (Contains/adjacency/rendering) needs to consider
// every piece instead of assuming exactly one.
[method: JsonConstructor]
public sealed record Room(string Id, string Name, IReadOnlyList<RectF> Rects)
{
    // Compat constructor - every existing call site (every hand-authored hull, EnemyShipLayout,
    // Station.cs) keeps building a plain single-rect Room exactly as before. [JsonConstructor] on
    // the OTHER (Rects) constructor disambiguates for System.Text.Json (a record with 2 public
    // constructors is otherwise a hard error - "no suitable constructor"); this one is never a JSON
    // deserialization target itself.
    public Room(string Id, string Name, float X, float Y, float Width, float Height)
        : this(Id, Name, new[] { new RectF(X, Y, Width, Height) })
    {
    }

    // Record-synthesized Equals/GetHashCode would use IReadOnlyList<RectF>'s own (reference)
    // equality, silently breaking every "are these two independently-built rooms the same" check
    // (e.g. TestRunner.StationProcedural.cs's determinism test) the moment Rects holds more than a
    // literal reference-shared array. Sequence-compare instead - the actual field that matters.
    public bool Equals(Room? other) =>
        other is not null && Id == other.Id && Name == other.Name && Rects.SequenceEqual(other.Rects);

    public override int GetHashCode()
    {
        var hash = HashCode.Combine(Id, Name);
        foreach (var rect in Rects)
            hash = HashCode.Combine(hash, rect);
        return hash;
    }

    // Derived bounding box - every read site that only ever wants "a" size/position (lighting
    // radius, UI, HullSkin's nose/flank reach math) keeps working unchanged. Equals the true single
    // rect exactly whenever Rects.Count == 1 (every hand-authored hull, forever).
    public float X => Rects.Min(r => r.X);
    public float Y => Rects.Min(r => r.Y);
    public float Width => Rects.Max(r => r.Right) - X;
    public float Height => Rects.Max(r => r.Bottom) - Y;
    public float Left => X;
    public float Right => X + Width;
    public float Top => Y;
    public float Bottom => Y + Height;

    // Area-weighted centroid, not bbox-center - for an L-shape the bbox center can sit in the
    // notch (outside the room entirely), which would misplace anything anchored on Center (room
    // lamps, the room name label).
    public Vec2 Center => Rects.Count == 1
        ? Rects[0].Center
        : RoomGeometry.AreaWeightedCentroid(Rects);

    public bool Contains(Vec2 p) => Rects.Any(r => r.Contains(p));
}
