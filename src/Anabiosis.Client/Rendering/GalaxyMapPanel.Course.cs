using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Where the ship has been and where it is going: a fading trail behind it, and - while the autopilot holds a
// course - a line of dashes running toward the destination with the distance left written beside it.
public sealed partial class GalaxyMapPanel
{
    private static readonly Color TrailColor = new(120, 205, 255);
    private static readonly Color CourseColor = new(120, 240, 150);
    private const double TrailStepUnits = 14.0;
    private const int TrailMaxPoints = 420;
    // A gap this big between two samples is a jump or a reload, not flight - the old trail is dropped.
    private const double TrailBreakUnits = 1500.0;

    // The trail is remembered by this panel alone: it starts when the game does and is not saved.
    private readonly List<Vec2> _trail = new();
    private bool _aimingNose;
    private string? _trailSystemId;

    private void UpdateTrail(WorldSnapshot snapshot)
    {
        var position = snapshot.Voyage.ShipMapPosition;
        if (_trailSystemId != snapshot.CurrentSystemId)
        {
            _trailSystemId = snapshot.CurrentSystemId;
            _trail.Clear();
        }
        if (_trail.Count == 0)
        {
            _trail.Add(position);
            return;
        }
        var moved = (position - _trail[^1]).Length();
        if (moved > TrailBreakUnits)
        {
            _trail.Clear();
            _trail.Add(position);
        }
        else if (moved >= TrailStepUnits)
        {
            _trail.Add(position);
            if (_trail.Count > TrailMaxPoints)
                _trail.RemoveAt(0);
        }
    }

    private void DrawTrail(SpriteBatch spriteBatch, Vector2 mapOrigin, float zoom, Vector2 shipCenter)
    {
        if (_trail.Count < 2)
            return;
        // Zoomed far out the samples sit a pixel or two apart; skipping some keeps the line from being
        // drawn as hundreds of specks.
        var stride = Math.Max(1, (int)(4f / (float)(TrailStepUnits * PixelsPerUnit * zoom)));
        var previous = shipCenter;
        for (var i = _trail.Count - 1; i >= 0; i -= stride)
        {
            var point = FieldToScreen(mapOrigin, _trail[i], zoom);
            var age = 1f - (float)i / _trail.Count; // 0 at the ship, 1 at the far end
            if (IsOnScreen(point, 40f) || IsOnScreen(previous, 40f))
                HudIcons.DrawLine(spriteBatch, _pixel, previous, point, Ink(TrailColor * (0.55f * (1f - age) * (1f - age))), 1.6f);
            previous = point;
        }
    }

    private void DrawCourse(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 mapOrigin, float zoom, Vector2 shipCenter, float totalSeconds)
    {
        if (snapshot.Autopilot is not { IsActive: true, DestinationX: { } destX, DestinationY: { } destY })
            return;
        var destination = new Vec2(destX, destY);
        var destScreen = FieldToScreen(mapOrigin, destination, zoom);
        var color = Ink(CourseColor);

        DrawDashedLine(spriteBatch, shipCenter, destScreen, color * 0.75f, dash: 9f, gap: 6f, phase: totalSeconds * 26f, thickness: 1.6f);

        // The destination: a steady ring, a second one breathing outward, and four ticks.
        var pulse = totalSeconds * 0.9f % 1f;
        DrawGlow(spriteBatch, destScreen, 20f, CourseColor, 0.35f);
        HudIcons.DrawRingArc(spriteBatch, _pixel, destScreen, 8f, 0f, 360f, color, 20, 1.8f);
        HudIcons.DrawRingArc(spriteBatch, _pixel, destScreen, 8f + 12f * pulse, 0f, 360f, color * (0.6f * (1f - pulse)), 24, 1.3f);
        foreach (var direction in new[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY })
            HudIcons.DrawLine(spriteBatch, _pixel, destScreen + direction * 11f, destScreen + direction * 16f, color, 1.6f);

        if (_aimingNose)
            DrawAutopilotGhostHull(spriteBatch, snapshot, destScreen, destination, zoom, snapshot.Autopilot.PredictedFacingDegrees);

        if (IsOnScreen(destScreen, 0f))
            DrawPlate(spriteBatch, destScreen + new Vector2(0f, 20f), "КУРС", "осталось " + FormatDistance((destination - _shipMapPosition).Length()), CourseColor, emphasised: false);
    }

    // Dashes that march from one point toward the other. Only the part of the line near the screen is walked,
    // so a destination thousands of pixels away costs no more than a near one.
    private void DrawDashedLine(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color color, float dash, float gap, float phase, float thickness)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length < 1f)
            return;
        var direction = delta / length;
        var period = dash + gap;

        // The stretch of the line that can be on screen: the closest approach to the middle of the view,
        // plus and minus the view's own reach.
        var middle = _sonar ? _radarCenter : new Vector2(_bounds.Center.X, _bounds.Center.Y);
        var reach = _sonar ? RadarCircleRadius + 20f : new Vector2(_bounds.Width, _bounds.Height).Length() / 2f + 20f;
        var along = Vector2.Dot(middle - from, direction);
        var start = MathF.Max(0f, along - reach);
        var end = MathF.Min(length, along + reach);
        if (end <= start)
            return;

        var offset = phase % period;
        var first = MathF.Floor((start - offset) / period) * period + offset;
        for (var at = first; at < end; at += period)
        {
            var a = MathF.Max(at, 0f);
            var b = MathF.Min(at + dash, length);
            if (b - a > 0.5f)
                HudIcons.DrawLine(spriteBatch, _pixel, from + direction * a, from + direction * b, color, thickness);
        }
    }
}
