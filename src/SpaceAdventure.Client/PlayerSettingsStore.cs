using System;
using System.IO;
using System.Text.Json;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared;

namespace SpaceAdventure.Client;

// The things that are the *player's* rather than the *save's* - a nickname and a preferred crew
// role, both typed/picked once at the menu and remembered across every future launch, independent
// of which ship or save is active (mirrors SaveStore.cs's own file, in the same folder, but this
// one never gets deleted when a save does). Same swallow-failures philosophy: a machine that can't
// write this file just re-asks each launch instead of crashing on it.
public sealed record PlayerSettings(string? Nickname = null, CrewRole? Role = null,
    int? ResolutionWidth = null, int? ResolutionHeight = null, WindowMode? WindowMode = null,
    bool? VSync = null, float? MasterVolume = null, float? BloomStrength = null, int? MaxParticles = null);

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
            settings.VSync ?? true, settings.MasterVolume ?? 1f, settings.BloomStrength ?? 1f, settings.MaxParticles ?? Rendering.AtmosphereField.MaxParticles);
    }

    public static void SaveGraphicsSettings(GraphicsSettings graphics, string? path = null) =>
        Save(Load(path) with
        {
            ResolutionWidth = graphics.ResolutionWidth,
            ResolutionHeight = graphics.ResolutionHeight,
            WindowMode = graphics.WindowMode,
            VSync = graphics.VSync,
            MasterVolume = graphics.MasterVolume,
            BloomStrength = graphics.BloomStrength,
            MaxParticles = graphics.MaxParticles,
        }, path);
}

// ResolutionWidth/Height null means "use the desktop's own current resolution" - the same default
// Game1.Initialize already forces today, kept as the fallback so a machine that never opens the
// Settings screen sees no behavior change at all.
public readonly record struct GraphicsSettings(int? ResolutionWidth, int? ResolutionHeight, WindowMode WindowMode,
    bool VSync, float MasterVolume, float BloomStrength, int MaxParticles);
