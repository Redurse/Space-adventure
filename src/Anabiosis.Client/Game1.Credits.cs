using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Anabiosis.Client;

// The credits roll behind the main menu's АВТОРЫ button. Its own file rather than another few
// hundred lines in Game1.Menu.cs, which is already the busiest screen in the game.
public partial class Game1
{
    // Design pixels per second. Slow enough to read a line, fast enough that the whole list goes by
    // without the player feeling trapped - it loops rather than ending, so there is no dead screen
    // at the bottom either.
    private const float CreditsScrollSpeed = 46f;
    private const float CreditsLineHeight = 26f;
    private const float CreditsGap = 14f;

    // Null until the roll is first drawn; the scroll is derived from the elapsed time since, so
    // nothing has to be advanced by hand each frame. Cleared on the way out so re-entering starts
    // from the top instead of halfway down.
    private float? _creditsStart;

    // Every role credits the same one solo dev except ТЕСТИРОВАНИЕ (testing), which names both -
    // direct user request.
    private const string CreditsName = "Eisenhorn";
    private const string TesterName = "ValeN, Eisenhorn";

    private static readonly (string Role, string Name)[] CreditsEntries =
    {
        ("ГЕЙМ-ДИЗАЙН", CreditsName),
        ("ВЕДУЩИЙ ПРОГРАММИСТ", CreditsName),
        ("КОД-МЕНЕДЖЕР", CreditsName),
        ("АРХИТЕКТУРА", CreditsName),
        ("СЕТЕВОЙ КОД", CreditsName),
        ("ИГРОВАЯ ЛОГИКА", CreditsName),
        ("ИСКУССТВЕННЫЙ ИНТЕЛЛЕКТ", CreditsName),
        ("ФИЗИКА", CreditsName),
        ("ГРАФИКА И ШЕЙДЕРЫ", CreditsName),
        ("ОСВЕЩЕНИЕ", CreditsName),
        ("ТЕХНИЧЕСКИЙ ХУДОЖНИК", CreditsName),
        ("КОНЦЕПТ-АРТ", CreditsName),
        ("АРТ-ДИРЕКТОР", CreditsName),
        ("АНИМАЦИЯ", CreditsName),
        ("ИНТЕРФЕЙС", CreditsName),
        ("ЗВУК", CreditsName),
        ("МУЗЫКА", CreditsName),
        ("СЦЕНАРИЙ", CreditsName),
        ("БАЛАНС", CreditsName),
        ("ИНСТРУМЕНТЫ РАЗРАБОТКИ", CreditsName),
        ("СБОРКА И РЕЛИЗ", CreditsName),
        ("ОПТИМИЗАЦИЯ", CreditsName),
        ("КОНТРОЛЬ КАЧЕСТВА", CreditsName),
        ("ТЕСТИРОВАНИЕ", TesterName),
        ("ЛОКАЛИЗАЦИЯ", CreditsName),
        ("ДОКУМЕНТАЦИЯ", CreditsName),
        ("ТЕХНИЧЕСКИЙ ДИРЕКТОР", CreditsName),
        ("ПРОДЮСЕР", CreditsName),
        ("ИСПОЛНИТЕЛЬНЫЙ ПРОДЮСЕР", CreditsName),
        ("КОММЬЮНИТИ-МЕНЕДЖЕР", CreditsName),
        ("ПОДДЕРЖКА ИГРОКОВ", CreditsName),
        ("МАРКЕТИНГ", CreditsName),
        ("ЮРИДИЧЕСКИЙ ОТДЕЛ", CreditsName),
        ("БУХГАЛТЕРИЯ", CreditsName),
        ("ПОДБОР ПЕРСОНАЛА", CreditsName),
        ("СИСТЕМНЫЙ АДМИНИСТРАТОР", CreditsName),
        ("СНАБЖЕНИЕ КОФЕ", CreditsName),
        ("МОРАЛЬНАЯ ПОДДЕРЖКА", CreditsName),
        ("ОСОБАЯ БЛАГОДАРНОСТЬ", CreditsName),
    };

    private void HandleCreditsScreen(KeyboardState keyboard)
    {
        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        _prevMenuLeftMouseButton = mouse.LeftButton;

        // Escape goes back through LeaveSubScreen like every other sub-screen; these are the extra
        // ways out, because a roll you cannot skip is the one thing everybody hates about credits.
        if (clicked || Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Space))
            ReturnFromCredits();
    }

    private void ReturnFromCredits()
    {
        _menuScreen = MenuScreen.Main;
        _creditsStart = null;
    }

    private void DrawCreditsScreen(float totalSeconds)
    {
        _creditsStart ??= totalSeconds;

        var blockHeight = CreditsEntries.Length * (CreditsLineHeight + CreditsGap);
        var travelled = (totalSeconds - _creditsStart.Value) * CreditsScrollSpeed;
        // Loop by wrapping the distance travelled rather than resetting the start time: no jump at
        // the seam, and the list simply comes round again.
        travelled %= blockHeight + DesignHeight;
        var top = DesignHeight - travelled;

        var titleSize = _font.MeasureString("АВТОРЫ") * 0.9f;
        _spriteBatch.DrawString(_font, "АВТОРЫ", new Vector2((DesignWidth - titleSize.X) / 2f, 18f),
            new Color(198, 214, 235), 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);

        for (var i = 0; i < CreditsEntries.Length; i++)
        {
            var y = top + i * (CreditsLineHeight + CreditsGap);
            // Off screen either way - skip before measuring anything.
            if (y < -CreditsLineHeight || y > DesignHeight)
                continue;

            // Fade in and out at the edges so lines arrive and leave instead of popping.
            var edge = MathHelper.Clamp(MathF.Min(y, DesignHeight - y) / 60f, 0f, 1f);

            var (role, name) = CreditsEntries[i];
            var roleSize = _font.MeasureString(role) * 0.6f;
            // Role right-aligned into the left column, name left-aligned into the right one, so the
            // two meet at the middle and the eye has a single vertical line to follow down.
            _spriteBatch.DrawString(_font, role, new Vector2(DesignWidth / 2f - 24f - roleSize.X, y),
                new Color(150, 166, 186) * edge, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, name, new Vector2(DesignWidth / 2f + 24f, y - 3f),
                new Color(236, 226, 190) * edge, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        }

        const string hint = "ESC / ПРОБЕЛ / КЛИК - НАЗАД";
        var hintSize = _font.MeasureString(hint) * 0.5f;
        _spriteBatch.DrawString(_font, hint, new Vector2((DesignWidth - hintSize.X) / 2f, DesignHeight - 26f),
            new Color(120, 132, 148), 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }
}
