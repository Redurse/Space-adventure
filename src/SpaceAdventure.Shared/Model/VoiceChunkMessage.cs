namespace SpaceAdventure.Shared.Model;

// A relayed voice chunk for this tick only - NOT an append-only growing log like ChatLogEntry.
// Missing one tick's worth of audio because of a dropped/late snapshot is an acceptable, inaudible
// gap (a few tens of milliseconds), unlike a lost chat line, so there is no cap/replay concern
// here at all - the server-side list this is built from is read-and-cleared every tick (see
// World.Voice.cs), not capped-and-kept.
public sealed record VoiceChunkMessage(int SenderPlayerId, byte[] Samples, int SampleRate, bool IsRadio);
