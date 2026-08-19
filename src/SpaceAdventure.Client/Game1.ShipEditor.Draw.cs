using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client;

public partial class Game1
{
    private void DrawShipEditorScreen()
    {
        _spriteBatch.DrawString(_font, "Редактор корабля", new Vector2(20, 36), Color.White, 0f, Vector2.Zero, 1.1f, SpriteEffects.None, 0f);

        DrawEditorCanvas();
        DrawEditorSidebar();
        DrawEditorBottomBar();
    }

    private void DrawEditorCanvas()
    {
        _spriteBatch.Draw(_pixel, ShipEditorCanvas, new Color(18, 20, 28));

        for (var x = 0; x <= ShipEditorGridCols; x++)
            HudIcons.DrawLine(_spriteBatch, _pixel,
                new Vector2(ShipEditorCanvas.X + x * ShipEditorCellSize, ShipEditorCanvas.Top),
                new Vector2(ShipEditorCanvas.X + x * ShipEditorCellSize, ShipEditorCanvas.Bottom),
                new Color(50, 54, 64), 1f);
        for (var y = 0; y <= ShipEditorGridRows; y++)
            HudIcons.DrawLine(_spriteBatch, _pixel,
                new Vector2(ShipEditorCanvas.Left, ShipEditorCanvas.Y + y * ShipEditorCellSize),
                new Vector2(ShipEditorCanvas.Right, ShipEditorCanvas.Y + y * ShipEditorCellSize),
                new Color(50, 54, 64), 1f);

        foreach (var room in _editorRooms)
            DrawEditorRoom(room);

        if (_editorRoomDragStart is { } start)
            DrawEditorRoomDragPreview(start);

        foreach (var candidate in GetEditorDoorCandidates())
            DrawEditorDoorMarker(candidate.ScreenPos, candidate.HasDoor, isDoor: true);
        foreach (var candidate in GetEditorAirlockCandidates())
            DrawEditorDoorMarker(candidate.ScreenPos, candidate.HasAirlock, isDoor: false);

        foreach (var device in _editorDevices)
            DrawEditorDevice(device);
    }

    private void DrawEditorRoom(CustomRoomDef room)
    {
        var rect = new Rectangle(
            ShipEditorCanvas.X + room.X * ShipEditorCellSize, ShipEditorCanvas.Y + room.Y * ShipEditorCellSize,
            room.Width * ShipEditorCellSize, room.Height * ShipEditorCellSize);
        _spriteBatch.Draw(_pixel, rect, new Color(46, 52, 66));
        DrawRectOutline(rect, new Color(120, 130, 150), 2f);

        var labelSize = _font.MeasureString(room.Name) * 0.55f;
        _spriteBatch.DrawString(_font, room.Name,
            new Vector2(rect.Center.X - labelSize.X / 2f, rect.Y + 4), Color.LightGray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private void DrawEditorRoomDragPreview((int X, int Y) start)
    {
        var cell = GridCellAt(_designMouse) ?? start;
        var x0 = System.Math.Min(start.X, cell.X);
        var y0 = System.Math.Min(start.Y, cell.Y);
        var w = System.Math.Abs(cell.X - start.X) + 1;
        var h = System.Math.Abs(cell.Y - start.Y) + 1;
        var rect = new Rectangle(
            ShipEditorCanvas.X + x0 * ShipEditorCellSize, ShipEditorCanvas.Y + y0 * ShipEditorCellSize,
            w * ShipEditorCellSize, h * ShipEditorCellSize);
        var overlaps = _editorRooms.Any(r => RoomsOverlap(r, x0, y0, w, h));
        _spriteBatch.Draw(_pixel, rect, (overlaps ? new Color(200, 60, 60) : new Color(90, 160, 110)) * 0.35f);
        DrawRectOutline(rect, overlaps ? Color.OrangeRed : Color.LightGreen, 2f);
    }

    private void DrawEditorDoorMarker(Vector2 screenPos, bool active, bool isDoor)
    {
        var color = active
            ? (isDoor ? new Color(120, 200, 255) : new Color(255, 200, 90))
            : new Color(90, 90, 100);
        HudIcons.FillCircle(_spriteBatch, _pixel, screenPos, active ? 6f : 4f, color);
    }

    private void DrawEditorDevice(CustomDeviceDef device)
    {
        var pos = WorldToEditorScreen(device.X, device.Y);
        var size = 18;
        var rect = new Rectangle((int)pos.X - size / 2, (int)pos.Y - size / 2, size, size);
        _spriteBatch.Draw(_pixel, rect, CustomDeviceCatalog.Tint(device.Kind));
        DrawRectOutline(rect, Color.Black, 1f);
        var glyph = CustomDeviceCatalog.ShortGlyph(device.Kind);
        var glyphSize = _font.MeasureString(glyph) * 0.5f;
        _spriteBatch.DrawString(_font, glyph, new Vector2(pos.X - glyphSize.X / 2f, pos.Y - glyphSize.Y / 2f),
            Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(Rectangle rect, Color color, float thickness)
    {
        HudIcons.DrawLine(_spriteBatch, _pixel, new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top), color, thickness);
        HudIcons.DrawLine(_spriteBatch, _pixel, new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Right, rect.Bottom), color, thickness);
        HudIcons.DrawLine(_spriteBatch, _pixel, new Vector2(rect.Left, rect.Top), new Vector2(rect.Left, rect.Bottom), color, thickness);
        HudIcons.DrawLine(_spriteBatch, _pixel, new Vector2(rect.Right, rect.Top), new Vector2(rect.Right, rect.Bottom), color, thickness);
    }

    private static readonly string[] EditorToolLabels = { "Отсеки", "Двери/люки", "Устройства" };

    private void DrawEditorSidebar()
    {
        for (var i = 0; i < EditorToolLabels.Length; i++)
        {
            var rect = GetEditorToolButtonRect(i);
            var selected = (int)_editorTool == i;
            _spriteBatch.Draw(_pixel, rect, selected ? new Color(120, 92, 30) : Color.DimGray * 0.6f);
            _spriteBatch.DrawString(_font, EditorToolLabels[i], new Vector2(rect.X + 6, rect.Y + 5),
                selected ? Color.White : Color.LightGray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        }

        if (_editorTool == EditorTool.Room)
        {
            _spriteBatch.DrawString(_font, "Тащите по сетке, чтобы построить отсек.\nПКМ по отсеку - удалить.",
                new Vector2(EditorSidebarX, 168), Color.Gray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            return;
        }

        if (_editorTool == EditorTool.DoorAirlock)
        {
            _spriteBatch.DrawString(_font,
                "Клик у общей стены - дверь между отсеками.\nКлик у внешней стены - шлюзовой люк.\nПовторный клик убирает.",
                new Vector2(EditorSidebarX, 168), Color.Gray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            return;
        }

        _spriteBatch.DrawString(_font, "Палитра (клик - поставить, ПКМ - убрать):",
            new Vector2(EditorSidebarX, 178), Color.Gray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        for (var i = 0; i < CustomDeviceCatalog.All.Length; i++)
        {
            var kind = CustomDeviceCatalog.All[i];
            var rect = GetEditorPaletteRect(i);
            var selected = kind == _editorDeviceKind;
            _spriteBatch.Draw(_pixel, rect, selected ? CustomDeviceCatalog.Tint(kind) * 0.5f : Color.DimGray * 0.4f);
            DrawRectOutline(rect, selected ? Color.White : Color.DimGray, 1f);
            _spriteBatch.DrawString(_font, CustomDeviceCatalog.Name(kind), new Vector2(rect.X + 4, rect.Y + 3),
                Color.White, 0f, Vector2.Zero, 0.48f, SpriteEffects.None, 0f);
        }

        if (_editorDeviceKind is CustomDeviceKind.TurretBallistic or CustomDeviceKind.TurretLaser)
        {
            _spriteBatch.DrawString(_font, "Борт установки:", new Vector2(EditorSidebarX, 380), Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            var labels = new[] { "Корма", "Нос", "Левый", "Правый" };
            for (var i = 0; i < EditorMountSides.Length; i++)
            {
                var rect = GetEditorMountSideRect(i);
                var selected = EditorMountSides[i] == _editorTurretMountSide;
                _spriteBatch.Draw(_pixel, rect, selected ? new Color(120, 92, 30) : Color.DimGray * 0.5f);
                _spriteBatch.DrawString(_font, labels[i], new Vector2(rect.X + 4, rect.Y + 3),
                    Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            }
        }
    }

    private static readonly string[] EditorForwardArrowLabels = { "→", "↓", "←", "↑" };

    private void DrawEditorBottomBar()
    {
        _spriteBatch.DrawString(_font, $"Название: {_editorShipName}", new Vector2(20, 502), Color.LightGray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        _spriteBatch.DrawString(_font, "Нос:", new Vector2(300, 502), Color.Gray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        for (var i = 0; i < EditorForwardOptions.Length; i++)
        {
            var rect = GetEditorForwardArrowRect(i);
            var selected = MathHelperNearlyEqual(_editorForwardDegrees, EditorForwardOptions[i]);
            _spriteBatch.Draw(_pixel, rect, selected ? new Color(120, 92, 30) : Color.DimGray * 0.6f);
            _spriteBatch.DrawString(_font, EditorForwardArrowLabels[i], new Vector2(rect.X + 9, rect.Y + 2),
                Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        }

        var errors = CustomShipValidator.Validate(BuildEditorDefinition());
        var status = errors.Count == 0 ? "Готов к игре!" : errors[0];
        _spriteBatch.DrawString(_font, status, new Vector2(500, 502),
            errors.Count == 0 ? Color.LightGreen : Color.OrangeRed, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        DrawEditorActionButton(EditorAction.Back, "НАЗАД", enabled: true);
        DrawEditorActionButton(EditorAction.Clear, "ОЧИСТИТЬ", enabled: true);
        DrawEditorActionButton(EditorAction.Save, "СОХРАНИТЬ", enabled: true);
        DrawEditorActionButton(EditorAction.Play, "ИГРАТЬ", enabled: errors.Count == 0);
    }

    private void DrawEditorActionButton(EditorAction action, string label, bool enabled)
    {
        var rect = GetEditorActionRect(action);
        var hovered = enabled && rect.Contains(_designMouse);
        _spriteBatch.Draw(_pixel, rect, !enabled ? Color.DimGray * 0.3f : hovered ? new Color(120, 92, 30) : Color.DimGray * 0.6f);
        _spriteBatch.DrawString(_font, label, new Vector2(rect.X + 8, rect.Y + 5),
            enabled ? Color.White : Color.Gray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    private static bool MathHelperNearlyEqual(float a, float b) => System.Math.Abs(a - b) < 0.01f;
}
