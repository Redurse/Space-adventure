using System.Text.Json;
using Anabiosis.Shared;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// The campaigns that can be picked in the lobby's "Загр. кампанию" tab. Alongside the one autosave slot (save.json, which "Продолжить" reads) every autosave
// of a real game is also kept in a file of its own, one per campaign (named after the world's seed), so several campaigns can be played in turn and any of
// them continued later. A file is overwritten by that campaign's next autosave; nothing here is ever deleted by the game.
public static partial class SaveStore
{
    // The folder of the campaign slots that belongs to a save path: "saves" beside it. A server given a temporary save path (every test) therefore keeps
    // its slots in that temporary folder and never in the player's own.
    public static string SlotsDirectoryFor(string savePath) => Path.Combine(Path.GetDirectoryName(Path.GetFullPath(savePath)) ?? ".", "saves");

    private const string SlotExtension = ".json";

    // A save with no seed (an old file) has no campaign to belong to: it is not kept as a slot.
    public static string? SlotIdFor(SaveGame save) => save.WorldSeed is { } seed and > 0 ? $"c{seed}{SlotExtension}" : null;

    public static void SaveSlot(SaveGame save, string slotsDirectory)
    {
        if (SlotIdFor(save) is not { } id)
            return;
        Save(save, Path.Combine(slotsDirectory, id));
    }

    // An id from outside is a bare file name of the kind SlotIdFor makes, nothing that could point elsewhere on the disk.
    public static bool IsValidSlotId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 40 && id.StartsWith('c') && id.EndsWith(SlotExtension, StringComparison.Ordinal)
        && id[1..^SlotExtension.Length].All(char.IsDigit) && id.Length > 1 + SlotExtension.Length;

    public static SaveGame? LoadSlot(string slotsDirectory, string id) => IsValidSlotId(id) ? Load(Path.Combine(slotsDirectory, id)) : null;

    // What the tab lists, newest first. A file that cannot be read as a save is left out.
    public static IReadOnlyList<LobbySaveInfo> ListSlots(string slotsDirectory)
    {
        var result = new List<LobbySaveInfo>();
        try
        {
            if (!Directory.Exists(slotsDirectory))
                return result;
            foreach (var path in Directory.EnumerateFiles(slotsDirectory, "c*" + SlotExtension))
            {
                var id = Path.GetFileName(path);
                if (!IsValidSlotId(id) || Load(path) is not { } save)
                    continue;
                var ship = save.CustomShip?.Name is { Length: > 0 } name ? name : ShipCatalog.Name(save.ShipKind);
                result.Add(new LobbySaveInfo(id, $"Кампания {id[1..^SlotExtension.Length]}", ship, save.Credits, File.GetLastWriteTimeUtc(path)));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // An unreadable folder is an empty list.
        }
        return result.OrderByDescending(s => s.LastPlayedUtc).ToList();
    }
}
