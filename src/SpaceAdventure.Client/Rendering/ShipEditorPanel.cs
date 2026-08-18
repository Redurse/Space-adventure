using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// The top bar's "Управление" button (previously an unlit placeholder) - a Barotrauma submarine-
// editor-styled inspector: every wiring component down the left, its own properties on the right,
// and (reusing ConnectionsPanel) its full pin list below that. This game's ships are fixed,
// compile-time layouts (Ship.cs and its per-hull partials) with no save/load format to edit into,
// so this is read-only - the "resembling" schematic view the reference screenshot's palette +
// inspector layout maps onto, not a true drag-and-place level editor.
public sealed class ShipEditorPanel
{
    private const int PanelWidth = 1080;
    private const int PanelHeight = 560;
    private const int HeaderHeight = 40;
    private const int ListWidth = 260;
    private const int RowHeight = 22;
    private const int BorderThickness = 2;
    private static readonly Color PanelBackground = new(20, 26, 22);
    private static readonly Color PanelBorder = new(90, 110, 95);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public ShipEditorPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetRowRect(int index, Vector2 panelOrigin) =>
        new((int)panelOrigin.X + 8, (int)panelOrigin.Y + HeaderHeight + 8 + index * RowHeight, ListWidth - 16, RowHeight - 2);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, string? selectedComponentId,
        ConnectionsPanel connectionsPanel, Vector2 panelOrigin)
    {
        var panelRect = new Rectangle((int)panelOrigin.X, (int)panelOrigin.Y, PanelWidth, PanelHeight);
        spriteBatch.Draw(_pixel, panelRect, PanelBackground * 0.95f);
        DrawRectOutline(spriteBatch, panelRect, PanelBorder, BorderThickness);

        var headerRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, HeaderHeight);
        spriteBatch.Draw(_pixel, headerRect, new Color(30, 38, 33));
        spriteBatch.Draw(_pixel, new Rectangle(headerRect.X, headerRect.Bottom - BorderThickness, headerRect.Width, BorderThickness), PanelBorder);
        spriteBatch.DrawString(_font, "Редактор корабля", panelOrigin + new Vector2(16, 10), Color.White, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);

        spriteBatch.Draw(_pixel, new Rectangle(panelRect.X + ListWidth, panelRect.Y + HeaderHeight, BorderThickness, panelRect.Height - HeaderHeight), PanelBorder);

        DrawComponentList(spriteBatch, snapshot, selectedComponentId, panelOrigin);

        var rightX = panelRect.X + ListWidth + 20;
        var rightWidth = panelRect.Width - ListWidth - 30;
        if (selectedComponentId is null || snapshot.Components.All(c => c.Id != selectedComponentId))
        {
            spriteBatch.DrawString(_font, "Выберите компонент слева.", new Vector2(rightX, panelRect.Y + HeaderHeight + 16),
                Color.Gray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            return;
        }

        var inspectorBottom = DrawInspector(spriteBatch, snapshot, selectedComponentId, new Vector2(rightX, panelRect.Y + HeaderHeight + 16));
        var wiringTop = (int)inspectorBottom + 14;
        var wiringBounds = new Rectangle(rightX, wiringTop, rightWidth, panelRect.Bottom - wiringTop - 10);
        connectionsPanel.Draw(spriteBatch, snapshot, selectedComponentId, wiringBounds);
    }

    private void DrawComponentList(SpriteBatch spriteBatch, WorldSnapshot snapshot, string? selectedComponentId, Vector2 panelOrigin)
    {
        for (var i = 0; i < snapshot.Components.Count; i++)
        {
            var component = snapshot.Components[i];
            var rect = GetRowRect(i, panelOrigin);
            var selected = component.Id == selectedComponentId;
            if (selected)
                spriteBatch.Draw(_pixel, rect, new Color(70, 100, 85) * 0.6f);
            spriteBatch.DrawString(_font, ComponentRenderer.ComponentLabel(snapshot, component.Id),
                new Vector2(rect.X + 2, rect.Y + 2), selected ? Color.White : Color.LightGray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }
    }

    private float DrawInspector(SpriteBatch spriteBatch, WorldSnapshot snapshot, string componentId, Vector2 origin)
    {
        var component = snapshot.Components.First(c => c.Id == componentId);
        spriteBatch.DrawString(_font, ComponentRenderer.ComponentLabel(snapshot, componentId), origin, Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        var lines = new List<string>
        {
            $"Id: {component.Id}",
            $"Тип: {component.Kind}",
            $"Отсек: {component.RoomId}",
            $"Позиция: {component.X:0.0}, {component.Y:0.0}",
        };
        if (component.TargetId is not null)
            lines.Add($"Цель: {component.TargetId}");
        if (component.Kind == ComponentKind.Timer)
            lines.Add($"Таймер: {component.TimerSeconds:0.0} с");
        var signal = snapshot.ComponentStates.FirstOrDefault(s => s.ComponentId == componentId);
        if (signal is not null)
            lines.Add($"Сигнал: {(signal.SignalValue ? "включён" : "выключен")}");

        var y = origin.Y + 30;
        foreach (var line in lines)
        {
            spriteBatch.DrawString(_font, line, new Vector2(origin.X, y), Color.LightGray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            y += 20;
        }
        return y;
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
