using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    // Редактор корабля в духе Cosmoteer + несколько сохранённых кораблей (humble-soaring-cat.md,
    // Step 5) - one file per named ship, the file name (sanitized) IS the identity: a slot name is a
    // different thing from CustomShipDefinition.Name (the ship's own in-game display name, shown on
    // ShipSelect/HUD) - renaming the ship itself must not orphan or duplicate the save slot it lives
    // in. DefaultPath/Load/Save above stay untouched - they remain the editor's own single "currently
    // open, not yet saved under a name" scratch slot, same as before this feature existed.
    //
    // Every method below takes an optional explicit directory, same reason Load/Save above already
    // take an optional explicit path: a test must never touch the real player's
    // %LocalAppData%/.../custom-ships folder (TestRunner.QuestsAndSave.cs's own SaveStore tests
    // already establish this "always pass an explicit temp path from a test" convention).
    public static string DefaultShipsDirectory => Path.Combine(GameDataPath.Root, "custom-ships");

    private static string SanitizeSlotName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return sanitized.Length == 0 ? "корабль" : sanitized;
    }

    private static string ShipSlotPath(string slotName, string directory) => Path.Combine(directory, SanitizeSlotName(slotName) + ".json");

    // Runs once - the moment the per-name directory doesn't exist yet, which is exactly "this
    // installation still only ever had the old single custom-ship.json". The legacy file's own
    // Name becomes the first slot's name; if there was no legacy file either (a genuinely fresh
    // install), this just creates an empty directory and every later ListShips call skips it.
    // legacyPath mirrors DefaultPath - only ever overridden by a test, alongside its own directory.
    private static void MigrateLegacyShipIfNeeded(string directory, string legacyPath)
    {
        if (Directory.Exists(directory))
            return;
        var legacy = Load(legacyPath);
        Directory.CreateDirectory(directory);
        if (legacy is not null)
            SaveShip(legacy.Name, legacy, directory);
    }

    public static IReadOnlyList<string> ListShips(string? directory = null, string? legacyPath = null)
    {
        var dir = directory ?? DefaultShipsDirectory;
        MigrateLegacyShipIfNeeded(dir, legacyPath ?? DefaultPath);
        try
        {
            return Directory.GetFiles(dir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(n => n is not null)
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    public static CustomShipDefinition? LoadShip(string slotName, string? directory = null, string? legacyPath = null)
    {
        var dir = directory ?? DefaultShipsDirectory;
        MigrateLegacyShipIfNeeded(dir, legacyPath ?? DefaultPath);
        return Load(ShipSlotPath(slotName, dir));
    }

    public static void SaveShip(string slotName, CustomShipDefinition definition, string? directory = null) =>
        Save(definition, ShipSlotPath(slotName, directory ?? DefaultShipsDirectory));

    public static void DeleteShip(string slotName, string? directory = null)
    {
        try
        {
            var path = ShipSlotPath(slotName, directory ?? DefaultShipsDirectory);
            if (File.Exists(path))
                File.Delete(path);
            var tilePath = ShipSlotTileCanvasPath(slotName, directory ?? DefaultShipsDirectory);
            if (File.Exists(tilePath))
                File.Delete(tilePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Same "annoying, not fatal" tolerance as every other method here.
        }
    }

    // The tile canvas (Game1.ShipEditor.cs's _editorTiles/_editorDeviceKinds/_editorZones) - direct
    // user request ("сохранять построенные корабли"). CustomShipDefinition (above) is a lossy,
    // one-way export of this data, so it needs its own file rather than trying to round-trip
    // through Room rectangles - saved as a sibling to the ship's own .json, same slot name, so
    // deleting/renaming a slot naturally takes both with it (DeleteShip above already does).
    private static string TileCanvasPath(string basePath) => Path.ChangeExtension(basePath, null) + ".tiles.json";
    private static string ShipSlotTileCanvasPath(string slotName, string directory) => TileCanvasPath(ShipSlotPath(slotName, directory));
    public static string DefaultTileCanvasPath => TileCanvasPath(DefaultPath);

    public static CustomShipTileCanvas? LoadTileCanvas(string? path = null)
    {
        try
        {
            var target = path ?? DefaultTileCanvasPath;
            if (!File.Exists(target))
                return null;
            return JsonSerializer.Deserialize<CustomShipTileCanvas>(File.ReadAllText(target), Options);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public static void SaveTileCanvas(CustomShipTileCanvas canvas, string? path = null)
    {
        try
        {
            var target = path ?? DefaultTileCanvasPath;
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temporary = target + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(canvas, Options));
            File.Move(temporary, target, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // Same "annoying, not fatal" tolerance as Save above.
        }
    }

    public static CustomShipTileCanvas? LoadShipTileCanvas(string slotName, string? directory = null) =>
        LoadTileCanvas(ShipSlotTileCanvasPath(slotName, directory ?? DefaultShipsDirectory));

    public static void SaveShipTileCanvas(string slotName, CustomShipTileCanvas canvas, string? directory = null) =>
        SaveTileCanvas(canvas, ShipSlotTileCanvasPath(slotName, directory ?? DefaultShipsDirectory));
}
