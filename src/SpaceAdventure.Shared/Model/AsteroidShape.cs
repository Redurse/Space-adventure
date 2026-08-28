namespace SpaceAdventure.Shared.Model;

// The rock's actual outline: a closed polygon whose vertices are the base radius pushed in and out
// by a fixed amount per angle. One shape serves both the picture and the physics - an asteroid that
// looks like a jagged rock but collides like a perfect circle is the thing everyone notices, either
// because the hull stops short of an obvious gap or because it sinks into a spur.
//
// Generated from the id rather than stored, so it costs nothing to send and every part of the game
// derives the same rock. The hash is written out here instead of using string.GetHashCode: that one
// is randomised per process, which would give the server and the renderer different rocks the
// moment they stop sharing one.
public static class AsteroidShape
{
    public const int VertexCount = 14;

    public static float[] RadiusFactors(string id)
    {
        var factors = new float[VertexCount];
        var seed = StableHash(id);
        var random = new Random(seed);

        for (var i = 0; i < VertexCount; i++)
            factors[i] = 0.74f + (float)random.NextDouble() * 0.42f;

        // One smoothing pass over the ring: raw noise per vertex reads as a star, while a rock has
        // long faces and the odd sharp spur.
        var smoothed = new float[VertexCount];
        for (var i = 0; i < VertexCount; i++)
        {
            var previous = factors[(i - 1 + VertexCount) % VertexCount];
            var next = factors[(i + 1) % VertexCount];
            smoothed[i] = (previous + factors[i] * 2f + next) / 4f;
        }
        return smoothed;
    }

    public static Vec2[] Outline(Asteroid asteroid)
    {
        var factors = RadiusFactors(asteroid.Id);
        var points = new Vec2[VertexCount];
        for (var i = 0; i < VertexCount; i++)
        {
            var angle = i * (MathF.PI * 2f / VertexCount);
            var radius = asteroid.Radius * factors[i];
            points[i] = asteroid.Position + new Vec2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius);
        }
        return points;
    }

    // Radius of the rock along one bearing from its centre - the cheap answer for anything that
    // only needs "how far out does it reach this way", which is most of the physics.
    public static float RadiusAt(Asteroid asteroid, Vec2 fromCenter)
    {
        var factors = RadiusFactors(asteroid.Id);
        var angle = MathF.Atan2((float)fromCenter.Y, (float)fromCenter.X);
        if (angle < 0)
            angle += MathF.PI * 2f;

        var step = MathF.PI * 2f / VertexCount;
        var slot = angle / step;
        var low = (int)MathF.Floor(slot) % VertexCount;
        var high = (low + 1) % VertexCount;
        var blend = slot - MathF.Floor(slot);

        return asteroid.Radius * (factors[low] * (1f - blend) + factors[high] * blend);
    }

    public static bool Contains(Asteroid asteroid, Vec2 point)
    {
        var offset = point - asteroid.Position;
        var length = offset.Length();
        return length <= (length < 0.0001f ? asteroid.Radius : RadiusAt(asteroid, offset));
    }

    // How far outside the rock a point is: 0 on or under the surface, positive out in space.
    public static float DistanceOutside(Asteroid asteroid, Vec2 point)
    {
        var offset = point - asteroid.Position;
        var length = offset.Length();
        if (length < 0.0001f)
            return -asteroid.Radius;
        return (float)length - RadiusAt(asteroid, offset);
    }

    // Where a point sits once pulled onto the surface, standing `clearance` clear of the rock.
    public static Vec2 SurfacePoint(Asteroid asteroid, Vec2 point, float clearance)
    {
        var offset = point - asteroid.Position;
        if (offset.Length() < 0.0001f)
            offset = new Vec2(1f, 0f);
        var direction = offset.Normalized();
        return asteroid.Position + direction * (RadiusAt(asteroid, offset) + clearance);
    }

    // Internal rather than private - SystemOrbits.cs (M48's planet/belt generation) reuses this
    // exact same "same string always hashes the same way, even across processes/.NET versions"
    // property for its own per-system seeding, rather than keeping a second copy in sync.
    internal static int StableHash(string text)
    {
        unchecked
        {
            var hash = (int)2166136261;
            foreach (var c in text)
                hash = (hash ^ c) * 16777619;
            return hash & 0x7FFFFFFF;
        }
    }
}
