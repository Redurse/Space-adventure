using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Anabiosis.Client.Audio;

// One place that owns every sound effect, so adding the twentieth does not mean adding a twentieth
// field, a twentieth try/catch and a twentieth null check at the call site.
//
// Three things every game needs from a sound layer and none of them are the loading:
//
//   * Pitch variation. The same file fired twice in a row is instantly recognisable as a file. A
//     few percent of random detune per shot is the difference between a footstep and a machine gun
//     made of one footstep.
//   * Throttling. Several doors closing on the same tick must not play three copies at full volume
//     into each other - that is not louder, it is distorted.
//   * Failing quietly. A missing file costs the sound, never the game, exactly like Shaders.TryLoad
//     does for effects.
//
// Routes through GameAudioEngine now (direct user request, real output-device selection - see
// GameAudioEngine's own doc comment) instead of MonoGame's SoundEffect - each Play call opens its own
// fresh AudioFile.TryOpen so overlapping plays of the same clip (two doors on one tick) get
// independent playback positions, the same way SoundEffect.Play already gave every call its own
// transient voice.
public sealed class GameSounds
{
    // Names are the asset paths under Content/Sounds, without extension.
    public const string UiClick = "ui_click";
    public const string UiDeny = "ui_deny";
    public const string PanelOpen = "panel_open";
    public const string PanelClose = "panel_close";
    public const string DoorOpen = "door_open";
    public const string DoorClose = "door_close";
    public const string AirlockCycle = "airlock_cycle";
    public const string ItemPickup = "item_pickup";
    public const string ItemDrop = "item_drop";
    public const string HullBreach = "hull_breach";
    public const string LaserShot = "laser_shot";
    public const string RifleShot = "rifle_shot";
    public const string LowOxygen = "low_oxygen";
    public const string WeldLoop = "weld_loop";
    public const string CutterLoop = "cutter_loop";
    public const string AlarmLoop = "alarm_loop";
    public const string JetpackLoop = "jetpack_loop";
    public const string SteamVent = "steam_vent";
    public const string ReactorHumLoop = "reactor_hum_loop";

    private readonly GameAudioEngine _engine;
    private readonly Dictionary<string, double> _lastPlayed = new();
    private readonly Random _random = new();

    // Shortest gap between two plays of the same sound. Anything closer is the same event being
    // reported twice, not two events.
    private const double MinRepeatSeconds = 0.06;

    public GameSounds(GameAudioEngine engine)
    {
        _engine = engine;
    }

    private static string PathFor(string name) => Path.Combine(GameAudioEngine.ContentRoot, "Sounds", name + ".wav");

    // Volume is this sound's own level; GameAudioEngine.SoundVolume (set from the settings screen) scales
    // all of them on top of it, same "per-call level times a live bus multiplier" shape as before.
    public void Play(string name, double nowSeconds, float volume = 1f, float pitchSpread = 0.06f, float pan = 0f) =>
        PlayInternal(name, nowSeconds, volume, pitchSpread, pan, _engine.SoundVolume);

    // Direct user request ("звук как при нажатии на кнопки в баротравме") - button/tab clicks route
    // through the separate "Громкость звуков интерфейса" bus instead of "Громкость звуков"; that bus
    // (GameAudioEngine.UiVolume) already existed from the Audio tab's own Baротравма-reference
    // redesign, it just had no sound actually routed through it yet.
    public void PlayUi(string name, double nowSeconds, float volume = 1f, float pitchSpread = 0.02f) =>
        PlayInternal(name, nowSeconds, volume, pitchSpread, 0f, _engine.UiVolume);

    private void PlayInternal(string name, double nowSeconds, float volume, float pitchSpread, float pan, float busVolume)
    {
        if (_lastPlayed.TryGetValue(name, out var last) && nowSeconds - last < MinRepeatSeconds)
            return;
        if (AudioFile.TryOpen(PathFor(name)) is not { } opened)
            return;

        _lastPlayed[name] = nowSeconds;
        var pitchSemitoneOctaves = pitchSpread <= 0f ? 0f : (float)(_random.NextDouble() * 2 - 1) * pitchSpread;
        var pitchFactor = MathF.Pow(2f, pitchSemitoneOctaves);
        var chain = AudioFile.ToMasterFormat(opened.Sample, pitchFactor, Math.Clamp(pan, -1f, 1f));
        var withVolume = new VolumeSampleProvider(chain) { Volume = Math.Clamp(volume, 0f, 1f) * busVolume * _engine.DuckMultiplier };
        _engine.PlayOneShot(withVolume, opened.Reader);
    }

    // A looping instance the caller owns and starts/stops itself - for the welder, the reactor bed
    // and anything else that runs for as long as a state holds rather than firing once. Not currently
    // called anywhere in the client (dead but present, same as before this rewrite) - kept working
    // rather than removed, since nothing about this rewrite is meant to drop existing surface area.
    public SoundLoop? CreateLoop(string name, float volume = 1f)
    {
        if (AudioFile.TryOpen(PathFor(name)) is not { } opened)
            return null;
        var chain = AudioFile.ToMasterFormat(opened.Sample);
        var looped = new LoopingSampleProvider(chain, opened.Reader);
        var withVolume = new VolumeSampleProvider(looped) { Volume = Math.Clamp(volume, 0f, 1f) * _engine.SoundVolume };
        return new SoundLoop(_engine, withVolume, opened.Reader);
    }
}

// SoundEffectInstance-shaped replacement for CreateLoop's old return type - Play/Stop/Volume, owned
// and driven entirely by the caller, same as before.
public sealed class SoundLoop : IDisposable
{
    private readonly GameAudioEngine _engine;
    private readonly VolumeSampleProvider _provider;
    private readonly IDisposable _reader;
    private bool _playing;

    internal SoundLoop(GameAudioEngine engine, VolumeSampleProvider provider, IDisposable reader)
    {
        _engine = engine;
        _provider = provider;
        _reader = reader;
    }

    public float Volume
    {
        get => _provider.Volume;
        set => _provider.Volume = value;
    }

    public void Play()
    {
        if (_playing)
            return;
        _playing = true;
        _engine.AddMixerInput(_provider);
    }

    public void Stop()
    {
        if (!_playing)
            return;
        _playing = false;
        _engine.RemoveMixerInput(_provider);
    }

    public void Dispose()
    {
        Stop();
        _reader.Dispose();
    }
}
