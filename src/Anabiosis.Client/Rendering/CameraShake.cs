using System;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Screen shake in the "trauma" style: every blow adds a little trauma (0..1), it drains away on its own, and the
// shake actually shown is trauma squared - so a hail of small knocks barely stirs the picture while one big blast
// really throws it. The offset is smooth noise (a few sines at unrelated frequencies), not a random jitter every
// frame, so it reads as the ship shuddering rather than the screen flickering.
public sealed class CameraShake
{
    // Largest offset in design pixels at full trauma.
    public const float MaxPixels = 22f;
    private const float DrainPerSecond = 1.5f;

    // 0 turns the shake off altogether (an option for anyone it makes queasy), 1 is the intended amount.
    public float Strength { get; set; } = 1f;

    private float _trauma;
    private float _time;

    public float Trauma => _trauma;

    public void Add(float amount)
    {
        if (amount > 0f)
            _trauma = MathF.Min(1f, _trauma + amount);
    }

    public void Step(float deltaSeconds)
    {
        _time += deltaSeconds;
        _trauma = MathF.Max(0f, _trauma - DrainPerSecond * deltaSeconds);
    }

    public Vector2 Offset()
    {
        if (_trauma <= 0f || Strength <= 0f)
            return Vector2.Zero;
        var power = _trauma * _trauma * MaxPixels * Strength;
        var x = MathF.Sin(_time * 51f) + 0.6f * MathF.Sin(_time * 87f + 1.3f);
        var y = MathF.Sin(_time * 59f + 2.1f) + 0.6f * MathF.Sin(_time * 93f + 0.4f);
        return new Vector2(x, y) * (power / 1.6f);
    }
}
