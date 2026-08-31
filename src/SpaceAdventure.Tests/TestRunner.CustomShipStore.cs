using System;
using System.IO;
using System.Linq;
using SpaceAdventure.Client;
using SpaceAdventure.Shared.Model;

// Редактор корабля в духе Cosmoteer + несколько сохранённых кораблей (humble-soaring-cat.md, Step 5)
// - storage-level tests for CustomShipStore's own ListShips/LoadShip/SaveShip/DeleteShip, always
// against an explicit temp directory (never GameDataPath.Root - TestRunner.QuestsAndSave.cs's own
// SaveStore tests already establish that convention for exactly this reason).
internal static partial class TestRunner
{
    private static string TempShipsDirectory() =>
        Path.Combine(Path.GetTempPath(), $"spaceadventure-test-ships-{Guid.NewGuid():N}");

    private static CustomShipDefinition SampleShipDefinition(string name) => new(
        name,
        new[] { new CustomRoomDef("room-1", "Отсек", 0f, 0f, 3f, 3f) },
        Array.Empty<CustomDoorDef>(),
        new[] { new CustomAirlockDef("room-1", EdgeSide.Left) },
        Array.Empty<CustomDeviceDef>(),
        ForwardDegrees: 0f);

    private static bool CustomShipStore_SavesListsAndDeletesMultipleNamedShips()
    {
        var dir = TempShipsDirectory();
        try
        {
            CustomShipStore.SaveShip("Корабль А", SampleShipDefinition("Корабль А"), dir);
            CustomShipStore.SaveShip("Корабль Б", SampleShipDefinition("Корабль Б"), dir);

            var names = CustomShipStore.ListShips(dir);
            if (names.Count != 2 || !names.Contains("Корабль А") || !names.Contains("Корабль Б"))
                return false;

            var loadedA = CustomShipStore.LoadShip("Корабль А", dir);
            if (loadedA is null || loadedA.Name != "Корабль А")
                return false;

            CustomShipStore.DeleteShip("Корабль Б", dir);
            var remaining = CustomShipStore.ListShips(dir);
            return remaining.Count == 1 && remaining[0] == "Корабль А";
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    // The legacy single-slot custom-ship.json (pre-Step-5 installs) must not strand the player's
    // one existing design the moment they update - it has to show up as a normal named slot the
    // very first time the new multi-ship storage is ever touched.
    private static bool CustomShipStore_MigratesLegacySingleSlotFileOnFirstAccess()
    {
        var dir = TempShipsDirectory(); // must not already exist - migration only fires then
        var legacyPath = Path.Combine(Path.GetTempPath(), $"spaceadventure-test-legacy-{Guid.NewGuid():N}.json");
        try
        {
            CustomShipStore.Save(SampleShipDefinition("Старый корабль"), legacyPath);

            var names = CustomShipStore.ListShips(dir, legacyPath);
            return names.Count == 1 && names[0] == "Старый корабль"
                && CustomShipStore.LoadShip("Старый корабль", dir, legacyPath)?.Name == "Старый корабль";
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
            if (File.Exists(legacyPath))
                File.Delete(legacyPath);
        }
    }
}
