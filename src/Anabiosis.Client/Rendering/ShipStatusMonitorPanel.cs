using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Direct user request ("монитор состояния корабля... в этом приборе написаны все отсеки корабля и
// пишется какие повреждения на корабле, сколько кислорода в каждом отсеке, сколько хп у каждого
// отсека... рисуется в виде настоящего корабля где корабль в центре и на каждый отсек при наведении
// мышкой пишется доп информация") - a full-screen takeover (Game1.cs's own _shipStatusMonitorOpen),
// same "empty scene-batch branch, drawn later as an unmasked HUD-batch overlay" treatment as the
// galactic map. Every room drawn to scale at its real relative position (not RoomHpPanel's own flat
// list of rows - that one's still there, this is the spatial version), tinted by Hp like
// ShipRenderer's own DrawRoomFloor tints by oxygen deficit, plus an oxygen bar per room and a
// hover tooltip for the rest.
public sealed class ShipStatusMonitorPanel
{
    private const int Margin = 40;
    private const int TitleHeight = 40;
    private const int OxygenBarHeight = 6;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public ShipStatusMonitorPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Rectangle area, Point hoverPoint)
    {
        spriteBatch.Draw(_pixel, area, new Color(10, 14, 12));
        var title = "МОНИТОР СОСТОЯНИЯ КОРАБЛЯ";
        var titleSize = _font.MeasureString(title) * 0.6f;
        spriteBatch.DrawString(_font, title, new Vector2(area.Center.X - titleSize.X / 2f, area.Y + 12),
            new Color(140, 220, 160), 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        var rooms = snapshot.Rooms;
        if (rooms.Count == 0)
            return;

        var hpByRoomId = new Dictionary<string, RoomHpState>();
        if (snapshot.RoomHp is { } hpStates)
            foreach (var state in hpStates)
                hpByRoomId[state.RoomId] = state;
        var oxygenByRoomId = snapshot.RoomOxygen.ToDictionary(o => o.RoomId, o => o.Oxygen);

        // Fit the whole hull's own bounding box into the schematic area, centered - "корабль в
        // центре" (direct user request), same "shrink to fit, then center the remainder" shape
        // Game1.Camera.cs's own ship-overview auto-fit already uses for a similar "see it all" view.
        var hullLeft = rooms.Min(r => r.X);
        var hullTop = rooms.Min(r => r.Y);
        var hullWidth = rooms.Max(r => r.Right) - hullLeft;
        var hullHeight = rooms.Max(r => r.Bottom) - hullTop;
        var schematicArea = new Rectangle(area.X + Margin, area.Y + TitleHeight, area.Width - Margin * 2, area.Height - TitleHeight - Margin);
        var scale = hullWidth <= 0 || hullHeight <= 0 ? 1f
            : System.Math.Min(schematicArea.Width / hullWidth, schematicArea.Height / hullHeight) * 0.92f;
        var schematicOffset = new Vector2(
            schematicArea.Center.X - (hullLeft + hullWidth / 2f) * scale,
            schematicArea.Center.Y - (hullTop + hullHeight / 2f) * scale);

        Room? hoveredRoom = null;
        Rectangle hoveredRect = default;
        foreach (var room in rooms)
        {
            var rect = new Rectangle(
                (int)(room.X * scale + schematicOffset.X), (int)(room.Y * scale + schematicOffset.Y),
                System.Math.Max(2, (int)(room.Width * scale)), System.Math.Max(2, (int)(room.Height * scale)));

            var hasHp = hpByRoomId.TryGetValue(room.Id, out var hpState);
            var hpFraction = hasHp && hpState!.MaxHp > 0f ? MathHelper.Clamp(hpState.Hp / hpState.MaxHp, 0f, 1f) : 1f;
            var fillColor = !hasHp ? new Color(60, 64, 68)
                : hpFraction > 0.5f ? new Color(70, 110, 75) : hpFraction > 0.2f ? new Color(150, 120, 50) : new Color(140, 55, 50);
            spriteBatch.Draw(_pixel, rect, fillColor);

            var oxygen = oxygenByRoomId.GetValueOrDefault(room.Id, 100f);
            var oxygenFraction = MathHelper.Clamp(oxygen / 100f, 0f, 1f);
            if (rect.Height > OxygenBarHeight * 2)
            {
                var barRect = new Rectangle(rect.X + 2, rect.Bottom - OxygenBarHeight - 2, rect.Width - 4, OxygenBarHeight);
                spriteBatch.Draw(_pixel, barRect, new Color(15, 18, 20));
                var barFillWidth = (int)(barRect.Width * oxygenFraction);
                if (barFillWidth > 0)
                    spriteBatch.Draw(_pixel, new Rectangle(barRect.X, barRect.Y, barFillWidth, barRect.Height), new Color(90, 160, 220));
            }

            var isHovered = rect.Contains(hoverPoint);
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, isHovered ? Color.White : new Color(20, 24, 22), isHovered ? 2 : 1);
            if (isHovered)
            {
                hoveredRoom = room;
                hoveredRect = rect;
            }

            if (rect.Width > 50 && rect.Height > 20)
            {
                var nameSize = _font.MeasureString(room.Name) * 0.32f;
                if (nameSize.X < rect.Width - 6)
                    spriteBatch.DrawString(_font, room.Name, new Vector2(rect.X + 4, rect.Y + 3), Color.White, 0f, Vector2.Zero, 0.32f, SpriteEffects.None, 0f);
            }
        }

        if (hoveredRoom is { } hovered)
            DrawTooltip(spriteBatch, hovered, hpByRoomId.GetValueOrDefault(hovered.Id), oxygenByRoomId.GetValueOrDefault(hovered.Id, 100f), hoveredRect, area);
    }

    private void DrawTooltip(SpriteBatch spriteBatch, Room room, RoomHpState? hp, float oxygen, Rectangle roomRect, Rectangle clampArea)
    {
        var lines = new[]
        {
            room.Name,
            hp is { } h ? $"Прочность: {h.Hp:0}/{h.MaxHp:0}" : "Прочность: н/д",
            $"Кислород: {oxygen:0}%",
        };
        var width = lines.Max(l => _font.MeasureString(l).X * 0.4f) + 16;
        var height = lines.Length * 16 + 10;
        var x = MathHelper.Clamp(roomRect.Right + 8, clampArea.X, clampArea.Right - width);
        var y = MathHelper.Clamp(roomRect.Top, clampArea.Y, clampArea.Bottom - height);
        var tooltipRect = new Rectangle((int)x, (int)y, (int)width, (int)height);

        spriteBatch.Draw(_pixel, tooltipRect, new Color(20, 24, 26) * 0.96f);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, tooltipRect, new Color(90, 110, 95), 1);
        for (var i = 0; i < lines.Length; i++)
            spriteBatch.DrawString(_font, lines[i], new Vector2(tooltipRect.X + 8, tooltipRect.Y + 5 + i * 16),
                i == 0 ? Color.White : Color.LightGray, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
    }
}
