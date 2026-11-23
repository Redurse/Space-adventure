using System;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Direct user request ("сделай перископ для турели красивее с адекватной моделькой в стиле основы
// пушки из space haven") - the gunner's periscope station used to be live-drawn primitives (an
// octagon, two square grips and a flat box). Now it's a baked model in the language of a Space
// Haven gun base: an octagonal gunmetal deck plate with hazard-striped chamfers and edge bolts, a
// heavy traverse ring with its own ring of bolt heads, a domed rotating hub lit from above, and the
// periscope head on top of it - an eyepiece block between two rubber-gripped handles, a red trigger
// on the right one, cables running off into the deck. Only what changes stays live and is drawn by
// ShipRenderer.DrawPeriscopeStation over this: the eyepiece glow and the status lamp.
public sealed partial class DeviceSkin
{
    // The eyepiece well's centre and radius as fractions of the canvas - DrawPeriscopeStation puts
    // its live glow exactly there.
    public const float PeriscopeLensX = 0.5f;
    public const float PeriscopeLensY = 0.43f;
    public const float PeriscopeLensRadius = 0.06f;

    private void Periscope(bool lit)
    {
        float size = _canvasWidth;
        var c = size / 2f;
        _chamfer = (int)(size * 0.24f);
        _offsetX = 0;
        _offsetY = 0;

        // The deck plate: brushed gunmetal, lit from above, a vignette into the corners.
        for (var y = 0; y < _canvasHeight; y++)
        for (var x = 0; x < _canvasWidth; x++)
        {
            if (!Inside(x, y))
                continue;
            var v = (Hash(x * 13 + 401) - 0.5f) * 12f + (Hash(x + 401, y / 6) - 0.5f) * 6f + (Hash(x * 7 + y * 11, 9) - 0.5f) * 4f;
            var g = 12f * (1f - y / (size * 0.5f));
            var dx = (x - c) / (size * 0.62f);
            var dy = (y - c) / (size * 0.62f);
            var vignette = -14f * MathHelper.Clamp(dx * dx + dy * dy - 0.35f, 0f, 1f);
            _buffer[y * _canvasWidth + x] = new Color(
                (int)MathHelper.Clamp(70 + v + g + vignette, 0, 255),
                (int)MathHelper.Clamp(76 + v + g + vignette, 0, 255),
                (int)MathHelper.Clamp(86 + v + g + vignette, 0, 255), 255);
        }

        // Hazard tape on each of the four chamfers - a band just inside the cut edge, striped
        // across it.
        var band = MathF.Max(3f, size * 0.06f);
        for (var y = 0; y < _canvasHeight; y++)
        for (var x = 0; x < _canvasWidth; x++)
        {
            var w = _canvasWidth - 1 - x;
            var h = _canvasHeight - 1 - y;
            var d = MathF.Min(MathF.Min(x + y, w + y), MathF.Min(x + h, w + h)) - _chamfer;
            if (d < 1f || d > 1f + band * 1.4f)
                continue;
            // Across the chamfer: the top-left/bottom-right cuts run along (1,-1), the other two
            // along (1,1), so the stripes step along whichever diagonal that corner's cut follows.
            var along = (x < c) == (y < c) ? x - y : x + y;
            var stripe = ((int)MathF.Floor(along / MathF.Max(3f, size * 0.045f)) & 1) == 0;
            Px(x, y, stripe ? new Color(226, 184, 52) : new Color(28, 26, 22), 0.9f);
        }

        // Bevel on the plate's straight edges.
        for (var x = _chamfer; x < _canvasWidth - _chamfer; x++)
        {
            Px(x, 0, new Color(220, 228, 240), 0.45f);
            Px(x, 1, new Color(220, 228, 240), 0.15f);
            Px(x, _canvasHeight - 1, Color.Black, 0.6f);
        }
        for (var y = _chamfer; y < _canvasHeight - _chamfer; y++)
        {
            Px(0, y, new Color(210, 220, 236), 0.3f);
            Px(_canvasWidth - 1, y, Color.Black, 0.5f);
        }

        // Edge bolts, two per straight side.
        var edgeBolt = MathF.Max(1.4f, size * 0.018f);
        foreach (var (bx, by) in new[]
                 {
                     (size * 0.38f, size * 0.05f), (size * 0.62f, size * 0.05f), (size * 0.38f, size * 0.95f), (size * 0.62f, size * 0.95f),
                     (size * 0.05f, size * 0.38f), (size * 0.05f, size * 0.62f), (size * 0.95f, size * 0.38f), (size * 0.95f, size * 0.62f),
                 })
            Bolt(bx, by, edgeBolt);

        // Cables off the hub into the deck, under everything round.
        foreach (var side in new[] { -1f, 1f })
        {
            var x0 = c + side * size * 0.12f;
            var x1 = c + side * size * 0.2f;
            for (var t = 0f; t <= 1f; t += 0.02f)
            {
                var x = x0 + (x1 - x0) * t;
                var y = c + size * 0.18f + size * 0.25f * t;
                Disc(x, y, MathF.Max(1.2f, size * 0.017f), new Color(22, 23, 26));
                Px(x - 0.5f, y - 0.5f, new Color(90, 94, 100), 0.4f);
            }
            Rect(x1 - size * 0.025f, c + size * 0.42f, size * 0.05f, size * 0.035f, new Color(50, 54, 60));
        }

        // The traverse ring: a dark groove, the ring itself lit along its top, a ring of bolts.
        var ringR = size * 0.36f;
        Disc(c + 1, c + 2, ringR + size * 0.03f, Color.Black, 0.35f);
        Disc(c, c, ringR + size * 0.03f, new Color(30, 32, 37));
        Disc(c, c, ringR, new Color(92, 98, 110));
        for (var a = 0f; a < MathF.PI * 2f; a += 0.01f)
        {
            var light = -MathF.Sin(a); // brightest at the top of the ring
            Px(c + MathF.Cos(a) * (ringR - 1), c + MathF.Sin(a) * (ringR - 1), light > 0 ? Color.White : Color.Black, MathF.Abs(light) * 0.35f);
        }
        for (var i = 0; i < 16; i++)
        {
            var a = i * MathF.PI / 8f + MathF.PI / 16f;
            Bolt(c + MathF.Cos(a) * ringR * 0.88f, c + MathF.Sin(a) * ringR * 0.88f, MathF.Max(1.2f, size * 0.014f));
        }

        // The rotating hub: a dome, lit from the top-left, seated in the ring.
        var hubR = size * 0.27f;
        Ring(c, c, hubR + 1, new Color(18, 19, 22), 0.9f, 1.4f);
        for (var y = (int)(c - hubR); y <= (int)(c + hubR); y++)
        for (var x = (int)(c - hubR); x <= (int)(c + hubR); x++)
        {
            var dx = (x - c) / hubR;
            var dy = (y - c) / hubR;
            var d2 = dx * dx + dy * dy;
            if (d2 > 1f)
                continue;
            var shade = 1f - 0.55f * MathF.Sqrt(d2) + 0.35f * MathHelper.Clamp(-(dx + dy) * 0.7f, -1f, 1f);
            var grain = (Hash(x * 5 + 77, y) - 0.5f) * 6f;
            Px(x, y, new Color(
                (int)MathHelper.Clamp(78 * shade + 28 + grain, 0, 255),
                (int)MathHelper.Clamp(84 * shade + 30 + grain, 0, 255),
                (int)MathHelper.Clamp(96 * shade + 34 + grain, 0, 255)));
        }
        Disc(c - hubR * 0.35f, c - hubR * 0.4f, hubR * 0.22f, Color.White, 0.1f);

        // Handles left and right of the hub: a steel arm out to a rubber grip, a red trigger on the
        // right one.
        foreach (var side in new[] { -1f, 1f })
        {
            var armFrom = c + side * hubR * 0.85f;
            var armTo = c + side * size * 0.42f;
            var armY = c + size * 0.02f;
            var left = MathF.Min(armFrom, armTo);
            Rect(left, armY - size * 0.02f + 1, MathF.Abs(armTo - armFrom), size * 0.04f, Color.Black, 0.35f);
            Rect(left, armY - size * 0.02f, MathF.Abs(armTo - armFrom), size * 0.04f, new Color(104, 110, 122));
            Rect(left, armY - size * 0.02f, MathF.Abs(armTo - armFrom), 1, Color.White, 0.35f);

            var gripX = armTo - size * 0.035f;
            var gripTop = armY - size * 0.1f;
            var gripH = size * 0.2f;
            var gripW = size * 0.07f;
            Rect(gripX + 1, gripTop + 1, gripW, gripH, Color.Black, 0.4f);
            Rect(gripX, gripTop, gripW, gripH, new Color(34, 35, 38));
            for (var k = 1; k < 6; k++)
                Rect(gripX, gripTop + gripH * k / 6f, gripW, 1, new Color(70, 72, 76), 0.8f);
            Rect(gripX, gripTop, 1, gripH, Color.White, 0.18f);
            if (side > 0)
                Rect(gripX - size * 0.025f, gripTop + gripH * 0.25f, size * 0.025f, size * 0.04f, lit ? new Color(206, 62, 48) : new Color(110, 44, 38));
        }

        // The periscope head on the hub: an armoured block lit along its top, the eyepiece well in
        // it (the live glow goes in there), a hood over it and a small status lamp bezel beneath.
        var headW = size * 0.26f;
        var headH = size * 0.18f;
        var headX = c - headW / 2f;
        var headY = size * PeriscopeLensY - headH * 0.55f;
        Rect(headX + 1.5f, headY + 2, headW, headH, Color.Black, 0.4f);
        Rect(headX, headY, headW, headH, new Color(58, 63, 72));
        Rect(headX, headY, headW, MathF.Max(1f, headH * 0.18f), new Color(122, 130, 142));
        Rect(headX, headY + headH - 1, headW, 1, Color.Black, 0.6f);
        Rect(headX, headY, 1, headH, Color.White, 0.2f);
        Rect(headX + headW - 1, headY, 1, headH, Color.Black, 0.4f);
        var lensX = size * PeriscopeLensX;
        var lensY = size * PeriscopeLensY;
        var lensR = size * PeriscopeLensRadius;
        Disc(lensX, lensY, lensR + 2, new Color(20, 22, 26));
        Ring(lensX, lensY, lensR + 1.5f, new Color(150, 156, 168), 0.6f, 0.8f);
        Disc(lensX, lensY, lensR, lit ? new Color(16, 40, 48) : new Color(12, 16, 18));
        Rect(headX - size * 0.015f, headY - size * 0.03f, headW + size * 0.03f, size * 0.035f, new Color(40, 44, 52));
        Rect(headX - size * 0.015f, headY - size * 0.03f, headW + size * 0.03f, 1, Color.White, 0.3f);
        Bolt(headX + size * 0.025f, headY + headH - size * 0.035f, MathF.Max(1f, size * 0.011f));
        Bolt(headX + headW - size * 0.025f, headY + headH - size * 0.035f, MathF.Max(1f, size * 0.011f));

        // Status lamp bezel and a vent grille on the hub's lower half.
        Disc(c, c + hubR * 0.62f, size * 0.03f, new Color(22, 24, 28));
        for (var k = 0; k < 3; k++)
        {
            var vy = c + hubR * 0.25f + k * size * 0.03f;
            Rect(c - hubR * 0.45f, vy, hubR * 0.9f, MathF.Max(1f, size * 0.012f), Color.Black, 0.45f);
            Rect(c - hubR * 0.45f, vy + MathF.Max(1f, size * 0.012f), hubR * 0.9f, 1, Color.White, 0.12f);
        }

        // Grime pooled low on the plate.
        for (var i = 0; i < 20; i++)
        {
            var gx = Hash(i * 3 + 401, 1) * size;
            var gy = size - 1 - Hash(i * 3 + 401, 2) * Hash(i * 3 + 401, 2) * size * 0.35f;
            Disc(gx, gy, (1f + Hash(i, 401) * 1.8f) * _u, new Color(26, 24, 22), 0.14f);
        }
    }

    // A hex-ish bolt head: dark body, lit top-left pixel.
    private void Bolt(float x, float y, float r)
    {
        Disc(x + 0.5f, y + 0.6f, r, Color.Black, 0.35f);
        Disc(x, y, r, new Color(54, 58, 66));
        Px(x - r * 0.4f, y - r * 0.4f, new Color(210, 218, 230), 0.6f);
    }
}
