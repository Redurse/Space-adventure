namespace SpaceAdventure.Shared.Model;

// M56 - "карта солнечной системы по размерам как в KSP": widened from float to double. At literal
// KSP distances (the outer edge of a system reaches tens of billions of units - Eeloo's real orbit
// is ~90 118 820 000 m) float32's own ~7-digit precision floor (~10 000 units at that magnitude)
// would swallow every local-scale quantity this same type is used for elsewhere (ship maneuvering,
// EVA, docking, collisions - all in the 1-1000 unit range) - not a rare edge case, every one of
// them. double keeps roughly 15-16 significant digits, comfortably covering both ends at once.
// Client-side rendering still narrows down to MonoGame's own float-only Vector2 at the drawing
// boundary (an explicit (float) cast at each Vector2 construction site) - that boundary is where
// this precision genuinely stops mattering (screen pixels), not a step earlier.
public readonly record struct Vec2(double X, double Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public double Length() => Math.Sqrt(X * X + Y * Y);

    public Vec2 Normalized()
    {
        var length = Length();
        return length > 0.0001 ? new Vec2(X / length, Y / length) : Zero;
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 v) => new(-v.X, -v.Y);
    // Both overloads kept explicit (rather than relying on float->double's own implicit widening
    // for a single `double s` overload) so a caller multiplying by an already-double scalar (e.g.
    // deltaSeconds) never needs its own (float) cast just to satisfy this operator.
    public static Vec2 operator *(Vec2 v, float s) => new(v.X * s, v.Y * s);
    public static Vec2 operator *(Vec2 v, double s) => new(v.X * s, v.Y * s);

    public Vec2 Clamp(double minX, double minY, double maxX, double maxY) =>
        new(Math.Clamp(X, minX, maxX), Math.Clamp(Y, minY, maxY));

    // The OTHER float/double seam, inside the shared model itself: ship-local fixtures (Room,
    // CustomDeviceDef, HelmConsole, ...) stay float - a ship interior is always tens of units
    // across, nowhere near float32's precision floor - while Vec2 stays double for field-scale
    // positions (this record's own doc comment above). Reach for this named, explicit conversion
    // instead of a bare (float)v.X/(float)v.Y pair at a call site, the same way client rendering
    // already has ONE explicit cast boundary at each MonoGame Vector2 construction rather than
    // scattered raw casts - a truncation that has a name reads as intentional, not an oversight.
    public (float X, float Y) AsFloat() => ((float)X, (float)Y);
}
