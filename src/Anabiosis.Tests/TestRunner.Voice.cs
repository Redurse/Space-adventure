using System;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    // Push-to-talk voice (direct user request, "как в Баротравме", local + radio). VoiceChunk is
    // edge-triggered like ChatMessage: applied once via ApplyCommand, then a Step to mirror how
    // every other edge-triggered field's own test in this suite exercises ApplyCommand followed by
    // a Step before reading CreateSnapshot back. The critical behavior here is that VoiceChunks is
    // a read-and-clear per-tick list, NOT a growing log like ChatLog - calling CreateSnapshot a
    // second time without a new command must come back empty.
    private static bool World_Voice_ChunkAppearsInSnapshotThenClears()
    {
        var world = new World();
        world.SpawnCharacter(1);

        var payload = new VoiceChunkPayload(new byte[] { 1, 2, 3, 4 }, 24000, IsRadio: false);
        world.ApplyCommand(1, new ClientCommand(1, VoiceChunk: payload));
        world.Step(RealtimeStep);

        var chunks = world.CreateSnapshot().VoiceChunks;
        if (chunks is not { Count: 1 })
            return false;
        var chunk = chunks[0];
        if (chunk.SenderPlayerId != 1 || chunk.SampleRate != 24000 || chunk.IsRadio)
            return false;

        // Read-and-clear: calling CreateSnapshot again without applying any new command must come
        // back empty - this is the one most likely to catch a real "growing log" bug.
        var chunksAgain = world.CreateSnapshot().VoiceChunks;
        return chunksAgain is null || chunksAgain.Count == 0;
    }

    // Radio needs a worn ItemType.Radio to transmit (World.Voice.cs's RelayVoiceChunk, direct user
    // request matching Barotrauma's own radio-as-item model) - TakeFromRack + a drag into
    // EquipSlot.Headset is the same "get geared up" setup every other equip test in this suite uses.
    private static bool World_Voice_RadioFlagIsRelayed()
    {
        var world = new World();
        world.SpawnCharacter(1);

        var radioSlot = TakeFromRack(world, ItemType.Radio);
        world.ApplyCommand(1, new ClientCommand(1,
            MoveItemFrom: new SlotRef(ItemSlotKind.Main, radioSlot),
            MoveItemTo: new SlotRef(ItemSlotKind.Equip, (int)EquipSlot.Headset)));

        var payload = new VoiceChunkPayload(new byte[] { 5, 6 }, 44100, IsRadio: true);
        world.ApplyCommand(1, new ClientCommand(1, VoiceChunk: payload));
        world.Step(RealtimeStep);

        var chunks = world.CreateSnapshot().VoiceChunks;
        if (chunks is not { Count: 1 })
            return false;
        var chunk = chunks[0];
        return chunk.SenderPlayerId == 1 && chunk.SampleRate == 44100 && chunk.IsRadio;
    }

    // The exact opposite of the above - no radio worn, radio chunk should be dropped silently
    // rather than relayed (or, worse, silently downgraded to local).
    private static bool World_Voice_RadioWithoutItemIsDropped()
    {
        var world = new World();
        world.SpawnCharacter(1);

        var payload = new VoiceChunkPayload(new byte[] { 5, 6 }, 44100, IsRadio: true);
        world.ApplyCommand(1, new ClientCommand(1, VoiceChunk: payload));
        world.Step(RealtimeStep);

        var chunks = world.CreateSnapshot().VoiceChunks;
        return chunks is null || chunks.Count == 0;
    }

    private static bool World_Voice_EmptySamplesAreIgnored()
    {
        var world = new World();
        world.SpawnCharacter(1);

        var payload = new VoiceChunkPayload(Array.Empty<byte>(), 24000, IsRadio: false);
        world.ApplyCommand(1, new ClientCommand(1, VoiceChunk: payload));
        world.Step(RealtimeStep);

        var chunks = world.CreateSnapshot().VoiceChunks;
        return chunks is null || chunks.Count == 0;
    }
}
