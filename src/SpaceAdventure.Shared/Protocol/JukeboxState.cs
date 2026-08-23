using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// The jukebox's block position plus its on/off + selected track + volume (World.cs) - bundled
// together, unlike ReactorLeverState/ReactorBlock which ride separately, because this whole thing
// is null on WorldSnapshot when the ship has no jukebox device placed at all.
public sealed record JukeboxState(Jukebox Block, bool On, int TrackIndex, int Volume);
