using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Audio;

// Plays back every OTHER player's incoming voice chunks each tick - one persistent
// DynamicSoundEffectInstance + (for radio only) one persistent RadioVoiceFilter per sender,
// created lazily and kept for the rest of the session (torn down only if the sender's sample rate
// changes, which forces a fresh DynamicSoundEffectInstance since MonoGame's own doesn't allow
// changing sample rate after construction).
public sealed class VoicePlayback
{
    private const int MaxPendingBuffers = 6;
    // Local (non-radio) voice is inaudible beyond this many world units - same "keep it simple,
    // no wall/occlusion modelling" scope as everything else about this feature; linear falloff
    // from full volume at 0 to silent at MaxLocalRange.
    private const float MaxLocalRange = 15f;

    private sealed class Speaker
    {
        public DynamicSoundEffectInstance? Sound;
        public RadioVoiceFilter? RadioFilter;
        public int SampleRate;
    }

    private readonly Dictionary<int, Speaker> _speakers = new();

    // myPosition/senderPositionLookup let local-mode volume fall off with distance; ignored
    // entirely for radio chunks, which always play at full volume regardless of distance (direct
    // user request - that's the whole point of a radio channel).
    public void Update(IReadOnlyList<VoiceChunkMessage>? chunks, Vec2 myPosition, System.Func<int, Vec2?> senderPositionLookup)
    {
        if (chunks is not { Count: > 0 })
            return;

        foreach (var chunk in chunks)
        {
            if (!_speakers.TryGetValue(chunk.SenderPlayerId, out var speaker))
            {
                speaker = new Speaker { SampleRate = chunk.SampleRate };
                _speakers[chunk.SenderPlayerId] = speaker;
            }
            if (speaker.Sound is null || speaker.SampleRate != chunk.SampleRate)
            {
                speaker.Sound?.Stop();
                speaker.Sound?.Dispose();
                speaker.SampleRate = chunk.SampleRate;
                speaker.Sound = new DynamicSoundEffectInstance(chunk.SampleRate, AudioChannels.Mono);
                speaker.Sound.Play();
            }
            if (speaker.Sound.PendingBufferCount > MaxPendingBuffers)
                continue; // falling behind - drop this chunk rather than let latency grow

            var samples = chunk.Samples;
            if (chunk.IsRadio)
            {
                speaker.RadioFilter ??= new RadioVoiceFilter(chunk.SampleRate);
                // Apply mutates in place - fine, this array is this call's own deserialized copy,
                // nothing else reads it afterward.
                speaker.RadioFilter.Apply(samples);
                speaker.Sound.Volume = 1f;
            }
            else
            {
                speaker.RadioFilter = null; // in case this sender switches modes between chunks
                var senderPos = senderPositionLookup(chunk.SenderPlayerId);
                var distance = senderPos is { } p ? (p - myPosition).Length() : (double)MaxLocalRange;
                speaker.Sound.Volume = (float)System.Math.Clamp(1.0 - distance / MaxLocalRange, 0.0, 1.0);
            }
            speaker.Sound.SubmitBuffer(samples);
        }
    }
}
