using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// M-doors-as-edges (humble-soaring-cat.md) - the narrow-door-as-a-barrier-between-2-tiles redesign,
// direct user request ("я хочу чтобы на стыке 2 тайлов была палка... эта палка не занимала тайлов").
// Deliberately its OWN rendering rather than shoehorning DrawDoor's elaborate frame/leaf/braces art
// (ShipRenderer.Doors.cs) into a barrier that has no rectangle of its own to fill - this is a
// plan-approved decision, not a placeholder: DrawDoor's art assumes a real 1x2/2x2 footprint rect
// to fill, while an edge door's only geometry is a 1-unit-long LINE sitting exactly on the seam
// between two tiles. Drawn as a Barotrauma-style steel bulkhead door laid along that line
// (DrawDoorEdge's own comment), with a green/red/pulsing-orange state lamp.
public sealed partial class ShipRenderer
{
    private static readonly Color DoorEdgeFrame = new(64, 70, 80);
    private static readonly Color DoorEdgeFrameLit = new(104, 112, 124);
    private static readonly Color DoorEdgeFrameDestroyed = new(70, 64, 60);

    // Shared with Game1.Interactables.cs/Game1.Input.cs's own click/hover hit-testing - the union of
    // both flanking tiles' own full-tile rects (direct user request - "клик по хитбоксу этих 2
    // клеток"), never just the thin seam line itself, so either tile is a legal click target.
    public static Rectangle GetDoorEdgeHitRect(TileCoord coord, TileSide side, Vector2 origin) =>
        Rectangle.Union(GetTileRect(coord, origin), GetTileRect(side.Offset(coord), origin));

    private static Rectangle GetTileRect(TileCoord coord, Vector2 origin) => new(
        (int)(origin.X + coord.X * PixelsPerUnit),
        (int)(origin.Y + coord.Y * PixelsPerUnit),
        (int)PixelsPerUnit,
        (int)PixelsPerUnit);

    // The bar itself: a short perpendicular strip centered exactly on the seam line between `coord`
    // and its neighbor toward `side` (TileGrid.CanonicalEdgeKey's own convention - side is always
    // East or South here, since that's the only shape Ship.DoorEdges ever carries), full 1-unit
    // length along the seam, a fraction of a tile thick across it - never widening into either
    // tile's own floor space, unlike a real footprint-based door.
    // Direct user requests: "сделай двери красивую текстуры в нашем стиле, а то зеленая полоска
    // смотрится уродливо" (the bar used to be one flat green/red/violet fill - the state colour WAS
    // the door), then "сделай ее как на космическом корабле а также похожей на дверь в баротравме"
    // (a first copper two-leaf version was rejected). So: a Barotrauma bulkhead - one heavy steel
    // slab, top-lit, stiffened by cross ribs, running in a dark recessed track between two
    // gunmetal frame posts with bolts. Open, the slab has slid away into the first post and only
    // its end shows above the empty threshold; destroyed, it's a bent, scorched stump. The state
    // colour survives only as a small lamp on each post. A door onto vacuum keeps its hazard tape.
    private static readonly Color[] DoorEdgeSlabBands = { new(158, 166, 176), new(132, 140, 152), new(108, 116, 128), new(86, 92, 104) };
    private static readonly Color DoorEdgeSlabRib = new(58, 64, 74);
    private static readonly Color DoorEdgeSlabRibLit = new(184, 192, 202);
    private static readonly Color DoorEdgeSlabCap = new(70, 76, 88);
    private static readonly Color DoorEdgeSlabOutline = new(34, 38, 46);
    private static readonly Color DoorEdgeTrack = new(26, 28, 34);
    private static readonly Color DoorEdgeTrackLit = new(66, 72, 82);
    private static readonly Color DoorEdgeSlot = new(12, 14, 18);
    private static readonly Color DoorEdgeScorch = new(38, 33, 30);

    internal void DrawDoorEdge(SpriteBatch spriteBatch, TileCoord coord, TileSide side, bool isOpen,
        bool destroyed, Vector2 origin, float totalSeconds = 0f, bool leadsToVacuum = false)
    {
        const float thicknessUnits = 0.22f;
        var thickness = Math.Max(6, (int)(thicknessUnits * PixelsPerUnit));
        var length = (int)PixelsPerUnit;
        var half = thickness / 2;
        var postLength = Math.Max(4, length / 7);
        var postHalf = half + 3;
        var vertical = side == TileSide.East; // the seam runs top-to-bottom, so the door does too
        var seam = vertical
            ? (int)(origin.X + (coord.X + 1) * PixelsPerUnit)
            : (int)(origin.Y + (coord.Y + 1) * PixelsPerUnit);
        var alongStart = vertical
            ? (int)(origin.Y + coord.Y * PixelsPerUnit)
            : (int)(origin.X + coord.X * PixelsPerUnit);

        // Everything below is laid out once in door-local terms - `along` the seam from the door's
        // own start, `across` it from the seam line itself - so one drawing serves both orientations.
        Rectangle Part(int along, int alongLength, int across, int acrossLength) => vertical
            ? new Rectangle(seam + across, alongStart + along, acrossLength, alongLength)
            : new Rectangle(alongStart + along, seam + across, alongLength, acrossLength);

        var trackLength = length - postLength * 2;
        spriteBatch.Draw(_pixel, Part(postLength, trackLength, -half, thickness), destroyed ? DoorEdgeScorch : DoorEdgeTrack);
        spriteBatch.Draw(_pixel, Part(postLength, trackLength, -half, 1), DoorEdgeTrackLit);
        spriteBatch.Draw(_pixel, Part(postLength, trackLength, half - 1, 1), Color.Black * 0.5f);

        if (destroyed)
            DrawDestroyedDoorEdgeSlab(spriteBatch, Part, postLength, trackLength, half, vertical);
        else if (isOpen)
            DrawOpenDoorEdgeSlab(spriteBatch, Part, postLength, trackLength, half, vertical, leadsToVacuum);
        else
            DrawClosedDoorEdgeSlab(spriteBatch, Part, postLength, trackLength, half, vertical, leadsToVacuum);

        // Same "leadsToVacuum only changes the OPEN color" rule DrawDoor's own leaf uses - a closed
        // edge reads as sealed red regardless of what's behind it.
        var indicator = destroyed
            ? Color.OrangeRed * (0.6f + 0.4f * MathF.Sin(totalSeconds * 6f))
            : isOpen
                ? (leadsToVacuum ? new Color(190, 140, 255) : new Color(90, 230, 120))
                : new Color(255, 90, 90);
        DrawDoorEdgePost(spriteBatch, Part(0, postLength, -postHalf, postHalf * 2), destroyed, indicator, vertical);
        DrawDoorEdgePost(spriteBatch, Part(length - postLength, postLength, -postHalf, postHalf * 2), destroyed, indicator, vertical);
    }

    private delegate Rectangle DoorEdgePart(int along, int alongLength, int across, int acrossLength);

    // One top-lit steel slab from `from` to `to` along the door: darker end caps, a cross rib every
    // few pixels (a dark groove with a lit edge beside it), and a dark outline round the lot.
    private void DrawDoorEdgeSlab(SpriteBatch spriteBatch, DoorEdgePart part, int from, int to, int leafHalf, bool vertical)
    {
        var slab = part(from, to - from, -leafHalf, leafHalf * 2);
        DrawTopLitBands(spriteBatch, slab, horizontal: !vertical, DoorEdgeSlabBands);

        const int ribSpacing = 6;
        for (var along = from + ribSpacing; along < to - 3; along += ribSpacing)
        {
            spriteBatch.Draw(_pixel, part(along, 1, -leafHalf, leafHalf * 2), DoorEdgeSlabRib);
            spriteBatch.Draw(_pixel, part(along + 1, 1, -leafHalf, leafHalf * 2), DoorEdgeSlabRibLit * 0.55f);
        }

        spriteBatch.Draw(_pixel, part(from, 2, -leafHalf, leafHalf * 2), DoorEdgeSlabCap);
        spriteBatch.Draw(_pixel, part(to - 2, 2, -leafHalf, leafHalf * 2), DoorEdgeSlabCap);
        DrawRectOutline(spriteBatch, slab, DoorEdgeSlabOutline, 1);
    }

    private void DrawClosedDoorEdgeSlab(SpriteBatch spriteBatch, DoorEdgePart part, int trackStart, int trackLength,
        int half, bool vertical, bool leadsToVacuum)
    {
        var leafHalf = half - 1;
        DrawDoorEdgeSlab(spriteBatch, part, trackStart, trackStart + trackLength, leafHalf, vertical);
        if (leadsToVacuum)
        {
            DrawHazardStripes(spriteBatch, part(trackStart + 2, trackLength - 4, -leafHalf, 2), horizontal: !vertical);
            DrawHazardStripes(spriteBatch, part(trackStart + 2, trackLength - 4, leafHalf - 2, 2), horizontal: !vertical);
        }
    }

    // Slid away into the first post: just the slab's end still showing, then the empty threshold -
    // the recessed track with its centre guide slot, hazard-taped when what's past it is vacuum.
    private void DrawOpenDoorEdgeSlab(SpriteBatch spriteBatch, DoorEdgePart part, int trackStart, int trackLength,
        int half, bool vertical, bool leadsToVacuum)
    {
        var leafHalf = half - 1;
        var stub = Math.Max(4, trackLength / 7);
        var threshold = part(trackStart + stub, trackLength - stub, -leafHalf, leafHalf * 2);
        if (leadsToVacuum)
            DrawHazardStripes(spriteBatch, threshold, horizontal: !vertical);
        spriteBatch.Draw(_pixel, part(trackStart + stub, trackLength - stub, -1, 2), DoorEdgeSlot);
        DrawDoorEdgeSlab(spriteBatch, part, trackStart, trackStart + stub, leafHalf, vertical);
    }

    // Destroyed is always jammed open (World.Doors.cs) - a bent, burnt stump of the slab sticking
    // out of its post, and a crack across the scorched track past it.
    private void DrawDestroyedDoorEdgeSlab(SpriteBatch spriteBatch, DoorEdgePart part, int trackStart, int trackLength,
        int half, bool vertical)
    {
        var leafHalf = half - 1;
        var stump = Math.Max(6, trackLength / 3);
        var burnt = new Color(84, 78, 74);
        spriteBatch.Draw(_pixel, part(trackStart, stump, -leafHalf, leafHalf * 2), burnt);
        spriteBatch.Draw(_pixel, part(trackStart, stump, -leafHalf, 2), new Color(110, 102, 96));
        spriteBatch.Draw(_pixel, part(trackStart + stump - 3, 3, -leafHalf + 2, leafHalf * 2 - 2), DoorEdgeScorch); // torn end
        DrawRectOutline(spriteBatch, part(trackStart, stump, -leafHalf, leafHalf * 2), DoorEdgeSlabOutline, 1);

        var crackFrom = part(trackStart + trackLength / 2, 1, -leafHalf, 1);
        var crackTo = part(trackStart + trackLength * 5 / 6, 1, leafHalf - 1, 1);
        HudIcons.DrawLine(spriteBatch, _pixel, new Vector2(crackFrom.X, crackFrom.Y), new Vector2(crackTo.X, crackTo.Y), new Color(14, 12, 10), 2f);
    }

    // A gunmetal frame post at one end of the door - the housing the slab slides into, wider than
    // the slab so it reads as bolted into the wall: top-lit like DrawDoorFrame's bezel, a bolt head
    // at each outer corner, and the door's state lamp set into its middle.
    private void DrawDoorEdgePost(SpriteBatch spriteBatch, Rectangle post, bool destroyed, Color indicator, bool vertical)
    {
        spriteBatch.Draw(_pixel, post, destroyed ? DoorEdgeFrameDestroyed : DoorEdgeFrame);
        if (!destroyed)
            spriteBatch.Draw(_pixel, new Rectangle(post.X, post.Y, post.Width, Math.Max(1, post.Height / 4)), DoorEdgeFrameLit);
        DrawRectOutline(spriteBatch, post, Color.Black * 0.6f, 1);

        // Bolts on the two ends sticking out past the slab, either side of the lamp.
        var boltA = vertical ? new Point(post.X + 2, post.Center.Y) : new Point(post.Center.X, post.Y + 2);
        var boltB = vertical ? new Point(post.Right - 3, post.Center.Y) : new Point(post.Center.X, post.Bottom - 3);
        foreach (var bolt in new[] { boltA, boltB })
        {
            spriteBatch.Draw(_pixel, new Rectangle(bolt.X, bolt.Y, 1, 1), Color.White * 0.45f);
            spriteBatch.Draw(_pixel, new Rectangle(bolt.X, bolt.Y + 1, 1, 1), Color.Black * 0.5f);
        }

        const int lamp = 3;
        var center = post.Center;
        spriteBatch.Draw(_pixel, new Rectangle(center.X - lamp / 2 - 1, center.Y - lamp / 2 - 1, lamp + 2, lamp + 2), Color.Black * 0.6f);
        spriteBatch.Draw(_pixel, new Rectangle(center.X - lamp / 2, center.Y - lamp / 2, lamp, lamp), indicator);
    }
}
