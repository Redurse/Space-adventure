using System;
using Microsoft.Xna.Framework;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// Raw goods and refined materials, in the painted, gritty manner of the user's reference sheets
// (direct user request - style reference only, the art here is our own): ore as a heap of
// fractured, faceted lumps carrying flecks of the mineral, tagged with a riveted plate stamped
// with its element symbol; plates and ingots as worn, edge-polished slabs with a stamped mark;
// cable on a steel spool; plastic as pellets in a labelled jar; hyperium sealed in a glass vial.
// Baked supersampled (ItemSkin.Get) so the facets and grime come out as painted detail.
public static partial class ItemSkin
{
    private static bool Supersampled(ItemType type) => type is ItemType.Mineral or ItemType.IronOre or ItemType.NickelOre
        or ItemType.ZincOre or ItemType.CopperOre or ItemType.TitaniumOre or ItemType.UraniumOre or ItemType.AluminumOre
        or ItemType.PlastalloyOre or ItemType.Silicon or ItemType.Carbon or ItemType.SteelPlate or ItemType.TitaniumPlate
        or ItemType.CopperCable or ItemType.Plastic or ItemType.Plastalloy or ItemType.Hyperium
        // the toolkit redrawn after Barotrauma's sheet (ItemSkin.WorkTools.cs, ItemSkin.Kit.cs)
        or ItemType.Wrench or ItemType.Screwdriver or ItemType.GoshaScrewdriver or ItemType.WeldingTool or ItemType.Cutter
        or ItemType.Radio or ItemType.OxygenTank or ItemType.WeldingTank or ItemType.BeltBag
        // the firearms and what they fire (ItemSkin.Guns.cs, ItemSkin.Ammo.cs)
        || GunDefinitions.Of(type) is not null || GunDefinitions.IsAmmo(type) || ArmorDefinitions.IsArmor(type)
        || OreArt.Has(type); // raw ore - OreArt.cs

    // Size-independent noise, so a rock keeps its shape at every icon size.
    private static float Rand(int seed, int i)
    {
        var n = unchecked(seed * 92821 + i * 68917 + 1013904223);
        n = unchecked((n ^ (n >> 13)) * 1274126177);
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    // Smooth value noise in design units - grime and mottling that reads as painted, not as
    // single-pixel static.
    private static float Noise(float x, float y, int seed)
    {
        static float Smooth(float t) => t * t * (3f - 2f * t);
        float Corner(int xi, int yi) => Rand(seed, unchecked(xi * 7919 + yi * 104729));
        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = Smooth(x - x0);
        var fy = Smooth(y - y0);
        var top = Corner(x0, y0) + (Corner(x0 + 1, y0) - Corner(x0, y0)) * fx;
        var bottom = Corner(x0, y0 + 1) + (Corner(x0 + 1, y0 + 1) - Corner(x0, y0 + 1)) * fx;
        return top + (bottom - top) * fy;
    }

    private static float Fbm(float x, float y, int seed) =>
        Noise(x * 0.45f, y * 0.45f, seed) * 0.6f + Noise(x * 1.1f, y * 1.1f, seed + 1) * 0.3f + Noise(x * 2.6f, y * 2.6f, seed + 2) * 0.1f;

    // Grime over everything painted so far: dark, brownish blotches where the noise runs high.
    private static void Grime(int seed, float strength = 0.5f) =>
        Each(0f, 0f, Design, Design, (px, py, dx, dy) =>
        {
            if (_buf[py * _size + px].A < 200)
                return;
            var n = Fbm(dx, dy, seed);
            if (n > 0.55f)
                Put(px, py, new Color(48, 40, 30), Math.Min(0.55f, (n - 0.55f) * 2.4f) * strength);
        });

    // ------------------------------------------------------------------ ore

    // One fractured lump: an irregular outline split into flat facets that each catch the light
    // according to which way they face, plus a lit top face; crystals of the mineral set into it.
    private static void Lump(float cx, float cy, float r, int seed, Color rock, Color crystal, int crystals)
    {
        const int corners = 7;
        var xy = new float[corners * 2];
        var spin = Rand(seed, 99) * MathF.Tau;
        for (var i = 0; i < corners; i++)
        {
            var a = spin + i * MathF.Tau / corners + (Rand(seed, i) - 0.5f) * 0.5f;
            var rr = r * (0.72f + Rand(seed, i + 10) * 0.3f);
            xy[i * 2] = cx + MathF.Cos(a) * rr;
            xy[i * 2 + 1] = cy + MathF.Sin(a) * rr * 0.82f;
        }
        // a dark crevice around the lump first, so lumps in a heap read as separate pieces
        var shadowXy = new float[xy.Length];
        for (var i = 0; i < xy.Length; i += 2)
        {
            shadowXy[i] = cx + (xy[i] - cx) * 1.1f + 0.3f;
            shadowXy[i + 1] = cy + (xy[i + 1] - cy) * 1.1f + 0.5f;
        }
        Poly(new Mat(new Color(26, 22, 20), 0f, 0f), shadowXy);
        var topRadius = 0.36f + Rand(seed, 50) * 0.12f;
        Each(cx - r, cy - r, cx + r, cy + r, (px, py, dx, dy) =>
        {
            if (!InsidePoly(xy, dx, dy))
                return;
            var vx = dx - cx;
            var vy = (dy - cy) / 0.82f;
            var dist = MathF.Sqrt(vx * vx + vy * vy) / r;
            float light;
            var crack = false;
            if (dist < topRadius)
            {
                light = 0.95f;                                                     // the broken top face
                crack = dist > topRadius - 0.07f;
            }
            else
            {
                var a = MathF.Atan2(vy, vx) - spin;
                var unit = ((a % MathF.Tau) + MathF.Tau) % MathF.Tau / (MathF.Tau / corners);
                var sector = (int)MathF.Floor(unit);
                var mid = spin + (sector + 0.5f) * MathF.Tau / corners;
                light = 0.3f + 0.62f * MathF.Max(0f, -0.6f * MathF.Cos(mid) - 0.8f * MathF.Sin(mid))
                    + (Rand(seed, sector + 60) - 0.5f) * 0.16f;
                crack = unit - sector < 0.06f;                                      // the ridge between facets
            }
            var mottle = (Fbm(dx * 1.6f, dy * 1.6f, seed) - 0.5f) * 0.4f;
            var c = Scale(rock, 0.5f + 0.72f * light + mottle);
            Put(px, py, crack ? Scale(c, 0.55f) : c);
        });
        // flecks of the mineral, set into the rock rather than stuck on top of it
        for (var i = 0; i < crystals * 2; i++)
        {
            var a = Rand(seed, i + 70) * MathF.Tau;
            var d = Rand(seed, i + 80) * r * 0.6f;
            var x = cx + MathF.Cos(a) * d;
            var y = cy + MathF.Sin(a) * d * 0.8f;
            var s = r * (0.08f + Rand(seed, i + 90) * 0.1f);
            Poly(new Mat(crystal, 0.7f, 0.04f), x - s, y, x - s * 0.1f, y - s * 1.2f, x + s, y - s * 0.2f, x + s * 0.3f, y + s * 0.9f);
            Dot(x - s * 0.2f, y - s * 0.5f, new Color(244, 238, 222), 0.6f);
        }
    }

    // Ore in an open bin, the way the user's reference sprites carry it: a dark interior, the ore
    // packed in solid and heaped above the rim (no background showing between the lumps), and a
    // painted steel front panel - corner brackets, rivets, a worn top lip - carrying a plate with
    // the element's symbol. The front panel is drawn last, so it cuts across the front row of ore.
    private static void Heap(int seed, Color rock, Color crystal, string? tag, int crystals = 2, Color? glow = null)
    {
        var paint = new Color(118, 54, 42);          // the bin's maroon paint
        var steel = new Mat(new Color(84, 86, 88), 0.35f, 0.05f);

        // interior of the bin, seen over the front lip
        Poly(new Mat(new Color(34, 28, 24), 0f, 0.04f), 4f, 12.6f, 28f, 12.6f, 29.6f, 19f, 2.4f, 19f);
        Box(3.2f, 11.6f, 25.6f, 1.6f, new Mat(Scale(paint, 0.7f), 0.2f, 0.06f), 0.4f);  // back lip

        // the ore: rows from the back, packed tight, the middle mounded up over the rim
        var row = 0;
        foreach (var (y, r, xs) in new[]
        {
            (7.6f, 3.4f, new[] { 14.4f, 18.6f }),
            (9.4f, 3.8f, new[] { 11.8f, 17f, 21.6f }),
            (11.6f, 3.6f, new[] { 6.4f, 11.4f, 16.4f, 21.4f, 26f }),
            (14f, 4f, new[] { 5f, 10.2f, 15.6f, 21f, 26.6f }),
            (16.6f, 4.2f, new[] { 4.4f, 9.6f, 15f, 20.4f, 25.8f, 29f }),
        })
        {
            for (var i = 0; i < xs.Length; i++)
                Lump(xs[i] + (Rand(seed, row * 10 + i) - 0.5f) * 0.8f, y + (Rand(seed, row * 10 + i + 5) - 0.5f) * 0.6f,
                    r + Rand(seed, row * 10 + i + 7) * 0.8f, seed * 31 + row * 10 + i, rock, crystal, crystals);
            row++;
        }
        Grime(seed, 0.3f);
        if (glow is { } g)
            Glow(16f, 12f, 13f, g, 0.5f);

        // front panel of the bin
        Box(2f, 18.4f, 28f, 11.6f, new Mat(paint, 0.25f, 0.08f), 0.6f);
        Line(2.4f, 18.8f, 29.6f, 18.8f, new Color(206, 170, 150), 0.45f);                  // worn top lip
        Line(2.4f, 26.6f, 29.6f, 26.6f, Scale(paint, 0.55f), 0.8f);                       // pressed rib
        foreach (var x in new[] { 2f, 26.6f })
        {
            Box(x, 18.4f, 3.4f, 11.6f, steel, 0.4f);                                       // corner brackets
            Dot(x + 1.7f, 20.4f, Ink, 0.9f, 0.5f);
            Dot(x + 1.7f, 28f, Ink, 0.9f, 0.5f);
        }
        Each(2f, 18.4f, 30f, 30f, (px, py, dx, dy) =>                                      // chipped paint
        {
            if (dx > 5.4f && dx < 26.6f && dy > 19f && Fbm(dx * 2f, dy * 2f, seed + 9) > 0.7f)
                Put(px, py, new Color(70, 70, 70), 0.6f);
        });
        Grime(seed + 5, 0.25f);

        // the symbol plate on the front
        if (tag is null)
            return;
        var w = tag.Length > 1 ? 11.6f : 7.4f;
        Box(16f - w / 2f, 20.2f, w, 7.4f, new Mat(new Color(196, 190, 174), 0.2f, 0.06f), 0.6f);
        Line(16.4f - w / 2f, 20.6f, 15.6f + w / 2f, 20.6f, Color.White, 0.3f);
        DrawCode(tag, 16f, 21.4f, new Color(40, 34, 28));
    }

    private static void Mineral() => Heap(1, new Color(108, 104, 98), new Color(226, 226, 230), null, 3);
    private static void IronOre() => Heap(2, new Color(104, 70, 56), new Color(166, 76, 50), "FE");
    private static void NickelOre() => Heap(3, new Color(94, 98, 86), new Color(160, 182, 140), "NI");
    private static void ZincOre() => Heap(4, new Color(86, 92, 102), new Color(160, 182, 208), "ZN");
    private static void CopperOre() => Heap(5, new Color(92, 82, 72), new Color(190, 106, 58), "CU");
    private static void TitaniumOre() => Heap(6, new Color(122, 122, 126), new Color(208, 212, 220), "TI");
    private static void UraniumOre() => Heap(7, new Color(70, 78, 62), new Color(150, 236, 96), "U", 3, new Color(150, 255, 100));
    private static void AluminumOre() => Heap(8, new Color(144, 136, 120), new Color(220, 220, 226), "AL");
    private static void PlastalloyOre() => Heap(9, new Color(80, 74, 92), new Color(176, 124, 214), "PL");
    private static void Silicon() => Heap(10, new Color(62, 72, 96), new Color(140, 162, 206), "SI", 3);
    private static void Carbon() => Heap(11, new Color(40, 40, 42), new Color(104, 106, 112), "C", 1);

    // ------------------------------------------------------------------ refined

    // A worn metal slab in three-quarter view: top, front and side faces, brushed and mottled,
    // the edges polished bright by handling, a stamped recess on top with its mark.
    private static void Slab(float x, float y, float len, float depth, float thick, Color metal, int seed, string? stamp)
    {
        var sx = depth * 0.9f;   // how far the far edge sits to the right
        var top = new[] { x, y + depth, x + sx, y, x + sx + len, y, x + len, y + depth };
        var front = new[] { x, y + depth, x + len, y + depth, x + len, y + depth + thick, x, y + depth + thick };
        var side = new[] { x + len, y + depth, x + sx + len, y, x + sx + len, y + thick, x + len, y + depth + thick };
        void Face(float[] poly, float light) =>
            Each(x - 1f, y - 1f, x + sx + len + 1f, y + depth + thick + 1f, (px, py, dx, dy) =>
            {
                if (!InsidePoly(poly, dx, dy))
                    return;
                var brushed = (Noise(dx * 0.35f, dy * 6f, seed) - 0.5f) * 0.16f;
                var mottle = (Fbm(dx * 1.2f, dy * 1.2f, seed + 3) - 0.5f) * 0.2f;
                Put(px, py, Scale(metal, light + brushed + mottle));
            });
        Face(side, 0.62f);
        Face(front, 0.8f);
        Face(top, 0.94f);
        var edge = new Color(236, 232, 222);
        Line(x, y + depth, x + len, y + depth, edge, 0.55f);                 // worn front edge
        Line(x + sx, y, x + sx + len, y, edge, 0.3f);
        Line(x + len, y + depth, x + sx + len, y, edge, 0.4f);
        if (stamp is null)
            return;
        // the mark struck into the top face: dark letters with a lit lip below each stroke
        var cx = x + (len + sx) / 2f;
        var cy = y + depth / 2f - 2.2f;
        DrawCode(stamp, cx + 0.25f, cy + 0.3f, Scale(metal, 1.3f));
        DrawCode(stamp, cx, cy, Scale(metal, 0.42f));
    }

    // Refined metal as a tight stack of cast bars - two below, one laid across their middle on
    // top, the mark struck into the top bar - so the icon is one solid object with no gaps.
    private static void Ingots(Color metal, int seed, string stamp)
    {
        Slab(1.6f, 18.6f, 12.4f, 4.6f, 4f, Scale(metal, 0.92f), seed, null);
        Slab(15.4f, 18.6f, 12.4f, 4.6f, 4f, Scale(metal, 0.95f), seed + 1, null);
        Slab(7.6f, 10.6f, 14f, 6.4f, 4.4f, metal, seed + 2, stamp);
        Grime(seed + 3, 0.35f);
    }

    private static void SteelPlate() => Ingots(new Color(98, 102, 108), 21, "ST");
    private static void TitaniumPlate() => Ingots(new Color(128, 136, 150), 31, "TI");
    private static void Plastalloy() => Ingots(new Color(108, 90, 126), 41, "PL");

    // A steel cable drum: two dished flanges, copper windings between them turn by turn, the free
    // end of the cable led out over the top.
    private static void CopperCable()
    {
        var steel = new Mat(new Color(96, 98, 100), 0.35f, 0.05f);
        var copper = new Color(176, 100, 58);
        Box(4f, 5f, 4f, 22f, steel, 1.6f);
        Box(7.6f, 8f, 16.8f, 16f, new Mat(copper, 0.4f, 0.06f), 0.8f);
        for (var y = 8.6f; y < 24f; y += 1.6f)
        {
            Line(7.8f, y, 24.2f, y + 0.4f, new Color(96, 50, 28), 0.7f);
            Line(7.8f, y + 0.6f, 24.2f, y + 1f, new Color(232, 168, 118), 0.4f);
        }
        Box(24f, 5f, 4f, 22f, steel, 1.6f);
        foreach (var x in new[] { 6f, 26f })
        {
            Ball(x, 16f, 1.4f, new Mat(new Color(60, 62, 64), 0.3f, 0.05f), sphere: false);  // axle bosses
            Dot(x, 7.4f, Ink, 0.9f);
            Dot(x, 24.6f, Ink, 0.9f);
        }
        Rod(20f, 8.2f, 23f, 3.2f, 0.9f, new Mat(copper, 0.5f, 0.04f));
        Rod(23f, 3.2f, 29.4f, 3.4f, 0.9f, new Mat(copper, 0.5f, 0.04f));
        Grime(51, 0.3f);
    }

    // Plastic: polymer pellets in a screw-top jar with a paper label.
    private static void Plastic()
    {
        var glass = new Mat(new Color(70, 84, 92), 0.6f, 0.02f);
        Box(7f, 9f, 18f, 20f, glass, 2.4f);
        Each(8f, 13f, 24f, 28f, (px, py, dx, dy) =>
        {
            if (dx < 8.2f || dx > 23.8f || dy < 13.4f || dy > 27.8f)
                return;
            var cell = Rand(61, (int)(dx * 1.6f) * 37 + (int)(dy * 1.6f));
            var shade = 0.7f + cell * 0.35f;
            Put(px, py, Scale(new Color(210, 206, 196), shade));
        });
        Box(8f, 4.6f, 16f, 5.2f, new Mat(new Color(58, 60, 62), 0.35f, 0.05f), 1f);   // lid
        for (var x = 9.4f; x < 23.6f; x += 1.6f)
            Line(x, 5.2f, x, 9.2f, Ink, 0.4f);
        Box(8.6f, 16.4f, 14.8f, 8.6f, new Mat(new Color(206, 196, 168), 0.1f, 0.08f), 0.6f); // label
        Box(8.6f, 16.4f, 14.8f, 1.8f, new Mat(new Color(58, 104, 150), 0.2f, 0.04f));
        DrawCode("PL", 16f, 19.4f, new Color(54, 48, 40));
        Line(9.6f, 10.4f, 9.6f, 27.4f, new Color(236, 240, 244), 0.45f);                // glass highlight
        Grime(62, 0.25f);
    }

    // Hyperium: ice-blue crystals glowing inside a sealed, steel-capped glass vial.
    private static void Hyperium()
    {
        var glass = new Mat(new Color(36, 58, 70), 0.6f, 0.02f);
        Rod(16f, 7f, 16f, 25f, 6f, glass);
        void Crystal(float x, float baseY, float h, float w, Color c)
        {
            var m = new Mat(c, 0.8f, 0.02f);
            Poly(m, x - w, baseY, x - w * 0.6f, baseY - h, x, baseY - h - w * 0.8f, x + w * 0.6f, baseY - h, x + w, baseY);
            Line(x, baseY - h - w * 0.6f, x - w * 0.3f, baseY - 1f, new Color(230, 250, 255), 0.5f);
        }
        Crystal(12.6f, 25f, 7f, 2.4f, new Color(48, 150, 190));
        Crystal(19.2f, 25f, 9f, 2.6f, new Color(80, 184, 222));
        Crystal(15.8f, 25.4f, 12f, 3f, new Color(120, 222, 250));
        Glow(16f, 17f, 11f, new Color(120, 225, 255), 0.6f);
        foreach (var y in new[] { 3.4f, 25.6f })
        {
            Box(9.2f, y, 13.6f, 3.4f, new Mat(new Color(98, 100, 104), 0.4f, 0.05f), 1f);
            Dot(11f, y + 1.7f, Ink, 0.9f);
            Dot(21f, y + 1.7f, Ink, 0.9f);
        }
        Line(11.6f, 8f, 11.6f, 24f, new Color(236, 244, 250), 0.45f);
        Grime(71, 0.2f);
    }
}
