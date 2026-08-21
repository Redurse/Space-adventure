using SpaceAdventure.Client.Audio;

namespace SpaceAdventure.Client;

// Where the round's music is turned on and off. One call site rather than two, because the thing that
// decides is whether a session is live - not which of the several ways it was started.
public partial class Game1
{
    private GameMusic? _music;

    private void UpdateGameMusic(double nowSeconds)
    {
        if (_music is null)
            return;

        // The menu keeps its quiet. Stop is idempotent, so calling it every frame out here costs a
        // branch and saves having to find every path back to the main menu.
        if (!_sessionStarted)
        {
            _music.Stop();
            return;
        }

        _music.Update(nowSeconds);
    }
}
