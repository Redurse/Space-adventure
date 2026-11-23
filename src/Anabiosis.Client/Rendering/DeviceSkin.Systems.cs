using System;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Direct user request ("обнови текстуры для всех устройств по аналогии как мы делали уже со
// сделанными") - the ship's square 1x1 machines redrawn in the same Barotrauma / Space Haven
// language as the bridge consoles and the periscope: raised steel blocks lit from above, bolted
// bezels, small CRTs, knobs, buttons and LEDs from DeviceSkin.BridgeConsoles.cs, hazard tape where
// something pinches or bites. Still drawn on the 40-unit content square (U(n)) - these footprints
// are square - so the live overlays ShipRenderer.Devices.cs draws on top keep their exact places:
// the battery's charge column (28,17)-(33,35) and the rack's three shelves at y 12/20/28.
public sealed partial class DeviceSkin
{
    // Life support: two oxygen bottles strapped to the plate, a manifold with a red valve wheel
    // feeding them, a pressure gauge and the O2 readout.
    private void Oxygen(bool lit)
    {
        var accent = new Color(64, 162, 178);
        Housing(accent, 3);
        foreach (var tx in new[] { U(7), U(14.5f) })
        {
            Rect(tx + 0.6f, U(11) + 1, U(6), U(19), Color.Black, 0.3f);
            Rect(tx, U(11), U(6), U(19), new Color(196, 202, 210));
            Rect(tx, U(11), U(1.5f), U(19), Color.White, 0.3f);
            Rect(tx + U(4.5f), U(11), U(1.5f), U(19), Color.Black, 0.2f);
            Rect(tx, U(15), U(6), U(2), accent, 0.85f);
            Rect(tx + U(1.5f), U(9), U(3), U(2.2f), new Color(110, 116, 128));
            Rect(tx, U(24), U(6), MathF.Max(1f, U(0.8f)), new Color(60, 64, 72));
        }
        Rect(U(9), U(8), U(22), MathF.Max(1.5f, U(1.6f)), new Color(120, 126, 138));
        Rect(U(9), U(8), U(22), 1, Color.White, 0.3f);
        Rect(U(29.5f), U(8), MathF.Max(1.5f, U(1.6f)), U(12), new Color(120, 126, 138));
        Disc(U(30.3f), U(14), U(3.2f), new Color(190, 50, 40));
        Disc(U(30.3f), U(14), U(1.8f), new Color(36, 30, 30));
        for (var i = 0; i < 4; i++)
        {
            var ang = i * MathF.PI / 2f + 0.4f;
            Line(U(30.3f), U(14), U(30.3f) + MathF.Cos(ang) * U(3), U(14) + MathF.Sin(ang) * U(3), new Color(190, 50, 40));
        }
        Disc(U(30.3f), U(26), U(4.4f), new Color(26, 28, 32));
        Disc(U(30.3f), U(26), U(3.6f), new Color(226, 228, 220));
        for (var i = 0; i < 7; i++)
        {
            var ang = MathF.PI * (0.8f + i * 0.233f);
            Px(U(30.3f) + MathF.Cos(ang) * U(2.9f), U(26) + MathF.Sin(ang) * U(2.9f), new Color(40, 40, 44));
        }
        Line(U(30.3f), U(26), U(30.3f) + U(2.4f), U(26) - U(1.4f), new Color(200, 40, 30));
        var s = (X: U(7), Y: U(33), W: U(14), H: U(3.5f));
        ConsoleScreen(s, new Color(10, 34, 38), lit);
        if (lit)
            Rect(s.X + 1, s.Y + 1, s.W * 0.7f, s.H - 2, new Color(110, 230, 230), 0.8f);
        ConsoleScreenFinish(s, lit);
    }

    // Engine room machinery: a turbine intake behind a bolted ring, blades fanned round a hot hub,
    // exhaust stubs either side and hazard tape along the bottom.
    private void Engine(bool lit)
    {
        var accent = new Color(220, 120, 60);
        Housing(accent, 11);
        foreach (var ex in new[] { U(3), U(34) })
        {
            Rect(ex, U(14), U(3), U(13), new Color(70, 74, 84));
            Rect(ex, U(14), U(3), 1, Color.White, 0.3f);
            for (var k = 0; k < 3; k++)
                Rect(ex, U(16) + k * U(4), U(3), 1, Color.Black, 0.5f);
        }
        Disc(U(20) + 1, U(21) + 1.5f, U(12.5f), Color.Black, 0.35f);
        Disc(U(20), U(21), U(12.5f), new Color(62, 66, 76));
        Ring(U(20), U(21), U(12), new Color(150, 156, 168), 0.6f, 0.9f);
        for (var i = 0; i < 12; i++)
        {
            var ang = i * MathF.PI / 6f;
            Bolt(U(20) + MathF.Cos(ang) * U(11), U(21) + MathF.Sin(ang) * U(11), MathF.Max(0.8f, U(0.7f)));
        }
        Disc(U(20), U(21), U(9.5f), new Color(18, 18, 20));
        for (var i = 0; i < 9; i++)
        {
            var ang = i * MathF.PI * 2f / 9f;
            for (var t = 0f; t < 1f; t += 0.08f)
            {
                var r = U(2.5f) + t * U(6.5f);
                var a2 = ang + t * 0.9f;
                Disc(U(20) + MathF.Cos(a2) * r, U(21) + MathF.Sin(a2) * r, MathF.Max(0.7f, U(0.9f) * (1f - t * 0.4f)), new Color(130, 136, 148));
            }
        }
        Disc(U(20), U(21), U(2.6f), new Color(70, 74, 84));
        Disc(U(20), U(21), U(1.4f), lit ? new Color(255, 160, 80) : new Color(80, 60, 44));
        if (lit)
            Disc(U(20), U(21), U(4f), new Color(255, 140, 60), 0.18f);
        HazardTape(U(5), U(35), U(30), MathF.Max(2f, U(2.6f)));
    }

    // Shield generator: a stacked emitter coil round a crystal that glows blue when powered, four
    // capacitor posts at the corners, a field-strength readout.
    private void Shields(bool lit)
    {
        var accent = new Color(80, 130, 220);
        Housing(accent, 23);
        foreach (var (px, py) in new[] { (U(7.5f), U(12)), (U(32.5f), U(12)), (U(7.5f), U(29)), (U(32.5f), U(29)) })
        {
            Disc(px + 0.6f, py + 0.8f, U(2.6f), Color.Black, 0.35f);
            Disc(px, py, U(2.6f), new Color(84, 90, 102));
            Disc(px, py, U(1.4f), new Color(150, 156, 168));
            Disc(px, py, MathF.Max(0.7f, U(0.6f)), lit ? new Color(150, 200, 255) : new Color(50, 56, 66));
        }
        Disc(U(20), U(20.5f), U(10), new Color(36, 40, 50));
        for (var i = 0; i < 4; i++)
            Ring(U(20), U(20.5f), U(9 - i * 1.8f), i % 2 == 0 ? new Color(184, 120, 60) : new Color(150, 96, 50), 0.95f, MathF.Max(0.8f, U(0.7f)));
        if (lit)
        {
            Disc(U(20), U(20.5f), U(7), new Color(110, 170, 255), 0.18f);
            Ring(U(20), U(20.5f), U(9.8f), new Color(140, 200, 255), 0.35f, 0.8f);
        }
        Disc(U(20), U(20.5f), U(2.8f), lit ? new Color(150, 210, 255) : new Color(50, 64, 84));
        Disc(U(19.2f), U(19.6f), U(1), Color.White, lit ? 0.7f : 0.2f);
        var s = (X: U(11), Y: U(33), W: U(18), H: U(3.5f));
        ConsoleScreen(s, new Color(10, 20, 40), lit);
        if (lit)
            for (var i = 0; i < 6; i++)
                Rect(s.X + 1 + i * (s.W - 2) / 6f, s.Y + 1, MathF.Max(1f, (s.W - 2) / 6f - 1), s.H - 2, new Color(120, 180, 255), 0.85f);
        ConsoleScreenFinish(s, lit);
    }

    // Weapon charger: a bank of three big capacitors with charge bars, a hazard-taped front and a
    // heavy armoured cable into the deck.
    private void Weapons(bool lit)
    {
        var accent = new Color(200, 70, 60);
        Housing(accent, 31);
        for (var i = 0; i < 3; i++)
        {
            var cx = U(7) + i * U(9);
            Rect(cx + 0.6f, U(10) + 1, U(7), U(20), Color.Black, 0.35f);
            Rect(cx, U(10), U(7), U(20), new Color(54, 58, 66));
            Rect(cx, U(10), U(1.6f), U(20), Color.White, 0.18f);
            Rect(cx + U(5.4f), U(10), U(1.6f), U(20), Color.Black, 0.25f);
            Rect(cx + U(1), U(8.5f), U(5), U(1.8f), new Color(150, 156, 168));
            Rect(cx, U(12), U(7), MathF.Max(1f, U(0.8f)), accent, 0.8f);
            for (var k = 0; k < 5; k++)
            {
                var on = lit && k < 4 - i;
                Rect(cx + U(2.4f), U(27) - k * U(3), U(2.2f), MathF.Max(1f, U(2)), on ? new Color(255, 120, 80) : new Color(40, 34, 34));
            }
        }
        HazardTape(U(6), U(31.5f), U(28), MathF.Max(2f, U(2.4f)));
        Rect(U(18), U(34.5f), U(4), U(4.5f), new Color(28, 28, 32));
        Rect(U(18), U(34.5f), U(4), 1, Color.White, 0.2f);
    }

    // Auxiliary systems (lights, airlocks, scanner): a switch panel - four toggles with lamps, an
    // analogue gauge, a short status readout.
    private void Auxiliary(bool lit)
    {
        var accent = new Color(140, 148, 160);
        Housing(accent, 41);
        SteelBlock(U(6), U(10), U(17), U(19), new Color(66, 72, 82));
        for (var i = 0; i < 4; i++)
        {
            var sx = U(8.5f) + i % 2 * U(7.5f);
            var sy = U(12.5f) + i / 2 * U(8.5f);
            Rect(sx, sy, U(5), U(6), new Color(30, 32, 38));
            var up = i != 2;
            SteelBlock(sx + U(1.6f), up ? sy + U(0.5f) : sy + U(3), U(1.8f), U(2.6f), new Color(186, 192, 202));
            ConsoleLed(sx + U(2.5f), sy + U(7.3f), up ? new Color(110, 230, 120) : new Color(240, 186, 60), lit);
        }
        Disc(U(30.5f), U(17), U(6.2f), new Color(26, 28, 32));
        Disc(U(30.5f), U(17), U(5.2f), new Color(228, 230, 222));
        for (var i = 0; i < 9; i++)
        {
            var ang = MathF.PI * (0.75f + i * 0.1875f);
            Line(U(30.5f) + MathF.Cos(ang) * U(4.2f), U(17) + MathF.Sin(ang) * U(4.2f),
                U(30.5f) + MathF.Cos(ang) * U(4.9f), U(17) + MathF.Sin(ang) * U(4.9f), new Color(40, 40, 44));
        }
        Rect(U(30.5f) + U(2.2f), U(17) - U(3.6f), U(2.2f), MathF.Max(1f, U(0.7f)), new Color(200, 50, 40), 0.8f);
        Line(U(30.5f), U(17), U(30.5f) - U(3), U(17) - U(2.4f), new Color(30, 30, 34));
        Disc(U(30.5f), U(17), MathF.Max(0.8f, U(0.8f)), new Color(60, 60, 64));
        var s = (X: U(26), Y: U(26), W: U(9), H: U(3.5f));
        ConsoleScreen(s, new Color(14, 22, 20), lit);
        if (lit)
            Rect(s.X + 1, s.Y + 1, s.W * 0.55f, s.H - 2, new Color(140, 230, 160), 0.8f);
        ConsoleScreenFinish(s, lit);
    }

    // Power distribution: five slider channels each with a lamp, set at different levels, over a
    // copper busbar.
    private void Distribution(bool lit)
    {
        var accent = new Color(170, 120, 210);
        Housing(accent, 47);
        SteelBlock(U(5), U(10), U(30), U(22), new Color(58, 62, 72));
        var levels = new[] { 0.7f, 0.35f, 0.9f, 0.5f, 0.2f };
        for (var i = 0; i < 5; i++)
        {
            var cx = U(9) + i * U(5.5f);
            ConsoleLed(cx, U(12.5f), i == 4 ? new Color(240, 186, 60) : new Color(110, 230, 120), lit);
            Rect(cx - U(0.6f), U(15), U(1.2f), U(14), new Color(16, 18, 22));
            for (var k = 0; k < 5; k++)
                Rect(cx + U(1.4f), U(15) + k * U(3.4f), U(1.2f), 1, new Color(150, 156, 168), 0.6f);
            var ky = U(15) + U(14) * (1f - levels[i]) - U(1.2f);
            SteelBlock(cx - U(2), ky, U(4), U(2.4f), new Color(176, 182, 194));
            Rect(cx - U(2), ky + U(1.1f), U(4), 1, accent, 0.9f);
        }
        Rect(U(6), U(33.5f), U(28), U(2.4f), new Color(184, 120, 60));
        Rect(U(6), U(33.5f), U(28), 1, new Color(240, 180, 120), 0.6f);
        for (var i = 0; i < 6; i++)
            Bolt(U(8) + i * U(4.8f), U(34.7f), MathF.Max(0.7f, U(0.6f)));
    }

    // Battery bank: two big cells with their terminals and a lightning-bolt plate on the left; on the
    // right the bezelled slot the live charge column (ShipRenderer.DrawBatteryBlock) shows in.
    private void Battery(bool lit)
    {
        var accent = new Color(110, 200, 120);
        Housing(accent, 53);
        foreach (var cy in new[] { U(10), U(23) })
        {
            Rect(U(5) + 0.6f, cy + 1, U(19), U(11), Color.Black, 0.35f);
            SteelBlock(U(5), cy, U(19), U(11), new Color(46, 52, 48));
            Rect(U(5), cy + U(3.5f), U(19), U(2.2f), accent, 0.75f);
            foreach (var (tx, positive) in new[] { (U(7.5f), true), (U(19), false) })
            {
                Rect(tx, cy - U(1.2f), U(2.6f), U(1.6f), new Color(180, 186, 196));
                Rect(tx + U(0.6f), cy + U(7.5f), U(1.4f), MathF.Max(1f, U(0.6f)), positive ? new Color(220, 60, 50) : new Color(40, 40, 44));
                if (positive)
                    Rect(tx + U(1), cy + U(7), MathF.Max(1f, U(0.6f)), U(1.6f), new Color(220, 60, 50));
            }
        }
        Line(U(14), U(13.8f), U(12.5f), U(16.2f), new Color(250, 220, 90), 0.9f);
        Line(U(12.5f), U(16.2f), U(14.5f), U(16.2f), new Color(250, 220, 90), 0.9f);
        Line(U(14.5f), U(16.2f), U(13), U(18.6f), new Color(250, 220, 90), 0.9f);

        // The charge column's bezel - ShipRenderer fills (28,17)-(33,35) with segments.
        Rect(U(26.5f), U(15.5f), U(8), U(21), new Color(36, 40, 48));
        Rect(U(26.5f), U(15.5f), U(8), 1, new Color(176, 184, 196), 0.5f);
        Rect(U(27.5f), U(16.5f), U(6), U(19), new Color(10, 12, 14));
        Bolt(U(27), U(15.8f), MathF.Max(0.7f, U(0.6f)));
        Bolt(U(34), U(15.8f), MathF.Max(0.7f, U(0.6f)));
        ConsoleLed(U(30.5f), U(11.5f), new Color(110, 230, 120), lit);
    }

    // Cargo shelving: steel uprights, three shelf boards carrying crates (at the rows the live "how
    // full" readout in ShipRenderer.DrawStorageRack dims: y 12, 20, 28, 7 units tall each).
    private void Rack(bool lit)
    {
        var accent = new Color(180, 130, 80);
        Housing(accent, 59);
        Rect(U(6), U(11), U(28), U(26), new Color(30, 32, 36));
        foreach (var ux in new[] { U(5.5f), U(33.5f) })
        {
            SteelBlock(ux - U(1), U(10), U(2), U(27), new Color(110, 116, 128));
            for (var k = 0; k < 6; k++)
                Px(ux, U(12) + k * U(4), Color.Black, 0.5f);
        }
        for (var shelf = 0; shelf < 3; shelf++)
        {
            var top = U(12 + shelf * 8);
            for (var c = 0; c < 3; c++)
            {
                var cw = U(7 + Hash(c, shelf + 59) * 1.5f);
                var cx = U(7.5f) + c * U(8.6f);
                var ch = U(5 + Hash(c + 3, shelf + 59) * 2);
                var cy = top + U(7) - ch;
                var crate = (shelf + c) % 3 == 0 ? new Color(120, 90, 56) : (shelf + c) % 3 == 1 ? new Color(84, 96, 70) : new Color(96, 102, 114);
                SteelBlock(cx, cy, cw, ch, crate);
                Line(cx + 1, cy + 1, cx + cw - 2, cy + ch - 2, Color.Black, 0.2f);
                Rect(cx + cw * 0.3f, cy + ch * 0.4f, cw * 0.4f, MathF.Max(1f, U(0.8f)), new Color(220, 200, 150), 0.6f);
            }
            SteelBlock(U(6), top + U(7), U(28), MathF.Max(1.5f, U(1.2f)), new Color(130, 136, 148));
        }
    }

    // Suit locker: a tall steel cabinet with two doors, a window in the left one with the helmet
    // behind it (only while a suit is inside - `lit`), vent slits, a handle. The live status light
    // ShipRenderer draws sits on the painted band above.
    private void Locker(bool lit)
    {
        var accent = new Color(120, 160, 190);
        Housing(accent, 67);
        SteelBlock(U(6), U(9.5f), U(28), U(28), new Color(84, 92, 104));
        Rect(U(20), U(9.5f), MathF.Max(1f, U(0.8f)), U(28), new Color(28, 30, 36));
        Rect(U(9), U(12.5f), U(8), U(11), new Color(20, 24, 28));
        Rect(U(9), U(12.5f), U(8), 1, new Color(170, 178, 190), 0.4f);
        if (lit)
        {
            Disc(U(13), U(17.5f), U(3.2f), new Color(206, 210, 216));
            Disc(U(13), U(17.8f), U(2.1f), new Color(80, 150, 190));
            Disc(U(12.3f), U(17), MathF.Max(0.7f, U(0.6f)), Color.White, 0.7f);
            Rect(U(10), U(21), U(6), U(2.5f), new Color(206, 210, 216));
        }
        Line(U(9.5f), U(13), U(12), U(22.5f), Color.White, 0.1f);
        for (var k = 0; k < 4; k++)
        {
            Rect(U(23), U(13) + k * U(2.6f), U(8), MathF.Max(1f, U(1)), Color.Black, 0.5f);
            Rect(U(23), U(13) + k * U(2.6f) + MathF.Max(1f, U(1)), U(8), 1, Color.White, 0.12f);
        }
        SteelBlock(U(17.5f), U(25), U(1.6f), U(6), new Color(186, 192, 202));
        SteelBlock(U(21.5f), U(25), U(1.6f), U(6), new Color(186, 192, 202));
        Rect(U(9), U(31), U(8), U(3), accent, 0.7f);
        Rect(U(23), U(31), U(8), U(3), accent, 0.7f);
    }
}
