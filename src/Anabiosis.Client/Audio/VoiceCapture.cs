using System;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Audio;

// Push-to-talk mic capture for the crew voice chat (direct user request, "как в Баротравме", both
// local and radio modes), with an optional voice-activation mode as an alternative to holding V
// (direct user follow-up) - radio (R) always stays push-to-talk regardless of that setting, since
// transmitting to the whole ship should stay a deliberate action, not something that fires itself
// the moment the mic picks up ambient noise. Null-safe throughout, since a dev machine or a real
// player may simply have no capture device, and this must never crash the game either way.
//
// Direct user report ("почему в устройствах ввода я не могу найти микрофон наушников?") - this used
// to wrap MonoGame's own Microphone class, whose device enumeration is a separate, older API from
// the WASAPI one GameAudioEngine already uses for output; several real headsets (confirmed: a Jabra
// USB headset already proven visible on the output side) simply never show up through it. Rewritten
// on NAudio's WasapiCapture instead, enumerated the exact same way as output (GameAudioEngine.
// EnumerateInputDevices) - one consistent device list for both directions.
public sealed class VoiceCapture
{
    private WasapiCapture? _capture;
    private bool _isRadio;
    private byte[]? _pendingChunk;
    private int _pendingSampleRate;

    // Set from Settings (Game1.ApplyGraphicsSettings) each time it changes - read by OnDataAvailable
    // to decide whether a quiet buffer should still be sent (push-to-talk: yes, the key already
    // gated it) or held back (voice activation: only once it's loud enough).
    public bool VoiceActivationMode { get; set; }
    public float VoiceActivationThreshold { get; set; } = 0.12f;

    // Direct user request ("устройство ввода" dropdown) - the friendly Name of the microphone to
    // open, matched against the same WASAPI capture-endpoint list GameAudioEngine.
    // EnumerateInputDevices returns (null/not-found falls back to the OS's own default capture
    // device, same "never just go silent over a stale saved choice" contract AudioEngine.
    // SelectOutputDevice already uses for output).
    public string? SelectedMicrophoneName { get; set; }

    // Direct user request ("громкость микрофона" up to 900%) - a software gain applied to the
    // captured PCM buffer before it's sent, same decode/scale/soft-clip/re-encode shape
    // RadioVoiceFilter.cs already uses for the radio's own drive stage. 1 = unchanged.
    public float MicGainMultiplier { get; set; } = 1f;

    // Direct user request ("предотвращение отключения") - once voice-activation transmission has
    // started, keeps IsTransmitting true for this many milliseconds after the signal drops back
    // below threshold, so a sentence doesn't get chopped off mid-word during a brief natural pause.
    public float DisconnectPreventionMs { get; set; } = 200f;
    private double _lastLoudAt = double.NegativeInfinity;

    public bool IsAvailable
    {
        get
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                return enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).Any();
            }
            catch
            {
                return false;
            }
        }
    }

    public bool IsRecording { get; private set; }
    public bool IsRadio => _isRadio;
    // True only once the mic is both open AND (in voice-activation mode) actually above threshold -
    // what the HUD label ("ГОВОРИТ"/"РАЦИЯ", Game1.cs) should key off instead of IsRecording, or a
    // voice-activation session would show as permanently "talking" the instant it opens.
    public bool IsTransmitting { get; private set; }

    // Shared by VoiceCapture and MicMonitor (its own local calibration loopback) - name null/empty
    // or not found falls back to the OS's own default capture device.
    internal static MMDevice? ResolveMicrophoneDevice(string? name)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            MMDevice? device = null;
            if (name is { Length: > 0 })
                device = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .FirstOrDefault(d => d.FriendlyName == name);
            device ??= enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            return device;
        }
        catch
        {
            return null;
        }
    }

    public void BeginTalking(bool isRadio)
    {
        if (IsRecording)
            return;
        var device = ResolveMicrophoneDevice(SelectedMicrophoneName);
        if (device is null)
            return;
        _isRadio = isRadio;
        try
        {
            _capture = new WasapiCapture(device, false, 100);
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
            IsRecording = true;
            // Push-to-talk (and radio, always) starts "transmitting" the instant the key goes down -
            // only local voice activation waits for a buffer to actually cross the threshold first.
            IsTransmitting = isRadio || !VoiceActivationMode;
            _lastLoudAt = double.NegativeInfinity;
        }
        catch
        {
            // A real device that refuses to start (in use elsewhere, driver hiccup) should just
            // silently mean "no voice this time", not crash the session.
            if (_capture is not null)
                _capture.DataAvailable -= OnDataAvailable;
            _capture?.Dispose();
            _capture = null;
        }
    }

    public void StopTalking()
    {
        if (_capture is null)
            return;
        var capture = _capture;
        _capture = null;
        capture.DataAvailable -= OnDataAvailable;
        try { capture.StopRecording(); } catch { /* already stopped/disconnected - fine either way */ }
        try { capture.Dispose(); } catch { }
        IsRecording = false;
        IsTransmitting = false;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_capture is null || e.BytesRecorded <= 0)
            return;
        var chunk = ConvertToMono16(e.Buffer, e.BytesRecorded, _capture.WaveFormat);
        if (chunk.Length == 0)
            return;

        if (MicGainMultiplier != 1f)
            ApplyGain(chunk, MicGainMultiplier);

        // Voice activation only ever gates the LOCAL channel - radio got here by holding R, which
        // is already the deliberate action voice activation exists to replace for V.
        if (!_isRadio && VoiceActivationMode)
        {
            var loudEnough = Rms16(chunk) >= VoiceActivationThreshold;
            var nowMs = Environment.TickCount64;
            if (loudEnough)
                _lastLoudAt = nowMs;
            IsTransmitting = loudEnough || nowMs - _lastLoudAt <= DisconnectPreventionMs;
            if (!IsTransmitting)
                return; // stay silent rather than uploading empty-ish background noise
        }

        _pendingChunk = chunk;
        _pendingSampleRate = _capture.WaveFormat.SampleRate;
    }

    // WASAPI shared-mode capture hands back whatever the device's own native mix format is (commonly
    // 32-bit IEEE float, sometimes 16/24-bit int, often stereo) - every downstream consumer
    // (RadioVoiceFilter, VoicePlayback, the network payload itself) already assumes 16-bit
    // little-endian mono PCM, the one format MonoGame's old Microphone class always handed back, so
    // this normalizes to that instead of touching any of those consumers. Channels beyond mono are
    // averaged down rather than just taking the first, so a stereo mic doesn't silently drop one side.
    internal static byte[] ConvertToMono16(byte[] raw, int bytesRecorded, WaveFormat format)
    {
        var bytesPerSample = format.BitsPerSample / 8;
        var channels = Math.Max(1, format.Channels);
        var frameSize = bytesPerSample * channels;
        if (frameSize <= 0)
            return Array.Empty<byte>();
        var frameCount = bytesRecorded / frameSize;
        var output = new byte[frameCount * 2];
        for (var frame = 0; frame < frameCount; frame++)
        {
            var sum = 0f;
            for (var channel = 0; channel < channels; channel++)
                sum += ReadSample(raw, frame * frameSize + channel * bytesPerSample, format);
            var pcm16 = (short)Math.Clamp(sum / channels * 32767f, short.MinValue, short.MaxValue);
            output[frame * 2] = (byte)(pcm16 & 0xFF);
            output[frame * 2 + 1] = (byte)((pcm16 >> 8) & 0xFF);
        }
        return output;
    }

    // Returns a single sample normalized to -1..1, regardless of the native encoding/bit depth.
    private static float ReadSample(byte[] raw, int offset, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
            return BitConverter.ToSingle(raw, offset);
        if (format.BitsPerSample == 16)
            return BitConverter.ToInt16(raw, offset) / 32768f;
        if (format.BitsPerSample == 24)
        {
            var sample = raw[offset] | (raw[offset + 1] << 8) | (raw[offset + 2] << 16);
            if ((sample & 0x800000) != 0)
                sample |= unchecked((int)0xFF000000); // sign-extend the 24-bit value
            return sample / 8388608f;
        }
        if (format.BitsPerSample == 32)
            return BitConverter.ToInt32(raw, offset) / 2147483648f;
        return 0f;
    }

    // In-place decode/scale/soft-clip/re-encode - mirrors RadioVoiceFilter.Apply's own shape. A soft
    // (tanh) clip rather than a hard one, so a gain pushed well past what the signal can take
    // saturates instead of wrapping around into harsh digital noise. Internal (not private) so
    // MicMonitor.cs can apply the exact same gain curve to its own local-loopback preview.
    internal static void ApplyGain(byte[] pcm16, float gain)
    {
        for (var i = 0; i + 1 < pcm16.Length; i += 2)
        {
            var sample = (short)(pcm16[i] | (pcm16[i + 1] << 8));
            var boosted = sample / 32768f * gain;
            var clipped = MathF.Tanh(boosted);
            var outSample = (short)Math.Clamp(clipped * 32767f, short.MinValue, short.MaxValue);
            pcm16[i] = (byte)(outSample & 0xFF);
            pcm16[i + 1] = (byte)((outSample >> 8) & 0xFF);
        }
    }

    // Root-mean-square of a 16-bit little-endian PCM buffer, normalised to 0..1 against the format's
    // own full-scale amplitude - cheap enough to run once per 100ms buffer without profiling it.
    private static float Rms16(byte[] pcm16)
    {
        if (pcm16.Length < 2)
            return 0f;
        double sumSquares = 0;
        var sampleCount = pcm16.Length / 2;
        for (var i = 0; i < sampleCount; i++)
        {
            var sample = (short)(pcm16[i * 2] | (pcm16[i * 2 + 1] << 8));
            var normalized = sample / 32768.0;
            sumSquares += normalized * normalized;
        }
        return (float)Math.Sqrt(sumSquares / sampleCount);
    }

    /// <summary>Current input level, 0..1 - drives the settings screen's live level indicator over
    /// the noise-threshold slider (direct user request, matches the reference screenshot's own
    /// running orange bar). Just re-reads the last captured buffer's own RMS, no separate polling.</summary>
    public float CurrentLevel => _pendingChunk is { } chunk ? Rms16(chunk) : 0f;

    // Consumes whatever chunk has accumulated since the last call - matches the project's own
    // "capture, send once, clear" outgoing-field lifecycle (same shape as _pendingChatMessage).
    public VoiceChunkPayload? TakePendingChunk()
    {
        if (_pendingChunk is null)
            return null;
        var payload = new VoiceChunkPayload(_pendingChunk, _pendingSampleRate, _isRadio);
        _pendingChunk = null;
        return payload;
    }
}
