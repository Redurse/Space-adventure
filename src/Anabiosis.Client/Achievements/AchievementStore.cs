using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Anabiosis.Shared;

namespace Anabiosis.Client.Achievements;

// Mirrors PlayerSettingsStore.cs's own shape exactly (same folder, same swallow-failures
// philosophy - a machine that can't write this file just re-unlocks silently next launch instead
// of crashing) - a player-level file, not a save-level one, so achievements earned on one campaign
// stay earned after starting a new one, the same way a Steam achievement never resets with a new
// save file.
public static class AchievementStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(GameDataPath.Root, "achievements.json");

    public static Dictionary<string, DateTime> Load(string? path = null)
    {
        try
        {
            var target = path ?? DefaultPath;
            if (!File.Exists(target))
                return new Dictionary<string, DateTime>();
            return JsonSerializer.Deserialize<Dictionary<string, DateTime>>(File.ReadAllText(target), Options)
                ?? new Dictionary<string, DateTime>();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Dictionary<string, DateTime>();
        }
    }

    public static void Save(IReadOnlyDictionary<string, DateTime> unlocked, string? path = null)
    {
        try
        {
            var target = path ?? DefaultPath;
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temporary = target + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(unlocked, Options));
            File.Move(temporary, target, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
        }
    }
}
