using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// Content-каталог отсеков - one silhouette per catalog entry (RoomCatalog.cs), keyed on the exact
// same room.Name string AccentByCatalogName already switches on (TryBuildRoom names every built
// room after its catalog entry). Drawn from reference screenshots the player supplied for each of
// the 13 real types (plus the plain corridor, which gets none - it is deliberately the one flat,
// undecorated shape in the set). Every shape is expressed as a FRACTION of rect's own width/height
// rather than fixed pixel offsets, unlike DrawFurniture's corner props above (which only ever sit
// in one fixed-size walkable room) - these rooms range from a single 3x3 tile up to a 12x12
// bridge, so a fixed-pixel prop would either vanish inside the big ones or overflow the small ones.
public static partial class RoomDecor
{
    public static void DrawCatalogDecor(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, string? roomName, Color accent)
    {
        switch (roomName)
        {
            case "Реакторный отсек":
                DrawReactorCells(spriteBatch, pixel, rect, accent);
                break;
            case "Двигатель маршевый (малый)":
                DrawMarchingEngine(spriteBatch, pixel, rect, accent, big: false);
                break;
            case "Двигатель маршевый (большой)":
                DrawMarchingEngine(spriteBatch, pixel, rect, accent, big: true);
                break;
            case "Кокпит (малый)":
                DrawCockpit(spriteBatch, pixel, rect, accent, seats: 4);
                break;
            case "Капитанский мостик (большой)":
                DrawCockpit(spriteBatch, pixel, rect, accent, seats: 6);
                break;
            case "Турель лазерная":
                DrawTurretBay(spriteBatch, pixel, rect, accent, barrelWidthFraction: 0.16f);
                break;
            case "Турель пушечная":
                DrawTurretBay(spriteBatch, pixel, rect, accent, barrelWidthFraction: 0.32f);
                break;
            case "Каюта":
                DrawQuartersBunk(spriteBatch, pixel, rect, accent);
                break;
            case "Манёвровый двигатель (однонаправленный)":
                DrawRcsThruster(spriteBatch, pixel, rect, accent, nozzles: 1);
                break;
            case "Манёвровый двигатель (двусторонний)":
                DrawRcsThruster(spriteBatch, pixel, rect, accent, nozzles: 2);
                break;
            case "Манёвровый двигатель (трёхсторонний)":
                DrawRcsThruster(spriteBatch, pixel, rect, accent, nozzles: 3);
                break;
            case "Генератор щита":
                DrawShieldGenerator(spriteBatch, pixel, rect, accent);
                break;
            case "Камера":
                DrawCameraPod(spriteBatch, pixel, rect, accent);
                break;
            // "Коридор" and the original 2 empty shells fall through undecorated on purpose -
            // Accent's own flat grey already reads as "just a passage", and the plan's own catalog
            // table calls the corridor out as deliberately the plainest piece in the set.
        }
    }

    // Reference: a grid of bright yellow-green fuel cells behind a dark housing frame.
    private static void DrawReactorCells(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color accent)
    {
        var housing = Inset(rect, 0.08f, 0.06f);
        spriteBatch.Draw(pixel, housing, new Color(46, 44, 40));
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, housing, new Color(150, 130, 90), 2);

        var glow = Color.Lerp(accent, Color.White, 0.25f);
        const int columns = 2, rows = 4;
        var cellW = housing.Width * 0.28f;
        var cellH = housing.Height * 0.16f;
        var gapX = (housing.Width - cellW * columns) / (columns + 1);
        var gapY = housing.Height * 0.05f;
        var top = housing.Y + housing.Height * 0.08f;
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < columns; col++)
            {
                var x = housing.X + gapX * (col + 1) + cellW * col;
                var y = top + row * (cellH + gapY);
                spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)cellW, (int)cellH), glow);
                spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)cellW, (int)(cellH * 0.3f)), Color.White * 0.5f);
            }
        }
        // A shorter row of smaller trim cells underneath the main bank, same "grid of light" the
        // reference photo's own bottom strip shows.
        var trimY = housing.Bottom - housing.Height * 0.14f;
        var trimW = housing.Width * 0.12f;
        for (var i = 0; i < 5; i++)
        {
            var x = housing.X + housing.Width * 0.08f + i * (trimW + housing.Width * 0.02f);
            spriteBatch.Draw(pixel, new Rectangle((int)x, (int)trimY, (int)trimW, (int)(housing.Height * 0.08f)), glow * 0.85f);
        }
    }

    // Reference: a lit console at the intake end, a tall cylindrical housing, a hazard-striped
    // exhaust nozzle at the other end. `big` scales up the glow tube and drops a second console
    // band, matching the large marching engine's own bulkier reference photo.
    private static void DrawMarchingEngine(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color accent, bool big)
    {
        var console = new Rectangle(rect.X + (int)(rect.Width * 0.15f), rect.Y + (int)(rect.Height * 0.05f),
            (int)(rect.Width * 0.7f), (int)(rect.Height * (big ? 0.16f : 0.12f)));
        spriteBatch.Draw(pixel, console, new Color(40, 46, 52));
        spriteBatch.Draw(pixel, Inset(console, 0.15f, 0.25f), Color.Lerp(new Color(120, 210, 220), accent, big ? 0.5f : 0.2f) * 0.85f);

        var housing = new Rectangle(rect.X + (int)(rect.Width * 0.22f), console.Bottom + (int)(rect.Height * 0.03f),
            (int)(rect.Width * 0.56f), (int)(rect.Height * 0.55f));
        spriteBatch.Draw(pixel, housing, new Color(66, 70, 76));
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, housing, Color.Black * 0.35f, 1);
        var glowTube = new Rectangle(housing.Center.X - (int)(housing.Width * (big ? 0.22f : 0.14f)), housing.Y,
            (int)(housing.Width * (big ? 0.44f : 0.28f)), housing.Height);
        spriteBatch.Draw(pixel, glowTube, Color.Lerp(accent, Color.White, 0.35f) * 0.9f);

        // Hazard-striped exhaust collar - the one recurring "this end gets hot" motif every engine
        // and thruster reference photo shares.
        DrawHazardStripe(spriteBatch, pixel, new Rectangle(rect.X, rect.Bottom - (int)(rect.Height * 0.12f), rect.Width, (int)(rect.Height * 0.12f)));
    }

    // Reference: two symmetric chairs either side of a rounded viewport/hatch, with a low console
    // in front. `seats` scales it up to the bridge's own 6-seat reference (4 corner chairs plus 2
    // more flanking the hatch) rather than the cockpit's 4.
    private static void DrawCockpit(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color accent, int seats)
    {
        var seatColor = new Color(196, 168, 132);
        var seatW = rect.Width * 0.14f;
        var seatH = rect.Height * 0.16f;
        void Seat(float fx, float fy)
        {
            var seat = new Rectangle(rect.X + (int)(rect.Width * fx - seatW / 2), rect.Y + (int)(rect.Height * fy), (int)seatW, (int)seatH);
            spriteBatch.Draw(pixel, seat, seatColor);
            spriteBatch.Draw(pixel, new Rectangle(seat.X, seat.Y - (int)(seatH * 0.4f), seat.Width, (int)(seatH * 0.4f)), seatColor * 0.9f);
        }

        Seat(0.28f, 0.14f);
        Seat(0.72f, 0.14f);
        if (seats >= 6)
        {
            Seat(0.12f, 0.62f);
            Seat(0.88f, 0.62f);
        }
        Seat(0.4f, 0.62f);
        Seat(0.6f, 0.62f);

        // The rounded hatch/viewport at the room's own centre - a dark disc built from shrinking
        // squares, the same cheap "circle" DrawLightPool's own concentric rects already fake.
        var hatchCenter = new Vector2(rect.Center.X, rect.Y + rect.Height * 0.42f);
        var hatchRadius = rect.Width * 0.16f;
        for (var i = 0; i < 4; i++)
        {
            var r = hatchRadius * (1f - i * 0.22f);
            var square = new Rectangle((int)(hatchCenter.X - r), (int)(hatchCenter.Y - r), (int)(r * 2), (int)(r * 2));
            spriteBatch.Draw(pixel, square, Color.Lerp(new Color(28, 30, 34), accent, i == 0 ? 0f : 0.15f));
        }

        DrawHazardStripe(spriteBatch, pixel, new Rectangle(rect.X, rect.Bottom - (int)(rect.Height * 0.08f), rect.Width, (int)(rect.Height * 0.08f)));
    }

    // Reference: a barrel/gimbal assembly on a hazard-striped turntable. barrelWidthFraction is
    // the one real difference between the laser (thin, missile-like) and the ballistic cannon
    // (thick, boxy) reference photos - everything else about the bay reads the same.
    private static void DrawTurretBay(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color accent, float barrelWidthFraction)
    {
        DrawHazardStripe(spriteBatch, pixel, Inset(rect, 0.06f, 0.06f));

        var mount = new Rectangle(rect.Center.X - (int)(rect.Width * 0.22f), rect.Y + (int)(rect.Height * 0.55f),
            (int)(rect.Width * 0.44f), (int)(rect.Height * 0.3f));
        spriteBatch.Draw(pixel, mount, new Color(58, 60, 66));
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, mount, Color.Black * 0.4f, 2);

        var barrel = new Rectangle(rect.Center.X - (int)(rect.Width * barrelWidthFraction / 2), rect.Y + (int)(rect.Height * 0.04f),
            (int)(rect.Width * barrelWidthFraction), (int)(rect.Height * 0.56f));
        spriteBatch.Draw(pixel, barrel, new Color(74, 78, 84));
        spriteBatch.Draw(pixel, new Rectangle(barrel.X, barrel.Y, (int)(barrel.Width * 0.3f), barrel.Height), Color.White * 0.12f);
        spriteBatch.Draw(pixel, new Rectangle(barrel.X, barrel.Y, barrel.Width, (int)(barrel.Height * 0.1f)), Color.Lerp(accent, Color.White, 0.3f));
    }

    // Reference: a single bed against one wall, a small dresser/monitor against the other - muted,
    // lived-in colours rather than the sterile equipment palette every other room above uses.
    private static void DrawQuartersBunk(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color accent)
    {
        var bed = new Rectangle(rect.X + (int)(rect.Width * 0.1f), rect.Y + (int)(rect.Height * 0.1f),
            (int)(rect.Width * 0.5f), (int)(rect.Height * 0.32f));
        spriteBatch.Draw(pixel, bed, new Color(150, 110, 112));
        spriteBatch.Draw(pixel, new Rectangle(bed.X, bed.Y, bed.Width, (int)(bed.Height * 0.3f)), new Color(196, 158, 158));
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, bed, Color.Black * 0.3f, 1);

        var dresser = new Rectangle(rect.Right - (int)(rect.Width * 0.32f), rect.Y + (int)(rect.Height * 0.5f),
            (int)(rect.Width * 0.24f), (int)(rect.Height * 0.3f));
        spriteBatch.Draw(pixel, dresser, new Color(76, 74, 78));
        spriteBatch.Draw(pixel, new Rectangle(dresser.X + 2, dresser.Y + 2, dresser.Width - 4, (int)(dresser.Height * 0.4f)),
            Color.Lerp(accent, Color.White, 0.4f) * 0.7f);
    }

    // Reference: a compact thruster block with a hazard-striped floor patch and one glowing nozzle
    // tip per direction the room actually turns the ship - a 1-way RCS gets one, a 3-way gets
    // three, so the room's own silhouette communicates the same thing its name does.
    private static void DrawRcsThruster(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color accent, int nozzles)
    {
        DrawHazardStripe(spriteBatch, pixel, Inset(rect, 0.1f, 0.1f));
        var block = Inset(rect, 0.28f, 0.22f);
        spriteBatch.Draw(pixel, block, new Color(62, 64, 70));
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, block, Color.Black * 0.4f, 1);

        var nozzleW = block.Width * 0.22f;
        var spacing = block.Width / (nozzles + 1);
        for (var i = 0; i < nozzles; i++)
        {
            var x = block.X + spacing * (i + 1) - nozzleW / 2;
            var nozzle = new Rectangle((int)x, block.Bottom - 2, (int)nozzleW, (int)(rect.Height * 0.1f));
            spriteBatch.Draw(pixel, nozzle, Color.Lerp(accent, Color.White, 0.3f));
        }
    }

    // Reference: a tall bank of glowing capacitor cells in a rounded teal-lit housing - the same
    // family as DrawFurniture's own DrawCapacitorBank, just the room's own centrepiece rather than
    // corner dressing, and taller (this catalog room is 6x9, not a small prop tucked in a corner).
    private static void DrawShieldGenerator(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color accent)
    {
        var housing = Inset(rect, 0.16f, 0.06f);
        spriteBatch.Draw(pixel, housing, new Color(44, 50, 54));
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, housing, Color.Lerp(accent, Color.White, 0.2f), 2);

        const int cells = 4;
        var cellW = housing.Width * 0.16f;
        var gap = (housing.Width - cellW * cells) / (cells + 1);
        for (var i = 0; i < cells; i++)
        {
            var x = housing.X + gap * (i + 1) + cellW * i;
            var cell = new Rectangle((int)x, housing.Y + (int)(housing.Height * 0.08f), (int)cellW, (int)(housing.Height * 0.7f));
            spriteBatch.Draw(pixel, cell, Color.Black * 0.35f);
            spriteBatch.Draw(pixel, new Rectangle(cell.X, cell.Bottom - (int)(cell.Height * 0.55f), cell.Width, (int)(cell.Height * 0.55f)),
                Color.Lerp(accent, Color.White, 0.35f) * 0.9f);
        }
    }

    // Reference: a small dark octagonal pod with a single glowing lens at its centre.
    private static void DrawCameraPod(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color accent)
    {
        var pod = Inset(rect, 0.14f, 0.14f);
        spriteBatch.Draw(pixel, pod, new Color(38, 40, 44));
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, pod, Color.Black * 0.4f, 2);

        var lensRadius = pod.Width * 0.22f;
        for (var i = 0; i < 3; i++)
        {
            var r = lensRadius * (1f - i * 0.3f);
            var square = new Rectangle((int)(pod.Center.X - r), (int)(pod.Center.Y - r), (int)(r * 2), (int)(r * 2));
            spriteBatch.Draw(pixel, square, i == 2 ? Color.Lerp(accent, Color.White, 0.5f) : new Color(30, 32, 36));
        }
    }

    // Diagonal red/yellow hazard stripes - the one motif every weapon/engine/thruster reference
    // photo shares for "this is the dangerous end". Each stripe is ONE rotated-quad draw call (the
    // same "1x1 pixel, scaled and spun" trick ComponentRenderer's own wire-line drawing already
    // uses), not per-pixel rectangles - a per-pixel version of this ran thousands of draw calls for
    // a single room and would have cost real frame time on every hazard-striped compartment.
    //
    // Each stripe is an infinite 45-degree line clipped down to just the segment that actually
    // falls inside `area` (ClipLineToRect below) rather than one fixed length for all of them - a
    // uniform length would only be exactly right for the one stripe through the rect's own centre;
    // every other one would either stick out past the room's edges into whatever is drawn next to
    // it, or fall short and leave a gap near the corners.
    private static void DrawHazardStripe(SpriteBatch spriteBatch, Texture2D pixel, Rectangle area)
    {
        spriteBatch.Draw(pixel, area, new Color(40, 36, 30));

        const float rotation = MathHelper.PiOver4;
        const float period = 22f;
        var dir = new Vector2(MathF.Cos(rotation), MathF.Sin(rotation));
        var perp = new Vector2(-dir.Y, dir.X);
        var center = new Vector2(area.Center.X, area.Center.Y);
        var diagonal = MathF.Sqrt(area.Width * area.Width + area.Height * area.Height);
        var halfCount = (int)(diagonal / period) + 1;

        for (var i = -halfCount; i <= halfCount; i++)
        {
            var basePoint = center + perp * (i * period);
            if (!ClipLineToRect(basePoint, dir, area, out var tMin, out var tMax))
                continue;

            var color = i % 2 == 0 ? new Color(196, 64, 48) : new Color(210, 172, 60);
            var start = basePoint + dir * tMin;
            spriteBatch.Draw(pixel, start, null, color * 0.85f, rotation, Vector2.Zero,
                new Vector2(tMax - tMin, period * 0.55f), SpriteEffects.None, 0f);
        }
    }

    // Standard slab method: walks the line's own X and Y coverage of the rect down to the single
    // [tMin, tMax] range where both hold at once. False when the line never crosses the rect at all
    // (every stripe beyond the corners, since halfCount above deliberately overshoots a little).
    private static bool ClipLineToRect(Vector2 origin, Vector2 dir, Rectangle rect, out float tMin, out float tMax)
    {
        tMin = float.NegativeInfinity;
        tMax = float.PositiveInfinity;
        if (MathF.Abs(dir.X) > 1e-6f)
        {
            var tx1 = (rect.Left - origin.X) / dir.X;
            var tx2 = (rect.Right - origin.X) / dir.X;
            tMin = MathF.Max(tMin, MathF.Min(tx1, tx2));
            tMax = MathF.Min(tMax, MathF.Max(tx1, tx2));
        }
        if (MathF.Abs(dir.Y) > 1e-6f)
        {
            var ty1 = (rect.Top - origin.Y) / dir.Y;
            var ty2 = (rect.Bottom - origin.Y) / dir.Y;
            tMin = MathF.Max(tMin, MathF.Min(ty1, ty2));
            tMax = MathF.Min(tMax, MathF.Max(ty1, ty2));
        }
        return tMin < tMax;
    }

    private static Rectangle Inset(Rectangle rect, float fx, float fy)
    {
        var insetX = (int)(rect.Width * fx);
        var insetY = (int)(rect.Height * fy);
        return new Rectangle(rect.X + insetX, rect.Y + insetY, rect.Width - insetX * 2, rect.Height - insetY * 2);
    }
}
