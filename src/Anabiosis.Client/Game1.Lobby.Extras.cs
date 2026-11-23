using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Client.Audio;
using Anabiosis.Client.Networking;
using Anabiosis.Shared.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Anabiosis.Client;

// The waiting room's plumbing: the chat line, being removed or losing the host, and two small drawing helpers. Where things sit and what a click does is
// Game1.Lobby.cs; the panels are Game1.Lobby.Panels.cs; the server's side is GameServer.Lobby.cs.
public partial class Game1
{
    private string _lobbyChatInput = "";
    private const int LobbyChatInputMax = 120;

    // ---- the chat line ----

    // Typed characters while the lobby is on screen go to the chat line, unless a field of the settings window or the campaign filter was clicked (Backspace deletes).
    private void AppendLobbyChatCharacter(char character)
    {
        switch (_lobbyTextTarget)
        {
            case LobbyTextTarget.ServerName: _editServerName = EditedText(_editServerName, character, LobbySettings.MaxNameLength); break;
            case LobbyTextTarget.Motd: _editMotd = EditedText(_editMotd, character, LobbySettings.MaxMotdLength); break;
            case LobbyTextTarget.SaveFilter: _lobbySaveFilter = EditedText(_lobbySaveFilter, character, 30); break;
            default: _lobbyChatInput = EditedText(_lobbyChatInput, character, LobbyChatInputMax); break;
        }
    }

    private static string EditedText(string text, char character, int max)
    {
        if (character == '\b')
            return text.Length > 0 ? text[..^1] : text;
        return !char.IsControl(character) && text.Length < max ? text + character : text;
    }

    // Enter: sends the chat line; or applies the name / message being typed in the settings window; or just leaves the campaign filter.
    private void HandleLobbyTextEnter(KeyboardState keyboard)
    {
        if (!Pressed(keyboard, Keys.Enter) || _client is null)
            return;
        switch (_lobbyTextTarget)
        {
            case LobbyTextTarget.ServerName:
            case LobbyTextTarget.Motd:
                if (_client.LatestLobby is { } lobby && lobby.Players.FirstOrDefault(p => p.PlayerId == _client.PlayerId)?.IsHost == true)
                    CommitServerTextEdits(lobby);
                _lobbyTextTarget = LobbyTextTarget.Chat;
                return;
            case LobbyTextTarget.SaveFilter:
                _lobbyTextTarget = LobbyTextTarget.Chat;
                return;
        }
        if (_lobbyChatInput.Trim().Length == 0)
            return;
        _client.Send(new ClientCommand(_client.PlayerId, LobbyChat: _lobbyChatInput.Trim(), LobbyReady: _lobbyReady));
        PlayItemSound(GameSounds.ChatSent, 0.6f);
        _lobbyChatInput = "";
    }

    // ---- being removed, or losing the host ----

    // True when the waiting room is over for this player: the host removed them, or the connection to the host went away. They land on the join
    // screen with the reason, ready to try again.
    private bool CheckLobbyKickedOrLost()
    {
        if (_client is null)
            return false;
        string? reason = null;
        if (_client.LatestLobby?.KickedPlayerIds?.Contains(_client.PlayerId) == true)
            reason = "хост убрал вас из лобби";
        else if (_session is NetworkSession { IsConnected: false })
            reason = "соединение с хостом потеряно";
        if (reason is null)
            return false;

        ReturnToMainMenu();
        _joinError = reason;
        _menuScreen = MenuScreen.Join;
        return true;
    }

    // ---- helpers ----

    // Words onto lines no wider than `width` pixels at `scale` (a word longer than a line is cut where it overflows).
    private List<string> WrapText(string text, int width, float scale)
    {
        var lines = new List<string>();
        var current = "";
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (_font.MeasureString(candidate).X * scale <= width)
            {
                current = candidate;
                continue;
            }
            if (current.Length > 0)
                lines.Add(current);
            current = word;
            while (current.Length > 1 && _font.MeasureString(current).X * scale > width)
            {
                var cut = current.Length - 1;
                while (cut > 1 && _font.MeasureString(current[..cut]).X * scale > width)
                    cut--;
                lines.Add(current[..cut]);
                current = current[cut..];
            }
        }
        if (current.Length > 0)
            lines.Add(current);
        return lines;
    }

    private void DrawLobbyCheckbox(Rectangle rect, string label, bool on, bool editable)
    {
        _spriteBatch.Draw(_pixel, rect, new Color(20, 22, 26) * (editable ? 0.9f : 0.6f));
        DrawBevel(rect, raised: false, strength: editable ? 0.6f : 0.35f);
        if (on)
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8), SettingsAccentTeal * (editable ? 1f : 0.6f));
        _spriteBatch.DrawString(_font, label, new Vector2(rect.Right + 8, rect.Y + (rect.Height - 14) / 2), editable ? SettingsTextPrimary : SettingsTextDim, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }
}
