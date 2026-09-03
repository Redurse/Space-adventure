using System.Collections.Generic;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// A speech bubble above whoever just sent a chat message (direct user request, "как в
// Баротравме") - keyed by sender PlayerId so a new message from the same player refreshes their
// bubble's timer rather than stacking a second one. Driven by NEW ChatLogEntry.Id values arriving
// in the snapshot (not a WorldSnapshot before/after diff the way TransientEffect/EffectTracker
// detect weld/cut sparks - a chat message is a discrete announcement, not a state transition), but
// reuses the same "spawn, tick down, expire" shape and fade-by-Progress idiom those already use.
public sealed class ChatBubbleTracker
{
    private const float BubbleSeconds = 4.5f;
    private readonly Dictionary<int, (string Text, float Remaining)> _bubbles = new();
    private int _lastSeenId;

    public void Update(IReadOnlyList<ChatLogEntry>? chatLog, float deltaSeconds)
    {
        if (chatLog is { Count: > 0 })
        {
            foreach (var entry in chatLog)
            {
                if (entry.Id <= _lastSeenId)
                    continue;
                _bubbles[entry.SenderPlayerId] = (entry.Text, BubbleSeconds);
            }
            _lastSeenId = chatLog[^1].Id;
        }

        var expired = new List<int>();
        foreach (var key in _bubbles.Keys)
        {
            var (text, remaining) = _bubbles[key];
            remaining -= deltaSeconds;
            if (remaining <= 0f)
                expired.Add(key);
            else
                _bubbles[key] = (text, remaining);
        }
        foreach (var key in expired)
            _bubbles.Remove(key);
    }

    // null if this player has no active bubble. alpha fades over the last ~1.5s.
    public (string Text, float Alpha)? BubbleFor(int playerId)
    {
        if (!_bubbles.TryGetValue(playerId, out var bubble))
            return null;
        var alpha = bubble.Remaining < 1.5f ? bubble.Remaining / 1.5f : 1f;
        return (bubble.Text, alpha);
    }
}
