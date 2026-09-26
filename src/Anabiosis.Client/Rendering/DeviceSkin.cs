using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

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
        // Direct user request - the "производство" tab's 4 workbenches, previously all Generic.
        ConstructionBench, Fabricator, Deconstructor, WeaponWorkbench,
        // Direct user request - a real turret mount (distinct from Weapons, which is the capacitor/
        // charging look, not the gun itself), plus a bed and a shuttle hangar bay.
        Turret, Bed, ShuttleHangar,
        // Direct user request ("тройная дверь") - a purely cosmetic device themed to look like the
        // real in-game door (ShipRenderer.Doors.cs's own bronze frame/orange panel/brace reskin).
        TripleDoor,
        // Direct user request ("сделай им свои уникальные текстуры") - ShipStatusMonitor/
        // CommsConsole used to both borrow Navigation's own scope-dish art (no bespoke look existed
        // yet); each gets its own now.
        ShipStatusMonitor, CommsConsole,
        // Direct user request ("сделай щитку свою собственную текстуру") - "Щиток"
        // (CustomDeviceKind.Junction) used to fall back to the flat tinted-swatch-plus-glyph look
        // every not-yet-fitted kind gets (ShipRenderer.Devices.cs's own DrawJunctionBox); a small
        // single-breaker relay box now, distinct from Distribution's own full multi-breaker panel.
        Junction,
    }

    private readonly GraphicsDevice _graphics;
    private readonly Dictionary<(Face, int, int, bool), Texture2D> _cache = new();

    public DeviceSkin(GraphicsDevice graphics) => _graphics = graphics;

    /// <param name="width">Pixel width of the device's own destination rect.</param>
    /// <param name="height">Pixel height of the device's own destination rect. Direct user request
    /// ("у устройств которые не являются квадратами... текстура была в соответствии с этой
    /// формой") - width and height are no longer forced equal (every face used to bake as one
    /// square texture that a non-square footprint's own caller then stretched non-uniformly). The
    /// hero art (each face's own sub-method - vice, print bed, turret...) still bakes at its
    /// originally-designed SQUARE proportions and sits centered in whichever of width/height is
    /// smaller; Housing's own background (fill/bevel/band/bolts/serial tag) spans the FULL
    /// rectangle, so a wide or tall device reads as a genuinely longer/taller mounting plate around
    /// the same centered hero art, never a stretched or squashed picture. See Housing's own doc
    /// comment for exactly where that split happens.</param>
    /// <param name="lit">Whether the ship can power it. Baked in rather than drawn over, because an
    /// unpowered machine differs from a powered one across the whole face - dead indicators, dark
    /// glass - and two bakes cost less than compositing that every frame.</param>
    public Texture2D Get(Face face, int width, int height, bool lit)
    {
        width = Math.Max(8, width);
        height = Math.Max(8, height);
        if (_cache.TryGetValue((face, width, height, lit), out var cached))
            return cached;
        var baked = Bake(face, width, height, lit);
        _cache[(face, width, height, lit)] = baked;
        return baked;
    }

    public void Dispose()
    {
        foreach (var texture in _cache.Values)
            texture.Dispose();
        _cache.Clear();
    }

    // ---------------------------------------------------------------- the canvas

    // The physical baked texture, which may be non-square (a wide/tall device footprint).
    private int _canvasWidth;
    private int _canvasHeight;
    // The hero art's own square working area - always Math.Min(_canvasWidth, _canvasHeight), the
    // same 40-unit reference square every face was originally designed at (U(n) below).
    private int _contentSize;
    // Added to every Px/Rect/Disc/Ring/Line/Glass coordinate so the hero art centers within a
    // non-square canvas instead of stretching to fill it - zero while Housing draws its OWN
    // full-canvas background, set to the real centering offset at the end of Housing (see its own
    // doc comment), so every face method's existing hero-art calls (unchanged, still just "Housing
    // (...); <hero art>;") automatically land centered with no per-face edits needed.
    private int _offsetX;
    private int _offsetY;
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

    // x/y here are already ABSOLUTE canvas coordinates (Px adds the offset before calling this) -
    // the chamfer clips the actual physical plate's own 4 corners, whatever its aspect ratio, not
    // the hero content square's corners.
    private bool Inside(int x, int y)
    {
        var w = _canvasWidth - 1;
        var h = _canvasHeight - 1;
        return !(x + y < _chamfer || (w - x) + y < _chamfer
                 || x + (h - y) < _chamfer || (w - x) + (h - y) < _chamfer);
    }

    private void Px(float fx, float fy, Color c, float a = 1f)
    {
        int x = (int)MathF.Round(fx) + _offsetX, y = (int)MathF.Round(fy) + _offsetY;
        if (a <= 0f || x < 0 || y < 0 || x >= _canvasWidth || y >= _canvasHeight || !Inside(x, y))
            return;
        var d = _buffer[y * _canvasWidth + x];
        // Only ever composited over an already-opaque plate, so the result stays opaque and there is
        // no premultiplied-alpha trap on the way into the texture.
        _buffer[y * _canvasWidth + x] = new Color(
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

    // Draws in TWO coordinate spaces, split by _offsetX/_offsetY (direct user request - "у устройств
    // которые не являются квадратами... текстура была в соответствии с этой формой"): everything in
    // THIS method runs with the offset at zero, i.e. in absolute canvas space, so the background
    // fill/bevel/painted band/bolts/grime/serial tag all span the FULL _canvasWidth x _canvasHeight
    // rectangle exactly as a real mounting plate that size would look - a wide device gets a wider
    // band and corner bolts further apart, a tall one gets a taller plate, never a stretched image.
    // The offset is only switched ON at the very end, right before returning - every face method's
    // own hero-art calls (vice, print bed, turret...) run AFTER their own call to this method
    // returns, so they automatically land offset into the centered square content area with zero
    // per-face changes needed.
    private void Housing(Color accent, int seed)
    {
        _offsetX = 0;
        _offsetY = 0;
        for (var y = 0; y < _canvasHeight; y++)
        for (var x = 0; x < _canvasWidth; x++)
        {
            if (!Inside(x, y))
                continue;
            var v = (Hash(x * 13 + seed) - 0.5f) * 16f + (Hash(x + seed, y / 7) - 0.5f) * 7f;
            // A finer, higher-frequency grain on top of the coarser brushed-metal streaks above -
            // direct user request ("более интересные текстуры... качественнее, детализированнее") -
            // reads as a machined surface up close instead of a flat tinted plate. Shared by EVERY
            // face (Housing is the base every one of them paints over), so this one change lifts all
            // of them at once.
            var grain = (Hash(x * 7 + y * 11 + seed, 5) - 0.5f) * 5f;
            var g = 13f * (1f - y / (_canvasHeight * 0.5f));      // lit from above, like the rest of the deck
            // Soft vignette - the plate reads as lit from a point above center rather than flat-
            // shaded, the same depth cue a real photographed panel would have.
            var dx = (x - _canvasWidth / 2f) / (_canvasWidth * 0.6f);
            var dy = (y - _canvasHeight / 2f) / (_canvasHeight * 0.6f);
            var vignette = -10f * MathHelper.Clamp(dx * dx + dy * dy - 0.4f, 0f, 1f);
            _buffer[y * _canvasWidth + x] = new Color(
                (int)MathHelper.Clamp(Steel.R + v + grain + g + vignette, 0, 255),
                (int)MathHelper.Clamp(Steel.G + v + grain + g + vignette, 0, 255),
                (int)MathHelper.Clamp(Steel.B + v + grain + g + vignette, 0, 255), 255);
        }

        // Bevel. Two lines, and the plate stops reading as a sticker.
        for (var x = _chamfer; x < _canvasWidth - _chamfer; x++)
        {
            Px(x, 0, new Color(235, 240, 250), 0.45f);
            Px(x, _canvasHeight - 1, Color.Black, 0.55f);
        }
        for (var y = _chamfer; y < _canvasHeight - _chamfer; y++)
        {
            Px(0, y, new Color(215, 225, 240), 0.28f);
            Px(_canvasWidth - 1, y, Color.Black, 0.42f);
        }
        for (var i = 0; i < _chamfer; i++)
        {
            Px(i, _chamfer - i, new Color(220, 230, 245), 0.35f);
            Px(_canvasWidth - 1 - i, _chamfer - i, new Color(200, 210, 230), 0.25f);
            Px(i, _canvasHeight - 1 - (_chamfer - i), Color.Black, 0.40f);
            Px(_canvasWidth - 1 - i, _canvasHeight - 1 - (_chamfer - i), Color.Black, 0.50f);
        }

        // The painted band. Wear is rolled per two-pixel row rather than per row: at finer grain the
        // gaps lined up into columns and the band read as unreadable lettering, which is worse than
        // a clean stripe because the eye stops and tries to read it.
        var bandTop = MathF.Round(3f * _u);
        var bandBottom = MathF.Round(7f * _u);
        for (var x = _chamfer; x < _canvasWidth - _chamfer; x++)
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
        // A second, thinner pinstripe just under the separator - a two-tone paint job reads as
        // more deliberately manufactured than one flat stripe.
        var pinY = bandBottom + MathF.Max(2f, U(2f));
        var pinAccent = new Color((int)(accent.R * 0.6f), (int)(accent.G * 0.6f), (int)(accent.B * 0.6f));
        for (var x = _chamfer; x < _canvasWidth - _chamfer; x++)
            Px(x, pinY, pinAccent, 0.7f);

        // Bolts, and grime pooling low where nobody wipes.
        var bolt = MathF.Max(1.2f, 1.6f * _u);
        foreach (var (bx, by) in new[]
                 {
                     (_chamfer - 1, _chamfer + 1), (_canvasWidth - _chamfer, _chamfer + 1),
                     (_chamfer - 1, _canvasHeight - _chamfer - 1), (_canvasWidth - _chamfer, _canvasHeight - _chamfer - 1),
                 })
        {
            Disc(bx, by, bolt, new Color(58, 62, 70));
            Px(bx, by - 1, new Color(200, 208, 220), 0.55f);
        }
        for (var i = 0; i < 26; i++)
        {
            var gx = Hash(seed * 3 + i, 1) * _canvasWidth;
            var gy = _canvasHeight - 1 - Hash(seed * 3 + i, 2) * Hash(seed * 3 + i, 2) * (_canvasHeight * 0.45f);
            Disc(gx, gy, (1f + Hash(i, seed) * 1.6f) * _u, new Color(30, 28, 26), 0.16f);
        }

        // A small rating/serial plate near the bottom-right bolt - a manufactured machine carries a
        // maker's tag; three tiny ticks read as one at this size without spending real letterforms.
        var tagW = MathF.Max(4f, U(6));
        var tagH = MathF.Max(2f, U(3));
        var tagX = _canvasWidth - _chamfer - tagW - 1;
        var tagY = _canvasHeight - _chamfer - tagH - 1;
        Rect(tagX, tagY, tagW, tagH, new Color(184, 188, 194), 0.5f);
        for (var i = 0; i < 3; i++)
            Rect(tagX + 1 + i * MathF.Max(1f, U(1.6f)), tagY + 1, MathF.Max(0.8f, U(1f)), MathF.Max(0.8f, U(1f)),
                new Color(56, 60, 66), 0.7f);

        // From here on, every subsequent Px/Rect/Disc/Line/Glass call - the per-face HERO art drawn
        // by the caller right after this returns - is centered in the smaller "content square"
        // instead of stretched across a non-square canvas.
        _offsetX = (_canvasWidth - _contentSize) / 2;
        _offsetY = (_canvasHeight - _contentSize) / 2;
    }

    // ---------------------------------------------------------------- the faces

    private Texture2D Bake(Face face, int width, int height, bool lit)
    {
        _canvasWidth = width;
        _canvasHeight = height;
        _contentSize = Math.Min(width, height);
        _chamfer = Math.Max(2, _contentSize / 6);
        _u = _contentSize / 40f;
        _buffer = new Color[width * height];

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
            case Face.ConstructionBench: ConstructionBench(lit); break;
            case Face.Fabricator: Fabricator(lit); break;
            case Face.Deconstructor: Deconstructor(lit); break;
            case Face.WeaponWorkbench: WeaponWorkbench(lit); break;
            case Face.Turret: Turret(lit); break;
            case Face.Bed: Bed(lit); break;
            case Face.ShuttleHangar: ShuttleHangar(lit); break;
            case Face.TripleDoor: TripleDoor(lit); break;
            case Face.ShipStatusMonitor: ShipStatusMonitor(lit); break;
            case Face.CommsConsole: CommsConsole(lit); break;
            case Face.Junction: Junction(lit); break;
            default: Housing(new Color(140, 148, 160), 97); break;
        }

        var texture = new Texture2D(_graphics, _canvasWidth, _canvasHeight);
        texture.SetData(_buffer);
        _buffer = Array.Empty<Color>();
        return texture;
    }

    private float U(float atForty) => atForty * _u;

    private void Oxygen(bool lit)
    {
        var accent = new Color(64, 162, 178);
        Housing(accent, 3);
        // Cast shadow the dial pair throws onto the plate - the same "sits ON the surface" cue the
        // workbenches' own vice/print-bed shadows already use.
        Rect(U(8), U(21.5f), U(24), MathF.Max(1f, U(1.2f)), Color.Black, 0.2f);
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
        // A valve wheel between the dials - a real gas panel is never without one, and it's the one
        // thing here a hand would actually turn.
        Ring(U(20), U(13.5f), U(2.6f), new Color(150, 156, 166), 0.85f, 1f);
        for (var i = 0; i < 4; i++)
        {
            var ang = i * MathF.PI / 2f + 0.4f;
            Line(U(20), U(13.5f), U(20) + MathF.Cos(ang) * U(2.6f), U(13.5f) + MathF.Sin(ang) * U(2.6f), new Color(120, 126, 136), 0.7f);
        }
        Disc(U(20), U(13.5f), MathF.Max(0.6f, U(0.8f)), new Color(60, 66, 70));
        // Pipework running off the plate: hardware that continues past the housing says the machine
        // is plumbed into the ship rather than parked on it. Frost rime haloing each joint - a line
        // carrying compressed gas runs cold, and condensation is the tell that gives it away.
        Rect(U(10), U(27), U(20), MathF.Max(2f, U(4)), new Color(128, 134, 142));
        Rect(U(10), U(27), U(20), 1, new Color(215, 225, 235), 0.5f);
        Rect(U(10), U(30), U(20), 1, Color.Black, 0.5f);
        foreach (var fx in new[] { U(14), U(26) })
        {
            Rect(fx, U(26), MathF.Max(1f, U(2)), U(6), new Color(86, 92, 100));
            Disc(fx + U(1), U(26.5f), U(2.4f), new Color(200, 224, 230), 0.22f);
            Rect(fx - U(1), U(31.5f), MathF.Max(2f, U(3)), MathF.Max(0.6f, U(0.8f)), new Color(70, 62, 54), 0.24f);
        }
        Rect(U(28), U(31), MathF.Max(2f, U(4)), U(6), new Color(128, 134, 142));
        Disc(U(30), U(34), U(2.2f), new Color(150, 158, 168));
        // A stencilled O2 tag near the corner bolt - a manufactured machine carries a label
        // identifying what it handles, the same instinct Housing's own serial tag already uses.
        Rect(U(6.5f), U(6), MathF.Max(3f, U(4)), MathF.Max(2f, U(3)), new Color(210, 214, 208), 0.5f);
        Rect(U(7), U(6.6f), MathF.Max(2f, U(3)), MathF.Max(1f, U(1.6f)), new Color(50, 130, 140), 0.6f);
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
        // Rivets around the cowling ring - the collar is bolted on, not moulded, and a bolt every
        // 45 degrees is the same "manufactured object" tell Housing's own corner bolts use.
        for (var i = 0; i < 8; i++)
        {
            var ang = i * MathF.PI / 4f + 0.3f;
            Disc(U(20) + MathF.Cos(ang) * U(10.4f), U(23) + MathF.Sin(ang) * U(10.4f), MathF.Max(0.6f, U(0.7f)), new Color(150, 154, 160), 0.6f);
        }
        for (var i = 0; i < 30; i++)
            Px(Hash(i, 5) * _contentSize, U(9) + Hash(i, 6) * U(5), new Color(58, 40, 30), 0.3f);
        // Oil weeping from the cowling seal, streaking down toward the hazard band - a hot machine
        // that's actually been run leaves a trail, not a clean skirt.
        foreach (var ox in new[] { U(13), U(28) })
            Rect(ox, U(33), MathF.Max(0.8f, U(1)), U(3), new Color(30, 24, 20), 0.3f);
        // A soft ground shadow along the cowling's own lower rim, the same "sits ON the plate, not
        // painted flat onto it" cue the workbenches' cast shadows already use.
        Ring(U(20), U(23), U(11), Color.Black, 0.16f, U(1.4f));
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
        // Cast shadow the insulator stacks throw across the lower plate.
        Rect(U(6), U(34.5f), U(28), MathF.Max(1f, U(1.2f)), Color.Black, 0.2f);
        // Emitter: arcs around a hot node, so the thing that projects a field looks like it projects
        // something.
        foreach (var (r, a) in new[] { (10f, 0.30f), (7.5f, 0.45f), (5f, 0.65f) })
            Ring(U(20), U(26), U(r), new Color(120, 180, 255), lit ? a : a * 0.25f, 1.1f);
        Disc(U(20), U(26), U(3), new Color(30, 44, 70));
        Disc(U(20), U(26), U(2), lit ? new Color(170, 215, 255) : new Color(60, 74, 96), 0.95f);
        // Scorch marks around the node, where the field arcs when the emitter cycles - a machine
        // that throws energy leaves its own burn signature the way Deconstructor's mouth does.
        for (var i = 0; i < 8; i++)
        {
            var ang = Hash(i, 23) * MathF.PI * 2f;
            var r0 = U(3.5f) + Hash(i, 3) * U(1.5f);
            Line(U(20) + MathF.Cos(ang) * r0, U(26) + MathF.Sin(ang) * r0,
                U(20) + MathF.Cos(ang) * (r0 + U(2)), U(26) + MathF.Sin(ang) * (r0 + U(2)), new Color(30, 30, 34), 0.2f);
        }
        // Ceramic insulators: the stacked white discs that say high voltage without a warning label.
        // Heavy cabling runs from each stack up to the emitter, so the power actually goes somewhere.
        foreach (var ix in new[] { U(9), U(31) })
        {
            for (var k = 0; k < 3; k++)
            {
                Rect(ix - U(3), U(22) + k * U(3), U(6), MathF.Max(1f, U(2)), new Color(216, 212, 200));
                Rect(ix - U(3), U(23) + k * U(3), U(6), 1, new Color(120, 118, 110), 0.6f);
            }
            Rect(ix - 1, U(31), MathF.Max(1f, U(2)), U(4), new Color(96, 102, 110));
            Line(ix, U(22), U(20) + (ix < U(20) ? U(3) : -U(3)), U(23.5f), new Color(40, 42, 48), 0.6f);
        }
        // A stencilled high-voltage warning plate - the same tag every real capacitor bank carries.
        Rect(U(7), U(33), MathF.Max(3f, U(4)), MathF.Max(2f, U(2.6f)), new Color(226, 194, 40), 0.6f);
        Rect(U(7.6f), U(33.5f), MathF.Max(2f, U(2.6f)), MathF.Max(1f, U(1.2f)), new Color(24, 24, 26), 0.7f);
        Glass(U(11), U(11), U(18), MathF.Max(2f, U(5)), accent, lit);
    }

    private void Weapons(bool lit)
    {
        var accent = new Color(206, 80, 64);
        Housing(accent, 101);
        // Cast shadow the cylinder pair throws onto the plate below them.
        Rect(U(7), U(32.5f), U(26), MathF.Max(1f, U(1.4f)), Color.Black, 0.22f);
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
            // Cooling vents at the base - a bank that dumps charge that fast needs somewhere for the
            // heat to go.
            for (var v = 0; v < 3; v++)
                Rect(cx - U(3) + v * U(2.2f), U(30.5f), MathF.Max(0.8f, U(1)), MathF.Max(1f, U(1.6f)), new Color(24, 24, 28), 0.6f);
        }
        // A heavy jumper cable slung between the two cylinders, low, below the spark gap - the
        // charge actually has a path between them besides the arc itself.
        Line(U(15.5f), U(28), U(20), U(30.5f), new Color(40, 40, 44), 0.7f);
        Line(U(20), U(30.5f), U(24.5f), U(28), new Color(40, 40, 44), 0.7f);
        // The gap itself. Dead metal when the ship cannot power it - an arc drawn on an unpowered
        // machine is the sort of detail that quietly tells the player the wrong thing.
        if (lit)
        {
            Line(U(14), U(12), U(19), U(10), new Color(200, 230, 255), 0.85f);
            Line(U(19), U(10), U(26), U(12), new Color(200, 230, 255), 0.85f);
            Disc(U(20), U(10), U(1.6f), Color.White, 0.9f);
        }
        // Hazard tape wrapped around each cylinder's own base - the same "anything that stores or
        // releases energy gets marked" instinct Shields'/ConstructionBench's tape already follows.
        foreach (var cx in new[] { U(12), U(28) })
            for (var i = 0; i < 4; i++)
                Rect(cx - U(4) + i * U(2), U(31.6f), MathF.Max(1f, U(1.4f)), MathF.Max(0.7f, U(0.8f)),
                    i % 2 == 0 ? new Color(226, 170, 50) : new Color(24, 24, 26));
        Glass(U(9), U(34), U(22), MathF.Max(2f, U(4)), accent, lit);
    }

    private void Auxiliary(bool lit)
    {
        var accent = new Color(150, 162, 178);
        Housing(accent, 107);
        // Relay bank and a round meter. Deliberately the plainest face of the set: this is the
        // system with no single job, and dressing it up as something specific would be a lie the
        // player has to unlearn later - the extra texture below is wear and improvisation, not a
        // borrowed identity from some other machine.
        for (var k = 0; k < 4; k++)
        {
            var rx = U(8) + k % 2 * U(9);
            var ry = U(13) + k / 2 * U(9);
            Rect(rx, ry, U(7), U(7), new Color(52, 56, 64));
            Rect(rx, ry, U(7), 1, new Color(150, 158, 170), 0.45f);
            Rect(rx + U(2), ry + U(2), U(3), U(3), new Color(90, 96, 106));
            Disc(rx + U(3.5f), ry + U(5.5f), MathF.Max(0.8f, U(1f)),
                lit && k != 1 ? new Color(120, 230, 150) : new Color(50, 54, 60), 0.9f);
            // A scuff at one corner of each relay box - four identical boxes read as parts stamped
            // from the same die, but no two of them wear the same.
            Rect(rx + Hash(k, 3) * U(4), ry + U(5.5f) + Hash(k, 4) * U(1), MathF.Max(0.8f, U(1)), MathF.Max(0.6f, U(0.8f)),
                new Color(30, 30, 32), 0.22f);
        }
        Disc(U(29), U(19), U(6.4f), new Color(22, 26, 30));
        Disc(U(29), U(19), U(5.4f), new Color(198, 204, 196));
        Line(U(29), U(19), U(26), U(15), new Color(60, 66, 74));
        Disc(U(29), U(19), MathF.Max(0.8f, U(1f)), new Color(40, 44, 48));
        // Cable gland: the conduit has to go somewhere. A second, loose jumper hangs off it unused -
        // the one touch that says this panel gets reconfigured by hand, not wired once and sealed.
        Rect(U(12), U(31), U(16), MathF.Max(2f, U(4)), new Color(58, 62, 70));
        Rect(U(12), U(31), U(16), 1, new Color(150, 158, 170), 0.4f);
        Line(U(14), U(33), U(13), U(35.5f), new Color(40, 40, 44), 0.7f);
        Line(U(13), U(35.5f), U(15), U(36.5f), new Color(40, 40, 44), 0.7f);
        Disc(U(15), U(36.5f), MathF.Max(0.6f, U(0.7f)), new Color(60, 62, 68));
        Glass(U(10), U(36), U(14), MathF.Max(2f, U(3)), accent, lit);
    }

    private void Distribution(bool lit)
    {
        var accent = new Color(154, 116, 206);
        Housing(accent, 31);
        // Cast shadow the breaker row throws onto the plate.
        Rect(U(6), U(27.5f), U(28), MathF.Max(1f, U(1.2f)), Color.Black, 0.2f);
        // Copper bus bars behind five breakers - one per system, which is exactly what this panel
        // hands out, so the face states the machine's job before anybody opens it. Oxidation
        // blooming along the bars where they run hottest - live copper tarnishes, and a bar that's
        // never discoloured a day in its life reads as a prop.
        foreach (var bx in new[] { U(11), U(20), U(29) })
        {
            Rect(bx - 1, U(14), MathF.Max(1f, U(2)), U(20), new Color(150, 96, 52));
            Rect(bx - 1, U(14), 1, U(20), new Color(206, 146, 88), 0.7f);
            for (var s = 0; s < 3; s++)
                Disc(bx + Hash((int)bx, s) * U(1) - U(0.5f), U(16) + s * U(6), MathF.Max(0.8f, U(1.2f)), new Color(70, 100, 84), 0.22f);
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
        // A stencilled high-voltage warning strip along the bottom edge of the breaker row - the
        // same tag Shields' own capacitor bank carries, since this panel is exactly as live.
        for (var i = 0; i < 10; i++)
            Rect(U(7) + i * MathF.Max(1.6f, U(2.2f)), U(28), MathF.Max(1.2f, U(1.6f)), MathF.Max(0.8f, U(1)),
                i % 2 == 0 ? new Color(226, 194, 40) : new Color(24, 24, 26), 0.55f);
        Glass(U(8), U(30), U(24), MathF.Max(2f, U(5)), accent, lit);
    }

    private void Battery(bool lit)
    {
        var accent = new Color(96, 186, 116);
        Housing(accent, 41);
        // Terminal posts, capped red and black. Nothing else on the deck looks like this. A crust of
        // corrosion around each post - a real terminal weeps and crystallizes over time, which is
        // exactly the kind of wear a clean cap would lie about.
        foreach (var (tx, cap) in new[] { (U(13), new Color(188, 62, 54)), (U(27), new Color(36, 38, 44)) })
        {
            Rect(tx - U(3), U(12), U(6), MathF.Max(2f, U(3)), new Color(140, 146, 156));
            Disc(tx, U(12), U(2.6f), cap);
            Px(tx - 1, U(11), Color.White, 0.4f);
            for (var i = 0; i < 5; i++)
                Px(tx + (Hash(i, (int)tx) - 0.5f) * U(5), U(15) + Hash(i, 9) * U(1.4f), new Color(190, 200, 170), 0.3f);
        }
        // A retaining strap buckled across the bank - the small hardware that says these cells are
        // actually secured against ship acceleration, not just resting in a tray.
        Rect(U(7), U(20.5f), U(20), MathF.Max(1f, U(1.4f)), new Color(50, 52, 56), 0.6f);
        Rect(U(24.5f), U(19.5f), MathF.Max(2f, U(3)), MathF.Max(2f, U(3.2f)), new Color(90, 92, 98));
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
        Rect(U(28), U(17), MathF.Max(3f, U(5)), 1, new Color(120, 128, 138), 0.35f);
        // Cast shadow the cell bank throws onto the plate below it.
        Rect(U(7), U(35.5f), U(20), MathF.Max(1f, U(1.2f)), Color.Black, 0.2f);
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
            // Cast shadow the shelf's own lip throws onto the crates sitting under it.
            Rect(U(7), y - MathF.Max(1f, U(1)), U(26), MathF.Max(0.8f, U(1)), Color.Black, 0.18f);
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
                // A stencilled contents tick on each crate - the same "manufactured object carries a
                // label" instinct as the manifest card below, just closer to the goods themselves.
                if (Hash((int)(k * 13 + cx), 6) > 0.4f)
                    Rect(U(7) + U(cx) + 1, y + U(2.6f), MathF.Max(1f, U(cw * 0.4f)), MathF.Max(0.6f, U(0.8f)),
                        new Color(230, 224, 208), 0.5f);
            }
            Rect(U(6), y + U(7), U(28), MathF.Max(1f, U(2)), new Color(128, 134, 142));
            Rect(U(6), y + U(7), U(28), 1, new Color(206, 214, 224), 0.5f);
        }
        // A cargo strap and buckle lashed diagonally across the middle shelf - one crate is held
        // down against acceleration instead of just resting there.
        Line(U(9), U(20.5f), U(24), U(17.5f), new Color(60, 84, 60), 0.65f);
        Rect(U(22.5f), U(16.8f), MathF.Max(2f, U(2.6f)), MathF.Max(2f, U(2.2f)), new Color(90, 92, 98));
        // Hazard corner tape on the top shelf's own end - Barotrauma marks anything sharp-edged or
        // liable to shift.
        for (var i = 0; i < 3; i++)
            Rect(U(6) + i * MathF.Max(1.2f, U(1.4f)), U(12), MathF.Max(0.8f, U(0.9f)), MathF.Max(2f, U(3)),
                i % 2 == 0 ? new Color(226, 170, 50) : new Color(24, 24, 26), 0.6f);
        // A manifest card. Dropped below a certain size: at that point it is three grey pixels and
        // reads as damage rather than as paper.
        if (_contentSize < 26)
            return;
        Rect(U(27), U(30), U(8), U(6), new Color(222, 216, 196));
        for (var k = 0; k < 3; k++)
            Rect(U(28), U(31) + k * U(2), U(6), 1, new Color(70, 74, 80), 0.6f);
    }

    private void Navigation(bool lit)
    {
        var accent = new Color(72, 196, 208);
        Housing(accent, 61);
        // Cast shadow the scope bezel throws onto the plate.
        Ring(U(20), U(21.5f), U(10.6f), Color.Black, 0.16f, U(1.6f));
        // A round scope rather than another rectangle. The silhouette alone separates this console
        // from the helm across the room, which is the first thing the eye sorts objects by. A
        // compass-rose ring of ticks around the bezel - the one detail that says "this reads a
        // bearing" even with the glass dark.
        for (var i = 0; i < 16; i++)
        {
            var ang = i * MathF.PI / 8f;
            var r0 = U(10.3f);
            var r1 = r0 + (i % 4 == 0 ? U(1.4f) : U(0.8f));
            Line(U(20) + MathF.Cos(ang) * r0, U(20) + MathF.Sin(ang) * r0,
                U(20) + MathF.Cos(ang) * r1, U(20) + MathF.Sin(ang) * r1, new Color(150, 156, 164), 0.5f);
        }
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
        // A worn scratch across the bezel's own lower-left, and a cable running off it down toward
        // the keys - the console is plumbed into the console frame below it, not floating free.
        Line(U(13), U(27), U(15), U(24), new Color(20, 22, 26), 0.4f);
        Line(U(16), U(29.5f), U(16), U(31.5f), new Color(40, 40, 44), 0.6f);
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
        // Cast shadow the crossbar throws across the pedestal below it.
        Rect(U(10), U(30.3f), U(20), MathF.Max(0.8f, U(1)), Color.Black, 0.2f);
        Rect(U(10), U(31), U(20), MathF.Max(2f, U(3)), new Color(66, 70, 78));
        Rect(U(10), U(31), U(20), 1, new Color(172, 180, 192), 0.5f);
        Rect(U(10), U(34), U(20), 1, Color.Black, 0.45f);
        foreach (var gx in new[] { U(10), U(27) })
        {
            Rect(gx, U(28), MathF.Max(2f, U(3)), U(6), new Color(52, 56, 64));
            Rect(gx, U(28), MathF.Max(2f, U(3)), 1, new Color(184, 192, 204), 0.45f);
            // Worn grip texture on each handle - a real yoke gets held, and bare metal shows it.
            for (var k = 0; k < 3; k++)
                Rect(gx + MathF.Max(0.5f, U(0.6f)), U(29) + k * MathF.Max(1f, U(1.4f)), MathF.Max(1f, U(1.6f)), MathF.Max(0.5f, U(0.6f)),
                    new Color(150, 154, 160), 0.25f);
        }
        for (var i = 0; i < 4; i++)
        {
            Rect(U(12) + i * U(5), U(36), MathF.Max(2f, U(3)), MathF.Max(1f, U(2)), new Color(44, 48, 56));
            Px(U(13) + i * U(5), U(36), new Color(250, 212, 124), lit && i != 2 ? 0.9f : 0.25f);
        }
        // A small brass builder's plate on the pedestal - the one piece of hardware that says this
        // console was actually manufactured somewhere, not conjured whole.
        Rect(U(30.5f), U(31.5f), MathF.Max(3f, U(4)), MathF.Max(2f, U(2.4f)), new Color(180, 148, 80), 0.6f);
        Rect(U(31), U(32), MathF.Max(2f, U(2.6f)), MathF.Max(0.6f, U(0.7f)), new Color(80, 62, 30), 0.6f);
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
        // A coin/credit slot and a small maker's plate - the machine takes payment and was built by
        // someone, the same "manufactured object carries a label" instinct as Housing's own tag.
        Rect(U(29), U(31), MathF.Max(1.2f, U(1.4f)), MathF.Max(2f, U(3)), new Color(24, 22, 20), 0.7f);
        Rect(U(9), U(31.5f), MathF.Max(3f, U(4)), MathF.Max(1.6f, U(2)), new Color(210, 176, 90), 0.5f);
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
        // Dents and scuffs on the leaves - a locker that's actually opened and shut every shift
        // takes hits at hand height, not just around the edges.
        Rect(U(6), U(11), U(28), U(24), new Color(74, 78, 88));
        Rect(U(6), U(11), U(28), 1, new Color(160, 168, 180), 0.4f);
        for (var i = 0; i < 5; i++)
        {
            var dx = U(7) + Hash(i, 83) * U(26);
            var dy = U(23) + Hash(i, 4) * U(10);
            Disc(dx, dy, MathF.Max(0.6f, U(0.7f)), Color.Black, 0.14f);
        }
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
        // Handles, each with a small hasp for a padlock - the one detail that says whatever's inside
        // is somebody's own, not communal shelf space.
        foreach (var hx in new[] { U(16), U(22) })
        {
            Rect(hx, U(28), MathF.Max(1f, U(2)), U(5), new Color(160, 166, 176));
            Rect(hx - MathF.Max(1f, U(1.2f)), U(29), MathF.Max(2f, U(2.6f)), MathF.Max(1.4f, U(1.8f)), new Color(70, 74, 82));
        }
        Disc(U(19), U(29.8f), MathF.Max(0.8f, U(1)), new Color(50, 44, 30), 0.8f);
        // A stencilled ID tag near the top bolt - every locker on the ward carries one.
        Rect(U(8), U(12.5f), MathF.Max(3f, U(4)), MathF.Max(1.6f, U(2)), new Color(200, 204, 210), 0.5f);
        for (var x = U(7); x < U(33); x++)
            Rect(x, U(36), 1, MathF.Max(1f, U(2)),
                (int)(x / MathF.Max(2f, U(3))) % 2 == 0 ? new Color(232, 190, 60) : new Color(30, 30, 34));
    }

    // Direct user request - the "производство" tab's own workbenches, previously all sharing the
    // same Generic plate. Same design canvas (40-unit reference, U(n)) every other face above uses -
    // the actual footprint's aspect ratio never reaches this baked art (it only ever fills a SQUARE
    // palette button; the on-canvas placed-device look comes from CustomDeviceCatalog's tint/glyph
    // instead, per DrawEditorDeviceAt), so nothing here needs to account for being 2x3/3x3/2x4.

    // Direct user request ("сделай текстуры этих 4 устройств ближе к текстурам из Баротравмы...
    // максимально проработанными") - Barotrauma's own machinery reads as heavy, worn submarine
    // equipment: muted materials scarred by use, a stencilled ID plate, hazard tape on anything
    // that pinches, and enough small hardware (pegboard tools, a drawer, cabling) that the eye
    // keeps finding new detail instead of resolving the shape in one glance. Every face below adds
    // exactly that layer, still through the same flat-fill primitives every other face uses - no
    // new drawing technique, just more passes.
    private void ConstructionBench(bool lit)
    {
        var accent = new Color(200, 150, 70);
        Housing(accent, 149);
        // Bench top: wood-grained planks, not a flat fill - the streaks are what sells "wood"
        // rather than "brown metal".
        Rect(U(6), U(21), U(28), MathF.Max(4f, U(6)), new Color(96, 74, 48));
        for (var y = U(21); y < U(27); y += MathF.Max(1f, U(1.4f)))
            Rect(U(6), y, U(28), 1, new Color(74, 56, 34), 0.35f);
        for (var i = 0; i < 14; i++)
        {
            var gx = U(7) + Hash(i, 149) * U(26);
            Rect(gx, U(21), MathF.Max(1f, U(1.2f)), U(6), new Color(60, 44, 26), 0.2f + Hash(i, 6) * 0.15f);
        }
        Rect(U(6), U(21), U(28), 1, new Color(170, 138, 92), 0.5f);
        Rect(U(6), U(27), U(28), 1, Color.Black, 0.45f);
        foreach (var lx in new[] { U(8), U(30) })
        {
            Rect(lx, U(27), MathF.Max(2f, U(3)), U(9), new Color(56, 60, 66));
            Rect(lx, U(27), MathF.Max(2f, U(3)), 1, new Color(120, 126, 136), 0.35f);
        }
        // A drawer under the bench, with a real handle and a keyhole - the kind of small hardware
        // that reads as "somebody's tools live here" rather than a prop.
        Rect(U(15), U(28.5f), U(10), MathF.Max(3f, U(5)), new Color(48, 52, 58));
        Rect(U(15), U(28.5f), U(10), 1, new Color(110, 116, 126), 0.4f);
        Rect(U(18.5f), U(30.5f), U(3), MathF.Max(1f, U(1.2f)), new Color(150, 156, 166));
        Disc(U(21.5f), U(30.7f), MathF.Max(0.6f, U(0.5f)), new Color(24, 22, 20), 0.7f);
        // Cast shadow the vice throws onto the bench, so it reads as sitting ON the surface.
        Rect(U(14), U(20.5f), U(13), MathF.Max(1f, U(1.4f)), Color.Black, 0.22f);
        // Vice: two jaws on a swivel base, cranked open around a held workpiece, with visible screw
        // threads on the crank shaft.
        Rect(U(15), U(13.5f), U(10), U(7.5f), new Color(74, 78, 86));
        Rect(U(15), U(13.5f), U(10), 1, new Color(178, 186, 198), 0.4f);
        Rect(U(15), U(20.5f), U(10), 1, Color.Black, 0.3f);
        Rect(U(17), U(9.5f), U(2), U(5.2f), new Color(56, 60, 66));
        Rect(U(23), U(9.5f), U(2), U(5.2f), new Color(56, 60, 66));
        for (var k = 0; k < 4; k++)
        {
            Px(U(18), U(10) + k * U(1.2f), new Color(30, 30, 34), 0.5f);
            Px(U(24), U(10) + k * U(1.2f), new Color(30, 30, 34), 0.5f);
        }
        Rect(U(18.5f), U(8.5f), MathF.Max(2f, U(3)), U(3.2f), new Color(150, 108, 60));
        Rect(U(18.5f), U(8.5f), MathF.Max(2f, U(3)), 1, new Color(206, 164, 100), 0.5f);
        Disc(U(20), U(8.5f), MathF.Max(1f, U(1.4f)), new Color(214, 172, 100));
        // Held workpiece peeking out of the jaws.
        Rect(U(17.5f), U(15.5f), U(5), MathF.Max(1f, U(2)), new Color(150, 156, 166));
        Rect(U(17.5f), U(15.5f), U(5), 1, new Color(220, 224, 230), 0.4f);
        // A full pegboard behind the bench: outline, holes, and four hung tools instead of two -
        // wrench, hammer, screwdriver, hand saw - each a distinct enough silhouette to read at a
        // glance, the same "identity through characteristic hardware" DeviceSkin's own doc comment
        // already argues for.
        Rect(U(6), U(4), U(28), MathF.Max(3f, U(5)), new Color(52, 54, 58));
        Rect(U(6), U(4), U(28), 1, new Color(100, 104, 112), 0.35f);
        for (var i = 0; i < 12; i++)
        {
            var hx = U(8) + i % 6 * U(4.6f);
            var hy = U(5) + i / 6 * U(3.4f);
            Disc(hx, hy, MathF.Max(0.5f, U(0.5f)), new Color(24, 24, 26), 0.5f);
        }
        // Wrench.
        Line(U(8), U(9), U(11.5f), U(15), new Color(150, 156, 166), 0.9f);
        Line(U(8.6f), U(9.6f), U(12.1f), U(15.6f), new Color(60, 62, 68), 0.4f);
        Disc(U(8), U(9), MathF.Max(1f, U(1.7f)), new Color(196, 202, 212));
        Ring(U(8), U(9), MathF.Max(1f, U(1.7f)), new Color(90, 94, 102), 0.6f, 0.8f);
        // Hammer.
        Rect(U(29), U(8), MathF.Max(1f, U(1.6f)), U(9), new Color(148, 106, 62));
        Rect(U(29), U(8), 1, U(9), new Color(196, 154, 96), 0.3f);
        Rect(U(26.5f), U(6.5f), MathF.Max(4f, U(6)), MathF.Max(1.5f, U(2.2f)), new Color(80, 84, 92));
        Rect(U(26.5f), U(6.5f), MathF.Max(4f, U(6)), 1, new Color(160, 166, 176), 0.4f);
        // Screwdriver.
        Rect(U(15), U(6.5f), MathF.Max(0.8f, U(1)), U(7), new Color(180, 184, 192));
        Rect(U(14.4f), U(4.5f), MathF.Max(2f, U(2.2f)), U(2.6f), new Color(214, 70, 50));
        // Hand saw - a triangular blade silhouette plus a handle, distinct from everything else here.
        Line(U(18), U(15), U(24), U(6), new Color(180, 184, 192), 0.85f);
        for (var t = 0f; t < 1f; t += 0.12f)
        {
            var sx = U(18) + (U(24) - U(18)) * t;
            var sy = U(15) + (U(6) - U(15)) * t;
            Px(sx, sy + U(0.6f), new Color(60, 62, 68), 0.5f);
        }
        Rect(U(22.4f), U(7.6f), MathF.Max(2f, U(2.4f)), MathF.Max(1.4f, U(1.6f)), new Color(90, 66, 40));
        // Hazard corner - Barotrauma marks anything with moving/pinching parts.
        for (var i = 0; i < 4; i++)
            Rect(U(6) + i * MathF.Max(1.4f, U(1.6f)), U(20.6f), MathF.Max(0.8f, U(0.9f)), MathF.Max(0.8f, U(0.9f)),
                i % 2 == 0 ? new Color(226, 170, 50) : new Color(24, 24, 26));
        Glass(U(9), U(33), U(12), MathF.Max(2f, U(3)), accent, lit);
    }

    private void Fabricator(bool lit)
    {
        var accent = new Color(140, 210, 230);
        Housing(accent, 151);
        // Side casing with riveted seams and a vent stack - the bulk of a real machine that just
        // happens to have a print bed on top, not a bare bed floating on the plate.
        Rect(U(7), U(9), U(4), U(24), new Color(64, 68, 76));
        Rect(U(31), U(9), U(4), U(24), new Color(64, 68, 76));
        for (var k = 0; k < 5; k++)
        {
            Disc(U(9), U(11) + k * U(4.4f), MathF.Max(0.6f, U(0.6f)), new Color(30, 32, 36), 0.6f);
            Disc(U(33), U(11) + k * U(4.4f), MathF.Max(0.6f, U(0.6f)), new Color(30, 32, 36), 0.6f);
        }
        for (var y = U(10); y < U(30); y += MathF.Max(2f, U(3)))
        {
            Rect(U(7.5f), y, U(3), MathF.Max(0.8f, U(1)), new Color(30, 34, 40), 0.5f);
            Rect(U(31.5f), y, U(3), MathF.Max(0.8f, U(1)), new Color(30, 34, 40), 0.5f);
        }
        // A glowing print bed - the one shape that reads as "makes new things appear" rather than
        // "stores" or "repairs" - with a real recessed lip and a warmer secondary glow bleeding out.
        Rect(U(9), U(24), U(22), MathF.Max(4f, U(7)), new Color(24, 26, 32));
        Rect(U(9), U(24), U(22), 1, new Color(90, 96, 106), 0.5f);
        Rect(U(11), U(25.5f), U(18), MathF.Max(2f, U(4)), lit ? new Color(120, 224, 240) : new Color(30, 46, 50), lit ? 0.85f : 1f);
        if (lit)
        {
            for (var x = U(11); x < U(29); x += MathF.Max(2f, U(3)))
                Px(x, U(26.5f), Color.White, 0.4f);
            Rect(U(9), U(30.5f), U(22), MathF.Max(1f, U(1.4f)), new Color(90, 200, 220), 0.25f);
        }
        // Gantry: uprights, a crossbeam, diagonal cross-bracing (the strut a real gantry needs to
        // not sway), and the nozzle head riding it on a visible carriage.
        foreach (var gx in new[] { U(10), U(30) })
        {
            Rect(gx, U(9), MathF.Max(2f, U(3)), U(16), new Color(78, 82, 90));
            Rect(gx, U(9), MathF.Max(2f, U(3)), 1, new Color(190, 196, 206), 0.35f);
        }
        Rect(U(10), U(9), U(23), MathF.Max(2f, U(3)), new Color(90, 94, 102));
        Rect(U(10), U(9), U(23), 1, new Color(196, 202, 212), 0.35f);
        Line(U(12), U(24), U(18), U(12), new Color(60, 64, 72), 0.6f);
        Line(U(30), U(24), U(24), U(12), new Color(60, 64, 72), 0.6f);
        Rect(U(18), U(11.5f), MathF.Max(3f, U(5)), U(6.5f), new Color(58, 62, 70));
        Rect(U(18), U(11.5f), MathF.Max(3f, U(5)), 1, new Color(196, 202, 212), 0.4f);
        Rect(U(19), U(18), MathF.Max(1.4f, U(2)), MathF.Max(1.4f, U(2)), new Color(30, 30, 34));
        Disc(U(20.5f), U(19), MathF.Max(1f, U(1.3f)), lit ? new Color(200, 250, 255) : new Color(60, 70, 74), 0.9f);
        // Status LEDs along the crossbeam - idle machinery has ONE thing still awake.
        for (var i = 0; i < 4; i++)
            Disc(U(13) + i * U(2.4f), U(10.5f), MathF.Max(0.5f, U(0.5f)),
                lit && i == 1 ? new Color(120, 240, 150) : new Color(50, 54, 60), 0.9f);
        // A striped warning plate on the casing - Barotrauma tags anything that moves fast.
        for (var i = 0; i < 5; i++)
            Rect(U(8), U(15) + i * MathF.Max(1f, U(1.2f)), U(2.4f), MathF.Max(0.7f, U(0.8f)),
                i % 2 == 0 ? new Color(226, 170, 50) : new Color(24, 24, 26));
        Glass(U(30), U(31), U(6), MathF.Max(2f, U(3)), accent, lit);
    }

    private void Deconstructor(bool lit)
    {
        var accent = new Color(220, 100, 60);
        Housing(accent, 157);
        // Scorch/gouge marks radiating from the opening - evidence the mouth is actually used, not
        // a clean prop. Drawn BEFORE the mouth itself so its own edge cleanly overlaps them.
        for (var i = 0; i < 16; i++)
        {
            var ang = Hash(i, 157) * MathF.PI * 2f;
            var r0 = U(9.5f) + Hash(i, 3) * U(2f);
            var r1 = r0 + U(2f) + Hash(i, 4) * U(3f);
            Line(U(20) + MathF.Cos(ang) * r0, U(19) + MathF.Sin(ang) * r0,
                U(20) + MathF.Cos(ang) * r1, U(19) + MathF.Sin(ang) * r1, new Color(50, 40, 34), 0.22f);
        }
        // A dark, toothed mouth over hazard banding - it takes things apart, so the housing itself
        // reads as something you keep your hands away from. Two-tone teeth (a lit edge on each) so
        // the ring reads as individual gnashing pieces rather than a dashed circle.
        Disc(U(20), U(19), U(11.4f), new Color(16, 14, 14));
        Disc(U(20), U(19), U(9.4f), new Color(34, 30, 28));
        for (var y = U(9.6f); y < U(28.4f); y += MathF.Max(0.7f, U(0.8f)))
            for (var x = U(9.6f); x < U(30.4f); x += MathF.Max(0.7f, U(0.8f)))
            {
                var d = MathF.Sqrt((x - U(20)) * (x - U(20)) + (y - U(19)) * (y - U(19)));
                if (d < U(9.4f))
                    Px(x, y, new Color(24, 20, 20), 0.06f + Hash((int)x, (int)y) * 0.06f);
            }
        for (var i = 0; i < 12; i++)
        {
            var ang = i * MathF.PI * 2f / 12f;
            var tx = U(20) + MathF.Cos(ang) * U(7.6f);
            var ty = U(19) + MathF.Sin(ang) * U(7.6f);
            Rect(tx - U(1), ty - U(1), MathF.Max(1.5f, U(2)), MathF.Max(1.5f, U(2)), new Color(148, 154, 164));
            Px(tx - U(0.6f), ty - U(0.6f), new Color(210, 214, 222), 0.5f);
            Px(tx + U(0.6f), ty + U(0.6f), Color.Black, 0.4f);
        }
        Disc(U(20), U(19), U(3.6f), new Color(30, 28, 26));
        Disc(U(20), U(19), U(3.0f), lit ? new Color(230, 120, 70) : new Color(40, 34, 30), 0.9f);
        Ring(U(20), U(19), U(9.8f), new Color(12, 10, 10), 0.85f, 1.3f);
        // A hydraulic feed arm on one flank, so something visibly PUSHES material toward the mouth.
        Rect(U(30), U(15), MathF.Max(3f, U(5)), MathF.Max(2f, U(3.4f)), new Color(58, 62, 70));
        Rect(U(30), U(15), MathF.Max(3f, U(5)), 1, new Color(150, 156, 166), 0.4f);
        Rect(U(34), U(15.4f), MathF.Max(3f, U(4)), MathF.Max(1.4f, U(2.6f)), new Color(74, 78, 86));
        Disc(U(30), U(16.7f), MathF.Max(1f, U(1.2f)), new Color(40, 42, 48));
        // A small riveted warning plate, and a pressure gauge - the hardware that says something
        // here is under load.
        Rect(U(6), U(23), MathF.Max(5f, U(7)), MathF.Max(3f, U(4.4f)), new Color(70, 66, 60));
        for (var i = 0; i < 3; i++)
            Rect(U(7), U(24) + i * MathF.Max(1f, U(1.2f)), MathF.Max(4f, U(5)), MathF.Max(0.6f, U(0.7f)),
                new Color(30, 28, 26), 0.6f);
        Disc(U(9), U(30), U(2.6f), new Color(24, 26, 30));
        Disc(U(9), U(30), U(2.1f), new Color(200, 200, 194));
        Line(U(9), U(30), U(9) + U(1.6f), U(30) - U(1.2f), new Color(180, 40, 36));
        var step = MathF.Max(2f, U(3));
        for (var x = U(8); x < U(32); x++)
            Rect(x, U(35), 1, MathF.Max(1f, U(2)),
                (int)(x / step) % 2 == 0 ? new Color(232, 140, 60) : new Color(28, 28, 32));
        Glass(U(30), U(31), U(9), MathF.Max(2f, U(3)), accent, lit);
    }

    private void WeaponWorkbench(bool lit)
    {
        var accent = new Color(170, 90, 80);
        Housing(accent, 163);
        // A long bench, scuffed matte metal rather than a flat fill - gun oil and range use leave
        // marks a workbench never quite scrubs out.
        Rect(U(6), U(19), U(28), MathF.Max(4f, U(6)), new Color(70, 62, 60));
        for (var i = 0; i < 10; i++)
        {
            var sx = U(7) + Hash(i, 163) * U(26);
            Rect(sx, U(19), MathF.Max(1f, U(1.4f)), U(6), new Color(50, 44, 42), 0.18f + Hash(i, 4) * 0.15f);
        }
        Rect(U(6), U(19), U(28), 1, new Color(150, 128, 122), 0.5f);
        Rect(U(6), U(25), U(28), 1, Color.Black, 0.45f);
        // Cast shadow under the vice cradle.
        Rect(U(10), U(18.5f), U(20), MathF.Max(1f, U(1.2f)), Color.Black, 0.2f);
        // The vice cradle and the barrel it's holding, plus a stock/receiver block so it reads as a
        // whole rifle being serviced, not a floating pipe.
        Rect(U(11), U(13.5f), U(4), U(6.5f), new Color(62, 66, 74));
        Rect(U(25), U(13.5f), U(4), U(6.5f), new Color(62, 66, 74));
        Rect(U(11), U(13.5f), U(4), 1, new Color(160, 166, 176), 0.4f);
        Rect(U(25), U(13.5f), U(4), 1, new Color(160, 166, 176), 0.4f);
        Rect(U(9), U(15), U(23), MathF.Max(2f, U(3)), new Color(42, 42, 46));
        Rect(U(9), U(15), U(23), 1, new Color(124, 128, 136), 0.4f);
        Rect(U(9), U(15.4f), MathF.Max(4f, U(6)), MathF.Max(1.6f, U(2.2f)), new Color(90, 62, 40));
        Disc(U(29.5f), U(16.5f), MathF.Max(1f, U(1.4f)), lit ? new Color(240, 120, 96) : new Color(60, 50, 48), 0.9f);
        Ring(U(29.5f), U(16.5f), MathF.Max(1f, U(1.4f)), new Color(24, 22, 22), 0.7f, 0.8f);
        // A cleaning rod and an oil can, resting on the bench beside the vice - the small hardware
        // that says this bench is used, not staged.
        Rect(U(13), U(22), MathF.Max(1f, U(0.9f)), U(14), new Color(150, 150, 150), 0.55f);
        Rect(U(11.5f), U(21.3f), MathF.Max(2f, U(2.4f)), MathF.Max(1f, U(1.2f)), new Color(90, 64, 40));
        Rect(U(20), U(22), MathF.Max(2.4f, U(3)), MathF.Max(3f, U(4)), new Color(58, 92, 70));
        Rect(U(20), U(22), MathF.Max(2.4f, U(3)), 1, new Color(120, 160, 132), 0.4f);
        Rect(U(20.6f), U(21), MathF.Max(0.8f, U(0.9f)), MathF.Max(1f, U(1.2f)), new Color(40, 40, 42));
        // Tool rack above the bench: a small screwdriver set and a bore brush.
        Rect(U(9), U(9), U(22), MathF.Max(2f, U(3)), new Color(50, 52, 58));
        Rect(U(9), U(9), U(22), 1, new Color(110, 114, 122), 0.35f);
        for (var i = 0; i < 4; i++)
        {
            var x = U(11) + i * U(3.2f);
            Rect(x, U(6), MathF.Max(0.8f, U(0.9f)), U(6), new Color(190, 194, 202));
            Rect(x - U(0.6f), U(5), MathF.Max(1.6f, U(2)), MathF.Max(1f, U(1.2f)),
                new Color(60 + i * 20, 50, 50));
        }
        Rect(U(27), U(6.5f), U(6), MathF.Max(1.2f, U(1.4f)), new Color(150, 150, 150), 0.6f);
        Rect(U(27), U(6.5f), MathF.Max(1.6f, U(2)), MathF.Max(1.2f, U(1.4f)), new Color(90, 64, 40));
        // Ammo rack: partitioned slots along the bottom, a couple filled, each with a stencilled
        // caliber tick instead of a plain block - the same "manufactured object carries a label"
        // instinct Housing's own serial tag already uses.
        for (var k = 0; k < 5; k++)
        {
            var x = U(8) + k * U(5.2f);
            Rect(x, U(27), MathF.Max(2f, U(4)), U(9), new Color(36, 34, 32));
            Rect(x, U(27), MathF.Max(2f, U(4)), 1, new Color(110, 100, 92), 0.4f);
            for (var s = 0; s < 2; s++)
                Rect(x + MathF.Max(0.4f, U(0.5f)) + s * MathF.Max(1f, U(1.2f)), U(28), MathF.Max(0.5f, U(0.6f)), MathF.Max(0.5f, U(0.6f)),
                    new Color(180, 176, 168), 0.5f);
            if (Hash(k, 9) > 0.35f)
            {
                Rect(x + U(1), U(29.5f), MathF.Max(1f, U(2)), U(4.5f), new Color(150, 118, 60));
                Rect(x + U(1), U(29.5f), MathF.Max(1f, U(2)), 1, new Color(206, 168, 100), 0.4f);
            }
        }
        Glass(U(9), U(33), U(12), MathF.Max(2f, U(3)), accent, lit);
    }

    // Direct user request ("сделай чтобы все турели занимали 3 на 3") - a real turret mount,
    // deliberately distinct from Weapons' capacitor-bank look (that's the CHARGING station, not
    // the gun itself): a rotating ring base with twin barrels, so a 3x3 turret reads as a weapon
    // the moment it's placed, not a bigger version of the charger icon.
    // Direct user request (screenshot: wall tiles arranged in the shape a turret mount should have -
    // "в сумме форм всех стен так должно выглядеть устройство") - the real gun/muzzle the player
    // actually sees rotate and fire lives entirely outside the hull (TurretMount.cs, ShipRenderer.
    // Devices.cs's own DrawTurret/TurretSkin - untouched by any of this); this is only the periscope
    // station's own EDITOR-CANVAS icon, now reshaped for its own real 1-wide x 3-tall mount column
    // (CustomDeviceFootprint.Size) instead of the old plain 3x3 square. Runs in absolute CANVAS space
    // (bypassing the usual U()-scaled/centered content square - Housing's own doc comment explains
    // the split) so the same three-part read - a scope/sight domed at the muzzle end, a wide
    // traverse ring at the crew's own periscope position, a mounting foot at the far end - fills the
    // real elongated shape instead of shrinking to a small centered logo. `vertical` picks which
    // canvas axis is the long (3-tile) one, so a Rotated instance (CustomDeviceFootprint.Size
    // swapped to 3 wide x 1 tall) still reads the same mount turned on its side, not stretched.
    private void Turret(bool lit)
    {
        var accent = new Color(190, 70, 60);
        Housing(accent, 173);

        var vertical = _canvasHeight >= _canvasWidth;
        var longSize = vertical ? _canvasHeight : _canvasWidth;
        var shortSize = vertical ? _canvasWidth : _canvasHeight;
        // Canvas-absolute coordinates - Px/Rect/Disc/Ring/Line all add _offsetX/_offsetY themselves
        // (the usual content-square centering), so this cancels that back out to a true canvas
        // position, the same trick Housing's own background pass above uses.
        (float X, float Y) At(float along, float across)
        {
            var a = along * longSize;
            var c = across * shortSize;
            return vertical ? (c - _offsetX, a - _offsetY) : (a - _offsetX, c - _offsetY);
        }
        (float W, float H) Size(float alongSpan, float acrossSpan) =>
            vertical ? (acrossSpan * shortSize, alongSpan * longSize) : (alongSpan * longSize, acrossSpan * shortSize);

        // The scope/sight dome at the muzzle end (canvas "along" 0) - a lit lens under a hooded
        // shade, the one detail that reads as "aims at something" even standing still.
        var (domeX, domeY) = At(0.14f, 0.5f);
        Disc(domeX, domeY, shortSize * 0.34f, new Color(46, 44, 46));
        Ring(domeX, domeY, shortSize * 0.34f, new Color(24, 22, 22), 0.8f, MathF.Max(1f, shortSize * 0.06f));
        Disc(domeX, domeY, shortSize * 0.18f, lit ? new Color(230, 90, 70) : new Color(60, 48, 46), 0.95f);

        // The traverse ring/collar, centered at "along" 0.5 - roughly where the mount's own middle
        // skirt row sits (TurretMountSkirt.cs), so the fixture's own widest point visually lines up
        // with the half-block armor plates flanking it.
        var (ringX, ringY) = At(0.5f, 0.5f);
        Ring(ringX, ringY, shortSize * 0.46f, new Color(40, 38, 40), 0.9f, shortSize * 0.09f);
        for (var i = 0; i < 6; i++)
        {
            var ang = i * MathF.PI / 3f;
            Disc(ringX + MathF.Cos(ang) * shortSize * 0.4f, ringY + MathF.Sin(ang) * shortSize * 0.4f,
                MathF.Max(1f, shortSize * 0.05f), new Color(30, 28, 28));
        }
        Disc(ringX, ringY, shortSize * 0.3f, new Color(70, 68, 70));
        Ring(ringX, ringY, shortSize * 0.3f, new Color(30, 28, 28), 0.7f, 1f);

        // The spine connecting scope to ring to the mounting foot - a single fixture, not three
        // floating shapes.
        var (spineX, spineY) = At(0.16f, 0.42f);
        var (spineW, spineH) = Size(0.68f, 0.16f);
        Rect(spineX, spineY, spineW, spineH, new Color(58, 60, 66), 0.85f);

        // The mounting foot at the far end - a small bolted plate, echoing the corner blocks the
        // skirt itself uses either side.
        var (footX, footY) = At(0.84f, 0.5f);
        var (footW, footH) = Size(0.22f, 0.8f);
        Rect(footX - footW / 2f, footY - footH / 2f, footW, footH, new Color(50, 52, 58));
        Rect(footX - footW / 2f, footY - footH / 2f, footW, 1, new Color(150, 158, 170), 0.4f);

        var (glassX, glassY) = At(0.9f, 0.2f);
        var (glassW, glassH) = Size(0.09f, 0.6f);
        Glass(glassX, glassY, MathF.Max(2f, glassW), MathF.Max(4f, glassH), accent, lit);
    }

    // Direct user request ("сделай чтобы кровать занимала 1 на 2 тайла") - a pillow, a folded
    // blanket, a footboard - a bed's own silhouette seen from above, oriented top-to-bottom so a
    // 1(wide)x2(tall) footprint reads correctly even before any stretch.
    private void Bed(bool lit)
    {
        var accent = new Color(150, 130, 180);
        Housing(accent, 179);
        Rect(U(6), U(8), U(28), U(29), new Color(74, 66, 58));
        Rect(U(6), U(8), U(28), 1, new Color(144, 130, 116), 0.4f);
        Rect(U(8), U(10), U(24), U(25), new Color(210, 202, 184));
        Rect(U(8), U(10), U(24), MathF.Max(3f, U(6)), new Color(230, 224, 208));
        Rect(U(8), U(10), U(24), 1, Color.White, 0.35f);
        for (var k = 0; k < 3; k++)
            Rect(U(8), U(19) + k * U(3.2f), U(24), 1, new Color(164, 152, 130), 0.4f);
        Rect(U(8), U(30), U(24), MathF.Max(2f, U(4)), new Color(140, 90, 96));
        Rect(U(8), U(30), U(24), 1, new Color(192, 142, 148), 0.35f);
        Glass(U(28), U(11), MathF.Max(3f, U(4)), MathF.Max(2f, U(3)), accent, lit);
    }

    // Direct user request ("ангары для шаттлов 5 на 6") - a big sliding double bay door with hazard
    // chevrons along the threshold, so a genuinely large 5x6 footprint reads as "something launches
    // from here" rather than a stretched storage icon.
    private void ShuttleHangar(bool lit)
    {
        var accent = new Color(150, 150, 180);
        Housing(accent, 181);
        Rect(U(4), U(6), U(15), U(28), new Color(64, 66, 74));
        Rect(U(21), U(6), U(15), U(28), new Color(64, 66, 74));
        Rect(U(4), U(6), U(15), 1, new Color(150, 156, 168), 0.4f);
        Rect(U(21), U(6), U(15), 1, new Color(150, 156, 168), 0.4f);
        Rect(U(19), U(6), MathF.Max(2f, U(3)), U(28), new Color(18, 18, 22));
        for (var k = 0; k < 6; k++)
        {
            Rect(U(4), U(8) + k * U(4.5f), U(15), 1, new Color(40, 42, 48), 0.5f);
            Rect(U(21), U(8) + k * U(4.5f), U(15), 1, new Color(40, 42, 48), 0.5f);
        }
        // Hazard chevrons along the bottom threshold.
        for (var i = 0; i < 8; i++)
        {
            var x = U(4) + i * U(4);
            Rect(x, U(35), U(3), MathF.Max(1f, U(2)), i % 2 == 0 ? new Color(232, 176, 60) : new Color(24, 24, 28));
        }
        Disc(U(6), U(9), MathF.Max(1f, U(1.4f)), lit ? new Color(120, 230, 150) : new Color(50, 60, 54), 0.9f);
        Disc(U(34), U(9), MathF.Max(1f, U(1.4f)), lit ? new Color(120, 230, 150) : new Color(50, 60, 54), 0.9f);
        Glass(U(15), U(9), U(10), MathF.Max(2f, U(3)), accent, lit);
    }

    // Direct user request ("тройная дверь... по аналогии как работают остальные устройства") - a
    // real 3-tile-span door needs real changes to CustomDoorDef/TileGrid.LinkDoors/TileShipBuilder
    // (CustomShipDefinition.cs's own doc comment on TripleDoor explains why), so this is the same
    // "placeable but cosmetic" workaround every other genuinely new device kind already uses,
    // themed to read as the exact same fixture as the real in-game door: same bronze frame, same
    // orange top-lit panel bands, same diagonal brace, same lit inspection glass (ShipRenderer.
    // Doors.cs's own DrawDoorFrame/DrawClosedDoorLeaf colors, reused here rather than invented).
    private void TripleDoor(bool lit)
    {
        var accent = new Color(200, 98, 60);
        Housing(accent, 233);

        var frame = new Color(90, 68, 46);
        var frameLit = new Color(140, 107, 74);
        Rect(U(3), U(3), U(34), U(3), frameLit);
        Rect(U(3), U(3), U(3), U(34), frame);
        Rect(U(34), U(3), U(3), U(34), frame);
        Rect(U(3), U(34), U(34), U(3), frame);

        var bands = new[] { new Color(224, 128, 80), new Color(200, 98, 60), new Color(168, 78, 48), new Color(136, 60, 36) };
        var bandHeight = U(7);
        for (var i = 0; i < bands.Length; i++)
            Rect(U(6), U(6) + i * bandHeight, U(28), bandHeight, bands[i]);

        Rect(U(19), U(6), U(2), U(28), new Color(110, 50, 32));
        Line(U(20), U(6), U(20), U(34), new Color(232, 138, 95), 0.6f);

        var brace = new Color(92, 44, 24);
        Line(U(9), U(31), U(17), U(9), brace, 0.85f);
        Line(U(23), U(9), U(31), U(31), brace, 0.85f);

        Glass(U(15), U(15), U(10), MathF.Max(2f, U(3)), accent, lit);
    }

    // Direct user request ("монитор состояния корабля... своя уникальная текстура") - a damage-
    // control board: a simplified top-down hull silhouette subdivided into status cells, the same
    // "one characteristic shape per machine" instinct Navigation's own scope dish follows, just
    // themed as a readout of the WHOLE ship rather than one instrument.
    private void ShipStatusMonitor(bool lit)
    {
        var accent = new Color(120, 220, 150);
        Housing(accent, 191);

        // The screen recess the hull diagram sits inside - a plain rect rather than Glass (used
        // below for the status strip instead), since the diagram itself has to paint over it.
        Rect(U(6), U(9), U(28), U(20), new Color(10, 16, 13));
        Rect(U(6), U(9), U(28), 1, new Color(150, 200, 165), 0.35f);

        // A top-down hull outline: nose narrow, midsection wide, tail narrower - the same reading
        // order a real tactical plot uses, legible even this small.
        var hull = new (float X, float Y)[] { (20, 10), (26, 14), (28, 22), (25, 27), (15, 27), (12, 22), (14, 14) };
        for (var i = 0; i < hull.Length; i++)
        {
            var (x0, y0) = hull[i];
            var (x1, y1) = hull[(i + 1) % hull.Length];
            Line(U(x0), U(y0), U(x1), U(y1), lit ? new Color(170, 235, 190) : new Color(60, 74, 64), lit ? 0.9f : 0.6f);
        }
        // Compartment grid inside the hull, each cell its own status colour - dead cells when the
        // ship can't power the board, same reasoning every other Glass/lit branch here uses.
        var cellColors = new[]
        {
            new Color(90, 200, 120), new Color(90, 200, 120), new Color(226, 176, 60),
            new Color(90, 200, 120), new Color(200, 80, 70), new Color(90, 200, 120),
        };
        var cells = new (float X, float Y)[] { (17, 16), (23, 16), (17, 20), (23, 20), (17, 24), (23, 24) };
        for (var i = 0; i < cells.Length; i++)
        {
            var (cx, cy) = cells[i];
            Rect(U(cx - 2.4f), U(cy - 1.6f), U(4.8f), U(3.2f), lit ? cellColors[i] : new Color(40, 46, 42), lit ? 0.85f : 1f);
        }
        Line(U(20), U(10), U(20), U(27), new Color(20, 30, 24), 0.4f);
        Line(U(12.5f), U(18), U(27.5f), U(18), new Color(20, 30, 24), 0.4f);
        Line(U(12.5f), U(22), U(27.5f), U(22), new Color(20, 30, 24), 0.4f);

        // A status-key row under the screen - the same 3 colours the cells above use, so the legend
        // reads as belonging to the diagram rather than as decoration.
        var keyColors = new[] { new Color(90, 200, 120), new Color(226, 176, 60), new Color(200, 80, 70) };
        for (var i = 0; i < 3; i++)
            Disc(U(11) + i * U(4), U(32), MathF.Max(0.9f, U(1.1f)), lit ? keyColors[i] : new Color(50, 54, 58), 0.9f);

        Glass(U(9), U(35.5f), U(22), MathF.Max(2f, U(3)), accent, lit);
    }

    // Direct user request ("консоль связи... своя уникальная текстура") - a parabolic dish on a
    // swivel mount with signal waves radiating off it, the one shape that reads as "talks to
    // something far away" the same instinct Navigation's own scope disc gives to "reads a bearing".
    private void CommsConsole(bool lit)
    {
        var accent = new Color(220, 170, 90);
        Housing(accent, 199);

        // Cast shadow the pedestal throws onto the plate.
        Rect(U(10), U(31.5f), U(20), MathF.Max(1f, U(1.2f)), Color.Black, 0.2f);

        // The dish itself: a shallow arc rather than a full disc - the same "silhouette over
        // detail" choice Navigation's own bezel makes, tilted to read as a receiver, not a porthole.
        for (var i = 0; i <= 40; i++)
        {
            var t = i / 40f;
            var ang = MathF.PI * (0.15f + t * 0.7f);
            Disc(U(20) + MathF.Cos(ang) * U(12), U(15) - MathF.Sin(ang) * U(7), MathF.Max(1f, U(1.3f)), new Color(180, 186, 196), 0.9f);
        }
        // Mesh grid across the dish's own face.
        for (var i = 1; i < 5; i++)
            Line(U(20) + (i - 2.5f) * U(4), U(9), U(20) + (i - 2.5f) * U(3), U(19), new Color(120, 126, 136), 0.35f);
        Line(U(9), U(14), U(31), U(14), new Color(120, 126, 136), 0.3f);
        // Feed horn at the dish's own focal point.
        Disc(U(20), U(14), U(1.8f), new Color(60, 64, 72));
        Disc(U(20), U(14), U(0.9f), lit ? new Color(255, 210, 130) : new Color(70, 62, 50), 0.9f);
        // Signal waves radiating off the feed horn - the one detail that says "transmitting" rather
        // than just "shaped like a dish"; dead when the ship can't power it, same reasoning
        // Weapons' own spark gap gets.
        if (lit)
            foreach (var r in new[] { 3f, 5.5f, 8f })
                Ring(U(20), U(6), U(r), new Color(255, 210, 130), 0.35f - r * 0.02f, 1f);
        // Swivel mount and pedestal.
        Rect(U(18), U(19), MathF.Max(2f, U(4)), U(8), new Color(70, 74, 82));
        Rect(U(18), U(19), MathF.Max(2f, U(4)), 1, new Color(180, 186, 196), 0.4f);
        Rect(U(11), U(27), U(18), MathF.Max(2f, U(4)), new Color(58, 62, 70));
        Rect(U(11), U(27), U(18), 1, new Color(150, 158, 170), 0.4f);
        Disc(U(20), U(29), U(2.2f), new Color(44, 48, 54));
        // A frequency-readout strip - the console's own tuning display.
        Glass(U(9), U(33), U(22), MathF.Max(2f, U(4)), accent, lit);
    }

    // Direct user request ("сделай щитку свою собственную текстуру") - a single breaker/relay box,
    // deliberately smaller and plainer than Distribution's own full multi-breaker panel (this is the
    // one-off fixture scattered through a "Щитовая" room's interior, not the room's own main hub) -
    // one toggle lever, one status light, a couple of mounting screws.
    private void Junction(bool lit)
    {
        var accent = new Color(210, 200, 80);
        Housing(accent, 233);

        // Cast shadow the breaker throws onto the plate.
        Rect(U(10), U(25.5f), U(20), MathF.Max(1f, U(1.1f)), Color.Black, 0.18f);

        // The breaker's own recessed mounting plate.
        Rect(U(10), U(11), U(20), U(16), new Color(46, 50, 58));
        Rect(U(10), U(11), U(20), 1, new Color(150, 158, 170), 0.4f);
        // Two mounting screws, top corners of the plate.
        foreach (var sx in new[] { U(12.5f), U(27.5f) })
        {
            Disc(sx, U(13), MathF.Max(0.8f, U(1.1f)), new Color(70, 74, 82));
            Line(sx - U(0.6f), U(13), sx + U(0.6f), U(13), new Color(40, 42, 46), 0.6f);
        }

        // The lever itself, thrown up (on) or down (off) - the one moving-looking part on a fixture
        // this small, same reasoning Distribution's own up/down breaker row uses.
        var throwUp = lit;
        Rect(U(18), U(16), MathF.Max(2f, U(4)), U(2), new Color(40, 42, 46));
        Rect(U(18.5f), throwUp ? U(13) : U(19), MathF.Max(1.4f, U(3)), U(6), new Color(60, 64, 72));
        Rect(U(18.5f), throwUp ? U(13) : U(19), MathF.Max(1.4f, U(3)), 1, new Color(150, 158, 170), 0.5f);
        Disc(U(20), throwUp ? U(13) : U(19), MathF.Max(1f, U(1.4f)), new Color(210, 214, 220), 0.6f);

        // Status light, the same lit/dead convention every other panel here uses.
        Disc(U(25.5f), U(16), MathF.Max(0.9f, U(1.2f)), lit ? new Color(120, 240, 150) : new Color(52, 56, 62), 0.95f);

        // A short stencilled warning stripe along the plate's own bottom edge - the same tag
        // Distribution's own breaker row carries, just one row instead of a full strip.
        for (var i = 0; i < 6; i++)
            Rect(U(11) + i * MathF.Max(1.6f, U(2.8f)), U(24), MathF.Max(1.2f, U(1.8f)), MathF.Max(0.8f, U(1)),
                i % 2 == 0 ? new Color(226, 194, 40) : new Color(24, 24, 26), 0.5f);

        Glass(U(11), U(29), U(18), MathF.Max(2f, U(4)), accent, lit);
    }
}
