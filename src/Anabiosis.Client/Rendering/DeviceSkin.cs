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
public sealed partial class DeviceSkin : IDisposable
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
        // Direct user request - the turret's periscope station (DeviceSkin.Periscope.cs).
        Periscope,
        // Direct user request ("обнови текстуры для всех устройств") - free-standing furniture and
        // floor kit, drawn on a transparent canvas (DeviceSkin.Furniture.cs).
        Table, Chair, Sofa, Nightstand, Plant, FloorLamp, Spotlight, CardTable, AmmoCrates,
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
        if (_rotated)
            (fx, fy) = (fy, _canvasHeight - 1 - fx); // PortraitBody's local portrait space onto a landscape canvas
        int x = (int)MathF.Round(fx) + _offsetX, y = (int)MathF.Round(fy) + _offsetY;
        if (a <= 0f || x < 0 || y < 0 || x >= _canvasWidth || y >= _canvasHeight || !Inside(x, y))
            return;
        var d = _buffer[y * _canvasWidth + x];
        // Premultiplied "over": over an opaque plate this is exactly the old plain lerp and stays
        // opaque; over the transparent canvas a free-standing furniture face starts from
        // (DeviceSkin.Furniture.cs) it leaves a correctly premultiplied, partly transparent pixel -
        // a soft shadow stays a shadow instead of turning solid black.
        a = MathF.Min(a, 1f);
        _buffer[y * _canvasWidth + x] = new Color(
            (int)(c.R * a + d.R * (1f - a)), (int)(c.G * a + d.G * (1f - a)), (int)(c.B * a + d.B * (1f - a)),
            (int)(255 * a + d.A * (1f - a)));
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
        _rotated = false;

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
            case Face.Periscope: Periscope(lit); break;
            case Face.Table: Table(lit); break;
            case Face.Chair: Chair(lit); break;
            case Face.Sofa: Sofa(lit); break;
            case Face.Nightstand: Nightstand(lit); break;
            case Face.Plant: Plant(lit); break;
            case Face.FloorLamp: FloorLamp(lit); break;
            case Face.Spotlight: Spotlight(lit); break;
            case Face.CardTable: CardTable(lit); break;
            case Face.AmmoCrates: AmmoCrates(lit); break;
            default: Housing(new Color(140, 148, 160), 97); break;
        }

        var texture = new Texture2D(_graphics, _canvasWidth, _canvasHeight);
        texture.SetData(_buffer);
        _buffer = Array.Empty<Color>();
        return texture;
    }

    private float U(float atForty) => atForty * _u;

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
        // skirt row sits (CustomDeviceFootprint.ShapedFootprint), so the fixture's own widest point visually lines up
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

}
