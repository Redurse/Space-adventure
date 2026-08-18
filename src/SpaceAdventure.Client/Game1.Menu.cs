using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceAdventure.Client.Networking;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Networking;

namespace SpaceAdventure.Client;

// Everything that happens before there is a world to render: picking a hull, opening the ship to a
// co-op crew, or joining someone else's. Two ways in, one outcome - a live GameClient - after which
// nothing in the rest of Game1 can tell which of them it was.
public partial class Game1
{
    private enum MenuScreen
    {
        Nickname,
        Role,
        Main,
        ShipSelect,
        Join,
    }

    private static readonly ShipKind[] SelectableShipKinds = { ShipKind.Scout, ShipKind.Frigate, ShipKind.Cruiser, ShipKind.Corvette };
    private static readonly CrewRole[] RoleChoices = { CrewRole.Captain, CrewRole.Engineer, CrewRole.Mechanic, CrewRole.Security, CrewRole.Medic };
    private const int RoleIconBoxSize = 70;
    private const int RoleIconGap = 30;
    private const int RoleIconsY = 220;

    // Shown first, every launch (not just the first one) - pre-filled from PlayerSettingsStore so
    // returning players just confirm rather than retype, but the screen itself always appears
    // rather than silently reusing whatever was saved.
    private MenuScreen _menuScreen = MenuScreen.Nickname;
    private string _nickname = PlayerSettingsStore.LoadNickname() ?? "";
    // Same "always ask, pre-filled from last time" shape as the nickname above - purely a
    // self-identification label (Character.cs's own comment), so there's no wrong answer and no
    // need to force a choice; Enter with nothing picked just continues without one, same as
    // today's default for a player who never opens the crew panel.
    private CrewRole? _selectedRole = PlayerSettingsStore.LoadRole();
    private bool _openToNetwork;
    private string _joinAddress = "127.0.0.1";
    private string? _joinError;
    // The join handshake talks to a machine that may not answer; running it on the game thread would
    // freeze the window for the whole timeout, so the menu keeps drawing while this is in flight.
    private Task<NetworkSession>? _joinTask;
    private KeyboardState _prevMenuKeyboard;
    private string? _localAddresses;
    // Edge-triggered like every other click handler in this project (Game1.Input.cs's own
    // _prevLeftMouseButton) - kept separate from that one since it only ever runs before a session
    // exists, but sharing the field would work just as well; two small fields is clearer than one
    // shared field whose name would otherwise suggest it's gameplay-only.
    private ButtonState _prevMenuLeftMouseButton = ButtonState.Released;

    // Registered once from Initialize: MonoGame's TextInput is the only way to read typed characters
    // with the keyboard layout applied, which an IP address entry (and a nickname, which has to
    // take Cyrillic/etc. through the same layout) needs.
    private void OnMenuTextInput(object? sender, TextInputEventArgs e)
    {
        if (_sessionStarted || _joinTask is not null)
            return;

        if (_menuScreen == MenuScreen.Nickname)
        {
            if (e.Character == '\b')
            {
                if (_nickname.Length > 0)
                    _nickname = _nickname[..^1];
                return;
            }
            // Anything printable and not a control character - a nickname isn't restricted to an
            // address's host/port alphabet the way the Join screen's box is.
            if (!char.IsControl(e.Character) && _nickname.Length < 20)
                _nickname += e.Character;
            return;
        }

        if (_menuScreen != MenuScreen.Join)
            return;

        if (e.Character == '\b')
        {
            if (_joinAddress.Length > 0)
                _joinAddress = _joinAddress[..^1];
            return;
        }

        // Host and port only - anything else in this box is a typo, not an address.
        if ((char.IsLetterOrDigit(e.Character) || e.Character is '.' or ':' or '-') && _joinAddress.Length < 40)
            _joinAddress += e.Character;
    }

    private void HandleMenu(KeyboardState keyboard)
    {
        if (_joinTask is not null)
        {
            PollJoin();
            _prevMenuKeyboard = keyboard;
            return;
        }

        if (_menuScreen == MenuScreen.Nickname)
            HandleNicknameScreen(keyboard);
        else if (_menuScreen == MenuScreen.Role)
            HandleRoleScreen(keyboard);
        else if (_menuScreen == MenuScreen.Main)
            HandleMainMenuClick();
        else if (_menuScreen == MenuScreen.ShipSelect)
            HandleShipSelect(keyboard);
        else
            HandleJoinScreen(keyboard);

        _prevMenuKeyboard = keyboard;
    }

    // Enter confirms whatever's typed (blank falls back to "Игрок" rather than sending an empty
    // name to the server) and remembers it for next launch - a fresh machine sees this screen
    // empty, but every launch after that starts pre-filled, satisfying "always ask" and "remember
    // between sessions" at once.
    private void HandleNicknameScreen(KeyboardState keyboard)
    {
        if (!Pressed(keyboard, Keys.Enter))
            return;

        _nickname = _nickname.Trim();
        if (_nickname.Length == 0)
            _nickname = "Игрок";

        PlayerSettingsStore.SaveNickname(_nickname);
        _menuScreen = MenuScreen.Role;
    }

    // Click one of the 5 roles to pick it, save it, and move on - or just press Enter to continue
    // with whatever's already selected (including none, same as today's default for a player who
    // never opens the crew panel in-game). The actual assignment happens once the session starts
    // (StartHostedSession/PollJoin queue it through the exact same _pendingSetOwnRoleTo the crew
    // panel's own click handler uses - Game1.Input.cs - so there's only one code path that ever
    // sends SetOwnRoleTo).
    private void HandleRoleScreen(KeyboardState keyboard)
    {
        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        _prevMenuLeftMouseButton = mouse.LeftButton;
        if (clicked)
        {
            for (var i = 0; i < RoleChoices.Length; i++)
            {
                if (!GetRoleChoiceRect(i).Contains(_designMouse))
                    continue;
                _selectedRole = RoleChoices[i];
                PlayerSettingsStore.SaveRole(_selectedRole);
                _menuScreen = MenuScreen.Main;
                return;
            }
        }

        if (Pressed(keyboard, Keys.Enter))
            _menuScreen = MenuScreen.Main;
    }

    private bool Pressed(KeyboardState keyboard, Keys key) => keyboard.IsKeyDown(key) && _prevMenuKeyboard.IsKeyUp(key);

    // Called by Update when Escape comes in: true means "handled, stay in the game" (steps back one
    // screen toward Main rather than quitting outright). Main itself has nowhere further back to
    // go, so Escape there falls through to Exit(), same as it always has.
    private bool LeaveSubScreen()
    {
        if (_menuScreen == MenuScreen.Join && _joinTask is null)
        {
            _menuScreen = MenuScreen.Main;
            _joinError = null;
            return true;
        }
        if (_menuScreen == MenuScreen.ShipSelect)
        {
            _menuScreen = MenuScreen.Main;
            return true;
        }
        return false;
    }

    // The main menu's own click targets - mouse-driven (unlike the keyboard-shortcut screens either
    // side of it), matching the reference layout's clickable list rather than "[1] Пункт" prompts.
    private void HandleMainMenuClick()
    {
        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        _prevMenuLeftMouseButton = mouse.LeftButton;
        if (!clicked)
            return;

        var point = _designMouse;
        if (GetMainMenuItemRect(0).Contains(point))
        {
            _menuScreen = MenuScreen.ShipSelect;
        }
        else if (_existingSave is { } save && GetMainMenuItemRect(1).Contains(point))
        {
            StartHostedSession(save.ShipKind, save);
        }
        else if (GetMainMenuItemRect(2).Contains(point))
        {
            _menuScreen = MenuScreen.Join;
            _joinError = null;
        }
        else if (GetMainMenuItemRect(4).Contains(point))
        {
            _menuScreen = MenuScreen.Nickname;
        }
        else if (GetMainMenuExitRect().Contains(point))
        {
            Exit();
        }
    }

    // Pressing 1-4 picks a class (game_design.md section 9) and only then spins up the embedded
    // server with that layout - SoloSession/GameServer/World all take the ShipKind at construction,
    // so it has to be known before the session starts.
    private void HandleShipSelect(KeyboardState keyboard)
    {
        // Toggled before starting, not after: the listen socket opens together with the server, and
        // a crew joins the ship its host already chose.
        if (Pressed(keyboard, Keys.H))
            _openToNetwork = !_openToNetwork;

        if (Pressed(keyboard, Keys.J))
        {
            _menuScreen = MenuScreen.Join;
            _joinError = null;
            return;
        }

        // C continues the autosaved run (game_design.md section 5) instead of picking a hull -
        // the saved game already knows which ship the crew flies.
        if (_existingSave is not null && keyboard.IsKeyDown(Keys.C))
        {
            StartHostedSession(_existingSave.ShipKind, _existingSave);
            return;
        }

        var index = keyboard.IsKeyDown(Keys.D1) ? 0
            : keyboard.IsKeyDown(Keys.D2) ? 1
            : keyboard.IsKeyDown(Keys.D3) ? 2
            : keyboard.IsKeyDown(Keys.D4) ? 3
            : -1;
        if (index < 0)
            return;

        // Starting fresh abandons the old run - the first docking would overwrite it anyway, so
        // clearing it now keeps "continue" from offering a save that no longer matches.
        SaveStore.Delete();
        StartHostedSession(SelectableShipKinds[index], loadFrom: null);
    }

    private void HandleJoinScreen(KeyboardState keyboard)
    {
        if (!Pressed(keyboard, Keys.Enter) || _joinAddress.Length == 0)
            return;

        var (host, port) = ParseAddress(_joinAddress);
        _joinError = null;
        _joinTask = Task.Run(() => NetworkSession.Join(host, port));
    }

    private void PollJoin()
    {
        if (!_joinTask!.IsCompleted)
            return;

        if (_joinTask.IsCompletedSuccessfully)
        {
            var session = _joinTask.Result;
            _session = session;
            _client = new GameClient(session.Connection, session.PlayerId);
            _sessionStarted = true;
            // Rides the same _pendingSetOwnRoleTo the in-game crew panel's own click handler uses
            // (Game1.Input.cs) - picked up on the very next Update, exactly like a manual click.
            _pendingSetOwnRoleTo = _selectedRole;
        }
        else
        {
            // Refused, timed out, wrong port - all the same to the player: it didn't work, try again.
            _joinError = (_joinTask.Exception?.GetBaseException() ?? new Exception("не удалось")).Message;
        }

        _joinTask = null;
    }

    private static (string Host, int Port) ParseAddress(string text)
    {
        var parts = text.Split(':', 2);
        return parts.Length == 2 && int.TryParse(parts[1], out var port)
            ? (parts[0], port)
            : (text, SpaceAdventure.Shared.Networking.Wire.DefaultPort);
    }

    // The host is a player like any other - their own session is the same SoloSession solo mode
    // uses, with the listen socket as the only difference.
    private void StartHostedSession(ShipKind shipKind, SaveGame? loadFrom)
    {
        var session = new SoloSession(shipKind, loadFrom, _openToNetwork ? SpaceAdventure.Shared.Networking.Wire.DefaultPort : null);
        _session = session;
        _client = new GameClient(session.Connection, session.PlayerId);
        _sessionStarted = true;
        // Rides the same _pendingSetOwnRoleTo the in-game crew panel's own click handler uses
        // (Game1.Input.cs) - picked up on the very next Update, exactly like a manual click.
        _pendingSetOwnRoleTo = _selectedRole;
    }

    // The pause menu's "ГЛАВНОЕ МЕНЮ" - the one path back to this screen that doesn't close the
    // whole process (unlike Escape/"ЗАКОНЧИТЬ РАУНД" at the ship-select stage, which do). Tears
    // down the live session exactly like the window-close path already does (UnloadContent's
    // `_session?.Dispose()`) and resets every bit of state a fresh session starts with, so picking
    // a new game (or joining one) afterward behaves identically to a cold launch.
    private void ReturnToMainMenu()
    {
        _session?.Dispose();
        _session = null;
        _client = null!;
        _sessionStarted = false;
        _existingSave = SaveStore.Load(); // pick up whatever the run just ending autosaved

        _pauseMenuOpen = false;
        _openBlock = ClickTarget.None;
        _crewPanelOpen = false;
        _infoPanelOpen = false;
        _shipEditorOpen = false;
        _talkingToNpcId = null;

        _menuScreen = MenuScreen.Main;
    }

    // What to tell the other players to type in. Resolved once, on demand - it never changes while
    // the game runs, and asking the OS for it costs a DNS round trip on some machines.
    private string LocalAddresses()
    {
        if (_localAddresses is not null)
            return _localAddresses;
        try
        {
            var addresses = Dns.GetHostAddresses(Dns.GetHostName())
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString())
                .ToArray();
            _localAddresses = addresses.Length > 0 ? string.Join(", ", addresses) : "адрес не определён";
        }
        catch (Exception)
        {
            _localAddresses = "адрес не определён";
        }
        return _localAddresses;
    }

    private void DrawMenu(float totalSeconds)
    {
        _spriteBatch.Begin(transformMatrix: _renderScale);
        if (_menuScreen == MenuScreen.Nickname)
            DrawNicknameScreen();
        else if (_menuScreen == MenuScreen.Role)
            DrawRoleScreen();
        else if (_menuScreen == MenuScreen.Main)
            DrawMainMenuScreen(totalSeconds);
        else if (_menuScreen == MenuScreen.ShipSelect)
            DrawShipSelectScreen();
        else
            DrawJoinScreen();
        _spriteBatch.End();
    }

    // Item indices, shared between the click handler above and the layout below, so a rect can
    // never drift out of sync with the row it's supposed to catch.
    private static Rectangle GetMainMenuItemRect(int index) => index switch
    {
        0 => new Rectangle(64, 60, 380, 20), // Новая игра
        1 => new Rectangle(64, 84, 380, 20), // Продолжить (only drawn/clickable with a save)
        2 => new Rectangle(64, 166, 380, 20), // Присоединиться
        3 => new Rectangle(64, 246, 380, 20), // Настройки (inert placeholder)
        4 => new Rectangle(64, 270, 380, 20), // Сменить ник
        _ => Rectangle.Empty,
    };

    private static Rectangle GetMainMenuExitRect() => new(24, 500, 160, 24);

    // A grouped, mouse-driven front screen (icon + colored header bar per section, sub-items
    // listed under it, a bare flat list at the bottom) instead of the "[1] Пункт" keyboard prompts
    // the screens either side of it still use - the layout the user asked to match, not a literal
    // reproduction of another game's content (this project has no tutorial/mods/settings screen to
    // point "Тренировка"/"Моды"/"Настройки" at, so only the sections with something real behind
    // them are clickable; the rest are drawn dim, same placeholder convention already used for the
    // "Управление" top-bar button before it got the ship editor).
    private void DrawMainMenuScreen(float totalSeconds)
    {
        DrawMainMenuBackdrop(totalSeconds);

        DrawMainMenuSection("КАМПАНИЯ", 24, HudIcons.DrawShipGlyph);
        DrawMainMenuItem(0, "НОВАЯ ИГРА", enabled: true);
        if (_existingSave is { } save)
            DrawMainMenuItem(1, $"ПРОДОЛЖИТЬ ({ShipCatalog.Name(save.ShipKind)}, {save.Credits} кред.)", enabled: true);

        DrawMainMenuSection("СЕТЕВАЯ ИГРА", 130, HudIcons.DrawSignalGlyph);
        DrawMainMenuItem(2, "ПРИСОЕДИНИТЬСЯ", enabled: true);

        DrawMainMenuSection("ПЕРСОНАЛИЗАЦИЯ", 210,
            (sb, px, c, sc, col) => HudIcons.DrawRoleGlyph(sb, px, c, sc, col, CrewRole.Mechanic));
        DrawMainMenuItem(3, "НАСТРОЙКИ (скоро)", enabled: false);
        DrawMainMenuItem(4, $"СМЕНИТЬ НИК ({_nickname})", enabled: true);

        var exitRect = GetMainMenuExitRect();
        _spriteBatch.DrawString(_font, "ВЫХОД", new Vector2(exitRect.X, exitRect.Y), Color.LightGray, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
    }

    private void DrawMainMenuSection(string label, int y, Action<SpriteBatch, Texture2D, Vector2, float, Color> drawIcon)
    {
        var iconRect = new Rectangle(24, y, 28, 28);
        _spriteBatch.Draw(_pixel, iconRect, new Color(30, 60, 55));
        drawIcon(_spriteBatch, _pixel, new Vector2(iconRect.Center.X, iconRect.Center.Y), 0.85f, Color.White);

        var headerRect = new Rectangle(64, y + 2, 380, 24);
        _spriteBatch.Draw(_pixel, headerRect, new Color(210, 140, 50));
        _spriteBatch.DrawString(_font, label, new Vector2(headerRect.X + 8, headerRect.Y + 4),
            Color.Black, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    private void DrawMainMenuItem(int index, string label, bool enabled)
    {
        var rect = GetMainMenuItemRect(index);
        var hovered = enabled && rect.Contains(_designMouse);
        var color = !enabled ? Color.Gray : hovered ? Color.Gold : Color.LightGray;
        _spriteBatch.DrawString(_font, label, new Vector2(rect.X, rect.Y), color, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
    }

    // Right-hand art pane - a planet on a slow orbit with the player's own ship circling it
    // (MenuPlanetScene), standing in for the reference screenshot's submarine photo since there
    // are no image assets anywhere in this project.
    private void DrawMainMenuBackdrop(float totalSeconds)
    {
        var pane = new Rectangle(480, 0, DesignWidth - 480, DesignHeight);
        MenuPlanetScene.Draw(_spriteBatch, _pixel, pane, totalSeconds);

        var title = "SPACE ADVENTURE";
        var titlePosition = new Vector2(pane.Right - 340, pane.Bottom - 60);
        _spriteBatch.DrawString(_font, title, titlePosition + new Vector2(2, 2), Color.Black * 0.5f, 0f, Vector2.Zero, 1.1f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, title, titlePosition, Color.White, 0f, Vector2.Zero, 1.1f, SpriteEffects.None, 0f);
    }

    private void DrawNicknameScreen()
    {
        _spriteBatch.DrawString(_font, "Как вас зовут?", new Vector2(60, 60), Color.White, 0f, Vector2.Zero, 1.4f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, "Этот ник видит остальной экипаж - над головой персонажа и в списке команды.",
            new Vector2(60, 140), Color.LightGray, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, _nickname + "_", new Vector2(60, 178), Color.Gold, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, "[Enter] продолжить", new Vector2(60, 250), Color.LightSteelBlue, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
    }

    // Shared between the click handler above and the layout here, same convention as the main
    // menu's GetMainMenuItemRect, so a click always lands on exactly the icon it looks like it
    // should.
    private static Rectangle GetRoleChoiceRect(int index)
    {
        var totalWidth = RoleChoices.Length * RoleIconBoxSize + (RoleChoices.Length - 1) * RoleIconGap;
        var startX = (DesignWidth - totalWidth) / 2;
        return new Rectangle(startX + index * (RoleIconBoxSize + RoleIconGap), RoleIconsY, RoleIconBoxSize, RoleIconBoxSize);
    }

    private void DrawRoleScreen()
    {
        _spriteBatch.DrawString(_font, "Выберите роль в экипаже", new Vector2(60, 60), Color.White, 0f, Vector2.Zero, 1.4f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, "Это просто метка для остальных - не ограничивает, что вам разрешено делать на корабле.",
            new Vector2(60, 140), Color.LightGray, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        for (var i = 0; i < RoleChoices.Length; i++)
        {
            var rect = GetRoleChoiceRect(i);
            var role = RoleChoices[i];
            var selected = _selectedRole == role;
            var hovered = rect.Contains(_designMouse);
            _spriteBatch.Draw(_pixel, rect, selected ? new Color(120, 92, 30) : hovered ? Color.DimGray * 0.8f : Color.DimGray * 0.5f);
            HudIcons.DrawRoleGlyph(_spriteBatch, _pixel, new Vector2(rect.Center.X, rect.Center.Y), 1.4f, selected ? Color.White : Color.LightGray, role);

            var label = CrewRoles.Name(role);
            var labelSize = _font.MeasureString(label) * 0.55f;
            _spriteBatch.DrawString(_font, label, new Vector2(rect.Center.X - labelSize.X / 2f, rect.Bottom + 8),
                selected ? Color.Gold : Color.LightGray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }

        _spriteBatch.DrawString(_font, "[Enter] продолжить", new Vector2(60, 380), Color.LightSteelBlue, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
    }

    private void DrawShipSelectScreen()
    {
        _spriteBatch.DrawString(_font, "Выберите корабль", new Vector2(60, 40), Color.White, 0f, Vector2.Zero, 1.4f, SpriteEffects.None, 0f);
        for (var i = 0; i < SelectableShipKinds.Length; i++)
        {
            var kind = SelectableShipKinds[i];
            var y = 110 + i * 70;
            _spriteBatch.DrawString(_font, $"[{i + 1}] {ShipCatalog.Name(kind)}", new Vector2(60, y), Color.Gold, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, ShipCatalog.Description(kind), new Vector2(80, y + 24), Color.LightSteelBlue, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        }

        if (_existingSave is { } save)
        {
            _spriteBatch.DrawString(_font, $"[C] Продолжить: {ShipCatalog.Name(save.ShipKind)}, {save.Credits} кред.",
                new Vector2(60, 396), Color.LightGreen, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, "Выбор корабля начнёт новую игру и сотрёт сохранение.",
                new Vector2(80, 420), Color.Gray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
        }

        var hostLine = _openToNetwork
            ? $"[H] Кооп: ОТКРЫТ, порт {SpaceAdventure.Shared.Networking.Wire.DefaultPort} — друзья вводят {LocalAddresses()}"
            : "[H] Кооп: закрыт (игра только для вас)";
        _spriteBatch.DrawString(_font, hostLine, new Vector2(60, 460),
            _openToNetwork ? Color.LightGreen : Color.LightGray, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, "[J] Присоединиться к чужому кораблю", new Vector2(60, 488),
            Color.LightSkyBlue, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
    }

    private void DrawJoinScreen()
    {
        _spriteBatch.DrawString(_font, "Подключение к кораблю", new Vector2(60, 60), Color.White, 0f, Vector2.Zero, 1.4f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, "Адрес хоста (можно адрес:порт):", new Vector2(60, 140), Color.LightGray, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);

        // A caret while idle, "подключение…" while the handshake is in flight - the only two states
        // this screen has.
        var text = _joinTask is null ? _joinAddress + "_" : $"{_joinAddress} — подключение…";
        _spriteBatch.DrawString(_font, text, new Vector2(60, 178), Color.Gold, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);

        _spriteBatch.DrawString(_font, "[Enter] подключиться    [Esc] назад", new Vector2(60, 250), Color.LightSteelBlue, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, $"Хост должен включить кооп ([H] в меню) и открыть порт {SpaceAdventure.Shared.Networking.Wire.DefaultPort}.",
            new Vector2(60, 280), Color.Gray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        if (_joinError is { } error)
            _spriteBatch.DrawString(_font, $"Не удалось: {error}", new Vector2(60, 330), Color.OrangeRed, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
    }
}
