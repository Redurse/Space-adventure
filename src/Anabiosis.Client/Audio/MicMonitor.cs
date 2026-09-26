using System;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Anabiosis.Client.Audio;

// Direct user request ("в Baротравме, когда настраиваешь голосовой чат, микрофон активируется и
// передаёт звук в уши") - a local monitor for the Settings screen's own Audio tab, so a player can
// hear themselves while picking a microphone and setting the noise threshold, same as Barotrauma's
// own calibration screen. Deliberately NOT the multiplayer voice chat path (VoiceCapture/
// VoicePlayback) - this never touches the network or the server at all; it's a direct local loop,
// mic in one ear, straight back out the other, active only while Game1's own Settings > Audio tab
// is open (Game1.cs decides when to Start/Stop this each frame). Same NAudio/WasapiCapture basis as
// VoiceCapture (moved off MonoGame's own Microphone class - see VoiceCapture's own doc comment) so
// this hears the exact same device list the "УСТРОЙСТВО ВВОДА" stepper shows.
public sealed class MicMonitor
{
    private readonly GameAudioEngine _engine;
    private WasapiCapture? _capture;
    private BufferedWaveProvider? _buffer;
    private VolumeSampleProvider? _volume;
    private ISampleProvider? _mixerInput;
    private int _sampleRate;

    public bool IsActive { get; private set; }
    public string? SelectedMicrophoneName { get; set; }
    public float GainMultiplier { get; set; } = 1f;
    public float Volume { get; set; } = 1f;
    private string? _openedMicrophoneName;

    public MicMonitor(GameAudioEngine engine)
    {
        _engine = engine;
    }

    // Safe to call every frame while the Audio tab is open - a no-op once already listening to the
    // currently-selected device, but reopens on the CORRECT device the instant the player cycles the
    // "УСТРОЙСТВО ВВОДА" stepper to a different one, same live-preview feel as every slider here.
    public void Start()
    {
        if (IsActive && _openedMicrophoneName == SelectedMicrophoneName)
            return;
        if (IsActive)
            Stop();

        var device = VoiceCapture.ResolveMicrophoneDevice(SelectedMicrophoneName);
        if (device is null)
            return;

        try
        {
            // Same event-driven-instead-of-polling fix as VoiceCapture.BeginTalking (its own doc
            // comment has the full reasoning) - this is the identical capture setup, just for the
            // Settings screen's own local self-listen loopback instead of the network voice path.
            _capture = new WasapiCapture(device, true, 100);
            _sampleRate = 0; // forced fresh on the first buffer, since the real rate is only known once DataAvailable fires
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
            IsActive = true;
            _openedMicrophoneName = SelectedMicrophoneName;
        }
        catch
        {
            // Same "no device this time, not a crash" contract VoiceCapture.BeginTalking keeps.
            if (_capture is not null)
                _capture.DataAvailable -= OnDataAvailable;
            _capture?.Dispose();
            _capture = null;
        }
    }

    public void Stop()
    {
        if (!IsActive)
            return;
        IsActive = false;
        _openedMicrophoneName = null;
        var capture = _capture;
        _capture = null;
        if (capture is not null)
        {
            capture.DataAvailable -= OnDataAvailable;
            try { capture.StopRecording(); } catch { /* already stopped/disconnected - fine either way */ }
            try { capture.Dispose(); } catch { }
        }

        if (_mixerInput is not null)
            _engine.RemoveMixerInput(_mixerInput);
        _buffer = null;
        _volume = null;
        _mixerInput = null;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_capture is null || e.BytesRecorded <= 0)
            return;
        var chunk = VoiceCapture.ConvertToMono16(e.Buffer, e.BytesRecorded, _capture.WaveFormat);
        if (chunk.Length == 0)
            return;

        if (GainMultiplier != 1f)
            VoiceCapture.ApplyGain(chunk, GainMultiplier);

        var sampleRate = _capture.WaveFormat.SampleRate;
        if (_buffer is null || _sampleRate != sampleRate)
        {
            if (_mixerInput is not null)
                _engine.RemoveMixerInput(_mixerInput);
            _sampleRate = sampleRate;
            _buffer = new BufferedWaveProvider(new WaveFormat(_sampleRate, 16, 1))
            {
                DiscardOnBufferOverflow = true,
                BufferDuration = TimeSpan.FromSeconds(1),
            };
            ISampleProvider chain = new MonoToStereoSampleProvider(_buffer.ToSampleProvider());
            if (_sampleRate != GameAudioEngine.SampleRate)
                chain = new WdlResamplingSampleProvider(chain, GameAudioEngine.SampleRate);
            _volume = new VolumeSampleProvider(chain) { Volume = Volume };
            _mixerInput = _volume;
            _engine.AddMixerInput(_mixerInput);
        }
        if (_volume is not null)
            _volume.Volume = Volume;
        if (_buffer.BufferedDuration < TimeSpan.FromMilliseconds(500))
            _buffer.AddSamples(chunk, 0, chunk.Length);
    }
}
