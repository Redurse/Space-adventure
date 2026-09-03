using System.Collections.Generic;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed partial class World
{
    private readonly List<VoiceChunkMessage> _pendingVoiceChunks = new();

    private void RelayVoiceChunk(Character character, VoiceChunkPayload payload)
    {
        if (payload.Samples.Length == 0)
            return;
        _pendingVoiceChunks.Add(new VoiceChunkMessage(character.PlayerId, payload.Samples, payload.SampleRate, payload.IsRadio));
    }

    // Read-and-clear, not a capped growing log (see VoiceChunkMessage's own doc comment) - safe to
    // call exactly once per tick, from CreateSnapshot, since GameServer's own per-tick method
    // drains and applies every connection's commands before ever calling CreateSnapshot.
    private IReadOnlyList<VoiceChunkMessage> CreateVoiceChunks()
    {
        if (_pendingVoiceChunks.Count == 0)
            return System.Array.Empty<VoiceChunkMessage>();
        var result = _pendingVoiceChunks.ToArray();
        _pendingVoiceChunks.Clear();
        return result;
    }
}
