using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;

namespace SpaceAdventure.Client.Audio;

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
// players read as a bug ("why this one again"), while a bag guarantees all seven come round before
// any repeats - with a check that a reshuffle cannot put the same track twice across the seam.
public sealed class GameMusic
{
    // Asset names under Content/Music. The five ambiences were 15-second loops; they are stored
    // repeated to just over 75 seconds so a single play is worth starting.
    private static readonly string[] TrackNames =
    {
        "space_ambience_1", "space_ambience_2", "space_ambience_3", "space_ambience_4",
        "space_ambience_5", "fantasy_space", "deep_space",
    };

    // How long the silence between two tracks lasts. Wide on purpose - a fixed gap is a metronome.
    private const double MinGapSeconds = 32.0;
    private const double MaxGapSeconds = 115.0;

    // And before the first one, so a round does not open on a musical cue every single time.
    private const double MinFirstGapSeconds = 10.0;
    private const double MaxFirstGapSeconds = 40.0;

    // Music sits under the effects. It is the bed, not the event.
    private const float MusicLevel = 0.55f;

    // MediaPlayer reports Stopped for a moment after Play as well as after a track finishes, so a
    // track is not tested for having ended until it has plausibly started.
    private const double SettleSeconds = 1.5;

    private readonly List<Song> _songs = new();
    private readonly List<int> _bag = new();
    private readonly Random _random = new();

    private bool _running;
    private int _playing = -1;
    private int _lastTaken = -1;
    private double _startedAt;
    private double _nextStartAt;
    private float _master = 1f;

    public GameMusic(ContentManager content)
    {
        foreach (var name in TrackNames)
        {
            try
            {
                _songs.Add(content.Load<Song>("Music/" + name));
            }
            catch (Exception)
            {
                // A missing track costs that track, never the game - the same contract GameSounds
                // and Shaders.TryLoad keep.
            }
        }
    }

    public bool Available => _songs.Count > 0;

    /// <summary>How many tracks actually loaded. Public so the check in SpaceAdventure.ShaderCheck can
    /// tell "the content build dropped the music" apart from "the music is meant to be silent".</summary>
    public int TrackCount => _songs.Count;

    /// <summary>The settings screen's master volume, applied on top of the music's own level.</summary>
    public void SetMasterVolume(float master)
    {
        _master = Math.Clamp(master, 0f, 1f);
        if (_running)
            MediaPlayer.Volume = MusicLevel * _master;
    }

    /// <summary>Called every frame while a round is live. Idempotent on the first call.</summary>
    public void Update(double nowSeconds)
    {
        if (!Available)
            return;

        if (!_running)
        {
            _running = true;
            MediaPlayer.IsRepeating = false;
            MediaPlayer.Volume = MusicLevel * _master;
            _nextStartAt = nowSeconds + Gap(MinFirstGapSeconds, MaxFirstGapSeconds);
            return;
        }

        if (_playing >= 0)
        {
            // Still going, or too soon to tell.
            if (nowSeconds - _startedAt < SettleSeconds || MediaPlayer.State == MediaState.Playing)
                return;
            _playing = -1;
            _nextStartAt = nowSeconds + Gap(MinGapSeconds, MaxGapSeconds);
            return;
        }

        if (nowSeconds < _nextStartAt)
            return;

        var next = TakeFromBag();
        try
        {
            MediaPlayer.Volume = MusicLevel * _master;
            MediaPlayer.Play(_songs[next]);
            _playing = next;
            _startedAt = nowSeconds;
        }
        catch (Exception)
        {
            // Some machines have no media stack at all. Fall back to silence rather than retrying
            // every frame forever.
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
        try
        {
            MediaPlayer.Stop();
        }
        catch (Exception)
        {
            // Nothing to do about it, and nothing that depends on it.
        }
    }

    private double Gap(double min, double max) => min + _random.NextDouble() * (max - min);

    private int TakeFromBag()
    {
        if (_bag.Count == 0)
        {
            for (var i = 0; i < _songs.Count; i++)
                _bag.Add(i);
            for (var i = _bag.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
            }
            // The seam between two bags is the one place a shuffle can still repeat: if the fresh
            // bag ends with what the last one ended with, that track plays twice in a row.
            if (_songs.Count > 1 && _bag[^1] == _lastTaken)
                (_bag[^1], _bag[0]) = (_bag[0], _bag[^1]);
        }

        var index = _bag[^1];
        _bag.RemoveAt(_bag.Count - 1);
        _lastTaken = index;
        return index;
    }
}
