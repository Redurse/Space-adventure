using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// Every small icon drawn on the new top-bar buttons and the Info panel's tab column - built from
// the same 1x1 pixel texture as the rest of the client's UI (no image assets in this project), the
// same way GalaxyMapPanel/ShipRenderer/TurretReticle already do. Kept in one file since several of
// these icons (the three-person "crew" glyph in particular) are drawn in more than one place.
public static class HudIcons
{
    // Filled circle via a stack of horizontal spans - fine at icon sizes (a handful of pixels
    // across), where a texture-based circle would be overkill for something this small.
    // internal so ItemIcons' tools/tanks (rounded caps, valve knobs, a wrench's actual ring) get the
    // same round shapes instead of reinventing a circle fill.
    internal static void FillCircle(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color)
    {
        var r = (int)MathF.Ceiling(radius);
        for (var dy = -r; dy <= r; dy++)
        {
            var half = MathF.Sqrt(MathF.Max(0f, radius * radius - dy * dy));
            if (half < 0.5f)
                continue;
            spriteBatch.Draw(pixel, new Rectangle((int)(center.X - half), (int)(center.Y + dy), (int)(half * 2f), 1), color);
        }
    }

    private const int SoftCircleTextureSize = 64;

    // A small pre-baked circular texture (solid out to 85% of its own radius, a short soft fade to
    // fully transparent past that) for drawing circles at arbitrary - potentially huge - radii in
    // a single draw call. FillCircle's own per-row loop above costs one Draw per pixel of radius,
    // which is fine at icon sizes but becomes catastrophic (M50 - a real celestial body's own disc,
    // or its gravity well, can be tens of thousands of pixels across at real field scale) - this is
    // O(1) regardless of target size instead. Baked once by whichever renderer needs it
    // (FieldRenderer/GalaxyMapPanel), reused for the life of the client.
    public static Texture2D CreateSoftCircleTexture(GraphicsDevice graphicsDevice)
    {
        const int size = SoftCircleTextureSize;
        var data = new Color[size * size];
        var center = (size - 1) / 2f;
        var radius = size / 2f;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distance = MathF.Sqrt(dx * dx + dy * dy);
                var edgeStart = radius * 0.85f;
                var alpha = distance <= edgeStart ? 1f : Math.Clamp(1f - (distance - edgeStart) / (radius - edgeStart), 0f, 1f);
                data[y * size + x] = Color.White * alpha;
            }
        var texture = new Texture2D(graphicsDevice, size, size);
        texture.SetData(data);
        return texture;
    }

    // The O(1) counterpart to FillCircle above, for celestial bodies/gravity spheres specifically
    // (M50) - a single scaled draw of the pre-baked texture CreateSoftCircleTexture produced,
    // however large radius actually is.
    internal static void DrawScaledCircle(SpriteBatch spriteBatch, Texture2D softCircleTexture, Vector2 center, float radius, Color color)
    {
        if (radius < 0.5f)
            return;
        var scale = radius * 2f / softCircleTexture.Width;
        var origin = new Vector2(softCircleTexture.Width / 2f, softCircleTexture.Height / 2f);
        spriteBatch.Draw(softCircleTexture, center, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    private const int ShadedSphereTextureSize = 64;

    // "как в KSP" (GalaxyMapPanel follow-up) - planets/moons used to be flat FillCircle discs, one
    // solid tier colour with no depth at all. A pre-baked pseudo-3D shading, the same "bake once in
    // white, tint with the draw-time colour" trick CreateSoftCircleTexture already uses for the
    // plain soft circle: each pixel's height off the disc (dz = sqrt(1-dx²-dy²), a hemisphere facing
    // the viewer) stands in for a surface normal, lit from the upper-left the way KSP's own map
    // icons read as lit spheres rather than flat dots. Baked once, reused for every body at whatever
    // radius DrawShadedSphere below actually needs, the same O(1)-regardless-of-size shape
    // DrawScaledCircle already relies on.
    public static Texture2D CreateShadedSphereTexture(GraphicsDevice graphicsDevice)
    {
        const int size = ShadedSphereTextureSize;
        var data = new Color[size * size];
        var center = (size - 1) / 2f;
        var light = Vector3.Normalize(new Vector3(-0.55f, -0.55f, 0.65f));
        const float ambient = 0.4f;
        const float edgeStart = 0.92f;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = (x - center) / center;
                var dy = (y - center) / center;
                var distSq = dx * dx + dy * dy;
                if (distSq > 1f)
                {
                    data[y * size + x] = Color.Transparent;
                    continue;
                }
                var dz = MathF.Sqrt(Math.Max(0f, 1f - distSq));
                var diffuse = Math.Max(0f, dx * light.X + dy * light.Y + dz * light.Z);
                var brightness = ambient + diffuse * (1f - ambient);
                var dist = MathF.Sqrt(distSq);
                var alpha = dist <= edgeStart ? 1f : Math.Clamp(1f - (dist - edgeStart) / (1f - edgeStart), 0f, 1f);
                data[y * size + x] = new Color(brightness, brightness, brightness) * alpha;
            }
        var texture = new Texture2D(graphicsDevice, size, size);
        texture.SetData(data);
        return texture;
    }

    internal static void DrawShadedSphere(SpriteBatch spriteBatch, Texture2D shadedSphereTexture, Vector2 center, float radius, Color color)
    {
        if (radius < 0.5f)
            return;
        var scale = radius * 2f / shadedSphereTexture.Width;
        var origin = new Vector2(shadedSphereTexture.Width / 2f, shadedSphereTexture.Height / 2f);
        spriteBatch.Draw(shadedSphereTexture, center, null, color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    internal static void DrawRingArc(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, float startDegrees, float endDegrees, Color color, int segments = 10, float thickness = 1.4f)
    {
        var start = startDegrees * (MathF.PI / 180f);
        var end = endDegrees * (MathF.PI / 180f);
        for (var i = 0; i < segments; i++)
        {
            var a0 = start + (end - start) * i / segments;
            var a1 = start + (end - start) * (i + 1) / segments;
            var p0 = center + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius;
            var p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            DrawLine(spriteBatch, pixel, p0, p1, color, thickness);
        }
    }

    internal static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, Vector2 to, Color color, float thickness = 1.4f)
    {
        var delta = to - from;
        var length = delta.Length();
        if (length < 0.5f)
            return;
        var rotation = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(pixel, from, null, color, rotation, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    // A single simplified person: round head over a trapezoid-ish body (approximated as a
    // triangle-topped rectangle via two overlapping rects, cheap and reads fine this small).
    public static void DrawPerson(SpriteBatch spriteBatch, Texture2D pixel, Vector2 feetCenter, float scale, Color color)
    {
        var headRadius = 2.6f * scale;
        var headCenter = feetCenter - new Vector2(0, 8.5f * scale);
        FillCircle(spriteBatch, pixel, headCenter, headRadius, color);
        var bodyWidth = 7f * scale;
        var bodyHeight = 6.5f * scale;
        spriteBatch.Draw(pixel, new Rectangle(
            (int)(feetCenter.X - bodyWidth / 2f), (int)(feetCenter.Y - bodyHeight - 1f * scale),
            (int)bodyWidth, (int)bodyHeight), color);
    }

    // Three people, the center one drawn bigger and nudged forward (down/toward the viewer) - the
    // "экипаж" glyph, reused as-is for the Info panel's Team tab icon.
    public static void DrawCrewGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        DrawPerson(spriteBatch, pixel, center + new Vector2(-9f * scale, -2f * scale), 0.8f * scale, color * 0.75f);
        DrawPerson(spriteBatch, pixel, center + new Vector2(9f * scale, -2f * scale), 0.8f * scale, color * 0.75f);
        DrawPerson(spriteBatch, pixel, center + new Vector2(0, 3f * scale), 1.05f * scale, color);
    }

    // Four bars of increasing height - the "Информация" button's own glyph (a stats/report look).
    public static void DrawBarsGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        var heights = new[] { 6f, 10f, 14f, 18f };
        var barWidth = 3f * scale;
        var gap = 2f * scale;
        var totalWidth = heights.Length * barWidth + (heights.Length - 1) * gap;
        var x = center.X - totalWidth / 2f;
        var baseline = center.Y + 9f * scale;
        foreach (var h in heights)
        {
            var height = h * scale;
            spriteBatch.Draw(pixel, new Rectangle((int)x, (int)(baseline - height), (int)barWidth, (int)height), color);
            x += barWidth + gap;
        }
    }

    // Missions tab: a small flag on a pole.
    public static void DrawFlagGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        var poleTop = center + new Vector2(-6f * scale, -9f * scale);
        var poleBottom = center + new Vector2(-6f * scale, 9f * scale);
        DrawLine(spriteBatch, pixel, poleTop, poleBottom, color, 1.6f * scale);
        Primitives.FillTriangle(spriteBatch, pixel,
            poleTop,
            poleTop + new Vector2(11f * scale, 2.5f * scale),
            poleTop + new Vector2(0, 7f * scale),
            color);
    }

    // Reputation tab: a medal - circle with a two-strand ribbon above it.
    public static void DrawMedalGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        var ribbonTop = center + new Vector2(0, -10f * scale);
        var medalCenter = center + new Vector2(0, 2f * scale);
        Primitives.FillTriangle(spriteBatch, pixel, ribbonTop + new Vector2(-4f * scale, 0), ribbonTop + new Vector2(0, 0), medalCenter, color * 0.85f);
        Primitives.FillTriangle(spriteBatch, pixel, ribbonTop + new Vector2(4f * scale, 0), ribbonTop + new Vector2(0, 0), medalCenter, color * 0.85f);
        FillCircle(spriteBatch, pixel, medalCenter, 6f * scale, color);
        FillCircle(spriteBatch, pixel, medalCenter, 3f * scale, color * 0.6f);
    }

    // Ship tab: a simple side-view hull silhouette (matches the abstract, no-art style everything
    // else in this project uses for ships - a body with a raked nose, not a detailed illustration).
    public static void DrawShipGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        var hullWidth = 20f * scale;
        var hullHeight = 5f * scale;
        spriteBatch.Draw(pixel, new Rectangle(
            (int)(center.X - hullWidth / 2f), (int)(center.Y - hullHeight / 2f),
            (int)(hullWidth * 0.7f), (int)hullHeight), color);
        Primitives.FillTriangle(spriteBatch, pixel,
            center + new Vector2(hullWidth * 0.2f, -hullHeight / 2f),
            center + new Vector2(hullWidth * 0.2f, hullHeight / 2f),
            center + new Vector2(hullWidth / 2f, 0),
            color);
    }

    // Main menu's "Сетевая игра" section icon: three connected nodes - the simplest "network" look
    // that doesn't collide with DrawCrewGlyph's three people (that one means crew, not a wire graph).
    public static void DrawSignalGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        var left = center + new Vector2(-8f * scale, 6f * scale);
        var right = center + new Vector2(8f * scale, 6f * scale);
        var top = center + new Vector2(0, -7f * scale);
        DrawLine(spriteBatch, pixel, left, top, color, 1.6f * scale);
        DrawLine(spriteBatch, pixel, right, top, color, 1.6f * scale);
        FillCircle(spriteBatch, pixel, left, 3f * scale, color);
        FillCircle(spriteBatch, pixel, right, 3f * scale, color);
        FillCircle(spriteBatch, pixel, top, 3f * scale, color);
    }

    // Suit-locker card icon (SuitLockerPanel): helmet + shoulders, in the same CadetBlue a worn
    // suit already tints a character (ShipRenderer.DrawCharacter's outline ring/visor colour) -
    // the closest a "no art assets" project gets to the reference screenshot's suit portrait.
    public static void DrawSuitGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        var helmetCenter = center + new Vector2(0, -8f * scale);
        FillCircle(spriteBatch, pixel, helmetCenter, 7f * scale, color);
        FillCircle(spriteBatch, pixel, helmetCenter, 4f * scale, color * 0.55f);
        var bodyWidth = 16f * scale;
        var bodyHeight = 13f * scale;
        spriteBatch.Draw(pixel, new Rectangle(
            (int)(center.X - bodyWidth / 2f), (int)center.Y,
            (int)bodyWidth, (int)bodyHeight), color);
    }

    // Character tab (placeholder, does nothing yet) - a fingerprint approximated as nested
    // partial rings, the closest a "no art assets" project gets to the real texture.
    public static void DrawFingerprintGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        for (var i = 0; i < 4; i++)
        {
            var radius = (4f + i * 3f) * scale;
            DrawRingArc(spriteBatch, pixel, center + new Vector2(0, 1.5f * scale), radius, 200f, 340f, color);
        }
    }

    private static void DrawRectOutline(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    // Per-CrewRole icon for the crew roster (Button 1's slide-out, InfoPanel's Team tab) - a human
    // player (no CrewRole) gets the plain person glyph above; a bot gets one distinguishing shape
    // per job so the row reads at a glance without needing the text label next to it.
    public static void DrawRoleGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color, CrewRole? role)
    {
        switch (role)
        {
            case CrewRole.Captain:
                DrawStar(spriteBatch, pixel, center, 8f * scale, color);
                break;
            case CrewRole.Engineer:
                DrawWrench(spriteBatch, pixel, center, scale, color);
                break;
            case CrewRole.Mechanic:
                DrawGear(spriteBatch, pixel, center, scale, color);
                break;
            case CrewRole.Security:
                DrawShield(spriteBatch, pixel, center, scale, color);
                break;
            case CrewRole.Scientist:
                DrawMagnifier(spriteBatch, pixel, center, scale, color);
                break;
            default:
                DrawPerson(spriteBatch, pixel, center + new Vector2(0, 5f * scale), scale, color);
                break;
        }
    }

    private static void DrawStar(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color)
    {
        // A 4-point star (two overlapping diamonds) - cheap and legible at icon size, unlike a
        // true 5-point star's concave outline.
        Primitives.FillTriangle(spriteBatch, pixel, center + new Vector2(0, -radius), center + new Vector2(radius * 0.35f, 0), center + new Vector2(-radius * 0.35f, 0), color);
        Primitives.FillTriangle(spriteBatch, pixel, center + new Vector2(0, radius), center + new Vector2(radius * 0.35f, 0), center + new Vector2(-radius * 0.35f, 0), color);
        Primitives.FillTriangle(spriteBatch, pixel, center + new Vector2(-radius, 0), center + new Vector2(0, radius * 0.35f), center + new Vector2(0, -radius * 0.35f), color);
        Primitives.FillTriangle(spriteBatch, pixel, center + new Vector2(radius, 0), center + new Vector2(0, radius * 0.35f), center + new Vector2(0, -radius * 0.35f), color);
    }

    private static void DrawWrench(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        DrawLine(spriteBatch, pixel, center + new Vector2(-7f * scale, 7f * scale), center + new Vector2(6f * scale, -6f * scale), color, 3f * scale);
        FillCircle(spriteBatch, pixel, center + new Vector2(7f * scale, -7f * scale), 3.5f * scale, color);
    }

    private static void DrawGear(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        FillCircle(spriteBatch, pixel, center, 6f * scale, color);
        FillCircle(spriteBatch, pixel, center, 2.5f * scale, color * 0.5f);
        for (var i = 0; i < 6; i++)
        {
            var angle = i * MathF.PI / 3f;
            var toothCenter = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 8f * scale;
            spriteBatch.Draw(pixel, new Rectangle((int)toothCenter.X - (int)(1.5f * scale), (int)toothCenter.Y - (int)(1.5f * scale), (int)(3f * scale), (int)(3f * scale)), color);
        }
    }

    private static void DrawShield(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        var top = center + new Vector2(0, -8f * scale);
        var left = center + new Vector2(-7f * scale, -3f * scale);
        var right = center + new Vector2(7f * scale, -3f * scale);
        var bottom = center + new Vector2(0, 8f * scale);
        Primitives.FillTriangle(spriteBatch, pixel, top, left, right, color);
        Primitives.FillTriangle(spriteBatch, pixel, left, right, bottom, color);
    }

    // Content-каталог отсеков - StationBuildPanel's own category-tab row (build-catalog UI). Public
    // wrappers around the two glyphs above that were previously only reachable from DrawRoleGlyph's
    // switch - reused here as-is rather than duplicated, same shield/lens shapes just for a
    // different button.
    public static void DrawShieldGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color) =>
        DrawShield(spriteBatch, pixel, center, scale, color);

    public static void DrawSensorGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color) =>
        DrawMagnifier(spriteBatch, pixel, center, scale, color);

    // Reactor/power category: a core dot with radiating spokes - reads as "energy source" without
    // needing a literal lightning-bolt polygon (fiddly to keep legible at icon scale).
    public static void DrawPowerGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        FillCircle(spriteBatch, pixel, center, 4f * scale, color);
        for (var i = 0; i < 6; i++)
        {
            var angle = i * MathF.PI / 3f;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            DrawLine(spriteBatch, pixel, center + dir * 6f * scale, center + dir * 10f * scale, color, 1.6f * scale);
        }
    }

    // Propulsion category: a nozzle (triangle) with a short exhaust flame trailing behind it.
    public static void DrawThrusterGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        var nozzleTop = center + new Vector2(0, -9f * scale);
        Primitives.FillTriangle(spriteBatch, pixel,
            nozzleTop + new Vector2(-4f * scale, 0), nozzleTop + new Vector2(4f * scale, 0), center + new Vector2(0, 2f * scale), color);
        Primitives.FillTriangle(spriteBatch, pixel,
            center + new Vector2(-3f * scale, 2f * scale), center + new Vector2(3f * scale, 2f * scale), center + new Vector2(0, 10f * scale),
            Color.Lerp(color, Color.OrangeRed, 0.5f));
    }

    // Weapons category: a crosshair - reads as "targeting" for any turret regardless of its
    // specific weapon type.
    public static void DrawCrosshairGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        DrawRingArc(spriteBatch, pixel, center, 7f * scale, 0f, 360f, color, 16, 1.6f * scale);
        DrawLine(spriteBatch, pixel, center + new Vector2(-10f * scale, 0), center + new Vector2(-4f * scale, 0), color, 1.6f * scale);
        DrawLine(spriteBatch, pixel, center + new Vector2(4f * scale, 0), center + new Vector2(10f * scale, 0), color, 1.6f * scale);
        DrawLine(spriteBatch, pixel, center + new Vector2(0, -10f * scale), center + new Vector2(0, -4f * scale), color, 1.6f * scale);
        DrawLine(spriteBatch, pixel, center + new Vector2(0, 4f * scale), center + new Vector2(0, 10f * scale), color, 1.6f * scale);
    }

    // Structural category: a plain hollow square - bare hull/corridor, nothing installed in it.
    public static void DrawStructuralGlyph(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        var rect = new Rectangle((int)(center.X - 8f * scale), (int)(center.Y - 8f * scale), (int)(16f * scale), (int)(16f * scale));
        DrawRectOutline(spriteBatch, pixel, rect, color, (int)Math.Max(1, 2f * scale));
    }

    // The scientist's own two jobs, medic's healing cross replaced (M42) - a lens for the scanner
    // they run at the navigation console (M44), same reading either way: "looks closer at things".
    private static void DrawMagnifier(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float scale, Color color)
    {
        var lensCenter = center + new Vector2(-2f * scale, -2f * scale);
        DrawRingArc(spriteBatch, pixel, lensCenter, 5f * scale, 0f, 360f, color, 16, 2f * scale);
        DrawLine(spriteBatch, pixel, lensCenter + new Vector2(3.5f * scale, 3.5f * scale), center + new Vector2(8f * scale, 8f * scale), color, 2.5f * scale);
    }
}
