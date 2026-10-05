using Anabiosis.Client;

internal static partial class TestRunner
{
    private static bool PlayerSettings_FrameLimit_DefaultsTo60AndRoundTrips()
    {
        var path = Path.Combine(Path.GetTempPath(), $"frame-limit-{Guid.NewGuid():N}.json");
        try
        {
            if (PlayerSettingsStore.LoadGraphicsSettings(path).FrameLimit != 60)
                return false; // no file yet: the game's long-standing 60 FPS

            PlayerSettingsStore.SaveGraphicsSettings(PlayerSettingsStore.LoadGraphicsSettings(path) with { FrameLimit = 0 }, path);
            if (PlayerSettingsStore.LoadGraphicsSettings(path).FrameLimit != 0)
                return false; // uncapped must survive a reload, not fall back to the default

            PlayerSettingsStore.SaveGraphicsSettings(PlayerSettingsStore.LoadGraphicsSettings(path) with { FrameLimit = 144 }, path);
            return PlayerSettingsStore.LoadGraphicsSettings(path).FrameLimit == 144;
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
