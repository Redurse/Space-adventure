namespace Anabiosis.Shared.Model;

// The jukebox's physical position on the deck - same shape as CardTable, but genuinely optional
// (Ship.Jukebox is nullable): a hull with none placed in the Ship Editor simply has no jukebox at
// all, unlike CardTable which every hull gets whether the player asked for one or not.
public sealed record Jukebox(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}

// Track count only - titles and the actual audio assets are a client-side concern
// (Anabiosis.Client.Audio.JukeboxTracks). Kept here so World.cs can wrap the selected index
// into a valid one without the server project depending on the client project.
public static class JukeboxCatalog
{
    public const int TrackCount = 25;
}
