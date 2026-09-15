using System;
using System.Collections.Generic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Audio;

// Plays back every OTHER player's incoming voice chunks each tick - one persistent BufferedWaveProvider
// chain + (for radio only) one persistent RadioVoiceFilter per sender, created lazily and kept for
// the rest of the session (torn down only if the sender's sample rate changes, which forces a fresh
// BufferedWaveProvider since its own WaveFormat can't change after construction any more than
// MonoGame's DynamicSoundEffectInstance's could).
//
// Routes through GameAudioEngine now (direct user request, real output-device selection) instead of
// MonoGame's DynamicSoundEffectInstance - BufferedWaveProvider is NAudio's own direct equivalent of
// "push raw PCM bytes in as they arrive, play them out continuously" (SubmitBuffer's replacement is
// AddSamples). The existing distance/occlusion/radio-range volume math is untouched; only how the
// result reaches the speakers changed.
public sealed class VoicePlayback
{
    // Local (non-radio) voice is inaudible beyond this many world units; linear falloff from full
    // volume at 0 to silent at MaxLocalRange.
    private const float MaxLocalRange = 15f;
    // Radio's own range (direct user request, "рация как предмет с дальностью" - a limit instead
    // of unconditional ship-wide reach), generous enough to cover any single hand-authored hull.
    // Full volume until the last 15% of the range, then a short fade rather than a hard cliff.
    private const float MaxRadioRange = 60f;
    // How much a wall between speaker and listener cuts local voice down by - muffled, not
    // silenced outright, the same "still there, just harder to make out" read a real wall gives a
    // voice rather than a clean cutout.
    private const float OcclusionMultiplier = 0.25f;
    // Falling-behind guard (BufferedWaveProvider's own equivalent of the old PendingBufferCount check)
    // - roughly the same ~600ms of slack 6 pending 100ms buffers used to allow.
    private static readonly TimeSpan MaxBufferedAhead = TimeSpan.FromMilliseconds(600);
    // World-unit half-width of the stereo pan field (direct user request, "Направленный голосовой
    // чат") - a sender this far to either side of the listener already reads as fully left/right;
    // this is a top-down camera with no rotation to track, so panning is a simple world-space X
    // delta rather than anything relative to facing.
    private const float PanRange = 10f;

    private sealed class Speaker
    {
        public BufferedWaveProvider? Buffer;
        public MonoToStereoSampleProvider? Stereo;
        public VolumeSampleProvider? Volume;
        public ISampleProvider? MixerInput;
        public RadioVoiceFilter? RadioFilter;
        public int SampleRate;
    }

    private readonly GameAudioEngine _engine;
    private readonly Dictionary<int, Speaker> _speakers = new();

    // Direct user requests, both off by default (matching Barotrauma's own unchecked defaults).
    public bool DirectionalVoiceChat { get; set; }
    public bool VoiceChatPriority { get; set; }

    public VoicePlayback(GameAudioEngine engine)
    {
        _engine = engine;
    }

    // myPosition/senderPositionLookup let local-mode volume fall off with distance; isOccluded is a
    // caller-supplied wall test (Game1 builds the ship's own wall list via
    // Anabiosis.Client.Rendering.ShadowCast/TileOccluders, which this Audio-namespace class has no
    // business depending on directly) so a voice muffles through a wall the same way sight and room
    // lighting already do. Radio ignores both position and occlusion entirely for its own volume -
    // only its own range limit applies - since a radio transmission doesn't care what's between the
    // two radios.
    public void Update(IReadOnlyList<VoiceChunkMessage>? chunks, Vec2 myPosition,
        Func<int, Vec2?> senderPositionLookup, Func<Vec2, Vec2, bool>? isOccluded = null)
    {
        // Direct user request ("Приоритет гол. чата") - ducks the Sound/Music/UI buses for as long
        // as anyone has voice chunks arriving this tick, restored the instant nobody does.
        _engine.DuckMultiplier = VoiceChatPriority && chunks is { Count: > 0 } ? 0.35f : 1f;

        if (chunks is not { Count: > 0 })
            return;

        foreach (var chunk in chunks)
        {
            if (!_speakers.TryGetValue(chunk.SenderPlayerId, out var speaker))
            {
                speaker = new Speaker();
                _speakers[chunk.SenderPlayerId] = speaker;
            }
            if (speaker.Buffer is null || speaker.SampleRate != chunk.SampleRate)
            {
                if (speaker.MixerInput is not null)
                    _engine.RemoveMixerInput(speaker.MixerInput);

                speaker.SampleRate = chunk.SampleRate;
                speaker.Buffer = new BufferedWaveProvider(new WaveFormat(chunk.SampleRate, 16, 1))
                {
                    DiscardOnBufferOverflow = true,
                    BufferDuration = TimeSpan.FromSeconds(2),
                };
                ISampleProvider chain = speaker.Buffer.ToSampleProvider();
                speaker.Stereo = new MonoToStereoSampleProvider(chain);
                chain = speaker.Stereo;
                if (chunk.SampleRate != GameAudioEngine.SampleRate)
                    chain = new WdlResamplingSampleProvider(chain, GameAudioEngine.SampleRate);
                speaker.Volume = new VolumeSampleProvider(chain) { Volume = 0f };
                speaker.MixerInput = speaker.Volume;
                _engine.AddMixerInput(speaker.MixerInput);
            }
            if (speaker.Buffer.BufferedDuration > MaxBufferedAhead)
                continue; // falling behind - drop this chunk rather than let latency grow

            var samples = chunk.Samples;
            var senderPos = senderPositionLookup(chunk.SenderPlayerId);
            var distance = senderPos is { } p ? (p - myPosition).Length() : double.MaxValue;
            float volume;
            if (chunk.IsRadio)
            {
                speaker.RadioFilter ??= new RadioVoiceFilter(chunk.SampleRate);
                // Apply mutates in place - fine, this array is this call's own deserialized copy,
                // nothing else reads it afterward.
                speaker.RadioFilter.Apply(samples);
                const float fadeStart = MaxRadioRange * 0.85f;
                volume = 1f - Math.Clamp(((float)distance - fadeStart) / (MaxRadioRange - fadeStart), 0f, 1f);
                SetPan(speaker, 0f); // radio doesn't care what's between the two radios, so it never pans either
            }
            else
            {
                speaker.RadioFilter = null; // in case this sender switches modes between chunks
                var frac = (float)Math.Clamp(1.0 - distance / MaxLocalRange, 0.0, 1.0);
                if (frac > 0f && senderPos is { } sp && isOccluded?.Invoke(myPosition, sp) == true)
                    frac *= OcclusionMultiplier;
                volume = frac;
                var pan = DirectionalVoiceChat && senderPos is { } dp
                    ? Math.Clamp((float)(dp.X - myPosition.X) / PanRange, -1f, 1f)
                    : 0f;
                SetPan(speaker, pan);
            }
            if (speaker.Volume is not null)
                speaker.Volume.Volume = volume * _engine.VoiceVolume;
            speaker.Buffer.AddSamples(samples, 0, samples.Length);
        }
    }

    private static void SetPan(Speaker speaker, float pan)
    {
        if (speaker.Stereo is null)
            return;
        speaker.Stereo.LeftVolume = pan <= 0f ? 1f : 1f - pan;
        speaker.Stereo.RightVolume = pan >= 0f ? 1f : 1f + pan;
    }
}
