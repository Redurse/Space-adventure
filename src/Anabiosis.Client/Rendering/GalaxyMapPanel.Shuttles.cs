using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The shuttles out of their bays, on the helm's map: the craft itself, a dashed line to where it was sent, a ring and a hint when it is
// the one picked to give orders to (a click on the map then sends it there, a click on the ship calls it home).
public sealed partial class GalaxyMapPanel
{
    private static readonly Color ShuttleColor = new(130, 215, 255);

    // The bay id of the shuttle picked on the map, set by the game before drawing; null when none is.
    public string? SelectedShuttleBayId { get; set; }

    // How close, in pixels, a click has to be to pick a shuttle, and to the ship to call it home.
    public const float ShuttlePickRadius = 16f;
    public const float ShipCallRadius = 26f;

    // Where a shuttle is on the screen, carried forward along its velocity for as long as this snapshot has been on show.
    private Vector2 ShuttleScreenPosition(ShuttleState shuttle, Vector2 mapOrigin, float zoom, float extrapolationSeconds) =>
        FieldToScreen(mapOrigin, new Vec2(shuttle.X + shuttle.VelocityX * extrapolationSeconds, shuttle.Y + shuttle.VelocityY * extrapolationSeconds), zoom);

    // Where the shuttle marker is, for a click to be read against it (the same maths as the drawing, without the carrying forward).
    public static Vector2 FieldPointToScreen(Vec2 point, Vector2 mapOrigin, float zoom) => FieldToScreen(mapOrigin, point, zoom);

    public static Vector2 ShuttleClickPosition(ShuttleState shuttle, Vector2 mapOrigin, float zoom) =>
        FieldToScreen(mapOrigin, new Vec2(shuttle.X, shuttle.Y), zoom);

    private void DrawShuttles(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 mapOrigin, float zoom, float extrapolationSeconds, float totalSeconds)
    {
        if (snapshot.Shuttles is not { Count: > 0 } shuttles)
            return;
        foreach (var shuttle in shuttles)
        {
            if (shuttle.Phase is ShuttlePhase.Docked or ShuttlePhase.Opening)
                continue;
            var center = ShuttleScreenPosition(shuttle, mapOrigin, zoom, extrapolationSeconds);
            if (!IsOnScreen(center, 40f))
                continue;
            var selected = shuttle.BayId == SelectedShuttleBayId;

            if (shuttle.HasDestination)
            {
                var destination = FieldToScreen(mapOrigin, new Vec2(shuttle.DestinationX, shuttle.DestinationY), zoom);
                DrawDashedLine(spriteBatch, center, destination, ShuttleColor * 0.7f);
                HudIcons.DrawRingArc(spriteBatch, _pixel, destination, 5f, 0f, 360f, ShuttleColor * 0.9f, 14, 1.4f);
            }
            else if (shuttle.Phase == ShuttlePhase.Returning)
            {
                HudIcons.DrawRingArc(spriteBatch, _pixel, center, 11f, 0f, 360f, new Color(255, 214, 120) * 0.6f, 20, 1.2f);
            }

            var moving = shuttle.VelocityX * shuttle.VelocityX + shuttle.VelocityY * shuttle.VelocityY > 4f;
            var scale = MathF.Max(PixelsPerUnit * zoom, 2.4f);
            ShuttleArt.Draw(spriteBatch, _pixel, center, shuttle.RotationDegrees * MathF.PI / 180f, scale, moving, 1f, totalSeconds);

            if (selected)
            {
                var pulse = 0.5f + 0.5f * MathF.Sin(totalSeconds * 5f);
                HudIcons.DrawRingArc(spriteBatch, _pixel, center, 14f + 2f * pulse, 0f, 360f, Color.White * 0.9f, 28, 1.6f);
            }
            var label = shuttle.Phase == ShuttlePhase.Returning ? "Шаттл (домой)" : "Шаттл";
            spriteBatch.DrawString(_font, label, center + new Vector2(14f, -6f), (selected ? Color.White : ShuttleColor) * 0.9f, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
        }
    }

    private void DrawDashedLine(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color color)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length < 4f)
            return;
        var direction = delta / length;
        for (var at = 0f; at < length; at += 12f)
            HudIcons.DrawLine(spriteBatch, _pixel, from + direction * at, from + direction * MathF.Min(length, at + 6f), color, 1.2f);
    }
}
