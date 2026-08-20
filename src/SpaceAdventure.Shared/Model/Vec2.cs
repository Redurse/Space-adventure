namespace SpaceAdventure.Shared.Model;

public readonly record struct Vec2(float X, float Y)
{
    public static readonly Vec2 Zero = new(0, 0);

    public float Length() => MathF.Sqrt(X * X + Y * Y);

    public Vec2 Normalized()
    {
        var length = Length();
        return length > 0.0001f ? new Vec2(X / length, Y / length) : Zero;
    }

    public static Vec2 operator +(Vec2 a, Vec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec2 operator -(Vec2 a, Vec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec2 operator -(Vec2 v) => new(-v.X, -v.Y);
    public static Vec2 operator *(Vec2 v, float s) => new(v.X * s, v.Y * s);

    public Vec2 Clamp(float minX, float minY, float maxX, float maxY) =>
        new(Math.Clamp(X, minX, maxX), Math.Clamp(Y, minY, maxY));
}
