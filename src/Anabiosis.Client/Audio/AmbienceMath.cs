using System;

namespace Anabiosis.Client.Audio;

// The small pure pieces of the ship's soundscape (ShipAmbience): how loud something is at a distance, how fast a level follows its target, when the next
// footstep falls, when the next creak is due. Nothing here touches audio, so each can be checked by a test.
public static class AmbienceMath
{
    // 1 up close, 0 beyond `max`, falling smoothly in between (squared, so a sound is clear for a few steps and then dies off quickly).
    public static float Attenuation(float distance, float near = 3f, float max = 26f)
    {
        if (distance <= near)
            return 1f;
        if (distance >= max)
            return 0f;
        var t = (distance - near) / (max - near);
        return (1f - t) * (1f - t);
    }

    // `current` moving toward `target` at about `ratePerSecond` of the remaining gap per second - no jumps, so a level that changes never clicks.
    public static float Approach(float current, float target, float dt, float ratePerSecond)
    {
        var k = Math.Clamp(dt * ratePerSecond, 0f, 1f);
        return current + (target - current) * k;
    }

    // Left/right placement for a sound `dx` to one side of the listener.
    public static float Pan(float dx, float range = 14f) => Math.Clamp(dx / range, -0.85f, 0.85f);
}

// Counts footsteps from distance walked: one step each time `stride` of ground has been covered, however the movement was cut into frames.
public sealed class StrideCounter
{
    private float _walked;

    public int Advance(float distance, float stride)
    {
        _walked += distance;
        var steps = 0;
        while (_walked >= stride)
        {
            _walked -= stride;
            steps++;
        }
        return steps;
    }

    public void Reset() => _walked = 0f;
}

// "Something happens now and then": the next time is drawn at random from a range, and a rising `intensity` (hull damage, a fight) makes the gaps shorter.
public sealed class RandomEventTimer
{
    private readonly Random _random;
    private readonly double _minSeconds;
    private readonly double _maxSeconds;
    private double _nextAt = double.NaN;

    public RandomEventTimer(Random random, double minSeconds, double maxSeconds)
    {
        _random = random;
        _minSeconds = minSeconds;
        _maxSeconds = maxSeconds;
    }

    // True once when the time has come; `intensity` 0 = the plain gaps, 1 = a quarter of them.
    public bool Due(double now, float intensity)
    {
        if (double.IsNaN(_nextAt))
        {
            _nextAt = now + Gap(intensity) * _random.NextDouble() + 2.0; // the first one comes sooner than a full gap
            return false;
        }
        if (now < _nextAt)
            return false;
        _nextAt = now + Gap(intensity);
        return true;
    }

    private double Gap(float intensity)
    {
        var scale = 1.0 / (1.0 + 3.0 * Math.Clamp(intensity, 0f, 1f));
        return (_minSeconds + _random.NextDouble() * (_maxSeconds - _minSeconds)) * scale;
    }
}
