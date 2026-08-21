using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// Which backdrop a given prologue slide gets - one procedural scene per beat of the pre-campaign
// history, in the order they play.
public enum PrologueVisual
{
    Ruins,
    Exodus,
    Settlement,
    Expedition,
    Artifact,
}

// Five wordless backdrops behind the prologue's narration, built from this project's usual
// "no image assets" toolkit (Starfield, HudIcons, Primitives) the same way MenuPlanetScene is -
// a handful of primitives read as a specific moment as long as each screen commits to one clear
// idea instead of trying to illustrate the whole line of text.
public static class PrologueScene
{
    // Deliberately not the shared Starfield class: its nebula patches are sized and tinted for a
    // busy main-menu pane fighting a planet and a title for attention. Blown up to the whole
    // design canvas behind a narration panel, those same patches turned into dominant, distracting
    // washes of colour - a plain twinkling field reads as "quiet backdrop" instead.
    private static Vector2[]? _starPositions;
    private static float[]? _starPhases;
    private static float[]? _starSizes;

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float totalSeconds, PrologueVisual visual)
    {
        DrawGradientBackground(spriteBatch, pixel, pane, visual);
        DrawStars(spriteBatch, pixel, pane, totalSeconds);

        switch (visual)
        {
            case PrologueVisual.Ruins:
                DrawRuins(spriteBatch, pixel, pane, totalSeconds);
                break;
            case PrologueVisual.Exodus:
                DrawExodus(spriteBatch, pixel, pane, totalSeconds);
                break;
            case PrologueVisual.Settlement:
                DrawSettlement(spriteBatch, pixel, pane, totalSeconds);
                break;
            case PrologueVisual.Expedition:
                DrawExpedition(spriteBatch, pixel, pane, totalSeconds);
                break;
            case PrologueVisual.Artifact:
                DrawArtifact(spriteBatch, pixel, pane, totalSeconds);
                break;
        }

        DrawVignette(spriteBatch, pixel, pane);
    }

    private static void DrawGradientBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, PrologueVisual visual)
    {
        // The artifact slide gets a faint sickly green cast in the sky itself - the one visual hint
        // that this find is not like the others, without saying so in the backdrop.
        var (top, bottom) = visual == PrologueVisual.Artifact
            ? (new Color(6, 12, 11), new Color(12, 24, 20))
            : (new Color(6, 9, 14), new Color(13, 19, 27));

        const int bands = 14;
        for (var i = 0; i < bands; i++)
        {
            var y = pane.Y + pane.Height * i / bands;
            var height = pane.Height / bands + 1;
            spriteBatch.Draw(pixel, new Rectangle(pane.X, y, pane.Width, height), Color.Lerp(top, bottom, i / (float)(bands - 1)));
        }
    }

    // A plain twinkling field, seeded once against the (fixed) design canvas - no nebula patches,
    // no parallax drift, just depth-through-brightness the way a still slide needs it.
    private static void DrawStars(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float totalSeconds)
    {
        if (_starPositions is null)
        {
            const int count = 150;
            var random = new Random(20260821);
            _starPositions = new Vector2[count];
            _starPhases = new float[count];
            _starSizes = new float[count];
            for (var i = 0; i < count; i++)
            {
                _starPositions[i] = new Vector2(pane.X + (float)random.NextDouble() * pane.Width, pane.Y + (float)random.NextDouble() * pane.Height);
                _starPhases[i] = (float)random.NextDouble() * MathF.PI * 2f;
                _starSizes[i] = random.NextDouble() < 0.82 ? 1f : 1.8f;
            }
        }

        for (var i = 0; i < _starPositions!.Length; i++)
        {
            var twinkleSpeed = 0.4f + (i % 5) * 0.17f;
            var alpha = 0.35f + 0.4f * (0.5f + 0.5f * MathF.Sin(totalSeconds * twinkleSpeed + _starPhases![i]));
            spriteBatch.Draw(pixel, _starPositions[i], null, Color.White * alpha, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(_starSizes![i]), SpriteEffects.None, 0f);
        }
    }

    private static void DrawSoftGlow(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color, float peakAlpha)
    {
        const int rings = 6;
        for (var i = rings; i >= 1; i--)
        {
            var r = radius * i / rings;
            var alpha = peakAlpha * (1f - (float)i / rings);
            HudIcons.FillCircle(spriteBatch, pixel, center, r, color * alpha);
        }
    }

    private static void DrawRingOutline(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color)
    {
        const int segments = 48;
        for (var i = 0; i < segments; i++)
        {
            var a0 = i * 2f * MathF.PI / segments;
            var a1 = (i + 1) * 2f * MathF.PI / segments;
            var p0 = center + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius;
            var p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            HudIcons.DrawLine(spriteBatch, pixel, p0, p1, color, 1.2f);
        }
    }

    // Slide 1: the predtechi are gone, and all that is left is a broken skyline of pillars against
    // the stars - and, half-buried in it, one light that never got the memo to switch off.
    private static void DrawRuins(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float t)
    {
        var groundY = pane.Bottom - pane.Height * 0.16f;
        spriteBatch.Draw(pixel, new Rectangle(pane.X, (int)groundY, pane.Width, pane.Bottom - (int)groundY), new Color(22, 26, 28));

        var xs = new[] { 0.10f, 0.20f, 0.30f, 0.45f, 0.58f, 0.70f, 0.84f, 0.93f };
        var heights = new[] { 0.10f, 0.24f, 0.07f, 0.32f, 0.14f, 0.05f, 0.19f, 0.09f };
        var stone = new Color(34, 40, 44);
        for (var i = 0; i < xs.Length; i++)
        {
            var w = pane.Width * 0.026f;
            var h = pane.Height * heights[i];
            var x = pane.X + pane.Width * xs[i];
            var rect = new Rectangle((int)(x - w / 2f), (int)(groundY - h), (int)w, (int)h + 4);
            spriteBatch.Draw(pixel, rect, stone);

            // Every other column lost its top to whatever happened here.
            if (i % 2 == 0)
            {
                Primitives.FillTriangle(spriteBatch, pixel,
                    new Vector2(rect.X, rect.Y),
                    new Vector2(rect.Right, rect.Y),
                    new Vector2(rect.X + rect.Width * 0.15f, rect.Y - w * 0.7f),
                    stone);
            }
        }

        // One post still drawing power, deep in the wreckage - the closest this slide comes to
        // foreshadowing the guardians without naming them.
        var lightPos = new Vector2(pane.X + pane.Width * 0.455f, groundY - pane.Height * 0.20f);
        var pulse = 0.4f + 0.25f * MathF.Sin(t * 0.8f);
        DrawSoftGlow(spriteBatch, pixel, lightPos, 9f, new Color(140, 185, 165), pulse * 0.5f);
        spriteBatch.Draw(pixel, new Rectangle((int)lightPos.X - 1, (int)lightPos.Y - 1, 2, 2), new Color(200, 235, 210) * pulse);
    }

    // Slide 2: humanity's own mothership, mid-crossing, long since past the point where anyone
    // aboard remembers the sky the voyage started under.
    private static void DrawExodus(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float t)
    {
        var center = new Vector2(pane.X + pane.Width * 0.52f, pane.Y + pane.Height * 0.46f);
        var drift = new Vector2(MathF.Sin(t * 0.05f) * 6f, MathF.Cos(t * 0.04f) * 3f);
        var position = center + drift;

        var length = pane.Width * 0.30f;
        var width = pane.Height * 0.05f;
        var hull = new Color(76, 86, 100);

        spriteBatch.Draw(pixel, position, null, hull, 0f, new Vector2(0.5f, 0.5f),
            new Vector2(length * 0.5f, width * 0.5f), SpriteEffects.None, 0f);
        Primitives.FillTriangle(spriteBatch, pixel,
            position + new Vector2(length * 0.5f, 0f),
            position + new Vector2(length * 0.36f, -width * 0.5f),
            position + new Vector2(length * 0.36f, width * 0.5f),
            hull);

        // A scattered handful of lit ports along the flank - a century of people still awake in there.
        for (var i = 0; i < 12; i++)
        {
            var lx = position.X - length * 0.44f + length * 0.82f * (i / 11f);
            var lit = MathF.Sin(t * 0.5f + i * 1.9f) > 0.1f;
            var color = lit ? new Color(215, 222, 200) : new Color(50, 56, 62);
            spriteBatch.Draw(pixel, new Rectangle((int)lx, (int)position.Y - 1, 2, 2), color * 0.85f);
        }

        var tail = position - new Vector2(length * 0.5f, 0f);
        var flicker = 0.75f + 0.15f * MathF.Sin(t * 6f) + 0.1f * MathF.Sin(t * 13f);
        DrawSoftGlow(spriteBatch, pixel, tail, 20f, new Color(150, 190, 230), 0.45f * flicker);
    }

    // Slide 3: a hundred years of just holding on - a planet with a handful of lights on its dark
    // side and one small station keeping a slow watch over it.
    private static void DrawSettlement(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float t)
    {
        var center = new Vector2(pane.X + pane.Width * 0.5f, pane.Y + pane.Height * 0.56f);
        var radius = pane.Height * 0.32f;

        DrawSoftGlow(spriteBatch, pixel, center, radius * 1.3f, new Color(110, 140, 180), 0.26f);

        var night = new Color(22, 27, 32);
        var lit = new Color(150, 175, 130);
        var r = (int)MathF.Ceiling(radius);
        for (var dx = -r; dx <= r; dx++)
        {
            var half = MathF.Sqrt(MathF.Max(0f, radius * radius - dx * dx));
            if (half < 0.5f)
                continue;
            var tt = (dx + radius) / (2f * radius);
            var color = Color.Lerp(night, lit, MathF.Pow(tt, 0.9f));
            spriteBatch.Draw(pixel, new Rectangle((int)(center.X + dx), (int)(center.Y - half), 1, (int)MathF.Ceiling(half * 2f)), color);
        }

        // A scatter of colony lights on the dark limb, each one a settlement that took the whole
        // century to build.
        foreach (var s in new[] { -0.72f, -0.55f, -0.4f, -0.28f, -0.12f })
        {
            var dx = s * radius;
            var half = MathF.Sqrt(MathF.Max(0f, radius * radius - dx * dx));
            var pos = center + new Vector2(dx, half * 0.5f);
            var twinkle = 0.5f + 0.5f * MathF.Sin(t * 2f + s * 30f);
            spriteBatch.Draw(pixel, new Rectangle((int)pos.X, (int)pos.Y, 1, 1), new Color(230, 210, 160) * (0.35f + 0.4f * twinkle));
        }

        var angle = t * 0.1f;
        var stationPos = center + new Vector2(MathF.Cos(angle) * radius * 1.75f, MathF.Sin(angle) * radius * 0.5f);
        DrawSoftGlow(spriteBatch, pixel, stationPos, 5f, Color.White, 0.35f);
        spriteBatch.Draw(pixel, new Rectangle((int)stationPos.X - 1, (int)stationPos.Y - 1, 2, 2), Color.White * 0.8f);
    }

    // Slide 4: the same outpost, now sending something outward for the first time - a ship
    // climbing away from the station while the search signal itself ripples out into the dark.
    private static void DrawExpedition(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float t)
    {
        var stationPos = new Vector2(pane.X + pane.Width * 0.5f, pane.Y + pane.Height * 0.66f);
        var hab = new Color(92, 102, 114);
        spriteBatch.Draw(pixel, stationPos, null, hab, 0f, new Vector2(0.5f, 0.5f), new Vector2(18f, 5f), SpriteEffects.None, 0f);
        spriteBatch.Draw(pixel, stationPos, null, hab, 0f, new Vector2(0.5f, 0.5f), new Vector2(5f, 18f), SpriteEffects.None, 0f);
        DrawSoftGlow(spriteBatch, pixel, stationPos, 9f, new Color(150, 190, 220), 0.4f);

        var travel = (t * 13f) % 240f;
        var shipPos = stationPos + new Vector2(travel * 0.55f, -travel);
        if (travel < 220f)
        {
            Primitives.FillTriangle(spriteBatch, pixel,
                shipPos + new Vector2(0f, -6f), shipPos + new Vector2(-4f, 5f), shipPos + new Vector2(4f, 5f),
                new Color(185, 202, 210));
            DrawSoftGlow(spriteBatch, pixel, shipPos + new Vector2(0f, 6f), 6f, new Color(150, 190, 230), 0.45f);
        }

        for (var i = 0; i < 3; i++)
        {
            var phase = (t * 0.22f + i / 3f) % 1f;
            var ringRadius = phase * pane.Height * 0.55f;
            var alpha = (1f - phase) * 0.28f;
            DrawRingOutline(spriteBatch, pixel, stationPos, ringRadius, new Color(120, 200, 185) * alpha);
        }
    }

    // Slide 5: the find itself. Everything else on screen goes quiet so this is the one thing the
    // eye has to land on - the same broken skyline as slide one, seen from the other side.
    private static void DrawArtifact(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float t)
    {
        var groundY = pane.Bottom - pane.Height * 0.12f;
        spriteBatch.Draw(pixel, new Rectangle(pane.X, (int)groundY, pane.Width, pane.Bottom - (int)groundY), new Color(20, 26, 24));
        foreach (var (x, h) in new[] { (0.16f, 0.11f), (0.30f, 0.06f), (0.70f, 0.08f), (0.86f, 0.15f) })
        {
            var w = pane.Width * 0.026f;
            var px = pane.X + pane.Width * x;
            var ph = pane.Height * h;
            spriteBatch.Draw(pixel, new Rectangle((int)(px - w / 2f), (int)(groundY - ph), (int)w, (int)ph + 4), new Color(30, 38, 36));
        }

        var center = new Vector2(pane.X + pane.Width * 0.5f, groundY - pane.Height * 0.26f);
        var pulse = 0.75f + 0.25f * MathF.Sin(t * 1.4f) + 0.1f * MathF.Sin(t * 3.7f);
        var glow = new Color(115, 200, 160);

        DrawSoftGlow(spriteBatch, pixel, center, 72f * pulse, glow, 0.26f);
        DrawSoftGlow(spriteBatch, pixel, center, 30f * pulse, glow, 0.4f);
        spriteBatch.Draw(pixel, center, null, new Color(220, 245, 225) * pulse, MathF.PI / 4f,
            new Vector2(0.5f, 0.5f), new Vector2(7f, 7f), SpriteEffects.None, 0f);

        // Thin rays rather than a burst - something clearly not inert, not something exploding.
        for (var i = 0; i < 6; i++)
        {
            var angle = i * MathF.PI / 3f + t * 0.12f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            HudIcons.DrawLine(spriteBatch, pixel, center + dir * 12f, center + dir * (26f + 6f * pulse), glow * (0.32f * pulse), 1f);
        }
    }

    private static void DrawVignette(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane)
    {
        const int bands = 10;
        for (var i = 0; i < bands; i++)
        {
            var alpha = 0.05f * (1f - (float)i / bands);
            var inset = i * 3;
            var rect = new Rectangle(pane.X + inset, pane.Y + inset, Math.Max(1, pane.Width - inset * 2), Math.Max(1, pane.Height - inset * 2));
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), Color.Black * alpha);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Color.Black * alpha);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), Color.Black * alpha);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), Color.Black * alpha);
        }
    }
}
