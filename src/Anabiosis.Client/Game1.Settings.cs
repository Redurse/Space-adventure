using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Anabiosis.Client.Input;
using Anabiosis.Client.Rendering;

namespace Anabiosis.Client;

// The main menu's "НАСТРОЙКИ" screen - real graphics/audio controls, not the disabled placeholder
// it used to be. Edits are staged in a handful of fields here and only actually take effect
// (ApplyGraphicsSettings, Game1.cs) and get written to disk (PlayerSettingsStore) on "Применить";
// "Отмена"/Escape just drops the staged copy and leaves whatever was already running untouched.
//
// The "Звук" tab (direct user request, "почти точь в точь как в Baротравме") uses a device STEPPER
// (cycle with </>, same control DrawLabeledStepper already gives Resolution/WindowMode) rather than
// a literal opening dropdown list - a real "expand into a floating row list" widget doesn't exist
// anywhere in this project yet, and building one from scratch was a bigger, separate risk than this
// feature already carries; cycling through the same device list a dropdown would show gets the same
// end result (pick any enumerated device) with far less new UI-widget surface area.
public partial class Game1
{
    private enum SettingsTab
    {
        Graphics,
        Audio,
        Controls,
        Interface,
        Misc,
        // Direct user request ("в новом разделе настроек... достижения можно было листать как в
        // Стиме") - browse-only, nothing here to stage/apply (Game1.Settings.Achievements.cs).
        Achievements,
    }

    private static readonly (SettingsTab Tab, string Label)[] SettingsTabs =
    {
        (SettingsTab.Graphics, "Графика"),
        (SettingsTab.Audio, "Звук"),
        (SettingsTab.Controls, "Управление"),
        (SettingsTab.Interface, "Интерфейс"),
        (SettingsTab.Misc, "Прочее"),
        (SettingsTab.Achievements, "Достижения"),
    };

    private static readonly (int Width, int Height)[] ResolutionOptions =
    {
        (1280, 720), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160),
    };

    private SettingsTab _settingsTab = SettingsTab.Graphics;
    private int _stagedResolutionIndex;
    private WindowMode _stagedWindowMode;
    private bool _stagedVSync;
    private float _stagedBloomStrength;
    private int _stagedMaxParticles;
    private bool _stagedShadersEnabled;

    // Audio tab - device pickers (index 0 = "Системное по умолчанию"/null, 1.. = an enumerated
    // device) plus every Barotrauma-reference slider/checkbox.
    private IReadOnlyList<(string Id, string Name)> _stagedOutputDevices = Array.Empty<(string, string)>();
    private int _stagedOutputDeviceIndex;
    private string? _stagedOutputDeviceId;
    private IReadOnlyList<string> _stagedInputDevices = Array.Empty<string>();
    private int _stagedInputDeviceIndex;
    private string? _stagedInputMicrophoneName;
    private float _stagedSoundVolume;
    private float _stagedMusicVolume;
    private float _stagedUiVolume;
    private float _stagedVoiceChatVolume;
    private bool _stagedMuteOnFocusLoss;
    private bool _stagedDynamicRangeCompression;
    private bool _stagedDirectionalVoiceChat;
    private bool _stagedVoiceChatPriority;
    private bool _stagedVoiceActivationEnabled;
    private float _stagedVoiceActivationThreshold;
    private float _stagedMicGain;
    private float _stagedDisconnectPreventionMs;

    // Controls tab - a working copy of the live bindings, edited in place as the player rebinds
    // rows, committed on "Применить" like every other staged field. _awaitingRebindAction is which
    // row (if any) is currently waiting for the next physical key press to land in; null the rest
    // of the time.
    private PlayerActionBindings _stagedKeyBindings = new();
    private PlayerAction? _awaitingRebindAction;

    // Interface tab - language selector (no localized text behind it yet, direct user request
    // scoped to "just add the switch") plus toggles for the 3 already-real mechanics the reference
    // screenshot's own items actually map to in this game (item tooltips, chat speech bubbles,
    // boarding health bars). Deliberately excludes: "Пауза при переключении окна" (would need a
    // real multiplayer-aware world-pause that doesn't exist and would be unsafe to freeze other
    // players' sessions with), "Значки взаимодействия" (no interaction-icon-over-object system
    // exists, only the hover highlight + hand cursor), the three UI/inventory/text SIZE sliders
    // (every panel in this game is drawn in fixed DesignWidth/DesignHeight pixels - real per-element
    // scaling would be a much larger rendering change than this tab warrants) and the hidden-
    // servers/cross-play/user-stats items (Barotrauma's own network/telemetry concepts, nothing
    // here to gate).
    private string _stagedLanguage = "ru";
    private bool _stagedTooltipsEnabled;
    private bool _stagedChatBubblesEnabled;
    private bool _stagedEnemyHealthBarsEnabled;

    private const int SettingsPanelWidth = 1160;
    private const int SettingsPanelHeight = 540;
    private const int SettingsHeaderHeight = 40;
    private const int SettingsTabColumnWidth = 64;
    private const int SettingsTabButtonSize = 44;
    private const int SettingsContentX = SettingsTabColumnWidth + 20;
    private const int SettingsContentY = SettingsHeaderHeight + 20;
    // Every Audio-tab row (device pickers, sliders, checkboxes alike) sits on this same cadence -
    // dense on purpose, there are up to 9 rows in one column (the reference screenshot's own left
    // column) and only ~430px of panel height to fit them in. 46, not 38 - a stepper's own </>
    // buttons are 24px tall starting at +18, i.e. reaching all the way to +42; anything tighter than
    // that overlaps the next row's own label (direct user bug report, screenshot showed exactly this).
    private const int AudioRowHeight = 46;

    // Direct user request ("менюшку настроек красивой, стильной, на основе Baротравмы... несколько
    // цветов") - the whole screen's own palette. A cold blue-slate metal panel (not the flat single-
    // tone green-grey every widget used to share) with three distinct accents doing three distinct
    // jobs, the same way Baротравма's own UI never reuses one colour for unrelated meanings: warm
    // gold for "selected/important" (the active tab, Apply), cooler teal for "live/on" (slider
    // fills, a checked box), muted red for the one walk-away action (Отмена).
    private static readonly Color SettingsPanelTop = new(27, 33, 45);
    private static readonly Color SettingsPanelBottom = new(13, 17, 24);
    private static readonly Color SettingsHeaderTop = new(40, 50, 64);
    private static readonly Color SettingsHeaderBottom = new(22, 28, 38);
    private static readonly Color SettingsBorderSteel = new(78, 88, 102);
    private static readonly Color SettingsBorderGold = new(198, 162, 88);
    private static readonly Color SettingsAccentTeal = new(94, 214, 196);
    private static readonly Color SettingsAccentGold = new(224, 178, 84);
    private static readonly Color SettingsAccentRed = new(196, 92, 78);
    private static readonly Color SettingsAccentGreen = new(120, 196, 128);
    private static readonly Color SettingsTextPrimary = new(226, 222, 208);
    private static readonly Color SettingsTextDim = new(146, 154, 164);

    // Per-widget hover "ease" toward lit (1) or cold (0) - keyed by a string unique to the widget
    // (its own label plus its own screen rect, so the two same-labelled "Обновить" buttons in the
    // Audio tab never share one animation state). Stepped a fixed amount per DRAWN frame rather
    // than tied to real elapsed time - at the game's own frame rate that reads as a smooth ~100ms
    // brighten/dim without threading a delta-seconds parameter through every draw helper in this
    // file just for one cosmetic transition ("плавные переходы", direct user request).
    private readonly Dictionary<string, float> _settingsHover = new();

    private float SettingsHoverEase(string key, bool hovering)
    {
        var current = _settingsHover.TryGetValue(key, out var v) ? v : 0f;
        var target = hovering ? 1f : 0f;
        current = MathHelper.Clamp(current + MathHelper.Clamp(target - current, -0.22f, 0.22f), 0f, 1f);
        _settingsHover[key] = current;
        return current;
    }

    // Direct user request ("сделай менюшку настроек ещё качественнее, добавь плавные переходы") -
    // two more of the same "stepped a fixed amount per DRAWN frame" easing SettingsHoverEase already
    // uses above, just for whole-screen moments instead of one widget: the panel sliding/fading into
    // place the moment the screen is entered, and the content area doing the same in miniature every
    // time the player switches tabs (so a click doesn't just instantly swap one wall of widgets for
    // another). _settingsPanelOpenEase is reset in EnterSettingsScreen (both real entry points - the
    // main menu button and the pause menu's "Настройки" - funnel through it) but deliberately saved
    // and restored around ApplySettings' own internal re-stage call, the same way it already saves/
    // restores _settingsTab - "Применить" re-syncs staged fields without re-playing the opening
    // animation, since the panel was never actually closed.
    private float _settingsPanelOpenEase;
    private SettingsTab _settingsTabForTransition = SettingsTab.Graphics;
    private float _settingsTabSwitchEase = 1f;

    private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), 3f);

    private void DrawVerticalGradient(Rectangle rect, Color top, Color bottom, int bands = 10)
    {
        for (var i = 0; i < bands; i++)
        {
            var y = rect.Y + rect.Height * i / bands;
            var h = rect.Height / bands + 1;
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X, y, rect.Width, h), Color.Lerp(top, bottom, i / (float)Math.Max(1, bands - 1)));
        }
    }

    // The one bevel every raised (button) or recessed (slider track/checkbox) surface in this
    // screen shares - a single light/dark pixel line per edge is enough to read as a physically lit
    // edge instead of a flat sticker, the same trick DeviceSkin's own Housing() bevel already uses
    // on in-world device faces.
    private void DrawBevel(Rectangle rect, bool raised, float strength = 1f)
    {
        var light = Color.White * (0.30f * strength);
        var dark = Color.Black * (0.5f * strength);
        var top = raised ? light : dark;
        var bottom = raised ? dark : light;
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), top);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), top);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), bottom);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), bottom);
    }

    private static Vector2 SettingsPanelOrigin => new((DesignWidth - SettingsPanelWidth) / 2f, (DesignHeight - SettingsPanelHeight) / 2f);

    private void EnterSettingsScreen()
    {
        _stagedResolutionIndex = FindResolutionIndex(_graphicsSettings.ResolutionWidth, _graphicsSettings.ResolutionHeight);
        _stagedWindowMode = _graphicsSettings.WindowMode;
        _stagedVSync = _graphicsSettings.VSync;
        _stagedBloomStrength = _graphicsSettings.BloomStrength;
        _stagedMaxParticles = _graphicsSettings.MaxParticles;
        _stagedShadersEnabled = _graphicsSettings.ShadersEnabled;

        _stagedSoundVolume = _graphicsSettings.SoundVolume;
        _stagedMusicVolume = _graphicsSettings.MusicVolume;
        _stagedUiVolume = _graphicsSettings.UiVolume;
        _stagedVoiceChatVolume = _graphicsSettings.VoiceChatVolume;
        _stagedMuteOnFocusLoss = _graphicsSettings.MuteOnFocusLoss;
        _stagedDynamicRangeCompression = _graphicsSettings.DynamicRangeCompression;
        _stagedDirectionalVoiceChat = _graphicsSettings.DirectionalVoiceChat;
        _stagedVoiceChatPriority = _graphicsSettings.VoiceChatPriority;
        _stagedVoiceActivationEnabled = _graphicsSettings.VoiceActivationEnabled;
        _stagedVoiceActivationThreshold = _graphicsSettings.VoiceActivationThreshold;
        _stagedMicGain = _graphicsSettings.MicGain;
        _stagedDisconnectPreventionMs = _graphicsSettings.DisconnectPreventionMs;
        _stagedOutputDeviceId = _graphicsSettings.OutputDeviceId;
        _stagedInputMicrophoneName = _graphicsSettings.InputMicrophoneName;
        RefreshOutputDevices();
        RefreshInputDevices();

        _stagedKeyBindings = _keyBindings.Clone();
        _awaitingRebindAction = null;

        _stagedLanguage = _graphicsSettings.Language;
        _stagedTooltipsEnabled = _graphicsSettings.TooltipsEnabled;
        _stagedChatBubblesEnabled = _graphicsSettings.ChatBubblesEnabled;
        _stagedEnemyHealthBarsEnabled = _graphicsSettings.EnemyHealthBarsEnabled;

        _settingsTab = SettingsTab.Graphics;
        _achievementsScrollOffset = 0f;
        _achievementsThumbDragLastMouseY = null;
        _settingsPanelOpenEase = 0f;
        _settingsTabForTransition = SettingsTab.Graphics;
        _settingsTabSwitchEase = 1f;
        _menuScreen = MenuScreen.Settings;
    }

    private void RefreshOutputDevices()
    {
        _stagedOutputDevices = _audioEngine.EnumerateOutputDevices();
        var found = _stagedOutputDeviceId is null ? -1 : IndexOf(_stagedOutputDevices, _stagedOutputDeviceId);
        _stagedOutputDeviceIndex = found + 1; // 0 reserved for "system default"
    }

    private void RefreshInputDevices()
    {
        // Direct user report ("не могу найти микрофон наушников") - now the same WASAPI enumeration
        // GameAudioEngine already uses for output, not MonoGame's own (much more limited) Microphone
        // class - see VoiceCapture's own doc comment for why that one missed real devices.
        _stagedInputDevices = _audioEngine.EnumerateInputDevices().Select(d => d.Name).ToList();
        var found = _stagedInputMicrophoneName is null ? -1 : _stagedInputDevices.ToList().IndexOf(_stagedInputMicrophoneName);
        _stagedInputDeviceIndex = found + 1;
    }

    private static int IndexOf(IReadOnlyList<(string Id, string Name)> devices, string id)
    {
        for (var i = 0; i < devices.Count; i++)
            if (devices[i].Id == id)
                return i;
        return -1;
    }

    private void CycleOutputDevice(int direction)
    {
        var count = _stagedOutputDevices.Count + 1;
        _stagedOutputDeviceIndex = (_stagedOutputDeviceIndex + direction + count) % count;
        _stagedOutputDeviceId = _stagedOutputDeviceIndex == 0 ? null : _stagedOutputDevices[_stagedOutputDeviceIndex - 1].Id;
    }

    private void CycleInputDevice(int direction)
    {
        var count = _stagedInputDevices.Count + 1;
        _stagedInputDeviceIndex = (_stagedInputDeviceIndex + direction + count) % count;
        _stagedInputMicrophoneName = _stagedInputDeviceIndex == 0 ? null : _stagedInputDevices[_stagedInputDeviceIndex - 1];
    }

    private string OutputDeviceLabel() => _stagedOutputDeviceIndex == 0 ? "Системное по умолчанию" : _stagedOutputDevices[_stagedOutputDeviceIndex - 1].Name;
    private string InputDeviceLabel() => _stagedInputDeviceIndex == 0 ? "Системное по умолчанию" : _stagedInputDevices[_stagedInputDeviceIndex - 1];

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
    // Direct user request - "отключить все шейдеры и эффекты" for a machine that struggles with
    // the custom-shader path (room lighting's per-pixel glow, the whole post-processing chain, the
    // main menu's planet). Sits right under VSync, same left column.
    private static Rectangle GetShadersEnabledCheckboxRect(Vector2 panelOrigin) => new((int)SettingsContentOrigin(panelOrigin).X, (int)SettingsContentOrigin(panelOrigin).Y + 168, 20, 20);

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

    // ---- Audio tab rect helpers - left column ("УСТРОЙСТВО ВЫВОДА"), row index 0-8 ----
    private static Vector2 AudioLeftRow(Vector2 origin, int row) => new(origin.X, origin.Y + row * AudioRowHeight);
    // A real device name ("Наушники гарнитуры (Jabra EVOLVE 20 MS)") is much wider than
    // Resolution/WindowMode's own short values - direct user bug report, screenshot showed the name
    // running straight into the ">" button - so this one stepper gets a much wider gap than the
    // generic 220px every other one uses (DrawLabeledStepper's own auto-shrink is still there as a
    // backstop for anything longer still).
    private static Rectangle GetOutputDevicePrevRect(Vector2 panelOrigin) { var p = AudioLeftRow(SettingsContentOrigin(panelOrigin), 0); return new Rectangle((int)p.X, (int)p.Y + 18, 24, 24); }
    private static Rectangle GetOutputDeviceNextRect(Vector2 panelOrigin) { var p = AudioLeftRow(SettingsContentOrigin(panelOrigin), 0); return new Rectangle((int)p.X + 340, (int)p.Y + 18, 24, 24); }
    private static Rectangle GetOutputDeviceRefreshRect(Vector2 panelOrigin) { var p = AudioLeftRow(SettingsContentOrigin(panelOrigin), 0); return new Rectangle((int)p.X + 374, (int)p.Y + 18, 130, 24); }
    private static Rectangle GetSoundVolumeSliderRect(Vector2 panelOrigin) { var p = AudioLeftRow(SettingsContentOrigin(panelOrigin), 1); return new Rectangle((int)p.X, (int)p.Y + 18, 320, 10); }
    private static Rectangle GetMusicVolumeSliderRect(Vector2 panelOrigin) { var p = AudioLeftRow(SettingsContentOrigin(panelOrigin), 2); return new Rectangle((int)p.X, (int)p.Y + 18, 320, 10); }
    private static Rectangle GetUiVolumeSliderRect(Vector2 panelOrigin) { var p = AudioLeftRow(SettingsContentOrigin(panelOrigin), 3); return new Rectangle((int)p.X, (int)p.Y + 18, 320, 10); }
    private static Rectangle GetVoiceChatVolumeSliderRect(Vector2 panelOrigin) { var p = AudioLeftRow(SettingsContentOrigin(panelOrigin), 4); return new Rectangle((int)p.X, (int)p.Y + 18, 320, 10); }
    private static Rectangle GetMuteOnFocusLossCheckboxRect(Vector2 panelOrigin) { var p = AudioLeftRow(SettingsContentOrigin(panelOrigin), 5); return new Rectangle((int)p.X, (int)p.Y + 8, 20, 20); }
    private static Rectangle GetDrcCheckboxRect(Vector2 panelOrigin) { var p = AudioLeftRow(SettingsContentOrigin(panelOrigin), 6); return new Rectangle((int)p.X, (int)p.Y + 8, 20, 20); }
    private static Rectangle GetDirectionalVoiceCheckboxRect(Vector2 panelOrigin) { var p = AudioLeftRow(SettingsContentOrigin(panelOrigin), 7); return new Rectangle((int)p.X, (int)p.Y + 8, 20, 20); }
    private static Rectangle GetVoicePriorityCheckboxRect(Vector2 panelOrigin) { var p = AudioLeftRow(SettingsContentOrigin(panelOrigin), 8); return new Rectangle((int)p.X, (int)p.Y + 8, 20, 20); }

    // ---- Audio tab rect helpers - right column ("УСТРОЙСТВО ВВОДА"), row index 0-4 ----
    private static Vector2 AudioRightRow(Vector2 origin, int row) => new(SettingsRightColumnX(origin), origin.Y + row * AudioRowHeight);
    // Same widened gap as the output-device row - microphone names run just as long
    // ("Микрофон гарнитуры (Jabra EVOLVE 20 MS)").
    private static Rectangle GetInputDevicePrevRect(Vector2 panelOrigin) { var p = AudioRightRow(SettingsContentOrigin(panelOrigin), 0); return new Rectangle((int)p.X, (int)p.Y + 18, 24, 24); }
    private static Rectangle GetInputDeviceNextRect(Vector2 panelOrigin) { var p = AudioRightRow(SettingsContentOrigin(panelOrigin), 0); return new Rectangle((int)p.X + 300, (int)p.Y + 18, 24, 24); }
    private static Rectangle GetInputDeviceRefreshRect(Vector2 panelOrigin) { var p = AudioRightRow(SettingsContentOrigin(panelOrigin), 0); return new Rectangle((int)p.X + 334, (int)p.Y + 18, 130, 24); }
    private static Rectangle GetInputModePrevRect(Vector2 panelOrigin) { var p = AudioRightRow(SettingsContentOrigin(panelOrigin), 1); return new Rectangle((int)p.X, (int)p.Y + 18, 24, 24); }
    private static Rectangle GetInputModeNextRect(Vector2 panelOrigin) { var p = AudioRightRow(SettingsContentOrigin(panelOrigin), 1); return new Rectangle((int)p.X + 200, (int)p.Y + 18, 24, 24); }
    private static Rectangle GetNoiseThresholdSliderRect(Vector2 panelOrigin) { var p = AudioRightRow(SettingsContentOrigin(panelOrigin), 2); return new Rectangle((int)p.X, (int)p.Y + 18, 300, 10); }
    private static Rectangle GetMicGainSliderRect(Vector2 panelOrigin) { var p = AudioRightRow(SettingsContentOrigin(panelOrigin), 3); return new Rectangle((int)p.X, (int)p.Y + 18, 300, 10); }
    private static Rectangle GetDisconnectPreventionSliderRect(Vector2 panelOrigin) { var p = AudioRightRow(SettingsContentOrigin(panelOrigin), 4); return new Rectangle((int)p.X, (int)p.Y + 18, 300, 10); }

    // ---- Controls tab rect helpers - two 6-row columns (12 rebindable actions total) ----
    private const int ControlsRowHeight = 34;
    private static readonly PlayerAction[] ControlsLeftColumn =
    {
        PlayerAction.MoveUp, PlayerAction.MoveDown, PlayerAction.MoveLeft, PlayerAction.MoveRight,
        PlayerAction.Interact, PlayerAction.Fire,
    };
    private static readonly PlayerAction[] ControlsRightColumn =
    {
        PlayerAction.AutopilotStop, PlayerAction.ToggleLanding,
        PlayerAction.VoicePushToTalk, PlayerAction.RadioPushToTalk, PlayerAction.ToggleGalacticMap, PlayerAction.OpenChat,
    };

    private static Rectangle GetControlsKeyRect(PlayerAction action, Vector2 panelOrigin)
    {
        var content = SettingsContentOrigin(panelOrigin);
        var leftIndex = Array.IndexOf(ControlsLeftColumn, action);
        if (leftIndex >= 0)
            return new Rectangle((int)content.X + 270, (int)content.Y + leftIndex * ControlsRowHeight, 100, 26);
        var rightIndex = Array.IndexOf(ControlsRightColumn, action);
        return new Rectangle((int)SettingsRightColumnX(content) + 270, (int)content.Y + rightIndex * ControlsRowHeight, 100, 26);
    }

    private static Rectangle GetControlsResetButtonRect(Vector2 panelOrigin)
    {
        var content = SettingsContentOrigin(panelOrigin);
        return new Rectangle((int)content.X, (int)content.Y + ControlsLeftColumn.Length * ControlsRowHeight + 14, 220, 32);
    }

    // ---- Interface tab rect helpers ----
    private static Rectangle GetLanguagePrevRect(Vector2 panelOrigin) => new((int)SettingsContentOrigin(panelOrigin).X + 100, (int)SettingsContentOrigin(panelOrigin).Y + 108, 24, 24);
    private static Rectangle GetLanguageNextRect(Vector2 panelOrigin) => new((int)SettingsContentOrigin(panelOrigin).X + 320, (int)SettingsContentOrigin(panelOrigin).Y + 108, 24, 24);
    private static Rectangle GetTooltipsCheckboxRect(Vector2 panelOrigin) => new((int)SettingsContentOrigin(panelOrigin).X, (int)SettingsContentOrigin(panelOrigin).Y + 158, 20, 20);
    private static Rectangle GetChatBubblesCheckboxRect(Vector2 panelOrigin) => new((int)SettingsContentOrigin(panelOrigin).X, (int)SettingsContentOrigin(panelOrigin).Y + 198, 20, 20);
    private static Rectangle GetEnemyHealthBarsCheckboxRect(Vector2 panelOrigin) => new((int)SettingsContentOrigin(panelOrigin).X, (int)SettingsContentOrigin(panelOrigin).Y + 238, 20, 20);

    private void HandleSettingsScreen(KeyboardState keyboard)
    {
        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        var held = mouse.LeftButton == ButtonState.Pressed;
        var point = _designMouse;
        var origin = SettingsPanelOrigin;

        // A pending Controls-tab rebind capture (direct user request - click a binding, press a
        // key) takes over the keyboard entirely until it resolves: the very first physical key seen
        // becomes the new binding and capture ends immediately (no held-key/edge-triggering needed -
        // there's nothing left to re-trigger once _awaitingRebindAction goes back to null). Escape
        // is excluded - handled one layer up (Game1.cs/Game1.Menu.cs's own LeaveSubScreen) as
        // "cancel the capture" rather than a bindable key, so it never reaches here at all.
        if (_awaitingRebindAction is { } captureAction)
        {
            foreach (var pressedKey in keyboard.GetPressedKeys())
            {
                _stagedKeyBindings.Set(captureAction, pressedKey);
                _awaitingRebindAction = null;
                break;
            }
        }

        if (clicked)
        {
            // Direct user request ("звук как при нажатии на кнопки в баротравме") - one call covers
            // every tab/button/checkbox/stepper below (this screen is reached both from the main
            // menu and, unchanged, from the pause menu's "Настройки" - "вкладки меню" and "вкладки
            // escape" are the exact same code path), rather than repeating it at each of the many
            // branches individually.
            PlayUiClick();

            for (var i = 0; i < SettingsTabs.Length; i++)
                if (GetSettingsTabRect(i, origin).Contains(point))
                    _settingsTab = SettingsTabs[i].Tab;

            if (GetSettingsCancelButtonRect(origin).Contains(point))
            {
                // Reached from the pause menu (_inGameSettingsOpen), "Отмена" steps back to it -
                // there is no MenuScreen.Main to fall back to while a session is running, and
                // _sessionStarted staying true means nothing would draw it anyway.
                if (_inGameSettingsOpen)
                    _inGameSettingsOpen = false;
                else
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
                else if (GetShadersEnabledCheckboxRect(origin).Contains(point))
                    _stagedShadersEnabled = !_stagedShadersEnabled;
            }
            else if (_settingsTab == SettingsTab.Audio)
            {
                if (GetOutputDevicePrevRect(origin).Contains(point))
                    CycleOutputDevice(-1);
                else if (GetOutputDeviceNextRect(origin).Contains(point))
                    CycleOutputDevice(1);
                else if (GetOutputDeviceRefreshRect(origin).Contains(point))
                    RefreshOutputDevices();
                else if (GetMuteOnFocusLossCheckboxRect(origin).Contains(point))
                    _stagedMuteOnFocusLoss = !_stagedMuteOnFocusLoss;
                else if (GetDrcCheckboxRect(origin).Contains(point))
                    _stagedDynamicRangeCompression = !_stagedDynamicRangeCompression;
                else if (GetDirectionalVoiceCheckboxRect(origin).Contains(point))
                    _stagedDirectionalVoiceChat = !_stagedDirectionalVoiceChat;
                else if (GetVoicePriorityCheckboxRect(origin).Contains(point))
                    _stagedVoiceChatPriority = !_stagedVoiceChatPriority;
                else if (GetInputDevicePrevRect(origin).Contains(point))
                    CycleInputDevice(-1);
                else if (GetInputDeviceNextRect(origin).Contains(point))
                    CycleInputDevice(1);
                else if (GetInputDeviceRefreshRect(origin).Contains(point))
                    RefreshInputDevices();
                else if (GetInputModePrevRect(origin).Contains(point) || GetInputModeNextRect(origin).Contains(point))
                    _stagedVoiceActivationEnabled = !_stagedVoiceActivationEnabled;
            }
            else if (_settingsTab == SettingsTab.Controls)
            {
                if (GetControlsResetButtonRect(origin).Contains(point))
                {
                    _stagedKeyBindings.ResetAll();
                    _awaitingRebindAction = null;
                }
                else
                {
                    PlayerAction? clickedAction = null;
                    foreach (var action in PlayerActionBindings.All)
                        if (GetControlsKeyRect(action, origin).Contains(point))
                        {
                            clickedAction = action;
                            break;
                        }
                    if (clickedAction is { } action2)
                        // Clicking the row already awaiting its own key cancels capture (a toggle);
                        // clicking a different row switches capture to it instead.
                        _awaitingRebindAction = _awaitingRebindAction == action2 ? null : action2;
                }
            }
            // Nickname/Role are pre-session-only sub-screens (Game1.Prologue.cs) with nothing to
            // send the running session's own already-joined character - reached from the pause menu,
            // "Изменить" would just be a dead click (nothing draws MenuScreen.Nickname/Role while
            // _sessionStarted stays true), so it's a no-op there rather than a misleading one. The
            // language/tooltip/bubble/health-bar controls below aren't gated by that - they apply
            // just as well mid-session as before one starts.
            else if (_settingsTab == SettingsTab.Interface)
            {
                if (!_inGameSettingsOpen && GetChangeNicknameButtonRect(origin).Contains(point))
                    _menuScreen = MenuScreen.Nickname;
                else if (!_inGameSettingsOpen && GetChangeRoleButtonRect(origin).Contains(point))
                    _menuScreen = MenuScreen.Role;
                else if (GetLanguagePrevRect(origin).Contains(point) || GetLanguageNextRect(origin).Contains(point))
                    _stagedLanguage = _stagedLanguage == "ru" ? "en" : "ru";
                else if (GetTooltipsCheckboxRect(origin).Contains(point))
                    _stagedTooltipsEnabled = !_stagedTooltipsEnabled;
                else if (GetChatBubblesCheckboxRect(origin).Contains(point))
                    _stagedChatBubblesEnabled = !_stagedChatBubblesEnabled;
                else if (GetEnemyHealthBarsCheckboxRect(origin).Contains(point))
                    _stagedEnemyHealthBarsEnabled = !_stagedEnemyHealthBarsEnabled;
            }
            else if (_settingsTab == SettingsTab.Misc)
            {
                if (GetResetSettingsButtonRect(origin).Contains(point))
                {
                    var defaults = new GraphicsSettings(null, null, WindowMode.Borderless, true, 1f, 1f, 400, false, 0.12f);
                    _stagedResolutionIndex = FindResolutionIndex(null, null);
                    _stagedWindowMode = defaults.WindowMode;
                    _stagedVSync = defaults.VSync;
                    _stagedBloomStrength = defaults.BloomStrength;
                    _stagedMaxParticles = defaults.MaxParticles;
                    _stagedSoundVolume = defaults.SoundVolume;
                    _stagedMusicVolume = defaults.MusicVolume;
                    _stagedUiVolume = defaults.UiVolume;
                    _stagedVoiceChatVolume = defaults.VoiceChatVolume;
                    _stagedMuteOnFocusLoss = defaults.MuteOnFocusLoss;
                    _stagedDynamicRangeCompression = defaults.DynamicRangeCompression;
                    _stagedDirectionalVoiceChat = defaults.DirectionalVoiceChat;
                    _stagedVoiceChatPriority = defaults.VoiceChatPriority;
                    _stagedVoiceActivationEnabled = defaults.VoiceActivationEnabled;
                    _stagedVoiceActivationThreshold = defaults.VoiceActivationThreshold;
                    _stagedMicGain = defaults.MicGain;
                    _stagedDisconnectPreventionMs = defaults.DisconnectPreventionMs;
                    _stagedOutputDeviceId = defaults.OutputDeviceId;
                    _stagedInputMicrophoneName = defaults.InputMicrophoneName;
                    _stagedShadersEnabled = defaults.ShadersEnabled;
                    _stagedKeyBindings.ResetAll();
                    _stagedLanguage = defaults.Language;
                    _stagedTooltipsEnabled = defaults.TooltipsEnabled;
                    _stagedChatBubblesEnabled = defaults.ChatBubblesEnabled;
                    _stagedEnemyHealthBarsEnabled = defaults.EnemyHealthBarsEnabled;
                    RefreshOutputDevices();
                    RefreshInputDevices();
                }
                // Same "no pre-session screen to show it on" reasoning as Nickname/Role above.
                else if (GetOpenCreditsButtonRect(origin).Contains(point) && !_inGameSettingsOpen)
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
                _stagedSoundVolume = TrySliderValue(GetSoundVolumeSliderRect(origin), point, 0f, 1f) ?? _stagedSoundVolume;
                _stagedMusicVolume = TrySliderValue(GetMusicVolumeSliderRect(origin), point, 0f, 1f) ?? _stagedMusicVolume;
                _stagedUiVolume = TrySliderValue(GetUiVolumeSliderRect(origin), point, 0f, 1f) ?? _stagedUiVolume;
                _stagedVoiceChatVolume = TrySliderValue(GetVoiceChatVolumeSliderRect(origin), point, 0f, 2f) ?? _stagedVoiceChatVolume;
                _stagedVoiceActivationThreshold = TrySliderValue(GetNoiseThresholdSliderRect(origin), point, 0.01f, 0.5f) ?? _stagedVoiceActivationThreshold;
                _stagedMicGain = TrySliderValue(GetMicGainSliderRect(origin), point, 1f, 9f) ?? _stagedMicGain;
                _stagedDisconnectPreventionMs = TrySliderValue(GetDisconnectPreventionSliderRect(origin), point, 0f, 500f) ?? _stagedDisconnectPreventionMs;
            }
        }

        // Not gated by `held` above - the achievements list only ever scrolls (drag-a-thumb, same
        // convention ChangelogPanel already uses), and HandleAchievementsTabInput needs to see the
        // "button just let go" frame too (clears drag state), not just the frames it's held down.
        if (_settingsTab == SettingsTab.Achievements)
            HandleAchievementsTabInput(SettingsContentOrigin(origin), point, held, clicked);

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
        var settings = new GraphicsSettings(w, h, _stagedWindowMode, _stagedVSync, _stagedSoundVolume, _stagedBloomStrength, _stagedMaxParticles,
            _stagedVoiceActivationEnabled, _stagedVoiceActivationThreshold,
            _stagedOutputDeviceId, _stagedInputMicrophoneName,
            _stagedMusicVolume, _stagedUiVolume, _stagedVoiceChatVolume,
            _stagedMuteOnFocusLoss, _stagedDynamicRangeCompression,
            _stagedDirectionalVoiceChat, _stagedVoiceChatPriority,
            _stagedMicGain, _stagedDisconnectPreventionMs,
            _stagedShadersEnabled,
            _stagedKeyBindings.ToSettingsString(), _stagedLanguage,
            _stagedTooltipsEnabled, _stagedChatBubblesEnabled, _stagedEnemyHealthBarsEnabled);
        ApplyGraphicsSettings(settings);
        PlayerSettingsStore.SaveGraphicsSettings(settings);
        // Direct user request - "Применить" used to kick back to the main menu, same as "Отмена".
        // Stays on the Settings screen, on the SAME tab, now - EnterSettingsScreen re-stages every
        // field from what's now live (the settings just saved) but always resets to the Graphics
        // tab, so the tab the player was actually on is captured first and restored after.
        var tabBeforeApply = _settingsTab;
        var openEaseBeforeApply = _settingsPanelOpenEase;
        var tabSwitchEaseBeforeApply = _settingsTabSwitchEase;
        EnterSettingsScreen();
        _settingsTab = tabBeforeApply;
        _settingsTabForTransition = tabBeforeApply;
        _settingsPanelOpenEase = openEaseBeforeApply;
        _settingsTabSwitchEase = tabSwitchEaseBeforeApply;
    }

    private static Rectangle GetChangeNicknameButtonRect(Vector2 panelOrigin) =>
        new((int)SettingsContentOrigin(panelOrigin).X + 260, (int)SettingsContentOrigin(panelOrigin).Y + 4, 100, 28);
    private static Rectangle GetChangeRoleButtonRect(Vector2 panelOrigin) =>
        new((int)SettingsContentOrigin(panelOrigin).X + 260, (int)SettingsContentOrigin(panelOrigin).Y + 54, 100, 28);
    private static Rectangle GetResetSettingsButtonRect(Vector2 panelOrigin) =>
        new((int)SettingsContentOrigin(panelOrigin).X, (int)SettingsContentOrigin(panelOrigin).Y, 220, 32);
    private static Rectangle GetOpenCreditsButtonRect(Vector2 panelOrigin) =>
        new((int)SettingsContentOrigin(panelOrigin).X, (int)SettingsContentOrigin(panelOrigin).Y + 44, 220, 32);

    // Direct user request ("менюшку настроек красивой, стильной на основе Baротравмы... несколько
    // цветов, плавные переходы, красивые кнопки") - a from-scratch chrome pass over the whole
    // screen, kept entirely local to this file (not PanelFrame.cs itself) so no OTHER panel in the
    // game - InfoPanel, PauseMenuPanel, ConnectionsPanel... - changes look out from under it.
    // totalSeconds only drives the two purely cosmetic pulses below (active tab glow, a checked
    // box's own glow) - every hover transition instead uses SettingsHoverEase's own per-frame step,
    // so this is the only place in the whole screen that needs the game clock at all.
    private void DrawSettingsScreen(float totalSeconds)
    {
        // Panel entrance - eases 0->1 once per screen visit (EnterSettingsScreen resets it), a
        // gentle rise-and-settle rather than the screen just appearing fully formed. Detecting a
        // tab switch here (not in the click handler) keeps it a pure side effect of "the tab
        // actually changed since the last drawn frame", so it can never fire from HandleSettingsScreen
        // touching _settingsTab for an unrelated reason.
        _settingsPanelOpenEase = MathHelper.Clamp(_settingsPanelOpenEase + 0.09f, 0f, 1f);
        if (_settingsTab != _settingsTabForTransition)
        {
            _settingsTabForTransition = _settingsTab;
            _settingsTabSwitchEase = 0f;
        }
        _settingsTabSwitchEase = MathHelper.Clamp(_settingsTabSwitchEase + 0.16f, 0f, 1f);
        var openEase = EaseOutCubic(_settingsPanelOpenEase);
        var panelSlide = (1f - openEase) * 22f;

        var origin = SettingsPanelOrigin + new Vector2(0, panelSlide);
        var panelRect = new Rectangle((int)origin.X, (int)origin.Y, SettingsPanelWidth, SettingsPanelHeight);

        // A soft, layered drop shadow - the panel reads as floating a few pixels above the dimmed
        // background behind it instead of sitting flush on it, the same "elevation" cue a raised
        // button's own bevel gives at widget scale, just applied to the whole screen once.
        for (var i = 4; i >= 1; i--)
        {
            var spread = i * 3;
            var shadowRect = new Rectangle(panelRect.X - spread / 2, panelRect.Y - spread / 2 + spread, panelRect.Width + spread, panelRect.Height + spread);
            _spriteBatch.Draw(_pixel, shadowRect, Color.Black * (0.05f * openEase));
        }

        // The panel body: a cold blue-slate gradient (darker toward the bottom, like a bridge
        // console lit from above) instead of one flat fill, boxed by a steel outer line and a
        // thinner gold inset trim just inside it - the "frame within a frame" a riveted control
        // panel actually has, not just a single rectangle outline.
        DrawVerticalGradient(panelRect, SettingsPanelTop, SettingsPanelBottom);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, panelRect, SettingsBorderSteel, 2);
        var trimRect = new Rectangle(panelRect.X + 4, panelRect.Y + 4, panelRect.Width - 8, panelRect.Height - 8);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, trimRect, SettingsBorderGold * 0.55f, 1);
        ShipRenderer.DrawRivets(_spriteBatch, _pixel, panelRect);

        var headerRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, SettingsHeaderHeight);
        DrawVerticalGradient(headerRect, SettingsHeaderTop, SettingsHeaderBottom);
        // A slow breathing brightness on the header's own trim line - the same "this console is
        // live" cue the active tab's pulse already gives, echoed once at the top of the whole
        // screen so it doesn't read as the one static line on an otherwise animated panel.
        var headerPulse = 0.9f + 0.1f * MathF.Sin(totalSeconds * 1.3f);
        _spriteBatch.Draw(_pixel, new Rectangle(headerRect.X, headerRect.Bottom - 2, headerRect.Width, 2), SettingsBorderGold * headerPulse);
        _spriteBatch.Draw(_pixel, new Rectangle(headerRect.X, headerRect.Bottom, headerRect.Width, 3), Color.Black * 0.25f);
        // A soft drop-shadow copy of the title, offset one pixel down-right - reads as embossed
        // metal lettering rather than flat print, the cheap way every device label in this game
        // already gets its own legibility backing (DrawLabelBacking's own doc comment).
        _spriteBatch.DrawString(_font, "Настройки", origin + new Vector2(17, 11), Color.Black * 0.5f, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, "Настройки", origin + new Vector2(16, 10), SettingsAccentGold, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);

        // Tab column separator - a thin gold-tinted glow instead of a flat grey rule.
        _spriteBatch.Draw(_pixel, new Rectangle(panelRect.X + SettingsTabColumnWidth, headerRect.Bottom + 3, 1, panelRect.Height - SettingsHeaderHeight - 3), Color.Black * 0.4f);
        _spriteBatch.Draw(_pixel, new Rectangle(panelRect.X + SettingsTabColumnWidth + 1, headerRect.Bottom + 3, 1, panelRect.Height - SettingsHeaderHeight - 3), SettingsBorderGold * 0.35f);

        for (var i = 0; i < SettingsTabs.Length; i++)
        {
            var (tab, label) = SettingsTabs[i];
            var rect = GetSettingsTabRect(i, origin);
            var active = tab == _settingsTab;
            var hover = SettingsHoverEase($"tab:{label}", rect.Contains(_designMouse));
            if (active)
            {
                // A slow, gentle breathing glow - the same "this is the live/selected one" cue a
                // lit instrument panel gives, not a static block of colour.
                var pulse = 0.85f + 0.15f * MathF.Sin(totalSeconds * 2.1f);
                DrawVerticalGradient(rect, Color.Lerp(SettingsAccentGold, Color.White, 0.15f * pulse), SettingsAccentGold * 0.75f);
                DrawBevel(rect, raised: true, strength: 1.1f);
                _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 3, rect.Height), Color.Lerp(SettingsAccentGold, Color.White, 0.4f));
            }
            else
            {
                var baseColor = Color.Lerp(new Color(30, 37, 46), new Color(48, 58, 70), hover * 0.6f);
                _spriteBatch.Draw(_pixel, rect, baseColor);
                DrawBevel(rect, raised: hover > 0.05f, strength: 0.6f + hover * 0.4f);
                if (hover > 0.01f)
                    ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, rect, SettingsBorderGold * (0.25f * hover), 1);
            }
            DrawSettingsTabGlyph(tab, new Vector2(rect.Center.X, rect.Center.Y - 4f),
                active ? new Color(30, 24, 10) : Color.Lerp(SettingsTextDim, Color.White, hover * 0.6f));
        }

        // Tab-switch transition - the content area slides in from the right and settles, instead of
        // one tab's whole wall of widgets instantly replacing another's. Reuses the same eased
        // offset trick the panel-open animation above already uses, just on the horizontal axis and
        // over a shorter throw (this fires far more often - a snappier ease reads as responsive
        // rather than sluggish on every single tab click).
        var tabSwitchEase = EaseOutCubic(_settingsTabSwitchEase);
        var contentSlide = (1f - tabSwitchEase) * 26f;
        var content = SettingsContentOrigin(origin) + new Vector2(contentSlide, 0);
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
            case SettingsTab.Achievements:
                DrawAchievementsTab(content);
                break;
        }

        // Masks the tab-switch slide's own sharp start with a quick fade rather than letting the
        // new tab's widgets pop in fully opaque mid-slide - covers only the content area (not the
        // tab column/header/footer either side of it, which never moved).
        if (tabSwitchEase < 1f)
        {
            var contentFadeRect = new Rectangle(panelRect.X + SettingsTabColumnWidth + 2, headerRect.Bottom + 4,
                panelRect.Width - SettingsTabColumnWidth - 2, panelRect.Height - SettingsHeaderHeight - 60);
            _spriteBatch.Draw(_pixel, contentFadeRect, SettingsPanelBottom * ((1f - tabSwitchEase) * 0.85f));
        }

        // Footer separator, then the two exit actions - colour-coded by what they actually do
        // (Baротравма never paints a "leave without saving" button the same colour as "confirm"):
        // a cool, muted red for stepping back out, a lit green-gold for committing the change.
        var footerY = panelRect.Bottom - 56;
        _spriteBatch.Draw(_pixel, new Rectangle(panelRect.X + 16, footerY, panelRect.Width - 32, 1), Color.Black * 0.4f);
        _spriteBatch.Draw(_pixel, new Rectangle(panelRect.X + 16, footerY + 1, panelRect.Width - 32, 1), SettingsBorderGold * 0.2f);

        var cancelRect = GetSettingsCancelButtonRect(origin);
        var applyRect = GetSettingsApplyButtonRect(origin);
        DrawActionButton(cancelRect, "ОТМЕНА", SettingsAccentRed);
        DrawActionButton(applyRect, "ПРИМЕНИТЬ", SettingsAccentGreen);

        // The panel-open fade itself - drawn dead last so it sits over every widget above (tabs,
        // content, footer buttons alike) while the screen is still settling into place, then
        // disappears entirely (openEase reaches 1) rather than lingering as a permanent tint.
        if (openEase < 1f)
            _spriteBatch.Draw(_pixel, panelRect, Color.Black * ((1f - openEase) * 0.9f));
    }

    // The two footer actions share this instead of DrawSmallButton below - each gets its own accent
    // colour baked into the gradient/glow rather than the generic steel-grey every ordinary button
    // in this screen uses, so "commit" and "walk away" read apart from across the room, not just by
    // their label text.
    private void DrawActionButton(Rectangle rect, string label, Color accent)
    {
        var hover = SettingsHoverEase($"action:{label}", rect.Contains(_designMouse));
        var top = Color.Lerp(new Color(34, 38, 44), Color.Lerp(accent, Color.White, 0.1f), 0.35f + hover * 0.4f);
        var bottom = Color.Lerp(new Color(18, 20, 24), accent * 0.55f, 0.35f + hover * 0.35f);
        DrawVerticalGradient(rect, top, bottom, 6);
        DrawBevel(rect, raised: true, strength: 0.8f + hover * 0.5f);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, rect, Color.Lerp(SettingsBorderSteel, accent, 0.4f + hover * 0.5f), 1);
        DrawCenteredLabel(label, rect, Color.Lerp(SettingsTextPrimary, Color.White, hover * 0.5f));
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
            case SettingsTab.Achievements: // a small star/badge
                for (var i = 0; i < 5; i++)
                {
                    var angle = -MathHelper.PiOver2 + i * MathHelper.TwoPi / 5f;
                    var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 9f;
                    _spriteBatch.Draw(_pixel, new Rectangle((int)point.X - 1, (int)point.Y - 1, 3, 3), color);
                }
                HudIcons.FillCircle(_spriteBatch, _pixel, center, 4f, color);
                break;
        }
    }

    private void DrawGraphicsTab(Vector2 content)
    {
        var (w, h) = ResolutionOptions[_stagedResolutionIndex];
        DrawLabeledStepper(content, "РАЗРЕШЕНИЕ", $"{w}x{h}", GetResolutionPrevRect(SettingsPanelOrigin), GetResolutionNextRect(SettingsPanelOrigin));
        DrawLabeledStepper(content + new Vector2(0, 60), "РЕЖИМ ЭКРАНА", WindowModeLabel(_stagedWindowMode),
            GetWindowModePrevRect(SettingsPanelOrigin), GetWindowModeNextRect(SettingsPanelOrigin));

        DrawCheckboxRow(GetVSyncCheckboxRect(SettingsPanelOrigin), "Вертикальная синхронизация", _stagedVSync);
        DrawCheckboxRow(GetShadersEnabledCheckboxRect(SettingsPanelOrigin), "Отключить шейдеры и эффекты", !_stagedShadersEnabled);

        var right = new Vector2(SettingsRightColumnX(content), content.Y);
        DrawLabeledSlider(right, "СВЕЧЕНИЕ (BLOOM)", $"{_stagedBloomStrength * 50f:0}%", GetBloomSliderRect(SettingsPanelOrigin), _stagedBloomStrength / 2f);
        DrawLabeledSlider(right + new Vector2(0, 60), "МАКС. КОЛИЧЕСТВО ЧАСТИЦ", $"{_stagedMaxParticles}", GetParticlesSliderRect(SettingsPanelOrigin), _stagedMaxParticles / 1000f);
    }

    private void DrawCheckboxRow(Rectangle box, string label, bool @checked)
    {
        var hover = SettingsHoverEase($"chk:{label}", box.Contains(_designMouse));
        _spriteBatch.Draw(_pixel, box, Color.Lerp(new Color(15, 19, 25), new Color(24, 30, 38), hover));
        // Recessed, not raised - a checkbox is a socket something drops into, the opposite bevel
        // direction from a pressable button.
        DrawBevel(box, raised: false, strength: 0.85f);
        if (@checked)
        {
            var inset = new Rectangle(box.X + 3, box.Y + 3, box.Width - 6, box.Height - 6);
            DrawVerticalGradient(inset, Color.Lerp(SettingsAccentTeal, Color.White, 0.3f), SettingsAccentTeal * 0.7f, 4);
            _spriteBatch.Draw(_pixel, new Rectangle(inset.X, inset.Y, inset.Width, 1), Color.White * 0.45f);
        }
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, box,
            Color.Lerp(SettingsBorderSteel, SettingsAccentTeal, (@checked ? 0.55f : 0f) + hover * 0.3f), 1);
        _spriteBatch.DrawString(_font, label, new Vector2(box.Right + 10, box.Y - 1),
            Color.Lerp(@checked ? SettingsTextPrimary : SettingsTextDim, Color.White, hover * 0.5f), 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    // Direct user request - the whole "Звук" tab redesigned close to Barotrauma's own two-column
    // "УСТРОЙСТВО ВЫВОДА"/"УСТРОЙСТВО ВВОДА" screen. Left column drives GameAudioEngine's output device
    // + bus volumes + the whole-mix options; right column drives VoiceCapture's input device + mic
    // options - ApplyGraphicsSettings (Game1.cs) is what actually pushes every one of these into the
    // live objects once "Применить" is pressed, the same "staged here, applied there" split every
    // other tab already uses.
    private void DrawAudioTab(Vector2 content)
    {
        var panelOrigin = SettingsPanelOrigin;

        DrawLabeledStepper(AudioLeftRow(content, 0), "УСТРОЙСТВО ВЫВОДА", OutputDeviceLabel(),
            GetOutputDevicePrevRect(panelOrigin), GetOutputDeviceNextRect(panelOrigin));
        DrawSmallButton(GetOutputDeviceRefreshRect(panelOrigin), "Обновить");

        DrawLabeledSlider(AudioLeftRow(content, 1), "ГРОМКОСТЬ ЗВУКОВ", $"{_stagedSoundVolume * 100f:0}%", GetSoundVolumeSliderRect(panelOrigin), _stagedSoundVolume);
        DrawLabeledSlider(AudioLeftRow(content, 2), "ГРОМКОСТЬ МУЗЫКИ", $"{_stagedMusicVolume * 100f:0}%", GetMusicVolumeSliderRect(panelOrigin), _stagedMusicVolume);
        DrawLabeledSlider(AudioLeftRow(content, 3), "ГРОМКОСТЬ ЗВУКОВ ИНТЕРФЕЙСА", $"{_stagedUiVolume * 100f:0}%", GetUiVolumeSliderRect(panelOrigin), _stagedUiVolume);
        DrawLabeledSlider(AudioLeftRow(content, 4), "ГРОМКОСТЬ ГОЛОСОВОГО ЧАТА", $"{_stagedVoiceChatVolume * 100f:0}%", GetVoiceChatVolumeSliderRect(panelOrigin), _stagedVoiceChatVolume / 2f);

        DrawCheckboxRow(GetMuteOnFocusLossCheckboxRect(panelOrigin), "Заглушить при переключении окна", _stagedMuteOnFocusLoss);
        DrawCheckboxRow(GetDrcCheckboxRect(panelOrigin), "Компрессия динамического диапазона", _stagedDynamicRangeCompression);
        DrawCheckboxRow(GetDirectionalVoiceCheckboxRect(panelOrigin), "Направленный голосовой чат", _stagedDirectionalVoiceChat);
        DrawCheckboxRow(GetVoicePriorityCheckboxRect(panelOrigin), "Приоритет гол. чата", _stagedVoiceChatPriority);

        DrawLabeledStepper(AudioRightRow(content, 0), "УСТРОЙСТВО ВВОДА", InputDeviceLabel(),
            GetInputDevicePrevRect(panelOrigin), GetInputDeviceNextRect(panelOrigin));
        DrawSmallButton(GetInputDeviceRefreshRect(panelOrigin), "Обновить");

        DrawLabeledStepper(AudioRightRow(content, 1), "РЕЖИМ ВВОДА", _stagedVoiceActivationEnabled ? "Активность речи" : "Push-to-talk",
            GetInputModePrevRect(panelOrigin), GetInputModeNextRect(panelOrigin));

        // Live input-level indicator (direct user request, matches the reference screenshot's own
        // running bar) drawn UNDER the normal threshold fill, in a distinct colour, so both "where is
        // the threshold set" and "how loud is the mic right now" read at once.
        var thresholdRect = GetNoiseThresholdSliderRect(panelOrigin);
        var level = Math.Clamp(_voiceCapture.CurrentLevel, 0f, 1f);
        var levelWidth = (int)(thresholdRect.Width * level);
        if (levelWidth > 0)
            _spriteBatch.Draw(_pixel, new Rectangle(thresholdRect.X, thresholdRect.Y - 3, levelWidth, thresholdRect.Height + 6), new Color(214, 140, 40) * 0.6f);
        DrawLabeledSlider(AudioRightRow(content, 2), "ПОРОГ ШУМОПОДАВЛЕНИЯ",
            $"{(_stagedVoiceActivationThreshold - 0.01f) / 0.49f * 100f:0}%",
            thresholdRect, (_stagedVoiceActivationThreshold - 0.01f) / 0.49f);

        DrawLabeledSlider(AudioRightRow(content, 3), "ГРОМКОСТЬ МИКРОФОНА", $"{_stagedMicGain * 100f:0}%", GetMicGainSliderRect(panelOrigin), (_stagedMicGain - 1f) / 8f);
        DrawLabeledSlider(AudioRightRow(content, 4), "ПРЕДОТВРАЩЕНИЕ ОТКЛЮЧЕНИЯ", $"{_stagedDisconnectPreventionMs:0} ms", GetDisconnectPreventionSliderRect(panelOrigin), _stagedDisconnectPreventionMs / 500f);
    }

    // Direct user request ("чтобы в 3 настройке были расписаны все активные действия на клавишах и
    // чтобы я мог их назначать") - every action PlayerActionBindings exposes, two columns of 7 (see
    // ControlsLeftColumn/ControlsRightColumn above), each row a label plus a clickable "socket"
    // showing the bound key; clicking one starts a capture (HandleSettingsScreen's own top-of-method
    // check), the next physical key press lands in it. Arrow keys, dev/diagnostic keys (F3/F11/the
    // tilde cheat panel/the Ъ tile-grid overlay) and menu-navigation keys (Escape, Enter-to-confirm
    // on OTHER screens) are deliberately not listed - see PlayerActionBindings' own doc comment.
    private void DrawControlsTab(Vector2 content)
    {
        var origin = SettingsPanelOrigin;
        DrawControlsColumn(content, ControlsLeftColumn);
        DrawControlsColumn(new Vector2(SettingsRightColumnX(content), content.Y), ControlsRightColumn);
        DrawSmallButton(GetControlsResetButtonRect(origin), "СБРОСИТЬ УПРАВЛЕНИЕ");
    }

    private void DrawControlsColumn(Vector2 columnOrigin, PlayerAction[] actions)
    {
        var origin = SettingsPanelOrigin;
        for (var i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            var rowY = columnOrigin.Y + i * ControlsRowHeight;
            _spriteBatch.DrawString(_font, PlayerActionBindings.Label(action), new Vector2(columnOrigin.X, rowY + 5),
                SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            DrawControlsKeySocket(GetControlsKeyRect(action, origin), action);
        }
    }

    private void DrawControlsKeySocket(Rectangle rect, PlayerAction action)
    {
        var awaiting = _awaitingRebindAction == action;
        var hover = SettingsHoverEase($"key:{action}", rect.Contains(_designMouse));
        var label = awaiting ? "..." : PlayerActionBindings.KeyDisplayName(_stagedKeyBindings.Get(action));
        _spriteBatch.Draw(_pixel, rect, Color.Lerp(new Color(15, 19, 25), new Color(24, 30, 38), hover));
        DrawBevel(rect, raised: false, strength: 0.85f);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, rect,
            awaiting ? SettingsAccentGold : Color.Lerp(SettingsBorderSteel, SettingsAccentTeal, hover * 0.5f), awaiting ? 2 : 1);
        DrawCenteredLabel(label, rect, awaiting ? SettingsAccentGold : Color.Lerp(SettingsTextPrimary, Color.White, hover * 0.4f));
    }

    // Direct user request - matches the reference screenshot's own layout (language + a handful of
    // checkboxes) as far as this game actually has real mechanics to back them; see this tab's own
    // staged-fields doc comment above for exactly what was left out and why.
    private void DrawInterfaceTab(Vector2 content)
    {
        var origin = SettingsPanelOrigin;

        _spriteBatch.DrawString(_font, "Ник:", content, SettingsTextDim, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, _nickname, content + new Vector2(60, 0), SettingsAccentTeal, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        DrawSmallButton(GetChangeNicknameButtonRect(origin), "ИЗМЕНИТЬ");

        _spriteBatch.DrawString(_font, "Роль:", content + new Vector2(0, 50), SettingsTextDim, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, _selectedRole is { } role ? role.ToString() : "не выбрана", content + new Vector2(70, 50), SettingsAccentTeal, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        DrawSmallButton(GetChangeRoleButtonRect(origin), "ИЗМЕНИТЬ");

        DrawLabeledStepper(content + new Vector2(0, 90), "ЯЗЫК", _stagedLanguage == "ru" ? "Русский" : "English",
            GetLanguagePrevRect(origin), GetLanguageNextRect(origin));

        DrawCheckboxRow(GetTooltipsCheckboxRect(origin), "Отключить подсказки в игре", !_stagedTooltipsEnabled);
        DrawCheckboxRow(GetChatBubblesCheckboxRect(origin), "Облака с текстом", _stagedChatBubblesEnabled);
        DrawCheckboxRow(GetEnemyHealthBarsCheckboxRect(origin), "Индикаторы здоровья врагов", _stagedEnemyHealthBarsEnabled);
    }

    private void DrawMiscTab(Vector2 content)
    {
        DrawSmallButton(GetResetSettingsButtonRect(SettingsPanelOrigin), "СБРОСИТЬ НАСТРОЙКИ");
        DrawSmallButton(GetOpenCreditsButtonRect(SettingsPanelOrigin), "АВТОРЫ");
    }

    private void DrawSmallButton(Rectangle rect, string label)
    {
        // rect's own position, not just the label, keys the hover ease - "<"/">" repeat on every
        // single stepper in both tabs, and "Обновить" repeats twice in the Audio tab; two buttons
        // sharing one animation state would make hovering one visibly nudge the other's glow too.
        var hover = SettingsHoverEase($"btn:{label}:{rect.X},{rect.Y}", rect.Contains(_designMouse));
        var top = Color.Lerp(new Color(44, 52, 60), new Color(64, 84, 86), hover);
        var bottom = Color.Lerp(new Color(24, 29, 35), new Color(32, 46, 48), hover);
        DrawVerticalGradient(rect, top, bottom, 5);
        DrawBevel(rect, raised: true, strength: 0.7f + hover * 0.5f);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, rect, Color.Lerp(SettingsBorderSteel, SettingsAccentTeal, hover * 0.6f), 1);
        DrawCenteredLabel(label, rect, Color.Lerp(SettingsTextPrimary, SettingsAccentTeal, hover * 0.7f));
    }

    private void DrawLabeledStepper(Vector2 position, string label, string value, Rectangle prevRect, Rectangle nextRect)
    {
        _spriteBatch.DrawString(_font, label, position, SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        DrawSmallButton(prevRect, "<");
        DrawSmallButton(nextRect, ">");
        // A small recessed readout plate behind the value, the same "socket" bevel the checkboxes
        // use - a stepper's own current value reads as an instrument display between two physical
        // buttons, not text floating on the bare panel.
        var plateRect = new Rectangle(prevRect.Right + 2, prevRect.Y, Math.Max(0, nextRect.X - prevRect.Right - 4), prevRect.Height);
        if (plateRect.Width > 4)
        {
            _spriteBatch.Draw(_pixel, plateRect, new Color(12, 16, 21));
            DrawBevel(plateRect, raised: false, strength: 0.6f);
        }
        // A device name can run far longer than Resolution/WindowMode's own short values ever did
        // (direct user bug report - a long one ran straight into the ">" button) - shrinks to fit
        // the actual gap between the arrows instead of overflowing past them, the same "a slightly
        // smaller label beats a truncated/overlapping one" rule DrawRoomFloor's own name-plate
        // already follows.
        var scale = 0.6f;
        var gap = nextRect.X - prevRect.Right;
        var valueSize = _font.MeasureString(value) * scale;
        if (valueSize.X > gap && valueSize.X > 0f)
        {
            scale *= MathHelper.Clamp(gap / valueSize.X, 0.35f, 1f);
            valueSize = _font.MeasureString(value) * scale;
        }
        var valueCenter = new Vector2((prevRect.Right + nextRect.X) / 2f, prevRect.Center.Y);
        _spriteBatch.DrawString(_font, value, valueCenter - valueSize / 2f, SettingsAccentTeal, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawLabeledSlider(Vector2 position, string label, string value, Rectangle track, float fraction)
    {
        _spriteBatch.DrawString(_font, label, position, SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        // Recessed channel the fill sits inside, same socket bevel every other "live value" widget
        // in this screen shares.
        _spriteBatch.Draw(_pixel, track, new Color(12, 16, 21));
        DrawBevel(track, raised: false, strength: 0.75f);
        var fillWidth = (int)(track.Width * Math.Clamp(fraction, 0f, 1f));
        if (fillWidth > 0)
        {
            var fillRect = new Rectangle(track.X, track.Y, fillWidth, track.Height);
            DrawVerticalGradient(fillRect, Color.Lerp(SettingsAccentTeal, Color.White, 0.3f), SettingsAccentTeal * 0.7f, 4);
            _spriteBatch.Draw(_pixel, new Rectangle(fillRect.X, fillRect.Y, fillRect.Width, 1), Color.White * 0.4f);
        }
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, track, SettingsBorderSteel, 1);
        // A small round knob (glowing when hovered) instead of a bare vertical bar - the one part of
        // the slider a hand would actually grip.
        var handleCenter = new Vector2(track.X + fillWidth, track.Center.Y);
        var handleHover = SettingsHoverEase($"handle:{label}", new Rectangle((int)handleCenter.X - 6, track.Y - 6, 12, track.Height + 12).Contains(_designMouse));
        HudIcons.FillCircle(_spriteBatch, _pixel, handleCenter, 5f + handleHover * 1.5f, Color.Lerp(new Color(210, 216, 224), Color.White, handleHover));
        HudIcons.FillCircle(_spriteBatch, _pixel, handleCenter, 2.5f, Color.Lerp(SettingsAccentTeal, Color.White, 0.3f));
        _spriteBatch.DrawString(_font, value, new Vector2(track.Right + 12, track.Y - 5), Color.Lerp(SettingsTextPrimary, SettingsAccentTeal, 0.4f), 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private static string WindowModeLabel(WindowMode mode) => mode switch
    {
        WindowMode.Fullscreen => "Полноэкранный",
        WindowMode.Windowed => "Оконный",
        _ => "Оконный без рамки",
    };
}
