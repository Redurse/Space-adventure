using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Anabiosis.Client.Audio;

// Direct user request ("сделай чтобы 2 вкладка настроек была почти точь в точь как в Баротравме") -
// specifically the "УСТРОЙСТВО ВЫВОДА" (output device) picker. MonoGame's own SoundEffect/MediaPlayer
// always play through whichever device was the OS default at engine startup - there is no API to
// redirect them to a chosen device - so this class replaces MonoGame's audio OUTPUT entirely with a
// small NAudio/WASAPI mixer: every sound (SFX, music, jukebox, incoming voice) is added here as an
// ISampleProvider, mixed into one master bus, and that bus is what actually plays through the
// selected WasapiOut device. GameSounds/GameMusic/JukeboxAudio/VoicePlayback keep their own existing
// public API shapes; only what feeds their audio out to the speakers changes.
public sealed class GameAudioEngine : IDisposable
{
    public const int SampleRate = 44100;
    public static readonly WaveFormat MasterFormat = WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2);

    // Content/Sounds and Content/Music no longer go through the MonoGame Content pipeline
    // (Content.mgcb) - they're plain copied files next to the built exe now (Anabiosis.Client.csproj),
    // read directly here instead of via ContentManager.
    public static readonly string ContentRoot = Path.Combine(AppContext.BaseDirectory, "Content");

    private readonly MixingSampleProvider _master;
    private readonly VolumeSampleProvider _muteGate;
    private readonly SoftLimiterSampleProvider _limiter;
    private WasapiOut? _output;
    private string? _currentDeviceId;

    // Per-bus volumes (direct user request - separate sliders for "Громкость звуков"/"...музыки"/
    // "...интерфейса"/"...голосового чата", where MonoGame's SoundEffect.MasterVolume used to be one
    // flat multiplier over everything but music). Read live by whichever class owns a given sound's
    // VolumeSampleProvider - see GameSounds/GameMusic/JukeboxAudio/VoicePlayback.
    public float SoundVolume = 1f;
    public float MusicVolume = 1f;
    public float UiVolume = 1f;
    public float VoiceVolume = 1f;

    // While true (Game1 sets this from Game.IsActive each frame, gated by the "Заглушить при
    // переключении окна" checkbox), the whole mix goes silent without stopping/tearing down anything
    // actually playing - resuming focus just un-mutes.
    public bool Muted
    {
        get => _muteGate.Volume <= 0f;
        set => _muteGate.Volume = value ? 0f : 1f;
    }

    // Simple soft-limiter over the WHOLE mix (direct user decision - a real multi-band compressor is
    // out of scope, this is the "chestbox" checkbox's honest, cheaper equivalent). Off by default,
    // same as Barotrauma's own checkbox.
    public bool DynamicRangeCompression
    {
        get => _limiter.Enabled;
        set => _limiter.Enabled = value;
    }

    // Direct user request ("Приоритет гол. чата") - ducks the Sound/Music/UI buses (not voice itself)
    // while VoicePlayback says someone is actively talking. 1 = no ducking, smaller = quieter.
    public float DuckMultiplier { get; set; } = 1f;

    public string? CurrentDeviceId => _currentDeviceId;

    public GameAudioEngine()
    {
        _master = new MixingSampleProvider(MasterFormat) { ReadFully = true };
        _muteGate = new VolumeSampleProvider(_master) { Volume = 1f };
        _limiter = new SoftLimiterSampleProvider(_muteGate);
        SelectOutputDevice(null);
    }

    // No live hot-plug event is worth holding open permanently (same reasoning Barotrauma's own
    // "Обновить устройства" button implies) - re-enumerating on demand is cheap and exactly what that
    // button does.
    public IReadOnlyList<(string Id, string Name)> EnumerateOutputDevices()
    {
        var list = new List<(string, string)>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                list.Add((device.ID, device.FriendlyName));
                device.Dispose();
            }
        }
        catch
        {
            // No WASAPI render endpoints at all (odd machine/driver state) - an empty list just
            // means the dropdown shows nothing to pick, not a crash.
        }
        return list;
    }

    // Sentinel rather than null - null is itself a meaningful, real "use the system default" request
    // (ApplyGraphicsSettings' very first call, before anything has ever been selected, has to be
    // able to tell "never asked yet" apart from "explicitly asked for the default").
    private const string NeverRequested = "\0never-requested";
    private string? _lastRequestedDeviceId = NeverRequested;

    // deviceId null (or no longer present - unplugged since it was chosen) falls back to the current
    // OS default output, same "never just go silent because of a stale saved id" contract
    // GetOrCreateStation's own anchor-lookup fallback uses elsewhere in this project. A no-op if
    // this exact id was already the last one requested - ApplyGraphicsSettings calls this on every
    // "Применить", not just when the device picker actually changed, and tearing down/rebuilding
    // WasapiOut for an unrelated setting (bloom, particles, ...) would glitch the audio for nothing.
    public void SelectOutputDevice(string? deviceId)
    {
        if (deviceId == _lastRequestedDeviceId)
            return;
        _lastRequestedDeviceId = deviceId;

        _output?.Stop();
        _output?.Dispose();
        _output = null;
        _currentDeviceId = null;

        MMDevice? device = null;
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            if (deviceId is not null)
                device = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
                    .FirstOrDefault(d => d.ID == deviceId);
            device ??= enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            device = null;
        }
        if (device is null)
            return; // no output device at all on this machine - stay silent rather than throw

        try
        {
            _output = new WasapiOut(device, AudioClientShareMode.Shared, true, 100);
            _output.Init(_limiter);
            _output.Play();
            _currentDeviceId = device.ID;
        }
        catch
        {
            // A device that refuses to open (in exclusive use elsewhere, driver hiccup) should mean
            // "no sound this session", not crash the game - same contract VoiceCapture.BeginTalking
            // already keeps for a misbehaving microphone.
            _output?.Dispose();
            _output = null;
        }
    }

    // Direct user report ("почему в устройствах ввода я не могу найти микрофон наушников?") -
    // MonoGame's own Microphone.All (what VoiceCapture/MicMonitor/the Settings screen used to
    // enumerate input devices with) wraps an older, more limited capture-device API that a lot of
    // modern USB/Bluetooth headsets simply never register with, even though Windows itself (and
    // this same WASAPI enumerator, already proven to see this exact headset on the OUTPUT side
    // above) sees them fine. Mirrors EnumerateOutputDevices exactly, just DataFlow.Capture.
    public IReadOnlyList<(string Id, string Name)> EnumerateInputDevices()
    {
        var list = new List<(string, string)>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                list.Add((device.ID, device.FriendlyName));
                device.Dispose();
            }
        }
        catch
        {
            // No WASAPI capture endpoints at all (odd machine/driver state) - an empty list just
            // means the dropdown shows nothing to pick, not a crash.
        }
        return list;
    }

    public void AddMixerInput(ISampleProvider provider) => _master.AddMixerInput(provider);
    public void RemoveMixerInput(ISampleProvider provider) => _master.RemoveMixerInput(provider);

    // Every currently-playing one-shot SFX (GameSounds.Play), tracked just so Sweep below can
    // unregister + dispose the ones that have finished - a one-shot never needs a caller-visible
    // handle the way CreateLoop's return value does.
    private readonly List<(OneShotSampleProvider Provider, IDisposable Reader)> _activeOneShots = new();

    public void PlayOneShot(ISampleProvider chain, IDisposable reader)
    {
        var oneShot = new OneShotSampleProvider(chain);
        _activeOneShots.Add((oneShot, reader));
        _master.AddMixerInput(oneShot);
    }

    // Called once per frame from Game1.Update - removes+disposes whatever finished playing since the
    // last sweep. A one-shot SFX is a second or two long at most, so a per-frame sweep never lets
    // more than a handful accumulate.
    public void SweepFinishedOneShots()
    {
        for (var i = _activeOneShots.Count - 1; i >= 0; i--)
        {
            var (provider, reader) = _activeOneShots[i];
            if (!provider.Finished)
                continue;
            _master.RemoveMixerInput(provider);
            reader.Dispose();
            _activeOneShots.RemoveAt(i);
        }
    }

    public void Dispose()
    {
        _output?.Stop();
        _output?.Dispose();
        foreach (var (provider, reader) in _activeOneShots)
        {
            _master.RemoveMixerInput(provider);
            reader.Dispose();
        }
        _activeOneShots.Clear();
    }
}
