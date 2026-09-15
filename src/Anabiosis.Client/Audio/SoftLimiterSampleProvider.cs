using NAudio.Wave;

namespace Anabiosis.Client.Audio;

// Direct user decision ("Компрессия динамического диапазона" - Barotrauma checkbox, no real DSP
// compressor existed anywhere in this project) - a genuine multi-band compressor was out of scope,
// so this is the agreed honest, cheaper stand-in: a soft-knee limiter (tanh saturation) over the
// WHOLE mix, gentle right up near full scale and only really biting once a peak would otherwise
// clip. Same "shape a waveform sample-by-sample" idea RadioVoiceFilter.cs already uses for the
// radio's own drive/soft-clip, just applied to floats directly (already in NAudio's own -1..1 sample
// range) instead of decoding 16-bit PCM bytes first.
public sealed class SoftLimiterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public bool Enabled { get; set; }

    public SoftLimiterSampleProvider(ISampleProvider source)
    {
        _source = source;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        if (!Enabled)
            return read;

        // Linear up to the knee, tanh beyond it - loud mixes get gently rounded off instead of
        // clipping, quiet ones pass through untouched.
        const float knee = 0.7f;
        for (var i = 0; i < read; i++)
        {
            var sample = buffer[offset + i];
            var magnitude = System.MathF.Abs(sample);
            if (magnitude <= knee)
                continue;
            var sign = System.MathF.Sign(sample);
            var over = (magnitude - knee) / (1f - knee);
            buffer[offset + i] = sign * (knee + (1f - knee) * System.MathF.Tanh(over));
        }
        return read;
    }
}
