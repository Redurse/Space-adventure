using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client;

// The five-slide history lesson shown once, between picking a hull on ShipSelect and the campaign
// actually starting: predtechi, exodus, a century of settling in, the expedition finally launching,
// and the find that ends it. Continue/Tutorial skip straight past this file entirely - it only
// ever plays on the way into a genuinely new campaign (HandleShipSelect's index branch).
public partial class Game1
{
    private readonly record struct PrologueSlide(string[] Lines, PrologueVisual Visual);

    private static readonly PrologueSlide[] PrologueSlides =
    {
        new(new[]
        {
            "Люди пришли в эту галактику не первыми.",
            "До них здесь жила цивилизация, которую называют предтечами. От них остались только руины — и молчание.",
        }, PrologueVisual.Ruins),
        new(new[]
        {
            "На родине люди находили то же самое: развалины исчезнувших рас, ни одной живой.",
            "Дальше было только расширяться. Корабли-метрополии понесли целые поколения за пределы одной галактики.",
        }, PrologueVisual.Exodus),
        new(new[]
        {
            "Сто лет назад один такой корабль прибыл сюда.",
            "Первый век ушёл не на открытия, а на то, чтобы просто выжить и закрепиться.",
        }, PrologueVisual.Settlement),
        new(new[]
        {
            "Закрепление завершено. Колониальный союз наконец готов начать то, ради чего век держал на борту штат учёных:",
            "первые настоящие экспедиции по следам предтеч.",
        }, PrologueVisual.Expedition),
        new(new[]
        {
            "На фронтирной колонии, в развалинах древнего поста, экспедиция находит то, что искала.",
            "Артефакт. Первый — и последний — спокойный день новой эпохи.",
        }, PrologueVisual.Artifact),
    };

    private const float PrologueCharsPerSecond = 30f;
    private const float PrologueExitFadeSeconds = 0.9f;
    private const float PrologueSlideFadeInSeconds = 0.4f;

    private int _prologueIndex;
    private float? _prologueSlideStart;
    private bool _prologueRevealComplete;
    private bool _prologueSkipReveal;
    private bool _prologueExiting;
    private float _prologueExitElapsed;
    // What to actually start once the last slide fades out - stashed here rather than acted on
    // immediately so the whole five-slide sequence sits between "hull picked" and "world exists",
    // exactly where a history lesson belongs and nowhere a save or a server can see it.
    private ShipKind? _prologuePendingShipKind;

    // Entry point for HandleShipSelect's new-campaign branch. Continue/Tutorial call
    // StartHostedSession directly and never touch this - the prologue is about how the player's
    // own campaign began, which neither of those is doing.
    private void BeginPrologue(ShipKind shipKind)
    {
        _prologuePendingShipKind = shipKind;
        _prologueIndex = 0;
        _prologueSlideStart = null;
        _prologueRevealComplete = false;
        _prologueSkipReveal = false;
        _prologueExiting = false;
        _prologueExitElapsed = 0f;
        _menuScreen = MenuScreen.Prologue;
    }

    // Escape's one-shot "skip the whole thing" - unlike every other sub-screen's Escape, this does
    // not step back toward ShipSelect (there is nothing to reconsider here, just history to get
    // past), it heads straight for the same fade the last slide's own advance uses.
    private void SkipPrologue()
    {
        if (_prologueExiting)
            return;
        _prologueExiting = true;
        _prologueExitElapsed = 0f;
    }

    private void HandlePrologueScreen(KeyboardState keyboard, float deltaSeconds)
    {
        if (_prologueExiting)
        {
            _prologueExitElapsed += deltaSeconds;
            if (_prologueExitElapsed >= PrologueExitFadeSeconds)
                FinishPrologue();
            return;
        }

        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        _prevMenuLeftMouseButton = mouse.LeftButton;

        if (!(clicked || Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Space)))
            return;

        // First press just finishes the current line instantly - same "don't make me wait twice"
        // courtesy the credits roll gets via its own skip-to-exit.
        if (!_prologueRevealComplete)
        {
            _prologueSkipReveal = true;
            return;
        }

        if (_prologueIndex >= PrologueSlides.Length - 1)
        {
            _prologueExiting = true;
            _prologueExitElapsed = 0f;
        }
        else
        {
            _prologueIndex++;
            _prologueSlideStart = null;
            _prologueRevealComplete = false;
        }
    }

    // The one moment this file actually starts the session - always from Update (here or via the
    // exit-fade timer above), never from Draw, so _sessionStarted flips at the same kind of moment
    // PollJoin/StartHostedSession already flip it at everywhere else in the menu.
    private void FinishPrologue()
    {
        _prologueExiting = false;
        // The hull is kept, not consumed: the role screen is the one that starts the session now, and
        // it needs to know which ship the campaign was begun in. Picking a crew role is the last
        // thing that happens before boarding, which is the only moment it is ever asked.
        _menuScreen = MenuScreen.Role;
    }

    private void DrawPrologueScreen(float totalSeconds)
    {
        var pane = new Rectangle(0, 0, DesignWidth, DesignHeight);
        var slide = PrologueSlides[_prologueIndex];
        PrologueScene.Draw(_spriteBatch, _pixel, pane, totalSeconds, slide.Visual);

        _prologueSlideStart ??= totalSeconds;
        var sinceSlideStart = totalSeconds - _prologueSlideStart.Value;

        var fullLength = 0;
        foreach (var line in slide.Lines)
            fullLength += line.Length + 1;

        int shown;
        if (_prologueRevealComplete)
            shown = fullLength;
        else if (_prologueSkipReveal)
            shown = fullLength;
        else
            shown = (int)(sinceSlideStart * PrologueCharsPerSecond);
        _prologueSkipReveal = false;
        if (shown >= fullLength)
        {
            shown = fullLength;
            _prologueRevealComplete = true;
        }

        DrawPrologueText(slide, shown);
        DrawPrologueProgress();
        DrawPrologueHint();

        // A short fade up from black at the start of every slide - masks the hard cut of the
        // backdrop swapping under the text, cheaper than crossfading the two scenes for real.
        if (sinceSlideStart < PrologueSlideFadeInSeconds)
        {
            var fadeIn = 1f - sinceSlideStart / PrologueSlideFadeInSeconds;
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, DesignWidth, DesignHeight), Color.Black * (fadeIn * fadeIn));
        }

        if (_prologueExiting)
        {
            var fadeOut = MathHelper.Clamp(_prologueExitElapsed / PrologueExitFadeSeconds, 0f, 1f);
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, DesignWidth, DesignHeight), Color.Black * fadeOut);
        }
    }

    private void DrawPrologueText(PrologueSlide slide, int shown)
    {
        const float scale = 0.7f;
        const float lineHeight = 30f;
        var textTop = DesignHeight * 0.64f;
        var remaining = shown;

        // A dark band behind the narration, the same trick the docking-chatter ticker already
        // uses (Game1.Menu.cs's DrawTrafficTicker) - guarantees the text reads over whatever the
        // backdrop happens to be doing underneath it, rather than choreographing every scene to
        // dodge a fixed strip of the screen.
        var bandTop = textTop - 12f;
        var bandHeight = slide.Lines.Length * lineHeight + 8f;
        _spriteBatch.Draw(_pixel, new Rectangle(0, (int)bandTop, DesignWidth, (int)bandHeight), new Color(6, 9, 10) * 0.62f);

        for (var i = 0; i < slide.Lines.Length; i++)
        {
            var line = slide.Lines[i];
            var take = Math.Clamp(remaining, 0, line.Length);
            remaining -= line.Length + 1;
            if (take == 0)
                break;

            var visible = line[..take];
            // Measured against the full line, not the visible slice, so the text sits centred on
            // its final width the whole time it is typing in rather than re-centring every frame.
            var fullSize = _font.MeasureString(line) * scale;
            var pos = new Vector2((DesignWidth - fullSize.X) / 2f, textTop + i * lineHeight);

            _spriteBatch.DrawString(_font, visible, pos + new Vector2(2f, 2f), Color.Black * 0.6f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, visible, pos, new Color(224, 233, 227), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            if (take < line.Length)
                break;
        }
    }

    private void DrawPrologueProgress()
    {
        var count = PrologueSlides.Length;
        var spacing = 16f;
        var startX = (DesignWidth - (count - 1) * spacing) / 2f;
        var y = DesignHeight * 0.64f - 22f;
        for (var i = 0; i < count; i++)
        {
            var current = i == _prologueIndex;
            var color = current ? new Color(180, 222, 205) : new Color(64, 78, 74);
            HudIcons.FillCircle(_spriteBatch, _pixel, new Vector2(startX + i * spacing, y), current ? 3.2f : 2f, color);
        }
    }

    private void DrawPrologueHint()
    {
        var isLast = _prologueIndex >= PrologueSlides.Length - 1;
        var advance = !_prologueRevealComplete
            ? "ENTER / ПРОБЕЛ / КЛИК"
            : isLast ? "ENTER / ПРОБЕЛ / КЛИК — В ПУТЬ" : "ENTER / ПРОБЕЛ / КЛИК — ДАЛЕЕ";
        var hint = advance + "     ESC — ПРОПУСТИТЬ ПРОЛОГ";
        var hintSize = _font.MeasureString(hint) * 0.42f;
        _spriteBatch.DrawString(_font, hint, new Vector2((DesignWidth - hintSize.X) / 2f, DesignHeight - 24f),
            new Color(122, 140, 134) * 0.85f, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
    }
}
