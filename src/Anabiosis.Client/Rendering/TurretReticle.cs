using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// The gunner's own scope-style reticle while manning a turret - drawn in the unrotated HUD batch
// at the raw cursor position, not the rotated/zoomed scene, so it always sits exactly under the
// mouse regardless of TurretViewRotationDegrees (Game1.cs). It tracks the cursor directly rather
// than the turret's actual (slower, traversing) AimDegrees, so the player always sees exactly
// where they're pointing even while the barrel is still catching up to it.
public static class TurretReticle
{
    private const float OuterRadius = 42f;
    private const float InnerRadius = 13f;
    private const float CrosshairGap = InnerRadius + 4f;
    private const float CrosshairLength = OuterRadius + 14f;
    private const float TickGap = OuterRadius + 6f;
    private const float TickLength = 14f;
    private const int CircleSegments = 28;
    private const float LineThickness = 1.5f;

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, Color color)
    {
        DrawRing(spriteBatch, pixel, center, OuterRadius, color);
        DrawRing(spriteBatch, pixel, center, InnerRadius, color);

        // Crosshair through N/S/E/W, gapped around the inner ring so the exact aim point in the
        // middle stays clear rather than buried under crossing lines.
        DrawLine(spriteBatch, pixel, center + new Vector2(0, -CrosshairGap), center + new Vector2(0, -CrosshairLength), color);
        DrawLine(spriteBatch, pixel, center + new Vector2(0, CrosshairGap), center + new Vector2(0, CrosshairLength), color);
        DrawLine(spriteBatch, pixel, center + new Vector2(-CrosshairGap, 0), center + new Vector2(-CrosshairLength, 0), color);
        DrawLine(spriteBatch, pixel, center + new Vector2(CrosshairGap, 0), center + new Vector2(CrosshairLength, 0), color);

        // Four corner ticks, angled diagonally just outside the ring.
        foreach (var angleDegrees in new[] { 45f, 135f, 225f, 315f })
        {
            var radians = angleDegrees * (MathF.PI / 180f);
            var direction = new Vector2(MathF.Cos(radians), MathF.Sin(radians));
            DrawLine(spriteBatch, pixel, center + direction * TickGap, center + direction * (TickGap + TickLength), color);
        }

        spriteBatch.Draw(pixel, new Rectangle((int)center.X - 1, (int)center.Y - 1, 3, 3), color);
    }

    private static void DrawRing(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color)
    {
        for (var i = 0; i < CircleSegments; i++)
        {
            var a0 = MathF.Tau * i / CircleSegments;
            var a1 = MathF.Tau * (i + 1) / CircleSegments;
            var p0 = center + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius;
            var p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            DrawLine(spriteBatch, pixel, p0, p1, color);
        }
    }

    private static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, Vector2 to, Color color)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length < 0.5f)
            return;
        var rotation = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(pixel, from, null, color, rotation, Vector2.Zero, new Vector2(length, LineThickness), SpriteEffects.None, 0f);
    }
}
