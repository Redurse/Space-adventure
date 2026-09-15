using System.Collections.Generic;
using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

public sealed partial class World
{
    private readonly List<VoiceChunkMessage> _pendingVoiceChunks = new();

    // How many ticks a "recently spoke" stamp stays true for (IsRecentlySpeaking) - long enough to
    // bridge the gap between the mic's own back-to-back ~100ms chunks without flickering, short
    // enough that the indicator drops out promptly once a player actually lets go of the key.
    private const long RecentSpeakingTickWindow = 10;
    private readonly Dictionary<int, long> _lastVoiceChunkTick = new();

    private void RelayVoiceChunk(Character character, VoiceChunkPayload payload)
    {
        if (payload.Samples.Length == 0)
            return;
        // Direct user request - radio needs a worn Radio (ItemType.Radio's own doc comment) to
        // transmit on; local/proximity voice needs no item at all. A radio chunk sent without one
        // equipped is dropped silently rather than falling back to local, so pressing the radio key
        // with nothing worn just does nothing, the same "nothing happens" a real unpowered radio
        // would give you.
        if (payload.IsRadio && character.Inventory.Equipped.GetValueOrDefault(EquipSlot.Headset) != ItemType.Radio)
            return;
        _lastVoiceChunkTick[character.PlayerId] = Tick;
        _pendingVoiceChunks.Add(new VoiceChunkMessage(character.PlayerId, payload.Samples, payload.SampleRate, payload.IsRadio));
    }

    // Whether this player sent a voice chunk recently enough to still count as "speaking" for the
    // overhead indicator (ShipRenderer.DrawCharacterLabel) - a snapshot-frozen fact rather than
    // "IsRecording", which only the speaker's own client ever knows about itself.
    private bool IsRecentlySpeaking(int playerId) =>
        _lastVoiceChunkTick.TryGetValue(playerId, out var lastTick) && Tick - lastTick <= RecentSpeakingTickWindow;

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
