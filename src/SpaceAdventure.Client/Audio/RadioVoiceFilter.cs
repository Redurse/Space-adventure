using System;

namespace SpaceAdventure.Client.Audio;

// Makes a radio transmission actually SOUND like it's coming through a radio (direct user
// request: "накладывался режим, как будто игроки реально общаются через рацию") - a classic
// telephone/radio voice-band bandpass (cuts below ~300Hz and above ~2800Hz) plus a soft-clip/gain
// boost (real radio audio is heavily compressed and mildly distorted) plus a little broadband
// static mixed in. STATEFUL across chunks (the high-pass/low-pass are one-pole IIR filters with
// memory) - one instance must live per remote speaker's radio stream for as long as you're
// hearing them, never reset per chunk, or every chunk boundary would click audibly.
//
// NOTE (left for the user to tune by ear - this can't be judged without real audio hardware/
// playback, see the feature's own report): DriveAmount (2.4f) controls distortion intensity,
// StaticLevel (0.02f) controls static loudness. Reasonable first-pass values, not final.
public sealed class RadioVoiceFilter
{
    private const float DriveAmount = 2.4f;
    private const float StaticLevel = 0.02f;

    private readonly float _hpAlpha;
    private readonly float _lpAlpha;
    private float _hpPrevIn, _hpPrevOut;
    private float _lpPrevOut;
    private readonly Random _noise = new();

    public RadioVoiceFilter(int sampleRate)
    {
        _hpAlpha = OnePoleHighPassAlpha(300f, sampleRate);
        _lpAlpha = OnePoleLowPassAlpha(2800f, sampleRate);
    }

    private static float OnePoleHighPassAlpha(float cutoffHz, int sampleRate)
    {
        var rc = 1f / (2f * MathF.PI * cutoffHz);
        var dt = 1f / sampleRate;
        return rc / (rc + dt);
    }

    private static float OnePoleLowPassAlpha(float cutoffHz, int sampleRate)
    {
        var rc = 1f / (2f * MathF.PI * cutoffHz);
        var dt = 1f / sampleRate;
        return dt / (rc + dt);
    }

    public void Apply(byte[] pcmBytes)
    {
        for (var i = 0; i + 1 < pcmBytes.Length; i += 2)
        {
            var sample = (short)(pcmBytes[i] | (pcmBytes[i + 1] << 8));
            var x = sample / 32768f;

            var hp = _hpAlpha * (_hpPrevOut + x - _hpPrevIn);
            _hpPrevIn = x;
            _hpPrevOut = hp;

            var lp = _lpPrevOut + _lpAlpha * (hp - _lpPrevOut);
            _lpPrevOut = lp;

            var boosted = lp * DriveAmount;
            var clipped = MathF.Tanh(boosted);
            var withStatic = clipped * (1f - StaticLevel) + ((float)_noise.NextDouble() * 2f - 1f) * StaticLevel;

            var outSample = (short)Math.Clamp(withStatic * 32767f, short.MinValue, short.MaxValue);
            pcmBytes[i] = (byte)(outSample & 0xFF);
            pcmBytes[i + 1] = (byte)((outSample >> 8) & 0xFF);
        }
    }
}
