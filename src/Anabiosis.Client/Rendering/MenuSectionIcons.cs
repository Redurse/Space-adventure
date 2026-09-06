using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

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

    // The single warm element each plate is allowed. A cold drawing with one hot point in it has
    // somewhere for the eye to land; two hot points and it has none. It is also the amber the hover
    // bar uses, so the frame holds one warm colour rather than two unrelated ones.
    private static readonly Color Accent = new(240, 176, 62);

    public static void Draw(SpriteBatch batch, Texture2D pixel, MenuSection section, Rectangle box, Color colour,
        float time = 0f)
    {
        var s = box.Width / 64f;                       // these are drawn against a 64-unit square
        // Three weights, not one. In a drawing the weight of a line is information: heavy is the
        // object, medium is its structure, hairline is construction and dimension. Everything at one
        // pixel is a tracing, and the eye has nothing to sort.
        var heavy = MathF.Max(1f, 1.7f * s);
        var thin = MathF.Max(1f, 1.0f * s);
        var hair = MathF.Max(1f, 0.6f * s);
        var dim = colour * 0.5f;
        var faint = colour * 0.25f;
        var warm = Accent * (colour.A / 255f);

        ScreenField(batch, pixel, box, colour, s);
        Housing(batch, pixel, box, colour, s);

        switch (section)
        {
            case MenuSection.Campaign: Campaign(batch, pixel, box, colour, dim, faint, warm, s, heavy, thin, hair, time); break;
            case MenuSection.Network: Network(batch, pixel, box, colour, dim, faint, warm, s, heavy, thin, hair, time); break;
            case MenuSection.Shipyard: Shipyard(batch, pixel, box, colour, dim, faint, warm, s, heavy, thin, hair, time); break;
            case MenuSection.Systems: Systems(batch, pixel, box, colour, dim, faint, warm, s, heavy, thin, hair, time); break;
        }
    }

    // The plate the drawing sits on: a faint field, a frame that is only drawn at the corners, and
    // ticks along the bottom. Corner brackets rather than a closed rectangle because a full border
    // boxes the art in and competes with it - the corners say "instrument" just as clearly and leave
    // the drawing the whole square.
    // The plate as a recessed screen rather than a flat wash. Every other surface in this menu now has
    // a material - the panel is painted, the hull is plated - and these four were sitting on a plain
    // translucent rectangle, which is what made them read as stickers laid on top.
    //
    // The language is the one the ship's own instrument housings use: a sunken field, scanlines, a
    // sheen where the glass catches the light, and a lit inner lip on the two edges facing it.
    private static void ScreenField(SpriteBatch batch, Texture2D pixel, Rectangle box, Color colour, float s)
    {
        // The field, darker towards the bottom - a recess is not evenly lit.
        var rows = MathF.Max(1f, s);
        for (var y = box.Y; y < box.Bottom; y += (int)rows)
        {
            var t = (y - box.Y) / (float)box.Height;
            var shade = 1f - t * 0.45f;
            batch.Draw(pixel, new Rectangle(box.X, y, box.Width, (int)rows),
                new Color((int)(16 * shade), (int)(32 * shade), (int)(31 * shade)) * 0.92f);
        }

        // Scanlines. Every third row, and barely there: at any strength worth noticing they stop
        // being a screen and start being a pattern over the drawing.
        for (var y = box.Y; y < box.Bottom; y += (int)MathF.Max(2f, 3f * s))
            batch.Draw(pixel, new Rectangle(box.X, y, box.Width, (int)MathF.Max(1f, s * 0.7f)), Color.Black * 0.16f);

        // The sheen: one diagonal band across the upper left, wide and very weak. This is the single
        // mark that says "glass" - without it a dark rectangle is a hole.
        for (var i = 0; i < box.Width; i++)
        {
            var x = box.X + i;
            var top = box.Y + (int)(i * 0.55f) - (int)(box.Height * 0.42f);
            var height = (int)(box.Height * 0.34f);
            if (top + height <= box.Y || top >= box.Bottom)
                continue;
            var y0 = Math.Max(box.Y, top);
            var y1 = Math.Min(box.Bottom, top + height);
            batch.Draw(pixel, new Rectangle(x, y0, 1, y1 - y0), colour * 0.045f);
        }

        // The lip: lit along the top and left where the light comes from, shadowed opposite.
        var w = (int)MathF.Max(1f, s);
        batch.Draw(pixel, new Rectangle(box.X, box.Y, box.Width, w), colour * 0.22f);
        batch.Draw(pixel, new Rectangle(box.X, box.Y, w, box.Height), colour * 0.16f);
        batch.Draw(pixel, new Rectangle(box.X, box.Bottom - w, box.Width, w), Color.Black * 0.45f);
        batch.Draw(pixel, new Rectangle(box.Right - w, box.Y, w, box.Height), Color.Black * 0.35f);
    }

    private static void Housing(SpriteBatch batch, Texture2D pixel, Rectangle box, Color colour, float s)
    {

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
    private static void Campaign(SpriteBatch batch, Texture2D pixel, Rectangle b, Color colour, Color dim, Color faint, Color warm, float s, float heavy, float thin, float hair, float time)
    {
        var centre = At(b, 0.36f, 0.58f);
        var radius = b.Width * 0.23f;

        // The globe: a disc, latitude arcs that flatten towards the poles, and one meridian. The
        // flattening is the whole reason it reads as a sphere and not a circle with lines on it.
        HudIcons.FillCircle(batch, pixel, centre, radius, new Color(30, 58, 60) * 0.9f);
        HudIcons.DrawRingArc(batch, pixel, centre, radius, 0f, 360f, colour, 26, heavy);
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
        for (var i = 0; i < 16; i++)
        {
            var a = MathHelper.Lerp(-1.5f, 1.5f, i / 15f);
            var y = centre.Y + MathF.Sin(a) * radius;
            var x = centre.X - MathF.Cos(a) * radius * 0.42f;
            batch.Draw(pixel, new Rectangle((int)x, (int)y, (int)MathF.Max(1f, s), (int)MathF.Max(1f, s)), dim);
        }

        // The orbit, flattened so it reads as a ring seen at an angle, near half brighter than far -
        // that difference is what puts it around the planet rather than on top of it.
        for (var i = 0; i < 56; i++)
        {
            var a0 = i / 56f * MathF.Tau;
            var a1 = (i + 1) / 56f * MathF.Tau;
            var near = MathF.Sin(a0) > 0f;
            HudIcons.DrawLine(batch, pixel,
                centre + new Vector2(MathF.Cos(a0) * radius * 1.5f, MathF.Sin(a0) * radius * 0.45f),
                centre + new Vector2(MathF.Cos(a1) * radius * 1.5f, MathF.Sin(a1) * radius * 0.45f),
                near ? colour * 0.8f : faint, hair);
        }

        // Apoapsis and periapsis, ticked across the orbit the way a plot marks them.
        foreach (var mark in new[] { 0f, MathF.PI })
        {
            var p = centre + new Vector2(MathF.Cos(mark) * radius * 1.5f, MathF.Sin(mark) * radius * 0.45f);
            HudIcons.DrawLine(batch, pixel, p + new Vector2(0f, -2.5f * s), p + new Vector2(0f, 2.5f * s), dim, hair);
        }

        // The ship, going round. One turn every ninety seconds - slow enough that it is never a
        // distraction, fast enough that anyone who sits on this screen sees it has moved.
        var angle = -0.7f + time * (MathF.Tau / 90f);
        var shipAt = centre + new Vector2(MathF.Cos(angle) * radius * 1.5f, MathF.Sin(angle) * radius * 0.45f);
        var ahead = centre + new Vector2(MathF.Cos(angle + 0.12f) * radius * 1.5f, MathF.Sin(angle + 0.12f) * radius * 0.45f);
        var facing = Vector2.Normalize(ahead - shipAt);
        var side = new Vector2(-facing.Y, facing.X);
        Primitives.FillTriangle(batch, pixel,
            shipAt + facing * 2.4f * s, shipAt - facing * 1.5f * s + side * 1.5f * s,
            shipAt - facing * 1.5f * s - side * 1.5f * s, warm);
        // Its wake, four dashes fading out behind it.
        for (var i = 1; i <= 4; i++)
        {
            var a = angle - i * 0.16f;
            var p = centre + new Vector2(MathF.Cos(a) * radius * 1.5f, MathF.Sin(a) * radius * 0.45f);
            HudIcons.FillCircle(batch, pixel, p, 0.9f * s, warm * (0.5f - i * 0.1f));
        }

        // A moon on a wider, slower orbit - the system has more than one thing in it.
        var moonAngle = 1.9f - time * (MathF.Tau / 240f);
        var moonAt = centre + new Vector2(MathF.Cos(moonAngle) * radius * 2.1f, MathF.Sin(moonAngle) * radius * 0.72f);
        HudIcons.FillCircle(batch, pixel, moonAt, 1.6f * s, dim);

        // A scale bar down the right edge, with real ticks.
        var barX = b.X + b.Width * 0.88f;
        HudIcons.DrawLine(batch, pixel, new Vector2(barX, b.Y + b.Height * 0.18f), new Vector2(barX, b.Y + b.Height * 0.74f), dim, hair);
        for (var i = 0; i <= 6; i++)
        {
            var y = MathHelper.Lerp(b.Y + b.Height * 0.18f, b.Y + b.Height * 0.74f, i / 6f);
            var len = (i % 2 == 0 ? 5f : 3f) * s;
            HudIcons.DrawLine(batch, pixel, new Vector2(barX - len, y), new Vector2(barX, y), i % 2 == 0 ? dim : faint, hair);
        }

        // A readout block: three bars of unequal length standing in for a line of figures. Actual
        // digits at this size come out as smudges, and a smudge reads as a mistake where a bar reads
        // as text too small to make out - which is what it would be on a real panel.
        for (var i = 0; i < 3; i++)
        {
            var y = b.Y + b.Height * (0.80f + i * 0.055f);
            var w = b.Width * (i == 1 ? 0.22f : i == 0 ? 0.30f : 0.16f);
            batch.Draw(pixel, new Rectangle((int)(b.X + b.Width * 0.12f), (int)y, (int)w, (int)MathF.Max(1f, s * 1.1f)),
                i == 0 ? dim : faint);
        }

        // Two stars, because an empty corner on an instrument face reads as an unfinished one.
        batch.Draw(pixel, new Rectangle((int)(b.X + b.Width * 0.16f), (int)(b.Y + b.Height * 0.14f), (int)MathF.Max(1f, s), (int)MathF.Max(1f, s)), colour * 0.7f);
        batch.Draw(pixel, new Rectangle((int)(b.X + b.Width * 0.63f), (int)(b.Y + b.Height * 0.11f), (int)MathF.Max(1f, s), (int)MathF.Max(1f, s)), colour * 0.45f);
    }

    // A relay: one hub, several stations hanging off it, and traffic on the links. The hexagon is
    // borrowed from the reference deliberately - it is the shape that says "network cell" without
    // needing a caption.
    private static void Network(SpriteBatch batch, Texture2D pixel, Rectangle b, Color colour, Color dim, Color faint, Color warm, float s, float heavy, float thin, float hair, float time)
    {
        var hub = At(b, 0.48f, 0.48f);
        var r = b.Width * 0.15f;

        Hexagon(batch, pixel, hub, r, colour, heavy);
        Hexagon(batch, pixel, hub, r * 0.58f, dim, hair);
        HudIcons.FillCircle(batch, pixel, hub, r * 0.24f, colour);
        // Six struts between the two hexes, so the hub is a structure rather than two rings that
        // happen to share a centre.
        for (var i = 0; i < 6; i++)
        {
            var a = i / 6f * MathF.Tau;
            var d = new Vector2(MathF.Cos(a), MathF.Sin(a));
            HudIcons.DrawLine(batch, pixel, hub + d * r * 0.58f, hub + d * r, faint, hair);
        }

        // Five outstations at unequal radii - evenly spaced spokes read as a snowflake, and a network
        // that looks symmetrical looks decorative.
        var nodes = new[] { (-1.15f, 0.62f), (0.15f, 0.72f), (1.6f, 0.55f), (2.5f, 0.70f), (3.9f, 0.60f) };
        for (var i = 0; i < nodes.Length; i++)
        {
            var (angle, reach) = nodes[i];
            var p = hub + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * b.Width * reach * 0.5f;
            HudIcons.DrawLine(batch, pixel, hub, p, faint, hair);

            // Traffic. Each link runs at its own rate so they never line up into a pulse, and a
            // packet in transit is the one thing here that has to move: a network drawn at rest is a
            // diagram of one.
            var rate = 0.22f + i * 0.055f;
            for (var k = 0; k < 2; k++)
            {
                var t = (time * rate + k * 0.5f + i * 0.31f) % 1f;
                HudIcons.FillCircle(batch, pixel, Vector2.Lerp(hub, p, t), 1.2f * s,
                    colour * (0.85f - MathF.Abs(t - 0.5f)));
            }

            // Two are square terminals, the rest rings: a set of identical nodes is a pattern, and a
            // pattern is the thing that stops looking like equipment.
            if (i % 2 == 0)
            {
                var side = (int)(4.6f * s);
                ShipRenderer.DrawRectOutline(batch, pixel,
                    new Rectangle((int)p.X - side / 2, (int)p.Y - side / 2, side, side), colour, (int)MathF.Max(1f, s));
                batch.Draw(pixel, new Rectangle((int)p.X - (int)(1.2f * s), (int)p.Y - (int)(1.2f * s),
                    (int)(2.4f * s), (int)(2.4f * s)), dim);
            }
            else
            {
                HudIcons.DrawRingArc(batch, pixel, p, 3f * s, 0f, 360f, colour, 10, thin);
            }
        }

        // One station is calling, and it is the warm thing on this plate. The rings travel outward
        // rather than sitting still - a transmission that does not move is just an antenna.
        var caller = hub + new Vector2(MathF.Cos(1.6f), MathF.Sin(1.6f)) * b.Width * 0.55f * 0.5f;
        HudIcons.FillCircle(batch, pixel, caller, 1.3f * s, warm);
        for (var k = 0; k < 2; k++)
        {
            var t = (time * 0.5f + k * 0.5f) % 1f;
            HudIcons.DrawRingArc(batch, pixel, caller, (3f + t * 7f) * s, -70f, 70f, warm * ((1f - t) * 0.8f), 9, hair);
        }

        // Signal strength beside the far terminal: four bars, three of them up.
        var far = hub + new Vector2(MathF.Cos(3.9f), MathF.Sin(3.9f)) * b.Width * 0.60f * 0.5f;
        for (var i = 0; i < 4; i++)
        {
            var h = (2f + i * 1.6f) * s;
            batch.Draw(pixel, new Rectangle((int)(far.X + (4f + i * 1.8f) * s), (int)(far.Y + 3f * s - h),
                (int)MathF.Max(1f, s * 1.2f), (int)h), i < 3 ? dim : faint);
        }
    }

    // A board being routed: traces that turn at right angles, pads they land on, a chip and its vias.
    // Everything you build a ship out of, in the language the ship's own panels are drawn in.
    private static void Shipyard(SpriteBatch batch, Texture2D pixel, Rectangle b, Color colour, Color dim, Color faint, Color warm, float s, float heavy, float thin, float hair, float time)
    {
        var chip = new Rectangle((int)(b.X + b.Width * 0.37f), (int)(b.Y + b.Height * 0.37f),
            (int)(b.Width * 0.29f), (int)(b.Height * 0.23f));
        batch.Draw(pixel, chip, new Color(24, 46, 46) * 0.9f);
        ShipRenderer.DrawRectOutline(batch, pixel, chip, colour, (int)MathF.Max(1f, heavy * 0.6f));
        // Pin one, marked the way it is on a real package.
        HudIcons.FillCircle(batch, pixel, new Vector2(chip.X + 3f * s, chip.Y + 3f * s), 1.2f * s, dim);
        for (var i = 0; i < 4; i++)
        {
            var x = chip.X + chip.Width * (i + 0.5f) / 4f;
            HudIcons.DrawLine(batch, pixel, new Vector2(x, chip.Y), new Vector2(x, chip.Y - 3.5f * s), dim, thin);
            HudIcons.DrawLine(batch, pixel, new Vector2(x, chip.Bottom), new Vector2(x, chip.Bottom + 3.5f * s), dim, thin);
        }

        // Four traces, each routed with a right-angle turn onto a pad. Routed, not drawn: a straight
        // line from A to B is a wire, and a wire that turns is a board.
        var runs = new[]
        {
            (0.07f, 0.19f, 0.30f, 0.33f),
            (0.07f, 0.75f, 0.34f, 0.62f),
            (0.93f, 0.25f, 0.69f, 0.41f),
            (0.93f, 0.81f, 0.66f, 0.60f),
        };
        foreach (var (x0, y0, x1, y1) in runs)
        {
            var a = At(b, x0, y0);
            var corner = At(b, x1, y0);
            var end = At(b, x1, y1);
            HudIcons.DrawLine(batch, pixel, a, corner, colour * 0.75f, thin);
            HudIcons.DrawLine(batch, pixel, corner, end, colour * 0.75f, thin);
            HudIcons.DrawRingArc(batch, pixel, a, 2.8f * s, 0f, 360f, colour, 10, thin);
            HudIcons.FillCircle(batch, pixel, a, 1.1f * s, dim);
        }

        // One run at a time carries power, and the light walks along it before handing over to the
        // next - a board being brought up rather than a board sitting there.
        var live = (int)(time * 0.5f) % runs.Length;
        var walk = time * 0.5f % 1f;
        var (lx0, ly0, lx1, ly1) = runs[live];
        var from = At(b, lx0, ly0);
        var bend = At(b, lx1, ly0);
        var to = At(b, lx1, ly1);
        var legOne = Vector2.Distance(from, bend);
        var total = legOne + Vector2.Distance(bend, to);
        var travelled = walk * total;
        if (travelled <= legOne)
        {
            HudIcons.DrawLine(batch, pixel, from, Vector2.Lerp(from, bend, travelled / MathF.Max(1f, legOne)), warm, thin);
        }
        else
        {
            HudIcons.DrawLine(batch, pixel, from, bend, warm, thin);
            HudIcons.DrawLine(batch, pixel, bend,
                Vector2.Lerp(bend, to, (travelled - legOne) / MathF.Max(1f, total - legOne)), warm, thin);
        }
        HudIcons.FillCircle(batch, pixel, from, 1.2f * s, warm);

        // A component sitting in one of the runs: a body between two leads, the way a schematic draws
        // it. This is the mark that says these lines are a circuit and not a maze.
        var compAt = At(b, 0.19f, 0.75f);
        batch.Draw(pixel, new Rectangle((int)(compAt.X - 3f * s), (int)(compAt.Y - 1.8f * s),
            (int)(6f * s), (int)(3.6f * s)), colour * 0.8f);

        // Vias, and a dashed outline of something not yet placed - the board is still being worked
        // on, which is what an editor is for.
        for (var i = 0; i < 5; i++)
            HudIcons.FillCircle(batch, pixel, At(b, 0.20f + i * 0.035f, 0.47f), 1.1f * s, faint);
        var ghost = new Rectangle((int)(b.X + b.Width * 0.71f), (int)(b.Y + b.Height * 0.68f),
            (int)(b.Width * 0.17f), (int)(b.Height * 0.15f));
        var step = (int)MathF.Max(2f, 3f * s);
        var dot = (int)MathF.Max(1f, s);
        for (var i = 0; i < ghost.Width; i += step)
        {
            batch.Draw(pixel, new Rectangle(ghost.X + i, ghost.Y, dot, dot), faint);
            batch.Draw(pixel, new Rectangle(ghost.X + i, ghost.Bottom, dot, dot), faint);
        }
        for (var i = 0; i < ghost.Height; i += step)
        {
            batch.Draw(pixel, new Rectangle(ghost.X, ghost.Y + i, dot, dot), faint);
            batch.Draw(pixel, new Rectangle(ghost.Right, ghost.Y + i, dot, dot), faint);
        }

        // A dimension under it, arrows and all - the detail that says someone is measuring.
        var dimY = ghost.Bottom + 4f * s;
        HudIcons.DrawLine(batch, pixel, new Vector2(ghost.X, dimY), new Vector2(ghost.Right, dimY), faint, hair);
        HudIcons.DrawLine(batch, pixel, new Vector2(ghost.X, dimY - 1.6f * s), new Vector2(ghost.X, dimY + 1.6f * s), faint, hair);
        HudIcons.DrawLine(batch, pixel, new Vector2(ghost.Right, dimY - 1.6f * s), new Vector2(ghost.Right, dimY + 1.6f * s), faint, hair);
    }

    // A scope: range rings, a sweep that has just gone past two contacts, and a trace along the
    // bottom. This is the housekeeping section, and housekeeping on a ship is watching dials.
    private static void Systems(SpriteBatch batch, Texture2D pixel, Rectangle b, Color colour, Color dim, Color faint, Color warm, float s, float heavy, float thin, float hair, float time)
    {
        var centre = At(b, 0.46f, 0.42f);
        var r = b.Width * 0.29f;

        HudIcons.FillCircle(batch, pixel, centre, r, new Color(20, 44, 42) * 0.8f);
        HudIcons.DrawRingArc(batch, pixel, centre, r, 0f, 360f, colour, 28, heavy);
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

        // The sweep, turning once every six seconds, with a wedge of afterglow behind it. The wedge
        // is what makes it a sweep even in a still frame - a bare line is a hand on a clock.
        var sweep = time * (MathF.Tau / 6f);
        for (var i = 0; i < 10; i++)
        {
            var a = sweep - i * 0.05f;
            var fade = (1f - i / 10f) * 0.22f;
            HudIcons.DrawLine(batch, pixel, centre, centre + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r, colour * fade, hair);
        }
        HudIcons.DrawLine(batch, pixel, centre, centre + new Vector2(MathF.Cos(sweep), MathF.Sin(sweep)) * r, colour, thin);

        // Two contacts. One flares as the beam crosses it and fades until the beam comes round again,
        // which is the whole behaviour of a scope in one line of arithmetic.
        DrawContact(batch, pixel, centre, r * 0.62f, 0.9f, sweep, warm, dim, s, hair, true);
        DrawContact(batch, pixel, centre, r * 0.45f, 3.6f, sweep, warm, dim, s, hair, false);

        // A column of bar gauges down the left - the housekeeping this section is actually for.
        for (var i = 0; i < 4; i++)
        {
            var y = b.Y + b.Height * (0.20f + i * 0.10f);
            var full = b.Width * 0.13f;
            var x = b.X + b.Width * 0.06f;
            HudIcons.DrawLine(batch, pixel, new Vector2(x, y), new Vector2(x + full, y), faint, hair);
            var fill = full * (0.35f + 0.6f * ((i * 37 % 11) / 11f));
            batch.Draw(pixel, new Rectangle((int)x, (int)(y - 1.4f * s), (int)fill, (int)MathF.Max(1f, s * 1.4f)), dim);
        }

        // A trace along the bottom, because a scope is never the only readout on a panel.
        var baseY = b.Y + b.Height * 0.87f;
        var prev = new Vector2(b.X + b.Width * 0.10f, baseY);
        for (var i = 1; i <= 20; i++)
        {
            var t = i / 20f;
            var x = MathHelper.Lerp(b.X + b.Width * 0.10f, b.X + b.Width * 0.90f, t);
            var wave = MathF.Sin(t * 9.5f + time * 1.1f) * 0.6f + MathF.Sin(t * 21f - time * 0.7f) * 0.25f;
            var p = new Vector2(x, baseY - wave * b.Height * 0.055f);
            HudIcons.DrawLine(batch, pixel, prev, p, dim, hair);
            prev = p;
        }
    }

    // A contact on the scope face, lit by the beam passing over it and decaying afterwards.
    private static void DrawContact(SpriteBatch batch, Texture2D pixel, Vector2 centre, float radius,
        float bearing, float sweep, Color warm, Color dim, float s, float hair, bool ringed)
    {
        var p = centre + new Vector2(MathF.Cos(bearing), MathF.Sin(bearing)) * radius;
        // How long since the beam last crossed this bearing, as a fraction of one turn.
        var since = ((sweep - bearing) % MathF.Tau + MathF.Tau) % MathF.Tau / MathF.Tau;
        var glow = MathF.Pow(1f - since, 3f);
        HudIcons.FillCircle(batch, pixel, p, (1.1f + glow * 0.8f) * s, Color.Lerp(dim, warm, glow));
        if (ringed)
            HudIcons.DrawRingArc(batch, pixel, p, 3f * s, 0f, 360f, Color.Lerp(dim, warm, glow) * 0.55f, 10, hair);
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
