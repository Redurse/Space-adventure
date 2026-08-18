using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// Scorch marks on a room's own hull plate while a system device inside it is damaged - separate
// from DrawBreachedWallBlock's actual holes (this plate is still intact, just burned), so a
// disabled shield generator or a knocked-out engine shows on the outside of the ship too, not
// only as a red icon on a HUD panel.
public static partial class HullSkin
{
    private static void DrawHullDamage(SpriteBatch spriteBatch, Texture2D pixel, Room room, Vector2 origin, float totalSeconds)
    {
        var rect = RoomRect(room, origin);
        // Seeded off the room's own id (same trick DrawDeckPlating already uses) so the scorch
        // pattern stays put frame to frame instead of reshuffling every draw call.
        var layout = new Random(AsteroidTexture.Seed(room.Id) + 7);
        for (var i = 0; i < 2; i++)
        {
            var center = new Vector2(
                rect.X + (float)layout.NextDouble() * rect.Width,
                rect.Y + (float)layout.NextDouble() * rect.Height);
            DrawScorch(spriteBatch, pixel, center, 9f + (float)layout.NextDouble() * 7f, layout.Next());
        }

        // A faint pulsing ember rather than a static burn mark, so a damaged plate reads as still
        // smouldering, the way DrawWallToolTargetBar's own hazard pulse already does for a wall.
        var pulse = 0.5f + 0.5f * MathF.Sin(totalSeconds * 2f + room.Id.GetHashCode());
        HudIcons.FillCircle(spriteBatch, pixel, new Vector2(rect.Center.X, rect.Center.Y), 5f + pulse * 2f,
            new Color(255, 110, 40) * (0.07f + pulse * 0.05f));
    }

    private static void DrawScorch(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, int seed)
    {
        const int sides = 7;
        var points = new Vector2[sides];
        var random = new Random(seed);
        for (var i = 0; i < sides; i++)
        {
            var angle = i * MathF.PI * 2f / sides;
            var wobble = 0.6f + (float)random.NextDouble() * 0.6f;
            points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * wobble;
        }
        Primitives.FillPolygon(spriteBatch, pixel, center, points, Color.Black * 0.4f);
        Primitives.StrokePolygon(spriteBatch, pixel, points, new Color(255, 90, 40) * 0.3f, 1.5f);
    }
}
