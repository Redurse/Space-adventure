using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Anabiosis.Client.Audio;

// Loads a .wav or .mp3 file straight off disk (Content/Sounds, Content/Music - no longer compiled
// through the MonoGame Content pipeline, see GameAudioEngine's own doc comment) and folds it into the
// master mix's own format (GameAudioEngine.MasterFormat) - shared by GameSounds/GameMusic/JukeboxAudio so
// none of them re-implement "read file, expand to stereo, resample" three times over.
internal static class AudioFile
{
    // Raw, un-resampled - the caller (GameSounds, for pitch variation) may want to relabel the
    // sample rate before resampling, which has to happen before ToMasterFormat converts it.
    // The returned WaveStream (WaveFileReader/Mp3FileReader) is both the disposable the caller owns
    // (a one-shot: once it finishes; a cached track: never, until the whole session ends) and, for a
    // looping sound, the thing LoopingSampleProvider resets Position on.
    public static (ISampleProvider Sample, WaveStream Reader)? TryOpen(string path)
    {
        try
        {
            WaveStream reader = path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                ? new Mp3FileReader(path)
                : new WaveFileReader(path);
            return (reader.ToSampleProvider(), reader);
        }
        catch
        {
            // Missing or unbuilt: that one sound/track is silent, everything else still works - same
            // contract GameSounds/GameMusic's own load loops already keep.
            return null;
        }
    }

    // pitchFactor: XNA's own SoundEffect.Play(volume, pitch, pan) convention - pitch in [-1,1] maps
    // to [-1,+1] octave, i.e. playback-rate multiplier 2^pitch. Applied as a "rate lie" (relabel the
    // source's own declared sample rate before resampling to the master rate) - the classic cheap
    // pitch-via-resample trick: telling the resampler the source is faster/slower than it really is
    // makes it read through the same samples at a different speed, changing pitch and speed together
    // exactly the way XNA's own pitch parameter always did.
    // pan (only meaningful for a mono source, which every GameSounds SFX clip is - music/jukebox
    // never pass one): simple linear left/right blend, the same shape MonoGame's own
    // SoundEffect.Play(volume, pitch, pan) already gave callers, applied via
    // MonoToStereoSampleProvider's own LeftVolume/RightVolume rather than a separate pan stage.
    public static ISampleProvider ToMasterFormat(ISampleProvider sample, float pitchFactor = 1f, float pan = 0f)
    {
        if (pitchFactor != 1f)
            sample = new RateLieSampleProvider(sample, (int)(sample.WaveFormat.SampleRate * pitchFactor));
        if (sample.WaveFormat.Channels == 1)
        {
            var stereo = new MonoToStereoSampleProvider(sample)
            {
                LeftVolume = pan <= 0f ? 1f : 1f - pan,
                RightVolume = pan >= 0f ? 1f : 1f + pan,
            };
            sample = stereo;
        }
        if (sample.WaveFormat.SampleRate != GameAudioEngine.SampleRate)
            sample = new WdlResamplingSampleProvider(sample, GameAudioEngine.SampleRate);
        return sample;
    }

    private sealed class RateLieSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        public WaveFormat WaveFormat { get; }

        public RateLieSampleProvider(ISampleProvider source, int claimedSampleRate)
        {
            _source = source;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(Math.Max(1000, claimedSampleRate), source.WaveFormat.Channels);
        }

        public int Read(float[] buffer, int offset, int count) => _source.Read(buffer, offset, count);
    }
}
