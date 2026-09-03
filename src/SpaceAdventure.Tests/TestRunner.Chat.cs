using SpaceAdventure.Server;
using SpaceAdventure.Shared.Protocol;

internal static partial class TestRunner
{
    // One crew-wide text channel (direct user request, "как в Баротравме", MVP scope - no radio/
    // proximity). ChatMessage is edge-triggered like DoorToggleId: applied once via ApplyCommand,
    // then a Step to mirror how every other edge-triggered field's own test in this suite exercises
    // ApplyCommand followed by a Step before reading CreateSnapshot back.
    private static bool World_Chat_MessageAppearsInSnapshot()
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.ApplyCommand(1, new ClientCommand(1, Nickname: "Иван"));
        world.Step(RealtimeStep);

        world.ApplyCommand(1, new ClientCommand(1, ChatMessage: "привет"));
        world.Step(RealtimeStep);

        var chatLog = world.CreateSnapshot().ChatLog;
        if (chatLog is not { Count: 1 })
            return false;
        var entry = chatLog[0];
        return entry.SenderPlayerId == 1 && entry.Text == "привет" && entry.SenderName == "Иван";
    }

    // A fresh player who never sent a Nickname command resolves to "Игрок {id}" - the same fallback
    // World.Chat.cs's LogChat copies from CrewPanel.cs's own character.Nickname ?? $"Игрок {id}"
    // idiom.
    private static bool World_Chat_DefaultSenderNameIsPlayerIdFallback()
    {
        var world = new World();
        world.SpawnCharacter(2);

        world.ApplyCommand(2, new ClientCommand(2, ChatMessage: "тест"));
        world.Step(RealtimeStep);

        var entry = world.CreateSnapshot().ChatLog?.SingleOrDefault();
        return entry is not null && entry.SenderName == "Игрок 2";
    }

    // Capped like a real chat log (World.Chat.cs's ChatLogMaxEntries = 50) - sending more than the
    // cap must drop the OLDEST entries first, keeping only the most recent 50.
    private static bool World_Chat_LogIsCappedAtMaxEntries()
    {
        var world = new World();
        world.SpawnCharacter(1);

        const int totalMessages = 60;
        for (var i = 0; i < totalMessages; i++)
        {
            world.ApplyCommand(1, new ClientCommand(1, ChatMessage: $"msg{i}"));
            world.Step(RealtimeStep);
        }

        var chatLog = world.CreateSnapshot().ChatLog;
        if (chatLog is not { Count: 50 })
            return false;

        // The oldest 10 (msg0..msg9) should have been dropped - the surviving entries are msg10..msg59.
        return chatLog[0].Text == "msg10" && chatLog[^1].Text == "msg59";
    }

    // An empty ChatMessage (edge-triggered, same "null/empty = nothing happened" convention every
    // other edge-triggered field in ClientCommand uses) must not add anything to the log.
    private static bool World_Chat_EmptyMessageIsIgnored()
    {
        var world = new World();
        world.SpawnCharacter(1);

        world.ApplyCommand(1, new ClientCommand(1, ChatMessage: ""));
        world.Step(RealtimeStep);
        world.ApplyCommand(1, new ClientCommand(1)); // ChatMessage defaults to null
        world.Step(RealtimeStep);

        var chatLog = world.CreateSnapshot().ChatLog;
        return chatLog is null || chatLog.Count == 0;
    }
}
