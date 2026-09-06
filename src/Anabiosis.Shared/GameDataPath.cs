using System;
using System.IO;

namespace Anabiosis.Shared;

/// <summary>
/// Where the game keeps its saves, settings and custom ships.
/// </summary>
/// <remarks>
/// One place for it, because there are three stores and they must not disagree - a settings file and
/// a save file in different folders is a bug nobody notices until someone moves a machine.
///
/// It also carries the renames - there have been two, and there may be more. Simply pointing at a
/// new folder would leave every existing save, setting and custom ship behind in a directory nothing
/// reads any more: the player opens the game to find their run gone, with the files sitting there
/// perfectly intact. So the old folder is moved across, once, the first time a build with the new
/// name runs.
///
/// If that move fails - the folder is open in Explorer, the drive is read-only, anything - the old
/// path is used as it stands. Renaming a directory is not worth losing a campaign over.
/// </remarks>
public static class GameDataPath
{
    private const string FolderName = "Anabiosis";

    // Every name the folder has ever had, newest first. A single legacy name was enough while there
    // had only been one rename; a second one means a player who never ran a build in between would
    // otherwise be stranded, so the whole chain is tried in order.
    private static readonly string[] LegacyFolderNames = { "Diapause", "Latency", "Unidentified Signal", "SpaceAdventure" };

    private static string? _root;

    public static string Root => _root ??= Resolve();

    private static string Resolve()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var current = Path.Combine(local, FolderName);
        if (Directory.Exists(current))
            return current;

        foreach (var name in LegacyFolderNames)
        {
            var legacy = Path.Combine(local, name);
            if (!Directory.Exists(legacy))
                continue;

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

        return current;
    }
}
