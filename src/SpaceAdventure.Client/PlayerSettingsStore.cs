using System;
using System.IO;
using System.Text.Json;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client;

// The things that are the *player's* rather than the *save's* - a nickname and a preferred crew
// role, both typed/picked once at the menu and remembered across every future launch, independent
// of which ship or save is active (mirrors SaveStore.cs's own file, in the same folder, but this
// one never gets deleted when a save does). Same swallow-failures philosophy: a machine that can't
// write this file just re-asks each launch instead of crashing on it.
public sealed record PlayerSettings(string? Nickname = null, CrewRole? Role = null);

public static class PlayerSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpaceAdventure",
            "player-settings.json");

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
}
