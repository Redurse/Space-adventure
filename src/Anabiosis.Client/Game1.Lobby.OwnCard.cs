using System;
using System.Linq;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client;

// This player's own card in the waiting room (the top of the right panel): watching / away, the name, and two tabs - role with the starting kit, and appearance.
public partial class Game1
{
    // What this player looks like and starts with; remembered between runs, sent to the server on entering a lobby and whenever it changes.
    private CharacterLook _ownLook = PlayerSettingsStore.LoadLook() ?? CharacterLook.Random(new Random());
    private int _ownKit = PlayerSettingsStore.LoadKitIndex();
    // Sent once with the first command of a round that did not start from a lobby (solo), so the character wears the chosen look there too.
    private CharacterLook? _pendingLookToSend;

    private const int LookRowHeight = 24;
    private const int LookControlsX = 930;
    private static int LookRowY(int row) => LobbyCardArea.Y + 8 + row * LookRowHeight;
    private static Rectangle LookGenderButton(int index) => new(828 + index * 104, LookRowY(0), 98, 20);
    private static Rectangle LookSliderTrack(int row) => new(LookControlsX, LookRowY(row) + 4, 118, 12);
    private static Rectangle LookSwatch(int row, int index) => new(LookControlsX + index * 22, LookRowY(row), 18, 18);
    private static readonly Rectangle LookDice = new(1124, 240, 44, 28);

    private enum LookRowKind { Hair = 1, Beard = 2, Accessory = 3, HairColor = 4, BeardColor = 5, Skin = 6 }

    private void SetOwnLook(CharacterLook look)
    {
        _ownLook = look.Clamped();
        PlayerSettingsStore.SaveLook(_ownLook);
        _pendingLookToSend = _ownLook;
        SendLobby(c => c with { LobbyLook = _ownLook });
    }

    private void SetOwnKit(int index)
    {
        _ownKit = Math.Clamp(index, 0, StartKits.PerRole - 1);
        PlayerSettingsStore.SaveKitIndex(_ownKit);
        SendLobby(c => c with { LobbyKitIndex = _ownKit });
    }

    // ---- clicks ----

    private bool HandleLobbyOwnCardClick(LobbySnapshot lobby, Point point)
    {
        var me = lobby.Players.FirstOrDefault(p => p.PlayerId == _client!.PlayerId);
        var settings = lobby.Settings ?? new LobbySettings();

        if (LobbySpectateArea.Contains(point) && !lobby.RoundInProgress && (settings.AllowSpectate || me?.IsSpectator == true))
        {
            PlayUiClick();
            var on = !(me?.IsSpectator ?? false);
            SendLobby(c => c with { LobbySpectate = on });
            return true;
        }
        if (LobbyAfkArea.Contains(point) && (settings.AllowAfk || me?.IsAfk == true))
        {
            PlayUiClick();
            var on = !(me?.IsAfk ?? false);
            SendLobby(c => c with { LobbyAfk = on });
            return true;
        }

        if (LobbyTabRole.Contains(point) || LobbyTabLook.Contains(point))
        {
            PlayUiClick();
            _lobbyRightTab = LobbyTabLook.Contains(point) ? LobbyRightTab.Look : LobbyRightTab.Role;
            return true;
        }

        if (me is { IsAfk: true } or { IsSpectator: true })
            return false; // no character to dress

        return _lobbyRightTab == LobbyRightTab.Role ? HandleRoleTabClick(point) : HandleLookTabClick(point);
    }

    private bool HandleRoleTabClick(Point point)
    {
        if (LobbyRoleSlot.Contains(point))
        {
            PlayUiClick();
            _lobbyRolePopupOpen = true;
            return true;
        }
        for (var i = 0; i < StartKits.PerRole; i++)
        {
            if (!LobbyKitCard(i).Contains(point))
                continue;
            PlayUiClick();
            SetOwnKit(i);
            return true;
        }
        return false;
    }

    private bool HandleLookTabClick(Point point)
    {
        for (var i = 0; i < 2; i++)
        {
            if (!LookGenderButton(i).Contains(point))
                continue;
            PlayUiClick();
            SetOwnLook(_ownLook with { Gender = i == 0 ? CharacterGender.Male : CharacterGender.Female });
            return true;
        }
        if (LookDice.Contains(point))
        {
            PlayUiClick();
            SetOwnLook(CharacterLook.Random(new Random()));
            return true;
        }

        var male = _ownLook.Gender == CharacterGender.Male;
        // The three sliders: a click on the track puts the marker at the nearest notch.
        (LookRowKind Row, int Count, bool Enabled)[] sliders =
        {
            (LookRowKind.Hair, CharacterLook.HairStyles, true),
            (LookRowKind.Beard, CharacterLook.BeardStyles, male),
            (LookRowKind.Accessory, CharacterLook.Accessories, true),
        };
        foreach (var (row, count, enabled) in sliders)
        {
            var track = LookSliderTrack((int)row);
            var hit = new Rectangle(track.X - 4, track.Y - 4, track.Width + 8, track.Height + 8);
            if (!enabled || !hit.Contains(point))
                continue;
            var index = Math.Clamp((int)MathF.Round((point.X - track.X) / (float)track.Width * (count - 1)), 0, count - 1);
            PlayUiClick();
            SetOwnLook(row switch
            {
                LookRowKind.Hair => _ownLook with { Hair = index },
                LookRowKind.Beard => _ownLook with { Beard = index },
                _ => _ownLook with { Accessory = index },
            });
            return true;
        }

        (LookRowKind Row, int Count, bool Enabled)[] swatchRows =
        {
            (LookRowKind.HairColor, CharacterLook.HairColors, true),
            (LookRowKind.BeardColor, CharacterLook.HairColors, male),
            (LookRowKind.Skin, CharacterLook.SkinTones, true),
        };
        foreach (var (row, count, enabled) in swatchRows)
        {
            for (var i = 0; i < count && enabled; i++)
            {
                if (!LookSwatch((int)row, i).Contains(point))
                    continue;
                PlayUiClick();
                SetOwnLook(row switch
                {
                    LookRowKind.HairColor => _ownLook with { HairColor = i },
                    LookRowKind.BeardColor => _ownLook with { BeardColor = i },
                    _ => _ownLook with { Skin = i },
                });
                return true;
            }
        }
        return false;
    }

    // ---- drawing ----

    private void DrawLobbyOwnCard(LobbySnapshot lobby)
    {
        var me = lobby.Players.FirstOrDefault(p => p.PlayerId == _client!.PlayerId);
        var settings = lobby.Settings ?? new LobbySettings();
        var spectating = me?.IsSpectator ?? false;
        var away = me?.IsAfk ?? false;

        DrawLobbyCheckbox(LobbySpectateCheckbox, "Наблюдать", spectating, editable: !lobby.RoundInProgress && (settings.AllowSpectate || spectating));
        RegisterLobbyTip(LobbySpectateArea, settings.AllowSpectate
            ? "Смотреть раунд без своего персонажа."
            : "Хост запретил наблюдение.");
        DrawLobbyCheckbox(LobbyAfkCheckbox, "AFK", away, editable: settings.AllowAfk || away);
        RegisterLobbyTip(LobbyAfkArea, settings.AllowAfk
            ? "Отметить себя как \"ушёл от клавиатуры\" (AFK), если не хотите участвовать в следующем раунде. В раунд вы не попадёте и будете ждать в лобби."
            : "Хост запретил AFK.");

        DrawLobbyInset(LobbyNickBar);
        DrawCenteredText(FitText(_nickname, LobbyNickBar.Width - 16, 0.6f), LobbyNickBar, 0.6f, SettingsAccentGold);

        DrawLobbyTabHeader("РОЛЬ И НАБОР", LobbyTabRole, _lobbyRightTab == LobbyRightTab.Role, "lobby:tabrole");
        DrawLobbyTabHeader("ВНЕШНОСТЬ", LobbyTabLook, _lobbyRightTab == LobbyRightTab.Look, "lobby:tablook");

        if (spectating || away)
        {
            DrawLobbyInset(LobbyCardArea);
            DrawCenteredText(spectating ? "Наблюдает" : "AFK", new Rectangle(LobbyCardArea.X, LobbyCardArea.Y + 50, LobbyCardArea.Width, 30), 1.1f, SettingsTextPrimary);
            DrawCenteredText(spectating ? "Вы увидите раунд, но персонажа у вас не будет." : "В раунд вы не попадёте и подождёте в лобби.",
                new Rectangle(LobbyCardArea.X, LobbyCardArea.Y + 90, LobbyCardArea.Width, 20), 0.5f, SettingsTextDim);
            return;
        }

        if (_lobbyRightTab == LobbyRightTab.Role)
            DrawRoleTab();
        else
            DrawLookTab();
    }

    // The figure, turning slowly on the spot - a showcase of what the role wears and how it looks.
    private void DrawLobbyFigure(Vector2 center, float pixelsPerUnit)
    {
        _lobbyCrewSkin ??= new CrewSkin(GraphicsDevice);
        var angle = Environment.TickCount64 / 1000f * 0.7f;
        _lobbyCrewSkin.DrawPosed(_spriteBatch, center, pixelsPerUnit, _client?.PlayerId ?? 1,
            CrewSkin.UniformFor(_selectedRole, isBot: false), CrewSkin.AccentFor(false), Outfit.Crew, new Vector2(MathF.Cos(angle), MathF.Sin(angle)), false, 0f, _ownLook);
    }

    private void DrawRoleTab()
    {
        DrawLobbyInset(LobbyPortraitBox);
        DrawLobbyFigure(new Vector2(LobbyPortraitBox.X + 62, LobbyPortraitBox.Center.Y), 120f);

        var textX = LobbyPortraitBox.X + 140;
        var roleName = _selectedRole is { } role ? CrewRoles.Name(role) : "Не выбрана";
        _spriteBatch.DrawString(_font, roleName, new Vector2(textX, LobbyPortraitBox.Y + 10), SettingsAccentGold, 0f, Vector2.Zero, 0.95f, SpriteEffects.None, 0f);
        var blurb = _selectedRole is { } r ? LobbyRoleBlurb(r) : "Выберите роль ниже.";
        var lines = WrapText(blurb, LobbyPortraitBox.Right - textX - 12, 0.5f);
        for (var i = 0; i < lines.Count; i++)
            _spriteBatch.DrawString(_font, lines[i], new Vector2(textX, LobbyPortraitBox.Y + 38 + i * 15), SettingsTextPrimary, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        var note = WrapText("Роль ничего не запрещает - это подсказка экипажу.", LobbyPortraitBox.Right - textX - 12, 0.42f);
        for (var i = 0; i < note.Count; i++)
            _spriteBatch.DrawString(_font, note[i], new Vector2(textX, LobbyPortraitBox.Bottom - 8 - (note.Count - i) * 12), SettingsTextDim, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

        // The role slot: the chosen role, and a click opens the picker.
        var slot = LobbyRoleSlot;
        var slotHover = SettingsHoverEase("lobby:roleslot", slot.Contains(_designMouse) || _lobbyRolePopupOpen);
        _spriteBatch.Draw(_pixel, slot, Color.Lerp(new Color(24, 29, 37), new Color(46, 54, 66), slotHover));
        DrawBevel(slot, raised: !_lobbyRolePopupOpen, strength: 0.5f);
        HudIcons.DrawRoleGlyph(_spriteBatch, _pixel, new Vector2(slot.X + 24, slot.Center.Y), 1.1f, Color.White, _selectedRole);
        _spriteBatch.DrawString(_font, roleName, new Vector2(slot.X + 52, slot.Y + 5), SettingsAccentGold, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, "Нажмите, чтобы выбрать другую роль", new Vector2(slot.X + 52, slot.Y + 22), SettingsTextDim, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        DrawRightText(_lobbyRolePopupOpen ? "<" : ">", slot.Right - 14, slot.Y + 10, 0.8f, SettingsTextDim);
        RegisterLobbyTip(slot, "Роль в экипаже. От неё зависят набор и цвет формы.");

        // The three starting kits for this role: what is in the inventory when the character appears.
        var kits = StartKits.For(_selectedRole);
        for (var i = 0; i < StartKits.PerRole; i++)
        {
            var card = LobbyKitCard(i);
            var selected = _ownKit == i;
            var hover = card.Contains(_designMouse);
            _spriteBatch.Draw(_pixel, card, selected ? SettingsAccentTeal * 0.22f : hover ? new Color(40, 48, 60) : new Color(18, 22, 29));
            DrawBevel(card, raised: !selected, strength: 0.45f);
            if (selected)
                ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, card, SettingsAccentTeal, 1);
            _spriteBatch.DrawString(_font, $"{i + 1}  " + FitText(kits[i].Name, card.Width - 28, 0.45f), new Vector2(card.X + 6, card.Y + 4), selected ? SettingsAccentTeal : SettingsTextPrimary,
                0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
            for (var item = 0; item < Math.Min(kits[i].Items.Count, 5); item++)
                ItemIcons.Draw(_spriteBatch, _pixel, kits[i].Items[item], new Rectangle(card.X + 6 + item * 20, card.Y + 20, 18, 18));
            if (hover)
                _lobbyTipKit = (kits[i], i + 1);
        }
    }

    private void DrawLookTab()
    {
        DrawLobbyInset(LobbyCardArea);
        var male = _ownLook.Gender == CharacterGender.Male;

        for (var i = 0; i < 2; i++)
        {
            var rect = LookGenderButton(i);
            var selected = (i == 0) == male;
            var hover = rect.Contains(_designMouse);
            _spriteBatch.Draw(_pixel, rect, selected ? SettingsBorderSteel * 0.8f : hover ? new Color(40, 48, 60) : new Color(18, 22, 29));
            DrawBevel(rect, raised: !selected, strength: 0.45f);
            DrawCenteredText(i == 0 ? "Мужчина" : "Женщина", rect, 0.5f, selected ? Color.White : SettingsTextDim);
        }

        DrawLookSlider(LookRowKind.Hair, "ВОЛОСЫ", _ownLook.Hair, CharacterLook.HairStyles, true, CharacterLook.HairNames);
        DrawLookSlider(LookRowKind.Beard, "БОРОДА", _ownLook.Beard, CharacterLook.BeardStyles, male, CharacterLook.BeardNames);
        DrawLookSlider(LookRowKind.Accessory, "АКСЕССУАРЫ", _ownLook.Accessory, CharacterLook.Accessories, true, CharacterLook.AccessoryNames);
        DrawLookSwatches(LookRowKind.HairColor, "ЦВЕТ ВОЛОС", _ownLook.HairColor, CharacterLook.HairColors, true, CrewSkin.HairColorAt);
        DrawLookSwatches(LookRowKind.BeardColor, "ЦВЕТ БОРОДЫ", _ownLook.BeardColor, CharacterLook.HairColors, male, CrewSkin.HairColorAt);
        DrawLookSwatches(LookRowKind.Skin, "ЦВЕТ КОЖИ", _ownLook.Skin, CharacterLook.SkinTones, true, CrewSkin.SkinToneAt);

        // The figure beside the controls, and the dice: a whole new look.
        var preview = new Rectangle(1066, LobbyCardArea.Y + 6, 110, 140);
        _spriteBatch.Draw(_pixel, preview, new Color(6, 8, 11));
        DrawLobbyFigure(new Vector2(preview.Center.X, preview.Center.Y), 150f);
        var diceHover = SettingsHoverEase("lobby:dice", LookDice.Contains(_designMouse));
        _spriteBatch.Draw(_pixel, LookDice, Color.Lerp(new Color(30, 70, 66), new Color(60, 140, 130), diceHover));
        DrawBevel(LookDice, raised: true, strength: 0.6f);
        DrawCenteredText("КУБИК", LookDice, 0.45f, Color.White);
        RegisterLobbyTip(LookDice, "Случайная внешность.");
    }

    private void DrawLookSlider(LookRowKind kind, string label, int value, int count, bool enabled, string[] names)
    {
        var row = (int)kind;
        var y = LookRowY(row);
        var color = enabled ? SettingsTextPrimary : SettingsTextDim * 0.6f;
        _spriteBatch.DrawString(_font, label, new Vector2(828, y + 3), color, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        var track = LookSliderTrack(row);
        _spriteBatch.Draw(_pixel, new Rectangle(track.X, track.Center.Y - 1, track.Width, 2), (enabled ? SettingsBorderSteel : SettingsBorderSteel * 0.4f));
        for (var i = 0; i < count; i++)
            _spriteBatch.Draw(_pixel, new Rectangle(track.X + (int)MathF.Round(i / (float)(count - 1) * track.Width) - 1, track.Center.Y - 3, 2, 6), SettingsBorderSteel * (enabled ? 0.9f : 0.3f));
        if (!enabled)
            return;
        var knobX = track.X + (int)MathF.Round(value / (float)(count - 1) * track.Width);
        var knob = new Rectangle(knobX - 6, track.Center.Y - 6, 12, 12);
        _spriteBatch.Draw(_pixel, knob, SettingsAccentTeal);
        DrawBevel(knob, raised: true, strength: 0.7f);
        RegisterLobbyTip(new Rectangle(track.X - 4, track.Y - 4, track.Width + 8, track.Height + 8), names[Math.Clamp(value, 0, names.Length - 1)]);
    }

    private void DrawLookSwatches(LookRowKind kind, string label, int value, int count, bool enabled, Func<int, Color> colorAt)
    {
        var row = (int)kind;
        var y = LookRowY(row);
        _spriteBatch.DrawString(_font, label, new Vector2(828, y + 3), enabled ? SettingsTextPrimary : SettingsTextDim * 0.6f, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        if (!enabled)
            return;
        for (var i = 0; i < count; i++)
        {
            var rect = LookSwatch(row, i);
            _spriteBatch.Draw(_pixel, rect, colorAt(i));
            ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, rect, i == value ? Color.White : Color.Black * 0.6f, i == value ? 2 : 1);
        }
    }

    // Every role in a grid, the chosen one lit; a click picks it.
    private void DrawLobbyRolePopup()
    {
        var box = LobbyRolePopup;
        _spriteBatch.Draw(_pixel, new Rectangle(box.X - 3, box.Y - 3, box.Width + 6, box.Height + 6), Color.Black * 0.5f);
        _spriteBatch.Draw(_pixel, box, new Color(10, 13, 18));
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, box, SettingsBorderSteel, 2);
        for (var i = 0; i < RoleChoices.Length; i++)
        {
            var role = RoleChoices[i];
            var cell = LobbyRolePopupCell(i);
            var selected = _selectedRole == role;
            var hover = cell.Contains(_designMouse);
            _spriteBatch.Draw(_pixel, cell, selected ? SettingsAccentGold * 0.28f : hover ? new Color(40, 48, 60) : new Color(18, 22, 29));
            DrawBevel(cell, raised: !selected, strength: 0.4f);
            if (selected)
                ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, cell, SettingsAccentGold, 1);
            // The uniform's colour behind the glyph, so the roles read at a glance.
            var swatch = new Rectangle(cell.Center.X - 22, cell.Y + 12, 44, 44);
            _spriteBatch.Draw(_pixel, swatch, CrewSkin.UniformFor(role, isBot: false) * 0.75f);
            ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, swatch, Color.Black * 0.5f, 1);
            HudIcons.DrawRoleGlyph(_spriteBatch, _pixel, new Vector2(swatch.Center.X, swatch.Center.Y), 1.3f, Color.White, role);
            DrawCenteredText(CrewRoles.Name(role), new Rectangle(cell.X, cell.Y + 62, cell.Width, 16), 0.55f, selected ? SettingsAccentGold : SettingsTextPrimary);
            var lines = WrapText(LobbyRoleBlurb(role), cell.Width - 10, 0.38f);
            for (var l = 0; l < Math.Min(lines.Count, 2); l++)
                DrawCenteredText(lines[l], new Rectangle(cell.X, cell.Y + 78 + l * 10, cell.Width, 10), 0.38f, SettingsTextDim);
        }
    }
}
