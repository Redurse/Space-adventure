using System;
using System.IO;
using System.Text.Json;
using Anabiosis.Shared.Model;
using Anabiosis.Shared;

namespace Anabiosis.Client;

// The things that are the *player's* rather than the *save's* - a nickname and a preferred crew
// role, both typed/picked once at the menu and remembered across every future launch, independent
// of which ship or save is active (mirrors SaveStore.cs's own file, in the same folder, but this
// one never gets deleted when a save does). Same swallow-failures philosophy: a machine that can't
// write this file just re-asks each launch instead of crashing on it.
public sealed record PlayerSettings(string? Nickname = null, CrewRole? Role = null,
    int? ResolutionWidth = null, int? ResolutionHeight = null, WindowMode? WindowMode = null,
    bool? VSync = null, float? SoundVolume = null, float? BloomStrength = null, int? MaxParticles = null,
    // Voice chat activation mode (direct user request - an alternative to push-to-talk): whether
    // the local voice channel opens automatically once the mic's own input level crosses
    // Threshold, instead of requiring V held. Radio (R) stays push-to-talk regardless of this
    // setting - it's a deliberate broadcast action, not something that should fire itself.
    bool? VoiceActivationEnabled = null, float? VoiceActivationThreshold = null,
    // Direct user request ("2 вкладка настроек как в Baротравме") - real output/input device
    // selection plus the rest of Baротравма's own Audio tab. OutputDeviceId/InputMicrophoneName are
    // null = "use the system default"/"use Microphone.Default", same fallback-on-missing contract
    // AudioEngine.SelectOutputDevice and VoiceCapture.ResolveMicrophone both already keep.
    string? OutputDeviceId = null, string? InputMicrophoneName = null,
    float? MusicVolume = null, float? UiVolume = null, float? VoiceChatVolume = null,
    bool? MuteOnFocusLoss = null, bool? DynamicRangeCompression = null,
    bool? DirectionalVoiceChat = null, bool? VoiceChatPriority = null,
    float? MicGain = null, float? DisconnectPreventionMs = null,
    // Direct user request - a single "turn it all off" switch for players whose machine/GPU
    // struggles with the custom-shader path (RoomLighting's per-pixel Light.fx, ScenePost's whole
    // Post.fx bloom/grade/vignette/grain/aberration/distortion chain, the main menu's Planet.fx).
    // Forces the same graceful null-effect fallback every one of those already has for a content
    // build that never produced the .xnb (see Shaders.cs) - not a new code path, just the existing
    // one flipped on deliberately instead of by accident.
    bool? ShadersEnabled = null,
    // Direct user request ("настройки в 3 вкладке... чтобы с перезаходом они сохранялись") - the
    // Controls tab's rebound keys, serialized by PlayerActionBindings itself (Input/
    // PlayerActionBindings.cs) as a flat "Action=Key;Action=Key" string rather than a nested type,
    // matching every other field in this record.
    string? KeyBindings = null,
    // Direct user request ("сделай возможность выбрать английский язык") - the selector only, no
    // localized text anywhere yet (scoped decision, confirmed with the user) - "ru" or "en".
    string? Language = null,
    // Interface tab toggles (direct user request, matching the reference screenshot) - each gates
    // a real, already-existing mechanic (item tooltips, chat speech bubbles, boarding health bars)
    // rather than being a fresh feature of its own; see DrawInterfaceTab's own doc comment for the
    // reference-screenshot items that were deliberately left out because nothing here backs them.
    bool? TooltipsEnabled = null, bool? ChatBubblesEnabled = null, bool? EnemyHealthBarsEnabled = null);

public enum WindowMode
{
    Fullscreen,
    Borderless,
    Windowed,
}

public static class PlayerSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath =>
        Path.Combine(GameDataPath.Root, "player-settings.json");

    // Read-modify-write, not a fresh record each time - Nickname and Role are saved at two
    // different screens (SaveNickname on Enter, SaveRole on picking one), and each has to leave
    // the other's already-saved value alone rather than overwriting it back to null.
    private static PlayerSettings Load(string? path)
    {
        try
        {
            var target = path ?? DefaultPath;
            if (!File.Exists(target))
                return new PlayerSettings();
            return JsonSerializer.Deserialize<PlayerSettings>(File.ReadAllText(target), Options) ?? new PlayerSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new PlayerSettings();
        }
    }

    private static void Save(PlayerSettings settings, string? path)
    {
        try
        {
            var target = path ?? DefaultPath;
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temporary = target + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
            File.Move(temporary, target, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }

    public static string? LoadNickname(string? path = null)
    {
        var nickname = Load(path).Nickname;
        return string.IsNullOrWhiteSpace(nickname) ? null : nickname;
    }

    public static void SaveNickname(string nickname, string? path = null) =>
        Save(Load(path) with { Nickname = nickname }, path);

    public static CrewRole? LoadRole(string? path = null) => Load(path).Role;

    public static void SaveRole(CrewRole? role, string? path = null) =>
        Save(Load(path) with { Role = role }, path);

    // Graphics/audio settings are read together, as one record, since the Settings screen commits
    // them together on "Применить" - unlike Nickname/Role above, there's no separate screen that
    // saves just one of these fields on its own.
    public static GraphicsSettings LoadGraphicsSettings(string? path = null)
    {
        var settings = Load(path);
        return new GraphicsSettings(
            settings.ResolutionWidth, settings.ResolutionHeight, settings.WindowMode ?? Client.WindowMode.Borderless,
            settings.VSync ?? true, settings.SoundVolume ?? 1f, settings.BloomStrength ?? 1f, settings.MaxParticles ?? Rendering.AtmosphereField.MaxParticles,
            settings.VoiceActivationEnabled ?? false, settings.VoiceActivationThreshold ?? 0.12f,
            settings.OutputDeviceId, settings.InputMicrophoneName,
            settings.MusicVolume ?? 1f, settings.UiVolume ?? 1f, settings.VoiceChatVolume ?? 1f,
            settings.MuteOnFocusLoss ?? false, settings.DynamicRangeCompression ?? false,
            settings.DirectionalVoiceChat ?? false, settings.VoiceChatPriority ?? false,
            settings.MicGain ?? 1f, settings.DisconnectPreventionMs ?? 200f,
            settings.ShadersEnabled ?? true,
            settings.KeyBindings, settings.Language ?? "ru",
            settings.TooltipsEnabled ?? true, settings.ChatBubblesEnabled ?? true, settings.EnemyHealthBarsEnabled ?? true);
    }

    public static void SaveGraphicsSettings(GraphicsSettings graphics, string? path = null) =>
        Save(Load(path) with
        {
            ResolutionWidth = graphics.ResolutionWidth,
            ResolutionHeight = graphics.ResolutionHeight,
            WindowMode = graphics.WindowMode,
            VSync = graphics.VSync,
            SoundVolume = graphics.SoundVolume,
            BloomStrength = graphics.BloomStrength,
            MaxParticles = graphics.MaxParticles,
            VoiceActivationEnabled = graphics.VoiceActivationEnabled,
            VoiceActivationThreshold = graphics.VoiceActivationThreshold,
            OutputDeviceId = graphics.OutputDeviceId,
            InputMicrophoneName = graphics.InputMicrophoneName,
            MusicVolume = graphics.MusicVolume,
            UiVolume = graphics.UiVolume,
            VoiceChatVolume = graphics.VoiceChatVolume,
            MuteOnFocusLoss = graphics.MuteOnFocusLoss,
            DynamicRangeCompression = graphics.DynamicRangeCompression,
            DirectionalVoiceChat = graphics.DirectionalVoiceChat,
            VoiceChatPriority = graphics.VoiceChatPriority,
            MicGain = graphics.MicGain,
            DisconnectPreventionMs = graphics.DisconnectPreventionMs,
            ShadersEnabled = graphics.ShadersEnabled,
            KeyBindings = graphics.KeyBindings,
            Language = graphics.Language,
            TooltipsEnabled = graphics.TooltipsEnabled,
            ChatBubblesEnabled = graphics.ChatBubblesEnabled,
            EnemyHealthBarsEnabled = graphics.EnemyHealthBarsEnabled,
        }, path);
}

// ResolutionWidth/Height null means "use the desktop's own current resolution" - the same default
// Game1.Initialize already forces today, kept as the fallback so a machine that never opens the
// Settings screen sees no behavior change at all.
public readonly record struct GraphicsSettings(int? ResolutionWidth, int? ResolutionHeight, WindowMode WindowMode,
    bool VSync, float SoundVolume, float BloomStrength, int MaxParticles,
    bool VoiceActivationEnabled, float VoiceActivationThreshold,
    string? OutputDeviceId = null, string? InputMicrophoneName = null,
    float MusicVolume = 1f, float UiVolume = 1f, float VoiceChatVolume = 1f,
    bool MuteOnFocusLoss = false, bool DynamicRangeCompression = false,
    bool DirectionalVoiceChat = false, bool VoiceChatPriority = false,
    float MicGain = 1f, float DisconnectPreventionMs = 200f,
    bool ShadersEnabled = true,
    string? KeyBindings = null, string Language = "ru",
    bool TooltipsEnabled = true, bool ChatBubblesEnabled = true, bool EnemyHealthBarsEnabled = true);
