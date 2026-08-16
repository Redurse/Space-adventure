using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// The hardware bolted to the outside of the shell: engine bells at the tail, radiator fins along
// the flanks, docking collars where the airlocks are, paint down the spine, and the plating detail
// that stops a compartment's roof from reading as one flat panel.
//
// All of it is decoration over the armour drawn in HullSkin.cs - none of it moves, blocks or is
// clickable. The engines' *exhaust* is a separate thing entirely (FieldRenderer draws the plume,
// and only while the ship is actually under way); what lives here is the nozzle that is there
// whether the ship is flying or parked at a berth.
public static partial class HullSkin
{
    private static readonly Color Nozzle = new(38, 44, 54);
    private static readonly Color NozzleMouth = new(22, 26, 33);
    private static readonly Color Livery = new(206, 142, 58);

    // A bell at the tail end of whichever compartment each engine sits in, at the engine's own
    // lateral position. A ship whose engines only exist as a glow while moving looks, at rest, like
    // a building.
    private static void DrawEngineNozzles(SpriteBatch spriteBatch, Texture2D pixel, IReadOnlyList<Room> rooms,
        IReadOnlyList<ShipSystemDevice> devices, Vector2 bow, Vector2 origin)
    {
        var side = new Vector2(-bow.Y, bow.X);

        foreach (var device in devices)
        {
            if (device.System != PowerSystemId.Engine)
                continue;

            var room = FindRoom(rooms, device.RoomId);
            if (room is null)
                continue;

            var half = new Vector2(room.Width, room.Height) / 2f;
            var aft = Vector2.Dot(new Vector2(room.Center.X, room.Center.Y), -bow) + MathF.Abs(Vector2.Dot(half, bow));
            var lateral = Vector2.Dot(new Vector2(device.Position.X, device.Position.Y), side);

            // Housing tapers outward to the mouth, the way a bell does.
            var throatWidth = 0.85f * device.SizeScale;
            var mouthWidth = 1.25f * device.SizeScale;
            var length = 1.15f * device.SizeScale;
            var root = aft + MarginUnits * 0.4f;

            var housing = new[]
            {
                LayoutPoint(bow, side, root, lateral - throatWidth, origin),
                LayoutPoint(bow, side, root, lateral + throatWidth, origin),
                LayoutPoint(bow, side, root + length, lateral + mouthWidth, origin),
                LayoutPoint(bow, side, root + length, lateral - mouthWidth, origin),
            };
            var middle = (housing[0] + housing[2]) / 2f;
            Primitives.FillPolygon(spriteBatch, pixel, middle, housing, Nozzle);
            Primitives.StrokePolygon(spriteBatch, pixel, housing, Edge * 0.5f, 2f);

            // The mouth itself: a dark opening with a lit lip, so it reads as a hole rather than a
            // block. The plume that comes out of it is FieldRenderer's business.
            var mouth = new[]
            {
                LayoutPoint(bow, side, root + length * 0.72f, lateral - mouthWidth * 0.78f, origin),
                LayoutPoint(bow, side, root + length * 0.72f, lateral + mouthWidth * 0.78f, origin),
                LayoutPoint(bow, side, root + length * 0.96f, lateral + mouthWidth * 0.86f, origin),
                LayoutPoint(bow, side, root + length * 0.96f, lateral - mouthWidth * 0.86f, origin),
            };
            Primitives.FillPolygon(spriteBatch, pixel, (mouth[0] + mouth[2]) / 2f, mouth, NozzleMouth);
            Primitives.StrokePolygon(spriteBatch, pixel, mouth, new Color(150, 120, 90) * 0.4f, 2f);

            // Two struts tying the bell back into the hull.
            foreach (var offsetSign in new[] { -1f, 1f })
            {
                var from = LayoutPoint(bow, side, root - 0.2f, lateral + offsetSign * throatWidth * 0.9f, origin);
                var to = LayoutPoint(bow, side, root + length * 0.8f, lateral + offsetSign * mouthWidth * 0.95f, origin);
                var strut = to - from;
                spriteBatch.Draw(pixel, from, null, PlateLit, MathF.Atan2(strut.Y, strut.X), new Vector2(0f, 0.5f),
                    new Vector2(strut.Length(), 4f), SpriteEffects.None, 0f);
            }
        }
    }

    // Heat has to go somewhere, and on every hard-science ship ever drawn it goes out through fins.
    // Hung on the outermost flanks, aft of the middle, where a real one would be: away from the
    // bridge and clear of the docking collars.
    private static void DrawRadiatorFins(SpriteBatch spriteBatch, Texture2D pixel, IReadOnlyList<Room> rooms,
        Vector2 hullCenter, Vector2 bow, Vector2 origin)
    {
        var side = new Vector2(-bow.Y, bow.X);
        var deepest = float.NegativeInfinity;
        Room? portRoom = null;
        Room? starboardRoom = null;
        var portEdge = float.PositiveInfinity;
        var starboardEdge = float.NegativeInfinity;

        foreach (var room in rooms)
        {
            var half = new Vector2(room.Width, room.Height) / 2f;
            var lateral = Vector2.Dot(new Vector2(room.Center.X, room.Center.Y), side);
            var reach = MathF.Abs(Vector2.Dot(half, side));
            if (lateral - reach < portEdge)
            {
                portEdge = lateral - reach;
                portRoom = room;
            }
            if (lateral + reach > starboardEdge)
            {
                starboardEdge = lateral + reach;
                starboardRoom = room;
            }
            deepest = MathF.Max(deepest, Vector2.Dot(new Vector2(room.Center.X, room.Center.Y), -bow) + MathF.Abs(Vector2.Dot(half, bow)));
        }

        DrawFinBank(spriteBatch, pixel, portRoom, bow, side, portEdge, -1f, origin);
        DrawFinBank(spriteBatch, pixel, starboardRoom, bow, side, starboardEdge, 1f, origin);
    }

    private static void DrawFinBank(SpriteBatch spriteBatch, Texture2D pixel, Room? room, Vector2 bow, Vector2 side,
        float edge, float outward, Vector2 origin)
    {
        if (room is null)
            return;

        var half = new Vector2(room.Width, room.Height) / 2f;
        var along = Vector2.Dot(new Vector2(room.Center.X, room.Center.Y), -bow);
        var depth = MathF.Abs(Vector2.Dot(half, bow));

        for (var i = 0; i < 3; i++)
        {
            var at = along - depth * 0.35f + i * depth * 0.42f;
            var root = edge + outward * MarginUnits;
            var tip = edge + outward * (MarginUnits + 0.75f);
            var fin = new[]
            {
                LayoutPoint(bow, side, at - 0.42f, root, origin),
                LayoutPoint(bow, side, at + 0.42f, root, origin),
                LayoutPoint(bow, side, at + 0.28f, tip, origin),
                LayoutPoint(bow, side, at - 0.28f, tip, origin),
            };
            Primitives.FillPolygon(spriteBatch, pixel, (fin[0] + fin[2]) / 2f, fin, new Color(64, 72, 84));
            Primitives.StrokePolygon(spriteBatch, pixel, fin, Edge * 0.35f, 1.5f);
        }
    }

    // A collar around each airlock: the ring a station's connector actually clamps onto. Painted in
    // hazard yellow because that is what every airlock in this game is edged with, so the way out
    // is findable from outside as well as from the corridor.
    private static void DrawDockingCollars(SpriteBatch spriteBatch, Texture2D pixel,
        IReadOnlyList<AirlockOuterDoor> ports, Vector2 hullCenter, Vector2 origin)
    {
        foreach (var port in ports)
        {
            var center = origin + new Vector2(port.Position.X, port.Position.Y) * ShipRenderer.PixelsPerUnit;
            var outward = center - hullCenter;
            if (outward.LengthSquared() < 1f)
                continue;
            outward.Normalize();

            var vertical = port.Height >= port.Width;
            var span = (int)((vertical ? port.Height : port.Width) * ShipRenderer.PixelsPerUnit) + 14;
            var depth = (int)(MarginUnits * ShipRenderer.PixelsPerUnit) + 8;
            var seat = center + outward * (depth / 2f);

            var collar = vertical
                ? new Rectangle((int)seat.X - depth / 2, (int)seat.Y - span / 2, depth, span)
                : new Rectangle((int)seat.X - span / 2, (int)seat.Y - depth / 2, span, depth);

            spriteBatch.Draw(pixel, collar, new Color(74, 82, 96));
            // Clamp lugs at both ends of the ring.
            var lug = 6;
            if (vertical)
            {
                spriteBatch.Draw(pixel, new Rectangle(collar.X - 2, collar.Y, collar.Width + 4, lug), Color.Gold * 0.65f);
                spriteBatch.Draw(pixel, new Rectangle(collar.X - 2, collar.Bottom - lug, collar.Width + 4, lug), Color.Gold * 0.65f);
            }
            else
            {
                spriteBatch.Draw(pixel, new Rectangle(collar.X, collar.Y - 2, lug, collar.Height + 4), Color.Gold * 0.65f);
                spriteBatch.Draw(pixel, new Rectangle(collar.Right - lug, collar.Y - 2, lug, collar.Height + 4), Color.Gold * 0.65f);
            }
        }
    }

    // Paint down the spine, from the bow to the tail: a dark band edged in the ship's own colour.
    // Livery is most of what separates a vessel from a shape - navies and haulers alike paint their
    // hulls, and an unpainted one reads as untextured geometry.
    private static void DrawLivery(SpriteBatch spriteBatch, Texture2D pixel, IReadOnlyList<Room> rooms, Vector2 bow, Vector2 origin)
    {
        var side = new Vector2(-bow.Y, bow.X);
        var nose = ForwardMost(rooms, bow);
        var spine = Vector2.Dot(new Vector2(nose.Center.X, nose.Center.Y), side);

        var front = float.PositiveInfinity;
        var back = float.NegativeInfinity;
        foreach (var room in rooms)
        {
            var half = new Vector2(room.Width, room.Height) / 2f;
            var along = Vector2.Dot(new Vector2(room.Center.X, room.Center.Y), -bow);
            var depth = MathF.Abs(Vector2.Dot(half, bow));
            front = MathF.Min(front, along - depth);
            back = MathF.Max(back, along + depth);
        }
        // Starts at the base of the nose rather than out on the dome: paint that runs over the
        // canopy looks like paint over a windscreen.
        Band(spriteBatch, pixel, bow, side, front + 0.1f, back - 0.2f, spine, 0.42f, origin, Color.Black * 0.2f);
        Band(spriteBatch, pixel, bow, side, front + 0.1f, back - 0.2f, spine - 0.46f, 0.07f, origin, Livery * 0.8f);
        Band(spriteBatch, pixel, bow, side, front + 0.1f, back - 0.2f, spine + 0.46f, 0.07f, origin, Livery * 0.8f);
    }

    // One quad, not a filled polygon: Primitives fills by sweeping overlapping strips from a centre
    // point, which is invisible with an opaque colour and turns a translucent one into a dark smear
    // where the strips pile up. Paint is translucent, so it gets drawn in a single pass.
    private static void Band(SpriteBatch spriteBatch, Texture2D pixel, Vector2 bow, Vector2 side,
        float from, float to, float lateral, float halfWidth, Vector2 origin, Color color)
    {
        var start = LayoutPoint(bow, side, from, lateral, origin);
        var end = LayoutPoint(bow, side, to, lateral, origin);
        var run = end - start;
        if (run.Length() < 0.5f)
            return;

        spriteBatch.Draw(pixel, start, null, color, MathF.Atan2(run.Y, run.X), new Vector2(0f, 0.5f),
            new Vector2(run.Length(), halfWidth * 2f * ShipRenderer.PixelsPerUnit), SpriteEffects.None, 0f);
    }

    // The roof of a compartment seen from outside: seams, a couple of access hatches and a bank of
    // rivets, keyed off the compartment's id so a given deck always looks the same.
    private static void DrawDeckPlating(SpriteBatch spriteBatch, Texture2D pixel, Room room, Vector2 origin)
    {
        var rect = RoomRect(room, origin);
        var random = new Random(AsteroidTexture.Seed(room.Id));
        var tone = 1f + (float)(random.NextDouble() - 0.5) * 0.14f;
        spriteBatch.Draw(pixel, rect, new Color((int)(Deck.R * tone), (int)(Deck.G * tone), (int)(Deck.B * tone)));

        for (var x = rect.X + 26; x < rect.Right; x += 26)
            spriteBatch.Draw(pixel, new Rectangle(x, rect.Y, 1, rect.Height), Seam);
        for (var y = rect.Y + 26; y < rect.Bottom; y += 26)
            spriteBatch.Draw(pixel, new Rectangle(rect.X, y, rect.Width, 1), Seam);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), PlateLit);

        var hatches = 1 + random.Next(3);
        for (var i = 0; i < hatches; i++)
        {
            var w = 26 + random.Next(3) * 26;
            var h = 26 + random.Next(2) * 26;
            if (rect.Width < w + 40 || rect.Height < h + 40)
                continue;
            var hatch = new Rectangle(rect.X + 20 + random.Next(rect.Width - w - 40),
                rect.Y + 20 + random.Next(rect.Height - h - 40), w, h);
            spriteBatch.Draw(pixel, hatch, new Color(40, 47, 57));
            spriteBatch.Draw(pixel, new Rectangle(hatch.X, hatch.Y, hatch.Width, 2), PlateLit * 0.7f);
            spriteBatch.Draw(pixel, new Rectangle(hatch.X, hatch.Bottom - 2, hatch.Width, 2), Color.Black * 0.4f);
            for (var rivet = hatch.X + 5; rivet < hatch.Right - 3; rivet += 9)
            {
                spriteBatch.Draw(pixel, new Rectangle(rivet, hatch.Y + 4, 2, 2), Color.Black * 0.45f);
                spriteBatch.Draw(pixel, new Rectangle(rivet, hatch.Bottom - 6, 2, 2), Color.Black * 0.45f);
            }
        }
    }

    private static Room? FindRoom(IReadOnlyList<Room> rooms, string id)
    {
        foreach (var room in rooms)
            if (room.Id == id)
                return room;
        return null;
    }
}
