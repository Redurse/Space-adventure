using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client;

// The waiting room's panels, one drawing function each (where they sit is Game1.Lobby.cs; the own card's tabs Game1.Lobby.OwnCard.cs; the ship list and its
// picture Game1.Lobby.Ships.cs; the saved campaigns Game1.Lobby.Saves.cs).
public partial class Game1
{
    private const float LobbyChatScale = 0.45f;
    private const int LobbyChatLineHeight = 15;

    private static string LobbyRoleBlurb(CrewRole role) => role switch
    {
        CrewRole.Captain => "Штурвал, курс и решения.",
        CrewRole.Engineer => "Реактор, питание, проводка.",
        CrewRole.Mechanic => "Корпус, двигатели, пробоины.",
        CrewRole.Security => "Орудия, оборона, абордаж.",
        _ => "Лечит экипаж, работает со сканером.",
    };

    // ---- the banner across the top of the left panel ----

    private void DrawLobbyBanner(LobbySnapshot lobby)
    {
        DrawVerticalGradient(LobbyBanner, new Color(22, 46, 54), new Color(10, 18, 28), 12);
        // A few fixed stars, so the banner is not a flat bar.
        for (var i = 0; i < 70; i++)
        {
            var h = (uint)(i * 2654435761u + 12345u);
            var x = LobbyBanner.X + (int)(h % (uint)LobbyBanner.Width);
            var y = LobbyBanner.Y + (int)((h >> 12) % (uint)LobbyBanner.Height);
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, (h >> 24) % 5 == 0 ? 2 : 1, (h >> 24) % 5 == 0 ? 2 : 1), Color.White * (0.15f + 0.4f * ((h >> 8) % 100) / 100f));
        }
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, LobbyBanner, SettingsBorderSteel * 0.8f, 1);

        _spriteBatch.DrawString(_font, FitText(lobby.ServerName, 480, 1.25f), new Vector2(LobbyBanner.X + 14, LobbyBanner.Y + 6), SettingsAccentGold, 0f, Vector2.Zero, 1.25f, SpriteEffects.None, 0f);

        var host = lobby.Players.FirstOrDefault(p => p.IsHost)?.Nickname;
        var x0 = LobbyBanner.X + 14;
        x0 += DrawLobbyChip(x0, LobbyBanner.Y + 40, lobby.RoundInProgress ? "Раунд идёт" : "Лобби", lobby.RoundInProgress ? SettingsAccentGold : SettingsAccentTeal) + 6;
        x0 += DrawLobbyChip(x0, LobbyBanner.Y + 40, $"Игроков {lobby.Players.Count}/{lobby.MaxPlayers}", SettingsBorderSteel) + 6;
        if (host is not null)
            x0 += DrawLobbyChip(x0, LobbyBanner.Y + 40, "Хост: " + FitText(host, 120, 0.5f), SettingsBorderSteel) + 6;
        if (lobby.Settings is { OpenServer: false })
            DrawLobbyChip(x0, LobbyBanner.Y + 40, "Закрыт", SettingsAccentRed);

        // The message of the day, if the host wrote one: a line here, all of it in the tip.
        var motd = lobby.Settings?.Motd ?? "";
        var motdRect = new Rectangle(LobbyBanner.X + 400, LobbyBanner.Y + 40, 216, 18);
        _spriteBatch.Draw(_pixel, motdRect, Color.Black * 0.35f);
        _spriteBatch.DrawString(_font, FitText(motd.Length > 0 ? motd : "Описание не задано", motdRect.Width - 10, 0.45f), new Vector2(motdRect.X + 5, motdRect.Y + 3),
            motd.Length > 0 ? SettingsTextPrimary : SettingsTextDim * 0.7f, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        if (motd.Length > 0)
            RegisterLobbyTip(motdRect, motd);

        var hover = SettingsHoverEase("lobby:serverbutton", LobbyServerButton.Contains(_designMouse));
        _spriteBatch.Draw(_pixel, LobbyServerButton, Color.Lerp(new Color(70, 80, 92), new Color(104, 118, 134), hover) * 0.9f);
        DrawBevel(LobbyServerButton, raised: true, strength: 0.6f);
        DrawCenteredText("НАСТРОЙКИ СЕРВЕРА", LobbyServerButton, 0.5f, Color.White);
        RegisterLobbyTip(LobbyServerButton, "Название, сообщение дня, правила раунда, чёрный список. Меняет хост; остальные видят настройки.");
    }

    // A small tag; returns its width.
    private int DrawLobbyChip(int x, int y, string text, Color color)
    {
        var width = (int)(_font.MeasureString(text).X * 0.5f) + 16;
        var rect = new Rectangle(x, y, width, 18);
        _spriteBatch.Draw(_pixel, rect, color * 0.35f);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, rect, color * 0.9f, 1);
        _spriteBatch.DrawString(_font, text, new Vector2(x + 8, y + 3), SettingsTextPrimary, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        return width;
    }

    // ---- the game mode column (there is the one mode) ----

    private void DrawLobbyModeColumn(LobbySnapshot lobby)
    {
        DrawLobbyHeader("РЕЖИМ ИГРЫ", LobbyModeBox.X, LobbyModeBox.Y - 18);
        DrawLobbyInset(LobbyModeBox);

        var card = new Rectangle(LobbyModeBox.X + 6, LobbyModeBox.Y + 6, LobbyModeBox.Width - 12, 150);
        _spriteBatch.Draw(_pixel, card, SettingsAccentTeal * 0.12f);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, card, SettingsAccentTeal * 0.7f, 1);
        _spriteBatch.DrawString(_font, "СОВМЕСТНОЕ ВЫЖИВАНИЕ", new Vector2(card.X + 8, card.Y + 8), SettingsAccentTeal, 0f, Vector2.Zero, 0.58f, SpriteEffects.None, 0f);
        var lines = WrapText("Экипаж живёт на корабле: летает между системами, торгует, воюет, чинит и выживает. На галактической карте Союз воюет с восстанием.", card.Width - 16, 0.5f);
        for (var i = 0; i < lines.Count; i++)
            _spriteBatch.DrawString(_font, lines[i], new Vector2(card.X + 8, card.Y + 30 + i * 15), SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

        var taking = lobby.Players.Count(p => !p.IsAfk);
        var ready = lobby.Players.Count(p => !p.IsAfk && p.IsReady);
        _spriteBatch.DrawString(_font, $"Готовы: {ready} из {taking}", new Vector2(LobbyModeBox.X + 12, card.Bottom + 12), SettingsTextPrimary, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        if (lobby.Settings is { StartWhenReady: true } settings)
            _spriteBatch.DrawString(_font, $"Старт сам при {settings.StartWhenReadyPercent}% готовых", new Vector2(LobbyModeBox.X + 12, card.Bottom + 28), SettingsAccentTeal, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }

    // ---- the tabs under the ship list: the round's settings, and the saved campaigns ----

    private void DrawLobbyBottomTabs(LobbySnapshot lobby, bool isHost)
    {
        DrawLobbyTabHeader("НАСТРОЙКИ РАУНДА", LobbyTabNew, _lobbyBottomTab == LobbyBottomTab.Settings, "lobby:tabnew");
        DrawLobbyTabHeader("ЗАГРУЗИТЬ КАМПАНИЮ", LobbyTabLoad, _lobbyBottomTab == LobbyBottomTab.Load, "lobby:tabload");
        if (!isHost)
            DrawRightText("меняет хост", LobbySettingsBox.Right, LobbyTabNew.Y + 4, 0.45f, SettingsTextDim);
        DrawLobbyInset(LobbySettingsBox);

        if (_lobbyBottomTab == LobbyBottomTab.Load)
            DrawLobbySavesTab(lobby, isHost);
        else
            DrawLobbySettingsTab(lobby, isHost);
    }

    private void DrawLobbySettingsTab(LobbySnapshot lobby, bool isHost)
    {
        var settings = lobby.Settings ?? new LobbySettings();
        var loaded = lobby.LoadedSaveTitle is not null;

        _spriteBatch.DrawString(_font, "Стартовые кредиты", new Vector2(34, 372), loaded ? SettingsTextDim : SettingsTextPrimary, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        DrawLobbyStepper(LobbyCreditsMinus, LobbyCreditsPlus, settings.StartCredits.ToString(), isHost && !loaded);
        RegisterLobbyTip(new Rectangle(34, 370, 160, 52), loaded ? "Кредиты берутся из загруженной кампании." : "Сколько денег у экипажа в начале новой кампании.");
        if (loaded)
            _spriteBatch.DrawString(_font, "из сохранения", new Vector2(34, 426), SettingsTextDim, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

        DrawLobbyCheckbox(LobbyWarCheckbox, "Галактическая война (Союз и восстание)", settings.GalacticWar, isHost);
        RegisterLobbyTip(new Rectangle(214, 376, 296, 24), "На галактической карте идёт война Союза с восстанием. Работает в игре с сохранением.");
        DrawLobbyCheckbox(LobbyDockedCheckbox, "Начать пристыкованным к станции", settings.StartDocked, isHost && !loaded);
        RegisterLobbyTip(new Rectangle(214, 406, 296, 24), "Корабль в начале уже состыкован с домашней станцией, а не стоит рядом с ней.");

        _spriteBatch.DrawString(_font, "МОДИФИКАТОРЫ КАМПАНИИ", new Vector2(520, 368), SettingsTextPrimary, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        var rows = new (string Label, float Value, string Tip)[]
        {
            ("Награды за задания", settings.RewardMultiplier, "Во сколько раз платят за выполненные задания."),
            ("Цены в магазине", settings.ShopPriceMultiplier, "Во сколько раз торговец повышает цены (продажа ему меняется так же)."),
            ("Цены на верфи", settings.ShipyardPriceMultiplier, "Во сколько раз дороже строить отсеки и двери на станции."),
        };
        for (var i = 0; i < rows.Length; i++)
        {
            var minus = LobbyModifierMinus(i);
            _spriteBatch.DrawString(_font, rows[i].Label, new Vector2(520, minus.Y + 4), SettingsTextPrimary, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            DrawLobbyStepper(minus, LobbyModifierPlus(i), $"{rows[i].Value:0.0}", isHost);
            RegisterLobbyTip(new Rectangle(516, minus.Y - 2, 266, 26), rows[i].Tip);
        }
    }

    // ---- the chat, and the list of players beside it ----

    private void DrawLobbyChat(LobbySnapshot lobby)
    {
        DrawLobbyTab("ЧАТ", new Rectangle(LobbyChatBox.X, LobbyChatBox.Y - 22, LobbyChatBox.Width, 20));
        var box = LobbyChatBox;
        DrawLobbyInset(box);

        // The newest lines that fit, each wrapped to the box's width; drawn bottom-up so the latest is always in view.
        var rows = new List<string>();
        foreach (var line in lobby.Chat ?? Array.Empty<LobbyChatLine>())
        {
            var wrapped = WrapText($"{line.Author}: {line.Text}", box.Width - 12, LobbyChatScale);
            for (var i = 0; i < wrapped.Count; i++)
                rows.Add(i == 0 ? wrapped[i] : "   " + wrapped[i]);
        }
        var visible = (box.Height - 8) / LobbyChatLineHeight;
        var first = Math.Max(0, rows.Count - visible);
        for (var i = first; i < rows.Count; i++)
            _spriteBatch.DrawString(_font, rows[i], new Vector2(box.X + 6, box.Y + 4 + (i - first) * LobbyChatLineHeight), SettingsTextPrimary, 0f, Vector2.Zero, LobbyChatScale, SpriteEffects.None, 0f);

        var input = LobbyChatInput;
        _spriteBatch.Draw(_pixel, input, new Color(10, 12, 16) * 0.9f);
        DrawBevel(input, raised: false);
        var typed = _lobbyChatInput;
        // Only the end of a long line is shown, so the cursor is always in view.
        while (typed.Length > 1 && _font.MeasureString(typed + "_").X * 0.55f > input.Width - 14)
            typed = typed[1..];
        _spriteBatch.DrawString(_font, typed.Length == 0 ? "Сообщение... (Enter - отправить)" : typed + "_", new Vector2(input.X + 6, input.Y + 5),
            typed.Length == 0 ? SettingsTextDim * 0.8f : SettingsAccentTeal, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private void DrawLobbyPlayers(LobbySnapshot lobby, bool isHost)
    {
        DrawLobbyTab($"ИГРОКИ {lobby.Players.Count}/{lobby.MaxPlayers}", new Rectangle(LobbyPlayersBox.X, LobbyPlayersBox.Y - 22, LobbyPlayersBox.Width, 20));
        DrawLobbyInset(LobbyPlayersBox);

        for (var i = 0; i < Math.Min(lobby.Players.Count, 8); i++)
        {
            var player = lobby.Players[i];
            var row = LobbyPlayerRow(i);
            _spriteBatch.Draw(_pixel, row, new Color(22, 27, 34));
            HudIcons.DrawRoleGlyph(_spriteBatch, _pixel, new Vector2(row.X + 10, row.Center.Y), 0.55f, CrewSkin.UniformFor(player.Role, isBot: false) * 1.4f, player.Role);

            var kickable = isHost && !player.IsHost;
            var nameRight = row.Right - (kickable ? 64 : 24) - 26;
            var name = (player.Nickname ?? "...") + (player.IsHost ? " *" : "");
            _spriteBatch.DrawString(_font, FitText(name, nameRight - (row.X + 22), 0.5f), new Vector2(row.X + 22, row.Y + 4),
                player.PlayerId == _client?.PlayerId ? SettingsAccentTeal : player.IsHost ? SettingsAccentGold : player.IsAfk ? SettingsTextDim : SettingsTextPrimary, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

            // Away / watching, as a short tag before the ready square.
            if (player.IsAfk || player.IsSpectator)
            {
                _spriteBatch.DrawString(_font, player.IsAfk ? "AFK" : "наб.", new Vector2(nameRight + 2, row.Y + 5), SettingsTextDim, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
                RegisterLobbyTip(new Rectangle(nameRight, row.Y, 26, row.Height), player.IsAfk ? "Не участвует в раунде и ждёт в лобби." : "Смотрит раунд без своего персонажа.");
            }
            // Ready: a filled square; not yet: an empty one.
            var tick = new Rectangle(nameRight + 28, row.Y + 5, 11, 11);
            _spriteBatch.Draw(_pixel, tick, player.IsReady ? SettingsAccentTeal : new Color(14, 17, 22));
            ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, tick, player.IsReady ? SettingsAccentTeal : SettingsBorderSteel, 1);

            if (!kickable)
                continue;
            DrawLobbyRowButton(LobbyKickButton(i), "x", new Color(60, 30, 30), new Color(130, 44, 44), $"lobby:kick:{i}", "Убрать из лобби (можно зайти снова).");
            DrawLobbyRowButton(LobbyBanButton(i), "!", new Color(62, 48, 22), new Color(140, 100, 30), $"lobby:ban:{i}", "В чёрный список: убрать и не пускать с этого адреса.");
        }
    }

    private void DrawLobbyRowButton(Rectangle rect, string glyph, Color idle, Color hot, string hoverKey, string tip)
    {
        var hover = SettingsHoverEase(hoverKey, rect.Contains(_designMouse));
        _spriteBatch.Draw(_pixel, rect, Color.Lerp(idle, hot, hover));
        DrawBevel(rect, raised: true, strength: 0.6f);
        DrawCenteredText(glyph, rect, 0.55f, SettingsTextPrimary);
        RegisterLobbyTip(rect, tip);
    }

    private void DrawLobbyTab(string text, Rectangle rect)
    {
        _spriteBatch.Draw(_pixel, rect, SettingsBorderSteel * 0.55f);
        DrawBevel(rect, raised: true, strength: 0.5f);
        DrawCenteredText(text, rect, 0.5f, SettingsTextPrimary);
    }
}
