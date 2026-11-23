using System;
using Microsoft.Xna.Framework;

namespace Anabiosis.Client.Rendering;

// Direct user request ("обнови текстуры для всех устройств") - furniture and floor-standing kit used
// to be a flat tinted square with a letter on it. Unlike the machines, none of these is bolted to a
// steel plate: each is drawn free-standing on a TRANSPARENT canvas (FreeStanding - no Housing, no
// chamfer) with a soft drop shadow, so the deck shows round a chair or a plant the way it would in
// Barotrauma / Space Haven. ShipRenderer draws these without the device outline
// (ShipRenderer.Devices.cs's FreeStandingFaces).
public sealed partial class DeviceSkin
{
    private void FreeStanding()
    {
        _chamfer = 0;
        _offsetX = (_canvasWidth - _contentSize) / 2;
        _offsetY = (_canvasHeight - _contentSize) / 2;
        _rotated = false;
    }

    private void Shadow(float x, float y, float w, float h) => Rect(x + U(1), y + U(1.4f), w, h, Color.Black, 0.3f);

    private void Table(bool lit)
    {
        FreeStanding();
        Shadow(U(5), U(8), U(30), U(24));
        Rect(U(5), U(8), U(30), U(24), new Color(88, 94, 106));
        Rect(U(6.5f), U(9.5f), U(27), U(21), new Color(146, 152, 164));
        Rect(U(6.5f), U(9.5f), U(27), 1, Color.White, 0.4f);
        for (var i = 0; i < 5; i++)
            Rect(U(7), U(12) + i * U(4), U(26), 1, Color.Black, 0.05f);
        Disc(U(13), U(15), U(2.6f), Color.Black, 0.25f);
        Disc(U(12.6f), U(14.4f), U(2.4f), new Color(214, 216, 220));
        Disc(U(12.6f), U(14.4f), U(1.5f), new Color(70, 46, 30));
        Rect(U(14.8f), U(13.6f), U(1.2f), U(1.6f), new Color(214, 216, 220));
        Rect(U(20), U(19), U(10), U(7), new Color(30, 32, 38));
        Rect(U(21), U(20), U(8), U(5), lit ? new Color(90, 190, 220) : new Color(20, 30, 36));
        Rect(U(21), U(20), U(3), 1, Color.White, 0.4f);
    }

    private void Chair(bool lit)
    {
        FreeStanding();
        for (var i = 0; i < 5; i++)
        {
            var ang = -MathF.PI / 2f + i * MathF.PI * 2f / 5f;
            Line(U(20), U(21), U(20) + MathF.Cos(ang) * U(12), U(21) + MathF.Sin(ang) * U(12), new Color(40, 42, 48));
            Disc(U(20) + MathF.Cos(ang) * U(12), U(21) + MathF.Sin(ang) * U(12), U(1.2f), new Color(24, 24, 28));
        }
        Shadow(U(10), U(7), U(20), U(23));
        Rect(U(11), U(12), U(18), U(17), new Color(78, 92, 118));
        Rect(U(11), U(12), U(18), 1, Color.White, 0.3f);
        Rect(U(19.5f), U(14), 1, U(13), Color.Black, 0.15f);
        Rect(U(10), U(6.5f), U(20), U(6), new Color(54, 60, 74));
        Rect(U(10), U(6.5f), U(20), 1, Color.White, 0.3f);
        foreach (var ax in new[] { U(7.5f), U(29.5f) })
        {
            Rect(ax, U(13), U(3), U(12), new Color(120, 126, 138));
            Rect(ax, U(13), U(3), 1, Color.White, 0.35f);
        }
    }

    private void Sofa(bool lit)
    {
        FreeStanding();
        Shadow(U(3), U(8), U(34), U(24));
        Rect(U(3), U(8), U(34), U(7), new Color(62, 72, 94));
        Rect(U(3), U(8), U(34), 1, Color.White, 0.3f);
        foreach (var ax in new[] { U(3), U(32) })
        {
            Rect(ax, U(13), U(5), U(19), new Color(62, 72, 94));
            Rect(ax, U(13), U(5), 1, Color.White, 0.25f);
        }
        foreach (var cx in new[] { U(8), U(20) })
        {
            Rect(cx, U(15), U(12), U(16), new Color(92, 106, 134));
            Rect(cx, U(15), U(12), 1, Color.White, 0.35f);
            Rect(cx + U(11.5f), U(15), 1, U(16), Color.Black, 0.25f);
            Line(cx + U(2), U(19), cx + U(9), U(26), Color.Black, 0.08f);
        }
    }

    private void Nightstand(bool lit)
    {
        FreeStanding();
        Shadow(U(8), U(9), U(24), U(23));
        Rect(U(8), U(9), U(24), U(23), new Color(70, 76, 88));
        Rect(U(9), U(10), U(22), U(21), new Color(98, 104, 116));
        Rect(U(9), U(10), U(22), 1, Color.White, 0.35f);
        Rect(U(9), U(24), U(22), 1, Color.Black, 0.35f);
        Rect(U(16), U(27), U(8), MathF.Max(1f, U(1.2f)), new Color(190, 196, 206));
        Rect(U(11), U(13), U(8), U(6), new Color(120, 46, 40));
        Rect(U(11), U(13), U(8), 1, Color.White, 0.25f);
        Rect(U(12), U(13.5f), MathF.Max(1f, U(0.6f)), U(5), new Color(220, 200, 150));
        if (lit)
            Disc(U(25), U(15), U(5), new Color(255, 220, 150), 0.2f);
        Disc(U(25), U(15), U(3.2f), new Color(220, 206, 170));
        Disc(U(25), U(15), U(1.4f), lit ? new Color(255, 244, 210) : new Color(120, 112, 96));
    }

    private void Plant(bool lit)
    {
        FreeStanding();
        Disc(U(21), U(23.5f), U(8.5f), Color.Black, 0.3f);
        Disc(U(20), U(22), U(8.5f), new Color(66, 70, 80));
        Ring(U(20), U(22), U(8), new Color(140, 146, 158), 0.6f, 0.9f);
        Disc(U(20), U(22), U(6.6f), new Color(58, 42, 30));
        for (var i = 0; i < 8; i++)
        {
            var ang = i * MathF.PI / 4f + Hash(i, 71) * 0.4f;
            var len = U(9 + Hash(i, 72) * 5);
            var leaf = i % 2 == 0 ? new Color(58, 138, 70) : new Color(84, 168, 88);
            for (var t = 0.15f; t <= 1f; t += 0.08f)
            {
                var w = MathF.Sin(t * MathF.PI) * U(2.4f);
                Disc(U(20) + MathF.Cos(ang) * len * t, U(21) + MathF.Sin(ang) * len * t, MathF.Max(0.6f, w), leaf);
            }
            Line(U(20), U(21), U(20) + MathF.Cos(ang) * len * 0.9f, U(21) + MathF.Sin(ang) * len * 0.9f, new Color(150, 210, 140), 0.35f);
        }
    }

    private void FloorLamp(bool lit)
    {
        FreeStanding();
        if (lit)
            Disc(U(20), U(20), U(16), new Color(255, 220, 150), 0.14f);
        Disc(U(21), U(21.5f), U(9), Color.Black, 0.3f);
        Disc(U(20), U(20), U(9), new Color(214, 200, 166));
        Ring(U(20), U(20), U(8.6f), new Color(150, 136, 110), 0.7f, 0.9f);
        Ring(U(20), U(20), U(5.5f), new Color(190, 176, 144), 0.5f, 0.7f);
        Disc(U(20), U(20), U(2.6f), lit ? new Color(255, 248, 224) : new Color(110, 104, 90));
        Disc(U(18), U(17.5f), U(2), Color.White, 0.25f);
    }

    private void Spotlight(bool lit)
    {
        FreeStanding();
        foreach (var (lx, ly) in new[] { (U(7), U(33)), (U(33), U(33)), (U(20), U(6)) })
        {
            Line(U(20), U(21), lx, ly, new Color(40, 42, 48));
            Line(U(20.5f), U(21), lx + 0.5f, ly, new Color(40, 42, 48));
            Disc(lx, ly, U(1.3f), new Color(24, 24, 28));
        }
        Disc(U(21), U(22.5f), U(6.5f), Color.Black, 0.3f);
        Disc(U(20), U(21), U(6.5f), new Color(62, 66, 76));
        Ring(U(20), U(21), U(6.2f), new Color(150, 156, 168), 0.6f, 0.8f);
        Disc(U(20), U(21), U(4.2f), lit ? new Color(236, 244, 255) : new Color(70, 76, 86));
        if (lit)
            Disc(U(20), U(21), U(9), new Color(220, 236, 255), 0.15f);
        Disc(U(18.6f), U(19.6f), U(1.2f), Color.White, 0.6f);
    }

    // A card table: green felt in a dark wooden rim, a few cards dealt and a stack of chips.
    private void CardTable(bool lit)
    {
        FreeStanding();
        Shadow(U(3), U(7), U(34), U(26));
        Rect(U(3), U(7), U(34), U(26), new Color(78, 52, 34));
        Rect(U(3), U(7), U(34), 1, Color.White, 0.25f);
        Rect(U(5), U(9), U(30), U(22), new Color(34, 108, 68));
        for (var i = 0; i < 6; i++)
            Rect(U(5), U(10) + i * U(3.5f), U(30), 1, Color.Black, 0.06f);
        foreach (var (cx, cy, red) in new[] { (U(9), U(12), true), (U(13), U(12.5f), false), (U(24), U(21), true), (U(28), U(20), false) })
        {
            Rect(cx + 0.5f, cy + 0.8f, U(3.4f), U(4.6f), Color.Black, 0.3f);
            Rect(cx, cy, U(3.4f), U(4.6f), new Color(238, 236, 228));
            Rect(cx + U(1.1f), cy + U(1.6f), U(1.2f), U(1.4f), red ? new Color(200, 40, 40) : new Color(30, 30, 34));
        }
        for (var i = 0; i < 4; i++)
        {
            var chip = i % 2 == 0 ? new Color(220, 60, 50) : new Color(60, 110, 210);
            Disc(U(19), U(25) - i * MathF.Max(1f, U(0.8f)), U(1.8f), chip);
            Ring(U(19), U(25) - i * MathF.Max(1f, U(0.8f)), U(1.4f), Color.White, 0.5f, 0.5f);
        }
    }

    // Ammunition stores: two olive crates with stencilled bands, a tray of brass shells on top - the
    // shells and the stencil go dull once the stock is empty (`lit` false).
    private void AmmoCrates(bool lit)
    {
        FreeStanding();
        Shadow(U(4), U(9), U(32), U(25));
        foreach (var (cx, cy, cw, ch) in new[] { (U(4), U(16), U(17), U(18)), (U(20), U(9), U(16), U(24)) })
        {
            Rect(cx, cy, cw, ch, new Color(84, 96, 60));
            Rect(cx, cy, cw, 1, Color.White, 0.3f);
            Rect(cx + cw - 1, cy, 1, ch, Color.Black, 0.35f);
            Rect(cx, cy + ch - 1, cw, 1, Color.Black, 0.45f);
            Rect(cx, cy + ch * 0.25f, cw, 1, Color.Black, 0.35f);
            Rect(cx + cw * 0.15f, cy + ch * 0.5f, cw * 0.7f, MathF.Max(2f, ch * 0.12f), lit ? new Color(214, 184, 60) : new Color(110, 100, 60), 0.9f);
            Bolt(cx + U(1.6f), cy + U(1.6f), MathF.Max(0.7f, U(0.6f)));
            Bolt(cx + cw - U(1.8f), cy + U(1.6f), MathF.Max(0.7f, U(0.6f)));
        }
        Rect(U(22), U(10.5f), U(12), U(5), new Color(40, 42, 46));
        for (var i = 0; i < 5; i++)
            Disc(U(23.4f) + i * U(2.3f), U(13), U(0.95f), lit ? new Color(222, 178, 82) : new Color(60, 56, 50));
    }
}
