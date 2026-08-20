using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// The main menu's right-hand art pane: a planet on a slow orbit with the player's own ship
// circling it, built entirely from this project's usual "no image assets" primitive toolkit - a
// column-by-column gradient fill for the sphere's day/night shading (the same trick HudIcons.
// FillCircle uses, just varying colour across the columns instead of filling them flat), an N-gon
// ellipse for the ring and orbit path, and a handful of animated stars for depth.
public static class MenuPlanetScene
{
    // Lazily built against this pane's own bounds (fixed - DesignWidth/Height never change at
    // runtime) the first time Draw runs. Replaces the old flat single-layer star loop with the
    // same 3-depth-band parallax field + soft nebula patches the flight view uses - here with no
    // drift fed in (there's no ship velocity on the menu), so it's pure depth and twinkle.
    private static Starfield? _starfield;

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float totalSeconds)
    {
        DrawGradientBackground(spriteBatch, pixel, pane);
        _starfield ??= new Starfield(pixel, pane, count: 220);
        // Every star already carries its own Parallax factor and wraps at the pane edge, so one
        // drift vector is enough to separate the field into depths: the near stars slide, the far
        // ones barely move, and the screen stops being a flat picture.
        _starfield.Draw(spriteBatch, totalSeconds, new Vector2(totalSeconds * 7f, 0f));
        DrawDustMotes(spriteBatch, pixel, pane, totalSeconds);

        var planetCenter = new Vector2(pane.X + pane.Width * 0.6f, pane.Y + pane.Height * 0.56f);
        const float planetRadius = 116f;
        const float ringRadiusX = 205f;
        const float ringRadiusY = 44f;
        const float ringTilt = -0.22f;
        const float orbitRadiusX = 300f;
        const float orbitRadiusY = 185f;

        DrawOrbitPath(spriteBatch, pixel, planetCenter, orbitRadiusX, orbitRadiusY);
        DrawRingHalf(spriteBatch, pixel, planetCenter, ringRadiusX, ringRadiusY, ringTilt, front: false);
        DrawPlanet(spriteBatch, pixel, planetCenter, planetRadius, totalSeconds);
        DrawRingHalf(spriteBatch, pixel, planetCenter, ringRadiusX, ringRadiusY, ringTilt, front: true);
        DrawOrbitingKatyusha(spriteBatch, pixel, planetCenter, orbitRadiusX, orbitRadiusY, totalSeconds);

        DrawPassingShip(spriteBatch, pixel, pane, totalSeconds);
        DrawScanline(spriteBatch, pixel, pane, totalSeconds);
        DrawVignette(spriteBatch, pixel, pane);
    }

    // Where the Katyusha truck actually is on screen right now - the same planetCenter/orbit
    // constants and angle formula Draw/DrawOrbitingKatyusha use, exposed so Game1.Menu.cs can park
    // a caption next to it without needing its own copy of that math (and without this class
    // needing a SpriteFont just to draw one label itself).
    public static Vector2 GetKatyushaScreenPosition(Rectangle pane, float totalSeconds)
    {
        var planetCenter = new Vector2(pane.X + pane.Width * 0.6f, pane.Y + pane.Height * 0.56f);
        const float orbitRadiusX = 300f;
        const float orbitRadiusY = 185f;
        var angle = totalSeconds * 0.16f;
        return planetCenter + new Vector2(MathF.Cos(angle) * orbitRadiusX, MathF.Sin(angle) * orbitRadiusY);
    }

    // Faint drifting specks well in front of the starfield - not stars (they move, and far too
    // fast to be), just cabin-window dust catching whatever light is around. Loops via modulo the
    // same way the starfield wraps, just per-mote instead of per-frame-offset.
    private static void DrawDustMotes(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float totalSeconds)
    {
        const int count = 36;
        for (var i = 0; i < count; i++)
        {
            var seedX = (i * 71 + (i * i * 13) % 53) % pane.Width;
            var seedY = (i * 37 + (i * i * 5) % 61) % pane.Height;
            var speed = 5f + (i % 5) * 2f;
            var x = pane.X + Wrap(seedX + totalSeconds * speed, pane.Width);
            var y = pane.Y + Wrap(seedY + totalSeconds * speed * 0.3f, pane.Height);
            var alpha = 0.06f + 0.09f * MathF.Abs(MathF.Sin(i * 1.7f + totalSeconds * 0.4f));
            spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, 1, 1), new Color(210, 230, 235) * alpha);
        }
    }

    // A soft horizontal band sweeping slowly down the pane on a loop - a sensor/scan pass over the
    // scene, cheap and very low-alpha so it reads as ambient tech dressing, not a strobe.
    // A silhouette crossing far behind everything else, once every couple of minutes. It is not
    // decoration so much as scale: nothing else on this screen tells you how big the planet is,
    // and a ship that takes a hundred seconds to cross in front of it answers that instantly.
    private static void DrawPassingShip(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float totalSeconds)
    {
        const float period = 128f;
        var t = totalSeconds % period / period;
        // Only actually on screen for the first third of the cycle; the rest is the long wait
        // that makes it feel like a sighting rather than a loop.
        if (t > 0.34f)
            return;

        var progress = t / 0.34f;
        var x = pane.X - 90f + progress * (pane.Width + 180f);
        var y = pane.Y + pane.Height * 0.24f + MathF.Sin(progress * 3.1f) * 8f;
        var fade = MathHelper.Clamp(MathF.Min(progress, 1f - progress) * 6f, 0f, 1f) * 0.55f;
        var hull = new Color(16, 20, 28) * fade;

        spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, 54, 7), hull);
        spriteBatch.Draw(pixel, new Rectangle((int)x + 10, (int)y - 4, 26, 4), hull);
        spriteBatch.Draw(pixel, new Rectangle((int)x + 44, (int)y - 3, 8, 13), hull);
        // One running light, the only part of it that is not a shadow.
        spriteBatch.Draw(pixel, new Rectangle((int)x + 2, (int)y + 2, 2, 2), new Color(220, 120, 110) * fade * 1.8f);
    }

    private static void DrawScanline(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane, float totalSeconds)
    {
        const float period = 7f;
        const int bandHeight = 46;
        var t = Wrap(totalSeconds, period) / period;
        var centerY = pane.Y + t * pane.Height;
        for (var i = 0; i < bandHeight; i++)
        {
            var y = (int)(centerY - bandHeight / 2f + i);
            if (y < pane.Y || y >= pane.Bottom)
                continue;
            var alpha = 0.045f * (1f - MathF.Abs(i - bandHeight / 2f) / (bandHeight / 2f));
            spriteBatch.Draw(pixel, new Rectangle(pane.X, y, pane.Width, 1), new Color(120, 220, 210) * alpha);
        }
    }

    private static float Wrap(float value, float size) => (value % size + size) % size;

    // A dark frame around the pane's own edges - cheap "cinematic letterboxing" feel that keeps
    // the eye on the planet/ship instead of the flat corners, built from the same soft-glow rings
    // as the nebula/atmosphere, just anchored outside each edge rather than centred on a point.
    private static void DrawVignette(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane)
    {
        const int bands = 10;
        for (var i = 0; i < bands; i++)
        {
            var alpha = 0.05f * (1f - (float)i / bands);
            var inset = i * 3;
            var rect = new Rectangle(pane.X + inset, pane.Y + inset, Math.Max(1, pane.Width - inset * 2), Math.Max(1, pane.Height - inset * 2));
            // Only the outer 1px ring of this inset rect is drawn each pass (top/bottom/left/right
            // strips) - a filled rect at every inset would just repaint the whole pane opaque black.
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), Color.Black * alpha);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Color.Black * alpha);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), Color.Black * alpha);
            spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), Color.Black * alpha);
        }
    }

    private static void DrawGradientBackground(SpriteBatch spriteBatch, Texture2D pixel, Rectangle pane)
    {
        const int bands = 12;
        var top = new Color(5, 8, 15);
        var bottom = new Color(15, 24, 32);
        for (var i = 0; i < bands; i++)
        {
            var y = pane.Y + pane.Height * i / bands;
            var height = pane.Height / bands + 1;
            spriteBatch.Draw(pixel, new Rectangle(pane.X, y, pane.Width, height), Color.Lerp(top, bottom, i / (float)(bands - 1)));
        }
    }

    // Concentric circles fading outward - the one "glow" primitive the atmosphere halo and the
    // engine flare below are both built from (the nebula patches are now Starfield's own).
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

    // A dashed ellipse tracing the ship's own path around the planet - drawn once, fully, before
    // the planet body so the far side reads as passing behind it.
    private static void DrawOrbitPath(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radiusX, float radiusY)
    {
        const int segments = 72;
        for (var i = 0; i < segments; i += 2)
        {
            var a0 = i * 2f * MathF.PI / segments;
            var a1 = (i + 1) * 2f * MathF.PI / segments;
            var p0 = center + new Vector2(MathF.Cos(a0) * radiusX, MathF.Sin(a0) * radiusY);
            var p1 = center + new Vector2(MathF.Cos(a1) * radiusX, MathF.Sin(a1) * radiusY);
            HudIcons.DrawLine(spriteBatch, pixel, p0, p1, Color.CadetBlue * 0.22f, 1.2f);
        }
    }

    // The sphere itself, column by column rather than one flat fill - each column's own visible
    // height is clamped to the true circle (same maths as HudIcons.FillCircle), and its colour
    // blends from the night side to the lit side across the disk. That keeps the terminator a
    // clean gradient without ever painting outside the planet's own silhouette, which a simpler
    // "dark circle offset to one side" overlay could not do without a real clip.
    private static void DrawPlanet(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, float totalSeconds)
    {
        DrawSoftGlow(spriteBatch, pixel, center, radius * 1.4f, new Color(120, 175, 220), 0.22f);

        var night = new Color(28, 34, 30);
        var lit = new Color(150, 168, 118);
        var r = (int)MathF.Ceiling(radius);
        for (var dx = -r; dx <= r; dx++)
        {
            var half = MathF.Sqrt(MathF.Max(0f, radius * radius - dx * dx));
            if (half < 0.5f)
                continue;
            var t = (dx + radius) / (2f * radius);
            var color = Color.Lerp(night, lit, MathF.Pow(t, 0.8f));
            spriteBatch.Draw(pixel, new Rectangle((int)(center.X + dx), (int)(center.Y - half), 1, (int)MathF.Ceiling(half * 2f)), color);
        }

        // Cloud/continent blotches, small enough to stay clear of the rim, that drift slowly
        // around the sphere - the closest this project gets to a rotating planet texture.
        var seedAngles = new[] { 0.3f, 1.6f, 2.7f, 4.1f, 5.2f };
        for (var i = 0; i < seedAngles.Length; i++)
        {
            var angle = seedAngles[i] + totalSeconds * 0.05f;
            var orbitRadius = radius * (0.3f + 0.08f * (i % 3));
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle) * 0.55f) * orbitRadius;
            if (offset.Length() > radius * 0.7f)
                continue;
            var blobRadius = radius * (0.18f + 0.04f * (i % 3));
            var shade = MathF.Sin(angle) > 0f ? new Color(190, 200, 150) * 0.22f : new Color(20, 24, 20) * 0.25f;
            HudIcons.FillCircle(spriteBatch, pixel, center + offset, blobRadius, shade);
        }

        // A small bright limb highlight on the sunlit edge, on top of everything - the one thing
        // that really sells "sphere lit from one side" at a glance.
        HudIcons.FillCircle(spriteBatch, pixel, center + new Vector2(radius * 0.55f, -radius * 0.35f), radius * 0.22f, Color.White * 0.12f);
    }

    // One half of the ring (whichever side of the tilt axis is currently "front" or "back"),
    // drawn as a dashed-ish open arc rather than a closed StrokePolygon loop, so the two halves can
    // be layered on opposite sides of the planet body.
    private static void DrawRingHalf(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radiusX, float radiusY, float tilt, bool front)
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
            {
                var color = front ? new Color(210, 200, 180) * 0.6f : new Color(120, 112, 100) * 0.32f;
                HudIcons.DrawLine(spriteBatch, pixel, prev, point, color, front ? 3f : 2f);
            }
            previous = point;
        }
    }

    // A flatbed truck with an angled multi-tube rocket rack over the bed - the general silhouette
    // of a historical rocket-artillery vehicle (a real-world vehicle category, not anyone's
    // copyrighted character or logo), standing in for the ship as the scene's second focal point.
    // Still nose-facing its direction of travel around the same orbit path as the original ship.
    private static void DrawOrbitingKatyusha(SpriteBatch spriteBatch, Texture2D pixel, Vector2 planetCenter, float orbitRadiusX, float orbitRadiusY, float totalSeconds)
    {
        var angle = totalSeconds * 0.16f;
        var position = planetCenter + new Vector2(MathF.Cos(angle) * orbitRadiusX, MathF.Sin(angle) * orbitRadiusY);
        var tangent = new Vector2(-MathF.Sin(angle) * orbitRadiusX, MathF.Cos(angle) * orbitRadiusY);
        if (tangent.LengthSquared() < 0.01f)
            tangent = Vector2.UnitX;
        tangent.Normalize();
        var rotation = MathF.Atan2(tangent.Y, tangent.X);

        DrawSoftGlow(spriteBatch, pixel, position, 14f, new Color(200, 210, 220), 0.22f);
        DrawKatyushaSilhouette(spriteBatch, pixel, position, rotation);
    }

    private static void DrawKatyushaSilhouette(SpriteBatch spriteBatch, Texture2D pixel, Vector2 position, float rotation)
    {
        var forward = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
        var side = new Vector2(-forward.Y, forward.X);
        var bodyColor = new Color(74, 90, 58);
        var darkColor = new Color(30, 36, 28);
        var wheelColor = new Color(18, 18, 16);

        const float bodyLength = 40f;
        const float bodyWidth = 13f;

        // Chassis, then a boxy cab with a small windshield toward the front third - a 3-axle
        // truck bed (the historical launcher's actual base), not a generic 2-axle pickup.
        spriteBatch.Draw(pixel, position, null, bodyColor, rotation, new Vector2(0.5f, 0.5f),
            new Vector2(bodyLength * 0.5f, bodyWidth * 0.8f), SpriteEffects.None, 0f);
        var cabCenter = position + forward * bodyLength * 0.32f;
        spriteBatch.Draw(pixel, cabCenter, null, darkColor, rotation, new Vector2(0.5f, 0.5f),
            new Vector2(bodyLength * 0.12f, bodyWidth * 0.85f), SpriteEffects.None, 0f);
        spriteBatch.Draw(pixel, cabCenter + forward * bodyLength * 0.05f, null, new Color(150, 185, 195) * 0.5f, rotation,
            new Vector2(0.5f, 0.5f), new Vector2(1.2f, bodyWidth * 0.55f), SpriteEffects.None, 0f);

        // Three axles - a lone front one, a close-set pair at the rear (the classic 6x6 layout).
        foreach (var t in new[] { -0.36f, -0.08f, 0.34f })
        {
            HudIcons.FillCircle(spriteBatch, pixel, position + forward * bodyLength * t - side * bodyWidth * 0.6f, 3.3f, wheelColor);
            HudIcons.FillCircle(spriteBatch, pixel, position + forward * bodyLength * t + side * bodyWidth * 0.6f, 3.3f, wheelColor);
        }

        // The launch frame itself: a braced arm lifting a rectangular rail rack at a shallow angle
        // over the bed and out past the cab - parallel rails with rockets resting on some of them,
        // the actual detail that makes this read as "Katyusha" rather than a flatbed truck.
        var mount = position - forward * bodyLength * 0.02f;
        var tiltAngle = rotation - MathF.PI / 2f - 0.42f;
        var tiltDir = new Vector2(MathF.Cos(tiltAngle), MathF.Sin(tiltAngle));
        var railPerp = new Vector2(-tiltDir.Y, tiltDir.X);

        const float rackLength = 34f;
        var rackFar = mount + tiltDir * rackLength;

        // Two braces triangulating the rack against the chassis, so it reads as load-bearing
        // rather than floating.
        HudIcons.DrawLine(spriteBatch, pixel, position - forward * bodyLength * 0.20f, mount, darkColor, 2.2f);
        HudIcons.DrawLine(spriteBatch, pixel, position + forward * bodyLength * 0.12f, mount + tiltDir * rackLength * 0.35f, darkColor, 1.8f);

        // The rack's own frame outline.
        HudIcons.DrawLine(spriteBatch, pixel, mount - railPerp * 5f, mount + railPerp * 5f, darkColor, 2f);
        HudIcons.DrawLine(spriteBatch, pixel, rackFar - railPerp * 5f, rackFar + railPerp * 5f, darkColor, 2f);
        HudIcons.DrawLine(spriteBatch, pixel, mount - railPerp * 5f, rackFar - railPerp * 5f, darkColor, 1.4f);
        HudIcons.DrawLine(spriteBatch, pixel, mount + railPerp * 5f, rackFar + railPerp * 5f, darkColor, 1.4f);

        const int rails = 6;
        for (var i = 0; i < rails; i++)
        {
            var lateral = (i - (rails - 1) / 2f) * 1.9f;
            var railStart = mount + railPerp * lateral;
            var railEnd = railStart + tiltDir * rackLength;
            HudIcons.DrawLine(spriteBatch, pixel, railStart, railEnd, new Color(150, 150, 145), 0.8f);

            // Every other rail carries a loaded rocket, nose overhanging the rack's far end.
            if (i % 2 != 0)
                continue;
            var rocketBase = railStart + tiltDir * rackLength * 0.55f;
            var rocketTip = railStart + tiltDir * (rackLength * 0.55f + 11f);
            HudIcons.DrawLine(spriteBatch, pixel, rocketBase, rocketTip, new Color(95, 100, 92), 2.2f);
            Primitives.FillTriangle(spriteBatch, pixel, rocketTip,
                rocketTip - tiltDir * 3f + railPerp * 1.4f, rocketTip - tiltDir * 3f - railPerp * 1.4f,
                new Color(120, 60, 45));
        }
    }
}
