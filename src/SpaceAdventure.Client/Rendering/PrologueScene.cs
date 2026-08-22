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
// "no image assets" toolkit (HudIcons, Primitives) the same way MenuPlanetScene is - each one
// layers several small procedural details (parallax ruins, habitat rings, cloud bands, a dig
// site) rather than one flat silhouette, so the slide still reads as a specific, busy place
// instead of a single icon centred on a gradient.
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
        DrawDustMotes(spriteBatch, pixel, pane, totalSeconds);

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
            const int count = 170;
            var random = new Random(20260821);
            _starPositions = new Vector2[count];
            _starPhases = new float[count];
            _starSizes = new float[count];
            for (var i = 0; i < count; i++)
            {
                _starPositions[i] = new Vector2(pane.X + (float)random.NextDouble() * pane.Width, pane.Y + (float)random.NextDouble() * pane.Height);
                _starPhases[i] = (float)random.NextDouble() * MathF.PI * 2f;
                _starSizes[i] = random.NextDouble() < 0.8 ? 1f : random.NextDouble() < 0.95 ? 1.8f : 2.6f;
            }
        }

        for (var i = 0; i < _starPositions!.Length; i++)
        {
            var twinkleSpeed = 0.4f + (i % 5) * 0.17f;
            var alpha = 0.35f + 0.4f * (0.5f + 0.5f * MathF.Sin(totalSeconds * twinkleSpeed + _starPhases![i]));
            var size = _starSizes![i];
            spriteBatch.Draw(pixel, _starPositions[i], null, Color.White * alpha, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(size), SpriteEffects.None, 0f);

            // The handful of biggest stars get a faint four-point sparkle rather than just a bigger
            // dot - the one bit of traditional "space art" polish, kept rare so it stays a highlight.
            if (size <= 2f)
                continue;
            var sparkle = _starPositions[i];
            var sparkleAlpha = alpha * 0.5f;
            HudIcons.DrawLine(spriteBatch, pixel, sparkle - new Vector2(5f, 0f), sparkle + new Vector2(5f, 0f), Color.White * sparkleAlpha, 0.6f);
            HudIcons.DrawLine(spriteBatch, pixel, sparkle - new Vector2(0f, 5f), sparkle + new Vector2(0f, 5f), Color.White * sparkleAlpha, 0.6f);
        }
    }

    // Faint drifting specks well in front of the starfield - cabin-window dust rather than stars,
    // the same trick MenuPlanetScene uses to keep a still frame from reading as a photograph.
    private static void DrawDustMotes(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float t)
    {
        const int count = 26;
        for (var i = 0; i < count; i++)
        {
            var seedX = (i * 71 + (i * i * 13) % 53) % pane.Width;
            var seedY = (i * 37 + (i * i * 5) % 61) % pane.Height;
            var speed = 3f + (i % 5);
            var x = pane.X + Wrap(seedX + t * speed, pane.Width);
            var y = pane.Y + Wrap(seedY + t * speed * 0.3f, pane.Height);
            var alpha = 0.05f + 0.07f * MathF.Abs(MathF.Sin(i * 1.7f + t * 0.4f));
            spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, 1, 1), new Color(200, 215, 220) * alpha);
        }
    }

    private static float Wrap(float value, float size) => (value % size + size) % size;

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

    // A general tilted ellipse outline - planet rings, the dig site's pit rim, the artifact's halo.
    private static void DrawEllipseOutline(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radiusX, float radiusY, float tilt, Color color, float thickness = 1.2f)
    {
        const int segments = 56;
        var cosT = MathF.Cos(tilt);
        var sinT = MathF.Sin(tilt);
        Vector2? previous = null;
        for (var i = 0; i <= segments; i++)
        {
            var angle = i * 2f * MathF.PI / segments;
            var local = new Vector2(MathF.Cos(angle) * radiusX, MathF.Sin(angle) * radiusY);
            var rotated = new Vector2(local.X * cosT - local.Y * sinT, local.X * sinT + local.Y * cosT);
            var point = center + rotated;
            if (previous is { } prev)
                HudIcons.DrawLine(spriteBatch, pixel, prev, point, color, thickness);
            previous = point;
        }
    }

    // Same idea as MenuPlanetScene's own ring-half trick: only the half on one side of the tilt
    // axis is drawn, so the other half can sit behind the planet body and this one in front of it.
    private static void DrawPlanetRingHalf(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radiusX, float radiusY, float tilt, bool front, Color color)
    {
        const int segments = 56;
        var cosT = MathF.Cos(tilt);
        var sinT = MathF.Sin(tilt);
        Vector2? previous = null;
        for (var i = 0; i <= segments; i++)
        {
            var angle = i * 2f * MathF.PI / segments;
            var localY = MathF.Sin(angle) * radiusY;
            if ((localY >= 0f) != front)
            {
                previous = null;
                continue;
            }
            var localX = MathF.Cos(angle) * radiusX;
            var rotated = new Vector2(localX * cosT - localY * sinT, localX * sinT + localY * cosT);
            var point = center + rotated;
            if (previous is { } prev)
                HudIcons.DrawLine(spriteBatch, pixel, prev, point, color, front ? 1.6f : 1.1f);
            previous = point;
        }
    }

    // Slide 1: the predtechi are gone, and all that is left is a broken skyline of pillars against
    // the stars - a farther, hazier row behind the near one so it reads as a dead city rather than
    // eight columns in a field, plus one light that never got the memo to switch off.
    private static void DrawRuins(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float t)
    {
        var groundY = pane.Bottom - pane.Height * 0.16f;

        var farGroundY = groundY - pane.Height * 0.05f;
        var farXs = new[] { 0.04f, 0.14f, 0.24f, 0.38f, 0.5f, 0.63f, 0.75f, 0.88f, 0.97f };
        var farHeights = new[] { 0.05f, 0.09f, 0.04f, 0.12f, 0.06f, 0.08f, 0.03f, 0.10f, 0.05f };
        var haze = new Color(30, 38, 44) * 0.55f;
        for (var i = 0; i < farXs.Length; i++)
        {
            var w = pane.Width * 0.016f;
            var h = pane.Height * farHeights[i];
            var x = pane.X + pane.Width * farXs[i];
            spriteBatch.Draw(pixel, new Rectangle((int)(x - w / 2f), (int)(farGroundY - h), (int)w, (int)h + 3), haze);
        }
        // A broken ring on the horizon, far behind everything - one of their own orbital
        // structures, glimpsed as a silhouette rather than explained.
        DrawEllipseOutline(spriteBatch, pixel, new Vector2(pane.X + pane.Width * 0.78f, farGroundY - pane.Height * 0.10f),
            pane.Width * 0.10f, pane.Height * 0.05f, 0.1f, new Color(40, 48, 54) * 0.35f, 1f);

        spriteBatch.Draw(pixel, new Rectangle(pane.X, (int)groundY, pane.Width, pane.Bottom - (int)groundY), new Color(22, 26, 28));

        var xs = new[] { 0.10f, 0.20f, 0.30f, 0.45f, 0.58f, 0.70f, 0.84f, 0.93f };
        var heights = new[] { 0.10f, 0.24f, 0.07f, 0.32f, 0.14f, 0.05f, 0.19f, 0.09f };
        var stone = new Color(34, 40, 44);
        var rects = new Rectangle[xs.Length];
        for (var i = 0; i < xs.Length; i++)
        {
            var w = pane.Width * 0.026f;
            var h = pane.Height * heights[i];
            var x = pane.X + pane.Width * xs[i];
            var rect = new Rectangle((int)(x - w / 2f), (int)(groundY - h), (int)w, (int)h + 4);
            rects[i] = rect;
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

            // Rubble at the foot of every column - nothing here stands on clean ground.
            for (var d = 0; d < 3; d++)
            {
                var rubbleWidth = w * (0.3f + 0.15f * d);
                var rubbleX = rect.X + (d - 1) * w * 0.8f;
                spriteBatch.Draw(pixel, new Rectangle((int)rubbleX, (int)groundY - 2, (int)rubbleWidth, 4), stone);
            }
        }

        // Two columns still share a lintel - the one piece of architecture whole enough to read
        // as a gate rather than rubble.
        var gateA = rects[1];
        var gateB = rects[3];
        var lintelTop = Math.Min(gateA.Y, gateB.Y) - 3;
        spriteBatch.Draw(pixel, new Rectangle(gateA.Center.X, lintelTop, gateB.Center.X - gateA.Center.X, 5), stone);

        // One post still drawing power, deep in the wreckage - the closest this slide comes to
        // foreshadowing the guardians without naming them. A faint vertical wash above it reads
        // as a working light rather than just a bright pixel.
        var lightPos = new Vector2(pane.X + pane.Width * 0.455f, groundY - pane.Height * 0.20f);
        var pulse = 0.4f + 0.25f * MathF.Sin(t * 0.8f);
        for (var i = 0; i < 8; i++)
        {
            var fi = i / 7f;
            var beamPos = lightPos - new Vector2(0f, fi * pane.Height * 0.14f);
            spriteBatch.Draw(pixel, beamPos, null, new Color(140, 185, 165) * (pulse * 0.12f * (1f - fi)), 0f,
                new Vector2(0.5f, 0.5f), new Vector2(3f, 3f), SpriteEffects.None, 0f);
        }
        DrawSoftGlow(spriteBatch, pixel, lightPos, 9f, new Color(140, 185, 165), pulse * 0.5f);
        spriteBatch.Draw(pixel, new Rectangle((int)lightPos.X - 1, (int)lightPos.Y - 1, 2, 2), new Color(200, 235, 210) * pulse);

        // Thin cracks running from the lit post into the ground - the last of its wiring still
        // faintly warm.
        foreach (var angle in new[] { -0.5f, -0.15f, 0.2f, 0.55f })
        {
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.3f + 0.5f);
            HudIcons.DrawLine(spriteBatch, pixel, lightPos, lightPos + dir * pane.Width * 0.05f,
                new Color(120, 165, 150) * (pulse * 0.25f), 1f);
        }
    }

    // Slide 2: humanity's own mothership, mid-crossing, wearing a century of add-ons - habitat
    // rings, radiator fins, a small escort pair - long since past the point where anyone aboard
    // remembers the sky the voyage started under.
    private static void DrawExodus(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float t)
    {
        // A single small, subdued nebula patch tucked into a back corner - richness without
        // repeating the earlier mistake of nebulae big enough to fight the scene for attention.
        DrawSoftGlow(spriteBatch, pixel, new Vector2(pane.Right - pane.Width * 0.12f, pane.Y + pane.Height * 0.15f),
            pane.Width * 0.14f, new Color(70, 60, 120), 0.05f);

        var center = new Vector2(pane.X + pane.Width * 0.52f, pane.Y + pane.Height * 0.46f);
        var drift = new Vector2(MathF.Sin(t * 0.05f) * 6f, MathF.Cos(t * 0.04f) * 3f);
        var position = center + drift;

        // spriteBatch.Draw's "scale" parameter is the sprite's *total* rendered size on a 1x1
        // pixel texture (not a half-extent), so every half-extent used below is computed once,
        // up front, and the body/nose/fins/windows all key off these same numbers - the previous
        // version mixed the two and left the nose cone floating off the hull's actual edge.
        var hullHalfLength = pane.Width * 0.08f;
        var hullHalfHeight = pane.Height * 0.014f;
        var noseTip = hullHalfLength * 1.5f;
        var hull = new Color(78, 88, 102);
        var hullDark = new Color(52, 60, 72);
        var hullFaint = new Color(44, 51, 61);

        // Two counter-tilted habitat rings around the midsection - the one detail that makes this
        // read as a generation ship rather than a generic hull.
        DrawEllipseOutline(spriteBatch, pixel, position, hullHalfLength * 1.5f, hullHalfHeight * 5.5f, 0.5f + t * 0.06f, hullDark, 1.6f);
        DrawEllipseOutline(spriteBatch, pixel, position, hullHalfLength * 1.9f, hullHalfHeight * 7f, -0.4f - t * 0.05f, hullFaint, 1.3f);

        spriteBatch.Draw(pixel, position, null, hull, 0f, new Vector2(0.5f, 0.5f),
            new Vector2(hullHalfLength * 2f, hullHalfHeight * 2f), SpriteEffects.None, 0f);
        // Nose cone, base flush against the hull's real right edge - no gap between the two.
        Primitives.FillTriangle(spriteBatch, pixel,
            position + new Vector2(noseTip, 0f),
            position + new Vector2(hullHalfLength, -hullHalfHeight),
            position + new Vector2(hullHalfLength, hullHalfHeight),
            hull);

        // Radiator fins, top and bottom of the midsection - meant to stick out past the hull, the
        // way the reference Katyusha's wheels stick out past its own body.
        foreach (var finSide in new[] { -1f, 1f })
        {
            var finRoot = position + new Vector2(-hullHalfLength * 0.2f, finSide * hullHalfHeight);
            var finCenter = position + new Vector2(-hullHalfLength * 0.2f, finSide * hullHalfHeight * 4.5f);
            HudIcons.DrawLine(spriteBatch, pixel, finRoot, finCenter, hullDark, 1.4f);
            spriteBatch.Draw(pixel, finCenter, null, hullDark, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(hullHalfLength * 0.55f, hullHalfHeight * 3f), SpriteEffects.None, 0f);
        }

        // A thin dish antenna, the only thing on the hull actually pointed somewhere.
        var antennaBase = position + new Vector2(-hullHalfLength * 0.6f, -hullHalfHeight);
        var antennaTip = antennaBase + new Vector2(-4f, -14f);
        HudIcons.DrawLine(spriteBatch, pixel, antennaBase, antennaTip, hullDark, 1.2f);
        HudIcons.FillCircle(spriteBatch, pixel, antennaTip, 2.2f, hullDark);

        // Two rows of lit ports along the hull body itself (not the nose) - a century of people
        // still awake in there.
        foreach (var row in new[] { -1f, 1f })
        {
            for (var i = 0; i < 9; i++)
            {
                var lx = position.X - hullHalfLength * 0.85f + hullHalfLength * 1.6f * (i / 8f);
                var ly = position.Y + row * hullHalfHeight * 0.6f;
                var lit = MathF.Sin(t * 0.5f + i * 1.9f + row) > 0.1f;
                var color = lit ? new Color(220, 225, 200) : new Color(58, 64, 72);
                spriteBatch.Draw(pixel, new Rectangle((int)lx, (int)ly, 2, 2), color * 0.9f);
            }
        }

        // Three engines rather than one, fanned slightly so the tail doesn't read as a single
        // point - sat right at the hull's real left edge.
        var tailBase = position - new Vector2(hullHalfLength, 0f);
        var flicker = 0.75f + 0.15f * MathF.Sin(t * 6f) + 0.1f * MathF.Sin(t * 13f);
        foreach (var offset in new[] { -0.5f, 0f, 0.5f })
        {
            var tail = tailBase + new Vector2(0f, offset * hullHalfHeight * 2.6f);
            var radius = offset == 0f ? 22f : 14f;
            var peak = (offset == 0f ? 0.5f : 0.35f) * flicker;
            DrawSoftGlow(spriteBatch, pixel, tail, radius, new Color(150, 190, 230), peak);
        }

        // A pair of small escort/utility craft off the flank - the ship is not travelling
        // entirely alone.
        foreach (var (ox, oy, phase) in new[] { (2.6f, 0.30f, 0f), (1.4f, -0.34f, 2.1f) })
        {
            var escortPos = position + new Vector2(hullHalfLength * ox, pane.Height * oy) + new Vector2(MathF.Sin(t * 0.3f + phase) * 4f, 0f);
            Primitives.FillTriangle(spriteBatch, pixel,
                escortPos + new Vector2(5f, 0f), escortPos + new Vector2(-3f, -2.5f), escortPos + new Vector2(-3f, 2.5f),
                new Color(90, 100, 112));
            DrawSoftGlow(spriteBatch, pixel, escortPos + new Vector2(-4f, 0f), 4f, new Color(150, 190, 230), 0.3f);
        }
    }

    // Slide 3: a hundred years of just holding on - a planet with cloud bands, a thin ring, a
    // small moon, colony clusters on its dark side, and two stations keeping watch instead of one.
    private static void DrawSettlement(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float t)
    {
        var center = new Vector2(pane.X + pane.Width * 0.5f, pane.Y + pane.Height * 0.56f);
        var radius = pane.Height * 0.30f;

        // A small companion moon, well behind the planet.
        var moonPos = center + new Vector2(-radius * 2.3f, -radius * 0.9f);
        HudIcons.FillCircle(spriteBatch, pixel, moonPos, radius * 0.14f, new Color(70, 74, 78));
        HudIcons.FillCircle(spriteBatch, pixel, moonPos + new Vector2(radius * 0.04f, -radius * 0.02f), radius * 0.1f, new Color(128, 130, 126));

        const float ringTilt = -0.22f;
        DrawPlanetRingHalf(spriteBatch, pixel, center, radius * 1.9f, radius * 0.42f, ringTilt, front: false, new Color(150, 160, 150) * 0.3f);

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

        // Cloud bands across the lit hemisphere - a handful of soft, elongated streaks rather
        // than one flat gradient sphere.
        var cloudTone = Color.Lerp(night, lit, 0.75f);
        foreach (var (bandY, bandLen, tone) in new[] { (-0.35f, 0.7f, 1.15f), (0.05f, 0.85f, 0.9f), (0.4f, 0.55f, 1.05f) })
        {
            var by = center.Y + bandY * radius;
            var half = MathF.Sqrt(MathF.Max(0f, radius * radius - bandY * radius * bandY * radius));
            var bandColor = cloudTone * (0.4f * tone);
            var bx = center.X + radius * (1f - bandLen) * 0.4f;
            spriteBatch.Draw(pixel, new Rectangle((int)bx, (int)by, (int)(half * 2f * bandLen), 3), bandColor);
        }

        DrawPlanetRingHalf(spriteBatch, pixel, center, radius * 1.9f, radius * 0.42f, ringTilt, front: true, new Color(190, 195, 180) * 0.5f);

        // A bright rim along the sunlit limb - the one thing that sells "atmosphere" rather than
        // "rock".
        DrawEllipseOutline(spriteBatch, pixel, center, radius * 1.01f, radius * 1.01f, 0f, new Color(180, 210, 235) * 0.22f, 1.6f);

        // Colony lights in small clusters, not lone pixels - each cluster a settlement, not a
        // street lamp.
        foreach (var (s, clusterSize) in new[] { (-0.72f, 3), (-0.55f, 2), (-0.4f, 4), (-0.28f, 2), (-0.12f, 3) })
        {
            var dx = s * radius;
            var half = MathF.Sqrt(MathF.Max(0f, radius * radius - dx * dx));
            var basePos = center + new Vector2(dx, half * 0.5f);
            for (var c = 0; c < clusterSize; c++)
            {
                var jitter = new Vector2((c - clusterSize / 2f) * 2.2f, (c % 2) * 1.4f);
                var twinkle = 0.5f + 0.5f * MathF.Sin(t * 2f + s * 30f + c * 1.3f);
                spriteBatch.Draw(pixel, new Rectangle((int)(basePos.X + jitter.X), (int)(basePos.Y + jitter.Y), 1, 1),
                    new Color(230, 210, 160) * (0.35f + 0.4f * twinkle));
            }
        }

        // Two stations on two different orbits instead of one, so the sky already looks a
        // little busy.
        foreach (var (orbitScale, speed, hue) in new[] { (1.75f, 0.1f, new Color(255, 255, 255)), (1.35f, -0.16f, new Color(190, 210, 230)) })
        {
            var angle = t * speed;
            var stationPos = center + new Vector2(MathF.Cos(angle) * radius * orbitScale, MathF.Sin(angle) * radius * orbitScale * 0.28f);
            DrawSoftGlow(spriteBatch, pixel, stationPos, 5f, hue, 0.35f);
            spriteBatch.Draw(pixel, new Rectangle((int)stationPos.X - 1, (int)stationPos.Y - 1, 2, 2), hue * 0.8f);
        }
    }

    // Slide 4: the same outpost, now sending something outward for the first time - a proper
    // little station with docking arms and solar wings, a small fleet launching in a loose fan,
    // and the home world reduced to a crescent in the corner for continuity.
    private static void DrawExpedition(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float t)
    {
        var homeCenter = new Vector2(pane.Right - pane.Width * 0.12f, pane.Y + pane.Height * 0.18f);
        const float homeRadius = 30f;
        HudIcons.FillCircle(spriteBatch, pixel, homeCenter, homeRadius, new Color(24, 29, 34));
        HudIcons.FillCircle(spriteBatch, pixel, homeCenter + new Vector2(-homeRadius * 0.3f, -homeRadius * 0.2f), homeRadius * 0.85f, new Color(120, 150, 110) * 0.5f);

        var stationPos = new Vector2(pane.X + pane.Width * 0.5f, pane.Y + pane.Height * 0.66f);

        // A proper little station: a central hub, four docking arms, two solar wings.
        HudIcons.FillCircle(spriteBatch, pixel, stationPos, 7f, new Color(96, 106, 118));
        foreach (var armAngle in new[] { 0f, MathF.PI / 2f, MathF.PI, MathF.PI * 1.5f })
        {
            var dir = new Vector2(MathF.Cos(armAngle), MathF.Sin(armAngle));
            var armEnd = stationPos + dir * 17f;
            HudIcons.DrawLine(spriteBatch, pixel, stationPos, armEnd, new Color(92, 102, 114), 2.4f);
            spriteBatch.Draw(pixel, armEnd, null, new Color(80, 90, 102), armAngle, new Vector2(0.5f, 0.5f), new Vector2(3.5f, 3f), SpriteEffects.None, 0f);
        }
        spriteBatch.Draw(pixel, stationPos, null, new Color(50, 78, 96), 0f, new Vector2(0.5f, 0.5f), new Vector2(16f, 2.2f), SpriteEffects.None, 0f);

        // A slow blinking beacon light on top - the one part of the station actually meant to
        // be seen.
        if (MathF.Sin(t * 3f) > 0.6f)
            DrawSoftGlow(spriteBatch, pixel, stationPos + new Vector2(0f, -9f), 4f, new Color(220, 90, 80), 0.5f);

        DrawSoftGlow(spriteBatch, pixel, stationPos, 9f, new Color(150, 190, 220), 0.4f);

        // Three ships launching in a loose fan, not one - the expedition is a fleet, not an errand.
        foreach (var (vx, vy, offset) in new[] { (0.55f, -1f, 0f), (0.15f, -1f, 55f), (0.9f, -0.85f, 110f) })
        {
            var travel = (t * 13f + offset) % 240f;
            if (travel >= 220f)
                continue;
            var velocity = new Vector2(vx, vy);
            var shipPos = stationPos + velocity * travel;
            var dir = velocity.LengthSquared() > 0.001f ? Vector2.Normalize(velocity) : new Vector2(0f, -1f);
            var side = new Vector2(-dir.Y, dir.X);
            Primitives.FillTriangle(spriteBatch, pixel,
                shipPos + dir * 6f, shipPos - dir * 5f + side * 4f, shipPos - dir * 5f - side * 4f,
                new Color(185, 202, 210));
            DrawSoftGlow(spriteBatch, pixel, shipPos - dir * 6f, 6f, new Color(150, 190, 230), 0.45f);
        }

        for (var i = 0; i < 4; i++)
        {
            var phase = (t * 0.2f + i / 4f) % 1f;
            var ringRadius = phase * pane.Height * 0.6f;
            var alpha = (1f - phase) * 0.26f;
            var ringColor = i % 2 == 0 ? new Color(120, 200, 185) : new Color(150, 190, 230);
            DrawRingOutline(spriteBatch, pixel, stationPos, ringRadius, ringColor * alpha);
        }
    }

    // Slide 5: the find itself. A proper dig site - a pit rim, scaffolding, warm work-lanterns -
    // framing the one thing that is not remotely human: a slow halo, thin rays, and motes rising
    // off it.
    private static void DrawArtifact(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float t)
    {
        var groundY = pane.Bottom - pane.Height * 0.12f;
        spriteBatch.Draw(pixel, new Rectangle(pane.X, (int)groundY, pane.Width, pane.Bottom - (int)groundY), new Color(20, 26, 24));
        var stone = new Color(30, 38, 36);
        var stoneLit = new Color(44, 54, 50);
        foreach (var (x, h) in new[] { (0.16f, 0.11f), (0.30f, 0.06f), (0.70f, 0.08f), (0.86f, 0.15f) })
        {
            var w = pane.Width * 0.026f;
            var px = pane.X + pane.Width * x;
            var ph = pane.Height * h;
            spriteBatch.Draw(pixel, new Rectangle((int)(px - w / 2f), (int)(groundY - ph), (int)w, (int)ph + 4), stone);
        }

        var center = new Vector2(pane.X + pane.Width * 0.5f, groundY - pane.Height * 0.26f);
        var pulse = 0.75f + 0.25f * MathF.Sin(t * 1.4f) + 0.1f * MathF.Sin(t * 3.7f);
        var glow = new Color(115, 200, 160);

        // The excavation pit it came out of - a dished depression, not a flat floor.
        DrawEllipseOutline(spriteBatch, pixel, new Vector2(center.X, groundY - 2f), pane.Width * 0.09f, pane.Height * 0.025f, 0f, stoneLit, 1.4f);
        DrawEllipseOutline(spriteBatch, pixel, new Vector2(center.X, groundY - 2f), pane.Width * 0.065f, pane.Height * 0.017f, 0f, stoneLit, 1.1f);

        // Scaffolding - the dig was still active, or was until an hour ago.
        foreach (var sx in new[] { -0.055f, 0.06f })
        {
            var baseA = new Vector2(center.X + pane.Width * sx, groundY);
            var baseB = baseA + new Vector2(pane.Width * 0.018f, 0f);
            var top = new Vector2((baseA.X + baseB.X) / 2f, groundY - pane.Height * 0.16f);
            var beam = new Color(46, 52, 50);
            HudIcons.DrawLine(spriteBatch, pixel, baseA, top, beam, 1.4f);
            HudIcons.DrawLine(spriteBatch, pixel, baseB, top, beam, 1.4f);
            HudIcons.DrawLine(spriteBatch, pixel, Vector2.Lerp(baseA, top, 0.45f), Vector2.Lerp(baseB, top, 0.45f), beam, 1f);
        }

        // Warm work-lanterns at the site, deliberately human and steady next to the artifact's
        // cold, living pulse.
        foreach (var lx in new[] { -0.075f, 0.08f, 0.02f })
        {
            var lampPos = new Vector2(center.X + pane.Width * lx, groundY - 2f);
            DrawSoftGlow(spriteBatch, pixel, lampPos, 5f, new Color(230, 180, 110), 0.3f);
            spriteBatch.Draw(pixel, new Rectangle((int)lampPos.X, (int)lampPos.Y - 1, 1, 1), new Color(255, 220, 160));
        }

        // A slow halo ring - the one moving shape that says "not a rock".
        DrawRingOutline(spriteBatch, pixel, center, 20f + 3f * pulse, glow * (0.22f * pulse));

        DrawSoftGlow(spriteBatch, pixel, center, 72f * pulse, glow, 0.26f);
        DrawSoftGlow(spriteBatch, pixel, center, 30f * pulse, glow, 0.4f);
        spriteBatch.Draw(pixel, center, null, new Color(220, 245, 225) * pulse, MathF.PI / 4f,
            new Vector2(0.5f, 0.5f), new Vector2(7f, 7f), SpriteEffects.None, 0f);

        // Thin rays rather than a burst - something clearly not inert, not something exploding.
        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI / 4f + t * 0.12f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            HudIcons.DrawLine(spriteBatch, pixel, center + dir * 12f, center + dir * (26f + 6f * pulse), glow * (0.32f * pulse), 1f);
        }

        // A handful of motes drifting slowly up and away from it - spores, embers, something
        // leaving.
        for (var i = 0; i < 5; i++)
        {
            var seed = i * 1.31f;
            var riseT = (t * 0.15f + seed) % 1f;
            var motePos = center + new Vector2(MathF.Sin(seed * 3f) * 18f, -riseT * 50f);
            var moteAlpha = (1f - riseT) * 0.5f;
            spriteBatch.Draw(pixel, new Rectangle((int)motePos.X, (int)motePos.Y, 1, 1), glow * moteAlpha);
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
