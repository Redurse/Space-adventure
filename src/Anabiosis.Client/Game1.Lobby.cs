using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Anabiosis.Client;

// The waiting room, laid out like Barotrauma's (user-supplied screenshots): on the left the server's banner, the game mode, the list of ships with the chosen
// one's picture and figures, and below it two tabs - the round's settings (credits, war, the campaign modifiers) and the saved campaigns to continue; on the
// right this player's own card (watching / away, name, then two tabs: role + starting kit, and appearance), the chat beside the list of players, "ready" and
// the start button. A button in the banner opens the server settings window (four tabs). Everything is written in design pixels (DesignWidth x DesignHeight,
// 1200x560).
// This file is where things are, the state and the click router; the drawing of the panels is Game1.Lobby.Panels.cs, the ship list and its picture
// Game1.Lobby.Ships.cs, the own-card tabs Game1.Lobby.OwnCard.cs, the saved campaigns Game1.Lobby.Saves.cs, the settings window Game1.Lobby.ServerWindow.cs,
// the hover tips Game1.Lobby.Tips.cs, and the chat/kick plumbing Game1.Lobby.Extras.cs.
public partial class Game1
{
    private static readonly Rectangle LobbyLeftPanel = new(10, 10, 790, 540);
    private static readonly Rectangle LobbyRightPanel = new(810, 10, 380, 540);
    private static readonly Rectangle LobbyBanner = new(14, 14, 782, 64);
    private static readonly Rectangle LobbyServerButton = new(636, 22, 150, 24);

    private static readonly Rectangle LobbyModeBox = new(22, 110, 196, 220);
    private static readonly Rectangle LobbyShipListBox = new(228, 110, 230, 220);
    private static readonly Rectangle LobbyPreviewBox = new(468, 110, 320, 110);
    private static readonly Rectangle LobbyDetailsBox = new(468, 226, 320, 104);
    private static readonly Rectangle LobbyTabNew = new(22, 340, 200, 20);
    private static readonly Rectangle LobbyTabLoad = new(226, 340, 200, 20);
    private static readonly Rectangle LobbySettingsBox = new(22, 362, 766, 138);
    private static readonly Rectangle LobbyDisconnectButton = new(22, 510, 150, 30);

    private static readonly Rectangle LobbySpectateArea = new(820, 14, 170, 20);
    private static readonly Rectangle LobbySpectateCheckbox = new(822, 16, 16, 16);
    private static readonly Rectangle LobbyAfkArea = new(1008, 14, 150, 20);
    private static readonly Rectangle LobbyAfkCheckbox = new(1010, 16, 16, 16);
    private static readonly Rectangle LobbyNickBar = new(820, 38, 360, 22);
    private static readonly Rectangle LobbyTabRole = new(820, 64, 178, 22);
    private static readonly Rectangle LobbyTabLook = new(1002, 64, 178, 22);
    private static readonly Rectangle LobbyCardArea = new(820, 90, 360, 184);
    private static readonly Rectangle LobbyPortraitBox = new(820, 90, 360, 96);
    // The role slot under the portrait; a click opens the picker with every role (as Barotrauma's job slots do).
    private static readonly Rectangle LobbyRoleSlot = new(820, 190, 360, 38);
    private static readonly Rectangle LobbyRolePopup = new(496, 120, 308, 216);
    private static Rectangle LobbyRolePopupCell(int index) => new(LobbyRolePopup.X + 6 + index % 3 * 100, LobbyRolePopup.Y + 6 + index / 3 * 102, 98, 100);
    private static Rectangle LobbyKitCard(int index) => new(820 + index * 124, 232, 112, 42);

    private static readonly Rectangle LobbyChatBox = new(820, 302, 180, 192);
    private static readonly Rectangle LobbyPlayersBox = new(1006, 302, 174, 192);
    private static readonly Rectangle LobbyChatInput = new(820, 498, 360, 22);
    private static readonly Rectangle LobbyReadyArea = new(820, 524, 170, 24);
    private static readonly Rectangle LobbyReadyCheckbox = new(822, 528, 16, 16);
    private static readonly Rectangle LobbyStartButton = new(1000, 524, 180, 24);

    private const int LobbyShipRowHeight = 28;
    private const int LobbyPlayerRowStride = 23;
    private const int LobbyShipRowsVisible = 7; // (LobbyShipListBox.Height - 8) / LobbyShipRowHeight

    private static Rectangle LobbyShipRow(int visibleIndex) =>
        new(LobbyShipListBox.X + 4, LobbyShipListBox.Y + 4 + visibleIndex * LobbyShipRowHeight, LobbyShipListBox.Width - 16, LobbyShipRowHeight - 2);
    private static Rectangle LobbyPlayerRow(int index) =>
        new(LobbyPlayersBox.X + 4, LobbyPlayersBox.Y + 4 + index * LobbyPlayerRowStride, LobbyPlayersBox.Width - 8, 21);
    private static Rectangle LobbyKickButton(int index) { var row = LobbyPlayerRow(index); return new Rectangle(row.Right - 20, row.Y + 2, 18, 17); }
    private static Rectangle LobbyBanButton(int index) { var row = LobbyPlayerRow(index); return new Rectangle(row.Right - 40, row.Y + 2, 18, 17); }

    // The round settings tab: credits and the two switches on the left, the campaign modifiers on the right.
    private static Rectangle LobbyCreditsMinus => new(34, 396, 24, 24);
    private static Rectangle LobbyCreditsPlus => new(150, 396, 24, 24);
    private static Rectangle LobbyWarCheckbox => new(214, 378, 20, 20);
    private static Rectangle LobbyDockedCheckbox => new(214, 408, 20, 20);
    private static Rectangle LobbyModifierMinus(int row) => new(690, 388 + row * 30, 22, 22);
    private static Rectangle LobbyModifierPlus(int row) => new(754, 388 + row * 30, 22, 22);

    private enum LobbyRightTab { Role, Look }
    private enum LobbyBottomTab { Settings, Load }
    // Where typed characters go: the chat line unless a field of the settings window or the campaign filter was clicked.
    private enum LobbyTextTarget { Chat, ServerName, Motd, SaveFilter }

    private LobbyRightTab _lobbyRightTab;
    private LobbyBottomTab _lobbyBottomTab;
    private LobbyTextTarget _lobbyTextTarget;
    private bool _lobbyRolePopupOpen;
    private int _lobbyShipScroll;
    private int _prevLobbyWheel;
    private bool _lobbyWheelKnown;
    private CrewSkin? _lobbyCrewSkin;

    private void HandleLobbyScreen(KeyboardState keyboard)
    {
        _client?.PollSnapshots();

        // The round actually began - either this host just pressed the start button, or (joining an already-running host) a real WorldSnapshot arrives
        // within a tick or two regardless. Either way gameplay takes over exactly like it always has for every other entry point.
        if (_client?.LatestSnapshot is not null)
        {
            _sessionStarted = true; ResetMapPassword();
            _pendingSetOwnRoleTo = _selectedRole;
            _pendingLookToSend = _ownLook;
            return;
        }

        if (CheckLobbyKickedOrLost())
            return;
        HandleLobbyTextEnter(keyboard);

        var mouse = Mouse.GetState();
        HandleLobbyWheel(mouse);
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        _prevMenuLeftMouseButton = mouse.LeftButton;
        if (!clicked || _client is not { LatestLobby: { } lobby })
            return;

        var isHost = lobby.Players.FirstOrDefault(p => p.PlayerId == _client.PlayerId)?.IsHost ?? false;
        HandleLobbyClick(lobby, isHost, _designMouse);
    }

    // Sends a lobby command with this player's ready flag carried along (the server mirrors it from every command), after `edit` set what the command is for.
    private void SendLobby(Func<ClientCommand, ClientCommand> edit) =>
        _client?.Send(edit(new ClientCommand(_client.PlayerId, LobbyReady: _lobbyReady)));

    private void SendLobbySettings(LobbySettings settings) => SendLobby(c => c with { LobbySettingsUpdate = settings.Clamped() });

    // The wheel scrolls whichever list the pointer is over.
    private void HandleLobbyWheel(MouseState mouse)
    {
        var delta = _lobbyWheelKnown ? mouse.ScrollWheelValue - _prevLobbyWheel : 0;
        _prevLobbyWheel = mouse.ScrollWheelValue;
        _lobbyWheelKnown = true;
        if (delta == 0)
            return;
        var step = -Math.Sign(delta);
        if (_serverWindowOpen)
        {
            _serverWindowScroll = Math.Max(0, _serverWindowScroll + step);
            return;
        }
        if (LobbyShipListBox.Contains(_designMouse))
        {
            _lobbyShipScroll += step;
            ClampLobbyShipScroll();
        }
        else if (_lobbyBottomTab == LobbyBottomTab.Load && LobbySettingsBox.Contains(_designMouse))
            _lobbySaveScroll = Math.Max(0, _lobbySaveScroll + step);
    }

    private void ClampLobbyShipScroll()
    {
        var rows = _client?.LatestLobby is { } lobby ? LobbyShipRows(lobby).Count : 0;
        _lobbyShipScroll = Math.Clamp(_lobbyShipScroll, 0, Math.Max(0, rows - LobbyShipRowsVisible));
    }

    // True when the click hit something of the lobby (the chat takes a click as "here" without a sound).
    private bool HandleLobbyClick(LobbySnapshot lobby, bool isHost, Point point)
    {
        // The settings window is on top of everything.
        if (_serverWindowOpen)
            return HandleServerWindowClick(lobby, isHost, point);

        // The role picker is next: a click on a role takes it, a click anywhere else only closes the picker.
        if (_lobbyRolePopupOpen)
        {
            _lobbyRolePopupOpen = false;
            for (var i = 0; i < RoleChoices.Length; i++)
            {
                if (!LobbyRolePopupCell(i).Contains(point))
                    continue;
                PlayUiClick();
                _selectedRole = RoleChoices[i];
                PlayerSettingsStore.SaveRole(RoleChoices[i]);
                SendLobby(c => c with { SetOwnRoleTo = RoleChoices[i] });
                return true;
            }
            return true;
        }

        _lobbyTextTarget = LobbyTextTarget.Chat; // any other click puts the typing back on the chat; a field below takes it again if it was clicked

        if (LobbyChatInput.Contains(point) || LobbyChatBox.Contains(point))
            return true;

        if (LobbyServerButton.Contains(point))
        {
            PlayUiClick();
            OpenServerWindow(lobby);
            return true;
        }

        if (LobbyDisconnectButton.Contains(point))
        {
            PlayUiClick();
            ReturnToMainMenu();
            return true;
        }

        if (LobbyReadyArea.Contains(point))
        {
            PlayUiClick();
            _lobbyReady = !_lobbyReady;
            SendLobby(c => c);
            return true;
        }

        if (HandleLobbyOwnCardClick(lobby, point))
            return true;

        if (!isHost)
            return false;

        if (LobbyStartButton.Contains(point))
        {
            PlayUiClick();
            SendLobby(c => c with { LobbyStartRoundPressed = true });
            return true;
        }

        for (var i = 0; i < lobby.Players.Count; i++)
        {
            if (lobby.Players[i].IsHost)
                continue;
            var playerId = lobby.Players[i].PlayerId;
            if (LobbyKickButton(i).Contains(point))
            {
                PlayUiClick();
                SendLobby(c => c with { LobbyKickPlayerId = playerId });
                return true;
            }
            if (LobbyBanButton(i).Contains(point))
            {
                PlayUiClick();
                SendLobby(c => c with { LobbyBanPlayerId = playerId });
                return true;
            }
        }

        if (LobbyTabNew.Contains(point) || LobbyTabLoad.Contains(point))
        {
            PlayUiClick();
            _lobbyBottomTab = LobbyTabLoad.Contains(point) ? LobbyBottomTab.Load : LobbyBottomTab.Settings;
            return true;
        }

        if (_lobbyBottomTab == LobbyBottomTab.Load ? HandleLobbySavesClick(lobby, point) : HandleLobbySettingsClick(lobby, point))
            return true;

        var shipRows = LobbyShipRows(lobby);
        for (var i = 0; i < LobbyShipRowsVisible; i++)
        {
            var index = _lobbyShipScroll + i;
            if (index >= shipRows.Count || !LobbyShipRow(i).Contains(point))
                continue;
            PlayUiClick();
            if (shipRows[index] is not { } name)
                return true; // the default ship's row only shows that it is the one in use
            // Re-loaded rather than trusting the list's own moment-ago result: the file could have been deleted or broken out from under it.
            if (LoadPlayableCustomShip(name) is not { } definition || CustomShipValidator.Validate(definition).Count > 0)
                return true;
            _lobbySelectedShipName = name;
            SendLobby(c => c with { LobbySelectCustomShip = definition, LobbySelectCustomShipName = name });
            return true;
        }
        return false;
    }

    // The round settings tab (host only - everyone sees the values, only the host's clicks reach here).
    private bool HandleLobbySettingsClick(LobbySnapshot lobby, Point point)
    {
        var settings = lobby.Settings ?? new LobbySettings();
        LobbySettings? changed = null;
        if (LobbyCreditsMinus.Contains(point))
            changed = settings with { StartCredits = settings.StartCredits - LobbySettings.StartCreditsStep };
        else if (LobbyCreditsPlus.Contains(point))
            changed = settings with { StartCredits = settings.StartCredits + LobbySettings.StartCreditsStep };
        else if (LobbyWarCheckbox.Contains(point))
            changed = settings with { GalacticWar = !settings.GalacticWar };
        else if (LobbyDockedCheckbox.Contains(point))
            changed = settings with { StartDocked = !settings.StartDocked };
        else
        {
            for (var row = 0; row < 3 && changed is null; row++)
            {
                var delta = LobbyModifierMinus(row).Contains(point) ? -CampaignModifiers.Step : LobbyModifierPlus(row).Contains(point) ? CampaignModifiers.Step : 0f;
                if (delta == 0f)
                    continue;
                changed = row switch
                {
                    0 => settings with { RewardMultiplier = settings.RewardMultiplier + delta },
                    1 => settings with { ShopPriceMultiplier = settings.ShopPriceMultiplier + delta },
                    _ => settings with { ShipyardPriceMultiplier = settings.ShipyardPriceMultiplier + delta },
                };
            }
        }
        if (changed is null)
            return false;
        PlayUiClick();
        SendLobbySettings(changed);
        return true;
    }

    private void DrawLobbyScreen()
    {
        _lobbyTipText = null;
        _lobbyTipKit = null;

        DrawLobbyPanelFrame(LobbyLeftPanel);
        DrawLobbyPanelFrame(LobbyRightPanel);

        var lobby = _client?.LatestLobby;
        if (lobby is null)
        {
            _spriteBatch.DrawString(_font, "Ожидание ответа хоста...", new Vector2(30, 30), SettingsAccentGold, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
            DrawLobbyDisconnectButton();
            return;
        }

        var isHost = lobby.Players.FirstOrDefault(p => p.PlayerId == _client!.PlayerId)?.IsHost ?? false;
        ClampLobbyShipScroll();

        DrawLobbyBanner(lobby);
        DrawLobbyModeColumn(lobby);
        DrawLobbyShipList(lobby, isHost);
        DrawLobbyShipPreviewAndDetails(lobby);
        DrawLobbyBottomTabs(lobby, isHost);
        DrawLobbyDisconnectButton();

        DrawLobbyOwnCard(lobby);
        DrawLobbyChat(lobby);
        DrawLobbyPlayers(lobby, isHost);
        DrawLobbyReadyAndStart(isHost);
        if (_lobbyRolePopupOpen)
            DrawLobbyRolePopup();
        if (_serverWindowOpen)
            DrawServerWindow(lobby, isHost);
        DrawLobbyTip();
    }

    private void DrawLobbyDisconnectButton()
    {
        var hover = SettingsHoverEase("lobby:leave", LobbyDisconnectButton.Contains(_designMouse));
        _spriteBatch.Draw(_pixel, LobbyDisconnectButton, Color.Lerp(new Color(40, 46, 56), new Color(70, 78, 92), hover));
        DrawBevel(LobbyDisconnectButton, raised: true);
        DrawCenteredText("ОТКЛЮЧИТЬСЯ", LobbyDisconnectButton, 0.6f, SettingsTextPrimary);
        _spriteBatch.DrawString(_font, "[Esc] тоже выходит из лобби", new Vector2(LobbyDisconnectButton.Right + 12, LobbyDisconnectButton.Y + 9),
            SettingsTextDim * 0.8f, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }

    private void DrawLobbyReadyAndStart(bool isHost)
    {
        DrawLobbyCheckbox(LobbyReadyCheckbox, "Готов начать", _lobbyReady, editable: true);
        RegisterLobbyTip(LobbyReadyArea, "Отметьте, когда готовы. Если хост включил запуск по готовности, раунд начнётся сам, когда готово достаточно игроков.");

        var hover = SettingsHoverEase("lobby:start", isHost && LobbyStartButton.Contains(_designMouse));
        if (isHost)
        {
            DrawVerticalGradient(LobbyStartButton, Color.Lerp(SettingsAccentGold, Color.White, 0.15f * hover), SettingsAccentGold * 0.75f);
            DrawBevel(LobbyStartButton, raised: true);
            DrawCenteredText("НАЧАТЬ", LobbyStartButton, 0.7f, new Color(30, 24, 10));
        }
        else
        {
            _spriteBatch.Draw(_pixel, LobbyStartButton, new Color(30, 34, 42));
            DrawBevel(LobbyStartButton, raised: false, strength: 0.5f);
            DrawCenteredText("ЖДЁМ ХОСТА", LobbyStartButton, 0.55f, SettingsTextDim);
        }
    }

    // ---- small shared bits ----

    private void DrawLobbyPanelFrame(Rectangle panel)
    {
        DrawVerticalGradient(panel, SettingsPanelTop, SettingsPanelBottom);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, panel, SettingsBorderSteel, 2);
        ShipRenderer.DrawRivets(_spriteBatch, _pixel, panel);
    }

    // A dark inset box the content sits in.
    private void DrawLobbyInset(Rectangle box)
    {
        _spriteBatch.Draw(_pixel, box, new Color(11, 14, 19) * 0.92f);
        DrawBevel(box, raised: false, strength: 0.45f);
    }

    private void DrawLobbyHeader(string text, int x, int y) =>
        _spriteBatch.DrawString(_font, text, new Vector2(x, y), SettingsTextPrimary, 0f, Vector2.Zero, 0.62f, SpriteEffects.None, 0f);

    private void DrawCenteredText(string text, Rectangle rect, float scale, Color color)
    {
        var size = _font.MeasureString(text) * scale;
        _spriteBatch.DrawString(_font, text, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    // Right-aligned to `right`.
    private void DrawRightText(string text, int right, int y, float scale, Color color)
    {
        var size = _font.MeasureString(text) * scale;
        _spriteBatch.DrawString(_font, text, new Vector2(right - size.X, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    // `text` cut to fit `width` pixels at `scale`, with ".." where it was cut.
    private string FitText(string text, int width, float scale)
    {
        if (_font.MeasureString(text).X * scale <= width)
            return text;
        while (text.Length > 1 && _font.MeasureString(text + "..").X * scale > width)
            text = text[..^1];
        return text + "..";
    }

    // A tab header: lit when it is the open one.
    private void DrawLobbyTabHeader(string text, Rectangle rect, bool active, string hoverKey)
    {
        var hover = SettingsHoverEase(hoverKey, rect.Contains(_designMouse));
        var fill = active ? SettingsBorderSteel * 0.95f : Color.Lerp(new Color(30, 36, 46), new Color(52, 60, 74), hover);
        _spriteBatch.Draw(_pixel, rect, fill);
        DrawBevel(rect, raised: !active, strength: 0.5f);
        DrawCenteredText(text, rect, 0.5f, active ? Color.White : SettingsTextDim);
    }

    // A -/+ stepper with its value between, as the create-server screen draws them.
    private void DrawLobbyStepper(Rectangle minus, Rectangle plus, string value, bool editable)
    {
        if (editable)
        {
            DrawCreateServerStepperButton(minus, "-");
            DrawCreateServerStepperButton(plus, "+");
        }
        var size = _font.MeasureString(value) * 0.58f;
        _spriteBatch.DrawString(_font, value, new Vector2(minus.Right + (plus.X - minus.Right) / 2f - size.X / 2f, minus.Y + 4), SettingsAccentGold, 0f, Vector2.Zero, 0.58f, SpriteEffects.None, 0f);
    }
}
