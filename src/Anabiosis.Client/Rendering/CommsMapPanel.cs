using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The comms console's map (direct user request: "теперь это будет карта, изначальный центр которой будет корабль, но не обязательно он, его можно перемещать... мы
// тянем курсор и за ней идут волны. при наведении на станции мы тыкаем ПКМ и устанавливаем связь"). The system around the ship, centred on it at first and free to be
// moved; rings out from the ship with the hailing range marked; the stations in the system, each with its I.F.F. and distance as in Cosmoteer's comms box; and radio
// waves that run out from the ship toward the cursor and, with a line open, along the line to the station on it.
public sealed class CommsMapPanel
{
    public const float DefaultZoom = 0.28f;   // pixels per field unit: the stations around the ship read apart, the hailing range reaches the screen's sides
    public const float MinZoom = 0.03f;
    public const float MaxZoom = 1.2f;
    private const float PickRadius = 18f;
    private const float RingStep = 300f;      // field units between the rings

    private static readonly Color Cyan = new(110, 210, 255);
    private static readonly Color Friendly = new(110, 220, 130);
    private static readonly Color Hostile = new(236, 96, 88);
    private static readonly Color Neutral = new(210, 214, 222);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public CommsMapPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle RecenterButtonRect(Rectangle area) => new(area.X + 14, area.Bottom - 42, 190, 28);

    // Where a field position is on the screen: the ship sits in the middle of the area until the map is moved.
    private static Vector2 ToScreen(Rectangle area, WorldSnapshot snapshot, Vector2 pan, float zoom, Vec2 fieldPoint)
    {
        var ship = snapshot.Voyage.ShipMapPosition;
        var middle = new Vector2(area.Center.X, area.Center.Y) + pan;
        return middle + new Vector2((float)(fieldPoint.X - ship.X), (float)(fieldPoint.Y - ship.Y)) * zoom;
    }

    // The station under the cursor, if the cursor is close enough to one.
    public string? StationAt(Rectangle area, WorldSnapshot snapshot, Vector2 pan, float zoom, Point mouse)
    {
        string? best = null;
        var bestDistance = PickRadius;
        foreach (var point in snapshot.GalaxyPoints.Where(p => p.Kind == GalaxyPointKind.Station))
        {
            var distance = (ToScreen(area, snapshot, pan, zoom, point.Position) - new Vector2(mouse.X, mouse.Y)).Length();
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = point.Id;
            }
        }
        return best;
    }

    private static float DistanceUnits(WorldSnapshot snapshot, GalaxyPoint point) =>
        (float)(snapshot.Voyage.ShipMapPosition - point.Position).Length();

    private static (string Label, Color Color) Iff(WorldSnapshot snapshot, GalaxyPoint point)
    {
        var standing = snapshot.FactionStandings.FirstOrDefault(f => f.Faction == point.Faction)?.Standing ?? 0;
        if (standing <= FactionDefinitions.HostileThreshold)
            return ("ВРАЖДЕБНАЯ", Hostile);
        if (standing >= FactionDefinitions.FriendlyThreshold)
            return ("ДРУЖЕСТВЕННАЯ", Friendly);
        return ("НЕЙТРАЛЬНАЯ", Neutral);
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Rectangle area, Vector2 pan, float zoom, Point mouse, float seconds, bool overWindow)
    {
        var comms = snapshot.Comms;
        var range = comms?.Range ?? World_DefaultRange;

        spriteBatch.Draw(_pixel, area, new Color(5, 9, 15));
        var shipScreen = ToScreen(area, snapshot, pan, zoom, snapshot.Voyage.ShipMapPosition);

        // Rings out from the ship, the hailing range in a stronger line.
        for (var r = RingStep; r < range * 1.6f; r += RingStep)
            HudIcons.DrawRingArc(spriteBatch, _pixel, shipScreen, r * zoom, 0f, 360f, new Color(40, 62, 82) * 0.7f, 72, 1f);
        HudIcons.DrawRingArc(spriteBatch, _pixel, shipScreen, range * zoom, 0f, 360f, Cyan * 0.55f, 120, 2f);
        Text(spriteBatch, "РАДИУС СВЯЗИ", shipScreen + new Vector2(range * zoom * 0.707f + 6f, -range * zoom * 0.707f - 4f), Cyan * 0.7f, 0.4f);

        var hoveredId = overWindow ? null : StationAt(area, snapshot, pan, zoom, mouse);
        var linkedId = comms?.LinkedPointId;

        // The waves: to the cursor, and along the open line.
        if (!overWindow && area.Contains(mouse))
            DrawWaves(spriteBatch, shipScreen, new Vector2(mouse.X, mouse.Y), seconds, Cyan, 0.8f);
        if (linkedId is not null && snapshot.GalaxyPoints.FirstOrDefault(p => p.Id == linkedId) is { } linkedPoint)
            DrawWaves(spriteBatch, shipScreen, ToScreen(area, snapshot, pan, zoom, linkedPoint.Position), seconds * 1.4f, Friendly, 1.0f);

        // Stations close together would write their names over each other: each name drops below the ones already placed.
        var labelRects = new System.Collections.Generic.List<Rectangle>();
        foreach (var point in snapshot.GalaxyPoints.OrderBy(p => p.Y))
        {
            var at = ToScreen(area, snapshot, pan, zoom, point.Position);
            if (!area.Contains(new Point((int)at.X, (int)at.Y)))
                continue;
            if (point.Kind == GalaxyPointKind.Station)
            {
                var size = _font.MeasureString(point.Name) * 0.46f;
                var rect = new Rectangle((int)at.X + 12, (int)at.Y - 7, (int)size.X + 4, 26);
                while (labelRects.Any(r => r.Intersects(rect)))
                    rect.Y += 26;
                labelRects.Add(rect);
                DrawStation(spriteBatch, snapshot, point, at, point.Id == hoveredId, point.Id == linkedId, range, rect.Y - ((int)at.Y - 7));
            }
            else
            {
                var danger = point.Kind == GalaxyPointKind.HostileSector;
                var color = danger ? Hostile * 0.8f : Neutral * 0.5f;
                spriteBatch.Draw(_pixel, new Rectangle((int)at.X - 3, (int)at.Y - 3, 6, 6), color);
                Text(spriteBatch, point.Name, at + new Vector2(8f, -5f), color, 0.38f);
            }
        }

        // The ship: a bright diamond with a ring.
        spriteBatch.Draw(_pixel, shipScreen, null, Cyan, MathF.PI / 4f, new Vector2(0.5f, 0.5f), new Vector2(11f, 11f), SpriteEffects.None, 0f);
        HudIcons.DrawRingArc(spriteBatch, _pixel, shipScreen, 13f, 0f, 360f, Cyan * 0.8f, 24, 1.5f);
        Text(spriteBatch, "КОРАБЛЬ", shipScreen + new Vector2(12f, 8f), Cyan, 0.42f);

        // Title and what the hand can do.
        Text(spriteBatch, "КОНСОЛЬ СВЯЗИ", new Vector2(area.X + 16f, area.Y + 12f), new Color(220, 170, 90), 0.62f);
        Text(spriteBatch, "ПКМ по станции - вызвать   ПКМ + перетаскивание - двигать карту   колесо - масштаб   Esc - закрыть",
            new Vector2(area.X + 16f, area.Y + 36f), Color.White * 0.55f, 0.4f);
        if (comms?.Notice is { } notice)
            Centered(spriteBatch, notice, area.Center.X, area.Y + 62f, Hostile, 0.6f);

        var recenter = RecenterButtonRect(area);
        spriteBatch.Draw(_pixel, recenter, recenter.Contains(mouse) ? new Color(232, 230, 222) : new Color(210, 208, 200));
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, recenter, new Color(40, 40, 40), 1);
        Centered(spriteBatch, "КОРАБЛЬ В ЦЕНТР", recenter.Center.X, recenter.Y + 7f, Color.Black, 0.42f);

        if (hoveredId is not null && snapshot.GalaxyPoints.FirstOrDefault(p => p.Id == hoveredId) is { } hovered)
            DrawTooltip(spriteBatch, snapshot, hovered, mouse, range, area);
    }

    // Default range if no snapshot says otherwise (matches World.CommsRangeUnits).
    private const float World_DefaultRange = 1200f;

    private void DrawStation(SpriteBatch spriteBatch, WorldSnapshot snapshot, GalaxyPoint point, Vector2 at, bool hovered, bool linked, float range, float labelDrop)
    {
        var (_, color) = Iff(snapshot, point);
        var inRange = DistanceUnits(snapshot, point) <= range;
        var tint = inRange ? color : color * 0.5f;
        if (hovered || linked)
            HudIcons.DrawRingArc(spriteBatch, _pixel, at, 17f, 0f, 360f, (linked ? Friendly : Color.White) * 0.9f, 28, 2f);
        spriteBatch.Draw(_pixel, new Rectangle((int)at.X - 6, (int)at.Y - 6, 12, 12), tint);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, new Rectangle((int)at.X - 8, (int)at.Y - 8, 16, 16), tint, 1);
        Text(spriteBatch, point.Name, at + new Vector2(12f, -7f + labelDrop), tint, 0.46f);
        Text(spriteBatch, inRange ? "на связи" : "вне радиуса", at + new Vector2(12f, 6f + labelDrop), tint * 0.8f, 0.36f);
        if (labelDrop > 0f)
            HudIcons.DrawLine(spriteBatch, _pixel, at + new Vector2(8f, 0f), at + new Vector2(11f, labelDrop), tint * 0.5f, 1f);
    }

    private void DrawTooltip(SpriteBatch spriteBatch, WorldSnapshot snapshot, GalaxyPoint point, Point mouse, float range, Rectangle area)
    {
        var (iff, iffColor) = Iff(snapshot, point);
        var distance = DistanceUnits(snapshot, point);
        var inRange = distance <= range;
        var lines = new (string Text, Color Color)[]
        {
            (point.Name, Color.White),
            ($"Принадлежность: {iff}", iffColor),
            ($"Расстояние: {distance:0} ед.", Neutral),
            (inRange ? "ПКМ - вызвать" : "Вне радиуса связи", inRange ? Friendly : Hostile),
        };
        var width = lines.Max(l => (int)(_font.MeasureString(l.Text).X * 0.46f)) + 20;
        var box = new Rectangle(Math.Min(mouse.X + 16, area.Right - width - 8), Math.Min(mouse.Y + 12, area.Bottom - lines.Length * 18 - 16), width, lines.Length * 18 + 12);
        spriteBatch.Draw(_pixel, box, new Color(12, 16, 24) * 0.95f);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, box, new Color(70, 110, 150), 1);
        for (var i = 0; i < lines.Length; i++)
            Text(spriteBatch, lines[i].Text, new Vector2(box.X + 10, box.Y + 6 + i * 18), lines[i].Color, 0.46f);
    }

    // Wave fronts running out from the ship toward a point: arcs about the ship, a little more than the bearing's width, moving out and fading with distance.
    private void DrawWaves(SpriteBatch spriteBatch, Vector2 from, Vector2 to, float seconds, Color color, float strength)
    {
        var along = to - from;
        var reach = along.Length();
        if (reach < 12f)
            return;
        var bearing = MathF.Atan2(along.Y, along.X) * 180f / MathF.PI;
        const float spacing = 34f;
        var phase = (seconds * 0.9f) % 1f;
        for (var k = 0; ; k++)
        {
            var radius = (k + phase) * spacing;
            if (radius > reach)
                break;
            var fade = 1f - radius / reach * 0.75f;
            // The arc widens a little as it travels, like a front spreading, and is brightest near the ship.
            var half = 14f + 10f * (radius / reach);
            HudIcons.DrawRingArc(spriteBatch, _pixel, from, radius, bearing - half, bearing + half, color * (0.8f * fade * strength), 14, 2.2f);
        }
        HudIcons.DrawLine(spriteBatch, _pixel, from, to, color * (0.12f * strength), 1f);
    }

    private void Text(SpriteBatch spriteBatch, string text, Vector2 at, Color color, float scale) =>
        spriteBatch.DrawString(_font, text, at, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

    private void Centered(SpriteBatch spriteBatch, string text, float centerX, float y, Color color, float scale)
    {
        var size = _font.MeasureString(text) * scale;
        spriteBatch.DrawString(_font, text, new Vector2(centerX - size.X / 2f, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
