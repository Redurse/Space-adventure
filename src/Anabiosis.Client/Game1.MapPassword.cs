using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Client.Rendering;

namespace Anabiosis.Client;

// The galactic map's password prompt (direct user request: "при открытии галактической карты вначале требовало ввести пароль 3565 (без подсказок) и только
// потом разблокировалась карта"). No hint of any kind: no placeholder boxes that would give the length, nothing about the digits - only a star for each
// character typed. Right once and the map stays open to the end of the session (a new game or a return to the menu locks it again).
public partial class Game1
{
    private const int MapPasswordMaxLength = 16;

    private static double NowSeconds() => Environment.TickCount64 / 1000.0;

    private void HandleMapPasswordChar(char character)
    {
        if (character == '\b')
        {
            if (_mapPasswordInput.Length > 0)
                _mapPasswordInput = _mapPasswordInput[..^1];
            return;
        }
        if (character == '\r')
        {
            if (_mapPasswordInput == GalacticMapPassword)
            {
                _galacticMapUnlocked = true;
                EndMapPasswordPrompt();
                return;
            }
            if (_mapPasswordInput.Length > 0)
                _mapPasswordWrongUntil = NowSeconds() + 1.5;
            _mapPasswordInput = "";
            return;
        }
        if (character is >= '0' and <= '9' && _mapPasswordInput.Length < MapPasswordMaxLength)
        {
            _mapPasswordInput += character;
            _mapPasswordWrongUntil = 0;
        }
    }

    // Lets go of the keyboard the prompt held (the same focus the chat box uses).
    private void EndMapPasswordPrompt()
    {
        _mapPasswordMode = false;
        _mapPasswordInput = "";
        _chatFocused = false;
    }

    // A new game or a return to the menu: the map is locked again.
    private void ResetMapPassword()
    {
        _galacticMapUnlocked = !GalacticMapNeedsPassword;
        _galacticMapOpen = false;
        EndMapPasswordPrompt();
    }

    private void DrawMapPasswordPrompt(float totalSeconds)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, DesignWidth, DesignHeight), Color.Black * 0.94f);

        var panel = new Rectangle(DesignWidth / 2 - 190, DesignHeight / 2 - 80, 380, 160);
        _spriteBatch.Draw(_pixel, panel, new Color(14, 20, 28));
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, panel, new Color(70, 120, 160), 2);

        void Centered(string text, float y, Color color, float scale)
        {
            var size = _font.MeasureString(text) * scale;
            _spriteBatch.DrawString(_font, text, new Vector2(panel.Center.X - size.X / 2f, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        Centered("ГАЛАКТИЧЕСКАЯ КАРТА", panel.Y + 16, new Color(150, 200, 235), 0.6f);
        Centered("Введите пароль", panel.Y + 52, Color.White * 0.85f, 0.5f);

        // One dot per typed character, and a blinking cursor after them.
        var dots = new string('*', _mapPasswordInput.Length);
        var cursor = (int)(totalSeconds * 2f) % 2 == 0 ? "_" : " ";
        Centered(dots + cursor, panel.Y + 84, Color.White, 0.8f);

        if (NowSeconds() < _mapPasswordWrongUntil)
            Centered("Неверный пароль", panel.Y + 126, new Color(235, 110, 100), 0.5f);
    }
}
