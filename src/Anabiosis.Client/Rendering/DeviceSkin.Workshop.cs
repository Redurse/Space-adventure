using System;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Direct user request ("обнови текстуры для всех устройств по аналогии как мы делали уже со
// сделанными") - the elongated devices (bed, triple door, fabricator, deconstructor, both
// workbenches, shuttle hangar, junction box) used to paint a 40-unit square picture in the middle of
// their plate and leave the rest of a 2x3/2x4/5x6 footprint as bare steel. Redrawn the way the bridge
// consoles were (DeviceSkin.BridgeConsoles.cs): the whole plate is the machine, Barotrauma / Space
// Haven style - steel blocks lit from above, hazard tape where things pinch, CRTs in bolted bezels,
// real hardware filling every part of the footprint.
//
// Each is drawn once in a PORTRAIT local space (taller than wide). A device placed rotated - a
// landscape canvas - is drawn through the same code, turned a quarter turn by Px (_rotated), so it
// reads as the same machine on its side rather than a squashed one.
public sealed partial class DeviceSkin
{
    private bool _rotated;
    // The usable part of the plate below Housing's painted band, in local portrait coordinates.
    private (float X, float Y, float W, float H) _area;

    private void PortraitBody(Color accent, int seed)
    {
        Housing(accent, seed);
        _offsetX = 0;
        _offsetY = 0;
        float w = _canvasWidth, h = _canvasHeight;
        var margin = MathF.Max(2f, _chamfer * 0.5f) + MathF.Max(1f, U(1f));
        var top = MathF.Round(U(10.5f));
        // The usable canvas rect, then the same rect seen from the local portrait frame (Px maps
        // local (x, y) to canvas (y, h - 1 - x) while _rotated).
        float x0 = margin, y0 = top, x1 = w - margin, y1 = h - margin;
        _rotated = w > h;
        _area = _rotated ? (h - 1 - y1, x0, y1 - y0, x1 - x0) : (x0, y0, x1 - x0, y1 - y0);
    }

    // For a device that IS a piece of floor rather than a machine standing on one (the shuttle bay):
    // no plate, no painted band, no chamfer - the whole footprint is deck.
    private void FloorBody(Color deck)
    {
        _chamfer = 0;
        _offsetX = 0;
        _offsetY = 0;
        _rotated = false;
        Rect(0, 0, _canvasWidth, _canvasHeight, deck);
        for (var i = 0; i < 60; i++)
            Disc(Hash(i, 311) * _canvasWidth, Hash(i, 312) * _canvasHeight, (1f + Hash(i, 313) * 2.5f) * _u, new Color(24, 24, 26), 0.1f);
        float w = _canvasWidth, h = _canvasHeight;
        _rotated = w > h;
        _area = _rotated ? (0, 0, h, w) : (0, 0, w, h);
    }

    private float AX(float f) => _area.X + _area.W * f;
    private float AY(float f) => _area.Y + _area.H * f;
    private float AW(float f) => _area.W * f;
    private float AH(float f) => _area.H * f;

    // A raised steel block: fill, lit top/left edge, shadowed bottom/right edge.
    private void SteelBlock(float x, float y, float w, float h, Color fill)
    {
        Rect(x, y, w, h, fill);
        Rect(x, y, w, 1, Color.White, 0.3f);
        Rect(x, y, 1, h, Color.White, 0.14f);
        Rect(x, y + h - 1, w, 1, Color.Black, 0.45f);
        Rect(x + w - 1, y, 1, h, Color.Black, 0.3f);
    }

    // Diagonal yellow/black hazard tape filling a rect.
    private void HazardTape(float x, float y, float w, float h)
    {
        var stripe = MathF.Max(2f, U(2.2f));
        for (var yy = (int)MathF.Round(y); yy < (int)MathF.Round(y + h); yy++)
        for (var xx = (int)MathF.Round(x); xx < (int)MathF.Round(x + w); xx++)
            Px(xx, yy, ((int)MathF.Floor((xx + yy) / stripe) & 1) == 0 ? new Color(226, 184, 52) : new Color(26, 25, 22), 0.92f);
    }

    // Steel bands lit from the left, for a slab running top-to-bottom.
    private void SideLitBands(float x, float y, float w, float h, Color[] bands)
    {
        for (var i = 0; i < bands.Length; i++)
            Rect(x + w * i / bands.Length, y, MathF.Max(1f, w / bands.Length), h, bands[i]);
    }

    private static readonly Color[] SlabSteel = { new(160, 168, 178), new(134, 142, 154), new(110, 118, 130), new(88, 94, 106) };

    // ---------------------------------------------------------------- the devices

    // A bunk seen from above: steel frame and headboard with a reading lamp, a quilted mattress, a
    // pillow, a folded-back blanket, a footboard.
    private void Bed(bool lit)
    {
        var accent = new Color(96, 150, 170);
        PortraitBody(accent, 151);
        var a = _area;
        SteelBlock(a.X, a.Y, a.W, a.H, new Color(62, 66, 76));
        SteelBlock(a.X, a.Y, a.W, AH(0.1f), new Color(86, 92, 104));
        var lampX = AX(0.8f);
        var lampY = a.Y + AH(0.05f);
        if (lit)
            Disc(lampX, lampY, MathF.Max(2f, AW(0.12f)), new Color(255, 214, 140), 0.2f);
        Disc(lampX, lampY, MathF.Max(1.2f, AW(0.07f)), lit ? new Color(255, 226, 160) : new Color(84, 80, 72));

        var mx = a.X + 2;
        var my = a.Y + AH(0.11f);
        var mw = a.W - 4;
        var mh = AH(0.85f);
        Rect(mx, my, mw, mh, new Color(112, 124, 140));
        for (var yy = my + 3; yy < my + mh; yy += MathF.Max(3f, U(4f)))
            Rect(mx, yy, mw, 1, Color.Black, 0.12f);
        Rect(mx + mw / 2f, my, 1, mh, Color.Black, 0.1f);
        Rect(mx + mw - 2, my, 2, mh, Color.Black, 0.18f);

        var ph = AH(0.14f);
        Rect(mx + 2, my + 2, mw - 4, ph, new Color(208, 212, 218));
        Rect(mx + 2, my + 2, mw - 4, 1, Color.White, 0.5f);
        Rect(mx + 2, my + 2 + ph - 1, mw - 4, 1, Color.Black, 0.3f);
        Line(mx + mw / 2f, my + 4, mx + mw / 2f, my + ph - 1, new Color(150, 156, 166), 0.5f);

        var by = a.Y + AH(0.46f);
        var bh = a.Y + a.H - AH(0.05f) - by;
        Rect(mx, by, mw, bh, new Color(46, 84, 98));
        Rect(mx, by, mw, MathF.Max(2f, U(2.5f)), new Color(74, 124, 138));
        Rect(mx, by, mw, 1, Color.White, 0.3f);
        foreach (var f in new[] { 0.3f, 0.75f })
            Rect(mx, by + bh * f, mw, 1, accent, 0.55f);
        Line(mx + mw * 0.2f, by + bh * 0.2f, mx + mw * 0.45f, by + bh * 0.55f, Color.Black, 0.18f);
        Line(mx + mw * 0.7f, by + bh * 0.1f, mx + mw * 0.85f, by + bh * 0.5f, Color.Black, 0.15f);
        SteelBlock(a.X, a.Y + a.H - AH(0.05f), a.W, AH(0.05f), new Color(86, 92, 104));
    }

    // A wide bulkhead door: frame posts with lamps at both ends, three ribbed steel slabs in a dark
    // track, hazard tape down both sides.
    private void TripleDoor(bool lit)
    {
        var accent = new Color(226, 184, 52);
        PortraitBody(accent, 223);
        var a = _area;
        var post = AH(0.07f);
        var top = a.Y + post;
        var bottom = a.Y + a.H - post;
        HazardTape(a.X, top, AW(0.14f), bottom - top);
        HazardTape(a.X + a.W - AW(0.14f), top, AW(0.14f), bottom - top);
        Rect(AX(0.14f), top, AW(0.72f), bottom - top, new Color(24, 26, 32));

        var slabX = AX(0.2f);
        var slabW = AW(0.6f);
        var slabH = (bottom - top) / 3f;
        for (var i = 0; i < 3; i++)
        {
            var sy = top + i * slabH;
            SideLitBands(slabX, sy, slabW, slabH - 1, SlabSteel);
            for (var ry = sy + 4; ry < sy + slabH - 3; ry += MathF.Max(4f, U(5f)))
            {
                Rect(slabX, ry, slabW, 1, new Color(58, 64, 74));
                Rect(slabX, ry + 1, slabW, 1, Color.White, 0.18f);
            }
            Rect(slabX, sy + slabH - 2, slabW, 2, new Color(20, 22, 26));
            Rect(slabX, sy, 1, slabH - 1, Color.White, 0.2f);
            Rect(slabX + slabW - 1, sy, 1, slabH - 1, Color.Black, 0.4f);
        }

        foreach (var py in new[] { a.Y, a.Y + a.H - post })
        {
            SteelBlock(a.X, py, a.W, post, new Color(64, 70, 80));
            Bolt(a.X + AW(0.12f), py + post / 2f, MathF.Max(1f, U(1f)));
            Bolt(a.X + a.W - AW(0.12f), py + post / 2f, MathF.Max(1f, U(1f)));
            ConsoleLed(AX(0.5f), py + post / 2f, new Color(110, 230, 120), lit);
        }
    }

    // A tall print fabricator: job screen with a progress bar on top, the build chamber behind
    // glass (gantry, glowing print head, a part half-printed on the bed), feed spools and vents below.
    private void Fabricator(bool lit)
    {
        var accent = new Color(90, 190, 220);
        PortraitBody(accent, 181);
        var a = _area;

        var s = (X: AX(0.08f), Y: a.Y + 2, W: AW(0.84f), H: AH(0.17f));
        ConsoleScreen(s, new Color(8, 26, 34), lit);
        var cyan = lit ? new Color(110, 220, 240) : new Color(30, 52, 60);
        Rect(s.X + s.W * 0.1f, s.Y + s.H * 0.68f, s.W * 0.8f, MathF.Max(2f, s.H * 0.14f), new Color(10, 40, 50));
        Rect(s.X + s.W * 0.1f, s.Y + s.H * 0.68f, s.W * 0.8f * 0.62f, MathF.Max(2f, s.H * 0.14f), cyan, 0.9f);
        Line(s.X + s.W * 0.15f, s.Y + s.H * 0.25f, s.X + s.W * 0.55f, s.Y + s.H * 0.25f, cyan, 0.6f);
        Line(s.X + s.W * 0.15f, s.Y + s.H * 0.45f, s.X + s.W * 0.4f, s.Y + s.H * 0.45f, cyan, 0.45f);
        Ring(s.X + s.W * 0.78f, s.Y + s.H * 0.36f, MathF.Max(1.5f, s.H * 0.18f), cyan, 0.7f, 0.7f);
        ConsoleScreenFinish(s, lit);

        var c = (X: AX(0.06f), Y: AY(0.23f), W: AW(0.88f), H: AH(0.45f));
        SteelBlock(c.X - 2, c.Y - 2, c.W + 4, c.H + 4, new Color(70, 76, 86));
        Rect(c.X, c.Y, c.W, c.H, new Color(16, 18, 22));
        Rect(c.X + 2, c.Y + 2, 2, c.H - 4, new Color(120, 126, 138));
        Rect(c.X + c.W - 4, c.Y + 2, 2, c.H - 4, new Color(120, 126, 138));
        var barY = c.Y + c.H * 0.38f;
        SteelBlock(c.X + 2, barY, c.W - 4, MathF.Max(2f, U(2.4f)), new Color(96, 102, 114));
        var headX = c.X + c.W * 0.56f;
        SteelBlock(headX - U(3), barY - U(1.5f), U(6), U(5.5f), new Color(70, 76, 86));
        var nozzleY = barY + U(4.5f);
        if (lit)
            Disc(headX, nozzleY, MathF.Max(2f, U(3.2f)), new Color(255, 170, 80), 0.25f);
        Disc(headX, nozzleY, MathF.Max(1f, U(1.1f)), lit ? new Color(255, 214, 150) : new Color(70, 58, 48));
        var bedY = c.Y + c.H - MathF.Max(3f, U(3.5f));
        SteelBlock(c.X + 3, bedY, c.W - 6, MathF.Max(3f, U(3.5f)) - 1, new Color(110, 116, 128));
        for (var i = 0; i < 5; i++)
        {
            var lw = c.W * (0.42f - i * 0.05f);
            var ly = bedY - (i + 1) * MathF.Max(1.2f, U(1.3f));
            Rect(c.X + c.W * 0.5f - lw / 2f, ly, lw, MathF.Max(1f, U(1.2f)),
                i == 4 && lit ? new Color(255, 170, 80) : new Color(170, 176, 186 - i * 8));
        }
        Line(c.X + c.W * 0.15f, c.Y + 2, c.X + c.W * 0.05f, c.Y + c.H * 0.5f, Color.White, 0.08f);
        Line(c.X + c.W * 0.3f, c.Y + 2, c.X + c.W * 0.15f, c.Y + c.H * 0.7f, Color.White, 0.06f);

        for (var i = 0; i < 3; i++)
            ConsoleLed(AX(0.3f + i * 0.2f), AY(0.72f), i == 0 ? new Color(110, 230, 120) : i == 1 ? accent : new Color(240, 186, 60), lit && i != 2);
        foreach (var (fx, filament) in new[] { (0.28f, accent), (0.72f, new Color(230, 140, 70)) })
        {
            var r = MathF.Min(AW(0.17f), AH(0.07f));
            Disc(AX(fx) + 0.6f, AY(0.82f) + 1, r, Color.Black, 0.35f);
            Disc(AX(fx), AY(0.82f), r, new Color(40, 42, 48));
            Ring(AX(fx), AY(0.82f), r * 0.75f, filament, 0.9f, MathF.Max(1f, r * 0.25f));
            Disc(AX(fx), AY(0.82f), r * 0.3f, new Color(150, 156, 166));
        }
        for (var k = 0; k < 3; k++)
        {
            var vy = AY(0.91f) + k * MathF.Max(2f, U(2f));
            Rect(AX(0.15f), vy, AW(0.7f), 1, Color.Black, 0.5f);
            Rect(AX(0.15f), vy + 1, AW(0.7f), 1, Color.White, 0.12f);
        }
    }

    // A recycler: amber readout on top, a hazard-framed shredder throat with two toothed rollers,
    // an output tray of scrap, a big red emergency stop.
    private void Deconstructor(bool lit)
    {
        var accent = new Color(220, 110, 60);
        PortraitBody(accent, 187);
        var a = _area;

        var s = (X: AX(0.08f), Y: a.Y + 2, W: AW(0.84f), H: AH(0.15f));
        ConsoleScreen(s, new Color(34, 18, 6), lit);
        var amber = lit ? new Color(255, 170, 70) : new Color(70, 46, 24);
        for (var i = 0; i < 7; i++)
        {
            var bh = s.H * (0.25f + Hash(i, 187) * 0.6f);
            Rect(s.X + 2 + i * (s.W - 4) / 7f, s.Y + s.H - 2 - bh, MathF.Max(1f, (s.W - 4) / 7f - 1), bh, amber, 0.85f);
        }
        ConsoleScreenFinish(s, lit);

        var t = (X: AX(0.06f), Y: AY(0.22f), W: AW(0.88f), H: AH(0.38f));
        var tape = MathF.Max(2f, U(2.5f));
        HazardTape(t.X, t.Y, t.W, tape);
        HazardTape(t.X, t.Y + t.H - tape, t.W, tape);
        HazardTape(t.X, t.Y, tape, t.H);
        HazardTape(t.X + t.W - tape, t.Y, tape, t.H);
        var pit = (X: t.X + tape, Y: t.Y + tape, W: t.W - tape * 2, H: t.H - tape * 2);
        Rect(pit.X, pit.Y, pit.W, pit.H, new Color(10, 10, 12));
        if (lit)
            Rect(pit.X, pit.Y + pit.H * 0.46f, pit.W, pit.H * 0.08f, new Color(255, 90, 50), 0.35f);
        foreach (var (fy, phase) in new[] { (0.3f, 0), (0.7f, 1) })
        {
            var ry = pit.Y + pit.H * fy;
            var rh = pit.H * 0.26f;
            SideLitBands(pit.X + 1, ry - rh / 2f, pit.W - 2, rh, new[] { new Color(96, 100, 108), new Color(130, 136, 146), new Color(110, 116, 126), new Color(80, 84, 92) });
            var tooth = MathF.Max(2f, U(2.4f));
            for (var tx = pit.X + 1 + phase * tooth; tx < pit.X + pit.W - tooth; tx += tooth * 2)
                Rect(tx, ry - rh / 2f, tooth, rh, new Color(52, 54, 60));
            Rect(pit.X + 1, ry - rh / 2f, pit.W - 2, 1, Color.White, 0.3f);
        }

        var tray = (X: AX(0.1f), Y: AY(0.65f), W: AW(0.8f), H: AH(0.17f));
        SteelBlock(tray.X - 1, tray.Y - 1, tray.W + 2, tray.H + 2, new Color(76, 82, 92));
        Rect(tray.X + 1, tray.Y + 1, tray.W - 2, tray.H - 2, new Color(28, 30, 34));
        for (var i = 0; i < 11; i++)
        {
            var sx = tray.X + 2 + Hash(i, 188) * (tray.W - 6);
            var sy = tray.Y + 2 + Hash(i, 189) * (tray.H - 6);
            var colour = (i % 3) switch { 0 => new Color(150, 156, 166), 1 => new Color(150, 90, 50), _ => new Color(190, 130, 70) };
            Rect(sx, sy, MathF.Max(1.5f, U(2f) * (0.6f + Hash(i, 190))), MathF.Max(1f, U(1.4f)), colour);
        }

        var stop = (X: AX(0.74f), Y: AY(0.91f));
        var stopR = MathF.Min(AW(0.12f), AH(0.05f));
        Disc(stop.X, stop.Y, stopR + 1.5f, new Color(226, 184, 52));
        Disc(stop.X + 0.6f, stop.Y + 0.8f, stopR, Color.Black, 0.35f);
        Disc(stop.X, stop.Y, stopR, new Color(200, 46, 36));
        Disc(stop.X - stopR * 0.3f, stop.Y - stopR * 0.35f, stopR * 0.35f, Color.White, 0.3f);
        for (var k = 0; k < 3; k++)
        {
            var vy = AY(0.87f) + k * MathF.Max(2f, U(2f));
            Rect(AX(0.1f), vy, AW(0.45f), 1, Color.Black, 0.5f);
            Rect(AX(0.1f), vy + 1, AW(0.45f), 1, Color.White, 0.12f);
        }
    }

    // A workbench from above: a pegboard of tools, the bench top with a vise and a cutting mat
    // under a work lamp, a bank of drawers.
    private void ConstructionBench(bool lit)
    {
        var accent = new Color(220, 170, 70);
        PortraitBody(accent, 173);
        var a = _area;

        var peg = (X: a.X, Y: a.Y, W: a.W, H: AH(0.3f));
        SteelBlock(peg.X, peg.Y, peg.W, peg.H, new Color(54, 58, 64));
        var hole = MathF.Max(3f, U(3.2f));
        for (var yy = peg.Y + hole / 2f; yy < peg.Y + peg.H - 1; yy += hole)
        for (var xx = peg.X + hole / 2f; xx < peg.X + peg.W - 1; xx += hole)
            Px(xx, yy, Color.Black, 0.45f);
        // Wrench, hammer, screwdriver, saw hanging on it.
        Line(AX(0.1f), AY(0.05f), AX(0.22f), AY(0.24f), new Color(176, 182, 192));
        Line(AX(0.11f), AY(0.05f), AX(0.23f), AY(0.24f), new Color(176, 182, 192));
        Ring(AX(0.1f), AY(0.04f), MathF.Max(1.5f, U(1.6f)), new Color(176, 182, 192), 0.9f, 0.8f);
        Rect(AX(0.36f), AY(0.07f), MathF.Max(1.5f, U(1.4f)), AH(0.19f), new Color(124, 86, 52));
        SteelBlock(AX(0.36f) - U(2.2f), AY(0.05f), U(5.8f), U(2.6f), new Color(110, 116, 128));
        Rect(AX(0.55f), AY(0.05f), MathF.Max(1f, U(0.9f)), AH(0.12f), new Color(186, 192, 202));
        Rect(AX(0.55f) - U(0.8f), AY(0.17f), U(2.6f), AH(0.08f), accent);
        Rect(AX(0.7f), AY(0.05f), AW(0.2f), AH(0.13f), new Color(150, 156, 166));
        for (var tx = AX(0.7f); tx < AX(0.9f); tx += 2)
            Px(tx, AY(0.18f), new Color(80, 84, 92));
        Rect(AX(0.86f), AY(0.05f), AW(0.06f), AH(0.07f), new Color(60, 44, 30));

        var bench = (X: a.X, Y: AY(0.33f), W: a.W, H: AH(0.4f));
        SteelBlock(bench.X, bench.Y, bench.W, bench.H, new Color(132, 138, 148));
        for (var i = 0; i < 14; i++)
            Rect(bench.X + 2, bench.Y + 2 + Hash(i, 173) * (bench.H - 4), bench.W - 4, 1, Color.Black, 0.05f);
        SteelBlock(AX(0.04f), AY(0.37f), AW(0.12f), AH(0.07f), new Color(80, 86, 96));
        SteelBlock(AX(0.04f), AY(0.46f), AW(0.12f), AH(0.07f), new Color(80, 86, 96));
        Line(AX(0.1f), AY(0.44f), AX(0.1f), AY(0.46f), new Color(30, 32, 36));
        Line(AX(0.02f), AY(0.5f), AX(0.2f), AY(0.5f), new Color(180, 186, 196), 0.8f);
        var mat = (X: AX(0.28f), Y: AY(0.38f), W: AW(0.6f), H: AH(0.3f));
        Rect(mat.X, mat.Y, mat.W, mat.H, new Color(44, 94, 70));
        for (var gx = mat.X + 3; gx < mat.X + mat.W; gx += MathF.Max(3f, U(3.5f)))
            Rect(gx, mat.Y, 1, mat.H, new Color(110, 170, 130), 0.35f);
        for (var gy = mat.Y + 3; gy < mat.Y + mat.H; gy += MathF.Max(3f, U(3.5f)))
            Rect(mat.X, gy, mat.W, 1, new Color(110, 170, 130), 0.35f);
        SteelBlock(mat.X + mat.W * 0.3f, mat.Y + mat.H * 0.35f, mat.W * 0.35f, mat.H * 0.2f, new Color(160, 166, 176));
        SteelBlock(mat.X + mat.W * 0.3f, mat.Y + mat.H * 0.35f, mat.W * 0.1f, mat.H * 0.45f, new Color(160, 166, 176));
        if (lit)
            Disc(AX(0.84f), AY(0.43f), mat.W * 0.18f, new Color(255, 226, 160), 0.12f);
        Line(AX(0.95f), AY(0.33f), AX(0.85f), AY(0.4f), new Color(60, 64, 72));
        Disc(AX(0.84f), AY(0.41f), MathF.Max(1.5f, U(2f)), lit ? new Color(255, 228, 170) : new Color(90, 86, 80));

        for (var i = 0; i < 3; i++)
        {
            var dy = AY(0.77f) + i * AH(0.075f);
            SteelBlock(AX(0.04f), dy, AW(0.92f), AH(0.07f), new Color(88, 94, 106));
            Rect(AX(0.4f), dy + AH(0.03f), AW(0.2f), MathF.Max(1f, U(1f)), new Color(190, 196, 206));
            Rect(AX(0.08f), dy + AH(0.02f), AW(0.12f), AH(0.03f), new Color(210, 206, 190), 0.5f);
        }
    }

    // An armoury bench: two rifles on the wall rack, a pistol stripped down on the bench under a
    // red work lamp, ammunition crates below.
    private void WeaponWorkbench(bool lit)
    {
        var accent = new Color(200, 70, 60);
        PortraitBody(accent, 179);
        var a = _area;

        var rack = (X: a.X, Y: a.Y, W: a.W, H: AH(0.28f));
        SteelBlock(rack.X, rack.Y, rack.W, rack.H, new Color(78, 84, 94));
        var slot = MathF.Max(3f, U(3.2f));
        for (var yy = rack.Y + slot / 2f; yy < rack.Y + rack.H - 1; yy += slot)
        for (var xx = rack.X + slot / 2f; xx < rack.X + rack.W - 1; xx += slot)
            Px(xx, yy, Color.Black, 0.35f);
        foreach (var (fy, scoped) in new[] { (0.3f, false), (0.72f, true) })
        {
            var ry = rack.Y + rack.H * fy;
            var receiver = new Color(34, 36, 42);
            Rect(AX(0.05f) + 1, ry - U(1.3f) + 1, AW(0.9f), U(3.6f), Color.Black, 0.3f);
            Rect(AX(0.05f), ry - U(1.3f), AW(0.24f), U(3.2f), new Color(116, 80, 50));
            Rect(AX(0.05f), ry - U(1.3f), AW(0.24f), 1, Color.White, 0.25f);
            Rect(AX(0.29f), ry - U(1.6f), AW(0.3f), U(3.6f), receiver);
            Rect(AX(0.29f), ry - U(1.6f), AW(0.3f), 1, Color.White, 0.3f);
            Rect(AX(0.59f), ry - U(1.1f), AW(0.16f), U(2.6f), new Color(52, 56, 62));
            Rect(AX(0.75f), ry - U(0.5f), AW(0.2f), MathF.Max(1.5f, U(1.2f)), new Color(84, 90, 100));
            Rect(AX(0.43f), ry + U(1.8f), U(2.4f), U(3.4f), receiver);
            if (scoped)
            {
                Rect(AX(0.33f), ry - U(3.4f), AW(0.22f), U(1.8f), new Color(22, 24, 28));
                Px(AX(0.33f), ry - U(2.5f), new Color(120, 200, 230), 0.8f);
            }
            foreach (var hx in new[] { 0.2f, 0.7f })
                Rect(AX(hx), ry - U(2.4f), MathF.Max(1f, U(1f)), U(1.2f), new Color(170, 176, 186));
        }

        var bench = (X: a.X, Y: AY(0.31f), W: a.W, H: AH(0.32f));
        SteelBlock(bench.X, bench.Y, bench.W, bench.H, new Color(126, 132, 142));
        Rect(AX(0.1f), AY(0.36f), AW(0.36f), AH(0.035f), new Color(46, 48, 54));
        Rect(AX(0.1f), AY(0.36f), AW(0.36f), 1, Color.White, 0.25f);
        Rect(AX(0.1f), AY(0.42f), AW(0.3f), AH(0.035f), new Color(56, 58, 64));
        Rect(AX(0.32f), AY(0.42f), AW(0.08f), AH(0.06f), new Color(56, 58, 64));
        for (var i = 0; i < 6; i++)
            Line(AX(0.55f + i * 0.04f), AY(0.37f), AX(0.57f + i * 0.04f), AY(0.4f), new Color(190, 196, 206), 0.8f);
        Rect(AX(0.55f), AY(0.43f), AW(0.08f), AH(0.06f), new Color(40, 42, 48));
        for (var i = 0; i < 4; i++)
            Rect(AX(0.7f) + i * U(2.2f), AY(0.44f), U(1.4f), U(3), new Color(200, 160, 70));
        Line(AX(0.1f), AY(0.55f), AX(0.8f), AY(0.55f), new Color(160, 166, 176), 0.8f);
        Disc(AX(0.86f), AY(0.34f), MathF.Max(1.5f, U(1.8f)), lit ? new Color(255, 110, 90) : new Color(90, 50, 46));

        for (var i = 0; i < 2; i++)
        {
            var cx = AX(0.03f + i * 0.5f);
            var cy = AY(0.67f + i * 0.04f);
            var cw = AW(0.44f);
            var ch = AH(0.27f - i * 0.04f);
            SteelBlock(cx, cy, cw, ch, new Color(84, 94, 60));
            Rect(cx, cy + ch * 0.22f, cw, 1, Color.Black, 0.4f);
            Rect(cx + cw * 0.15f, cy + ch * 0.45f, cw * 0.7f, MathF.Max(2f, ch * 0.12f), new Color(214, 184, 60), 0.85f);
            Bolt(cx + 2.5f, cy + 2.5f, MathF.Max(0.8f, U(0.8f)));
            Bolt(cx + cw - 3, cy + 2.5f, MathF.Max(0.8f, U(0.8f)));
        }
        for (var i = 0; i < 5; i++)
            Rect(AX(0.08f) + i * U(2.4f), AY(0.62f), U(1.5f), U(3.4f), new Color(214, 170, 80));
    }

    // A shuttle bay from above: plated deck inside a hazard border, a landing pad with its ring and
    // H, guide lights down both sides, the bay door at the far end and a control booth by it.
    private void ShuttleHangar(bool lit)
    {
        FloorBody(new Color(46, 50, 56));
        var a = _area;
        var seam = MathF.Max(10f, U(5f));
        for (var gx = a.X + seam; gx < a.X + a.W; gx += seam)
        {
            Rect(gx, a.Y, 1, a.H, Color.Black, 0.35f);
            Rect(gx + 1, a.Y, 1, a.H, Color.White, 0.05f);
        }
        for (var gy = a.Y + seam; gy < a.Y + a.H; gy += seam)
        {
            Rect(a.X, gy, a.W, 1, Color.Black, 0.35f);
            Rect(a.X, gy + 1, a.W, 1, Color.White, 0.05f);
        }

        var door = AH(0.1f);
        SteelBlock(a.X, a.Y, a.W, door, new Color(70, 76, 86));
        for (var sx = a.X + a.W / 6f; sx < a.X + a.W - 1; sx += a.W / 6f)
            Rect(sx, a.Y + 1, 1, door - 2, Color.Black, 0.45f);
        Rect(a.X + a.W / 2f, a.Y, 2, door, new Color(20, 22, 26));
        var tape = MathF.Max(3f, U(1.6f));
        HazardTape(a.X, a.Y + door, a.W, tape);
        HazardTape(a.X, a.Y + a.H - tape, a.W, tape);
        HazardTape(a.X, a.Y + door, tape, a.H - door);
        HazardTape(a.X + a.W - tape, a.Y + door, tape, a.H - door);

        var padX = AX(0.5f);
        var padY = AY(0.56f);
        var padR = MathF.Min(a.W, a.H) * 0.33f;
        var yellow = new Color(226, 184, 52);
        Ring(padX, padY, padR, yellow, 0.85f, MathF.Max(1.5f, U(1f)));
        Ring(padX, padY, padR * 0.82f, yellow, 0.35f, 1f);
        var hw = padR * 0.45f;
        var stroke = MathF.Max(2f, padR * 0.1f);
        Rect(padX - hw, padY - hw, stroke, hw * 2, yellow, 0.85f);
        Rect(padX + hw - stroke, padY - hw, stroke, hw * 2, yellow, 0.85f);
        Rect(padX - hw, padY - stroke / 2f, hw * 2, stroke, yellow, 0.85f);
        foreach (var ang in new[] { 0.785f, 2.356f, 3.927f, 5.498f })
            Bolt(padX + MathF.Cos(ang) * padR * 1.12f, padY + MathF.Sin(ang) * padR * 1.12f, MathF.Max(1.5f, U(0.8f)));

        for (var i = 0; i < 8; i++)
        {
            var ly = a.Y + door + tape + (a.H - door - tape * 2) * (i + 0.5f) / 8f;
            var on = lit && i % 2 == 0;
            foreach (var lx in new[] { a.X + tape + U(1.6f), a.X + a.W - tape - U(1.6f) })
            {
                if (on)
                    Disc(lx, ly, U(1.6f), new Color(120, 220, 255), 0.15f);
                Disc(lx, ly, MathF.Max(1f, U(0.6f)), on ? new Color(180, 240, 255) : new Color(42, 44, 48));
            }
        }

        var booth = (X: a.X + tape + U(3), Y: a.Y + a.H - tape - AH(0.12f) - 2, W: AW(0.22f), H: AH(0.12f));
        SteelBlock(booth.X, booth.Y, booth.W, booth.H, new Color(76, 82, 92));
        var scr = (X: booth.X + 3, Y: booth.Y + 3, W: booth.W - 6, H: booth.H * 0.45f);
        ConsoleScreen(scr, new Color(10, 24, 34), lit);
        ConsoleScreenFinish(scr, lit);
        Disc(booth.X + booth.W * 0.3f, booth.Y + booth.H * 0.78f, MathF.Max(1f, U(0.6f)), lit ? new Color(110, 230, 120) : new Color(42, 44, 48));
        Disc(booth.X + booth.W * 0.6f, booth.Y + booth.H * 0.78f, MathF.Max(1f, U(0.6f)), lit ? new Color(240, 186, 60) : new Color(42, 44, 48));
    }

    // A breaker box: a steel cabinet with a status lamp, three breakers (one tripped), cable glands
    // into the bottom.
    private void Junction(bool lit)
    {
        var accent = new Color(210, 200, 80);
        PortraitBody(accent, 233);
        var a = _area;
        SteelBlock(a.X, a.Y, a.W, a.H, new Color(74, 80, 90));
        Rect(a.X + 2, a.Y + 2, a.W - 4, a.H - 4, Color.Black, 0.12f);
        ConsoleLed(AX(0.82f), AY(0.1f), new Color(110, 230, 120), lit);
        Rect(AX(0.1f), AY(0.06f), AW(0.4f), MathF.Max(2f, AH(0.08f)), accent, 0.75f);
        for (var i = 0; i < 3; i++)
        {
            var by = AY(0.24f + i * 0.22f);
            var body = (X: AX(0.12f), Y: by, W: AW(0.76f), H: AH(0.16f));
            SteelBlock(body.X, body.Y, body.W, body.H, new Color(34, 36, 42));
            var tripped = i == 1;
            var lever = (X: body.X + body.W * 0.38f, W: body.W * 0.24f, H: body.H * 0.5f);
            var ly = tripped ? body.Y + body.H * 0.5f : body.Y;
            SteelBlock(lever.X, ly, lever.W, lever.H, tripped ? new Color(200, 70, 50) : new Color(180, 186, 196));
            ConsoleLed(body.X + body.W * 0.85f, body.Y + body.H / 2f, tripped ? new Color(240, 80, 60) : new Color(110, 230, 120), lit);
        }
        foreach (var gx in new[] { 0.3f, 0.7f })
        {
            Disc(AX(gx), AY(0.95f), MathF.Max(1.5f, AW(0.08f)), new Color(30, 32, 36));
            Disc(AX(gx), AY(0.95f), MathF.Max(0.8f, AW(0.04f)), new Color(110, 116, 126));
        }
    }
}
