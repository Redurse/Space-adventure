using System;

namespace Anabiosis.Client.Rendering;

// How long ago the snapshot being drawn first arrived on screen, counted on the caller's own clock from the moment
// its tick was first seen - never from a comparison between the server's tick count and a client clock. That
// comparison depends on the two agreeing about when the game began, and they stop agreeing the moment a second
// game starts in the same process (or the server runs a tick slow): the map then drew the ship ahead of where it
// really was by its speed times the disagreement - minutes of flight - and a ship that had just left the berth
// seemed to shoot away at once. Here the age is always small, and capped.
public sealed class SnapshotAge
{
    private long _tick = -1;
    private double _since;

    public float Seconds(long tick, double now, float maxSeconds)
    {
        if (tick != _tick)
        {
            _tick = tick;
            _since = now;
        }
        return (float)Math.Clamp(now - _since, 0.0, maxSeconds);
    }
}
