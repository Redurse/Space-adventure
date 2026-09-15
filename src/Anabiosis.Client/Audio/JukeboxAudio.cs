using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Audio;

// Plays whichever single track the ship's jukebox has selected (World.cs's JukeboxOn/TrackIndex/
// Volume) - routes through GameAudioEngine now (direct user request, real output-device selection)
// instead of MonoGame's MediaPlayer/Song. GameMusic's ambient bag and this share GameAudioEngine's music
// bus conceptually the same way they used to share MediaPlayer's one global channel: Game1.Music.cs
// still simply stops the ambient bag outright whenever the jukebox is on, so the two never actually
// need to mix with each other, just with SFX/voice on the master bus.
//
// Tracks load lazily, one at a time, the first time each is actually selected - not all 29 up front
// in the constructor the way GameMusic preloads its own 5-track ambient bag (same "don't stall
// startup decoding ~110MB of mp3 nobody's picked yet" reasoning as before this rewrite).
public sealed class JukeboxAudio
{
    private readonly GameAudioEngine _engine;
    private readonly Dictionary<int, (ISampleProvider Sample, WaveStream Reader)?> _loaded = new();
    private LoopingSampleProvider? _playingLoop;
    private VolumeSampleProvider? _playingVolume;
    private bool _active;
    private int _loadedIndex = -1;

    public JukeboxAudio(GameAudioEngine engine)
    {
        _engine = engine;
    }

    public bool IsActive => _active;

    /// <summary>Called every frame. Starts/switches the selected track, or stops if the jukebox is
    /// off or the ship has none at all.</summary>
    public void Update(JukeboxState? jukebox)
    {
        if (jukebox is null || !jukebox.On)
        {
            Stop();
            return;
        }

        var index = Wrap(jukebox.TrackIndex, JukeboxTracks.All.Length);
        if ((!_active || index != _loadedIndex) && LoadTrack(index) is { } track)
        {
            try
            {
                StopCurrent();
                track.Reader.Position = 0;
                _playingLoop = new LoopingSampleProvider(track.Sample, track.Reader);
                _playingVolume = new VolumeSampleProvider(_playingLoop)
                {
                    Volume = MathHelper.Clamp(jukebox.Volume / 100f, 0f, 1f) * _engine.MusicVolume,
                };
                _engine.AddMixerInput(_playingVolume);
                _active = true;
                _loadedIndex = index;
            }
            catch (Exception)
            {
                // Some machines have no working audio device at all - fall back to silence.
                _active = false;
                _loadedIndex = -1;
            }
        }

        if (_active && _playingVolume is not null)
            _playingVolume.Volume = MathHelper.Clamp(jukebox.Volume / 100f, 0f, 1f) * _engine.MusicVolume;
    }

    private (ISampleProvider Sample, WaveStream Reader)? LoadTrack(int index)
    {
        if (_loaded.TryGetValue(index, out var cached))
            return cached;

        var path = Path.Combine(GameAudioEngine.ContentRoot, JukeboxTracks.All[index].AssetName.Replace('/', Path.DirectorySeparatorChar) + ".mp3");
        var opened = AudioFile.TryOpen(path);
        var track = opened is { } o ? (AudioFile.ToMasterFormat(o.Sample), o.Reader) : ((ISampleProvider, WaveStream)?)null;
        _loaded[index] = track;
        return track;
    }

    public void Stop()
    {
        if (!_active)
            return;
        _active = false;
        _loadedIndex = -1;
        StopCurrent();
    }

    private void StopCurrent()
    {
        if (_playingVolume is not null)
            _engine.RemoveMixerInput(_playingVolume);
        _playingLoop = null;
        _playingVolume = null;
    }

    private static int Wrap(int value, int count) => count <= 0 ? 0 : ((value % count) + count) % count;
}
