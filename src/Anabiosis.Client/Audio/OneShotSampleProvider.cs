using NAudio.Wave;

namespace Anabiosis.Client.Audio;

// Wraps a one-shot sound's sample chain so AudioEngine can tell once it's actually finished playing
// and safely remove it from the mixer - MixingSampleProvider never does this on its own (a source
// that returns fewer samples than asked just contributes partial silence forever unless something
// explicitly unregisters it, and the mixer's own input list would otherwise grow for the entire
// session).
internal sealed class OneShotSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    public bool Finished { get; private set; }

    public OneShotSampleProvider(ISampleProvider source) => _source = source;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        if (read < count)
            Finished = true;
        return read;
    }
}
