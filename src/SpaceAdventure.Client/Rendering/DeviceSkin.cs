using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

/// <summary>The painted face of a ship device, baked once per kind and size.</summary>
///
/// Every machine on the deck used to be the same chamfered box tinted a different saturated hue and
/// told apart by a single Cyrillic letter. Hardware does not work that way. Steel is grey; the
/// colour lives in painted markings and in whatever is lit. So identity here comes from three
/// places instead of one:
///
///   * a painted band in the machine's own colour, chipped, because paint on a working ship does
///     not survive - and the chips are what make it read as paint rather than as a coloured
///     rectangle;
///   * one piece of characteristic hardware per machine. The reactor already had its levers and
///     turbine from the concept pass and every other device had nothing, which is most of why they
///     all looked alike;
///   * brushed metal with a grain direction. Noise scattered both ways reads as dirt; noise running
///     along one axis reads as a machined surface, and that difference is most of the material.
///
/// Baked rather than drawn live: a 40x40 face is 1600 pixels of per-pixel work, and there are a
/// dozen devices on screen at sixty frames a second. What stays live is only what actually changes
/// - screens, charge levels, turbine spin - and the callers draw those on top.
public sealed class DeviceSkin : IDisposable
{
    public enum Face
    {
        Generic, Oxygen, Engine, Shields, Weapons, Auxiliary, Distribution, Battery, Rack,
        Navigation, Helm, Locker, Jukebox,
    }

    private readonly GraphicsDevice _graphics;
    private readonly Dictionary<(Face, int, bool), Texture2D> _cache = new();

    public DeviceSkin(GraphicsDevice graphics) => _graphics = graphics;

    /// <param name="size">Side of the square the device occupies, in design pixels.</param>
    /// <param name="lit">Whether the ship can power it. Baked in rather than drawn over, because an
    /// unpowered machine differs from a powered one across the whole face - dead indicators, dark
    /// glass - and two bakes cost less than compositing that every frame.</param>
    public Texture2D Get(Face face, int size, bool lit)
    {
        size = Math.Max(8, size);
        if (_cache.TryGetValue((face, size, lit), out var cached))
            return cached;
        var baked = Bake(face, size, lit);
        _cache[(face, size, lit)] = baked;
        return baked;
    }

    public void Dispose()
    {
        foreach (var texture in _cache.Values)
            texture.Dispose();
        _cache.Clear();
    }

    // ---------------------------------------------------------------- the canvas

    private int _size;
    private int _chamfer;
    private float _u;               // scale from the 40px reference the faces were designed at
    private Color[] _buffer = Array.Empty<Color>();

    private static readonly Color Steel = new(99, 105, 116);

    private static float Hash(int a, int b = 0)
    {
        var n = unchecked(a * 374761393 + b * 668265263);
        n = unchecked((n ^ (n >> 13)) * 1274126177);
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    private bool Inside(int x, int y)
    {
        var s = _size - 1;
        return !(x + y < _chamfer || (s - x) + y < _chamfer
                 || x + (s - y) < _chamfer || (s - x) + (s - y) < _chamfer);
    }

    private void Px(float fx, float fy, Color c, float a = 1f)
    {
        int x = (int)MathF.Round(fx), y = (int)MathF.Round(fy);
        if (a <= 0f || x < 0 || y < 0 || x >= _size || y >= _size || !Inside(x, y))
            return;
        var d = _buffer[y * _size + x];
        // Only ever composited over an already-opaque plate, so the result stays opaque and there is
        // no premultiplied-alpha trap on the way into the texture.
        _buffer[y * _size + x] = new Color(
            (int)(d.R + (c.R - d.R) * a), (int)(d.G + (c.G - d.G) * a), (int)(d.B + (c.B - d.B) * a), 255);
    }

    private void Rect(float x, float y, float w, float h, Color c, float a = 1f)
    {
        for (var yy = (int)MathF.Round(y); yy < (int)MathF.Round(y + h); yy++)
        for (var xx = (int)MathF.Round(x); xx < (int)MathF.Round(x + w); xx++)
            Px(xx, yy, c, a);
    }

    private void Disc(float cx, float cy, float r, Color c, float a = 1f)
    {
        for (var yy = (int)(cy - r) - 1; yy <= (int)(cy + r) + 1; yy++)
        for (var xx = (int)(cx - r) - 1; xx <= (int)(cx + r) + 1; xx++)
        {
            var d = MathF.Sqrt((xx - cx) * (xx - cx) + (yy - cy) * (yy - cy));
            if (d <= r)
                Px(xx, yy, c, a * MathF.Min(1f, r - d + 0.5f));
        }
    }

    private void Ring(float cx, float cy, float r, Color c, float a = 1f, float w = 1f)
    {
        for (var yy = (int)(cy - r) - 2; yy <= (int)(cy + r) + 2; yy++)
        for (var xx = (int)(cx - r) - 2; xx <= (int)(cx + r) + 2; xx++)
        {
            var d = MathF.Abs(MathF.Sqrt((xx - cx) * (xx - cx) + (yy - cy) * (yy - cy)) - r);
            if (d <= w)
                Px(xx, yy, c, a * (1f - d / (w + 0.4f)));
        }
    }

    private void Line(float x0, float y0, float x1, float y1, Color c, float a = 1f)
    {
        var n = (int)(MathF.Max(MathF.Abs(x1 - x0), MathF.Abs(y1 - y0)) * 2f) + 1;
        for (var i = 0; i <= n; i++)
        {
            var t = i / (float)n;
            Px(x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, c, a);
        }
    }

    // A lit display inset into the face: recess, glass, scanlines, one highlight where the room
    // light would catch it. Deliberately bright - ScenePost credits a lit pixel before testing it
    // against the bloom threshold, so clearing that threshold is what reads as "powered" rather
    // than as "painted a lighter colour".
    private void Glass(float x, float y, float w, float h, Color glow, bool lit)
    {
        if (w < 4f || h < 2f)
            return;
        Rect(x - 1, y - 1, w + 2, h + 2, new Color(14, 16, 20));
        Rect(x, y, w, h, lit
            ? new Color(Math.Min(255, (int)(glow.R * 0.55f + 40)), Math.Min(255, (int)(glow.G * 0.55f + 40)),
                Math.Min(255, (int)(glow.B * 0.55f + 40)))
            : new Color(glow.R / 9, glow.G / 9, glow.B / 9));
        if (!lit)
            return;
        for (var yy = y; yy < y + h; yy += 2)
            Rect(x, yy, w, 1, Color.Black, 0.26f);
        Rect(x + 1, y + 1, MathF.Max(2f, w / 3f), 1, Color.White, 0.42f);
    }

    // ---------------------------------------------------------------- the plate

    private void Housing(Color accent, int seed)
    {
        for (var y = 0; y < _size; y++)
        for (var x = 0; x < _size; x++)
        {
            if (!Inside(x, y))
                continue;
            var v = (Hash(x * 13 + seed) - 0.5f) * 16f + (Hash(x + seed, y / 7) - 0.5f) * 7f;
            var g = 13f * (1f - y / (_size * 0.5f));      // lit from above, like the rest of the deck
            _buffer[y * _size + x] = new Color(
                (int)MathHelper.Clamp(Steel.R + v + g, 0, 255),
                (int)MathHelper.Clamp(Steel.G + v + g, 0, 255),
                (int)MathHelper.Clamp(Steel.B + v + g, 0, 255), 255);
        }

        // Bevel. Two lines, and the plate stops reading as a sticker.
        for (var x = _chamfer; x < _size - _chamfer; x++)
        {
            Px(x, 0, new Color(235, 240, 250), 0.45f);
            Px(x, _size - 1, Color.Black, 0.55f);
        }
        for (var y = _chamfer; y < _size - _chamfer; y++)
        {
            Px(0, y, new Color(215, 225, 240), 0.28f);
            Px(_size - 1, y, Color.Black, 0.42f);
        }
        for (var i = 0; i < _chamfer; i++)
        {
            Px(i, _chamfer - i, new Color(220, 230, 245), 0.35f);
            Px(_size - 1 - i, _chamfer - i, new Color(200, 210, 230), 0.25f);
            Px(i, _size - 1 - (_chamfer - i), Color.Black, 0.40f);
            Px(_size - 1 - i, _size - 1 - (_chamfer - i), Color.Black, 0.50f);
        }

        // The painted band. Wear is rolled per two-pixel row rather than per row: at finer grain the
        // gaps lined up into columns and the band read as unreadable lettering, which is worse than
        // a clean stripe because the eye stops and tries to read it.
        var bandTop = MathF.Round(3f * _u);
        var bandBottom = MathF.Round(7f * _u);
        for (var x = _chamfer; x < _size - _chamfer; x++)
        {
            for (var y = bandTop; y < bandBottom; y++)
            {
                var wear = Hash((int)(x * 31 + seed * 7), (int)(y / 2));
                if (wear > 0.93f)
                    continue;
                Px(x, y, accent, 0.86f - wear * 0.16f);
            }
            Px(x, bandBottom, Color.Black, 0.35f);
        }

        // Bolts, and grime pooling low where nobody wipes.
        var bolt = MathF.Max(1.2f, 1.6f * _u);
        foreach (var (bx, by) in new[]
                 {
                     (_chamfer - 1, _chamfer + 1), (_size - _chamfer, _chamfer + 1),
                     (_chamfer - 1, _size - _chamfer - 1), (_size - _chamfer, _size - _chamfer - 1),
                 })
        {
            Disc(bx, by, bolt, new Color(58, 62, 70));
            Px(bx, by - 1, new Color(200, 208, 220), 0.55f);
        }
        for (var i = 0; i < 26; i++)
        {
            var gx = Hash(seed * 3 + i, 1) * _size;
            var gy = _size - 1 - Hash(seed * 3 + i, 2) * Hash(seed * 3 + i, 2) * (_size * 0.45f);
            Disc(gx, gy, (1f + Hash(i, seed) * 1.6f) * _u, new Color(30, 28, 26), 0.16f);
        }
    }

    // ---------------------------------------------------------------- the faces

    private Texture2D Bake(Face face, int size, bool lit)
    {
        _size = size;
        _chamfer = Math.Max(2, size / 6);
        _u = size / 40f;
        _buffer = new Color[size * size];

        switch (face)
        {
            case Face.Oxygen: Oxygen(lit); break;
            case Face.Engine: Engine(lit); break;
            case Face.Shields: Shields(lit); break;
            case Face.Weapons: Weapons(lit); break;
            case Face.Auxiliary: Auxiliary(lit); break;
            case Face.Distribution: Distribution(lit); break;
            case Face.Battery: Battery(lit); break;
            case Face.Rack: Rack(lit); break;
            case Face.Navigation: Navigation(lit); break;
            case Face.Helm: Helm(lit); break;
            case Face.Locker: Locker(lit); break;
            case Face.Jukebox: Jukebox(lit); break;
            default: Housing(new Color(140, 148, 160), 97); break;
        }

        var texture = new Texture2D(_graphics, size, size);
        texture.SetData(_buffer);
        _buffer = Array.Empty<Color>();
        return texture;
    }

    private float U(float atForty) => atForty * _u;

    private void Oxygen(bool lit)
    {
        var accent = new Color(64, 162, 178);
        Housing(accent, 3);
        // Dials. The most legible "life support" shape there is, and it survives at sizes where
        // printed text turns to mush.
        foreach (var gx in new[] { U(13), U(27) })
        {
            Disc(gx, U(17), U(5.4f), new Color(22, 26, 30));
            Disc(gx, U(17), U(4.4f), new Color(208, 214, 205));
            Ring(gx, U(17), U(4.4f), new Color(60, 66, 70), 0.5f);
            Line(gx, U(17), gx + U(3), U(14), new Color(170, 40, 36));
            Disc(gx, U(17), MathF.Max(0.8f, U(1f)), new Color(40, 44, 48));
            Px(gx - U(2), U(15), Color.White, 0.5f);
        }
        // Pipework running off the plate: hardware that continues past the housing says the machine
        // is plumbed into the ship rather than parked on it.
        Rect(U(10), U(27), U(20), MathF.Max(2f, U(4)), new Color(128, 134, 142));
        Rect(U(10), U(27), U(20), 1, new Color(215, 225, 235), 0.5f);
        Rect(U(10), U(30), U(20), 1, Color.Black, 0.5f);
        foreach (var fx in new[] { U(14), U(26) })
            Rect(fx, U(26), MathF.Max(1f, U(2)), U(6), new Color(86, 92, 100));
        Rect(U(28), U(31), MathF.Max(2f, U(4)), U(6), new Color(128, 134, 142));
        Disc(U(30), U(34), U(2.2f), new Color(150, 158, 168));
        Glass(U(9), U(33), U(14), MathF.Max(2f, U(4)), accent, lit);
    }

    private void Engine(bool lit)
    {
        var accent = new Color(204, 112, 46);
        Housing(accent, 11);
        // Intake: blades over a dark throat, and the scorch a hot machine leaves on its own face.
        // The stain is what dates the hardware.
        Disc(U(20), U(23), U(11), new Color(34, 30, 28));
        Disc(U(20), U(23), U(9.6f), new Color(70, 66, 64));
        for (var i = 0; i < 7; i++)
        {
            var ang = i * MathF.PI * 2f / 7f;
            for (var t = 0; t < 18; t++)
            {
                var f = t / 18f;
                var r = U(2.2f) + f * U(7.4f);
                var sw = ang + f * 0.85f;
                Px(U(20) + MathF.Cos(sw) * r, U(23) + MathF.Sin(sw) * r,
                    new Color(150, 156, 166), 0.85f - f * 0.3f);
            }
        }
        Disc(U(20), U(23), U(2.6f), new Color(44, 46, 52));
        Disc(U(20), U(23), U(1.4f), lit ? new Color(250, 176, 96) : new Color(70, 62, 56), 0.9f);
        Ring(U(20), U(23), U(10.4f), new Color(24, 22, 22), 0.8f, 1.2f);
        for (var i = 0; i < 30; i++)
            Px(Hash(i, 5) * _size, U(9) + Hash(i, 6) * U(5), new Color(58, 40, 30), 0.3f);
        // Hazard banding along the bottom, which is where the hot end is.
        var step = MathF.Max(2f, U(3));
        for (var x = U(9); x < U(31); x++)
            Rect(x, U(36), 1, MathF.Max(1f, U(2)),
                (int)(x / step) % 2 == 0 ? new Color(232, 176, 60) : new Color(30, 30, 34));
    }

    private void Shields(bool lit)
    {
        var accent = new Color(84, 134, 220);
        Housing(accent, 23);
        // Emitter: arcs around a hot node, so the thing that projects a field looks like it projects
        // something.
        foreach (var (r, a) in new[] { (10f, 0.30f), (7.5f, 0.45f), (5f, 0.65f) })
            Ring(U(20), U(26), U(r), new Color(120, 180, 255), lit ? a : a * 0.25f, 1.1f);
        Disc(U(20), U(26), U(3), new Color(30, 44, 70));
        Disc(U(20), U(26), U(2), lit ? new Color(170, 215, 255) : new Color(60, 74, 96), 0.95f);
        // Ceramic insulators: the stacked white discs that say high voltage without a warning label.
        foreach (var ix in new[] { U(9), U(31) })
        {
            for (var k = 0; k < 3; k++)
            {
                Rect(ix - U(3), U(22) + k * U(3), U(6), MathF.Max(1f, U(2)), new Color(216, 212, 200));
                Rect(ix - U(3), U(23) + k * U(3), U(6), 1, new Color(120, 118, 110), 0.6f);
            }
            Rect(ix - 1, U(31), MathF.Max(1f, U(2)), U(4), new Color(96, 102, 110));
        }
        Glass(U(11), U(11), U(18), MathF.Max(2f, U(5)), accent, lit);
    }

    private void Weapons(bool lit)
    {
        var accent = new Color(206, 80, 64);
        Housing(accent, 101);
        // A capacitor bank: two ribbed cylinders with a spark gap between them. What a weapon
        // charger does is store a great deal of energy and then let go of it at once, and that is
        // the one thing this shape says at a glance.
        foreach (var cx in new[] { U(12), U(28) })
        {
            Rect(cx - U(4), U(14), U(8), U(18), new Color(64, 68, 78));
            Rect(cx - U(4), U(14), U(8), 1, new Color(170, 178, 190), 0.5f);
            Rect(cx + U(3), U(14), 1, U(18), Color.Black, 0.45f);
            for (var k = 0; k < 4; k++)
                Rect(cx - U(4), U(17) + k * U(4), U(8), 1, Color.Black, 0.35f);
            Disc(cx, U(13), U(2.2f), new Color(150, 156, 166));
        }
        // The gap itself. Dead metal when the ship cannot power it - an arc drawn on an unpowered
        // machine is the sort of detail that quietly tells the player the wrong thing.
        if (lit)
        {
            Line(U(14), U(12), U(19), U(10), new Color(200, 230, 255), 0.85f);
            Line(U(19), U(10), U(26), U(12), new Color(200, 230, 255), 0.85f);
            Disc(U(20), U(10), U(1.6f), Color.White, 0.9f);
        }
        Glass(U(9), U(34), U(22), MathF.Max(2f, U(4)), accent, lit);
    }

    private void Auxiliary(bool lit)
    {
        var accent = new Color(150, 162, 178);
        Housing(accent, 107);
        // Relay bank and a round meter. Deliberately the plainest face of the set: this is the
        // system with no single job, and dressing it up as something specific would be a lie the
        // player has to unlearn later.
        for (var k = 0; k < 4; k++)
        {
            var rx = U(8) + k % 2 * U(9);
            var ry = U(13) + k / 2 * U(9);
            Rect(rx, ry, U(7), U(7), new Color(52, 56, 64));
            Rect(rx, ry, U(7), 1, new Color(150, 158, 170), 0.45f);
            Rect(rx + U(2), ry + U(2), U(3), U(3), new Color(90, 96, 106));
            Disc(rx + U(3.5f), ry + U(5.5f), MathF.Max(0.8f, U(1f)),
                lit && k != 1 ? new Color(120, 230, 150) : new Color(50, 54, 60), 0.9f);
        }
        Disc(U(29), U(19), U(6.4f), new Color(22, 26, 30));
        Disc(U(29), U(19), U(5.4f), new Color(198, 204, 196));
        Line(U(29), U(19), U(26), U(15), new Color(60, 66, 74));
        Disc(U(29), U(19), MathF.Max(0.8f, U(1f)), new Color(40, 44, 48));
        // Cable gland: the conduit has to go somewhere.
        Rect(U(12), U(31), U(16), MathF.Max(2f, U(4)), new Color(58, 62, 70));
        Rect(U(12), U(31), U(16), 1, new Color(150, 158, 170), 0.4f);
        Glass(U(10), U(36), U(14), MathF.Max(2f, U(3)), accent, lit);
    }

    private void Distribution(bool lit)
    {
        var accent = new Color(154, 116, 206);
        Housing(accent, 31);
        // Copper bus bars behind five breakers - one per system, which is exactly what this panel
        // hands out, so the face states the machine's job before anybody opens it.
        foreach (var bx in new[] { U(11), U(20), U(29) })
        {
            Rect(bx - 1, U(14), MathF.Max(1f, U(2)), U(20), new Color(150, 96, 52));
            Rect(bx - 1, U(14), 1, U(20), new Color(206, 146, 88), 0.7f);
        }
        for (var i = 0; i < 5; i++)
        {
            var x = U(8) + i * U(6);
            Rect(x, U(18), MathF.Max(2f, U(4)), U(9), new Color(44, 48, 56));
            Rect(x, U(18), MathF.Max(2f, U(4)), 1, new Color(150, 158, 170), 0.5f);
            var up = i % 2 == 0;
            Rect(x + U(1), up ? U(19) : U(23), MathF.Max(1f, U(2)), U(4), new Color(196, 202, 212));
            Disc(x + U(2), U(15), MathF.Max(1f, U(1.4f)),
                lit ? up ? new Color(110, 240, 140) : new Color(200, 70, 60) : new Color(52, 56, 62), 0.95f);
        }
        Glass(U(8), U(30), U(24), MathF.Max(2f, U(5)), accent, lit);
    }

    private void Battery(bool lit)
    {
        var accent = new Color(96, 186, 116);
        Housing(accent, 41);
        // Terminal posts, capped red and black. Nothing else on the deck looks like this.
        foreach (var (tx, cap) in new[] { (U(13), new Color(188, 62, 54)), (U(27), new Color(36, 38, 44)) })
        {
            Rect(tx - U(3), U(12), U(6), MathF.Max(2f, U(3)), new Color(140, 146, 156));
            Disc(tx, U(12), U(2.6f), cap);
            Px(tx - 1, U(11), Color.White, 0.4f);
        }
        // Three cell modules. The live charge column is drawn by the caller on top of the recess
        // this leaves for it - the level changes every second and has no business being baked.
        for (var k = 0; k < 3; k++)
        {
            var y = U(18) + k * U(6);
            Rect(U(8), y, U(18), MathF.Max(2f, U(5)), new Color(58, 62, 70));
            Rect(U(8), y, U(18), 1, new Color(140, 148, 160), 0.45f);
            Rect(U(9), y + 1, U(16), MathF.Max(1f, U(3)), new Color(44, 48, 54));
        }
        Rect(U(28), U(17), MathF.Max(3f, U(5)), U(18), new Color(20, 22, 26));
        Glass(U(8), U(36), U(12), MathF.Max(2f, U(3)), accent, lit);
    }

    private void Rack(bool lit)
    {
        var accent = new Color(186, 146, 92);
        Housing(accent, 53);
        // Open shelving with crate ends: the contents are the texture, and that is what makes
        // storage read as storage rather than as a cupboard.
        for (var k = 0; k < 3; k++)
        {
            var y = U(12) + k * U(8);
            Rect(U(7), y, U(26), MathF.Max(3f, U(7)), new Color(34, 32, 30));
            foreach (var (cx, cw, cc) in new[]
                     {
                         (3f, 6f, new Color(146, 118, 78)), (10f, 5f, new Color(110, 116, 124)),
                         (16f, 8f, new Color(92, 84, 70)),
                     })
            {
                if (Hash((int)(k * 9 + cx), 3) < 0.22f)
                    continue;
                Rect(U(7) + U(cx), y + 1, U(cw), MathF.Max(2f, U(5)), cc);
                Rect(U(7) + U(cx), y + 1, U(cw), 1, Color.White, 0.18f);
                Rect(U(7) + U(cx), y + U(5), U(cw), 1, Color.Black, 0.35f);
            }
            Rect(U(6), y + U(7), U(28), MathF.Max(1f, U(2)), new Color(128, 134, 142));
            Rect(U(6), y + U(7), U(28), 1, new Color(206, 214, 224), 0.5f);
        }
        // A manifest card. Dropped below a certain size: at that point it is three grey pixels and
        // reads as damage rather than as paper.
        if (_size < 26)
            return;
        Rect(U(27), U(30), U(8), U(6), new Color(222, 216, 196));
        for (var k = 0; k < 3; k++)
            Rect(U(28), U(31) + k * U(2), U(6), 1, new Color(70, 74, 80), 0.6f);
    }

    private void Navigation(bool lit)
    {
        var accent = new Color(72, 196, 208);
        Housing(accent, 61);
        // A round scope rather than another rectangle. The silhouette alone separates this console
        // from the helm across the room, which is the first thing the eye sorts objects by.
        Disc(U(20), U(20), U(10), new Color(12, 20, 22));
        Disc(U(20), U(20), U(9), lit ? new Color(18, 52, 56) : new Color(14, 24, 26));
        if (lit)
        {
            foreach (var r in new[] { 3f, 6f, 9f })
                Ring(U(20), U(20), U(r), new Color(60, 200, 210), 0.30f);
            for (var i = 0; i < 5; i++)
            {
                var bx = U(20) + (Hash(i, 7) - 0.5f) * U(15);
                var by = U(20) + (Hash(i, 8) - 0.5f) * U(15);
                if ((bx - U(20)) * (bx - U(20)) + (by - U(20)) * (by - U(20)) < U(9) * U(9) * 0.8f)
                    Disc(bx, by, MathF.Max(0.9f, U(1.1f)), new Color(170, 255, 200), 0.9f);
            }
        }
        Ring(U(20), U(20), U(10), new Color(24, 28, 34), 0.9f, 1.3f);
        for (var i = 0; i < 6; i++)
        {
            var kx = U(9) + i % 3 * U(8);
            var ky = U(32) + i / 3 * U(4);
            Rect(kx, ky, MathF.Max(3f, U(6)), MathF.Max(2f, U(3)), new Color(52, 56, 64));
            Rect(kx, ky, MathF.Max(3f, U(6)), 1, new Color(140, 148, 160), 0.4f);
        }
    }

    private void Helm(bool lit)
    {
        var accent = new Color(228, 172, 74);
        Housing(accent, 71);
        // An artificial horizon, tilted: the one instrument that says "this thing flies the ship".
        Rect(U(8), U(11), U(20), U(12), new Color(12, 14, 18));
        for (var y = U(12); y < U(23); y++)
        for (var x = U(9); x < U(28); x++)
        {
            var over = (y - U(17)) * 3f - (x - U(18));
            var sky = lit ? new Color(54, 112, 158) : new Color(26, 40, 52);
            var ground = lit ? new Color(104, 78, 48) : new Color(44, 36, 28);
            Px(x, y, over < 0 ? sky : ground);
        }
        Line(U(9), U(19), U(27), U(13), lit ? new Color(240, 226, 170) : new Color(96, 92, 78), 0.9f);
        Rect(U(17), U(16), MathF.Max(2f, U(3)), 1, lit ? new Color(255, 240, 190) : new Color(90, 86, 74));
        Rect(U(8), U(11), U(20), 1, Color.Black, 0.5f);
        // Throttle: a lever with a knob, in a slotted track.
        Rect(U(31), U(12), MathF.Max(2f, U(3)), U(20), new Color(26, 28, 34));
        Rect(U(30), U(19), MathF.Max(3f, U(5)), MathF.Max(2f, U(3)), new Color(150, 156, 166));
        Disc(U(32), U(20), U(2.4f), new Color(206, 84, 60));
        // A yoke seen from above: a cross-bar with two grips, which is what a pilot's hands find.
        // A wheel needs more plate than there is, and one clipped to fit left a ghost of a circle
        // rather than a control.
        Rect(U(10), U(31), U(20), MathF.Max(2f, U(3)), new Color(66, 70, 78));
        Rect(U(10), U(31), U(20), 1, new Color(172, 180, 192), 0.5f);
        Rect(U(10), U(34), U(20), 1, Color.Black, 0.45f);
        foreach (var gx in new[] { U(10), U(27) })
        {
            Rect(gx, U(28), MathF.Max(2f, U(3)), U(6), new Color(52, 56, 64));
            Rect(gx, U(28), MathF.Max(2f, U(3)), 1, new Color(184, 192, 204), 0.45f);
        }
        for (var i = 0; i < 4; i++)
        {
            Rect(U(12) + i * U(5), U(36), MathF.Max(2f, U(3)), MathF.Max(1f, U(2)), new Color(44, 48, 56));
            Px(U(13) + i * U(5), U(36), new Color(250, 212, 124), lit && i != 2 ? 0.9f : 0.25f);
        }
    }

    private void Jukebox(bool lit)
    {
        var accent = new Color(226, 176, 78);
        Housing(accent, 137);

        // The lit arch. It is the one shape that says jukebox and nothing else - every machine ever
        // built for this job has had one - and it survives at any size because it is a silhouette
        // rather than a detail.
        for (var k = 0; k <= 60; k++)
        {
            var t = k / 60f;
            var ang = MathF.PI * (1f - t);
            var x = U(20) + MathF.Cos(ang) * U(13.5f);
            var y = U(21) - MathF.Sin(ang) * U(9.5f);
            var glow = Color.Lerp(new Color(236, 132, 58), new Color(252, 216, 126), MathF.Sin(t * MathF.PI));
            Disc(x, y, MathF.Max(1.2f, U(1.9f)), lit ? glow : new Color(72, 58, 42), lit ? 1f : 0.9f);
        }
        // The tube behind the arch, so the light has something to come out of.
        for (var k = 0; k <= 60; k++)
        {
            var t = k / 60f;
            var ang = MathF.PI * (1f - t);
            Disc(U(20) + MathF.Cos(ang) * U(13.5f), U(21) - MathF.Sin(ang) * U(9.5f),
                MathF.Max(0.8f, U(0.8f)), lit ? new Color(255, 244, 214) : new Color(120, 110, 96), 0.85f);
        }

        // The selection window, where the current track shows.
        Glass(U(11), U(17), U(18), MathF.Max(3f, U(6)), new Color(110, 198, 226), lit);

        // Speaker grille: slats with a lit lip under each, so it reads as cut into the front panel
        // rather than printed on it.
        for (var k = 0; k < 5; k++)
        {
            var y = U(26) + k * U(2.4f);
            Rect(U(10), y, U(20), MathF.Max(1f, U(1.2f)), new Color(28, 24, 22), 0.85f);
            Rect(U(10), y + MathF.Max(1f, U(1.2f)), U(20), 1, Color.White, 0.16f);
        }

        // Chrome down both flanks and a row of selection keys along the bottom.
        foreach (var sx in new[] { U(7), U(31) })
        {
            Rect(sx, U(16), MathF.Max(1f, U(2)), U(20), new Color(178, 184, 194));
            Rect(sx, U(16), 1, U(20), Color.White, 0.45f);
        }
        for (var k = 0; k < 6; k++)
        {
            var x = U(10) + k * U(3.4f);
            Rect(x, U(37), MathF.Max(1f, U(2.4f)), MathF.Max(1f, U(2)), new Color(232, 228, 218));
            Rect(x, U(37), MathF.Max(1f, U(2.4f)), 1, Color.White, 0.5f);
            if (lit && k == 2)
                Rect(x, U(37), MathF.Max(1f, U(2.4f)), MathF.Max(1f, U(2)), accent, 0.75f);
        }
    }

    private void Locker(bool lit)
    {
        var accent = new Color(226, 188, 66);
        Housing(accent, 83);
        // Two door leaves with a seam down the middle, and a window with a suit hanging behind it.
        Rect(U(6), U(11), U(28), U(24), new Color(74, 78, 88));
        Rect(U(6), U(11), U(28), 1, new Color(160, 168, 180), 0.4f);
        Rect(U(19), U(11), MathF.Max(1f, U(2)), U(24), new Color(24, 26, 32));
        Rect(U(9), U(14), U(9), U(11), new Color(16, 26, 30));
        if (lit)
        {
            Disc(U(13), U(18), U(2.4f), new Color(150, 172, 196));
            Rect(U(11), U(20), U(5), U(5), new Color(120, 96, 60));
            Px(U(12), U(17), Color.White, 0.5f);
        }
        Rect(U(22), U(14), U(9), U(11), new Color(30, 34, 40));
        for (var k = 0; k < 3; k++)
            Rect(U(23), U(16) + k * U(3), U(7), 1, new Color(120, 126, 136), 0.5f);
        // Handles.
        foreach (var hx in new[] { U(16), U(22) })
            Rect(hx, U(28), MathF.Max(1f, U(2)), U(5), new Color(160, 166, 176));
        for (var x = U(7); x < U(33); x++)
            Rect(x, U(36), 1, MathF.Max(1f, U(2)),
                (int)(x / MathF.Max(2f, U(3))) % 2 == 0 ? new Color(232, 190, 60) : new Color(30, 30, 34));
    }
}
