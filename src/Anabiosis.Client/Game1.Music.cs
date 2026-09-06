using Anabiosis.Client.Audio;

namespace Anabiosis.Client;

// Where the round's music is turned on and off. One call site rather than two, because the thing that
// decides is whether a session is live - not which of the several ways it was started.
public partial class Game1
{
    private GameMusic? _music;

    // suppressed is true whenever the ship's jukebox (Game1.Jukebox.cs) is actually on - the
    // ambient bag simply stays stopped rather than trying to duck under or interleave with it,
    // since MediaPlayer is one global channel and the jukebox is the thing the crew asked for.
    private void UpdateGameMusic(double nowSeconds, bool suppressed)
    {
        if (_music is null)
            return;

        // The menu keeps its quiet. Stop is idempotent, so calling it every frame out here costs a
        // branch and saves having to find every path back to the main menu.
        if (!_sessionStarted || suppressed)
        {
            _music.Stop();
            return;
        }

        _music.Update(nowSeconds);
    }
}
