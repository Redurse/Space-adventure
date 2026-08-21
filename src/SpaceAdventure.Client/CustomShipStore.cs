using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared;

namespace SpaceAdventure.Client;

// The Ship Editor's own save slot - separate from SaveStore's run save, since a hull design is
// something the player keeps tinkering with across many runs, not campaign progress. Same
// write-to-temp-then-move and swallow-on-failure conventions as SaveStore/PlayerSettingsStore, for
// the same reason: a corrupt or unwritable file must mean "start from a blank hull", never a crash.
public static class CustomShipStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultPath =>
        Path.Combine(GameDataPath.Root, "custom-ship.json");

    public static CustomShipDefinition? Load(string? path = null)
    {
        try
        {
            var target = path ?? DefaultPath;
            if (!File.Exists(target))
                return null;
            return JsonSerializer.Deserialize<CustomShipDefinition>(File.ReadAllText(target), Options);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void Save(CustomShipDefinition definition, string? path = null)
    {
        try
        {
            var target = path ?? DefaultPath;
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temporary = target + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(definition, Options));
            File.Move(temporary, target, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Losing the in-progress design is annoying, not fatal - see the class comment.
        }
    }
}
