using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Door rendering: frame/leaf/braces/indicator/access terminals and the door's own footprint housing plate - split out of ShipRenderer.cs to keep that file to its own topic.
public sealed partial class ShipRenderer
{
    // Green: open and passable. Dark red: closed and airtight (game_design.md Phase 3, M16).
    // leadsToVacuum only changes the OPEN color (purple instead of green) and widens the hazard
    // tape - a closed door reads the same "sealed" red no matter what's behind it; only an open
    // one to vacuum is the state that can actually kill you. destroyed overrides everything else
    // about its color - a door with its own hit points hit zero (World.Doors.cs, cut open or
    // chopped through) is jammed open regardless of leadsToVacuum, and a pulsing warning color plus
    // scorch marks read as "broken" at a glance instead of looking like an ordinary open door.
    // A framed bezel with corner rivets (matching every other device housing's own treatment),
    // hazard tape on a closed leaf, and a small flat access terminal mounted on the wall on each
    // flank - the "press to open" control the crew would actually reach for - round this out from
    // a flat colored rectangle into a proper airlock. internal: reused by StationRenderer, which
    // draws the station's own Rooms/Doors/Characters through the exact same visual language
    // instead of duplicating it.
    internal void DrawDoor(SpriteBatch spriteBatch, float left, float top, float width, float height, bool vertical, bool isOpen, Vector2 origin,
        bool leadsToVacuum = false, bool destroyed = false, float totalSeconds = 0f) =>
        DrawDoor(spriteBatch, GetDoorRect(left, top, width, height, origin), vertical, isOpen, leadsToVacuum, destroyed, totalSeconds);

    // Rect-based overload (Game1.ShipEditor.Draw.cs's own DrawEditorDoorTile, direct user request -
    // "дверь своей моделькой, а не голым квадратом") - the editor has no world-unit origin/PixelsPerUnit
    // mapping of its own to feed the position-based overload above, but it already computes exactly
    // the merged (for a 2-tile wide door) or single screen rect its own placeholder used to fill flat,
    // so this skips straight to drawing the real door art at that rect instead of re-deriving one.
    // `vertical` (humble-soaring-cat.md "Дверь как устройство со своим footprint'ом") - the caller's
    // own orientation, used ONLY to widen the housing plate below into the door's own 1x2/2x2
    // footprint; the real game gets it for free from Door.IsVertical (sent over the wire), the
    // editor infers it locally (its own doc comment on DrawEditorDoorTile) since export hasn't run.
    internal void DrawDoor(SpriteBatch spriteBatch, Rectangle rect, bool vertical, bool isOpen,
        bool leadsToVacuum = false, bool destroyed = false, float totalSeconds = 0f)
    {
        var horizontal = rect.Width >= rect.Height;

        var indicator = destroyed
            ? Color.OrangeRed * (0.6f + 0.4f * MathF.Sin(totalSeconds * 6f))
            : isOpen
                ? (leadsToVacuum ? new Color(190, 140, 255) : new Color(90, 230, 120))
                : new Color(255, 90, 90);

        DrawDoorHousing(spriteBatch, GetDoorFootprintRect(rect, vertical));
        DrawDoorFrame(spriteBatch, rect, destroyed);

        if (destroyed)
            DrawDestroyedDoorLeaf(spriteBatch, rect, horizontal);
        else if (isOpen)
            DrawOpenDoorLeaf(spriteBatch, rect, horizontal, leadsToVacuum);
        else
            DrawClosedDoorLeaf(spriteBatch, rect, horizontal, leadsToVacuum);

        DrawDoorIndicator(spriteBatch, rect, indicator);
        DrawDoorTerminals(spriteBatch, rect, horizontal, indicator);
    }

    // The door's own footprint (direct user request - "устройство 1 на 2 тайла"/"устройство 2 на 2
    // тайла", mirrors Door.FootprintRect's own continuous-space math but on an already tile-
    // resolved screen Rectangle, in whatever pixel scale IT uses - PixelsPerUnit for the real game,
    // EditorCellSize for the ship editor's own preview, never hardcoded here) - same center as the
    // barrier rect, but its 1-tile THICKNESS axis (the smaller of Width/Height - always exactly one
    // tile for a real barrier rect) widened to 2 tiles, one into each room; the span axis (1 or 2
    // tiles already) is left exactly as-is.
    private static Rectangle GetDoorFootprintRect(Rectangle rect, bool vertical)
    {
        var unit = Math.Min(rect.Width, rect.Height);
        return vertical
            ? new Rectangle(rect.Center.X - unit, rect.Y, unit * 2, rect.Height)
            : new Rectangle(rect.X, rect.Center.Y - unit, rect.Width, unit * 2);
    }

    // Flat metal plate under the existing frame/leaf (direct user request - the door should read as
    // occupying its own housing, not a bare strip floating in empty space) - drawn FIRST so
    // DrawDoorFrame's own bezel+rivets, then the leaf, sit visibly "inset" within it. Deliberately
    // just a flat fill, no separate art - the frame/leaf right on top of it already carry all the
    // detail (rivets, lit strip, braces).
    private static readonly Color DoorHousing = new(58, 46, 36);

    private void DrawDoorHousing(SpriteBatch spriteBatch, Rectangle footprintRect) =>
        spriteBatch.Draw(_pixel, footprintRect, DoorHousing);

    // Bronze/brass housing (direct user request, matching a reference screenshot) - a plain flat
    // fill read as too flat next to the leaf's own top-lit bands below, so the bezel gets one too:
    // a lighter strip along its top edge standing in for the same single overhead light source.
    private static readonly Color DoorFrameLit = new(140, 107, 74);
    private static readonly Color DoorFrame = new(90, 68, 46);
    private static readonly Color DoorFrameDestroyed = new(92, 80, 72);

    private void DrawDoorFrame(SpriteBatch spriteBatch, Rectangle rect, bool destroyed)
    {
        const int margin = 5;
        var bezel = new Rectangle(rect.X - margin, rect.Y - margin, rect.Width + margin * 2, rect.Height + margin * 2);
        if (destroyed)
        {
            spriteBatch.Draw(_pixel, bezel, DoorFrameDestroyed);
        }
        else
        {
            spriteBatch.Draw(_pixel, bezel, DoorFrame);
            var litHeight = Math.Max(2, bezel.Height / 4);
            spriteBatch.Draw(_pixel, new Rectangle(bezel.X, bezel.Y, bezel.Width, litHeight), DoorFrameLit);
        }
        DrawRivets(spriteBatch, bezel);
    }

    // Warm painted-metal panel (direct user request, matching a reference screenshot) - a top-lit
    // gradient (approximated as flat bands, same "no image assets" convention as everywhere else in
    // this file) plus a stepped diagonal brace on each leaf half. Every offset is a fraction of
    // rect/halfWidth rather than a fixed pixel count, so the exact same drawing already scales
    // correctly whether this door spans one tile or two (Door.Width/Height, ShipRenderer.GetDoorRect) -
    // no separate "single" vs "double" art or code path needed.
    private static readonly Color[] DoorPanelBands = { new(224, 128, 80), new(200, 98, 60), new(168, 78, 48), new(136, 60, 36) };
    private static readonly Color DoorSeam = new(110, 50, 32);
    private static readonly Color DoorBrace = new(92, 44, 24);

    private static readonly Color DoorSeamHighlight = new(232, 138, 95);
    private static readonly Color DoorWornPaint = new(232, 168, 72);

    private void DrawClosedDoorLeaf(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, bool leadsToVacuum)
    {
        DrawTopLitBands(spriteBatch, rect, horizontal, DoorPanelBands);

        // The center seam - between the two leaf halves, exactly where DrawDoorLeafCaps' own split
        // already reads as "open" for the same door, so closed and open agree on where the leaf
        // actually divides. A thin lit sliver right beside it, same "one overhead light" language
        // the frame's own top strip and the panel's own bands already use.
        const int seamThickness = 4;
        if (horizontal)
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.Center.X - seamThickness / 2, rect.Y, seamThickness, rect.Height), DoorSeam);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Center.X, rect.Y, 1, rect.Height), DoorSeamHighlight);
        }
        else
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Center.Y - seamThickness / 2, rect.Width, seamThickness), DoorSeam);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Center.Y, rect.Width, 1), DoorSeamHighlight);
        }

        // Each half reads as its own riveted plate - an inset bevel outline plus the same 4-corner
        // rivet shared by every other device's own housing (DrawRivets), just run twice instead of
        // once so each half gets its own set rather than only the leaf's outer corners.
        const int panelInset = 6;
        foreach (var half in horizontal
                     ? new[]
                     {
                         new Rectangle(rect.X, rect.Y, rect.Width / 2, rect.Height),
                         new Rectangle(rect.Center.X, rect.Y, rect.Width - rect.Width / 2, rect.Height),
                     }
                     : new[]
                     {
                         new Rectangle(rect.X, rect.Y, rect.Width, rect.Height / 2),
                         new Rectangle(rect.X, rect.Center.Y, rect.Width, rect.Height - rect.Height / 2),
                     })
        {
            var inset = new Rectangle(half.X + panelInset, half.Y + panelInset, Math.Max(1, half.Width - panelInset * 2), Math.Max(1, half.Height - panelInset * 2));
            DrawRectOutline(spriteBatch, inset, DoorBrace, 1);
            DrawRivets(spriteBatch, half);
        }

        // A couple of worn-paint chips (direct user request, "детализированнее") - purely cosmetic
        // asymmetric wear so the panel doesn't read as a perfectly uniform print, always at the same
        // two corners regardless of door size so it never overlaps the brace/rivets above.
        const int wornSize = 5;
        spriteBatch.Draw(_pixel, new Rectangle(rect.X + panelInset + 2, rect.Bottom - panelInset - wornSize - 2, wornSize, wornSize / 2), DoorWornPaint * 0.7f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - panelInset - wornSize - 2, rect.Y + panelInset + 2, wornSize, wornSize / 2), DoorWornPaint * 0.7f);

        DrawDoorBraces(spriteBatch, rect, horizontal, leadsToVacuum);
    }

    // Fills rect with `bands.Length` equal strips along the SHORT axis (top-to-bottom for a
    // horizontal door, left-to-right for a vertical one - always across the leaf's own thickness,
    // never along its length), lightest band first - the same "one overhead light source" language
    // DrawDoorFrame's own lit strip above uses.
    private void DrawTopLitBands(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, Color[] bands)
    {
        var span = horizontal ? rect.Height : rect.Width;
        for (var i = 0; i < bands.Length; i++)
        {
            var from = i * span / bands.Length;
            var to = (i + 1) * span / bands.Length;
            var band = horizontal
                ? new Rectangle(rect.X, rect.Y + from, rect.Width, to - from)
                : new Rectangle(rect.X + from, rect.Y, to - from, rect.Height);
            spriteBatch.Draw(_pixel, band, bands[i]);
        }
    }

    // One stepped (not smooth) diagonal per leaf half, mirrored around the center seam - reads as a
    // welded cross-brace at a glance, blocky enough to sit comfortably next to the banded fill
    // above rather than looking like a stray anti-aliased line.
    private void DrawDoorBraces(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, bool leadsToVacuum)
    {
        const int steps = 6;
        var halfLength = (horizontal ? rect.Width : rect.Height) / 2f;
        var acrossExtent = horizontal ? rect.Height : rect.Width;
        // Divides the leaf's own thickness exactly (never overshoots it), independent of how far
        // along the diagonal actually runs - the two axes only need to agree on step COUNT, not size.
        var acrossStep = Math.Max(1, acrossExtent / steps);
        var thickness = Math.Max(2, acrossStep);
        var alongStep = Math.Max(2, (int)(halfLength * 0.7f / steps));

        void Brace(float lengthStart, int direction)
        {
            for (var i = 0; i < steps; i++)
            {
                var alongLeaf = lengthStart + direction * i * alongStep;
                var acrossLeaf = i * acrossStep;
                var pos = horizontal
                    ? new Rectangle(rect.X + (int)alongLeaf, rect.Y + acrossLeaf, alongStep, thickness)
                    : new Rectangle(rect.X + acrossLeaf, rect.Y + (int)alongLeaf, thickness, alongStep);
                spriteBatch.Draw(_pixel, pos, DoorBrace);
            }
        }

        // First half: brace runs from its near end inward; second half mirrors it outward from the
        // seam - together they form the same "V per half" shape regardless of how wide either half
        // actually is (one tile or two), since every offset above is relative to halfLength.
        Brace(halfLength * 0.15f, 1);
        Brace(halfLength * 0.85f, -1);
        Brace(halfLength * 1.15f, 1);
        Brace(halfLength * 1.85f, -1);

        // leadsToVacuum keeps a thin hazard edge (the one place stripes still earn their keep - the
        // functional "this one opens onto vacuum" signal, not decoration) rather than the old
        // all-doors-get-stripes treatment.
        if (leadsToVacuum)
            DrawDoorEdgeStripes(spriteBatch, rect, horizontal, 5);
    }

    private void DrawOpenDoorLeaf(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, bool leadsToVacuum)
    {
        var mid = leadsToVacuum ? new Color(70, 62, 96) : new Color(58, 64, 70);
        var cap = leadsToVacuum ? new Color(96, 80, 150) : new Color(63, 90, 68);
        spriteBatch.Draw(_pixel, rect, mid);
        DrawDoorLeafCaps(spriteBatch, rect, horizontal, cap);
    }

    // Destroyed is always jammed open (World.Doors.cs), so it gets the same retracted-leaf caps as
    // an ordinary open door, just worn-looking, plus scorch marks and a crack across the middle.
    private void DrawDestroyedDoorLeaf(SpriteBatch spriteBatch, Rectangle rect, bool horizontal)
    {
        spriteBatch.Draw(_pixel, rect, new Color(58, 52, 48));
        DrawDoorLeafCaps(spriteBatch, rect, horizontal, new Color(63, 90, 68) * 0.7f);

        var scorch = new Color(36, 31, 28) * 0.85f;
        var scorchSize = Math.Max(4, Math.Min(rect.Width, rect.Height) / 3);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, scorchSize, scorchSize), scorch);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - scorchSize, rect.Bottom - scorchSize, scorchSize, scorchSize), scorch);

        var crack = new Color(26, 21, 18);
        HudIcons.DrawLine(spriteBatch, _pixel, new Vector2(rect.X + rect.Width * 0.25f, rect.Y), new Vector2(rect.Center.X, rect.Center.Y), crack, 2f);
        HudIcons.DrawLine(spriteBatch, _pixel, new Vector2(rect.Center.X, rect.Center.Y), new Vector2(rect.X + rect.Width * 0.3f, rect.Bottom), crack, 2f);
    }

    // The leaf's own two halves shown slid back into the frame - what actually tells "open" apart
    // from "closed" now, instead of just a different flat color.
    private void DrawDoorLeafCaps(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, Color capColor)
    {
        var capThickness = horizontal ? Math.Max(6, rect.Width / 4) : Math.Max(6, rect.Height / 4);
        if (horizontal)
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, capThickness, rect.Height), capColor);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - capThickness, rect.Y, capThickness, rect.Height), capColor);
        }
        else
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, capThickness), capColor);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - capThickness, rect.Width, capThickness), capColor);
        }
    }

    private void DrawDoorEdgeStripes(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, int thickness)
    {
        if (horizontal)
        {
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Y, rect.Width, thickness), horizontal: true);
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), horizontal: true);
        }
        else
        {
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Y, thickness, rect.Height), horizontal: false);
            DrawHazardStripes(spriteBatch, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), horizontal: false);
        }
    }

    // A small backed light set into the leaf's own middle - the same state color the two side
    // terminals show, just read from the door itself once you're already close to it.
    private void DrawDoorIndicator(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        const int size = 8;
        var center = new Point(rect.Center.X, rect.Center.Y);
        spriteBatch.Draw(_pixel, new Rectangle(center.X - size / 2 - 1, center.Y - size / 2 - 1, size + 2, size + 2), Color.Black * 0.6f);
        spriteBatch.Draw(_pixel, new Rectangle(center.X - size / 2, center.Y - size / 2, size, size), color);
    }

    // The "press to open" control the crew would actually reach for, mounted on the wall on both
    // flanks of the doorway rather than on the door itself (which slides or jams) - one on each
    // side (never both on the same flank), a little below the door's own near end so it reads as
    // reachable rather than floating in the wall. Its own light mirrors the leaf's indicator, so
    // which way a door currently reads is obvious before getting close enough to see the leaf.
    private void DrawDoorTerminals(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, Color indicatorColor)
    {
        const int thickness = 8;
        const int length = 26;
        const int frameMargin = 5;
        const int gap = 2;
        const int alongOffset = 4;

        if (!horizontal)
        {
            var y = rect.Y + alongOffset;
            DrawDoorTerminal(spriteBatch, new Rectangle(rect.X - frameMargin - gap - thickness, y, thickness, length), indicatorColor, vertical: true);
            DrawDoorTerminal(spriteBatch, new Rectangle(rect.Right + frameMargin + gap, y, thickness, length), indicatorColor, vertical: true);
        }
        else
        {
            var x = rect.X + alongOffset;
            DrawDoorTerminal(spriteBatch, new Rectangle(x, rect.Y - frameMargin - gap - thickness, length, thickness), indicatorColor, vertical: false);
            DrawDoorTerminal(spriteBatch, new Rectangle(x, rect.Bottom + frameMargin + gap, length, thickness), indicatorColor, vertical: false);
        }
    }

    private void DrawDoorTerminal(SpriteBatch spriteBatch, Rectangle rect, Color indicatorColor, bool vertical)
    {
        spriteBatch.Draw(_pixel, rect, new Color(49, 54, 60));
        DrawRectOutline(spriteBatch, rect, Color.Black * 0.5f, 1);
        if (vertical)
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + 1, rect.Y + 2, rect.Width - 2, (int)(rect.Height * 0.4f)), new Color(16, 19, 24));
            var lightHeight = Math.Max(2, (int)(rect.Height * 0.25f));
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + 1, rect.Y + rect.Height / 2, rect.Width - 2, lightHeight), indicatorColor);
        }
        else
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + 2, rect.Y + 1, (int)(rect.Width * 0.4f), rect.Height - 2), new Color(16, 19, 24));
            var lightWidth = Math.Max(2, (int)(rect.Width * 0.25f));
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + rect.Width / 2, rect.Y + 1, lightWidth, rect.Height - 2), indicatorColor);
        }
    }
}
