using System;
using System.IO;

namespace SpaceAdventure.Shared;

/// <summary>
/// Where the game keeps its saves, settings and custom ships.
/// </summary>
/// <remarks>
/// One place for it, because there are three stores and they must not disagree - a settings file and
/// a save file in different folders is a bug nobody notices until someone moves a machine.
///
/// It also carries the rename. The folder used to be named after the working title, and simply
/// pointing at a new one would have left every existing save, setting and custom ship behind in a
/// directory nothing reads any more - the player would open the game to find their run gone, with
/// the files sitting there perfectly intact. So the old folder is moved across, once, the first time
/// a build with the new name runs.
///
/// If that move fails - the folder is open in Explorer, the drive is read-only, anything - the old
/// path is used as it stands. Renaming a directory is not worth losing a campaign over.
/// </remarks>
public static class GameDataPath
{
    private const string FolderName = "Unidentified Signal";
    private const string LegacyFolderName = "SpaceAdventure";

    private static string? _root;

    public static string Root => _root ??= Resolve();

    private static string Resolve()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var current = Path.Combine(local, FolderName);
        var legacy = Path.Combine(local, LegacyFolderName);

        // Already migrated, or nothing to migrate.
        if (Directory.Exists(current) || !Directory.Exists(legacy))
            return current;

        try
        {
            Directory.Move(legacy, current);
            return current;
        }
        catch (Exception)
        {
            return legacy;
        }
    }
}
