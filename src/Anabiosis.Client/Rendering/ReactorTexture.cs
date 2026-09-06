using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// A procedural reactor face, same "no reliable art, bake it in code instead" idea TileTextures/
// HullSkin already use for the hull and floor. The layout follows a Barotrauma reference the user
// supplied - chamfered top corners, ribbed side vents, two hooked pipes feeding down from a
// turbine grille into a large inspection screen, a row of gauge lights, a cluttered base with
// gears and valve wheels - baked as flat, hard-edged, posterized zones (deliberately "pixel art"
// rather than a smooth photographic render) in a dimmed steel/teal palette rather than a bright or
// rusty one. ShipRenderer.DrawReactorBlock still tints the whole face gray when off.
public static class ReactorTexture
{
    // 192 - enough room for real detail (rivets, vent fins, gear teeth) while every shape is still
    // built from hard flat zones rather than smooth gradients, so it reads as pixel art, not a render.
    public const int Size = 192;
    private const float Scale = Size / 128f;

    public static Texture2D Create(GraphicsDevice device)
    {
        var texture = new Texture2D(device, Size, Size);
        var data = new Color[Size * Size];
        for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
                data[y * Size + x] = PixelAt(x, y);
        texture.SetData(data);
        return texture;
    }

    // A muted, dimmed steel/teal palette - toned down from an earlier brighter pass so nothing on
    // the face reads as a light source except the small deliberate glow accents below.
    private static readonly Color BaseMetal = new(120, 128, 138);
    private static readonly Color BaseMetalDark = new(84, 91, 101);
    private static readonly Color DarkMetal = new(22, 25, 31);
    private static readonly Color PipeColor = new(36, 41, 49);
    private static readonly Color CollarColor = new(148, 155, 163);
    private static readonly Color ScreenGlass = new(14, 34, 43);
    private static readonly Color Highlight = new(188, 195, 203);
    private static readonly Color Shadow = new(5, 6, 7);
    private static readonly Color Accent = new(52, 150, 162);
    private static readonly Color WarningYellow = new(176, 150, 50);
    private static readonly Color WarningDark = new(34, 28, 8);
    private static readonly Color TrefoilColor = new(184, 158, 52);

    // Fixed rivets scattered across the main plating so it still reads as bolted sheet rather than
    // a smooth casting, plus a seam row marking where the top assembly meets the base clutter.
    private static readonly (float X, float Y)[] PanelRivets =
    {
        (34 * Scale, 20 * Scale), (94 * Scale, 20 * Scale), (34 * Scale, 92 * Scale), (94 * Scale, 92 * Scale), (64 * Scale, 96 * Scale),
        (64 * Scale, 16 * Scale), (18 * Scale, 60 * Scale), (110 * Scale, 60 * Scale),
    };

    private readonly record struct Segment(float Ax, float Ay, float Bx, float By);

    // The two pipes hooking down from the top edge into the screen's top corners, and the two
    // stubs continuing from the screen's bottom down into the base clutter - all as capsule paths
    // (distance-to-segment) rather than traced curves, which is enough to read as bent pipe at
    // this resolution and is far simpler than an actual arc.
    private static readonly Segment[] LeftPipe =
        { new(46 * Scale, -2 * Scale, 46 * Scale, 26 * Scale), new(46 * Scale, 26 * Scale, 42 * Scale, 38 * Scale), new(42 * Scale, 38 * Scale, 41 * Scale, 46 * Scale) };
    private static readonly Segment[] RightPipe =
        { new(Size - 46 * Scale, -2 * Scale, Size - 46 * Scale, 26 * Scale), new(Size - 46 * Scale, 26 * Scale, Size - 42 * Scale, 38 * Scale), new(Size - 42 * Scale, 38 * Scale, Size - 41 * Scale, 46 * Scale) };
    private static readonly Segment[] LeftStub = { new(54 * Scale, 84 * Scale, 54 * Scale, 118 * Scale) };
    private static readonly Segment[] RightStub = { new(Size - 54 * Scale, 84 * Scale, Size - 54 * Scale, 118 * Scale) };
    private const float PipeRadius = 7f * Scale;
    private const float StubRadius = 4f * Scale;

    // Bolted collar rings where a pipe meets the top edge or the screen - center X/Y, outer/inner
    // radius - kept well clear of the fan roundel (TryFan) so the three don't merge into one blob.
    private static readonly (float X, float Y, float R)[] Flanges =
        { (46 * Scale, 6 * Scale, 9 * Scale), (Size - 46 * Scale, 6 * Scale, 9 * Scale), (42 * Scale, 44 * Scale, 8 * Scale), (Size - 42 * Scale, 44 * Scale, 8 * Scale) };

    private static readonly float[] DialX = { 48f * Scale, 58f * Scale, 68f * Scale, 78f * Scale };
    private static readonly Color[] DialColors = { Accent, new(180, 132, 48), new(88, 168, 96), Accent };

    private const int ScreenLeft = 60, ScreenRight = 132, ScreenTop = 63, ScreenBottom = 126, ScreenFrame = 6;
    private const int Chamfer = 27;

    private static Color PixelAt(int x, int y)
    {
        if (!Inside(x, y)) return Color.Transparent;

        var color = InVent(x, y) ? VentPixel(x, y)
            : TryBottom(x, y, out var bottomColor) ? bottomColor
            : PanelPixel(x, y);

        if (TryGear(x, y, out var gearColor)) color = gearColor;
        if (TryCable(x, y, out var cableColor)) color = cableColor;
        if (TryPipe(x, y, LeftStub, StubRadius, out var stubShadeL)) color = Shade(PipeColor, stubShadeL);
        if (TryPipe(x, y, RightStub, StubRadius, out var stubShadeR)) color = Shade(PipeColor, stubShadeR);
        if (TryPipe(x, y, LeftPipe, PipeRadius, out var pipeShadeL)) color = Shade(PipeColor, pipeShadeL);
        if (TryPipe(x, y, RightPipe, PipeRadius, out var pipeShadeR)) color = Shade(PipeColor, pipeShadeR);
        if (TryValve(x, y, out var valveColor)) color = valveColor;
        if (TryFlange(x, y, out var flangeColor)) color = flangeColor;
        if (TryFan(x, y, out var fanColor)) color = fanColor;
        if (TryScreen(x, y, out var screenColor)) color = screenColor;
        if (TryLabel(x, y, out var labelColor)) color = labelColor;
        if (TryDial(x, y, out var dialColor)) color = dialColor;

        // A single hard-pixel outline around the whole silhouette - a flat 1px ring rather than a
        // soft blended one, the way a clean pixel-art sprite is inked rather than anti-aliased.
        if (EdgeDistance(x, y) < 1f) color = Shadow;

        // Crushing the final colour down to a handful of levels per channel is what actually reads
        // as "pixel art" rather than "flat-shaded vector" - it turns every smooth blend above into
        // visible banding, the same limited-palette look a real retro sprite is stuck with.
        return Posterize(color);
    }

    private static Color Posterize(Color c)
    {
        const float levels = 10f;
        return new Color(
            (byte)(MathF.Round(c.R / 255f * (levels - 1)) / (levels - 1) * 255f),
            (byte)(MathF.Round(c.G / 255f * (levels - 1)) / (levels - 1) * 255f),
            (byte)(MathF.Round(c.B / 255f * (levels - 1)) / (levels - 1) * 255f),
            c.A);
    }

    // A thin cable arcing over the top between the two vents, behind the pipes/fan - a small extra
    // bit of machinery detail in what would otherwise be empty background above the chamfer.
    private static bool TryCable(int x, int y, out Color color)
    {
        var t = MathHelper.Clamp((x - 22f * Scale) / (Size - 44f * Scale), 0f, 1f);
        var sag = 6f * Scale * 4f * t * (1f - t);
        var cableY = 3f * Scale + sag;
        if (x < 22 * Scale || x > Size - 22 * Scale || MathF.Abs(y - cableY) > 1.6f * Scale)
        {
            color = default;
            return false;
        }
        color = Shade(DarkMetal, y < cableY ? 0.12f : -0.12f);
        return true;
    }

    // Chamfered top-left/top-right corners, same boxy silhouette the reference has - everywhere
    // else stays a plain rectangle, filling the block edge to edge.
    private static bool Inside(int x, int y) => x + y >= Chamfer && Size - 1 - x + y >= Chamfer;

    private static float EdgeDistance(int x, int y)
    {
        var straight = MathF.Min(MathF.Min(x, Size - 1 - x), MathF.Min(y, Size - 1 - y));
        var chamferTl = (x + y - Chamfer) * 0.7071f;
        var chamferTr = (Size - 1 - x + y - Chamfer) * 0.7071f;
        return MathF.Min(straight, MathF.Min(chamferTl, chamferTr));
    }

    private const int VentTop = 12;
    private const int VentBottom = 153;
    private const int LeftVentStart = 6, LeftVentEnd = 45;
    private const int RightVentStart = Size - 45, RightVentEnd = Size - 6;
    private const int VentCell = 5;

    private static bool InVent(int x, int y) =>
        y >= VentTop && y < VentBottom && (x >= LeftVentStart && x < LeftVentEnd || x >= RightVentStart && x < RightVentEnd);

    // A cooling grille - narrow vertical fins (a bright leading edge, a dark body), flat and clean
    // rather than weathered, with a thin glowing conduit stripe through the middle standing in for
    // an active power line rather than a stain.
    private static Color VentPixel(int x, int y)
    {
        var onLeft = x < Size / 2;
        var localX = onLeft ? x - LeftVentStart : x - RightVentStart;
        var withinCell = localX % VentCell;
        var shade = withinCell == 0 ? 0.16f : withinCell == 1 ? -0.05f : -0.18f;
        if (y < VentTop + 4 * Scale || y >= VentBottom - 4 * Scale) shade -= 0.10f;

        var color = Shade(DarkMetal, shade);

        var midY = (VentTop + VentBottom) / 2f;
        if (MathF.Abs(y - midY) < 1.2f * Scale)
            color = Color.Lerp(color, Accent, 0.5f);

        return color;
    }

    // The flat plating everywhere that isn't a vent, pipe or screen - clean steel with a simple
    // top-lit gradient and a scatter of structural rivets, no weathering noise.
    private static Color PanelPixel(int x, int y)
    {
        var lightGrad = MathHelper.Lerp(0.06f, -0.10f, y / (float)Size);
        var color = Shade(BaseMetal, lightGrad);

        foreach (var (rx, ry) in PanelRivets)
            color = Shade(color, Rivet(x - rx, y - ry, 2.2f * Scale));

        return color;
    }

    private const int BottomTop = 144;
    private const int BaseRailTop = Size - 9;
    private const int LeftTankStart = 12, LeftTankEnd = 45;
    private const int RightTankStart = Size - 45, RightTankEnd = Size - 12;
    private const int TankWidth = LeftTankEnd - LeftTankStart;

    // The cluttered base the machine sits on: two blocky tanks flanking a dark tangle of unnamed
    // pipework, and a solid contact rail along the very bottom edge.
    private static bool TryBottom(int x, int y, out Color color)
    {
        if (y < BottomTop) { color = default; return false; }

        if (y >= BaseRailTop)
        {
            // A hazard-tape contact rail instead of a flat bar - a cheap extra bit of visual
            // interest along the one edge that's otherwise a plain stripe across the whole width.
            var stripe = (int)(x / (5 * Scale)) % 2 == 0;
            color = stripe ? Shade(WarningYellow, -0.20f) : Shadow;
            return true;
        }

        var onLeft = x is >= LeftTankStart and < LeftTankEnd;
        var onRight = x is >= RightTankStart and < RightTankEnd;
        if (onLeft || onRight)
        {
            var localX = onLeft ? x - LeftTankStart : x - RightTankStart;
            var shade = localX == 0 || localX == TankWidth - 1 ? 0.14f : 0f;
            shade += Rivet(x - (onLeft ? LeftTankStart + 6f * Scale : RightTankStart + 10f * Scale), y - (BottomTop + 8f * Scale), 2f * Scale);
            color = Shade(BaseMetalDark, shade);
            return true;
        }

        color = Shade(DarkMetal, -0.05f);
        return true;
    }

    // Cylindrical shading along a capsule (distance-to-segment) path - lit on one flank, shadowed
    // on the other, with a dark outline right at the pipe's own edge.
    private static bool TryPipe(float x, float y, Segment[] segments, float radius, out float shade)
    {
        var minDist = float.MaxValue;
        var bestSide = 0f;
        foreach (var s in segments)
        {
            var d = DistanceToSegment(x, y, s.Ax, s.Ay, s.Bx, s.By);
            if (d < minDist) { minDist = d; bestSide = SideOfSegment(x, y, s.Ax, s.Ay, s.Bx, s.By); }
        }
        if (minDist >= radius) { shade = 0f; return false; }

        shade = -MathHelper.Clamp(bestSide / radius, -1f, 1f) * 0.30f;
        if (minDist > radius * 0.88f) shade -= 0.20f;
        return true;
    }

    // The bolted collar where a pipe meets the top edge or the screen - a ring rather than a full
    // disc, so the darker pipe still shows through its hole instead of the collar swallowing it,
    // with a rim highlight and a ring of small rivets standing in for the actual union bolts.
    private static bool TryFlange(float x, float y, out Color color)
    {
        foreach (var (fx, fy, fr) in Flanges)
        {
            var dx = x - fx;
            var dy = y - fy;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > fr || dist < fr * 0.5f) continue;

            var shade = 0f;
            if (dist >= fr - 1.5f * Scale) shade += 0.18f;
            if (dist <= fr * 0.5f + 1.5f * Scale) shade -= 0.14f;

            const int rivets = 6;
            for (var i = 0; i < rivets; i++)
            {
                var angle = i * MathHelper.TwoPi / rivets;
                shade += Rivet(dx - MathF.Cos(angle) * fr * 0.78f, dy - MathF.Sin(angle) * fr * 0.78f, 1.6f * Scale);
            }

            color = Shade(CollarColor, shade);
            return true;
        }
        color = default;
        return false;
    }

    // A small spoked valve wheel sitting on each pipe at its bend - the "working part" a real pipe
    // run would have where a section could be shut off, rather than a plain uninterrupted tube.
    private static readonly (float X, float Y)[] Valves = { (44 * Scale, 31 * Scale), (Size - 44 * Scale, 31 * Scale) };

    private static bool TryValve(float x, float y, out Color color)
    {
        foreach (var (vx, vy) in Valves)
        {
            var dx = x - vx;
            var dy = y - vy;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            var outerR = 5f * Scale;
            var hubR = 1.8f * Scale;
            if (dist > outerR) continue;

            var onRim = dist >= outerR - 1.2f * Scale;
            var onHub = dist <= hubR;
            const int spokes = 5;
            var sector = MathHelper.TwoPi / spokes;
            var mod = AngleOf(dx, dy) % sector;
            var onSpoke = MathF.Min(mod, sector - mod) < 0.5f;

            if (!onRim && !onHub && !onSpoke) { color = default; return false; }

            color = Shade(CollarColor, onRim ? 0.16f : onHub ? -0.12f : 0.03f);
            return true;
        }
        color = default;
        return false;
    }

    // A gear pair set into each base tank - a small one meshed against a larger one, the clearest
    // "machine with moving parts" cue at a glance, even baked static, alongside the valve wheels
    // and the fan's own blades.
    private static readonly (float X, float Y, float BaseR, float ToothR, float HubR)[] Gears =
    {
        (19 * Scale, 108 * Scale, 5.5f * Scale, 7f * Scale, 1.6f * Scale),
        (Size - 19 * Scale, 108 * Scale, 5.5f * Scale, 7f * Scale, 1.6f * Scale),
        (28 * Scale, 100 * Scale, 3.2f * Scale, 4.4f * Scale, 1f * Scale),
        (Size - 28 * Scale, 100 * Scale, 3.2f * Scale, 4.4f * Scale, 1f * Scale),
    };

    private static bool TryGear(float x, float y, out Color color)
    {
        foreach (var (gx, gy, baseR, toothR, hubR) in Gears)
        {
            var dx = x - gx;
            var dy = y - gy;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            const int teeth = 8;
            var sector = MathHelper.TwoPi / teeth;
            var mod = AngleOf(dx, dy) % sector;
            var inTooth = MathF.Min(mod, sector - mod) < sector * 0.28f;
            var edgeR = inTooth ? toothR : baseR;
            if (dist > edgeR) continue;

            color = Shade(CollarColor, dist >= edgeR - 1f * Scale ? 0.14f : dist <= hubR ? -0.16f : -0.04f);
            return true;
        }
        color = default;
        return false;
    }

    // A small turbine-intake grille at top-center, between where the two pipes emerge, with a lit
    // hub standing in for an active core/status light instead of dead metal.
    private static bool TryFan(float x, float y, out Color color)
    {
        var cx = Size / 2f;
        var cy = 10f * Scale;
        var r = 8f * Scale;
        var dx = x - cx;
        var dy = y - cy;
        var dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist > r) { color = default; return false; }

        if (dist < r * 0.16f) { color = Accent; return true; }

        var shade = dist >= r - 1.4f * Scale ? 0.20f : 0f;
        const int blades = 7;
        var sector = MathHelper.TwoPi / blades;
        var mod = AngleOf(dx, dy) % sector;
        var distFromBoundary = MathF.Min(mod, sector - mod) / sector;
        if (distFromBoundary < 0.12f) shade -= 0.18f;
        else if (distFromBoundary > 0.30f) shade += 0.05f;

        color = Shade(DarkMetal, shade);
        return true;
    }

    // A large inspection window - flat tech-teal glass with faint scanlines rather than a noisy
    // photographic surface, and a radiation trefoil stencilled in the middle so the whole face
    // reads as a nuclear reactor rather than just an industrial machine.
    private static bool TryScreen(int x, int y, out Color color)
    {
        if (x < ScreenLeft - ScreenFrame || x >= ScreenRight + ScreenFrame || y < ScreenTop - ScreenFrame || y >= ScreenBottom + ScreenFrame)
        {
            color = default;
            return false;
        }

        var inGlass = x >= ScreenLeft && x < ScreenRight && y >= ScreenTop && y < ScreenBottom;
        if (!inGlass)
        {
            color = Shade(DarkMetal, x < ScreenLeft + Scale || y < ScreenTop + Scale ? 0.12f : -0.10f);
            return true;
        }

        var edgeDist = MathF.Min(MathF.Min(x - ScreenLeft, ScreenRight - 1 - x), MathF.Min(y - ScreenTop, ScreenBottom - 1 - y));
        var shade = edgeDist < 3 * Scale ? -0.10f : y % 3 == 0 ? -0.05f : 0f;
        var glassColor = Shade(ScreenGlass, shade);

        var tdx = x - (ScreenLeft + ScreenRight) / 2f;
        var tdy = y - (ScreenTop + ScreenBottom) / 2f;
        if (TrefoilMask(tdx, tdy, (ScreenBottom - ScreenTop) * 0.44f) > 0f)
            glassColor = Color.Lerp(glassColor, TrefoilColor, 0.55f);

        color = glassColor;
        return true;
    }

    // The classic three-bladed radiation symbol - a solid hub, then 3 wedges out of every 6
    // 60-degree sectors lit, the rest left as background - simple enough to read cleanly at this
    // resolution instead of trying to trace the symbol's real curved wedge outlines.
    private static float TrefoilMask(float dx, float dy, float outerR)
    {
        var dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist <= outerR * 0.20f) return 1f;
        if (dist < outerR * 0.32f || dist > outerR) return 0f;
        var sector = (int)(AngleOf(dx, dy) / (MathF.PI / 3f)) % 6;
        return sector % 2 == 0 ? 1f : 0f;
    }

    private const int LabelBandTop = 87, LabelBandBottom = 99;
    private const int LabelWidth = 12, LabelGap = 2;

    // Two small hazard stickers flanking the screen, illegible printed text stood in for by a
    // scatter of dark flecks over the yellow field.
    private static bool TryLabel(int x, int y, out Color color)
    {
        if (y >= LabelBandTop && y < LabelBandBottom)
        {
            var leftLabelStart = ScreenLeft - ScreenFrame - LabelGap - LabelWidth;
            var leftLabelEnd = ScreenLeft - ScreenFrame - LabelGap;
            if (x >= leftLabelStart && x < leftLabelEnd)
            {
                color = LabelPixel(x - leftLabelStart, y - LabelBandTop);
                return true;
            }
            var rightLabelStart = ScreenRight + ScreenFrame + LabelGap;
            var rightLabelEnd = rightLabelStart + LabelWidth;
            if (x >= rightLabelStart && x < rightLabelEnd)
            {
                color = LabelPixel(x - rightLabelStart, y - LabelBandTop);
                return true;
            }
        }
        color = default;
        return false;
    }

    private static Color LabelPixel(int lx, int ly) => Hash(lx, ly, 71) > 0.72f ? WarningDark : WarningYellow;

    private const int DialBandTop = 129, DialBandBottom = 141;

    // A row of small glowing status lights under the screen instead of plain dark bolts, each a
    // different colour rather than a uniform strip of identical dots - the clearest "this is
    // active technology" cue on the whole face besides the vent conduits.
    private static bool TryDial(int x, int y, out Color color)
    {
        if (y < DialBandTop || y >= DialBandBottom) { color = default; return false; }
        for (var i = 0; i < DialX.Length; i++)
        {
            var dx = x - DialX[i];
            var dy = y - 90f * Scale;
            var dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist < 1.3f * Scale) { color = DialColors[i]; return true; }
            if (dist < 3f * Scale)
            {
                color = Shade(DarkMetal, 0.12f);
                return true;
            }
        }
        color = default;
        return false;
    }

    private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
    {
        var abx = bx - ax;
        var aby = by - ay;
        var lenSq = abx * abx + aby * aby;
        var t = lenSq > 0f ? MathHelper.Clamp(((px - ax) * abx + (py - ay) * aby) / lenSq, 0f, 1f) : 0f;
        var dx = px - (ax + abx * t);
        var dy = py - (ay + aby * t);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    // Signed distance to the segment's own infinite line - which flank of the pipe a pixel falls
    // on, so TryPipe can light one side and shadow the other like a real cylinder.
    private static float SideOfSegment(float px, float py, float ax, float ay, float bx, float by)
    {
        var abx = bx - ax;
        var aby = by - ay;
        var len = MathF.Sqrt(abx * abx + aby * aby);
        if (len <= 0f) return 0f;
        return (abx * (py - ay) - aby * (px - ax)) / len;
    }

    private static float AngleOf(float dx, float dy)
    {
        var angle = MathF.Atan2(dy, dx);
        return angle < 0 ? angle + MathHelper.TwoPi : angle;
    }

    // Same lit/shadow blend HullSkin/TileTextures use for every other metal surface in the game.
    private static Color Shade(Color baseTone, float shade)
    {
        var t = MathHelper.Clamp(0.5f + shade * 2.2f, 0f, 1f);
        return t > 0.5f ? Color.Lerp(baseTone, Highlight, (t - 0.5f) * 2f) : Color.Lerp(Shadow, baseTone, t * 2f);
    }

    private static float Rivet(float dx, float dy, float rad)
    {
        var distance = MathF.Sqrt(dx * dx + dy * dy);
        if (distance > rad) return 0f;
        var lit = MathHelper.Clamp((-dx - dy) / (rad * 1.6f), -0.14f, 0.14f);
        return distance > rad * 0.65f ? lit * 0.4f - 0.1f : lit;
    }

    private static float Hash(int x, int y, int seed)
    {
        var n = x * 374761393 + y * 668265263 + seed * 362437;
        n = (n ^ (n >> 13)) * 1274126177;
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }
}
