using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

/// <summary>The gun out on the hull plating, baked.</summary>
///
/// It used to be a filled square with a filled rectangle sticking out of it - which is a diagram of
/// a turret rather than a turret. A gun is two separate things and drawing them as one is most of
/// why it read flat: a barbette bolted through the plating that never moves, and a rotating mass
/// sitting in it. So there are two sprites here, and only the second one turns.
///
/// What the detail is actually for, in order of how much it does:
///
///   * the muzzle brake. It is the one silhouette detail that survives at any zoom and it is what
///     says "gun" before anything else on the sprite does;
///   * shading across the barrel. A cylinder lit from one side reads as round; a rectangle of flat
///     colour reads as a stick no matter how carefully its outline is drawn;
///   * the sweep scoring on the barbette - a worn arc where the gun has swung back and forth. It is
///     the only part that says the thing has been used, and it costs four lines.
public sealed class TurretSkin : IDisposable
{
    public enum Look { Idle, Manned, Damaged }

    // The rotating half. Long enough that the muzzle lands where TurretMount says it does: the
    // barrel runs BarrelLength world units from the pivot, and the brake sits on the end of it.
    private const int GunWidth = 92;
    private const int GunHeight = 36;
    private const int PivotX = 20;
    private static readonly int MuzzleX =
        PivotX + (int)(TurretMount.BarrelLength * ShipRenderer.PixelsPerUnit);

    private const int BaseSize = 48;

    public static readonly Vector2 GunOrigin = new(PivotX, GunHeight / 2f);
    public static readonly Vector2 BaseOrigin = new(BaseSize / 2f, BaseSize / 2f);

    private static readonly Color Steel = new(104, 110, 120);
    private static readonly Color DarkSteel = new(58, 62, 70);
    private static readonly Color Shadow = new(24, 26, 32);

    private readonly GraphicsDevice _graphics;
    private readonly Dictionary<Look, Texture2D> _guns = new();
    private readonly Dictionary<Look, Texture2D> _bases = new();

    public TurretSkin(GraphicsDevice graphics) => _graphics = graphics;

    public Texture2D Gun(Look look)
    {
        if (_guns.TryGetValue(look, out var cached))
            return cached;
        var baked = BakeGun(look);
        _guns[look] = baked;
        return baked;
    }

    public Texture2D Base(Look look)
    {
        if (_bases.TryGetValue(look, out var cached))
            return cached;
        var baked = BakeBase(look);
        _bases[look] = baked;
        return baked;
    }

    public void Dispose()
    {
        foreach (var texture in _guns.Values)
            texture.Dispose();
        foreach (var texture in _bases.Values)
            texture.Dispose();
        _guns.Clear();
        _bases.Clear();
    }

    // The one colour that changes with state. Everything else stays steel: a manned gun is not made
    // of different metal, it has its running lights on.
    private static Color Trim(Look look) => look switch
    {
        Look.Manned => new Color(226, 178, 74),
        Look.Damaged => new Color(148, 58, 50),
        _ => new Color(128, 136, 148),
    };

    // ---------------------------------------------------------------- the gun

    private Texture2D BakeGun(Look look)
    {
        var c = new PixelCanvas(GunWidth, GunHeight);
        var trim = Trim(look);
        const float mid = GunHeight / 2f;

        // Breech mass behind the pivot. A gun that is all barrel and nothing behind the trunnions
        // looks like it would tip over; the counterweight is what makes the pivot believable.
        c.Rect(5, mid - 7, 16, 14, Steel);
        c.Rect(5, mid - 7, 16, 1, Color.White, 0.32f);
        c.Rect(5, mid + 6, 16, 1, Color.Black, 0.45f);
        c.Rect(4, mid - 5, 2, 10, DarkSteel);
        for (var i = 0; i < 3; i++)
            c.Rect(9 + i * 4, mid - 7, 1, 14, Color.Black, 0.22f);

        // The gun house, sloped toward the front so the eye can tell which way it points even when
        // the barrel is foreshortened. Deliberately narrow: the first cut made it wide enough to
        // cover the barbette completely, which threw away every bit of the mount underneath and left
        // one undifferentiated blob. A mount only reads as a mount if you can see the gun sitting
        // in it.
        for (var x = 14; x < 40; x++)
        {
            var t = (x - 14) / 26f;
            var half = 9f - t * 2.5f;
            var shade = 1f - t * 0.10f;
            c.Rect(x, mid - half, 1, half * 2f,
                new Color((int)(Steel.R * shade), (int)(Steel.G * shade), (int)(Steel.B * shade)));
            c.Px(x, mid + half - 1, Color.Black, 0.45f);
            // Trim paint follows the shield's own leading edge, column by column, rather than
            // sitting at a fixed height: a straight line above a tapering edge reads as a marking
            // floating over the gun instead of painted onto it.
            if (PixelCanvas.Hash(x * 17, 3) <= 0.88f)
                c.Rect(x, mid - half, 1, 2, trim, 0.85f);
            else
                c.Px(x, mid - half, Color.White, 0.30f);
        }

        // Trunnion caps: the pins the whole mass swings on, and the only round things on this half.
        foreach (var ty in new[] { mid - 9f, mid + 8f })
        {
            c.Disc(25, ty, 3f, DarkSteel);
            c.Disc(25, ty, 1.8f, Steel);
        }

        // Recoil cylinders, clear of the barrel on either side.
        foreach (var cy in new[] { mid - 8f, mid + 5f })
        {
            c.Tube(38, cy, 20, 3, DarkSteel);
            c.Rect(56, cy, 3, 3, Steel);
        }

        // The barrel: tapered, thinner than the house so the mantlet reads as armour around it, and
        // shaded like a cylinder. Two lines of shading are the whole difference between a barrel and
        // a stick.
        for (var x = 36; x < MuzzleX - 6; x++)
        {
            var t = (x - 36) / (float)(MuzzleX - 42);
            var half = 5f - t * 1.8f;
            c.Tube(x, mid - half, 1, half * 2f, Steel);
        }
        // Reinforcing bands, which also break up the length so a long barrel does not read as one
        // flat smear when the camera is pulled back.
        foreach (var bx in new[] { 54, 66 })
        {
            var t = (bx - 36) / (float)(MuzzleX - 42);
            var half = 5f - t * 1.8f + 1f;
            c.Rect(bx, mid - half, 3, half * 2f, DarkSteel);
            c.Rect(bx, mid - half, 3, 1, Color.White, 0.25f);
        }

        // Muzzle brake. The most recognisable piece of a gun there is, and the reason this
        // silhouette reads as artillery rather than as a pipe.
        c.Rect(MuzzleX - 8, mid - 5.5f, 10, 11, Steel);
        c.Rect(MuzzleX - 8, mid - 5.5f, 10, 1, Color.White, 0.35f);
        c.Rect(MuzzleX - 8, mid + 4.5f, 10, 1, Color.Black, 0.5f);
        foreach (var sx in new[] { MuzzleX - 6, MuzzleX - 2 })
        {
            c.Rect(sx, mid - 5.5f, 2, 2, Shadow);
            c.Rect(sx, mid + 3.5f, 2, 2, Shadow);
        }
        c.Rect(MuzzleX - 1, mid - 2, 2, 4, new Color(16, 14, 16));   // the bore

        // Ammunition feed, overlapping the house rather than hanging off its edge.
        c.Rect(13, mid + 5, 14, 8, DarkSteel);
        c.Rect(13, mid + 5, 14, 1, Color.White, 0.22f);
        c.Rect(13, mid + 12, 14, 1, Color.Black, 0.4f);
        for (var i = 0; i < 4; i++)
            c.Rect(15 + i * 3, mid + 7, 2, 4, new Color(150, 122, 70));

        // The gunner's sight, lit only when somebody is behind it.
        c.Disc(31, mid - 4, 2.6f, Shadow);
        c.Disc(31, mid - 4, 1.7f, look == Look.Manned ? new Color(170, 235, 255) : new Color(46, 60, 72),
            look == Look.Manned ? 0.95f : 1f);

        if (look == Look.Damaged)
            Scorch(c, 0, 0, GunWidth, GunHeight, 12);

        return c.ToTexture(_graphics);
    }

    // ---------------------------------------------------------------- the barbette

    private Texture2D BakeBase(Look look)
    {
        var c = new PixelCanvas(BaseSize, BaseSize);
        const float mid = BaseSize / 2f;
        var trim = Trim(look);

        // Deck plate: an octagon, matching the chamfered housings everything else on this ship is
        // built out of, so the gun belongs to the same shipyard as the rest of it.
        var r = 21f;
        for (var y = 0; y < BaseSize; y++)
        for (var x = 0; x < BaseSize; x++)
        {
            var dx = MathF.Abs(x - mid + 0.5f);
            var dy = MathF.Abs(y - mid + 0.5f);
            if (MathF.Max(dx, dy) > r || dx + dy > r * 1.32f)
                continue;
            var v = (PixelCanvas.Hash(x * 13 + 7, y / 6) - 0.5f) * 14f;
            var g = 10f * (1f - y / mid);
            c.Px(x, y, new Color(
                (int)MathHelper.Clamp(Steel.R + v + g, 0, 255),
                (int)MathHelper.Clamp(Steel.G + v + g, 0, 255),
                (int)MathHelper.Clamp(Steel.B + v + g, 0, 255)));
        }

        // Bolts around the rim: this is a thing fastened through plating, not resting on it.
        for (var i = 0; i < 8; i++)
        {
            var ang = i * MathF.PI / 4f + MathF.PI / 8f;
            var bx = mid + MathF.Cos(ang) * 17.5f;
            var by = mid + MathF.Sin(ang) * 17.5f;
            c.Disc(bx, by, 2f, DarkSteel);
            c.Px(bx, by - 1, Color.White, 0.5f);
        }

        // Sweep scoring: a worn arc where the gun has swung back and forth for years. Four lines,
        // and the mount stops looking as though it were installed this morning.
        for (var i = 0; i < 4; i++)
            c.Ring(mid, mid, 13.5f - i * 0.9f, new Color(150, 156, 166), 0.10f + i * 0.03f, 0.7f);

        // The race the gun turns on, with its teeth.
        c.Ring(mid, mid, 12f, Shadow, 0.9f, 2.2f);
        for (var i = 0; i < 24; i++)
        {
            var ang = i * MathF.PI * 2f / 24f;
            c.Line(mid + MathF.Cos(ang) * 10.5f, mid + MathF.Sin(ang) * 10.5f,
                mid + MathF.Cos(ang) * 13f, mid + MathF.Sin(ang) * 13f, new Color(140, 146, 156), 0.35f);
        }
        c.Disc(mid, mid, 9f, new Color(74, 78, 88));
        c.Ring(mid, mid, 9f, Color.Black, 0.5f, 1f);

        // Power and feed conduit running off to the plating.
        foreach (var ang in new[] { MathF.PI * 0.75f, MathF.PI * 1.25f })
        {
            var dx = MathF.Cos(ang);
            var dy = MathF.Sin(ang);
            for (var t = 13f; t < 22f; t++)
                c.Disc(mid + dx * t, mid + dy * t, 2.2f, t > 19f ? DarkSteel : new Color(84, 90, 100));
        }

        // A bearing index painted on the rim, in the colour the gun's own trim carries. The first
        // version swept an arc of dots around the plate at radius 18.5 and read as a crescent
        // floating above the gun - most of that arc fell outside the octagon, so it was paint on
        // nothing. A short mark on the plate says the same thing and stays on the metal.
        c.Rect(mid - 3, 3, 6, 2, trim, 0.85f);
        c.Rect(mid - 1, 5, 2, 3, trim, 0.6f);

        if (look == Look.Damaged)
            Scorch(c, 4, 4, BaseSize - 8, BaseSize - 8, 10);

        return c.ToTexture(_graphics);
    }

    // Soot rather than red paint. A wrecked machine that is still recognisably itself, with burns on
    // it, reads as damage; the same machine repainted red reads as a different machine.
    private static void Scorch(PixelCanvas c, int x, int y, int w, int h, int blobs)
    {
        for (var i = 0; i < blobs; i++)
        {
            var bx = x + PixelCanvas.Hash(i, 11) * w;
            var by = y + PixelCanvas.Hash(i, 13) * h;
            c.Disc(bx, by, 1.5f + PixelCanvas.Hash(i, 17) * 3f, new Color(26, 22, 20),
                0.20f + PixelCanvas.Hash(i, 19) * 0.25f);
        }
    }
}
