using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Anabiosis.Client.Audio;

// The main menu's theme (direct user request: "IRON NEST - Launch Trailer OST" plays in the game
// menu). One track on a seamless loop while no round is live, faded in when the menu opens and out
// when a round starts - GameMusic stays the round's own music and never plays here.
public sealed class MenuMusic
{
    private const string TrackName = "menu_theme";
    private const float Level = 0.7f;
    private const float FadeInSeconds = 1.5f;
    private const float FadeOutSeconds = 0.6f;

    private readonly GameAudioEngine _engine;
    private readonly VolumeSampleProvider? _volume;
    private readonly WaveStream? _reader;

    private bool _inMixer;
    private float _fade;
    private float _master = 1f;
    private double _lastUpdateSeconds = double.NaN;

    public MenuMusic(GameAudioEngine engine)
    {
        _engine = engine;
        var path = System.IO.Path.Combine(GameAudioEngine.ContentRoot, "Music", TrackName + ".mp3");
        // A missing file costs the menu its theme, never the game - same contract as GameMusic.
        if (AudioFile.TryOpen(path) is { } opened)
        {
            _reader = opened.Reader;
            _volume = new VolumeSampleProvider(new LoopingSampleProvider(AudioFile.ToMasterFormat(opened.Sample), opened.Reader)) { Volume = 0f };
        }
    }

    public bool Available => _volume is not null;

    /// <summary>Whether the theme is mixed in right now (public for the audio checks).</summary>
    public bool IsPlaying => _inMixer;

    /// <summary>The settings screen's music volume, applied on top of the theme's own level.</summary>
    public void SetMasterVolume(float master) => _master = Math.Clamp(master, 0f, 1f);

    /// <summary>Called every frame. wanted = "the menu is showing" (no round live).</summary>
    public void Update(double nowSeconds, bool wanted)
    {
        if (_volume is null)
            return;

        var dt = double.IsNaN(_lastUpdateSeconds) ? 0f : (float)Math.Clamp(nowSeconds - _lastUpdateSeconds, 0.0, 0.25);
        _lastUpdateSeconds = nowSeconds;

        if (wanted)
        {
            if (!_inMixer)
            {
                _reader!.Position = 0;
                _fade = 0f;
                _engine.AddMixerInput(_volume);
                _inMixer = true;
            }
            _fade = Math.Min(1f, _fade + dt / FadeInSeconds);
        }
        else if (_inMixer)
        {
            _fade = Math.Max(0f, _fade - dt / FadeOutSeconds);
            if (_fade <= 0f)
            {
                _engine.RemoveMixerInput(_volume);
                _inMixer = false;
            }
        }

        _volume.Volume = Level * _fade * _master * _engine.MusicVolume;
    }
}
