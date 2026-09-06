using SpaceAdventure.Shared.Model;

internal static partial class TestRunner
{
    // M80 (humble-soaring-cat.md) - pure data/algorithm tests against a plain TileGrid, exactly like
    // TestRunner.TileGrid.cs's own tests. Nothing here touches Ship/World/the Client - CompartmentPlacer
    // isn't wired into the Ship Editor yet (that's M81+).
    //
    // Every test that named a specific catalog entry (rotation transform, engine tier geometry,
    // wall-dedup, overlap rejection) was removed along with CompartmentCatalog.Entries itself
    // (direct user request, "вместо всех текущих отсеков я буду присылать новые вариации") - this
    // one survives because it's genuinely entry-agnostic: it iterates whatever Entries actually
    // holds, so it's still exactly the right smoke check to have once new entries land there.

    // ---- Every catalog entry is internally sane - a cheap smoke check across the whole catalog
    // (rectangular by construction, but the device/airlock positions are hand-authored data, so a
    // typo landing one on the ring or out of bounds would otherwise go unnoticed). ----
    private static bool CompartmentCatalog_EveryEntry_HasDevicesStrictlyInteriorAndInBounds()
    {
        foreach (var entry in CompartmentCatalog.Entries)
        {
            bool Inside(TileCoord p) => p.X >= 0 && p.X < entry.Width && p.Y >= 0 && p.Y < entry.Height;
            bool OnRing(TileCoord p) => p.X == 0 || p.X == entry.Width - 1 || p.Y == 0 || p.Y == entry.Height - 1;

            foreach (var device in entry.Devices)
                if (!Inside(device.RelativePosition) || OnRing(device.RelativePosition))
                    return false;

            if (entry.Airlock is { } airlock)
            {
                var doorPos = CompartmentPlacer.Rotate(entry, 0).Airlock?.DoorPosition;
                if (doorPos is null || !Inside(doorPos.Value) || !OnRing(doorPos.Value))
                    return false;
            }
        }
        return true;
    }
}
