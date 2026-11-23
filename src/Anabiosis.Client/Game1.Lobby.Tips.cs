using System;
using System.Collections.Generic;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client;

// The little black boxes Barotrauma shows beside the pointer when it rests on a control: one line or two of text, or - over a starting kit - the kit's name
// and its items with their pictures. Each panel registers the rectangles it wants a tip for while it draws; the one the pointer is on is shown last, over everything.
public partial class Game1
{
    private string? _lobbyTipText;
    private bool _drawingServerWindow;
    private (StartKit Kit, int Number)? _lobbyTipKit;

    private void RegisterLobbyTip(Rectangle area, string text)
    {
        // With the settings window open only its own rows have tips; what lies under it stays quiet.
        if (_serverWindowOpen && !_drawingServerWindow)
            return;
        if (area.Contains(_designMouse))
            _lobbyTipText = text;
    }

    private void DrawLobbyTip()
    {
        if (_lobbyTipKit is { } kitTip && !_serverWindowOpen)
        {
            DrawLobbyKitTip(kitTip.Kit, kitTip.Number);
            return;
        }
        if (_lobbyTipText is not { Length: > 0 } text)
            return;

        const float scale = 0.5f;
        const int width = 240;
        var lines = WrapText(text, width - 16, scale);
        var box = PlaceLobbyTip(width, lines.Count * 14 + 12);
        DrawLobbyTipBox(box);
        for (var i = 0; i < lines.Count; i++)
            _spriteBatch.DrawString(_font, lines[i], new Vector2(box.X + 8, box.Y + 6 + i * 14), SettingsTextPrimary, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    // "КОМПЛЕКТ #1" and the kit's items in a row of pictures, with their names under (the screenshot's tip).
    private void DrawLobbyKitTip(StartKit kit, int number)
    {
        const int cell = 42;
        var width = Math.Max(150, kit.Items.Count * (cell + 6) + 14);
        var box = PlaceLobbyTip(width, 26 + cell + 28);
        DrawLobbyTipBox(box);
        DrawCenteredText($"КОМПЛЕКТ #{number}: {kit.Name}", new Rectangle(box.X, box.Y + 4, box.Width, 16), 0.5f, SettingsAccentGold);
        for (var i = 0; i < kit.Items.Count; i++)
        {
            var slot = new Rectangle(box.X + 8 + i * (cell + 6), box.Y + 26, cell, cell);
            _spriteBatch.Draw(_pixel, slot, new Color(24, 30, 38));
            ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, slot, SettingsBorderSteel * 0.7f, 1);
            ItemIcons.Draw(_spriteBatch, _pixel, kit.Items[i], new Rectangle(slot.X + 3, slot.Y + 3, slot.Width - 6, slot.Height - 6));
            var name = FitText(ItemDefinitions.DisplayName(kit.Items[i]), cell + 4, 0.36f);
            DrawCenteredText(name, new Rectangle(slot.X - 2, slot.Bottom + 1, cell + 4, 12), 0.36f, SettingsTextDim);
        }
    }

    // Near the pointer, kept inside the screen: right and below by default, flipped when there is no room.
    private Rectangle PlaceLobbyTip(int width, int height)
    {
        var x = _designMouse.X + 14;
        var y = _designMouse.Y + 18;
        if (x + width > DesignWidth - 4)
            x = _designMouse.X - width - 8;
        if (y + height > DesignHeight - 4)
            y = _designMouse.Y - height - 8;
        return new Rectangle(Math.Max(4, x), Math.Max(4, y), width, height);
    }

    private void DrawLobbyTipBox(Rectangle box)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(box.X - 2, box.Y - 2, box.Width + 4, box.Height + 4), Color.Black * 0.4f);
        _spriteBatch.Draw(_pixel, box, new Color(8, 10, 13) * 0.97f);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, box, SettingsBorderSteel, 1);
    }
}
