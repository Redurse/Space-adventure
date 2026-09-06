using Anabiosis.Client.Audio;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// Drives the in-world jukebox device's audio from the latest server snapshot - the client-side
// half of Game1.Music.cs, kept in its own file since it's a separate audio channel with its own
// on/off/track/volume state (World.cs), not a variation on the ambient bag.
public partial class Game1
{
    private JukeboxAudio? _jukeboxAudio;

    private void UpdateJukeboxAudio(JukeboxState? jukebox) => _jukeboxAudio?.Update(jukebox);
}
