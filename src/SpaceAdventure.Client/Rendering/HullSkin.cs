using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// The ship's outer shell - the armour wrapped around the compartments, drawn underneath the whole
// interior so it shows as a plated border round the decks and as the bow that sticks out ahead of
// them. Compartments have to stay rectangles (the walking and the sight lines are separated per
// axis against those rectangles), but the hull built on top of them does not: the plates are cut
// at the corners and the bow is a dome, so the ship reads as a hull rather than as a floor plan.
//
// Always drawn the same way regardless of where the camera is (manning a turret, walking the
// decks, drifting outside) - the ship is one continuous space, not a "closed up" exterior model
// swapped in for the interior one. A turret's periscope view used to substitute deck plating for
// the compartments here specifically so a breach couldn't be seen through from behind the gun;
// now ShipRenderer always draws the real interior (including any breach), so there is nothing
// left for a separate "closed" mode to hide.
public static partial class HullSkin
{
    // How far the armour stands off the compartment walls, and how much is taken off each corner.
    public const float MarginUnits = 0.36f;
    private const float CornerCutUnits = 0.85f;
    private const float NoseLengthUnits = 2.3f;
    private const int NoseSegments = 14;

    // Matched to TileTextures' own HullPlateColor/HullCoreColor - the tiled texture now carries its
    // real gunmetal colour itself (see DrawTiled's Color.White tint below), so the flat fills here
    // (nose, stern brace, flank pods) have to use the same tones by hand instead of relying on a
    // shared tint to make them agree.
    private static readonly Color Plate = new(64, 71, 80);
    private static readonly Color PlateLit = new(92, 100, 112);
    private static readonly Color Edge = new(120, 133, 152);
    private static readonly Color Seam = new(30, 36, 45);

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Texture2D[] hullPlates, IReadOnlyList<Room> rooms,
        IReadOnlyList<AirlockOuterDoor> ports, IReadOnlyList<ShipSystemDevice> devices, Vector2 origin,
        float forwardDegrees, ShipKind shipKind = ShipKind.Frigate, float totalSeconds = 0f,
        IReadOnlyList<ShipSystemState>? systemStates = null)
    {
        if (rooms.Count == 0)
            return;

        var bow = Forward(forwardDegrees);
        var hullCenter = HullCenter(rooms, origin);

        // Everything that hangs off the hull goes down first, so the compartment plates cover the
        // joints where it is bolted on.
        DrawNose(spriteBatch, pixel, ForwardMost(rooms, bow), bow, origin);
        DrawSternBrace(spriteBatch, pixel, rooms, bow, origin);
        DrawEngineNozzles(spriteBatch, pixel, rooms, devices, bow, origin);
        DrawRadiatorFins(spriteBatch, pixel, rooms, hullCenter, bow, origin);

        foreach (var room in rooms)
        {
            var margins = ComputeMargins(room, rooms);
            var plate = PlatePolygon(room, rooms, margins, origin);
            var center = RoomCenter(room, origin);
            Primitives.FillPolygon(spriteBatch, pixel, center, plate, Plate);
            DrawHullPlating(spriteBatch, pixel, hullPlates, room, rooms, margins, origin);
            Primitives.StrokePolygon(spriteBatch, pixel, plate, Edge * 0.6f, 2.5f);
            DrawPlateShading(spriteBatch, pixel, room, margins, origin);
            DrawFlankGreebles(spriteBatch, pixel, room, hullCenter, origin);
            if (RoomHasDamagedDevice(room, devices, systemStates))
                DrawHullDamage(spriteBatch, pixel, room, origin, totalSeconds);
        }

        // Paint and fittings last: they sit on top of the armour, not under it.
        DrawLivery(spriteBatch, pixel, rooms, bow, origin, shipKind);
        DrawDockingCollars(spriteBatch, pixel, ports, hullCenter, origin);
        DrawNavigationLights(spriteBatch, pixel, rooms, bow, origin);
        DrawViewports(spriteBatch, pixel, rooms, hullCenter, origin, totalSeconds);
    }

    // A dome on the front of the forward-most compartment: a half ellipse from one flank to the
    // other, with a canopy set into it. This is the single line that turns the layout into a ship -
    // a stack of boxes has no front, and a hull that flies bow-first needs one to be legible.
    private static void DrawNose(SpriteBatch spriteBatch, Texture2D pixel, Room room, Vector2 bow, Vector2 origin)
    {
        var side = new Vector2(-bow.Y, bow.X);
        var center = RoomCenter(room, origin);
        var halfSpan = MathF.Abs(Vector2.Dot(new Vector2(room.Width, room.Height) / 2f, side)) + MarginUnits;
        var reach = MathF.Abs(Vector2.Dot(new Vector2(room.Width, room.Height) / 2f, bow)) + MarginUnits;

        var mouth = center + bow * reach * ShipRenderer.PixelsPerUnit;
        // Matched to the straight part of the compartment's plate, i.e. its width minus the two
        // corners taken off it - a dome the full width of the plate overhangs those cuts and reads
        // as a cap sitting on the ship rather than as the ship's own bow.
        var span = halfSpan * ShipRenderer.PixelsPerUnit - CornerCutUnits * ShipRenderer.PixelsPerUnit;
        var length = NoseLengthUnits * ShipRenderer.PixelsPerUnit;

        var points = new Vector2[NoseSegments + 1];
        for (var i = 0; i <= NoseSegments; i++)
        {
            var angle = -MathF.PI / 2f + i * (MathF.PI / NoseSegments);
            points[i] = mouth + bow * (length * MathF.Cos(angle)) + side * (span * MathF.Sin(angle));
        }

        Primitives.FillPolygon(spriteBatch, pixel, mouth, points, Plate);
        Primitives.StrokePolygon(spriteBatch, pixel, points, Edge * 0.55f, 2f);

        // Canopy: a smaller dome of glass just ahead of the compartment, lit along its leading edge.
        var canopy = new Vector2[NoseSegments + 1];
        for (var i = 0; i <= NoseSegments; i++)
        {
            var angle = -MathF.PI / 2f + i * (MathF.PI / NoseSegments);
            canopy[i] = mouth + bow * (length * 0.62f * MathF.Cos(angle)) + side * (span * 0.62f * MathF.Sin(angle));
        }
        Primitives.FillPolygon(spriteBatch, pixel, mouth, canopy, new Color(46, 74, 92));
        Primitives.StrokePolygon(spriteBatch, pixel, canopy, new Color(120, 190, 220) * 0.5f, 2f);

        // Spine seam running out to the tip, the way a nose section is actually welded together.
        var tip = mouth + bow * length;
        var seam = tip - mouth;
        spriteBatch.Draw(pixel, mouth, null, Seam * 0.8f, MathF.Atan2(seam.Y, seam.X), new Vector2(0f, 0.5f),
            new Vector2(seam.Length(), 2f), SpriteEffects.None, 0f);
    }

    // A beam across the gap between the two compartments that reach furthest aft. On a hull whose
    // tail is a pair of engine pylons - the corvette's is - the two of them alone read as legs;
    // one bar tying them together at the back turns them into a stern with nacelles on it.
    // Nothing is drawn when the tail is a single flat end, which is every other hull here.
    private static void DrawSternBrace(SpriteBatch spriteBatch, Texture2D pixel, IReadOnlyList<Room> rooms, Vector2 bow, Vector2 origin)
    {
        var side = new Vector2(-bow.Y, bow.X);
        var spans = new List<(float Aft, float From, float To)>();
        foreach (var room in rooms)
        {
            var center = new Vector2(room.Center.X, room.Center.Y);
            var half = new Vector2(room.Width, room.Height) / 2f;
            var lateral = Vector2.Dot(center, side);
            var reach = MathF.Abs(Vector2.Dot(half, side));
            spans.Add((Vector2.Dot(center, -bow) + MathF.Abs(Vector2.Dot(half, bow)),
                lateral - reach, lateral + reach));
        }
        spans.Sort((a, b) => a.From.CompareTo(b.From));

        var sternMost = float.NegativeInfinity;
        foreach (var span in spans)
            sternMost = MathF.Max(sternMost, span.Aft);

        var pylons = spans.FindAll(s => sternMost - s.Aft < 0.25f);
        if (pylons.Count < 2)
            return;

        const float depth = 1.5f;
        const float inset = 0.3f;
        for (var i = 0; i < pylons.Count - 1; i++)
        {
            var from = pylons[i].To;
            var to = pylons[i + 1].From;
            if (to - from < 0.5f)
                continue;

            var near = sternMost - depth;
            var far = sternMost - inset;
            var corners = new[]
            {
                LayoutPoint(bow, side, near, from, origin), LayoutPoint(bow, side, near, to, origin),
                LayoutPoint(bow, side, far, to, origin), LayoutPoint(bow, side, far, from, origin),
            };
            var middle = (corners[0] + corners[2]) / 2f;
            Primitives.FillPolygon(spriteBatch, pixel, middle, corners, Plate);
            Primitives.StrokePolygon(spriteBatch, pixel, corners, Edge * 0.5f, 2f);
        }
    }

    // Back out of the (distance aft, distance to one side) frame the stern brace is measured in.
    private static Vector2 LayoutPoint(Vector2 bow, Vector2 side, float aft, float lateral, Vector2 origin)
    {
        var layout = -bow * aft + side * lateral;
        return origin + layout * ShipRenderer.PixelsPerUnit;
    }

    // A lit edge along the top and left of every plate and a shadow along the bottom and right.
    // One flat tone over the whole hull reads as a cut-out; the same tone with a light on it reads
    // as armour with thickness.
    private static void DrawPlateShading(SpriteBatch spriteBatch, Texture2D pixel, Room room, RoomMargins margins, Vector2 origin)
    {
        var rect = RoomRect(room, origin);
        var plate = new Rectangle((int)(rect.X - margins.Left), (int)(rect.Y - margins.Top),
            (int)(rect.Width + margins.Left + margins.Right), (int)(rect.Height + margins.Top + margins.Bottom));
        var cut = (int)(CornerCutUnits * ShipRenderer.PixelsPerUnit);

        spriteBatch.Draw(pixel, new Rectangle(plate.X + cut, plate.Y + 2, plate.Width - cut * 2, 3), Color.White * 0.09f);
        spriteBatch.Draw(pixel, new Rectangle(plate.X + 2, plate.Y + cut, 3, plate.Height - cut * 2), Color.White * 0.06f);
        spriteBatch.Draw(pixel, new Rectangle(plate.X + cut, plate.Bottom - 5, plate.Width - cut * 2, 3), Color.Black * 0.35f);
        spriteBatch.Draw(pixel, new Rectangle(plate.Right - 5, plate.Y + cut, 3, plate.Height - cut * 2), Color.Black * 0.28f);
    }

    // Red to port, green to starboard, at the widest point of the hull - the one convention every
    // vessel in the world shares, and the quickest way for a shape in the dark to read as a ship
    // with a front and a left and a right.
    private static void DrawNavigationLights(SpriteBatch spriteBatch, Texture2D pixel, IReadOnlyList<Room> rooms, Vector2 bow, Vector2 origin)
    {
        var side = new Vector2(-bow.Y, bow.X);
        var port = float.PositiveInfinity;
        var starboard = float.NegativeInfinity;
        var alongAtPort = 0f;
        var alongAtStarboard = 0f;

        foreach (var room in rooms)
        {
            var center = new Vector2(room.Center.X, room.Center.Y);
            var half = new Vector2(room.Width, room.Height) / 2f;
            var lateral = Vector2.Dot(center, side);
            var reach = MathF.Abs(Vector2.Dot(half, side));
            var along = Vector2.Dot(center, -bow);
            if (lateral - reach < port)
            {
                port = lateral - reach;
                alongAtPort = along;
            }
            if (lateral + reach > starboard)
            {
                starboard = lateral + reach;
                alongAtStarboard = along;
            }
        }

        DrawLamp(spriteBatch, pixel, LayoutPoint(bow, side, alongAtPort, port - MarginUnits * 0.5f, origin), new Color(255, 70, 70));
        DrawLamp(spriteBatch, pixel, LayoutPoint(bow, side, alongAtStarboard, starboard + MarginUnits * 0.5f, origin), new Color(90, 255, 130));
    }

    private static void DrawLamp(SpriteBatch spriteBatch, Texture2D pixel, Vector2 position, Color color)
    {
        for (var i = 3; i >= 1; i--)
            spriteBatch.Draw(pixel, position, null, color * (0.12f * i), 0f, new Vector2(0.5f, 0.5f),
                new Vector2(4f + i * 5f, 4f + i * 5f), SpriteEffects.None, 0f);
        spriteBatch.Draw(pixel, position, null, Color.Lerp(color, Color.White, 0.5f), 0f, new Vector2(0.5f, 0.5f),
            new Vector2(6f, 6f), SpriteEffects.None, 0f);
    }

    // How long a room's own flank has to be, in world units, before it gets its own row of pods.
    // Without this, a run of small rooms stepping diagonally (a staircase built in the ship editor
    // to fake a slanted wall) gets a pod on every single step - a toothed row of little
    // protrusions where the hull should just read as one wall.
    private const float MinGreebleFlankUnits = 2f;

    // Sensor pods and tanks bolted to whichever flank faces away from the middle of the ship. They
    // cost nothing and they do most of the work of making a silhouette look built rather than
    // extruded - the hull stops being one unbroken outline.
    private static void DrawFlankGreebles(SpriteBatch spriteBatch, Texture2D pixel, Room room, Vector2 hullCenter, Vector2 origin)
    {
        var rect = RoomRect(room, origin);
        var center = RoomCenter(room, origin);
        var outward = center - hullCenter;
        if (outward.LengthSquared() < 1f)
            return;

        var horizontal = MathF.Abs(outward.X) > MathF.Abs(outward.Y);
        if ((horizontal ? room.Height : room.Width) < MinGreebleFlankUnits)
            return;
        var sign = horizontal ? MathF.Sign(outward.X) : MathF.Sign(outward.Y);
        var margin = (int)(MarginUnits * ShipRenderer.PixelsPerUnit);

        for (var i = -1; i <= 1; i++)
        {
            var along = i * (horizontal ? rect.Height : rect.Width) / 3.4f;
            var pod = horizontal
                ? new Rectangle((int)(sign > 0 ? rect.Right + margin - 4 : rect.X - margin - 6), (int)(center.Y + along) - 7, 10, 14)
                : new Rectangle((int)(center.X + along) - 7, (int)(sign > 0 ? rect.Bottom + margin - 4 : rect.Y - margin - 6), 14, 10);

            spriteBatch.Draw(pixel, pod, PlateLit);
            spriteBatch.Draw(pixel, new Rectangle(pod.X, pod.Y, pod.Width, 2), Edge * 0.5f);
        }
    }

    // How far the plate reaches past the room on each of its four sides (the same slim margin on
    // every side - the plate's footprint never grew for the tiled texture), and which of those
    // sides are open to space rather than facing a neighbouring compartment. Computed once per
    // room and threaded through PlatePolygon/DrawPlateShading/DrawHullPlating so all three agree on
    // the same silhouette instead of three separate margin calculations quietly drifting apart.
    private readonly record struct RoomMargins(float Top, float Bottom, float Left, float Right,
        bool TopExterior, bool BottomExterior, bool LeftExterior, bool RightExterior);

    // Interior (room-to-room) edges keep the flat, untextured bezel - texturing never mattered
    // there, since the room on the other side paints its own floor over any of it that would show.
    private static RoomMargins ComputeMargins(Room room, IReadOnlyList<Room> rooms)
    {
        var thin = MarginUnits * ShipRenderer.PixelsPerUnit;
        var top = IsExteriorEdge(room, rooms, 0);
        var bottom = IsExteriorEdge(room, rooms, 1);
        var left = IsExteriorEdge(room, rooms, 2);
        var right = IsExteriorEdge(room, rooms, 3);
        return new RoomMargins(thin, thin, thin, thin, top, bottom, left, right);
    }

    // Whether this edge of the room faces open space rather than another compartment - checked
    // against every other room for any overlapping run along the shared coordinate. Rooms in every
    // ship built so far touch edge-to-edge with no gap (see Ship.*.cs), so "shares this coordinate
    // and overlaps the span" is a reliable stand-in for "there is a neighbour here" without needing
    // an actual adjacency graph. Only used for HullSkin's own margin now - ShipRenderer's room
    // walls stopped distinguishing exterior from interior and carry this same plate texture on
    // every side, since that read better than mixing it with the plainer interior wall pattern.
    private static bool IsExteriorEdge(Room room, IReadOnlyList<Room> rooms, int edge)
    {
        const float epsilon = 0.02f;
        foreach (var other in rooms)
        {
            if (ReferenceEquals(other, room))
                continue;
            var sharesEdge = edge switch
            {
                0 => MathF.Abs(other.Bottom - room.Top) < epsilon,
                1 => MathF.Abs(other.Top - room.Bottom) < epsilon,
                2 => MathF.Abs(other.Right - room.Left) < epsilon,
                _ => MathF.Abs(other.Left - room.Right) < epsilon,
            };
            if (!sharesEdge)
                continue;
            var overlapsSpan = edge is 0 or 1
                ? other.Left < room.Right - epsilon && other.Right > room.Left + epsilon
                : other.Top < room.Bottom - epsilon && other.Bottom > room.Top + epsilon;
            if (overlapsSpan)
                return false;
        }
        return true;
    }

    // The hull plate's own texture, drawn flush against the room on every exterior edge - no gap,
    // no separate flat band first, this *is* the plate now. DrawSquares tiles it as a run of whole
    // square blocks exactly as thick as the plate's own slim margin, each one the complete 64px
    // design scaled down to fit rather than a crop of it - the whole plate in miniature, repeating,
    // not a sliver of just its frame. Extended into a shared corner whenever the adjoining edge is
    // exterior too so two open edges meet without a gap. Interior edges get nothing at all here
    // (the flat bezel from the base FillPolygon in Draw already covers them, unfaded, exactly as
    // before this whole texture pass existed).
    private static void DrawHullPlating(SpriteBatch spriteBatch, Texture2D pixel, Texture2D[] hullPlates, Room room, IReadOnlyList<Room> rooms, RoomMargins margins, Vector2 origin)
    {
        var rect = RoomRect(room, origin);
        var sourceSize = TileTextures.HullTileSize;
        var cellOrigin = new Point((int)origin.X, (int)origin.Y);

        if (margins.TopExterior || margins.BottomExterior)
        {
            var x = rect.X - (margins.LeftExterior ? (int)margins.Left : 0);
            var width = rect.Width + (margins.LeftExterior ? (int)margins.Left : 0) + (margins.RightExterior ? (int)margins.Right : 0);
            if (margins.TopExterior)
                TileTextures.DrawSquares(spriteBatch, hullPlates, sourceSize, (int)margins.Top, new Rectangle(x, (int)(rect.Y - margins.Top), width, (int)margins.Top), Color.White, cellOrigin);
            if (margins.BottomExterior)
                TileTextures.DrawSquares(spriteBatch, hullPlates, sourceSize, (int)margins.Bottom, new Rectangle(x, rect.Bottom, width, (int)margins.Bottom), Color.White, cellOrigin);
        }
        if (margins.LeftExterior || margins.RightExterior)
        {
            var y = rect.Y - (margins.TopExterior ? (int)margins.Top : 0);
            var height = rect.Height + (margins.TopExterior ? (int)margins.Top : 0) + (margins.BottomExterior ? (int)margins.Bottom : 0);
            if (margins.LeftExterior)
                TileTextures.DrawSquares(spriteBatch, hullPlates, sourceSize, (int)margins.Left, new Rectangle((int)(rect.X - margins.Left), y, (int)margins.Left, height), Color.White, cellOrigin);
            if (margins.RightExterior)
                TileTextures.DrawSquares(spriteBatch, hullPlates, sourceSize, (int)margins.Right, new Rectangle(rect.Right, y, (int)margins.Right, height), Color.White, cellOrigin);
        }

        // The bands above are plain rectangles and paint straight into the plate's own cut corners
        // on any exterior side; mask those back out with the same flat fill PlatePolygon's own
        // silhouette uses, reproducing the corner cut this would otherwise get for free from simply
        // never drawing there. Skipped wherever PlatePolygon itself left that corner square (see
        // FindDiagonalNeighbor) - masking a cut there would carve a notch out of a corner that is
        // no longer cut.
        if (margins.TopExterior || margins.BottomExterior || margins.LeftExterior || margins.RightExterior)
        {
            var outerLeft = rect.X - margins.Left;
            var outerTop = rect.Y - margins.Top;
            var outerWidth = rect.Width + margins.Left + margins.Right;
            var outerHeight = rect.Height + margins.Top + margins.Bottom;
            var cut = MathF.Min(CornerCutUnits * ShipRenderer.PixelsPerUnit, MathF.Min(outerWidth, outerHeight) / 3f);
            if (FindDiagonalNeighbor(room, rooms, 0) is null)
                MaskPlateCorner(spriteBatch, pixel, new Vector2(outerLeft, outerTop), cut, mirrorX: false, mirrorY: false);
            if (FindDiagonalNeighbor(room, rooms, 1) is null)
                MaskPlateCorner(spriteBatch, pixel, new Vector2(outerLeft + outerWidth, outerTop), cut, mirrorX: true, mirrorY: false);
            if (FindDiagonalNeighbor(room, rooms, 3) is null)
                MaskPlateCorner(spriteBatch, pixel, new Vector2(outerLeft, outerTop + outerHeight), cut, mirrorX: false, mirrorY: true);
            if (FindDiagonalNeighbor(room, rooms, 2) is null)
                MaskPlateCorner(spriteBatch, pixel, new Vector2(outerLeft + outerWidth, outerTop + outerHeight), cut, mirrorX: true, mirrorY: true);
        }

        DrawReentrantChamfers(spriteBatch, pixel, room, rooms, margins, origin);
    }

    // Whether another room's rectangle touches this one at exactly this corner point - a diagonal,
    // single-point join - rather than along a shared edge run. IsExteriorEdge's span-overlap test
    // only rules out edges that actually share a run; two rooms meeting only at a corner (an
    // L-shaped layout) both still read every edge there as "exterior", so both independently cut
    // that corner, and since the two rooms are rarely the same size the two cuts don't line up -
    // leaving a sliver of bare background between them. Corners are numbered the same way as the
    // vertices PlatePolygon builds: 0 top-left, 1 top-right, 2 bottom-right, 3 bottom-left. Returns
    // the neighbour itself (not just whether one exists) - DrawReentrantChamfers needs its rectangle
    // too, to bridge two different rooms' exterior edges into one shared diagonal.
    private static Room? FindDiagonalNeighbor(Room room, IReadOnlyList<Room> rooms, int corner)
    {
        const float epsilon = 0.02f;
        foreach (var other in rooms)
        {
            if (ReferenceEquals(other, room))
                continue;
            var touches = corner switch
            {
                0 => MathF.Abs(other.Right - room.Left) < epsilon && MathF.Abs(other.Bottom - room.Top) < epsilon,
                1 => MathF.Abs(other.Left - room.Right) < epsilon && MathF.Abs(other.Bottom - room.Top) < epsilon,
                2 => MathF.Abs(other.Left - room.Right) < epsilon && MathF.Abs(other.Top - room.Bottom) < epsilon,
                _ => MathF.Abs(other.Right - room.Left) < epsilon && MathF.Abs(other.Top - room.Bottom) < epsilon,
            };
            if (touches)
                return other;
        }
        return null;
    }

    // A room's exterior edge meeting a diagonal neighbour's exterior edge at the same point, with
    // a third, empty quadrant between them - an L-shaped layout, three compartments around a
    // point and open space in the fourth. Each edge's own margin band extends outward
    // independently there, so the two together read as a doubled-up, sharp right-angle nub rather
    // than a clean line. Chamfers it into a single diagonal the size of an ordinary convex corner
    // cut, using the same flat-plate mask - one call per corner, and only when exactly one of that
    // corner's two edges is exterior (the "square, don't cut" case above already covers a
    // neighbour whose *own* edges are both exterior, i.e. two rooms touching only at that point
    // with nothing else there at all).
    private static void DrawReentrantChamfers(SpriteBatch spriteBatch, Texture2D pixel, Room room, IReadOnlyList<Room> rooms, RoomMargins margins, Vector2 origin)
    {
        var rect = RoomRect(room, origin);
        var cut = MathF.Min(CornerCutUnits * ShipRenderer.PixelsPerUnit, MathF.Min(rect.Width, rect.Height) / 3f);
        var thin = MarginUnits * ShipRenderer.PixelsPerUnit;

        if (margins.TopExterior != margins.LeftExterior)
        {
            var neighbor = FindDiagonalNeighbor(room, rooms, 0);
            if (neighbor is not null)
            {
                var n = RoomRect(neighbor, origin);
                if (margins.TopExterior)
                    MaskPlateCorner(spriteBatch, pixel, new Vector2(n.Right + thin, rect.Y - margins.Top), cut, mirrorX: true, mirrorY: false);
                else
                    MaskPlateCorner(spriteBatch, pixel, new Vector2(rect.X - margins.Left, n.Bottom + thin), cut, mirrorX: false, mirrorY: true);
            }
        }
        if (margins.TopExterior != margins.RightExterior)
        {
            var neighbor = FindDiagonalNeighbor(room, rooms, 1);
            if (neighbor is not null)
            {
                var n = RoomRect(neighbor, origin);
                if (margins.TopExterior)
                    MaskPlateCorner(spriteBatch, pixel, new Vector2(n.Left - thin, rect.Y - margins.Top), cut, mirrorX: false, mirrorY: false);
                else
                    MaskPlateCorner(spriteBatch, pixel, new Vector2(rect.Right + margins.Right, n.Bottom + thin), cut, mirrorX: true, mirrorY: true);
            }
        }
        if (margins.BottomExterior != margins.RightExterior)
        {
            var neighbor = FindDiagonalNeighbor(room, rooms, 2);
            if (neighbor is not null)
            {
                var n = RoomRect(neighbor, origin);
                if (margins.BottomExterior)
                    MaskPlateCorner(spriteBatch, pixel, new Vector2(n.Left - thin, rect.Bottom + margins.Bottom), cut, mirrorX: false, mirrorY: true);
                else
                    MaskPlateCorner(spriteBatch, pixel, new Vector2(rect.Right + margins.Right, n.Top - thin), cut, mirrorX: true, mirrorY: false);
            }
        }
        if (margins.BottomExterior != margins.LeftExterior)
        {
            var neighbor = FindDiagonalNeighbor(room, rooms, 3);
            if (neighbor is not null)
            {
                var n = RoomRect(neighbor, origin);
                if (margins.BottomExterior)
                    MaskPlateCorner(spriteBatch, pixel, new Vector2(n.Right + thin, rect.Bottom + margins.Bottom), cut, mirrorX: true, mirrorY: true);
                else
                    MaskPlateCorner(spriteBatch, pixel, new Vector2(rect.X - margins.Left, n.Top - thin), cut, mirrorX: false, mirrorY: false);
            }
        }
    }

    // One diagonal cut corner filled back in with the flat plate colour - `corner` is the plate's
    // true rectangular corner (before the cut), and the triangle removed reaches `cut` pixels back
    // along each edge from it, mirrored to whichever of the four corners this call is for.
    private static void MaskPlateCorner(SpriteBatch spriteBatch, Texture2D pixel, Vector2 corner, float cut, bool mirrorX, bool mirrorY)
    {
        var alongX = new Vector2(mirrorX ? -cut : cut, 0f);
        var alongY = new Vector2(0f, mirrorY ? -cut : cut);
        Primitives.FillTriangle(spriteBatch, pixel, corner, corner + alongX, corner + alongY, Plate);
    }

    // Rectangle with its corners taken off - eight points, filled as a fan from the middle. A
    // corner that has a diagonal neighbour (FindDiagonalNeighbor) is left square instead - one
    // point instead of two - so the two rooms' plates meet flush there rather than each carving an
    // independent, differently-sized notch out of the same shared point.
    private static Vector2[] PlatePolygon(Room room, IReadOnlyList<Room> rooms, RoomMargins margins, Vector2 origin)
    {
        var rect = RoomRect(room, origin);
        var left = rect.X - margins.Left;
        var right = rect.Right + margins.Right;
        var top = rect.Y - margins.Top;
        var bottom = rect.Bottom + margins.Bottom;
        var cut = MathF.Min(CornerCutUnits * ShipRenderer.PixelsPerUnit, MathF.Min(right - left, bottom - top) / 3f);

        var points = new List<Vector2>(8);
        AddCorner(points, FindDiagonalNeighbor(room, rooms, 0) is not null, new Vector2(left, top + cut), new Vector2(left + cut, top), new Vector2(left, top));
        AddCorner(points, FindDiagonalNeighbor(room, rooms, 1) is not null, new Vector2(right - cut, top), new Vector2(right, top + cut), new Vector2(right, top));
        AddCorner(points, FindDiagonalNeighbor(room, rooms, 2) is not null, new Vector2(right, bottom - cut), new Vector2(right - cut, bottom), new Vector2(right, bottom));
        AddCorner(points, FindDiagonalNeighbor(room, rooms, 3) is not null, new Vector2(left + cut, bottom), new Vector2(left, bottom - cut), new Vector2(left, bottom));
        return points.ToArray();
    }

    private static void AddCorner(List<Vector2> points, bool diagonalNeighbor, Vector2 incoming, Vector2 outgoing, Vector2 square)
    {
        if (diagonalNeighbor)
            points.Add(square);
        else
        {
            points.Add(incoming);
            points.Add(outgoing);
        }
    }

    public static Vector2 Forward(float forwardDegrees)
    {
        var radians = forwardDegrees * (MathF.PI / 180f);
        return new Vector2(MathF.Cos(radians), MathF.Sin(radians));
    }

    private static Room ForwardMost(IReadOnlyList<Room> rooms, Vector2 bow)
    {
        var best = rooms[0];
        var bestReach = float.NegativeInfinity;
        foreach (var room in rooms)
        {
            var reach = Vector2.Dot(new Vector2(room.Center.X, room.Center.Y), bow)
                        + MathF.Abs(Vector2.Dot(new Vector2(room.Width, room.Height) / 2f, bow));
            if (reach > bestReach)
            {
                bestReach = reach;
                best = room;
            }
        }
        return best;
    }

    // Whether any system device physically inside this room is currently damaged - the trigger
    // for DrawHullDamage's scorch marks. No systemStates at all (an older/test call site) just
    // means nothing is ever damaged, not a crash.
    private static bool RoomHasDamagedDevice(Room room, IReadOnlyList<ShipSystemDevice> devices, IReadOnlyList<ShipSystemState>? systemStates)
    {
        if (systemStates is null)
            return false;
        foreach (var device in devices)
        {
            if (device.RoomId != room.Id)
                continue;
            foreach (var state in systemStates)
                if (state.DeviceId == device.Id && state.Damaged)
                    return true;
        }
        return false;
    }

    private static Vector2 HullCenter(IReadOnlyList<Room> rooms, Vector2 origin)
    {
        var center = ShipLocalFrame.GetHullCenter(rooms);
        return origin + new Vector2(center.X, center.Y) * ShipRenderer.PixelsPerUnit;
    }

    private static Vector2 RoomCenter(Room room, Vector2 origin) =>
        origin + new Vector2(room.Center.X, room.Center.Y) * ShipRenderer.PixelsPerUnit;

    private static Rectangle RoomRect(Room room, Vector2 origin) => new(
        (int)(origin.X + room.X * ShipRenderer.PixelsPerUnit),
        (int)(origin.Y + room.Y * ShipRenderer.PixelsPerUnit),
        (int)(room.Width * ShipRenderer.PixelsPerUnit),
        (int)(room.Height * ShipRenderer.PixelsPerUnit));
}
