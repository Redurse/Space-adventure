using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client;

// The lobby's "Загрузить кампанию" tab: the campaigns the server has saved (one per game, kept by every autosave), a filter, two orders, and a button that makes the
// round continue the chosen one instead of starting a new campaign. The host chooses; everyone sees which was chosen.
public partial class Game1
{
    private static readonly Rectangle SaveFilterField = new(34, 368, 260, 22);
    private static readonly Rectangle SaveSortButton = new(302, 368, 180, 22);
    private static readonly Rectangle SaveListBox = new(34, 394, 740, 68);
    private static readonly Rectangle SaveLoadButton = new(640, 468, 134, 24);
    private static readonly Rectangle SaveResetButton = new(502, 468, 130, 24);
    private const int SaveRowHeight = 22;
    private const int SaveRowsVisible = 3;

    private string _lobbySaveFilter = "";
    private bool _lobbySaveSortByName;
    private int _lobbySaveScroll;
    private string? _lobbySelectedSaveId;

    private static Rectangle SaveRow(int visibleIndex) => new(SaveListBox.X + 2, SaveListBox.Y + 2 + visibleIndex * SaveRowHeight, SaveListBox.Width - 12, SaveRowHeight - 1);

    // What the list shows: the server's saves narrowed by the filter (title or ship), in the chosen order.
    private List<LobbySaveInfo> FilteredSaves(LobbySnapshot lobby)
    {
        var saves = (lobby.Saves ?? Array.Empty<LobbySaveInfo>()).AsEnumerable();
        var filter = _lobbySaveFilter.Trim();
        if (filter.Length > 0)
            saves = saves.Where(s => s.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase) || s.ShipName.Contains(filter, StringComparison.CurrentCultureIgnoreCase));
        return (_lobbySaveSortByName ? saves.OrderBy(s => s.Title, StringComparer.CurrentCultureIgnoreCase) : saves.OrderByDescending(s => s.LastPlayedUtc)).ToList();
    }

    private bool HandleLobbySavesClick(LobbySnapshot lobby, Point point)
    {
        if (SaveFilterField.Contains(point))
        {
            _lobbyTextTarget = LobbyTextTarget.SaveFilter;
            return true;
        }
        if (SaveSortButton.Contains(point))
        {
            PlayUiClick();
            _lobbySaveSortByName = !_lobbySaveSortByName;
            return true;
        }

        var saves = FilteredSaves(lobby);
        for (var i = 0; i < SaveRowsVisible; i++)
        {
            var index = _lobbySaveScroll + i;
            if (index >= saves.Count || !SaveRow(i).Contains(point))
                continue;
            PlayUiClick();
            _lobbySelectedSaveId = saves[index].Id;
            return true;
        }

        if (SaveLoadButton.Contains(point) && _lobbySelectedSaveId is { } id && (lobby.Saves?.Any(s => s.Id == id) ?? false))
        {
            PlayUiClick();
            SendLobby(c => c with { LobbyLoadSaveId = id });
            return true;
        }
        if (SaveResetButton.Contains(point) && lobby.LoadedSaveTitle is not null)
        {
            PlayUiClick();
            SendLobby(c => c with { LobbyLoadSaveId = "" });
            return true;
        }
        return false;
    }

    private void DrawLobbySavesTab(LobbySnapshot lobby, bool isHost)
    {
        // The filter.
        _spriteBatch.Draw(_pixel, SaveFilterField, new Color(10, 12, 16) * 0.9f);
        DrawBevel(SaveFilterField, raised: false);
        var typing = _lobbyTextTarget == LobbyTextTarget.SaveFilter;
        _spriteBatch.DrawString(_font, _lobbySaveFilter.Length == 0 && !typing ? "Фильтр" : _lobbySaveFilter + (typing ? "_" : ""),
            new Vector2(SaveFilterField.X + 6, SaveFilterField.Y + 5), _lobbySaveFilter.Length == 0 && !typing ? SettingsTextDim * 0.8f : SettingsAccentTeal, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

        var sortHover = SettingsHoverEase("lobby:savesort", SaveSortButton.Contains(_designMouse));
        _spriteBatch.Draw(_pixel, SaveSortButton, Color.Lerp(new Color(30, 36, 46), new Color(52, 60, 74), sortHover));
        DrawBevel(SaveSortButton, raised: true, strength: 0.5f);
        DrawCenteredText(_lobbySaveSortByName ? "По названию" : "Последний сыгран", SaveSortButton, 0.5f, SettingsTextPrimary);
        RegisterLobbyTip(SaveSortButton, "Нажмите, чтобы сменить порядок списка.");

        // The list.
        DrawLobbyInset(SaveListBox);
        var saves = FilteredSaves(lobby);
        _lobbySaveScroll = Math.Clamp(_lobbySaveScroll, 0, Math.Max(0, saves.Count - SaveRowsVisible));
        if (saves.Count == 0)
            DrawCenteredText(lobby.Saves is { Count: > 0 } ? "Ничего не найдено" : "Сохранённых кампаний пока нет: они появятся после первой стыковки в новой игре.", SaveListBox, 0.5f, SettingsTextDim);
        for (var i = 0; i < SaveRowsVisible; i++)
        {
            var index = _lobbySaveScroll + i;
            if (index >= saves.Count)
                break;
            var save = saves[index];
            var row = SaveRow(i);
            var selected = save.Id == _lobbySelectedSaveId;
            var chosen = lobby.LoadedSaveTitle == save.Title;
            if (selected)
                _spriteBatch.Draw(_pixel, row, SettingsAccentTeal * 0.22f);
            else if (isHost && row.Contains(_designMouse))
                _spriteBatch.Draw(_pixel, row, Color.White * 0.08f);
            _spriteBatch.DrawString(_font, FitText(save.Title, 170, 0.55f), new Vector2(row.X + 8, row.Y + 4), chosen ? SettingsAccentGold : SettingsTextPrimary, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, FitText(save.ShipName, 170, 0.5f), new Vector2(row.X + 200, row.Y + 5), SettingsAccentTeal * 0.9f, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, $"{save.Credits} кред.", new Vector2(row.X + 400, row.Y + 5), SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            DrawRightText(save.LastPlayedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"), row.Right - 8, row.Y + 5, 0.5f, SettingsAccentGold * 0.9f);
        }
        if (saves.Count > SaveRowsVisible)
        {
            var track = new Rectangle(SaveListBox.Right - 8, SaveListBox.Y + 2, 5, SaveListBox.Height - 4);
            _spriteBatch.Draw(_pixel, track, Color.White * 0.08f);
            var thumb = Math.Max(10, track.Height * SaveRowsVisible / saves.Count);
            _spriteBatch.Draw(_pixel, new Rectangle(track.X, track.Y + (track.Height - thumb) * _lobbySaveScroll / Math.Max(1, saves.Count - SaveRowsVisible), track.Width, thumb), SettingsBorderSteel);
        }

        // The state line and the buttons.
        if (lobby.LoadedSaveTitle is { } title)
        {
            _spriteBatch.DrawString(_font, "Будет продолжена: " + FitText(title, 360, 0.55f), new Vector2(34, 472), SettingsAccentGold, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            if (isHost)
                DrawLobbyActionButton(SaveResetButton, "НОВАЯ КАМПАНИЯ", true, "lobby:savereset", "Отказаться от загрузки: раунд начнёт новую кампанию.");
        }
        else
        {
            _spriteBatch.DrawString(_font, isHost ? "Выберите кампанию и нажмите «Загрузить»: раунд продолжит её, а не начнёт новую." : "Кампанию выбирает хост.",
                new Vector2(34, 472), SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }
        if (isHost)
            DrawLobbyActionButton(SaveLoadButton, "ЗАГРУЗИТЬ", _lobbySelectedSaveId is not null, "lobby:saveload", "Раунд продолжит выбранную кампанию.");
    }

    private void DrawLobbyActionButton(Rectangle rect, string label, bool enabled, string hoverKey, string tip)
    {
        var hover = SettingsHoverEase(hoverKey, enabled && rect.Contains(_designMouse));
        _spriteBatch.Draw(_pixel, rect, enabled ? Color.Lerp(new Color(60, 74, 90), new Color(96, 116, 138), hover) : new Color(30, 34, 42));
        DrawBevel(rect, raised: enabled, strength: 0.6f);
        DrawCenteredText(label, rect, 0.5f, enabled ? Color.White : SettingsTextDim * 0.7f);
        RegisterLobbyTip(rect, tip);
    }
}
