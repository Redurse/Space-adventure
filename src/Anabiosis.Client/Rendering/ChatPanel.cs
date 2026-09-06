using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// One crew-wide text chat (direct user request, "как в Баротравме") - the always-on log of the
// last few messages plus the input box itself while actively typing. Fade timing is tracked off
// when THIS client first saw each ChatLogEntry.Id, not a server timestamp - avoids needing clock
// sync (same reasoning ChatBubbleTracker's own doc comment gives).
public sealed class ChatPanel
{
    private const int MaxVisibleEntries = 8;
    private const float DisplaySeconds = 8f;
    private const float FadeSeconds = 2f;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private readonly Dictionary<int, float> _seenAtSeconds = new();
    private float _clockSeconds;

    public ChatPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, IReadOnlyList<ChatLogEntry>? chatLog, bool chatFocused,
        string chatInput, Vector2 origin, float deltaSeconds)
    {
        _clockSeconds += deltaSeconds;

        if (chatLog is { Count: > 0 })
        {
            foreach (var entry in chatLog)
            {
                if (!_seenAtSeconds.ContainsKey(entry.Id))
                    _seenAtSeconds[entry.Id] = _clockSeconds;
            }

            // Prunes ids that fell off the server's own capped log (World.Chat.cs's
            // ChatLogMaxEntries) - otherwise this dictionary would grow for as long as the session
            // runs, one entry per chat message ever sent, never shrinking.
            if (_seenAtSeconds.Count > chatLog.Count)
            {
                var stillPresent = new HashSet<int>();
                foreach (var entry in chatLog)
                    stillPresent.Add(entry.Id);
                var stale = new List<int>();
                foreach (var id in _seenAtSeconds.Keys)
                    if (!stillPresent.Contains(id))
                        stale.Add(id);
                foreach (var id in stale)
                    _seenAtSeconds.Remove(id);
            }
        }

        var lineY = origin.Y;
        if (chatLog is { Count: > 0 })
        {
            var start = System.Math.Max(0, chatLog.Count - MaxVisibleEntries);
            var lines = new List<(string Text, float Alpha)>();
            for (var i = start; i < chatLog.Count; i++)
            {
                var entry = chatLog[i];
                var age = _seenAtSeconds.TryGetValue(entry.Id, out var seenAt) ? _clockSeconds - seenAt : 0f;
                if (age >= DisplaySeconds)
                    continue;
                var fadeStart = DisplaySeconds - FadeSeconds;
                var alpha = age <= fadeStart ? 1f : System.Math.Clamp(1f - (age - fadeStart) / FadeSeconds, 0f, 1f);
                lines.Add(($"{entry.SenderName}: {entry.Text}", alpha));
            }

            // Newest at the bottom - lay the visible lines out from the bottom up so the origin
            // stays a fixed anchor regardless of how many lines currently pass the fade cutoff.
            var y = origin.Y + (lines.Count - 1) * 18f;
            for (var i = lines.Count - 1; i >= 0; i--)
            {
                var (text, alpha) = lines[i];
                spriteBatch.DrawString(_font, text, new Vector2(origin.X, y), Color.White * alpha,
                    0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
                y -= 18f;
            }
            lineY = origin.Y + lines.Count * 18f;
        }

        if (chatFocused)
        {
            const int boxWidth = 360;
            const int boxHeight = 24;
            var boxOrigin = new Vector2(origin.X, lineY + 4f);
            spriteBatch.Draw(_pixel, new Rectangle((int)boxOrigin.X, (int)boxOrigin.Y, boxWidth, boxHeight), Color.Black * 0.6f);
            // A simple blink, half a second on/off, off the same local clock the fade timing uses.
            var cursor = _clockSeconds % 1f < 0.5f ? "|" : "";
            spriteBatch.DrawString(_font, chatInput + cursor, boxOrigin + new Vector2(6, 4), Color.White,
                0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        }
    }
}
