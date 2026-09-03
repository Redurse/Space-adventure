using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed partial class World
{
    private const int ChatLogMaxEntries = 50;
    private readonly List<ChatLogEntry> _chatLog = new();
    private int _nextChatMessageId = 1;

    // One crew-wide channel (direct user request, "как в Баротравме", MVP scope - no radio/
    // proximity). Capped like a real chat log (unlike StoryLog, which is campaign-scripted and
    // small enough to never trim) - a long session's chat could otherwise grow unboundedly since
    // CreateSnapshot resends the whole thing every tick, same "small data, full state each tick"
    // reasoning the rest of WorldSnapshot already relies on.
    private void LogChat(Character character, string text)
    {
        if (text.Length > 200)
            text = text[..200];
        var senderName = character.IsBot ? character.BotName ?? "?" : character.Nickname ?? $"Игрок {character.PlayerId}";
        _chatLog.Add(new ChatLogEntry(_nextChatMessageId++, character.PlayerId, senderName, text));
        if (_chatLog.Count > ChatLogMaxEntries)
            _chatLog.RemoveAt(0);
    }

    private IReadOnlyList<ChatLogEntry> CreateChatLog() => _chatLog.ToArray();
}
