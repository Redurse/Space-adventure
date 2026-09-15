using NAudio.Wave;

namespace Anabiosis.Client.Audio;

// Repeats a file-backed sample chain forever by rewinding the underlying WaveStream once it runs
// out - NAudio has no built-in "loop this" wrapper, unlike MonoGame's SoundEffectInstance.IsLooped.
// Used by GameSounds.CreateLoop (SoundEffectInstance's own looping replacement).
internal sealed class LoopingSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly WaveStream _reader;

    public LoopingSampleProvider(ISampleProvider source, WaveStream reader)
    {
        _source = source;
        _reader = reader;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        if (read >= count)
            return read;
        // Ran out mid-buffer - rewind and keep filling the rest of this same call from the top,
        // so a loop never has an audible gap at the seam.
        _reader.Position = 0;
        var more = _source.Read(buffer, offset + read, count - read);
        return read + more;
    }
}
