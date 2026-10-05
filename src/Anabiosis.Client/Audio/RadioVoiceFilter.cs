using System;

namespace Anabiosis.Client.Audio;

// Makes a radio transmission actually SOUND like it's coming through a radio (direct user
// request: "накладывался режим, как будто игроки реально общаются через рацию") - a classic
// telephone/radio voice-band bandpass (cuts below ~350Hz and above ~2800Hz) plus a gentle
// soft-clip (real radio audio is compressed and mildly coloured) plus a faint carrier hiss.
// STATEFUL across chunks (the high-pass/low-pass are one-pole IIR filters with memory, and the
// noise gate has an envelope) - one instance must live per remote speaker's radio stream for as
// long as you're hearing them, never reset per chunk, or every chunk boundary would click audibly.
//
// Direct user report ("через рацию какие-то невозможные помехи", local voice being fine): radio is
// the one channel that transmits continuously while R is held, including the pauses between words
// (local voice-activation gates those out), and the old filter then amplified that mic noise floor
// (drive 2.4 + tanh) and laid a constant 2% white-noise hiss over everything. So now: a noise gate
// mutes the pauses, the drive is mild, and the hiss exists only while someone is actually talking
// and at a fraction of its old level.
public sealed class RadioVoiceFilter
{
    private const float DriveAmount = 1.4f;
    private const float StaticLevel = 0.004f;

    // Envelope thresholds (full-scale = 1) of the noise gate: fully closed below GateClosed, fully
    // open above GateOpen, smooth in between so word endings fade instead of chopping.
    private const float GateClosed = 0.006f;
    private const float GateOpen = 0.02f;
    private const float EnvelopeAttackMs = 5f;
    private const float EnvelopeReleaseMs = 180f;

    private readonly float _hpAlpha;
    private readonly float _lpAlpha;
    private readonly float _attack;
    private readonly float _release;
    private float _hpPrevIn, _hpPrevOut;
    private float _lpPrevOut;
    private float _envelope;
    private readonly Random _noise = new();

    public RadioVoiceFilter(int sampleRate)
    {
        _hpAlpha = OnePoleHighPassAlpha(350f, sampleRate);
        _lpAlpha = OnePoleLowPassAlpha(2800f, sampleRate);
        _attack = EnvelopeCoefficient(EnvelopeAttackMs, sampleRate);
        _release = EnvelopeCoefficient(EnvelopeReleaseMs, sampleRate);
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

    private static float EnvelopeCoefficient(float milliseconds, int sampleRate) =>
        1f - MathF.Exp(-1f / (milliseconds * 0.001f * sampleRate));

    public void Apply(byte[] pcmBytes)
    {
        // Normalised so a small signal passes at roughly unity gain and only peaks get rounded off.
        var driveNorm = 1f / MathF.Sqrt(DriveAmount);
        for (var i = 0; i + 1 < pcmBytes.Length; i += 2)
        {
            var sample = (short)(pcmBytes[i] | (pcmBytes[i + 1] << 8));
            var x = sample / 32768f;

            var hp = _hpAlpha * (_hpPrevOut + x - _hpPrevIn);
            _hpPrevIn = x;
            _hpPrevOut = hp;

            var lp = _lpPrevOut + _lpAlpha * (hp - _lpPrevOut);
            _lpPrevOut = lp;

            // Gate on the band-limited signal's envelope, so low rumble/hum below the voice band
            // doesn't hold the gate open.
            var level = MathF.Abs(lp);
            _envelope += (level > _envelope ? _attack : _release) * (level - _envelope);
            var gate = Math.Clamp((_envelope - GateClosed) / (GateOpen - GateClosed), 0f, 1f);

            var clipped = MathF.Tanh(lp * DriveAmount) * driveNorm;
            var withStatic = (clipped + ((float)_noise.NextDouble() * 2f - 1f) * StaticLevel) * gate;

            var outSample = (short)Math.Clamp(withStatic * 32767f, short.MinValue, short.MaxValue);
            pcmBytes[i] = (byte)(outSample & 0xFF);
            pcmBytes[i + 1] = (byte)((outSample >> 8) & 0xFF);
        }
    }
}
