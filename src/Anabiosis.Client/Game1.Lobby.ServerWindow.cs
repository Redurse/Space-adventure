using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client;

// The "Настройки сервера" window over the lobby (Barotrauma's, with its four tabs: Сервер, Общие, Анти-грифинг, Чёрный список). The host changes what has a real
// mechanism behind it - the server's name and message, whether it is announced on the network, voice chat, watching, away, starting when the players are ready -
// and the black list; everyone else sees the same window read-only. The rest of Barotrauma's parameters are drawn, dimmed, so the window reads the same, and say in
// their tip that they change nothing yet. A row is described once and used for drawing and for clicks alike.
public partial class Game1
{
    private static readonly Rectangle ServerWindowRect = new(190, 30, 820, 500);
    private static readonly Rectangle ServerWindowContent = new(200, 102, 800, 384);
    private static readonly Rectangle ServerWindowClose = new(880, 494, 120, 26);
    private static Rectangle ServerWindowTabRect(int index) => new(200 + index * 201, 70, 197, 24);
    private static readonly string[] ServerWindowTabNames = { "СЕРВЕР", "ОБЩИЕ", "АНТИ-ГРИФИНГ", "ЧЁРНЫЙ СПИСОК" };

    private const string InertTip = "Пока ничего не меняет: в игре нет такой механики. Показано, как в Баротравме.";

    private bool _serverWindowOpen;
    private int _serverWindowTab;
    private int _serverWindowScroll;
    private string _editServerName = "";
    private string _editMotd = "";

    private enum ServerRowKind { Header, Check, Choice, Text, Memo, Slider, Button, Ban, Note }

    private sealed record ServerRow(ServerRowKind Kind, string Label, string Value = "", bool On = false, bool Real = false, string? Tip = null,
        Action? Click = null, Action<float>? SetFraction = null, float Fraction = 0f, LobbyTextTarget? Field = null, string? BanId = null)
    {
        public int Height => Kind switch
        {
            ServerRowKind.Header => 30,
            ServerRowKind.Memo => 120,
            ServerRowKind.Slider => 28,
            ServerRowKind.Note => 22,
            _ => 27,
        };
    }

    private void OpenServerWindow(LobbySnapshot lobby)
    {
        _serverWindowOpen = true;
        _serverWindowScroll = 0;
        _editServerName = lobby.Settings is { ServerName.Length: > 0 } s ? s.ServerName : lobby.ServerName;
        _editMotd = lobby.Settings?.Motd ?? "";
    }

    private void CloseServerWindow()
    {
        if (_client?.LatestLobby is { } lobby && lobby.Players.FirstOrDefault(p => p.PlayerId == _client.PlayerId)?.IsHost == true)
            CommitServerTextEdits(lobby);
        _serverWindowOpen = false;
        _lobbyTextTarget = LobbyTextTarget.Chat;
    }

    // Sends the name and the message as they were typed (when they differ from what the server has).
    private void CommitServerTextEdits(LobbySnapshot lobby)
    {
        var settings = lobby.Settings ?? new LobbySettings();
        var name = _editServerName.Trim();
        var motd = _editMotd.Trim();
        var currentName = settings.ServerName.Length > 0 ? settings.ServerName : lobby.ServerName;
        if ((name.Length > 0 && name != currentName) || motd != settings.Motd)
            SendLobbySettings(settings with { ServerName = name.Length > 0 ? name : settings.ServerName, Motd = motd });
    }

    // ---- what each tab holds ----

    private List<ServerRow> BuildServerRows(LobbySnapshot lobby, bool isHost)
    {
        var s = lobby.Settings ?? new LobbySettings();
        void Update(Func<LobbySettings, LobbySettings> change) => SendLobbySettings(change(s));
        ServerRow Check(string label, bool on, string? tip, Action? click) => new(ServerRowKind.Check, label, On: on, Real: click is not null, Tip: tip ?? (click is null ? InertTip : null), Click: click);
        ServerRow Inert(string label, bool on) => Check(label, on, null, null);
        ServerRow Choice(string label, string value) => new(ServerRowKind.Choice, label, value, Tip: InertTip);
        ServerRow Header(string label) => new(ServerRowKind.Header, label);

        var rows = new List<ServerRow>();
        switch (_serverWindowTab)
        {
            case 0:
                rows.Add(Check("Открытый сервер", s.OpenServer, "Показывать сервер в списке игр локальной сети. Если выключить, зайти можно только по адресу.", isHost ? () => Update(x => x with { OpenServer = !x.OpenServer }) : null));
                rows.Add(new ServerRow(ServerRowKind.Text, "Название сервера", _editServerName, Real: isHost, Tip: "Видно в списке серверов и в шапке лобби. Enter - применить.", Field: LobbyTextTarget.ServerName));
                rows.Add(new ServerRow(ServerRowKind.Memo, "Сообщение дня", _editMotd, Real: isHost, Tip: "Показывается в шапке лобби всем игрокам. Enter - применить.", Field: LobbyTextTarget.Motd));
                rows.Add(Choice("Стиль игры", "Развлечение"));
                rows.Add(new ServerRow(ServerRowKind.Button, "Пароль", "Установить пароль", Tip: InertTip));
                rows.Add(Inert("Блокировка после неверного пароля", false));
                rows.Add(Choice("Повтор пароля перед блокировкой", "3"));
                rows.Add(new ServerRow(ServerRowKind.Choice, "Макс. игроков", lobby.MaxPlayers.ToString(), Tip: "Число мест задаётся при создании сервера."));
                rows.Add(Choice("Язык", "Русский"));
                break;
            case 1:
                rows.Add(Header("УПРАВЛЕНИЕ РАУНДОМ"));
                rows.Add(Inert("Конец раунда решением голосования", true));
                rows.Add(Choice("Выбор подлодки", "Вручную"));
                rows.Add(Choice("Выбор режима игры", "Вручную"));
                rows.Add(Inert("Автоматический перезапуск", false));
                rows.Add(Check("Запуск по готовности игроков", s.StartWhenReady, "Раунд начнётся сам, когда готова нужная доля игроков (те, кто в AFK, не считаются).",
                    isHost ? () => Update(x => x with { StartWhenReady = !x.StartWhenReady }) : null));
                rows.Add(new ServerRow(ServerRowKind.Slider, $"Требуется готовность {s.StartWhenReadyPercent}% игроков", Real: isHost && s.StartWhenReady, Fraction: (s.StartWhenReadyPercent - 10) / 90f,
                    Tip: "Какая доля игроков должна отметить «Готов начать».",
                    SetFraction: isHost ? f => Update(x => x with { StartWhenReadyPercent = (int)(MathF.Round((10f + f * 90f) / 10f) * 10f) }) : null));
                rows.Add(Inert("Задавать уровню новый случайный шифр между раундами", true));
                rows.Add(Header("PVP"));
                rows.Add(Choice("Режим выбора команды", "Предпочтение игрока"));
                rows.Add(Choice("Порог автобаланса команд", "1"));
                rows.Add(Header("ГЕЙМПЛЕЙ"));
                rows.Add(Check("Голосовой чат включён", s.VoiceChat, "Если выключить, голос в раунде не передаётся никому.", isHost ? () => Update(x => x with { VoiceChat = !x.VoiceChat }) : null));
                rows.Add(Check("Разрешить наблюдение", s.AllowSpectate, "Можно ли отмечаться «Наблюдать». Выключение возвращает всех наблюдателей в игроки.", isHost ? () => Update(x => x with { AllowSpectate = !x.AllowSpectate }) : null));
                rows.Add(Check("Разрешить AFK", s.AllowAfk, "Можно ли отмечаться AFK. Выключение возвращает всех в игроки.", isHost ? () => Update(x => x with { AllowAfk = !x.AllowAfk }) : null));
                rows.Add(Choice("Линия видимости", "Полупрозрачная"));
                rows.Add(Choice("Индикаторы здоровья врагов", "Показать все"));
                rows.Add(Inert("Отключить реплики ботов", false));
                break;
            case 2:
                rows.Add(new ServerRow(ServerRowKind.Note, "Эти правила показаны, как в Баротравме, и пока ничего не меняют.") { });
                rows.Add(Inert("Разрешить огонь по своим", true));
                rows.Add(Inert("НИПов на аванпостах можно убить", true));
                rows.Add(Inert("Блокировать всю штатную проводку", false));
                rows.Add(Inert("Разрешить привязку wifi-компонентов к чату", false));
                rows.Add(Inert("Разрешить немедленную доставку предмета", true));
                rows.Add(Inert("Разрешить отдавать предметы перетаскиванием", true));
                rows.Add(Inert("Аванпосты можно разрушить", false));
                rows.Add(Inert("Разрешить работу с проводкой", true));
                rows.Add(Inert("Разрешить маскировку", true));
                rows.Add(Inert("Разрешить исключение игроков голосованием", true));
                rows.Add(new ServerRow(ServerRowKind.Choice, "Максимальный денежный перевод", "999999", Tip: InertTip));
                rows.Add(Choice("Назначение собранных денег", "Банк"));
                rows.Add(Inert("Включить защиту от DoS-атак", true));
                rows.Add(Header("КАРМА"));
                rows.Add(Inert("Использовать карму", false));
                break;
            default:
                if (lobby.Bans is not { Count: > 0 } bans)
                {
                    rows.Add(new ServerRow(ServerRowKind.Note, "Список пуст. Игрока можно добавить кнопкой «!» рядом с ним в списке игроков лобби."));
                    break;
                }
                foreach (var ban in bans)
                    rows.Add(new ServerRow(ServerRowKind.Ban, ban.Nickname, ban.Id, Real: isHost, BanId: ban.Id));
                break;
        }
        return rows;
    }

    // Which rows fit from `scroll` on, and where; the same answer for drawing and for a click.
    private List<(ServerRow Row, Rectangle Rect)> LayoutServerRows(List<ServerRow> rows, int scroll)
    {
        var result = new List<(ServerRow, Rectangle)>();
        var y = ServerWindowContent.Y + 8;
        for (var i = Math.Clamp(scroll, 0, Math.Max(0, rows.Count - 1)); i < rows.Count; i++)
        {
            if (y + rows[i].Height > ServerWindowContent.Bottom - 4)
                break;
            result.Add((rows[i], new Rectangle(ServerWindowContent.X, y, ServerWindowContent.Width, rows[i].Height)));
            y += rows[i].Height;
        }
        return result;
    }

    // The largest scroll that still shows the last row.
    private int MaxServerScroll(List<ServerRow> rows)
    {
        for (var scroll = 0; scroll < rows.Count; scroll++)
            if (LayoutServerRows(rows, scroll).Count + scroll >= rows.Count)
                return scroll;
        return Math.Max(0, rows.Count - 1);
    }

    // ---- clicks ----

    private bool HandleServerWindowClick(LobbySnapshot lobby, bool isHost, Point point)
    {
        var previous = _lobbyTextTarget;
        _lobbyTextTarget = LobbyTextTarget.Chat;

        bool Done(bool playClick = true)
        {
            if (playClick)
                PlayUiClick();
            if (isHost && previous is LobbyTextTarget.ServerName or LobbyTextTarget.Motd && _lobbyTextTarget != previous)
                CommitServerTextEdits(lobby);
            return true;
        }

        if (ServerWindowClose.Contains(point) || !ServerWindowRect.Contains(point))
        {
            PlayUiClick();
            CloseServerWindow();
            return true;
        }
        for (var i = 0; i < ServerWindowTabNames.Length; i++)
        {
            if (!ServerWindowTabRect(i).Contains(point))
                continue;
            _serverWindowTab = i;
            _serverWindowScroll = 0;
            return Done();
        }

        var rows = BuildServerRows(lobby, isHost);
        _serverWindowScroll = Math.Clamp(_serverWindowScroll, 0, MaxServerScroll(rows));
        foreach (var (row, rect) in LayoutServerRows(rows, _serverWindowScroll))
        {
            if (!rect.Contains(point) || !row.Real)
                continue;
            switch (row.Kind)
            {
                case ServerRowKind.Check:
                    if (new Rectangle(rect.X + 12, rect.Y, 380, rect.Height).Contains(point))
                    {
                        row.Click?.Invoke();
                        return Done();
                    }
                    break;
                case ServerRowKind.Slider:
                    var track = ServerSliderTrack(rect);
                    if (new Rectangle(track.X - 6, rect.Y, track.Width + 12, rect.Height).Contains(point))
                    {
                        row.SetFraction?.Invoke(Math.Clamp((point.X - track.X) / (float)track.Width, 0f, 1f));
                        return Done();
                    }
                    break;
                case ServerRowKind.Text:
                case ServerRowKind.Memo:
                    _lobbyTextTarget = row.Field ?? LobbyTextTarget.Chat;
                    return Done(playClick: false);
                case ServerRowKind.Ban:
                    if (ServerBanButton(rect).Contains(point) && row.BanId is { } id)
                    {
                        SendLobby(c => c with { LobbyUnbanId = id });
                        return Done();
                    }
                    break;
            }
        }
        return Done(playClick: false);
    }

    private static Rectangle ServerSliderTrack(Rectangle row) => new(row.X + 44, row.Y + 8, 300, 12);
    private static Rectangle ServerBanButton(Rectangle row) => new(row.Right - 110, row.Y + 3, 90, 20);

    // ---- drawing ----

    private void DrawServerWindow(LobbySnapshot lobby, bool isHost)
    {
        _drawingServerWindow = true;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, DesignWidth, DesignHeight), Color.Black * 0.62f);
        DrawLobbyPanelFrame(ServerWindowRect);
        _spriteBatch.DrawString(_font, "Настройки сервера", new Vector2(ServerWindowRect.X + 14, ServerWindowRect.Y + 10), SettingsAccentGold, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
        if (!isHost)
            DrawRightText("меняет хост", ServerWindowRect.Right - 16, ServerWindowRect.Y + 18, 0.5f, SettingsTextDim);
        for (var i = 0; i < ServerWindowTabNames.Length; i++)
            DrawLobbyTabHeader(ServerWindowTabNames[i], ServerWindowTabRect(i), _serverWindowTab == i, $"lobby:swtab{i}");

        DrawLobbyInset(ServerWindowContent);
        var rows = BuildServerRows(lobby, isHost);
        var max = MaxServerScroll(rows);
        _serverWindowScroll = Math.Clamp(_serverWindowScroll, 0, max);
        foreach (var (row, rect) in LayoutServerRows(rows, _serverWindowScroll))
            DrawServerRow(row, rect);
        if (max > 0)
        {
            var track = new Rectangle(ServerWindowContent.Right - 10, ServerWindowContent.Y + 6, 5, ServerWindowContent.Height - 12);
            _spriteBatch.Draw(_pixel, track, Color.White * 0.08f);
            var thumb = Math.Max(20, track.Height / 4);
            _spriteBatch.Draw(_pixel, new Rectangle(track.X, track.Y + (track.Height - thumb) * _serverWindowScroll / max, track.Width, thumb), SettingsBorderSteel);
        }

        DrawLobbyActionButton(ServerWindowClose, "ЗАКРЫТЬ", true, "lobby:swclose", "Закрыть окно (название и сообщение применяются).");
        _drawingServerWindow = false;
    }

    private void DrawServerRow(ServerRow row, Rectangle rect)
    {
        var dim = !row.Real;
        var textColor = dim ? SettingsTextDim * 0.8f : SettingsTextPrimary;
        RegisterLobbyTip(rect, row.Tip ?? "");
        switch (row.Kind)
        {
            case ServerRowKind.Header:
                _spriteBatch.DrawString(_font, row.Label, new Vector2(rect.X + 16, rect.Y + 8), SettingsTextPrimary, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
                break;
            case ServerRowKind.Note:
                _spriteBatch.DrawString(_font, row.Label, new Vector2(rect.X + 16, rect.Y + 4), SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
                break;
            case ServerRowKind.Check:
                DrawLobbyCheckbox(new Rectangle(rect.X + 16, rect.Y + 4, 18, 18), row.Label, row.On, row.Real);
                break;
            case ServerRowKind.Choice:
            case ServerRowKind.Button:
            {
                _spriteBatch.DrawString(_font, row.Label, new Vector2(rect.X + 16, rect.Y + 6), textColor, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
                var box = new Rectangle(rect.X + 440, rect.Y + 2, 300, 22);
                _spriteBatch.Draw(_pixel, box, new Color(6, 8, 11) * (dim ? 0.6f : 1f));
                ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, box, SettingsBorderSteel * 0.5f, 1);
                DrawCenteredText(row.Value, box, 0.5f, textColor);
                if (row.Kind == ServerRowKind.Choice)
                {
                    DrawCenteredText("<", new Rectangle(box.X - 22, box.Y, 20, 22), 0.6f, SettingsTextDim * 0.6f);
                    DrawCenteredText(">", new Rectangle(box.Right + 2, box.Y, 20, 22), 0.6f, SettingsTextDim * 0.6f);
                }
                break;
            }
            case ServerRowKind.Text:
            {
                _spriteBatch.DrawString(_font, row.Label, new Vector2(rect.X + 16, rect.Y + 6), textColor, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
                var box = new Rectangle(rect.X + 440, rect.Y + 2, 340, 22);
                var typing = _lobbyTextTarget == row.Field;
                _spriteBatch.Draw(_pixel, box, new Color(6, 8, 11));
                ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, box, typing ? SettingsAccentTeal : SettingsBorderSteel * 0.6f, 1);
                _spriteBatch.DrawString(_font, TailText(row.Value + (typing ? "_" : ""), box.Width - 12, 0.55f), new Vector2(box.X + 6, box.Y + 4), typing ? SettingsAccentTeal : textColor, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
                break;
            }
            case ServerRowKind.Memo:
            {
                _spriteBatch.DrawString(_font, row.Label, new Vector2(rect.X + 16, rect.Y + 4), textColor, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
                DrawRightText($"{row.Value.Length} / {LobbySettings.MaxMotdLength}", rect.Right - 20, rect.Y + 6, 0.5f, SettingsTextDim);
                var box = new Rectangle(rect.X + 16, rect.Y + 24, rect.Width - 56, 92);
                var typing = _lobbyTextTarget == row.Field;
                _spriteBatch.Draw(_pixel, box, new Color(6, 8, 11));
                ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, box, typing ? SettingsAccentTeal : SettingsBorderSteel * 0.6f, 1);
                var text = row.Value.Length == 0 && !typing ? "Нажмите сюда, чтобы написать сообщение сервера" : row.Value + (typing ? "_" : "");
                var lines = WrapText(text, box.Width - 16, 0.5f);
                for (var i = 0; i < Math.Min(lines.Count, 6); i++)
                    _spriteBatch.DrawString(_font, lines[i], new Vector2(box.X + 8, box.Y + 6 + i * 14), row.Value.Length == 0 && !typing ? SettingsTextDim * 0.7f : SettingsTextPrimary, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
                break;
            }
            case ServerRowKind.Slider:
            {
                var track = ServerSliderTrack(rect);
                _spriteBatch.Draw(_pixel, new Rectangle(track.X, track.Center.Y - 1, track.Width, 2), SettingsBorderSteel * (dim ? 0.4f : 1f));
                var knob = new Rectangle(track.X + (int)(track.Width * row.Fraction) - 6, track.Center.Y - 6, 12, 12);
                _spriteBatch.Draw(_pixel, knob, dim ? SettingsAccentTeal * 0.4f : SettingsAccentTeal);
                DrawBevel(knob, raised: true, strength: 0.7f);
                _spriteBatch.DrawString(_font, row.Label, new Vector2(track.Right + 20, rect.Y + 8), dim ? SettingsTextDim * 0.8f : SettingsTextPrimary, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
                break;
            }
            case ServerRowKind.Ban:
            {
                _spriteBatch.DrawString(_font, row.Label, new Vector2(rect.X + 16, rect.Y + 6), SettingsTextPrimary, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
                _spriteBatch.DrawString(_font, row.Value, new Vector2(rect.X + 300, rect.Y + 7), SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
                if (row.Real)
                    DrawLobbyActionButton(ServerBanButton(rect), "УБРАТЬ", true, $"lobby:unban:{row.BanId}", "Снять блокировку: этот адрес снова сможет зайти.");
                break;
            }
        }
    }

    // The end of `text` that fits `width` pixels at `scale`, so the cursor stays in view.
    private string TailText(string text, int width, float scale)
    {
        while (text.Length > 1 && _font.MeasureString(text).X * scale > width)
            text = text[1..];
        return text;
    }
}
