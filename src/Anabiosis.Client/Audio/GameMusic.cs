using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Anabiosis.Client.Audio;

// Background music for the round, and only for the round: the menu has its own quiet and does not
// want a track starting under it.
//
// Two decisions carry this class.
//
// The gaps matter more than the tracks. Music that plays end to end stops being heard after ten
// minutes and starts being wallpaper; music that arrives out of a long silence is noticed every time.
// So a track plays, then nothing plays for somewhere between half a minute and two, and the silence
// is as much a part of it as the audio.
//
// And the order is a shuffled bag, not a coin flip. Pure random repeats itself in clumps, which
// players read as a bug ("why this one again"), while a bag guarantees all five come round before
// any repeats - with a check that a reshuffle cannot put the same track twice across the seam.
//
// Routes through GameAudioEngine now (direct user request, real output-device selection) instead of
// MonoGame's MediaPlayer/Song - all 5 tracks are opened once at construction (same "load everything
// up front" shape as before) and replayed by rewinding the same reader, rather than reloading from
// disk each time.
public sealed class GameMusic
{
    // Asset names under Content/Music. Five full-length tracks, roughly one to four minutes each -
    // long enough that a single play is worth starting on its own, unlike the stitched-up 15-second
    // loops they replaced.
    private static readonly string[] TrackNames =
    {
        "ambience_engine_room_whales", "ambience_arriving_at_destination",
        "ambience_overclocked_weakness", "ambience_trick_or_trauma", "ambience_monster_nearby",
    };

    // How long the silence between two tracks lasts. Wide on purpose - a fixed gap is a metronome.
    private const double MinGapSeconds = 32.0;
    private const double MaxGapSeconds = 115.0;

    // And before the first one, so a round does not open on a musical cue every single time.
    private const double MinFirstGapSeconds = 10.0;
    private const double MaxFirstGapSeconds = 40.0;

    // Music sits under the effects. It is the bed, not the event.
    private const float MusicLevel = 0.55f;

    // A freshly-started track isn't tested for having ended until it has plausibly started - avoids
    // a false "finished" read from OneShotSampleProvider on the very first Read() call.
    private const double SettleSeconds = 0.5;

    private readonly GameAudioEngine _engine;
    private readonly List<(ISampleProvider Sample, WaveStream Reader)> _tracks = new();
    private readonly List<int> _bag = new();
    private readonly Random _random = new();

    private bool _running;
    private int _playing = -1;
    private int _lastTaken = -1;
    private double _startedAt;
    private double _nextStartAt;
    private float _master = 1f;
    private OneShotSampleProvider? _current;
    private VolumeSampleProvider? _currentVolume;

    public GameMusic(GameAudioEngine engine)
    {
        _engine = engine;
        foreach (var name in TrackNames)
        {
            var path = Path.Combine(GameAudioEngine.ContentRoot, "Music", name + ".mp3");
            if (AudioFile.TryOpen(path) is { } opened)
                _tracks.Add((AudioFile.ToMasterFormat(opened.Sample), opened.Reader));
            // A missing track costs that track, never the game - the same contract GameSounds
            // and Shaders.TryLoad keep.
        }
    }

    public bool Available => _tracks.Count > 0;

    /// <summary>How many tracks actually loaded. Public so the check in Anabiosis.ShaderCheck can
    /// tell "the content build dropped the music" apart from "the music is meant to be silent".</summary>
    public int TrackCount => _tracks.Count;

    /// <summary>Whether a track is actively mixed in right now - same "is it audibly playing" signal
    /// Anabiosis.ShaderCheck used to read off MonoGame's own MediaPlayer.State.</summary>
    public bool IsPlaying => _current is not null;

    /// <summary>The settings screen's music-bus volume, applied on top of the music's own level.</summary>
    public void SetMasterVolume(float master)
    {
        _master = Math.Clamp(master, 0f, 1f);
        if (_currentVolume is not null)
            _currentVolume.Volume = MusicLevel * _master * _engine.MusicVolume;
    }

    /// <summary>Called every frame while a round is live. Idempotent on the first call.</summary>
    public void Update(double nowSeconds)
    {
        if (!Available)
            return;

        if (!_running)
        {
            _running = true;
            _nextStartAt = nowSeconds + Gap(MinFirstGapSeconds, MaxFirstGapSeconds);
            return;
        }

        if (_playing >= 0)
        {
            // Still going, or too soon to tell.
            if (nowSeconds - _startedAt < SettleSeconds || _current is not { Finished: true })
                return;
            StopCurrent();
            _playing = -1;
            _nextStartAt = nowSeconds + Gap(MinGapSeconds, MaxGapSeconds);
            return;
        }

        if (nowSeconds < _nextStartAt)
            return;

        var next = TakeFromBag();
        var (sample, reader) = _tracks[next];
        try
        {
            reader.Position = 0;
            _currentVolume = new VolumeSampleProvider(sample) { Volume = MusicLevel * _master * _engine.MusicVolume };
            _current = new OneShotSampleProvider(_currentVolume);
            _engine.AddMixerInput(_current);
            _playing = next;
            _startedAt = nowSeconds;
        }
        catch (Exception)
        {
            // Some machines have no working audio device at all. Fall back to silence rather than
            // retrying every frame forever.
            _playing = -1;
            _nextStartAt = nowSeconds + MaxGapSeconds;
        }
    }

    /// <summary>Called when the round ends. Safe to call every frame; only acts once.</summary>
    public void Stop()
    {
        if (!_running)
            return;
        _running = false;
        _playing = -1;
        StopCurrent();
    }

    private void StopCurrent()
    {
        if (_current is not null)
            _engine.RemoveMixerInput(_current);
        _current = null;
        _currentVolume = null;
    }

    private double Gap(double min, double max) => min + _random.NextDouble() * (max - min);

    private int TakeFromBag()
    {
        if (_bag.Count == 0)
        {
            for (var i = 0; i < _tracks.Count; i++)
                _bag.Add(i);
            for (var i = _bag.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }
            // The seam between two bags is the one place a shuffle can still repeat: if the fresh
            // bag ends with what the last one ended with, that track plays twice in a row.
            if (_tracks.Count > 1 && _bag[^1] == _lastTaken)
                (_bag[^1], _bag[0]) = (_bag[0], _bag[^1]);
        }

        var index = _bag[^1];
        _bag.RemoveAt(_bag.Count - 1);
        _lastTaken = index;
        return index;
    }
}
