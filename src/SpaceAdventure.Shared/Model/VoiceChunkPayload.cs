namespace SpaceAdventure.Shared.Model;

// One chunk of raw 16-bit mono PCM captured this tick from a client's microphone (Microphone.
// BufferReady), sent up as part of that tick's ClientCommand. No compression (direct, deliberate
// choice) - this is a hobby-scale LAN co-op game, and the wire layer's existing JSON+deflate
// framing already handles the bytes fine. SampleRate travels with every chunk rather than being
// assumed constant, since Microphone.SampleRate reflects whatever the OS/hardware actually
// negotiated and could differ between two players' machines.
public sealed record VoiceChunkPayload(byte[] Samples, int SampleRate, bool IsRadio);
