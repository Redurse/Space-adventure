using System;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Direct user request ("сделай всем 4 приборам в рубке капитана такие же качественные текстуры
// вместо текущих", after the Barotrauma-style steel door) - the bridge's four consoles (scanner,
// navigation panel, ship status monitor, comms) used to be a square hero picture centred on a
// longer 2x1.5-tile plate, ringed by a thick saturated outline. Now each is a whole console laid out
// across its full plate the way Barotrauma's are: a recessed tray holding one big CRT in a heavy
// screwed bezel (scanlines, edge falloff, glare) next to a column of real hardware - knobs with a
// pointer and a shadow, raised buttons, LEDs that glow when powered - and a key strip under the
// screen. Everything is laid out in fractions of the actual canvas (not the 40-unit square), so the
// same drawing fills a wide plate and a tall one.
public sealed partial class DeviceSkin
{
    private (float X, float Y, float W, float H) _screen;
    private (float X, float Y, float W, float H) _controls;
    private (float X, float Y, float W, float H) _keys;

    // Housing, then the console's own tray split into a screen area, a control column beside it
    // (or below it on a tall plate) and a key strip under the screen.
    private void ConsoleBody(Color accent, int seed)
    {
        Housing(accent, seed);
        _offsetX = 0;
        _offsetY = 0;

        float w = _canvasWidth, h = _canvasHeight;
        var margin = _chamfer + MathF.Max(2f, U(1.2f));
        var top = MathF.Round(U(10.5f));
        var bottom = h - margin;
        var keyHeight = MathF.Max(2f, U(3.2f));
        // Housing's serial tag sits in the bottom-right corner - the controls stop short of it.
        var controlsBottom = h - _chamfer - MathF.Max(2f, U(3)) - 3;
        if (w >= h)
        {
            var split = MathF.Round(margin + (w - margin * 2) * 0.66f);
            _screen = (margin + 2, top + 2, split - margin - 4 - U(1.5f), bottom - top - keyHeight - U(3) - 4);
            _keys = (_screen.X, _screen.Y + _screen.H + U(2.2f) + 2, _screen.W, keyHeight);
            _controls = (split + U(0.5f), top, w - margin - split - U(0.5f), controlsBottom - top);
        }
        else
        {
            var split = MathF.Round(top + (bottom - top) * 0.56f);
            _screen = (margin + 2, top + 2, w - margin * 2 - 4, split - top - 4);
            _keys = (_screen.X, split + U(1), _screen.W, keyHeight);
            _controls = (margin, _keys.Y + keyHeight + U(1.5f), w - margin * 2, controlsBottom - _keys.Y - keyHeight - U(1.5f));
        }

        // The tray everything sits in: a shallow recess, lit lip along its bottom edge.
        var trayX = margin - 1;
        var trayW = w - margin * 2 + 2;
        Rect(trayX, top - 1, trayW, bottom - top + 2, Color.Black, 0.2f);
        Rect(trayX, bottom + 1, trayW, 1, Color.White, 0.12f);
    }

    // A heavy bezel round a CRT: steel lip lit along the top, black gasket, the glass, a screw in
    // each corner. The picture goes on next, then ConsoleScreenFinish over the top of it.
    private void ConsoleScreen((float X, float Y, float W, float H) s, Color glass, bool lit)
    {
        var (x, y, w, h) = s;
        Rect(x - 2, y - 2, w + 4, h + 4, new Color(38, 42, 50));
        Rect(x - 2, y - 2, w + 4, 1, new Color(176, 184, 196), 0.5f);
        Rect(x - 2, y + h + 1, w + 4, 1, Color.Black, 0.6f);
        Rect(x - 1, y - 1, w + 2, h + 2, new Color(8, 9, 11));
        Rect(x, y, w, h, lit ? glass : new Color(12, 15, 17));
        foreach (var (sx, sy) in new[] { (x - 2, y - 2), (x + w + 1, y - 2), (x - 2, y + h + 1), (x + w + 1, y + h + 1) })
            Px(sx, sy, new Color(160, 166, 176), 0.75f);
    }

    // Scanlines, the darker edge a curved tube has, and a streak of glare across the glass.
    private void ConsoleScreenFinish((float X, float Y, float W, float H) s, bool lit)
    {
        var (x, y, w, h) = s;
        if (lit)
            for (var yy = y + 1; yy < y + h; yy += 2)
                Rect(x, yy, w, 1, Color.Black, 0.18f);
        Rect(x, y, w, 1, Color.Black, 0.35f);
        Rect(x, y + h - 1, w, 1, Color.Black, 0.4f);
        Rect(x, y, 1, h, Color.Black, 0.3f);
        Rect(x + w - 1, y, 1, h, Color.Black, 0.35f);
        if (!lit)
        {
            Line(x + w * 0.15f, y + h * 0.2f, x + w * 0.35f, y + 1, Color.White, 0.08f);
            return;
        }
        Rect(x + 2, y + 1, w * 0.35f, 1, Color.White, 0.3f);
        Line(x + w * 0.62f, y + 1, x + w * 0.42f, y + h - 2, Color.White, 0.05f);
        Line(x + w * 0.66f, y + 1, x + w * 0.46f, y + h - 2, Color.White, 0.04f);
    }

    // Where the i-th of n controls goes - spread along the control area's longer side.
    private (float X, float Y) ControlSlot(int i, int n)
    {
        var c = _controls;
        return c.W >= c.H
            ? (c.X + c.W * (i + 0.5f) / n, c.Y + c.H / 2f)
            : (c.X + c.W / 2f, c.Y + c.H * (i + 0.5f) / n);
    }

    private float ControlSlotSize(int n)
    {
        var c = _controls;
        return c.W >= c.H ? MathF.Min(c.H, c.W / n) : MathF.Min(c.W, c.H / n);
    }

    private void ConsoleKnob(float cx, float cy, float r, float angle, Color mark)
    {
        r = MathF.Max(1.5f, r);
        Disc(cx + 0.7f, cy + 1f, r, Color.Black, 0.35f);
        Disc(cx, cy, r, new Color(44, 48, 56));
        Ring(cx, cy, r - 0.5f, new Color(120, 126, 138), 0.55f, 0.8f);
        Disc(cx - r * 0.3f, cy - r * 0.35f, r * 0.4f, Color.White, 0.12f);
        Line(cx, cy, cx + MathF.Cos(angle) * r * 0.85f, cy + MathF.Sin(angle) * r * 0.85f, mark, 0.95f);
    }

    private void ConsoleButton(float x, float y, float size, Color cap, bool glowing)
    {
        size = MathF.Max(2f, size);
        Rect(x + 0.5f, y + 1, size, size, Color.Black, 0.4f);
        Rect(x, y, size, size, cap);
        Rect(x, y, size, 1, Color.White, 0.35f);
        Rect(x + size - 1, y, 1, size, Color.Black, 0.25f);
        if (glowing)
            Disc(x + size / 2f, y + size / 2f, size * 0.9f, cap, 0.14f);
    }

    private void ConsoleLed(float x, float y, Color colour, bool on)
    {
        if (on)
            Disc(x, y, MathF.Max(1.6f, U(2f)), colour, 0.16f);
        Disc(x, y, MathF.Max(0.9f, U(0.9f)), on ? colour : new Color(42, 44, 48));
        if (on)
            Px(x - 0.5f, y - 0.5f, Color.White, 0.5f);
    }

    // The key strip under the screen: a row of chunky keys, a couple of them back-lit.
    private void ConsoleKeys(Color accent, bool lit, int seed)
    {
        var (x, y, w, h) = _keys;
        Rect(x - 1, y - 1, w + 2, h + 2, new Color(30, 33, 39));
        var key = MathF.Max(2f, h - 1);
        var count = Math.Max(3, (int)(w / (key + MathF.Max(1f, U(1f)))));
        var step = w / count;
        for (var i = 0; i < count; i++)
        {
            var on = lit && Hash(i, seed) > 0.7f;
            ConsoleButton(x + i * step + (step - key) / 2f, y, key, on ? accent : new Color(70, 75, 84), on);
        }
    }

    // ---------------------------------------------------------------- the four consoles

    // Сканер: a round radar scope with range rings, a sweeping beam leaving its afterglow behind it,
    // and contacts; gain and range knobs and a mode button row beside it.
    private void Navigation(bool lit)
    {
        var accent = new Color(72, 196, 208);
        ConsoleBody(accent, 61);
        var s = _screen;
        ConsoleScreen(s, new Color(8, 30, 32), lit);

        var cx = s.X + s.W / 2f;
        var cy = s.Y + s.H / 2f;
        var r = MathF.Min(s.W, s.H) / 2f - 1.5f;
        var phosphor = lit ? new Color(96, 236, 214) : new Color(28, 46, 46);
        foreach (var f in new[] { 0.34f, 0.67f, 1f })
            Ring(cx, cy, r * f, phosphor, lit ? 0.34f : 0.22f, 0.7f);
        Line(cx - r, cy, cx + r, cy, phosphor, 0.16f);
        Line(cx, cy - r, cx, cy + r, phosphor, 0.16f);
        for (var i = 0; i < 12; i++)
        {
            var a = i * MathF.PI / 6f;
            Line(cx + MathF.Cos(a) * r * 0.9f, cy + MathF.Sin(a) * r * 0.9f, cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r, phosphor, 0.4f);
        }
        if (lit)
        {
            const float beam = -0.75f;
            for (var k = 0; k < 18; k++)
            {
                var a = beam - k * 0.05f;
                Line(cx, cy, cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r, phosphor, 0.42f * (1f - k / 18f));
            }
            Line(cx, cy, cx + MathF.Cos(beam) * r, cy + MathF.Sin(beam) * r, new Color(220, 255, 246), 0.95f);
            for (var i = 0; i < 5; i++)
            {
                var a = Hash(i, 61) * MathF.PI * 2f;
                var d = (0.25f + Hash(i, 62) * 0.65f) * r;
                var bx = cx + MathF.Cos(a) * d;
                var by = cy + MathF.Sin(a) * d;
                Disc(bx, by, MathF.Max(1.6f, U(1.8f)), new Color(160, 255, 210), 0.22f);
                Disc(bx, by, MathF.Max(0.8f, U(0.8f)), new Color(220, 255, 236));
            }
        }
        Disc(cx, cy, MathF.Max(0.8f, U(0.7f)), phosphor, 0.8f);
        ConsoleScreenFinish(s, lit);

        var size = ControlSlotSize(3);
        var (k1x, k1y) = ControlSlot(0, 3);
        ConsoleKnob(k1x, k1y, size * 0.32f, -2.2f, accent);
        var (k2x, k2y) = ControlSlot(1, 3);
        ConsoleKnob(k2x, k2y, size * 0.32f, -0.6f, accent);
        var (bx0, by0) = ControlSlot(2, 3);
        var button = MathF.Max(2f, size * 0.26f);
        ConsoleButton(bx0 - button * 1.6f, by0 - button / 2f, button, lit ? accent : new Color(60, 66, 74), lit);
        ConsoleButton(bx0 - button / 2f, by0 - button / 2f, button, new Color(70, 75, 84), false);
        ConsoleButton(bx0 + button * 0.6f, by0 - button / 2f, button, lit ? new Color(220, 80, 64) : new Color(70, 52, 50), lit);
        ConsoleKeys(accent, lit, 61);
    }

    // Навигационная панель: a star chart with a plotted route, the ship's marker at its start and
    // a target ring at its end; a throttle in a slotted track and the flight stick beside it.
    private void Helm(bool lit)
    {
        var accent = new Color(228, 172, 74);
        ConsoleBody(accent, 71);
        var s = _screen;
        ConsoleScreen(s, new Color(10, 18, 34), lit);

        var grid = lit ? new Color(70, 110, 170) : new Color(24, 32, 44);
        for (var gx = s.X + s.W / 6f; gx < s.X + s.W - 1; gx += s.W / 6f)
            Line(gx, s.Y + 1, gx, s.Y + s.H - 2, grid, 0.18f);
        for (var gy = s.Y + s.H / 4f; gy < s.Y + s.H - 1; gy += s.H / 4f)
            Line(s.X + 1, gy, s.X + s.W - 2, gy, grid, 0.18f);
        for (var i = 0; i < 22; i++)
            Px(s.X + 1 + Hash(i, 71) * (s.W - 2), s.Y + 1 + Hash(i, 72) * (s.H - 2),
                lit ? new Color(200, 214, 255) : new Color(40, 44, 56), lit ? 0.35f + Hash(i, 73) * 0.6f : 0.6f);

        var route = new (float X, float Y)[] { (0.14f, 0.78f), (0.38f, 0.56f), (0.6f, 0.62f), (0.84f, 0.24f) };
        var routeColour = lit ? new Color(255, 206, 110) : new Color(70, 60, 40);
        for (var i = 0; i < route.Length - 1; i++)
        {
            var (ax, ay) = (s.X + route[i].X * s.W, s.Y + route[i].Y * s.H);
            var (bx, by) = (s.X + route[i + 1].X * s.W, s.Y + route[i + 1].Y * s.H);
            const int dashes = 6;
            for (var d = 0; d < dashes; d++)
            {
                var t0 = d / (float)dashes;
                var t1 = t0 + 0.6f / dashes;
                Line(ax + (bx - ax) * t0, ay + (by - ay) * t0, ax + (bx - ax) * t1, ay + (by - ay) * t1, routeColour, 0.85f);
            }
            Disc(bx, by, MathF.Max(0.8f, U(0.8f)), routeColour);
        }
        var (tx, ty) = (s.X + route[^1].X * s.W, s.Y + route[^1].Y * s.H);
        Ring(tx, ty, MathF.Max(2f, U(2.4f)), lit ? new Color(255, 120, 90) : new Color(70, 44, 40), 0.85f, 0.7f);
        var (shipX, shipY) = (s.X + route[0].X * s.W, s.Y + route[0].Y * s.H);
        var ship = lit ? new Color(140, 255, 170) : new Color(40, 60, 46);
        var arm = MathF.Max(1.5f, U(1.8f));
        Line(shipX - arm, shipY + arm, shipX + arm, shipY - arm * 0.2f, ship);
        Line(shipX - arm * 0.2f, shipY - arm, shipX + arm, shipY - arm * 0.2f, ship);
        Line(shipX - arm, shipY + arm, shipX - arm * 0.2f, shipY - arm, ship);
        ConsoleScreenFinish(s, lit);

        var size = ControlSlotSize(2);
        var (thx, thy) = ControlSlot(0, 2);
        var c = _controls;
        var vertical = c.H > c.W;
        var trackLong = size * 0.8f;
        var trackThin = MathF.Max(1.5f, size * 0.12f);
        if (vertical)
        {
            Rect(thx - trackThin / 2f, thy - trackLong / 2f, trackThin, trackLong, new Color(14, 15, 18));
            Rect(thx - size * 0.28f, thy - trackLong * 0.1f, size * 0.56f, MathF.Max(2f, size * 0.16f), new Color(150, 156, 166));
            Rect(thx - size * 0.28f, thy - trackLong * 0.1f, size * 0.56f, 1, Color.White, 0.4f);
            Disc(thx + size * 0.3f, thy - trackLong * 0.02f, MathF.Max(1.2f, size * 0.12f), new Color(206, 84, 60));
        }
        else
        {
            Rect(thx - trackLong / 2f, thy - trackThin / 2f, trackLong, trackThin, new Color(14, 15, 18));
            Rect(thx + trackLong * 0.1f, thy - size * 0.28f, MathF.Max(2f, size * 0.16f), size * 0.56f, new Color(150, 156, 166));
            Disc(thx + trackLong * 0.15f, thy - size * 0.3f, MathF.Max(1.2f, size * 0.12f), new Color(206, 84, 60));
        }
        var (jx, jy) = ControlSlot(1, 2);
        Disc(jx, jy, size * 0.34f, new Color(30, 32, 38));
        Ring(jx, jy, size * 0.34f, new Color(96, 102, 114), 0.6f, 0.8f);
        Disc(jx + 0.8f, jy + 1.2f, size * 0.18f, Color.Black, 0.35f);
        Disc(jx, jy, size * 0.18f, new Color(64, 68, 78));
        Disc(jx - size * 0.05f, jy - size * 0.06f, size * 0.09f, new Color(210, 74, 56));
        Px(jx - size * 0.08f, jy - size * 0.09f, Color.White, 0.5f);
        ConsoleKeys(accent, lit, 71);
    }

    // Монитор состояния: the hull in plan view with every compartment's state lit in its cell, and
    // three level bars down the screen's side; a row of status lamps and a page knob beside it.
    private void ShipStatusMonitor(bool lit)
    {
        var accent = new Color(120, 220, 150);
        ConsoleBody(accent, 191);
        var s = _screen;
        ConsoleScreen(s, new Color(6, 24, 14), lit);

        var plot = (X: s.X + 2, Y: s.Y + 2, W: s.W * 0.66f - 3, H: s.H - 4);
        var trace = lit ? new Color(170, 240, 190) : new Color(50, 66, 56);
        var hull = new (float X, float Y)[] { (0.5f, 0.02f), (0.78f, 0.22f), (0.9f, 0.58f), (0.74f, 0.96f), (0.26f, 0.96f), (0.1f, 0.58f), (0.22f, 0.22f) };
        for (var i = 0; i < hull.Length; i++)
        {
            var (x0, y0) = hull[i];
            var (x1, y1) = hull[(i + 1) % hull.Length];
            Line(plot.X + x0 * plot.W, plot.Y + y0 * plot.H, plot.X + x1 * plot.W, plot.Y + y1 * plot.H, trace, lit ? 0.9f : 0.6f);
        }
        var cellColours = new[]
        {
            new Color(90, 210, 120), new Color(90, 210, 120), new Color(232, 180, 60),
            new Color(90, 210, 120), new Color(214, 76, 64), new Color(90, 210, 120),
        };
        var cells = new (float X, float Y)[] { (0.38f, 0.32f), (0.62f, 0.32f), (0.38f, 0.56f), (0.62f, 0.56f), (0.38f, 0.8f), (0.62f, 0.8f) };
        var cellW = plot.W * 0.2f;
        var cellH = plot.H * 0.16f;
        for (var i = 0; i < cells.Length; i++)
            Rect(plot.X + cells[i].X * plot.W - cellW / 2f, plot.Y + cells[i].Y * plot.H - cellH / 2f, cellW, cellH,
                lit ? cellColours[i] : new Color(30, 40, 34), lit ? 0.8f : 1f);

        var barsX = s.X + s.W * 0.7f;
        var barW = MathF.Max(1f, (s.W * 0.28f - 2) / 3f - 1);
        for (var i = 0; i < 3; i++)
        {
            var level = 0.35f + Hash(i, 191) * 0.6f;
            var bx = barsX + i * (barW + 1);
            Rect(bx, s.Y + 2, barW, s.H - 4, new Color(10, 30, 18));
            var filled = (s.H - 4) * level;
            Rect(bx, s.Y + s.H - 2 - filled, barW, filled, lit ? (level > 0.75f ? new Color(232, 180, 60) : new Color(110, 220, 140)) : new Color(26, 38, 30), 0.85f);
        }
        ConsoleScreenFinish(s, lit);

        var size = ControlSlotSize(4);
        var lamps = new[] { new Color(90, 220, 120), new Color(240, 186, 60), new Color(226, 80, 64) };
        for (var i = 0; i < 3; i++)
        {
            var (lx, ly) = ControlSlot(i, 4);
            Disc(lx, ly, MathF.Max(1.6f, size * 0.26f), new Color(30, 32, 38));
            ConsoleLed(lx, ly, lamps[i], lit && i != 2);
        }
        var (kx, ky) = ControlSlot(3, 4);
        ConsoleKnob(kx, ky, size * 0.34f, 0.9f, accent);
        ConsoleKeys(accent, lit, 191);
    }

    // Связь: a waveform trace over a spectrum, the tuning dial, a speaker grille and the handset
    // hanging on its hook.
    private void CommsConsole(bool lit)
    {
        var accent = new Color(220, 170, 90);
        ConsoleBody(accent, 199);
        var s = _screen;
        ConsoleScreen(s, new Color(30, 20, 6), lit);

        var trace = lit ? new Color(255, 206, 120) : new Color(66, 52, 34);
        var midY = s.Y + s.H * 0.38f;
        var amplitude = s.H * 0.22f;
        float? lastX = null, lastY = null;
        for (var x = s.X + 1; x < s.X + s.W - 1; x += 0.5f)
        {
            var t = (x - s.X) / s.W;
            var y = midY + MathF.Sin(t * 19f) * amplitude * (0.35f + 0.65f * MathF.Sin(t * 3.1f + 0.4f) * MathF.Sin(t * 3.1f + 0.4f));
            if (lastX is { } lx && lastY is { } ly)
                Line(lx, ly, x, y, trace, 0.85f);
            lastX = x;
            lastY = y;
        }
        Line(s.X + 1, midY, s.X + s.W - 2, midY, trace, 0.15f);
        var bars = Math.Max(6, (int)(s.W / MathF.Max(2f, U(2.2f))));
        var barW = (s.W - 2) / bars;
        for (var i = 0; i < bars; i++)
        {
            var level = 0.2f + Hash(i, 199) * 0.8f * (1f - MathF.Abs(i - bars * 0.4f) / bars);
            var bh = s.H * 0.3f * level;
            Rect(s.X + 1 + i * barW, s.Y + s.H - 2 - bh, MathF.Max(1f, barW - 1), bh,
                lit ? new Color(240, 150, 70) : new Color(54, 38, 26), lit ? 0.75f : 1f);
        }
        ConsoleScreenFinish(s, lit);

        var size = ControlSlotSize(3);
        var (dx, dy) = ControlSlot(0, 3);
        var dial = size * 0.4f;
        for (var i = 0; i < 10; i++)
        {
            var a = MathF.PI * (0.75f + i * 0.15f);
            Line(dx + MathF.Cos(a) * dial * 1.05f, dy + MathF.Sin(a) * dial * 1.05f,
                dx + MathF.Cos(a) * dial * 1.25f, dy + MathF.Sin(a) * dial * 1.25f, new Color(170, 176, 186), 0.5f);
        }
        ConsoleKnob(dx, dy, dial, -1.1f, accent);

        var (gx, gy) = ControlSlot(1, 3);
        var grille = size * 0.38f;
        Disc(gx, gy, grille, new Color(28, 30, 34));
        for (var yy = gy - grille + 1; yy < gy + grille; yy += MathF.Max(1.5f, U(1.4f)))
            Line(gx - grille * 0.8f, yy, gx + grille * 0.8f, yy, Color.Black, 0.55f);
        Ring(gx, gy, grille, new Color(110, 116, 128), 0.55f, 0.7f);

        var (hx, hy) = ControlSlot(2, 3);
        var handset = size * 0.36f;
        Rect(hx - handset * 0.25f + 0.6f, hy - handset + 1, handset * 0.5f, handset * 2f, Color.Black, 0.35f);
        Rect(hx - handset * 0.25f, hy - handset, handset * 0.5f, handset * 2f, new Color(34, 36, 42));
        Rect(hx - handset * 0.25f, hy - handset, handset * 0.5f, 1, Color.White, 0.25f);
        Disc(hx, hy - handset * 0.7f, handset * 0.32f, new Color(52, 56, 64));
        Disc(hx, hy + handset * 0.7f, handset * 0.32f, new Color(52, 56, 64));
        ConsoleLed(hx + handset * 0.55f, hy - handset * 0.8f, new Color(120, 230, 120), lit);
        ConsoleKeys(accent, lit, 199);
    }
}
