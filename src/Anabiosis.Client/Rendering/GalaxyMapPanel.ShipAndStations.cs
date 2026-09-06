using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The ship's own marker (heading arrow / real hull schematic, velocity vector) and a docked
// station's real room-by-room schematic once zoomed in far enough - split out of GalaxyMapPanel.cs
// since both are "draw a known physical hull at its map position", independent of the scanner
// porthole geometry (GalaxyMapPanel.Scanner.cs) or field content like asteroids/contacts
// (GalaxyMapPanel.FieldContent.cs).
public sealed partial class GalaxyMapPanel
{
    // Own-ship marker (M47 follow-up - "видно схематично корабль и куда смотрит нос... в виде
    // стрелочки в навигаторе"): a compact heading arrow at ordinary zoom, replaced by the real
    // room-by-room hull (the same rotated schematic HelmPanel's own radar used to draw before it
    // moved onto this map) once zoomed in far enough to actually read it - the arrow alone reads
    // as a blob at that scale, but the real hull is legible. Either way, a separate vector for the
    // ship's actual velocity is drawn from its centre - heading and course are different things
    // once the ship is drifting off its own nose (RCS strafing, momentum through a turn).
    // Lowered from 1.4 (M48 follow-up - "чтобы при приближении раньше проявлялись реальные модельки
    // кораблей и станций") - the real hull/room schematic now takes over well before the old
    // near-max-zoom threshold, instead of staying a blob icon for most of the zoom range.
    private const float ShipSchematicZoomThreshold = 0.6f;
    private const float ShipHeadingArrowSize = 12f;
    private const float VelocityVectorScale = 6f; // pixels per unit/s of speed, before clamping
    private const float VelocityVectorMinLength = 12f;
    private const float VelocityVectorMaxLength = 80f;

    private void DrawShipMarker(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 shipCenter, float zoom)
    {
        var noseDegrees = snapshot.ShipField.RotationDegrees + snapshot.ShipForwardDegrees;

        // While docked, the ship's own map marker sits at the EXACT same point as the station it's
        // docked at by design (World.cs's ShipMapPosition doc comment - there's no meaningful
        // map-space offset for the real physical berth to plot). The ship still gets its own real
        // hull schematic here same as always - the points loop above is what nudges the DOCKED
        // station's own drawn position aside instead (M48 follow-up - "теперь корабль и не
        // рисуется" - hiding the ship's own hull was the wrong fix; moving the other marker keeps
        // both visible without overlapping).
        if (zoom >= ShipSchematicZoomThreshold && snapshot.Rooms.Count > 0)
            DrawShipHullSchematic(spriteBatch, snapshot, shipCenter, zoom, noseDegrees);
        else
            DrawShipHeadingArrow(spriteBatch, shipCenter, noseDegrees);

        DrawShipVelocityVector(spriteBatch, snapshot.ShipField, shipCenter);
    }

    // The "navigator" glyph - a small triangle pointing along the hull's real heading (RotationDegrees
    // + the hull's own ForwardDegrees offset, same convention World.ShipField.cs itself steers by),
    // constant screen size regardless of zoom like every other marker on this map (PointMarkerSize's
    // own doc comment).
    private void DrawShipHeadingArrow(SpriteBatch spriteBatch, Vector2 shipCenter, float noseDegrees)
    {
        var radians = noseDegrees * (MathF.PI / 180f);
        var forward = new Vector2(MathF.Cos(radians), MathF.Sin(radians));
        var side = new Vector2(-forward.Y, forward.X);

        var tip = shipCenter + forward * ShipHeadingArrowSize;
        var baseCenter = shipCenter - forward * ShipHeadingArrowSize * 0.6f;
        var points = new[]
        {
            tip,
            baseCenter + side * ShipHeadingArrowSize * 0.6f,
            baseCenter - side * ShipHeadingArrowSize * 0.6f,
        };
        Primitives.FillPolygon(spriteBatch, _pixel, shipCenter, points, Color.White * 0.9f);
        Primitives.StrokePolygon(spriteBatch, _pixel, points, Color.Black * 0.6f, 1.5f);
    }

    // "При приближении выдавали свою настоящую отсековую структуру станции" (M48 follow-up) - every
    // station now generates its own procedural shape (M49, Station.Procedural.cs), seeded purely
    // from its own GalaxyPoint id, so the client can reproduce the exact same layout the server has
    // for that one point with no round-trip - it just has to ask for that point's own id instead of
    // a shared kind template. Cached per point id since the layout never changes once generated.
    private static readonly Dictionary<string, Station> _stationSchematicCache = new();

    // Half the station schematic's own bounding width in world units (M48 follow-up - "чтобы
    // выглядело как на 2 скриншоте", not the earlier fixed-gap nudge) - lets the docked offset below
    // place the station schematic flush against the ship's own hull instead of floating an arbitrary
    // distance away from it.
    private static float GetStationHalfWidth(GalaxyPoint point)
    {
        var station = GetOrBuildSchematic(point);
        if (station.Rooms.Count == 0)
            return 0f;
        return (station.Rooms.Max(r => r.Right) - station.Rooms.Min(r => r.Left)) / 2f;
    }

    private static Station GetOrBuildSchematic(GalaxyPoint point)
    {
        if (!_stationSchematicCache.TryGetValue(point.Id, out var station))
            _stationSchematicCache[point.Id] = station = Station.CreateProcedural(point.Id, point.StationKind, Vec2.Zero);
        return station;
    }

    private void DrawStationSchematic(SpriteBatch spriteBatch, GalaxyPoint point, Vector2 markerScreen, float zoom)
    {
        var station = GetOrBuildSchematic(point);
        if (station.Rooms.Count == 0)
            return;

        var minX = station.Rooms.Min(r => r.Left);
        var maxX = station.Rooms.Max(r => r.Right);
        var minY = station.Rooms.Min(r => r.Top);
        var maxY = station.Rooms.Max(r => r.Bottom);
        var boundsCenter = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
        var scale = PixelsPerUnit * zoom;

        foreach (var room in station.Rooms)
        {
            var local = new Vector2((float)room.Center.X, (float)room.Center.Y) - boundsCenter;
            var size = new Vector2(room.Width, room.Height) * scale;
            var screenCenter = markerScreen + local * scale;
            spriteBatch.Draw(_pixel, screenCenter, null, Color.SteelBlue * 0.85f, 0f,
                new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
            DrawRectOutline(spriteBatch, new Rectangle((int)(screenCenter.X - size.X / 2f), (int)(screenCenter.Y - size.Y / 2f), (int)size.X, (int)size.Y),
                new Color(120, 150, 170), 1);
        }
    }

    // "При сильном приближении было видно корабль как в прошлых версиях" - the real hull, room by
    // room, rotated to its actual heading and scaled in true world units (PixelsPerUnit*zoom), so
    // it grows into a readable ship the same way any other object on this map would at this zoom
    // rather than staying a fixed-size icon.
    private void DrawShipHullSchematic(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 shipCenter, float zoom, float noseDegrees)
    {
        var radians = snapshot.ShipField.RotationDegrees * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
        var scale = PixelsPerUnit * zoom;

        foreach (var room in snapshot.Rooms)
        {
            var local = room.Center - hullCenter;
            var rotated = new Vector2((float)(local.X * cos - local.Y * sin), (float)(local.X * sin + local.Y * cos));
            var size = new Vector2(room.Width, room.Height) * scale;
            var breached = snapshot.WallBlockStates.Any(s =>
                s.Breached && snapshot.WallBlocks.FirstOrDefault(b => b.Id == s.Id)?.RoomId == room.Id);

            spriteBatch.Draw(_pixel, shipCenter + rotated * scale, null,
                (breached ? Color.IndianRed : Color.LightSteelBlue) * 0.9f, radians,
                new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
        }

        var noseRadians = noseDegrees * (MathF.PI / 180f);
        var nose = shipCenter + new Vector2(MathF.Cos(noseRadians), MathF.Sin(noseRadians)) * scale * 0.6f;
        spriteBatch.Draw(_pixel, new Rectangle((int)nose.X - 3, (int)nose.Y - 3, 6, 6), Color.White);
    }

    // The ship's own course, not its heading - the two only agree while flying straight ahead in
    // Arc mode. Length is a fixed screen distance driven by speed alone (not zoom), same reasoning
    // as the heading arrow's constant size: it needs to read at a glance at any zoom level, not be
    // measured against the map's own scale. Bodies/stations are all fixed now (M59), so the ship's
    // own absolute velocity IS its velocity relative to everything drawn around it - no host to
    // subtract.
    private void DrawShipVelocityVector(SpriteBatch spriteBatch, ShipFieldState shipField, Vector2 shipCenter)
    {
        var velocity = new Vector2(shipField.VelocityX, shipField.VelocityY);
        var speed = velocity.Length();
        if (speed < 0.05f)
            return;

        var direction = velocity / speed;
        var length = MathHelper.Clamp(speed * VelocityVectorScale, VelocityVectorMinLength, VelocityVectorMaxLength);
        var end = shipCenter + direction * length;
        var rotation = MathF.Atan2(direction.Y, direction.X);

        spriteBatch.Draw(_pixel, shipCenter, null, Color.Gold * 0.85f, rotation, new Vector2(0f, 0.5f),
            new Vector2(length, 2f), SpriteEffects.None, 0f);
        // Small arrowhead, same construction as the heading arrow's own triangle.
        var side = new Vector2(-direction.Y, direction.X);
        var headPoints = new[] { end, end - direction * 8f + side * 5f, end - direction * 8f - side * 5f };
        Primitives.FillPolygon(spriteBatch, _pixel, end, headPoints, Color.Gold * 0.85f);
    }
}
