using Anabiosis.Server;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

// The delta frames of the socket: every frame rebuilds to exactly the snapshot that was sent, and the second one onward is a small fraction of the first.
internal static partial class TestRunner
{
    private static ServerMessage RoundTripDelta(SnapshotDeltaEncoder encoder, SnapshotDeltaDecoder decoder, WorldSnapshot snapshot, out int frameBytes)
    {
        var json = encoder.Encode(snapshot);
        frameBytes = json.Length;
        var message = Wire.Deserialize<ServerMessage>(json);
        return message with { Snapshot = decoder.Decode(message.Snapshot!, message.Unchanged) };
    }

    private static bool Wire_DeltaFrames_RebuildEveryTickExactlyAndShrinkAfterTheFirst()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var encoder = new SnapshotDeltaEncoder();
        var decoder = new SnapshotDeltaDecoder();

        world.Step(RealtimeStep);
        var first = world.CreateSnapshot();
        var firstMessage = RoundTripDelta(encoder, decoder, first, out var firstBytes);
        if (!Wire.Serialize(first).AsSpan().SequenceEqual(Wire.Serialize(firstMessage.Snapshot!)))
            return false;

        var smallest = int.MaxValue;
        for (var i = 0; i < 90; i++)
        {
            // Walking, then docking/undocking partway, so both dynamic and static parts change.
            world.ApplyCommand(1, new ClientCommand(1, MoveX: i < 45 ? 1 : -1, DockPressed: i == 60));
            world.Step(RealtimeStep);
            var snapshot = world.CreateSnapshot();
            var message = RoundTripDelta(encoder, decoder, snapshot, out var bytes);
            if (!Wire.Serialize(snapshot).AsSpan().SequenceEqual(Wire.Serialize(message.Snapshot!)))
                return false;
            if (i > 2 && i != 60 && i != 61)
                smallest = Math.Min(smallest, bytes);
        }

        Console.WriteLine($"     дельта-кадр: первый {firstBytes} байт, потом от {smallest} (JSON до сжатия)");
        return smallest < firstBytes / 4;
    }

    private static bool Wire_DeltaFrame_ReferencingStateTheDecoderNeverSawIsRefused()
    {
        var world = new World();
        world.SpawnCharacter(1);
        var encoder = new SnapshotDeltaEncoder();
        world.Step(RealtimeStep);
        encoder.Encode(world.CreateSnapshot());
        var second = Wire.Deserialize<ServerMessage>(encoder.Encode(world.CreateSnapshot())); // refers back to the first frame
        try
        {
            new SnapshotDeltaDecoder().Decode(second.Snapshot!, second.Unchanged); // a decoder that missed the first frame
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }
}
