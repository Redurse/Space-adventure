using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Anabiosis.Client.Rendering;

namespace Anabiosis.Client;

// The main menu's "НАСТРОЙКИ" screen - real graphics/audio controls, not the disabled placeholder
// it used to be. Edits are staged in a handful of fields here and only actually take effect
// (ApplyGraphicsSettings, Game1.cs) and get written to disk (PlayerSettingsStore) on "Применить";
// "Отмена"/Escape just drops the staged copy and leaves whatever was already running untouched.
public partial class Game1
{
    private enum SettingsTab
    {
        Graphics,
        Audio,
        Controls,
        Interface,
        Misc,
    }

    private static readonly (SettingsTab Tab, string Label)[] SettingsTabs =
    {
        (SettingsTab.Graphics, "Графика"),
        (SettingsTab.Audio, "Звук"),
        (SettingsTab.Controls, "Управление"),
        (SettingsTab.Interface, "Интерфейс"),
        (SettingsTab.Misc, "Прочее"),
    };

    private static readonly (int Width, int Height)[] ResolutionOptions =
    {
        (1280, 720), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160),
    };

    private SettingsTab _settingsTab = SettingsTab.Graphics;
    private int _stagedResolutionIndex;
    private WindowMode _stagedWindowMode;
    private bool _stagedVSync;
    private float _stagedMasterVolume;
    private float _stagedBloomStrength;
    private int _stagedMaxParticles;

    private const int SettingsPanelWidth = 1080;
    private const int SettingsPanelHeight = 460;
    private const int SettingsHeaderHeight = 40;
    private const int SettingsTabColumnWidth = 64;
    private const int SettingsTabButtonSize = 44;
    private const int SettingsContentX = SettingsTabColumnWidth + 20;
    private const int SettingsContentY = SettingsHeaderHeight + 20;

    private static Vector2 SettingsPanelOrigin => new((DesignWidth - SettingsPanelWidth) / 2f, (DesignHeight - SettingsPanelHeight) / 2f);

    private void EnterSettingsScreen()
    {
        _stagedResolutionIndex = FindResolutionIndex(_graphicsSettings.ResolutionWidth, _graphicsSettings.ResolutionHeight);
        _stagedWindowMode = _graphicsSettings.WindowMode;
        _stagedVSync = _graphicsSettings.VSync;
        _stagedMasterVolume = _graphicsSettings.MasterVolume;
        _stagedBloomStrength = _graphicsSettings.BloomStrength;
        _stagedMaxParticles = _graphicsSettings.MaxParticles;
        _settingsTab = SettingsTab.Graphics;
        _menuScreen = MenuScreen.Settings;
    }

    private static int FindResolutionIndex(int? width, int? height)
    {
        if (width is int w && height is int h)
        {
            var index = Array.FindIndex(ResolutionOptions, r => r.Width == w && r.Height == h);
            if (index >= 0)
                return index;
        }
        return Array.FindIndex(ResolutionOptions, r => r.Width == 1920 && r.Height == 1080);
    }

    private static Rectangle GetSettingsTabRect(int index, Vector2 panelOrigin) =>
        new((int)panelOrigin.X + 10, (int)panelOrigin.Y + SettingsHeaderHeight + 10 + index * (SettingsTabButtonSize + 10),
            SettingsTabButtonSize, SettingsTabButtonSize);

    private static Rectangle GetSettingsCancelButtonRect(Vector2 panelOrigin) =>
        new((int)panelOrigin.X + 20, (int)panelOrigin.Y + SettingsPanelHeight - 44, SettingsPanelWidth / 2 - 30, 32);

    private static Rectangle GetSettingsApplyButtonRect(Vector2 panelOrigin) =>
        new((int)panelOrigin.X + SettingsPanelWidth / 2 + 10, (int)panelOrigin.Y + SettingsPanelHeight - 44, SettingsPanelWidth / 2 - 30, 32);

    // Every clickable row in the Graphics tab, in the same left/right two-column layout Draw uses -
    // one source for both, same "shared GetXRect" convention as GetTabRect/GetEulaAcceptButtonRect.
    private static Vector2 SettingsContentOrigin(Vector2 panelOrigin) => panelOrigin + new Vector2(SettingsContentX, SettingsContentY);
    private static float SettingsRightColumnX(Vector2 contentOrigin) => contentOrigin.X + (SettingsPanelWidth - SettingsContentX - 20) / 2f + 10f;

    private static Rectangle GetResolutionPrevRect(Vector2 panelOrigin) => new((int)SettingsContentOrigin(panelOrigin).X, (int)SettingsContentOrigin(panelOrigin).Y + 18, 24, 24);
    private static Rectangle GetResolutionNextRect(Vector2 panelOrigin) => new((int)SettingsContentOrigin(panelOrigin).X + 220, (int)SettingsContentOrigin(panelOrigin).Y + 18, 24, 24);
    private static Rectangle GetWindowModePrevRect(Vector2 panelOrigin) => new((int)SettingsContentOrigin(panelOrigin).X, (int)SettingsContentOrigin(panelOrigin).Y + 78, 24, 24);
    private static Rectangle GetWindowModeNextRect(Vector2 panelOrigin) => new((int)SettingsContentOrigin(panelOrigin).X + 220, (int)SettingsContentOrigin(panelOrigin).Y + 78, 24, 24);
    private static Rectangle GetVSyncCheckboxRect(Vector2 panelOrigin) => new((int)SettingsContentOrigin(panelOrigin).X, (int)SettingsContentOrigin(panelOrigin).Y + 128, 20, 20);

    private static Rectangle GetBloomSliderRect(Vector2 panelOrigin)
    {
        var origin = SettingsContentOrigin(panelOrigin);
        return new Rectangle((int)SettingsRightColumnX(origin), (int)origin.Y + 18, 260, 10);
    }

    private static Rectangle GetParticlesSliderRect(Vector2 panelOrigin)
    {
        var origin = SettingsContentOrigin(panelOrigin);
        return new Rectangle((int)SettingsRightColumnX(origin), (int)origin.Y + 78, 260, 10);
    }

    private static Rectangle GetVolumeSliderRect(Vector2 panelOrigin)
    {
        var origin = SettingsContentOrigin(panelOrigin);
        return new Rectangle((int)origin.X, (int)origin.Y + 18, 320, 10);
    }

    private void HandleSettingsScreen(KeyboardState keyboard)
    {
        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        var held = mouse.LeftButton == ButtonState.Pressed;
        var point = _designMouse;
        var origin = SettingsPanelOrigin;

        if (clicked)
        {
            for (var i = 0; i < SettingsTabs.Length; i++)
                if (GetSettingsTabRect(i, origin).Contains(point))
                    _settingsTab = SettingsTabs[i].Tab;

            if (GetSettingsCancelButtonRect(origin).Contains(point))
            {
                _menuScreen = MenuScreen.Main;
                _prevMenuLeftMouseButton = mouse.LeftButton;
                return;
            }
            if (GetSettingsApplyButtonRect(origin).Contains(point))
            {
                ApplySettings();
                _prevMenuLeftMouseButton = mouse.LeftButton;
                return;
            }

            if (_settingsTab == SettingsTab.Graphics)
            {
                if (GetResolutionPrevRect(origin).Contains(point))
                    _stagedResolutionIndex = (_stagedResolutionIndex - 1 + ResolutionOptions.Length) % ResolutionOptions.Length;
                else if (GetResolutionNextRect(origin).Contains(point))
                    _stagedResolutionIndex = (_stagedResolutionIndex + 1) % ResolutionOptions.Length;
                else if (GetWindowModePrevRect(origin).Contains(point))
                    _stagedWindowMode = CycleWindowMode(_stagedWindowMode, -1);
                else if (GetWindowModeNextRect(origin).Contains(point))
                    _stagedWindowMode = CycleWindowMode(_stagedWindowMode, 1);
                else if (GetVSyncCheckboxRect(origin).Contains(point))
                    _stagedVSync = !_stagedVSync;
            }
            else if (_settingsTab == SettingsTab.Interface)
            {
                if (GetChangeNicknameButtonRect(origin).Contains(point))
                    _menuScreen = MenuScreen.Nickname;
                else if (GetChangeRoleButtonRect(origin).Contains(point))
                    _menuScreen = MenuScreen.Role;
            }
            else if (_settingsTab == SettingsTab.Misc)
            {
                if (GetResetSettingsButtonRect(origin).Contains(point))
                {
                    var defaults = new GraphicsSettings(null, null, WindowMode.Borderless, true, 1f, 1f, 400);
                    _stagedResolutionIndex = FindResolutionIndex(null, null);
                    _stagedWindowMode = defaults.WindowMode;
                    _stagedVSync = defaults.VSync;
                    _stagedMasterVolume = defaults.MasterVolume;
                    _stagedBloomStrength = defaults.BloomStrength;
                    _stagedMaxParticles = defaults.MaxParticles;
                }
                else if (GetOpenCreditsButtonRect(origin).Contains(point))
                {
                    _menuScreen = MenuScreen.Credits;
                    _creditsStart = null;
                }
            }
        }

        // Sliders drag continuously rather than click-once, same "held, not edge-triggered" input
        // a track bar needs - unlike every button/checkbox above, which only ever act on the frame
        // the button goes down.
        if (held)
        {
            if (_settingsTab == SettingsTab.Graphics)
            {
                _stagedBloomStrength = TrySliderValue(GetBloomSliderRect(origin), point, 0f, 2f) ?? _stagedBloomStrength;
                _stagedMaxParticles = (int)(TrySliderValue(GetParticlesSliderRect(origin), point, 0f, 1000f) ?? _stagedMaxParticles);
            }
            else if (_settingsTab == SettingsTab.Audio)
            {
                _stagedMasterVolume = TrySliderValue(GetVolumeSliderRect(origin), point, 0f, 1f) ?? _stagedMasterVolume;
            }
        }

        _prevMenuLeftMouseButton = mouse.LeftButton;
    }

    // Null when the pointer isn't over the track at all (whether or not the button is held) - the
    // caller only overwrites the staged value when this actually hit.
    private static float? TrySliderValue(Rectangle track, Point point, float min, float max)
    {
        var hitBox = new Rectangle(track.X, track.Y - 8, track.Width, track.Height + 16);
        if (!hitBox.Contains(point))
            return null;
        var fraction = Math.Clamp((point.X - track.X) / (float)track.Width, 0f, 1f);
        return min + fraction * (max - min);
    }

    private static WindowMode CycleWindowMode(WindowMode mode, int direction)
    {
        var values = Enum.GetValues<WindowMode>();
        var index = Array.IndexOf(values, mode);
        return values[(index + direction + values.Length) % values.Length];
    }

    private void ApplySettings()
    {
        var (w, h) = ResolutionOptions[_stagedResolutionIndex];
        var settings = new GraphicsSettings(w, h, _stagedWindowMode, _stagedVSync, _stagedMasterVolume, _stagedBloomStrength, _stagedMaxParticles);
        ApplyGraphicsSettings(settings);
        PlayerSettingsStore.SaveGraphicsSettings(settings);
        _menuScreen = MenuScreen.Main;
    }

    private static Rectangle GetChangeNicknameButtonRect(Vector2 panelOrigin) =>
        new((int)SettingsContentOrigin(panelOrigin).X + 260, (int)SettingsContentOrigin(panelOrigin).Y + 4, 100, 28);
    private static Rectangle GetChangeRoleButtonRect(Vector2 panelOrigin) =>
        new((int)SettingsContentOrigin(panelOrigin).X + 260, (int)SettingsContentOrigin(panelOrigin).Y + 54, 100, 28);
    private static Rectangle GetResetSettingsButtonRect(Vector2 panelOrigin) =>
        new((int)SettingsContentOrigin(panelOrigin).X, (int)SettingsContentOrigin(panelOrigin).Y, 220, 32);
    private static Rectangle GetOpenCreditsButtonRect(Vector2 panelOrigin) =>
        new((int)SettingsContentOrigin(panelOrigin).X, (int)SettingsContentOrigin(panelOrigin).Y + 44, 220, 32);

    private void DrawSettingsScreen()
    {
        var origin = SettingsPanelOrigin;
        var panelRect = new Rectangle((int)origin.X, (int)origin.Y, SettingsPanelWidth, SettingsPanelHeight);
        var header = PanelFrame.DrawWithHeader(_spriteBatch, _pixel, panelRect, SettingsHeaderHeight);
        _spriteBatch.DrawString(_font, "Настройки", origin + new Vector2(16, 10), Color.White, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
        _spriteBatch.Draw(_pixel, new Rectangle(panelRect.X + SettingsTabColumnWidth, header.Bottom, 2, panelRect.Height - SettingsHeaderHeight), new Color(90, 110, 95));

        for (var i = 0; i < SettingsTabs.Length; i++)
        {
            var (tab, label) = SettingsTabs[i];
            var rect = GetSettingsTabRect(i, origin);
            var active = tab == _settingsTab;
            _spriteBatch.Draw(_pixel, rect, active ? new Color(70, 100, 85) : new Color(32, 40, 35));
            DrawSettingsTabGlyph(tab, new Vector2(rect.Center.X, rect.Center.Y - 4f), active ? Color.White : Color.LightGray);
        }

        var content = SettingsContentOrigin(origin);
        switch (_settingsTab)
        {
            case SettingsTab.Graphics:
                DrawGraphicsTab(content);
                break;
            case SettingsTab.Audio:
                DrawAudioTab(content);
                break;
            case SettingsTab.Controls:
                DrawControlsTab(content);
                break;
            case SettingsTab.Interface:
                DrawInterfaceTab(content);
                break;
            case SettingsTab.Misc:
                DrawMiscTab(content);
                break;
        }

        var cancelRect = GetSettingsCancelButtonRect(origin);
        var applyRect = GetSettingsApplyButtonRect(origin);
        var cancelHover = cancelRect.Contains(_designMouse);
        var applyHover = applyRect.Contains(_designMouse);
        _spriteBatch.Draw(_pixel, cancelRect, cancelHover ? new Color(80, 84, 86) : new Color(60, 64, 66));
        _spriteBatch.Draw(_pixel, applyRect, applyHover ? new Color(80, 130, 100) : new Color(60, 100, 80));
        DrawCenteredLabel("ОТМЕНА", cancelRect, Color.White);
        DrawCenteredLabel("ПРИМЕНИТЬ", applyRect, Color.White);
    }

    private void DrawCenteredLabel(string text, Rectangle rect, Color color)
    {
        var size = _font.MeasureString(text) * 0.55f;
        _spriteBatch.DrawString(_font, text, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f),
            color, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private void DrawSettingsTabGlyph(SettingsTab tab, Vector2 center, Color color)
    {
        switch (tab)
        {
            case SettingsTab.Graphics: // an eye - the graphics/display tab
                HudIcons.DrawRingArc(_spriteBatch, _pixel, center, 9f, 200f, 340f, color, 10, 1.6f);
                HudIcons.DrawRingArc(_spriteBatch, _pixel, center, 9f, 20f, 160f, color, 10, 1.6f);
                HudIcons.FillCircle(_spriteBatch, _pixel, center, 3.5f, color);
                break;
            case SettingsTab.Audio: // headphones
                HudIcons.DrawRingArc(_spriteBatch, _pixel, center + new Vector2(0, -2), 8f, 200f, 340f, color, 8, 1.8f);
                _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - 9, (int)center.Y - 2, 4, 8), color);
                _spriteBatch.Draw(_pixel, new Rectangle((int)center.X + 5, (int)center.Y - 2, 4, 8), color);
                break;
            case SettingsTab.Controls: // a small key grid
                for (var row = 0; row < 2; row++)
                    for (var col = 0; col < 3; col++)
                        _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - 12 + col * 8, (int)center.Y - 8 + row * 8, 6, 6), color);
                break;
            case SettingsTab.Interface: // literal "Aa" text, same shorthand the reference screenshot used
                _spriteBatch.DrawString(_font, "Aa", center - new Vector2(9, 8), color, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
                break;
            case SettingsTab.Misc: // a small diamond
                _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - 1, (int)center.Y - 9, 2, 18), color);
                _spriteBatch.Draw(_pixel, new Rectangle((int)center.X - 9, (int)center.Y - 1, 18, 2), color);
                break;
        }
    }

    private void DrawGraphicsTab(Vector2 content)
    {
        var (w, h) = ResolutionOptions[_stagedResolutionIndex];
        DrawLabeledStepper(content, "РАЗРЕШЕНИЕ", $"{w}x{h}", GetResolutionPrevRect(SettingsPanelOrigin), GetResolutionNextRect(SettingsPanelOrigin));
        DrawLabeledStepper(content + new Vector2(0, 60), "РЕЖИМ ЭКРАНА", WindowModeLabel(_stagedWindowMode),
            GetWindowModePrevRect(SettingsPanelOrigin), GetWindowModeNextRect(SettingsPanelOrigin));

        var vsyncRect = GetVSyncCheckboxRect(SettingsPanelOrigin);
        _spriteBatch.Draw(_pixel, vsyncRect, new Color(20, 26, 22));
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, vsyncRect, new Color(90, 110, 95), 1);
        if (_stagedVSync)
            _spriteBatch.Draw(_pixel, new Rectangle(vsyncRect.X + 4, vsyncRect.Y + 4, vsyncRect.Width - 8, vsyncRect.Height - 8), new Color(90, 220, 195));
        _spriteBatch.DrawString(_font, "Вертикальная синхронизация", new Vector2(vsyncRect.Right + 10, vsyncRect.Y + 2), Color.LightGray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        var right = new Vector2(SettingsRightColumnX(content), content.Y);
        DrawLabeledSlider(right, "СВЕЧЕНИЕ (BLOOM)", $"{_stagedBloomStrength * 50f:0}%", GetBloomSliderRect(SettingsPanelOrigin), _stagedBloomStrength / 2f);
        DrawLabeledSlider(right + new Vector2(0, 60), "МАКС. КОЛИЧЕСТВО ЧАСТИЦ", $"{_stagedMaxParticles}", GetParticlesSliderRect(SettingsPanelOrigin), _stagedMaxParticles / 1000f);
    }

    private void DrawAudioTab(Vector2 content) =>
        DrawLabeledSlider(content, "ОБЩАЯ ГРОМКОСТЬ", $"{_stagedMasterVolume * 100f:0}%", GetVolumeSliderRect(SettingsPanelOrigin), _stagedMasterVolume);

    private static readonly string[] ControlsReference =
    {
        "WASD — движение",
        "ЛКМ — использовать / стрелять",
        "F — взаимодействие",
        "Tab — экипаж",
        "Esc — меню паузы",
        "1-4 — выбор корабля (при старте)",
    };

    private void DrawControlsTab(Vector2 content)
    {
        for (var i = 0; i < ControlsReference.Length; i++)
            _spriteBatch.DrawString(_font, ControlsReference[i], content + new Vector2(0, i * 26), Color.LightGray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    private void DrawInterfaceTab(Vector2 content)
    {
        _spriteBatch.DrawString(_font, $"Ник: {_nickname}", content, Color.LightGray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        DrawSmallButton(GetChangeNicknameButtonRect(SettingsPanelOrigin), "ИЗМЕНИТЬ");

        _spriteBatch.DrawString(_font, $"Роль: {(_selectedRole is { } role ? role.ToString() : "не выбрана")}", content + new Vector2(0, 50), Color.LightGray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        DrawSmallButton(GetChangeRoleButtonRect(SettingsPanelOrigin), "ИЗМЕНИТЬ");
    }

    private void DrawMiscTab(Vector2 content)
    {
        DrawSmallButton(GetResetSettingsButtonRect(SettingsPanelOrigin), "СБРОСИТЬ НАСТРОЙКИ");
        DrawSmallButton(GetOpenCreditsButtonRect(SettingsPanelOrigin), "АВТОРЫ");
    }

    private void DrawSmallButton(Rectangle rect, string label)
    {
        var hovered = rect.Contains(_designMouse);
        _spriteBatch.Draw(_pixel, rect, hovered ? new Color(70, 90, 78) : new Color(45, 55, 50));
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, rect, new Color(90, 110, 95), 1);
        DrawCenteredLabel(label, rect, hovered ? Color.Gold : Color.LightGray);
    }

    private void DrawLabeledStepper(Vector2 position, string label, string value, Rectangle prevRect, Rectangle nextRect)
    {
        _spriteBatch.DrawString(_font, label, position, Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        DrawSmallButton(prevRect, "<");
        DrawSmallButton(nextRect, ">");
        var valueSize = _font.MeasureString(value) * 0.6f;
        var valueCenter = new Vector2((prevRect.Right + nextRect.X) / 2f, prevRect.Center.Y);
        _spriteBatch.DrawString(_font, value, valueCenter - valueSize / 2f, Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    private void DrawLabeledSlider(Vector2 position, string label, string value, Rectangle track, float fraction)
    {
        _spriteBatch.DrawString(_font, label, position, Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        _spriteBatch.Draw(_pixel, track, new Color(20, 26, 22));
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, track, new Color(90, 110, 95), 1);
        var fillWidth = (int)(track.Width * Math.Clamp(fraction, 0f, 1f));
        if (fillWidth > 0)
            _spriteBatch.Draw(_pixel, new Rectangle(track.X, track.Y, fillWidth, track.Height), new Color(90, 220, 195));
        var handleX = track.X + fillWidth;
        _spriteBatch.Draw(_pixel, new Rectangle(handleX - 2, track.Y - 3, 4, track.Height + 6), Color.White);
        _spriteBatch.DrawString(_font, value, new Vector2(track.Right + 12, track.Y - 5), Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private static string WindowModeLabel(WindowMode mode) => mode switch
    {
        WindowMode.Fullscreen => "Полноэкранный",
        WindowMode.Windowed => "Оконный",
        _ => "Оконный без рамки",
    };
}
