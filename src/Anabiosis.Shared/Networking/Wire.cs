using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Anabiosis.Shared.Networking;

// The bytes that actually go down a socket. Both ends of a co-op session speak this and nothing
// else, so the framing lives here once rather than in each connection class.
//
// A frame is [4 bytes payload length, little-endian][1 flag byte][payload], where the payload is
// UTF-8 JSON, deflated when it's big enough to be worth it. JSON rather than a hand-rolled binary
// format because the protocol records (WorldSnapshot, ClientCommand) change constantly as features
// land, and a format that reads them by name never needs updating in lockstep - the cost is size,
// and that's what the deflate is for. WorldSnapshot resends the whole ship/station layout every
// tick (see its own doc comment); compressed, that repetition is nearly free on the wire.
public static class Wire
{
    // Not an IANA-registered port, and outside the ephemeral range Windows hands out.
    public const int DefaultPort = 47624;

    private const int HeaderBytes = 5;
    private const int MaxFrameBytes = 16 * 1024 * 1024;
    private const int CompressAbove = 1024;
    private const byte FlagPlain = 0;
    private const byte FlagDeflate = 1;

    // UnsafeRelaxedJsonEscaping keeps the Russian names in the model (rooms, NPCs, quests) as real
    // UTF-8 instead of six-byte \uXXXX escapes - it's not "unsafe" here, nothing renders this as HTML.
    public static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);

    public static T Deserialize<T>(ReadOnlySpan<byte> utf8) =>
        JsonSerializer.Deserialize<T>(utf8, Options) ?? throw new InvalidDataException($"пустой кадр {typeof(T).Name}");

    public static void WriteFrame<T>(Stream stream, T value)
    {
        var json = Serialize(value);
        var deflate = json.Length >= CompressAbove;
        var body = deflate ? Deflate(json) : json;

        // Header and body in one write: on a NoDelay socket two writes are two packets, and the
        // header alone would go out ahead of its own payload 30 times a second.
        var frame = new byte[HeaderBytes + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length + 1);
        frame[4] = deflate ? FlagDeflate : FlagPlain;
        body.CopyTo(frame, HeaderBytes);

        stream.Write(frame, 0, frame.Length);
        stream.Flush();
    }

    // null means the peer closed cleanly. Anything else - a half-frame, a bad length - throws,
    // because that's a broken connection rather than a finished one.
    public static T? ReadFrame<T>(Stream stream) where T : class
    {
        var header = new byte[HeaderBytes];
        try
        {
            stream.ReadExactly(header);
        }
        catch (EndOfStreamException)
        {
            return null;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header) - 1;
        if (length < 0 || length > MaxFrameBytes)
            throw new InvalidDataException($"кадр недопустимой длины: {length}");

        var body = new byte[length];
        stream.ReadExactly(body);
        return Deserialize<T>(header[4] == FlagDeflate ? Inflate(body) : body);
    }

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream(data.Length / 2);
        using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, leaveOpen: true))
            deflate.Write(data, 0, data.Length);
        return output.ToArray();
    }

    private static byte[] Inflate(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(data.Length * 4);
        deflate.CopyTo(output);
        return output.ToArray();
    }
}
