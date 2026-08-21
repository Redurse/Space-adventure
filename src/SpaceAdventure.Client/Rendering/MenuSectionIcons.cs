using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

public enum MenuSection
{
    /// <summary>Tutorial, continue, new game - everything that starts a run.</summary>
    Campaign,
    /// <summary>Host and join.</summary>
    Network,
    /// <summary>Ship editor and callsign - the things you shape before you fly.</summary>
    Shipyard,
    /// <summary>Settings, credits, quit.</summary>
    Systems,
}

// One plate per section of the main menu, sitting where a heading would.
//
// These are not the button icons made bigger. A button tile is twenty pixels across and has to be
// read while the eye is already moving down a list, so it can hold one idea. A section plate is four
// times that and is looked at directly, which changes what belongs on it: not a clearer symbol, but a
// denser one - an instrument face with several things happening on it at once.
//
// The rule followed throughout is that every mark has to be something, not decoration. The scale bar
// beside the planet is a scale bar with real ticks; the traces on the shipyard plate route round
// corners and end on pads; the scope has range rings at sensible radii. Detail invented as texture
// looks like noise the moment anyone looks closely, which is exactly what these are for.
public static class MenuSectionIcons
{
    private static Vector2 At(Rectangle b, float u, float v) => new(b.X + b.Width * u, b.Y + b.Height * v);

    public static void Draw(SpriteBatch batch, Texture2D pixel, MenuSection section, Rectangle box, Color colour)
    {
        var s = box.Width / 64f;                       // these are drawn against a 64-unit square
        var thin = MathF.Max(1f, 1.0f * s);
        var hair = MathF.Max(1f, 0.7f * s);
        var dim = colour * 0.5f;
        var faint = colour * 0.25f;

        Housing(batch, pixel, box, colour, s);

        switch (section)
        {
            case MenuSection.Campaign: Campaign(batch, pixel, box, colour, dim, faint, s, thin, hair); break;
            case MenuSection.Network: Network(batch, pixel, box, colour, dim, faint, s, thin, hair); break;
            case MenuSection.Shipyard: Shipyard(batch, pixel, box, colour, dim, faint, s, thin, hair); break;
            case MenuSection.Systems: Systems(batch, pixel, box, colour, dim, faint, s, thin, hair); break;
        }
    }

    // The plate the drawing sits on: a faint field, a frame that is only drawn at the corners, and
    // ticks along the bottom. Corner brackets rather than a closed rectangle because a full border
    // boxes the art in and competes with it - the corners say "instrument" just as clearly and leave
    // the drawing the whole square.
    private static void Housing(SpriteBatch batch, Texture2D pixel, Rectangle box, Color colour, float s)
    {
        batch.Draw(pixel, box, new Color(18, 34, 33) * 0.55f);

        var arm = (int)MathF.Max(4f, box.Width * 0.22f);
        var w = (int)MathF.Max(1f, s);
        var c = colour * 0.85f;
        foreach (var (cx, cy, sx, sy) in new[] { (box.Left, box.Top, 1, 1), (box.Right - w, box.Top, -1, 1),
                                                  (box.Left, box.Bottom - w, 1, -1), (box.Right - w, box.Bottom - w, -1, -1) })
        {
            batch.Draw(pixel, new Rectangle(sx > 0 ? cx : cx - arm + w, cy, arm, w), c);
            batch.Draw(pixel, new Rectangle(cx, sy > 0 ? cy : cy - arm + w, w, arm), c);
        }

        for (var i = 0; i < 7; i++)
        {
            var x = box.X + (int)(box.Width * (0.14f + i * 0.12f));
            var h = (int)((i % 3 == 0 ? 4f : 2.5f) * s);
            batch.Draw(pixel, new Rectangle(x, box.Bottom + (int)(2 * s), (int)MathF.Max(1f, s * 0.8f), h), colour * 0.45f);
        }
    }

    // A world with something in orbit around it, and the instruments you would read it with. This is
    // the section that starts a run, and a run starts by arriving somewhere.
    private static void Campaign(SpriteBatch batch, Texture2D pixel, Rectangle b, Color colour, Color dim, Color faint, float s, float thin, float hair)
    {
        var centre = At(b, 0.38f, 0.58f);
        var radius = b.Width * 0.24f;

        // The globe: a disc, latitude arcs that flatten towards the poles, and one meridian. The
        // flattening is the whole reason it reads as a sphere and not a circle with lines on it.
        HudIcons.FillCircle(batch, pixel, centre, radius, new Color(30, 58, 60) * 0.9f);
        HudIcons.DrawRingArc(batch, pixel, centre, radius, 0f, 360f, colour, 24, thin);
        for (var i = -2; i <= 2; i++)
        {
            var t = i / 3f;
            var y = centre.Y + radius * t;
            var half = radius * MathF.Sqrt(MathF.Max(0f, 1f - t * t));
            HudIcons.DrawLine(batch, pixel, new Vector2(centre.X - half, y), new Vector2(centre.X + half, y),
                i == 0 ? dim : faint, hair);
        }
        HudIcons.DrawRingArc(batch, pixel, centre, radius, 90f, 270f, faint, 14, hair);

        // Terminator: the globe is lit from the upper right, like the menu's own star.
        for (var i = 0; i < 14; i++)
        {
            var a = MathHelper.Lerp(-1.5f, 1.5f, i / 13f);
            var y = centre.Y + MathF.Sin(a) * radius;
            var x = centre.X - MathF.Cos(a) * radius * 0.42f;
            batch.Draw(pixel, new Rectangle((int)x, (int)y, (int)MathF.Max(1f, s), (int)MathF.Max(1f, s)), dim);
        }

        // The orbit, drawn as a flattened ellipse so it reads as a ring seen at an angle, with the
        // near half brighter than the far half - that difference is what puts it around the planet
        // rather than on top of it.
        for (var i = 0; i < 48; i++)
        {
            var a0 = i / 48f * MathF.Tau;
            var a1 = (i + 1) / 48f * MathF.Tau;
            var near = MathF.Sin(a0) > 0f;
            HudIcons.DrawLine(batch, pixel,
                centre + new Vector2(MathF.Cos(a0) * radius * 1.45f, MathF.Sin(a0) * radius * 0.44f),
                centre + new Vector2(MathF.Cos(a1) * radius * 1.45f, MathF.Sin(a1) * radius * 0.44f),
                near ? colour * 0.8f : faint, hair);
        }

        // The ship on that orbit, with its heading marker.
        var shipAt = centre + new Vector2(radius * 1.45f * MathF.Cos(-0.7f), radius * 0.44f * MathF.Sin(-0.7f));
        Primitives.FillTriangle(batch, pixel,
            shipAt + new Vector2(3.6f * s, -1.2f * s), shipAt + new Vector2(-2.4f * s, -3f * s),
            shipAt + new Vector2(-2.4f * s, 1.6f * s), colour);
        HudIcons.DrawLine(batch, pixel, shipAt + new Vector2(-3f * s, 0f), shipAt + new Vector2(-8f * s, 2f * s), faint, hair);

        // A scale bar down the right edge, with real ticks and a caption block.
        var barX = b.X + b.Width * 0.86f;
        HudIcons.DrawLine(batch, pixel, new Vector2(barX, b.Y + b.Height * 0.20f), new Vector2(barX, b.Y + b.Height * 0.80f), dim, hair);
        for (var i = 0; i <= 6; i++)
        {
            var y = MathHelper.Lerp(b.Y + b.Height * 0.20f, b.Y + b.Height * 0.80f, i / 6f);
            var len = (i % 2 == 0 ? 5f : 3f) * s;
            HudIcons.DrawLine(batch, pixel, new Vector2(barX - len, y), new Vector2(barX, y), i % 2 == 0 ? dim : faint, hair);
        }

        // Two stars, because an empty corner on an instrument face reads as an unfinished one.
        batch.Draw(pixel, new Rectangle((int)(b.X + b.Width * 0.16f), (int)(b.Y + b.Height * 0.16f), (int)MathF.Max(1f, s), (int)MathF.Max(1f, s)), colour * 0.7f);
        batch.Draw(pixel, new Rectangle((int)(b.X + b.Width * 0.63f), (int)(b.Y + b.Height * 0.13f), (int)MathF.Max(1f, s), (int)MathF.Max(1f, s)), colour * 0.45f);
    }

    // A relay: one hub, several stations hanging off it, and traffic on the links. The hexagon is
    // borrowed from the reference deliberately - it is the shape that says "network cell" without
    // needing a caption.
    private static void Network(SpriteBatch batch, Texture2D pixel, Rectangle b, Color colour, Color dim, Color faint, float s, float thin, float hair)
    {
        var hub = At(b, 0.5f, 0.5f);
        var r = b.Width * 0.16f;

        Hexagon(batch, pixel, hub, r, colour, thin);
        Hexagon(batch, pixel, hub, r * 0.55f, dim, hair);
        HudIcons.FillCircle(batch, pixel, hub, r * 0.22f, colour);

        // Five outstations at unequal radii - evenly spaced spokes read as a snowflake, and a network
        // that looks symmetrical looks decorative.
        var nodes = new[] { (-1.15f, 0.62f), (0.15f, 0.72f), (1.6f, 0.55f), (2.5f, 0.70f), (3.9f, 0.60f) };
        for (var i = 0; i < nodes.Length; i++)
        {
            var (angle, reach) = nodes[i];
            var p = hub + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * b.Width * reach * 0.5f;

            // The link, drawn as a dashed run with one packet lit on it.
            const int dashes = 7;
            for (var d = 0; d < dashes; d++)
            {
                if (d % 2 == 1)
                    continue;
                var t0 = d / (float)dashes;
                var t1 = (d + 0.8f) / dashes;
                HudIcons.DrawLine(batch, pixel, Vector2.Lerp(hub, p, t0), Vector2.Lerp(hub, p, t1), faint, hair);
            }
            HudIcons.FillCircle(batch, pixel, Vector2.Lerp(hub, p, 0.62f), 1.3f * s, colour * 0.8f);

            // Two of them are square terminals, the rest are rings: a set of identical nodes is a
            // pattern, and a pattern is the thing that stops looking like equipment.
            if (i % 2 == 0)
            {
                var side = (int)(4.4f * s);
                ShipRenderer.DrawRectOutline(batch, pixel, new Rectangle((int)p.X - side / 2, (int)p.Y - side / 2, side, side), colour, (int)MathF.Max(1f, s));
                batch.Draw(pixel, new Rectangle((int)p.X - (int)(1.2f * s), (int)p.Y - (int)(1.2f * s), (int)(2.4f * s), (int)(2.4f * s)), dim);
            }
            else
            {
                HudIcons.DrawRingArc(batch, pixel, p, 2.8f * s, 0f, 360f, colour, 10, hair);
            }
        }

        // One station is calling: a pair of expanding rings on the top-right node.
        var caller = hub + new Vector2(MathF.Cos(1.6f), MathF.Sin(1.6f)) * b.Width * 0.55f * 0.5f;
        HudIcons.DrawRingArc(batch, pixel, caller, 5.5f * s, -70f, 70f, dim, 9, hair);
        HudIcons.DrawRingArc(batch, pixel, caller, 8f * s, -55f, 55f, faint, 9, hair);
    }

    // A board being routed: traces that turn at right angles, pads they land on, a chip and its vias.
    // Everything you build a ship out of, in the language the ship's own panels are drawn in.
    private static void Shipyard(SpriteBatch batch, Texture2D pixel, Rectangle b, Color colour, Color dim, Color faint, float s, float thin, float hair)
    {
        // The chip.
        var chip = new Rectangle((int)(b.X + b.Width * 0.36f), (int)(b.Y + b.Height * 0.38f),
            (int)(b.Width * 0.30f), (int)(b.Height * 0.24f));
        batch.Draw(pixel, chip, new Color(24, 46, 46) * 0.9f);
        ShipRenderer.DrawRectOutline(batch, pixel, chip, colour, (int)MathF.Max(1f, s));
        // Pin one, marked the way it is on a real package.
        HudIcons.FillCircle(batch, pixel, new Vector2(chip.X + 3f * s, chip.Y + 3f * s), 1.2f * s, dim);
        for (var i = 0; i < 4; i++)
        {
            var x = chip.X + chip.Width * (i + 0.5f) / 4f;
            HudIcons.DrawLine(batch, pixel, new Vector2(x, chip.Y), new Vector2(x, chip.Y - 3.5f * s), dim, hair);
            HudIcons.DrawLine(batch, pixel, new Vector2(x, chip.Bottom), new Vector2(x, chip.Bottom + 3.5f * s), dim, hair);
        }

        // Four traces, each routed with a right-angle turn and each ending on a pad. Routed, not
        // drawn: a straight line from A to B is a wire, and a wire that turns is a board.
        var runs = new[]
        {
            (0.08f, 0.20f, 0.30f, 0.34f),
            (0.08f, 0.74f, 0.34f, 0.62f),
            (0.92f, 0.26f, 0.68f, 0.42f),
            (0.92f, 0.80f, 0.66f, 0.60f),
        };
        foreach (var (x0, y0, x1, y1) in runs)
        {
            var a = At(b, x0, y0);
            var corner = At(b, x1, y0);
            var end = At(b, x1, y1);
            HudIcons.DrawLine(batch, pixel, a, corner, colour * 0.75f, hair);
            HudIcons.DrawLine(batch, pixel, corner, end, colour * 0.75f, hair);
            HudIcons.DrawRingArc(batch, pixel, a, 2.6f * s, 0f, 360f, colour, 10, hair);
            HudIcons.FillCircle(batch, pixel, a, 1.0f * s, dim);
        }

        // A cluster of vias, and a dashed outline of something not yet placed - the board is still
        // being worked on, which is what an editor is.
        for (var i = 0; i < 5; i++)
        {
            var p = At(b, 0.20f + i * 0.035f, 0.47f);
            HudIcons.FillCircle(batch, pixel, p, 1.1f * s, faint);
        }
        var ghost = new Rectangle((int)(b.X + b.Width * 0.70f), (int)(b.Y + b.Height * 0.68f),
            (int)(b.Width * 0.18f), (int)(b.Height * 0.16f));
        for (var i = 0; i < ghost.Width; i += (int)MathF.Max(2f, 3f * s))
        {
            batch.Draw(pixel, new Rectangle(ghost.X + i, ghost.Y, (int)MathF.Max(1f, s), (int)MathF.Max(1f, s)), faint);
            batch.Draw(pixel, new Rectangle(ghost.X + i, ghost.Bottom, (int)MathF.Max(1f, s), (int)MathF.Max(1f, s)), faint);
        }
        for (var i = 0; i < ghost.Height; i += (int)MathF.Max(2f, 3f * s))
        {
            batch.Draw(pixel, new Rectangle(ghost.X, ghost.Y + i, (int)MathF.Max(1f, s), (int)MathF.Max(1f, s)), faint);
            batch.Draw(pixel, new Rectangle(ghost.Right, ghost.Y + i, (int)MathF.Max(1f, s), (int)MathF.Max(1f, s)), faint);
        }
    }

    // A scope: range rings, a sweep that has just gone past two contacts, and a trace along the
    // bottom. This is the housekeeping section, and housekeeping on a ship is watching dials.
    private static void Systems(SpriteBatch batch, Texture2D pixel, Rectangle b, Color colour, Color dim, Color faint, float s, float thin, float hair)
    {
        var centre = At(b, 0.5f, 0.42f);
        var r = b.Width * 0.30f;

        HudIcons.FillCircle(batch, pixel, centre, r, new Color(20, 44, 42) * 0.8f);
        HudIcons.DrawRingArc(batch, pixel, centre, r, 0f, 360f, colour, 26, thin);
        HudIcons.DrawRingArc(batch, pixel, centre, r * 0.66f, 0f, 360f, faint, 20, hair);
        HudIcons.DrawRingArc(batch, pixel, centre, r * 0.33f, 0f, 360f, faint, 14, hair);
        HudIcons.DrawLine(batch, pixel, centre - new Vector2(r, 0), centre + new Vector2(r, 0), faint, hair);
        HudIcons.DrawLine(batch, pixel, centre - new Vector2(0, r), centre + new Vector2(0, r), faint, hair);

        // Bearing ticks round the rim, longer every ninety degrees.
        for (var i = 0; i < 24; i++)
        {
            var a = i / 24f * MathF.Tau;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var len = i % 6 == 0 ? 4.5f * s : 2.5f * s;
            HudIcons.DrawLine(batch, pixel, centre + dir * (r - len), centre + dir * r, i % 6 == 0 ? dim : faint, hair);
        }

        // The sweep, with a wedge of afterglow trailing it. The wedge is what makes it a sweep in a
        // still picture - a bare line is just a hand on a clock.
        const float sweepAngle = -0.9f;
        for (var i = 0; i < 12; i++)
        {
            var a = sweepAngle - i * 0.075f;
            var fade = (1f - i / 12f) * 0.5f;
            HudIcons.DrawLine(batch, pixel, centre,
                centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r, colour * fade, hair);
        }
        HudIcons.DrawLine(batch, pixel, centre,
            centre + new Vector2(MathF.Cos(sweepAngle), MathF.Sin(sweepAngle)) * r, colour, thin);

        // Two contacts, one of them just found and ringed.
        var blip = centre + new Vector2(MathF.Cos(sweepAngle - 0.25f), MathF.Sin(sweepAngle - 0.25f)) * r * 0.62f;
        HudIcons.FillCircle(batch, pixel, blip, 1.8f * s, colour);
        HudIcons.DrawRingArc(batch, pixel, blip, 4f * s, 0f, 360f, dim, 10, hair);
        HudIcons.FillCircle(batch, pixel, centre + new Vector2(-r * 0.45f, r * 0.30f), 1.3f * s, dim);

        // A trace along the bottom of the plate, because a scope is never the only readout.
        var baseY = b.Y + b.Height * 0.88f;
        var prev = new Vector2(b.X + b.Width * 0.10f, baseY);
        for (var i = 1; i <= 16; i++)
        {
            var t = i / 16f;
            var x = MathHelper.Lerp(b.X + b.Width * 0.10f, b.X + b.Width * 0.90f, t);
            var wave = MathF.Sin(t * 9.5f) * 0.6f + MathF.Sin(t * 21f) * 0.25f;
            var p = new Vector2(x, baseY - wave * b.Height * 0.06f);
            HudIcons.DrawLine(batch, pixel, prev, p, dim, hair);
            prev = p;
        }
    }

    private static void Hexagon(SpriteBatch batch, Texture2D pixel, Vector2 centre, float radius, Color colour, float thickness)
    {
        for (var i = 0; i < 6; i++)
        {
            var a0 = i / 6f * MathF.Tau;
            var a1 = (i + 1) / 6f * MathF.Tau;
            HudIcons.DrawLine(batch, pixel,
                centre + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius,
                centre + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius, colour, thickness);
        }
    }
}
