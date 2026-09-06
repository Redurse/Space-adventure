namespace Anabiosis.Client.Rendering;

// M57 - the helm screen's 3 windows (M47's "окно 1/2/3") become 3 switchable tabs instead of all
// drawn at once, one per crew post: Captain flies (window 2's buttons + the new time-acceleration/
// flip controls), Scientist watches the sonar (window 1's schematic/scanner map), Engineer works
// the ship's device list (window 3's schematic, extended with click-to-repair). Purely client-
// local (Game1.cs's own _helmTab) - like ClickTarget, never sent to or read from the server, so
// anyone at helm can be on a different tab, and several players can share the same one.
public enum HelmTab
{
    Captain,
    Scientist,
    Engineer,
}
