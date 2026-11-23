using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// What the scanner itself puts on the map: the ships it has found, and - on the console's own round screen -
// the beam the operator aims and the pulse each ping sends out. A contact is brightest the moment it is
// detected and dims over the next seconds, the way a blip fades on a phosphor screen; the server decides what
// is found, this only shows it.
public sealed partial class GalaxyMapPanel
{
    private const float ServerTicksPerSecond = 30f;
    private static readonly Color BeamColor = new(130, 255, 170);

    private void DrawScannerContacts(SpriteBatch spriteBatch, WorldSnapshot snapshot, CharacterState me, Vector2 mapOrigin, float zoom)
    {
        foreach (var contact in me.ScannerContacts ?? Array.Empty<ScannerContactState>())
        {
            var screen = FieldToScreen(mapOrigin, new Vec2(contact.X, contact.Y), zoom);
            if (!IsOnScreen(screen, 12f))
                continue;
            var color = contact.Kind switch
            {
                NpcShipKind.Cargo => Color.SteelBlue,
                NpcShipKind.Scout => Color.LightGray,
                _ => Color.OrangeRed,
            };
            var age = MathF.Max(0f, (snapshot.Tick - contact.DetectedAtTick) / ServerTicksPerSecond);
            var fresh = MathF.Exp(-age / 2.5f);
            var light = 0.45f + 0.55f * fresh;
            DrawGlow(spriteBatch, screen, 10f + 9f * fresh, color, 0.22f + 0.5f * fresh);

            // An arrowhead pointing the way the ship is heading.
            var heading = contact.RotationDegrees * (MathF.PI / 180f);
            var forward = new Vector2(MathF.Cos(heading), MathF.Sin(heading));
            var side = new Vector2(-forward.Y, forward.X);
            var hull = new[]
            {
                screen + forward * 7f, screen - forward * 5f + side * 4.5f,
                screen - forward * 2.5f, screen - forward * 5f - side * 4.5f,
            };
            Primitives.FillPolygon(spriteBatch, _pixel, screen, hull, Ink(color) * light);
            HudIcons.DrawRingArc(spriteBatch, _pixel, screen, 9.5f, 0f, 360f, Ink(color) * (0.55f * light), 18, 1.2f);
            if (age < 1.2f) // a fresh detection throws one ring outward
                HudIcons.DrawRingArc(spriteBatch, _pixel, screen, 9.5f + 20f * age / 1.2f, 0f, 360f, Ink(color) * (0.7f * (1f - age / 1.2f)), 22, 1.4f);
        }
    }

    // Console only. In circular mode a sweep line turns around the ship with its afterglow trailing behind;
    // in directional mode the cone the operator has aimed is shown as a faint wedge. Either way a ping sends
    // a wave outward that leaves a few fading rings in its wake.
    private void DrawScannerAim(SpriteBatch spriteBatch, CharacterState me, Vector2 shipCenter, Vector2 panelOrigin, float zoom, float totalSeconds)
    {
        var isCircular = me.ScannerMode == ScannerMode.Circular;
        var color = Ink(BeamColor);
        var aim = me.ScannerSweepDegrees * (MathF.PI / 180f);
        var halfCone = ScannerSweepHalfAngleDegrees * (MathF.PI / 180f);

        void Ray(float angle, float alpha, float thickness) =>
            spriteBatch.Draw(_pixel, shipCenter, null, color * alpha, angle, new Vector2(0f, 0.5f), new Vector2(RadarCircleRadius, thickness), SpriteEffects.None, 0f);

        if (isCircular)
        {
            var sweep = totalSeconds * 0.9f;
            const int afterglow = 28;
            for (var i = afterglow; i >= 1; i--)
            {
                var fade = 1f - (float)i / afterglow;
                Ray(sweep - i * 0.034f, 0.26f * fade * fade, 4f);
            }
            Ray(sweep, 0.8f, 1.6f);
        }
        else
        {
            const int fan = 16;
            var shimmer = 0.85f + 0.15f * MathF.Sin(totalSeconds * 5f);
            for (var i = 0; i <= fan; i++)
                Ray(aim - halfCone + 2f * halfCone * i / fan, 0.06f * shimmer, 3f);
            Ray(aim - halfCone, 0.35f, 1.2f);
            Ray(aim + halfCone, 0.35f, 1.2f);
            Ray(aim, 0.75f, 1.6f);
        }
        DrawScannerHandle(spriteBatch, GetScannerHandleScreen(panelOrigin, me.ScannerSweepDegrees), color);

        // How long ago the last ping fired is derived from the cooldown alone: full at the moment of firing.
        var elapsed = ScannerPingCooldownSeconds - me.ScannerCooldownRemaining;
        if (elapsed < 0f || elapsed >= ScannerPingPulseDurationSeconds)
            return;
        var progress = elapsed / ScannerPingPulseDurationSeconds;
        var reach = (isCircular ? ScannerRangeUnits / 2f : ScannerRangeUnits) * PixelsPerUnit * zoom;
        var from = isCircular ? 0f : me.ScannerSweepDegrees - ScannerSweepHalfAngleDegrees;
        var to = isCircular ? 360f : me.ScannerSweepDegrees + ScannerSweepHalfAngleDegrees;
        const int rings = 7;
        for (var ring = 0; ring < rings; ring++)
        {
            var radius = (progress - ring * 0.035f) * reach;
            if (radius <= 2f)
                break;
            var fade = 1f - (float)ring / rings;
            HudIcons.DrawRingArc(spriteBatch, _pixel, shipCenter, radius, from, to, color * ((1f - progress) * fade * fade), isCircular ? 56 : 18, ring == 0 ? 3f : 2f);
        }
    }
}
