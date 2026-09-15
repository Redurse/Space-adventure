using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Shared low-level drawing primitives reused across many device housings (panels, screens, rivets, scorch marks, hazard stripes, ambient particles) - split out of ShipRenderer.cs to keep that file to its own topic.
public sealed partial class ShipRenderer
{
    // A breach's steam, a damaged system's sparks, a starved reactor's embers - continuous rather
    // than a one-shot burst, so each is just a soft dot that drifts and fades over its own lifetime
    // (AtmosphereParticle.Progress) instead of DrawSparkBurst's radiating rays.
    private void DrawAtmosphereParticle(SpriteBatch spriteBatch, AtmosphereParticle particle, Vector2 origin)
    {
        var center = origin + new Vector2((float)particle.Position.X, (float)particle.Position.Y) * PixelsPerUnit;
        var alpha = 1f - particle.Progress;
        var color = particle.Kind switch
        {
            AtmosphereKind.Steam => Color.WhiteSmoke * (alpha * 0.32f),
            AtmosphereKind.Spark => Color.Lerp(Color.Yellow, Color.OrangeRed, particle.Progress) * alpha,
            _ => Color.Lerp(Color.Orange, new Color(90, 20, 10), particle.Progress) * alpha,
        };
        // Steam swells as it disperses; sparks and embers shrink towards nothing.
        var scale = particle.Kind == AtmosphereKind.Steam ? 1f + particle.Progress * 1.6f : 1f - particle.Progress * 0.6f;
        var size = particle.Size * PixelsPerUnit * scale;
        spriteBatch.Draw(_pixel, center, null, color, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size), SpriteEffects.None, 0f);
    }

    // Barotrauma-style brief spark burst for a tool action that just landed (welding a breach,
    // repairing a system) - a handful of short rays radiating from the point, expanding and
    // fading over the effect's lifetime (TransientEffect.Progress goes 0 -> 1).
    private void DrawSparkBurst(SpriteBatch spriteBatch, Vector2 center, float progress, Color color)
    {
        var alpha = 1f - progress;
        var length = 5f + progress * 16f;
        const int rayCount = 6;
        for (var i = 0; i < rayCount; i++)
        {
            var angle = i * MathF.PI * 2f / rayCount + progress * 2f;
            spriteBatch.Draw(_pixel, center, null, color * alpha, angle, new Vector2(0f, 0.5f), new Vector2(length, 2f), SpriteEffects.None, 0f);
        }
    }

    // Shared industrial "panel" look for equipment blocks (game_design.md Phase 3 visual pass) —
    // a beveled face plus four corner rivets, built entirely from the single white pixel texture
    // (this project has no image assets/content pipeline for real sprites).
    private void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, Color faceColor, Color borderColor, int borderThickness) =>
        DrawPanel(spriteBatch, _pixel, rect, faceColor, borderColor, borderThickness, _devicePlate, _faceShade);

    // internal + static, with the pixel texture passed explicitly, so ComponentRenderer.cs can draw
    // the exact same beveled-panel-plus-rivets look for installed components instead of a new art
    // style from scratch.
    //
    // `material` and `shade` are optional so the HUD callers (CardGamePanel) keep the flat look
    // they were drawn against - a machine face belongs on a machine, not on a playing card.
    internal static void DrawPanel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color faceColor, Color borderColor, int borderThickness,
        Texture2D? material = null, Texture2D? shade = null)
    {
        if (material is not null)
            TileTextures.DrawTiled(spriteBatch, material, TileTextures.DeviceTileSize, rect, faceColor);
        else
            spriteBatch.Draw(pixel, rect, faceColor);

        // Stretched over the whole face in one draw rather than tiled, so the top-to-bottom
        // shading spans the panel instead of repeating inside it.
        if (shade is not null)
            spriteBatch.Draw(shade, rect, Color.White);
        // Bevel: a lighter sliver along the top/left, a darker one along bottom/right.
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), Color.White * 0.18f);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), Color.White * 0.18f);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), Color.Black * 0.35f);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), Color.Black * 0.35f);
        DrawRectOutline(spriteBatch, pixel, rect, borderColor, borderThickness);
        DrawRivets(spriteBatch, pixel, rect);
        // A shadow just outside the bottom and right edges. Small, and the single biggest thing
        // that stops a panel reading as printed onto the deck rather than bolted onto it.
        spriteBatch.Draw(pixel, new Rectangle(rect.X + 2, rect.Bottom, rect.Width, 2), Color.Black * 0.28f);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right, rect.Y + 2, 2, rect.Height), Color.Black * 0.28f);
    }


    // The chamfered ("stepped octagon") housing every ship device now shares - the angular,
    // riveted-corner silhouette the Reactor's Hullwright's Bench redesign introduced, approximated
    // with 3 axis-aligned bands since this renderer has no arbitrary polygon fill. Reuses the same
    // material/shade textures DrawPanel already applies to a plain rect, so a chamfered device
    // still reads as the same family, not a different art style.
    private void DrawChamferedHousing(SpriteBatch spriteBatch, Rectangle rect, Color faceColor, Color borderColor, float borderThickness)
    {
        var chamfer = Math.Max(2, Math.Min(rect.Width, rect.Height) / 6);
        var topBand = new Rectangle(rect.X + chamfer, rect.Y, rect.Width - chamfer * 2, chamfer);
        var midBand = new Rectangle(rect.X, rect.Y + chamfer, rect.Width, rect.Height - chamfer * 2);
        var botBand = new Rectangle(rect.X + chamfer, rect.Bottom - chamfer, rect.Width - chamfer * 2, chamfer);

        TileTextures.DrawTiled(spriteBatch, _devicePlate, TileTextures.DeviceTileSize, topBand, faceColor);
        TileTextures.DrawTiled(spriteBatch, _devicePlate, TileTextures.DeviceTileSize, midBand, faceColor);
        TileTextures.DrawTiled(spriteBatch, _devicePlate, TileTextures.DeviceTileSize, botBand, faceColor);
        spriteBatch.Draw(_faceShade, rect, Color.White);

        Span<Vector2> vertices = stackalloc Vector2[]
        {
            new(rect.X + chamfer, rect.Y), new(rect.Right - chamfer, rect.Y),
            new(rect.Right, rect.Y + chamfer), new(rect.Right, rect.Bottom - chamfer),
            new(rect.Right - chamfer, rect.Bottom), new(rect.X + chamfer, rect.Bottom),
            new(rect.X, rect.Bottom - chamfer), new(rect.X, rect.Y + chamfer),
        };
        for (var i = 0; i < vertices.Length; i++)
        {
            HudIcons.DrawLine(spriteBatch, _pixel, vertices[i], vertices[(i + 1) % vertices.Length], borderColor, borderThickness);
            HudIcons.FillCircle(spriteBatch, _pixel, vertices[i], 1.2f, Color.Black * 0.5f);
        }

        // Same "bolted on, not painted on" drop shadow DrawPanel's plain rect gets, just clipped
        // short of the two chamfered corners it would otherwise poke past.
        spriteBatch.Draw(_pixel, new Rectangle(rect.X + chamfer, rect.Bottom, rect.Width - chamfer * 2, 2), Color.Black * 0.28f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right, rect.Y + chamfer, 2, rect.Height - chamfer * 2), Color.Black * 0.28f);
    }

    // A small dark backing sized to the text, drawn just under it - keeps a label legible over
    // whatever glow/screen/texture happens to sit behind it, rather than tuning every glow's own
    // brightness down to the point of looking dead just to keep text readable on top of it.
    // A device's baked face, plus the two things that have no business being baked into it: the
    // outline, which says whether this machine's panel is currently open, and the drop shadow that
    // sits it on the deck rather than on top of the picture.
    private void DrawDeviceFace(SpriteBatch spriteBatch, Rectangle rect, DeviceSkin.Face face, bool lit,
        Color borderColor, float borderThickness)
    {
        spriteBatch.Draw(_deviceSkin.Get(face, rect.Width, rect.Height, lit), rect, Color.White);

        var chamfer = Math.Max(2, Math.Min(rect.Width, rect.Height) / 6);
        Span<Vector2> vertices = stackalloc Vector2[]
        {
            new(rect.X + chamfer, rect.Y), new(rect.Right - chamfer, rect.Y),
            new(rect.Right, rect.Y + chamfer), new(rect.Right, rect.Bottom - chamfer),
            new(rect.Right - chamfer, rect.Bottom), new(rect.X + chamfer, rect.Bottom),
            new(rect.X, rect.Bottom - chamfer), new(rect.X, rect.Y + chamfer),
        };
        for (var i = 0; i < vertices.Length; i++)
            HudIcons.DrawLine(spriteBatch, _pixel, vertices[i], vertices[(i + 1) % vertices.Length], borderColor, borderThickness);

        spriteBatch.Draw(_pixel, new Rectangle(rect.X + chamfer, rect.Bottom, rect.Width - chamfer * 2, 2), Color.Black * 0.28f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right, rect.Y + chamfer, 2, rect.Height - chamfer * 2), Color.Black * 0.28f);
    }

    // A device's name, above the machine rather than painted across it.
    //
    // On the face it fought with the hardware: the painted band, the dials and the text all wanted
    // the same few pixels, and on a 24px device the dark backing plate alone covered a third of the
    // face. Moving it off also frees the name from having to fit - which is why these went from
    // "O2", "Э", "Б" to the actual words. A single letter is an abbreviation the player has to
    // learn; a word is just the name of the thing.
    private void DrawDeviceLabel(SpriteBatch spriteBatch, Rectangle rect, string text, float scale = 0.5f)
    {
        var size = _font.MeasureString(text) * scale;
        // Six pixels of clearance, which also steps over a console's hood - that sits three pixels
        // proud of the plate and is four deep, so anything tighter would have the name resting on it.
        var position = new Vector2(rect.Center.X - size.X / 2f, rect.Y - size.Y - 6f);
        DrawLabelBacking(spriteBatch, text, position, scale);
        spriteBatch.DrawString(_font, text, position, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawLabelBacking(SpriteBatch spriteBatch, string text, Vector2 position, float scale)
    {
        var size = _font.MeasureString(text) * scale;
        spriteBatch.Draw(_pixel, new Rectangle((int)position.X - 2, (int)position.Y - 1, (int)size.X + 4, (int)size.Y + 2), Color.Black * 0.55f);
    }

    // A lit display inset into the upper half of a device face. Deliberately drawn *brighter* than
    // anything around it: ScenePost credits a lit pixel with extra brightness before testing it
    // against the bloom threshold, so a screen that clears that threshold is what actually reads as
    // "this machine is powered" rather than "this rectangle is painted a lighter colour".
    //
    // `powered` is passed true by every caller today. Wiring it to real per-device power state is
    // the next step and belongs with the callers, not here.
    private void DrawScreen(SpriteBatch spriteBatch, Rectangle panel, Color glow, bool powered)
    {
        // Too small to read as a screen at all - better nothing than a two-pixel smear.
        if (panel.Width < 16 || panel.Height < 14)
            return;

        var screen = new Rectangle(panel.X + 4, panel.Y + 4, panel.Width - 8, (int)(panel.Height * 0.5f) - 2);
        spriteBatch.Draw(_pixel, new Rectangle(screen.X - 1, screen.Y - 1, screen.Width + 2, screen.Height + 2), Color.Black * 0.8f);

        // Only a little white mixed in: the screen has to clear the bloom threshold, but pushing it
        // further just washes the colour out and every console ends up the same white blob.
        var face = powered ? Color.Lerp(glow, Color.White, 0.15f) : glow * 0.10f;
        spriteBatch.Draw(_pixel, screen, face);

        if (!powered)
            return;

        // Scanlines: period 3 so they survive at small sizes, and they hand the relief pass a
        // gradient to work with on what would otherwise be another flat fill.
        for (var y = screen.Y + 1; y < screen.Bottom; y += 3)
            spriteBatch.Draw(_pixel, new Rectangle(screen.X, y, screen.Width, 1), Color.Black * 0.22f);

        // The brightest point on the device, top-left, where the glass would catch the room light.
        spriteBatch.Draw(_pixel, new Rectangle(screen.X + 1, screen.Y + 1, Math.Max(2, screen.Width / 4), 1), Color.White * 0.40f);
    }


    // Deterministic per-position noise for the scorch below: a burnt machine has to look the same
    // from frame to frame, not crawl.
    private static float Splat(int seed)
    {
        var n = seed * 374761393;
        n = (n ^ (n >> 13)) * 1274126177;
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    // Soot over a damaged device. Damage used to be painting the face red, which reads as a red box;
    // what reads as a machine that has been on fire is the machine still being itself, with burns
    // on it.
    private void DrawScorch(SpriteBatch spriteBatch, Rectangle rect)
    {
        var key = (rect.X * 73856093) ^ (rect.Y * 19349663);
        for (var i = 0; i < 8; i++)
        {
            var w = 2 + (int)(Splat(key + i * 3) * (rect.Width / 3f));
            var h = 2 + (int)(Splat(key + i * 3 + 1) * (rect.Height / 4f));
            var x = rect.X + (int)(Splat(key + i * 3 + 2) * Math.Max(1, rect.Width - w));
            var y = rect.Y + (int)(Splat(key + i * 7) * Math.Max(1, rect.Height - h));
            spriteBatch.Draw(_pixel, new Rectangle(x, y, w, h), Color.Black * (0.16f + Splat(key + i * 11) * 0.20f));
        }
    }

    // Cooling slots across the lower half of a face - each a dark slot with a lit lip beneath it,
    // so the relief pass sees a real edge instead of a painted line.
    private void DrawVents(SpriteBatch spriteBatch, Rectangle rect, int count)
    {
        var w = Math.Max(4, rect.Width - 10);
        var x = rect.X + (rect.Width - w) / 2;
        for (var i = 0; i < count; i++)
        {
            var y = rect.Bottom - 5 - i * 4;
            if (y <= rect.Y + rect.Height / 2)
                return;
            spriteBatch.Draw(_pixel, new Rectangle(x, y, w, 2), Color.Black * 0.55f);
            spriteBatch.Draw(_pixel, new Rectangle(x, y + 2, w, 1), Color.White * 0.20f);
        }
    }

    // A hood over the top edge of a console: what makes it read as something you stand at rather
    // than a plate bolted to the wall. Every device in this game was the same rectangle before
    // these, and silhouette is what the eye sorts objects by first.
    private void DrawHood(SpriteBatch spriteBatch, Rectangle rect)
    {
        var hood = new Rectangle(rect.X - 2, rect.Y - 3, rect.Width + 4, 4);
        spriteBatch.Draw(_pixel, hood, new Color(38, 42, 52));
        spriteBatch.Draw(_pixel, new Rectangle(hood.X, hood.Y, hood.Width, 1), Color.White * 0.22f);
        spriteBatch.Draw(_pixel, new Rectangle(hood.X, hood.Bottom - 1, hood.Width, 1), Color.Black * 0.5f);
    }

    // A grab handle down one side of a locker.
    private void DrawHandle(SpriteBatch spriteBatch, Rectangle rect)
    {
        var x = rect.Right - 5;
        var y = rect.Y + rect.Height / 3;
        var h = Math.Max(4, rect.Height / 3);
        spriteBatch.Draw(_pixel, new Rectangle(x, y, 2, h), new Color(150, 155, 165));
        spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, h), Color.White * 0.30f);
        spriteBatch.Draw(_pixel, new Rectangle(x - 1, y + h, 4, 1), Color.Black * 0.5f);
    }

    private void DrawRivets(SpriteBatch spriteBatch, Rectangle rect) => DrawRivets(spriteBatch, _pixel, rect);

    internal static void DrawRivets(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        const int inset = 3;
        const int size = 2;
        var color = Color.Black * 0.5f;
        foreach (var (x, y) in new[]
                 {
                     (rect.X + inset, rect.Y + inset), (rect.Right - inset - size, rect.Y + inset),
                     (rect.X + inset, rect.Bottom - inset - size), (rect.Right - inset - size, rect.Bottom - inset - size),
                 })
        {
            spriteBatch.Draw(pixel, new Rectangle(x, y, size, size), color);
            // One lit pixel on the head: a flat dark square is a hole, a dark square with a
            // highlight on it is a rivet - and the relief pass needs both flanks to see a bump.
            spriteBatch.Draw(pixel, new Rectangle(x, y, 1, 1), Color.White * 0.38f);
        }
    }

    // Alternating yellow/black hazard tape (SS13/Barotrauma convention for anything dangerous:
    // airlocks, breached hull) - plain vertical stripes rather than true diagonals since there's
    // no clipping/scissor rect available to cut a rotated stripe to the target shape.
    private void DrawHazardStripes(SpriteBatch spriteBatch, Rectangle rect, bool horizontal)
    {
        const int stripeSize = 5;
        if (horizontal)
        {
            for (var x = rect.X; x < rect.Right; x += stripeSize)
            {
                var w = Math.Min(stripeSize, rect.Right - x);
                var stripe = ((x - rect.X) / stripeSize) % 2 == 0 ? Color.Gold : Color.Black;
                spriteBatch.Draw(_pixel, new Rectangle(x, rect.Y, w, rect.Height), stripe * 0.9f);
            }
        }
        else
        {
            for (var y = rect.Y; y < rect.Bottom; y += stripeSize)
            {
                var h = Math.Min(stripeSize, rect.Bottom - y);
                var stripe = ((y - rect.Y) / stripeSize) % 2 == 0 ? Color.Gold : Color.Black;
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, y, rect.Width, h), stripe * 0.9f);
            }
        }
    }
}
