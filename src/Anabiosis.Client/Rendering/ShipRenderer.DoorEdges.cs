using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// M-doors-as-edges (humble-soaring-cat.md) - the narrow-door-as-a-barrier-between-2-tiles redesign,
// direct user request ("я хочу чтобы на стыке 2 тайлов была палка... эта палка не занимала тайлов").
// Deliberately its OWN, much simpler rendering rather than shoehorning DrawDoor's elaborate frame/
// leaf/braces art (ShipRenderer.Doors.cs) into a barrier that has no rectangle of its own to fill -
// this is a plan-approved decision, not a placeholder: DrawDoor's art assumes a real 1x2/2x2
// footprint rect to fill, while an edge door's only geometry is a 1-unit-long LINE sitting exactly
// on the seam between two tiles. Same color language (bronze frame, green/red/pulsing-orange
// indicator) so it still reads as "the same kind of door," just drawn as a short bar instead.
public sealed partial class ShipRenderer
{
    private static readonly Color DoorEdgeFrame = new(90, 68, 46); // same bronze as DoorFrame
    private static readonly Color DoorEdgeFrameDestroyed = new(92, 80, 72);

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
    internal void DrawDoorEdge(SpriteBatch spriteBatch, TileCoord coord, TileSide side, bool isOpen,
        bool destroyed, Vector2 origin, float totalSeconds = 0f)
    {
        const float thicknessUnits = 0.22f;
        var thickness = Math.Max(4, (int)(thicknessUnits * PixelsPerUnit));
        var length = (int)PixelsPerUnit;

        Rectangle bar, frame;
        if (side == TileSide.East)
        {
            var seamX = (int)(origin.X + (coord.X + 1) * PixelsPerUnit);
            var y = (int)(origin.Y + coord.Y * PixelsPerUnit);
            frame = new Rectangle(seamX - thickness / 2 - 2, y, thickness + 4, length);
            bar = new Rectangle(seamX - thickness / 2, y, thickness, length);
        }
        else
        {
            var seamY = (int)(origin.Y + (coord.Y + 1) * PixelsPerUnit);
            var x = (int)(origin.X + coord.X * PixelsPerUnit);
            frame = new Rectangle(x, seamY - thickness / 2 - 2, length, thickness + 4);
            bar = new Rectangle(x, seamY - thickness / 2, length, thickness);
        }

        spriteBatch.Draw(_pixel, frame, destroyed ? DoorEdgeFrameDestroyed : DoorEdgeFrame);

        var indicator = destroyed
            ? Color.OrangeRed * (0.6f + 0.4f * MathF.Sin(totalSeconds * 6f))
            : isOpen
                ? new Color(90, 230, 120)
                : new Color(255, 90, 90);
        spriteBatch.Draw(_pixel, bar, indicator);
    }
}
