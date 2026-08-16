using System.Text.Json;
using System.Text.Json.Serialization;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Reads and writes the single save slot (game_design.md section 5). One slot is deliberate: the
// design calls for an autosave at every docking, not a manual save-anywhere system, so there's
// nothing for the player to choose between.
//
// Failures are swallowed on purpose. A missing, corrupt, or version-mismatched file means "start a
// new run", and a save that can't be written (locked file, read-only directory) must never take
// the game down mid-play - losing progress is bad, crashing on a routine dock is worse.
public static class SaveStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }, // readable/greppable, and survives enum reordering
    };

    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpaceAdventure",
            "save.json");

    public static bool Exists(string? path = null) => File.Exists(path ?? DefaultPath);

    public static void Save(SaveGame save, string? path = null)
    {
        var target = path ?? DefaultPath;
        try
        {
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            // Write to a temp file and move into place, so an interrupted write can't leave a
            // half-written save where a good one used to be.
            var temporary = target + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(save, Options));
            File.Move(temporary, target, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Progress is lost, but the run continues - see the class comment.
        }
    }

    public static SaveGame? Load(string? path = null)
    {
        var target = path ?? DefaultPath;
        try
        {
            if (!File.Exists(target))
                return null;

            var save = JsonSerializer.Deserialize<SaveGame>(File.ReadAllText(target), Options);
            return save?.Version == SaveGame.CurrentVersion ? save : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null; // treat an unreadable save as no save at all
        }
    }

    public static void Delete(string? path = null)
    {
        try
        {
            var target = path ?? DefaultPath;
            if (File.Exists(target))
                File.Delete(target);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }
}
