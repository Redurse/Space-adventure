using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// The studio's opening scene, shown while the game loads (and played out to the end after it): a ring
// that draws itself around a stylised "A" with a small planet orbiting it, then the name rising letter
// by letter, a light glint sweeping across it, and a fade to black. Everything is drawn from primitives
// (no image assets) and is a pure function of time `t`, so it can be redrawn at any point of the load.
public static class StudioSplash
{
    public const string Name = "ANDREY";
    public const string Subtitle = "STUDIOS";
    public const float Duration = 4.8f;
    public const float FadeOutSeconds = 0.7f;
    // Before this the scene cannot be skipped (a key press during the first moments is usually just the
    // player's hand still on the keyboard from launching the game).
    public const float SkippableAfter = 1.0f;

    private static readonly Color Ice = new(120, 196, 255);
    private static readonly Color IceDim = new(70, 130, 200);
    private static readonly Color Warm = new(255, 214, 140);

    public static bool IsDone(float t) => t >= Duration;

    public static float Smooth(float x)
    {
        x = MathHelper.Clamp(x, 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    // 0 before `start`, 1 after start+length, eased between.
    public static float Step(float t, float start, float length) => Smooth((t - start) / length);

    // How much of the ring has been drawn (0..1).
    public static float RingProgress(float t) => Step(t, 0.35f, 1.2f);
    // How far the two legs and crossbar of the A have grown (0..1).
    public static float GlyphProgress(float t) => Step(t, 0.9f, 1.0f);
    public static float LetterAlpha(float t, int index) => Step(t, 1.7f + index * 0.09f, 0.4f);
    public static float SubtitleAlpha(float t, int index) => Step(t, 2.35f + index * 0.07f, 0.4f);
    // 1 for most of the scene, 0 at the very end.
    public static float Visibility(float t) => 1f - Step(t, Duration - FadeOutSeconds, FadeOutSeconds);

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, int width, int height, float t, float? loadProgress = null)
    {
        var s = height / 1080f;
        var center = new Vector2(width / 2f, height / 2f - 60f * s);
        var fade = Visibility(t);

        spriteBatch.Draw(pixel, new Rectangle(0, 0, width, height), new Color(4, 6, 12));
        DrawGlow(spriteBatch, pixel, center, s, t, fade);
        DrawStars(spriteBatch, pixel, width, height, t, fade);
        DrawEmblem(spriteBatch, pixel, center, s, t, fade);
        DrawName(spriteBatch, pixel, font, new Vector2(width / 2f, center.Y + 170f * s), s, t, fade);

        if (loadProgress is { } progress && t < Duration)
            DrawLoadBar(spriteBatch, pixel, font, width, height, s, progress, fade);
    }

    private static void DrawGlow(SpriteBatch sb, Texture2D pixel, Vector2 center, float s, float t, float fade)
    {
        var rise = Step(t, 0.1f, 1.4f) * fade;
        for (var i = 0; i < 9; i++)
        {
            var radius = (520f - i * 52f) * s;
            HudIcons.FillCircle(sb, pixel, center, radius, new Color(30, 70, 130) * (0.018f * rise));
        }
    }

    private static void DrawStars(SpriteBatch sb, Texture2D pixel, int width, int height, float t, float fade)
    {
        var appear = Step(t, 0.05f, 1.0f) * fade;
        for (var i = 0; i < 70; i++)
        {
            var x = Hash(i * 2) * width;
            var y = Hash(i * 2 + 1) * height;
            var twinkle = 0.45f + 0.55f * MathF.Sin(t * (0.8f + Hash(i + 300) * 1.6f) + i);
            var size = Hash(i + 700) > 0.85f ? 3 : 2;
            sb.Draw(pixel, new Rectangle((int)x, (int)y, size, size), new Color(190, 215, 255) * (0.5f * twinkle * appear));
        }
    }

    private static float Hash(int n)
    {
        unchecked
        {
            var x = (uint)(n * 374761393 + 668265263);
            x = (x ^ (x >> 13)) * 1274126177;
            x ^= x >> 16;
            return (x & 0xFFFFFF) / (float)0x1000000;
        }
    }

    private static void DrawEmblem(SpriteBatch sb, Texture2D pixel, Vector2 c, float s, float t, float fade)
    {
        var ring = RingProgress(t);
        var radius = 118f * s;

        // The ring, drawn as an arc that grows clockwise from the top; a faint full track sits under it.
        HudIcons.DrawRingArc(sb, pixel, c, radius, 0f, 360f, IceDim * (0.18f * ring * fade), 96, 2f * s);
        if (ring > 0.001f)
            HudIcons.DrawRingArc(sb, pixel, c, radius, -90f, -90f + 360f * ring, Ice * fade, 96, 5f * s);

        // Orbiting planet: rides the ring once it has finished drawing, with a short comet tail behind it.
        var orbit = Step(t, 1.2f, 0.5f);
        if (orbit > 0f)
        {
            var angle = -90f + 360f * (t - 1.2f) * 0.22f;
            for (var i = 6; i >= 0; i--)
            {
                var a = (angle - i * 5f) * (MathF.PI / 180f);
                var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * radius;
                HudIcons.FillCircle(sb, pixel, p, (9f - i) * s, Warm * (0.16f * (7 - i) / 7f * orbit * fade));
            }
            var head = angle * (MathF.PI / 180f);
            var pos = c + new Vector2(MathF.Cos(head), MathF.Sin(head)) * radius;
            HudIcons.FillCircle(sb, pixel, pos, 15f * s, Warm * (0.22f * orbit * fade));
            HudIcons.FillCircle(sb, pixel, pos, 9f * s, Warm * (orbit * fade));
            HudIcons.FillCircle(sb, pixel, pos, 4f * s, Color.White * (orbit * fade));
        }

        // The A: two legs rising to an apex and a crossbar, each growing out from where it starts.
        var grow = GlyphProgress(t);
        if (grow > 0f)
        {
            var apex = c + new Vector2(0f, -72f * s);
            var left = c + new Vector2(-60f * s, 56f * s);
            var right = c + new Vector2(60f * s, 56f * s);
            var leg = Math.Clamp(grow * 1.5f, 0f, 1f);
            var bar = Math.Clamp((grow - 0.4f) / 0.6f, 0f, 1f);
            var thick = 11f * s;
            HudIcons.DrawLine(sb, pixel, left, Vector2.Lerp(left, apex, leg), Color.White * fade, thick);
            HudIcons.DrawLine(sb, pixel, right, Vector2.Lerp(right, apex, leg), Color.White * fade, thick);
            var barL = Vector2.Lerp(c + new Vector2(-4f * s, 12f * s), c + new Vector2(-33f * s, 12f * s), bar);
            var barR = Vector2.Lerp(c + new Vector2(4f * s, 12f * s), c + new Vector2(33f * s, 12f * s), bar);
            HudIcons.DrawLine(sb, pixel, barL, barR, Ice * fade, 9f * s);
        }

        // A four-point glint on the apex once the A is complete, flaring and settling.
        var glint = Step(t, 1.9f, 0.3f) * (1f - Step(t, 2.2f, 0.6f) * 0.7f);
        if (glint > 0f)
        {
            var apex = c + new Vector2(0f, -72f * s);
            var len = 46f * s * glint;
            HudIcons.DrawLine(sb, pixel, apex + new Vector2(-len, 0), apex + new Vector2(len, 0), Color.White * (0.9f * fade), 2.5f * s);
            HudIcons.DrawLine(sb, pixel, apex + new Vector2(0, -len), apex + new Vector2(0, len), Color.White * (0.9f * fade), 2.5f * s);
            HudIcons.FillCircle(sb, pixel, apex, 8f * s * glint, Color.White * (0.7f * fade));
        }
    }

    private static void DrawName(SpriteBatch sb, Texture2D pixel, SpriteFont font, Vector2 center, float s, float t, float fade)
    {
        // "ANDREY" large, letter by letter; "STUDIOS" smaller and spaced wide beneath it.
        DrawSpacedText(sb, font, Name, center, 2.7f * s, 12f * s, Color.White, t, LetterAlpha, 22f * s, fade);
        var rule = Step(t, 2.15f, 0.6f);
        if (rule > 0f)
        {
            var half = 300f * s * rule;
            var y = center.Y + 52f * s;
            sb.Draw(pixel, new Rectangle((int)(center.X - half), (int)y, (int)(half * 2f), Math.Max(1, (int)(2f * s))), Ice * (0.8f * fade));
        }
        DrawSpacedText(sb, font, Subtitle, center + new Vector2(0f, 98f * s), 1.5f * s, 30f * s, Ice, t, SubtitleAlpha, 14f * s, fade);

        // A light glint crossing the name once it has all arrived.
        var sweep = (t - 3.0f) / 0.9f;
        if (sweep is > 0f and < 1f)
        {
            var x = MathHelper.Lerp(center.X - 360f * s, center.X + 360f * s, sweep);
            var top = (int)(center.Y - 36f * s);
            var heightPx = (int)(72f * s);
            for (var i = 0; i < 5; i++)
                sb.Draw(pixel, new Rectangle((int)(x - (i + 1) * 10f * s), top, (int)(10f * s), heightPx), Color.White * ((0.10f - i * 0.018f) * fade));
        }
    }

    private static void DrawSpacedText(SpriteBatch sb, SpriteFont font, string text, Vector2 center, float scale, float spacing,
        Color color, float t, Func<float, int, float> alphaOf, float rise, float fade)
    {
        var widths = new float[text.Length];
        var total = 0f;
        for (var i = 0; i < text.Length; i++)
        {
            widths[i] = font.MeasureString(text[i].ToString()).X * scale;
            total += widths[i] + (i < text.Length - 1 ? spacing : 0f);
        }

        var x = center.X - total / 2f;
        var lineHeight = font.MeasureString("A").Y * scale;
        for (var i = 0; i < text.Length; i++)
        {
            var a = alphaOf(t, i);
            if (a > 0f)
            {
                var position = new Vector2(x, center.Y - lineHeight / 2f + (1f - a) * rise);
                sb.DrawString(font, text[i].ToString(), position + new Vector2(2f, 3f) * scale * 0.5f, Color.Black * (0.5f * a * fade), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                sb.DrawString(font, text[i].ToString(), position, color * (a * fade), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            x += widths[i] + spacing;
        }
    }

    private static void DrawLoadBar(SpriteBatch sb, Texture2D pixel, SpriteFont font, int width, int height, float s, float progress, float fade)
    {
        var barWidth = (int)(360f * s);
        var bar = new Rectangle((width - barWidth) / 2, (int)(height - 96f * s), barWidth, Math.Max(2, (int)(4f * s)));
        sb.Draw(pixel, bar, new Color(40, 56, 84) * fade);
        sb.Draw(pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * MathHelper.Clamp(progress, 0f, 1f)), bar.Height), Ice * fade);
        const string label = "ЗАГРУЗКА";
        var size = font.MeasureString(label) * 0.5f * s;
        sb.DrawString(font, label, new Vector2((width - size.X) / 2f, bar.Y - size.Y - 8f * s), IceDim * fade, 0f, Vector2.Zero, 0.5f * s, SpriteEffects.None, 0f);
    }
}
