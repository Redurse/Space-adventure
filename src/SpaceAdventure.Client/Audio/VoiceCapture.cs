using System;
using Microsoft.Xna.Framework.Audio;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Audio;

// Push-to-talk mic capture for the crew voice chat (direct user request, "как в Баротравме", both
// local and radio modes). Wraps Microphone.Default - null-safe throughout, since a dev machine or
// a real player may simply have no capture device, and this must never crash the game either way.
public sealed class VoiceCapture
{
    private Microphone? _mic;
    private bool _isRadio;
    private byte[]? _pendingChunk;
    private int _pendingSampleRate;

    public bool IsAvailable => Microphone.Default is not null;
    public bool IsRecording { get; private set; }
    public bool IsRadio => _isRadio;

    public void BeginTalking(bool isRadio)
    {
        if (IsRecording || Microphone.Default is null)
            return;
        _mic = Microphone.Default;
        _isRadio = isRadio;
        try
        {
            _mic.BufferDuration = TimeSpan.FromMilliseconds(100);
            _mic.BufferReady += OnBufferReady;
            _mic.Start();
            IsRecording = true;
        }
        catch
        {
            // A real device that refuses to start (in use elsewhere, driver hiccup) should just
            // silently mean "no voice this time", not crash the session.
            _mic.BufferReady -= OnBufferReady;
            _mic = null;
        }
    }

    public void StopTalking()
    {
        if (_mic is null)
            return;
        try
        {
            _mic.Stop();
        }
        catch { /* already stopped/disconnected - fine either way */ }
        _mic.BufferReady -= OnBufferReady;
        _mic = null;
        IsRecording = false;
    }

    private void OnBufferReady(object? sender, EventArgs e)
    {
        if (_mic is null)
            return;
        var size = _mic.GetSampleSizeInBytes(_mic.BufferDuration);
        var buffer = new byte[size];
        var read = _mic.GetData(buffer);
        if (read <= 0)
            return;
        _pendingChunk = read == buffer.Length ? buffer : buffer[..read];
        _pendingSampleRate = _mic.SampleRate;
    }

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
