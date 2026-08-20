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
        Eula,
        Nickname,
        Role,
        Main,
        ShipSelect,
        Join,
        ShipEditor,
        Credits,
        Settings,
    }

    private static readonly ShipKind[] SelectableShipKinds = { ShipKind.Scout, ShipKind.Frigate, ShipKind.Cruiser, ShipKind.Corvette };
    private static readonly CrewRole[] RoleChoices = { CrewRole.Captain, CrewRole.Engineer, CrewRole.Mechanic, CrewRole.Security, CrewRole.Medic };
    private const int RoleIconBoxSize = 70;
    private const int RoleIconGap = 30;
    private const int RoleIconsY = 220;

    // Shown first, every launch (not just the first one) - the same "always ask, never skip"
    // convention the nickname/role screens right after it already use, just for a joke user
    // agreement instead of a real setting.
    private MenuScreen _menuScreen = MenuScreen.Eula;
    private float _screenChangedAt = -99f;
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
    // When the front screen was last (re-)entered (Main's own game-time, from DrawMenu) - drives
    // the staggered fade/slide-in each section plays on arrival, and null means "already settled",
    // so a screen that's been sitting open for a while never replays it.
    private float? _mainMenuEnterTime;
    private MenuScreen? _lastDrawnMenuScreen;

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

        if (_menuScreen == MenuScreen.Eula)
            HandleEulaScreen(keyboard);
        else if (_menuScreen == MenuScreen.Nickname)
            HandleNicknameScreen(keyboard);
        else if (_menuScreen == MenuScreen.Role)
            HandleRoleScreen(keyboard);
        else if (_menuScreen == MenuScreen.Main)
            HandleMainMenuClick();
        else if (_menuScreen == MenuScreen.ShipSelect)
            HandleShipSelect(keyboard);
        else if (_menuScreen == MenuScreen.ShipEditor)
            HandleShipEditorScreen(keyboard);
        else if (_menuScreen == MenuScreen.Credits)
            HandleCreditsScreen(keyboard);
        else if (_menuScreen == MenuScreen.Settings)
            HandleSettingsScreen(keyboard);
        else
            HandleJoinScreen(keyboard);

        _prevMenuKeyboard = keyboard;
    }

    // A joke gate before the real ones (nickname/role) - Enter or clicking the button both just
    // move on to Nickname, same "not actually a real gate" spirit as the text itself. Escape here
    // falls through to LeaveSubScreen's default (false), which Update then treats the same as
    // Main's own Escape - straight to Exit() - since there's nowhere further back from the very
    // first screen.
    private void HandleEulaScreen(KeyboardState keyboard)
    {
        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        _prevMenuLeftMouseButton = mouse.LeftButton;

        if (Pressed(keyboard, Keys.Enter) || (clicked && GetEulaAcceptButtonRect().Contains(_designMouse)))
            _menuScreen = MenuScreen.Nickname;
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
        if (_menuScreen == MenuScreen.ShipEditor)
        {
            _menuScreen = MenuScreen.Main;
            return true;
        }
        if (_menuScreen == MenuScreen.Credits)
        {
            ReturnFromCredits();
            return true;
        }
        if (_menuScreen == MenuScreen.Settings)
        {
            _menuScreen = MenuScreen.Main; // Escape discards staged edits, same as "Отмена"
            return true;
        }
        return false;
    }

    // What a main-menu button actually does on click - Placeholder means nothing yet, drawn
    // dimmed like Настройки already was, until the user asks for it to do something real.
    private enum MainMenuAction
    {
        NewGame,
        Continue,
        Join,
        ShipEditor,
        ChangeNick,
        Settings,
        Credits,
        Exit,
        Placeholder,
        Tutorial,
    }

    // Same small glyph vocabulary the old grouped sections showed before each header - restored
    // per-button here since the flat layout dropped them when the section headers went away.
    private enum MainMenuIcon
    {
        Play, Ship, Flag, Signal, Plug, Wrench, Person, Bars, Medal, Exit,
    }

    // The user's own hand-laid-out front screen (built with the Menu Layout Rig tool and pasted
    // back as coordinates) - a flat list of free-standing buttons rather than the old grouped
    // icon+header sections, so this is the single source both the click handler and the drawing
    // below iterate over instead of two separate hand-kept layouts.
    private static readonly (string Label, Rectangle Rect, MainMenuAction Action, MainMenuIcon Icon)[] MainMenuButtons =
    {
        ("ПРОДОЛЖИТЬ", new Rectangle(144, 64, 160, 24), MainMenuAction.Continue, MainMenuIcon.Play),
        ("НОВАЯ ИГРА", new Rectangle(144, 96, 160, 24), MainMenuAction.NewGame, MainMenuIcon.Ship),
        ("ОБУЧЕНИЕ", new Rectangle(144, 32, 160, 26), MainMenuAction.Tutorial, MainMenuIcon.Flag),
        ("СОЗДАТЬ СЕРВЕР", new Rectangle(88, 168, 160, 26), MainMenuAction.Placeholder, MainMenuIcon.Signal),
        ("ПРИСОЕДИНИТЬСЯ", new Rectangle(88, 200, 160, 24), MainMenuAction.Join, MainMenuIcon.Plug),
        ("РЕДАКТОР КОРАБЛЯ", new Rectangle(168, 316, 160, 24), MainMenuAction.ShipEditor, MainMenuIcon.Wrench),
        ("СМЕНИТЬ НИК", new Rectangle(168, 348, 160, 24), MainMenuAction.ChangeNick, MainMenuIcon.Person),
        ("НАСТРОЙКИ", new Rectangle(76, 420, 160, 24), MainMenuAction.Settings, MainMenuIcon.Bars),
        ("АВТОРЫ", new Rectangle(76, 456, 160, 26), MainMenuAction.Credits, MainMenuIcon.Medal),
        ("ВЫХОД", new Rectangle(76, 488, 160, 24), MainMenuAction.Exit, MainMenuIcon.Exit),
    };

    // Continue only exists once there's actually a save to continue - same "not drawn/clickable
    // at all without one" behaviour the old grouped layout had, just expressed as a skip here
    // instead of a separate conditional draw call.
    private bool IsMainMenuButtonVisible(MainMenuAction action) =>
        action != MainMenuAction.Continue || _existingSave is not null;

    private bool IsMainMenuButtonEnabled(MainMenuAction action) =>
        action is not MainMenuAction.Placeholder;

    private string ResolveMainMenuLabel(string staticLabel, MainMenuAction action) => action switch
    {
        MainMenuAction.Continue when _existingSave is { } save =>
            $"ПРОДОЛЖИТЬ ({ShipCatalog.Name(save.ShipKind)}, {save.Credits} кред.)",
        MainMenuAction.ChangeNick => $"СМЕНИТЬ НИК ({_nickname})",
        _ => staticLabel,
    };

    // The main menu's own click targets - mouse-driven (unlike the keyboard-shortcut screens either
    // side of it). Iterates the same MainMenuButtons list DrawMainMenuScreen draws, so a click can
    // never land on a rect the drawing doesn't actually show (or vice versa).
    private void HandleMainMenuClick()
    {
        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        _prevMenuLeftMouseButton = mouse.LeftButton;
        if (!clicked)
            return;

        var point = _designMouse;
        foreach (var (_, rect, action, _) in MainMenuButtons)
        {
            if (!IsMainMenuButtonVisible(action) || !IsMainMenuButtonEnabled(action) || !rect.Contains(point))
                continue;

            switch (action)
            {
                case MainMenuAction.NewGame:
                    _menuScreen = MenuScreen.ShipSelect;
                    break;
                case MainMenuAction.Tutorial:
                    // Always the starter Frigate, no ship-select step - the tutorial's own room ids
                    // (World.Tutorial.cs) are hardcoded to that hull's layout.
                    StartHostedSession(ShipKind.Frigate, loadFrom: null, isTutorial: true);
                    break;
                case MainMenuAction.Credits:
                    _menuScreen = MenuScreen.Credits;
                    _creditsStart = null;
                    break;
                case MainMenuAction.Settings:
                    EnterSettingsScreen();
                    break;
                case MainMenuAction.Continue when _existingSave is { } save:
                    StartHostedSession(save.ShipKind, save);
                    break;
                case MainMenuAction.Join:
                    _menuScreen = MenuScreen.Join;
                    _joinError = null;
                    break;
                case MainMenuAction.ShipEditor:
                    EnterShipEditor();
                    break;
                case MainMenuAction.ChangeNick:
                    _menuScreen = MenuScreen.Nickname;
                    break;
                case MainMenuAction.Exit:
                    Exit();
                    break;
            }
            return;
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
    private void StartHostedSession(ShipKind shipKind, SaveGame? loadFrom, CustomShipDefinition? customShip = null, bool isTutorial = false)
    {
        var session = new SoloSession(shipKind, loadFrom,
            _openToNetwork ? SpaceAdventure.Shared.Networking.Wire.DefaultPort : null, customShip, isTutorial);
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
        // Freshly arrived at Main this frame (from anywhere: role screen, ship-select's Escape,
        // ReturnToMainMenu) - stamp the entrance time so DrawMainMenuScreen's stagger starts over.
        if (_menuScreen == MenuScreen.Main && _lastDrawnMenuScreen != MenuScreen.Main)
            _mainMenuEnterTime = totalSeconds;
        // Any screen change at all, not just arriving at Main - the wipe below covers every one of
        // them, so a hard cut between sub-screens stops being the one rough edge left in the menu.
        if (_lastDrawnMenuScreen != _menuScreen)
            _screenChangedAt = totalSeconds;
        _lastDrawnMenuScreen = _menuScreen;

        _spriteBatch.Begin(transformMatrix: _renderScale);
        if (_menuScreen == MenuScreen.Eula)
            DrawEulaScreen(totalSeconds);
        else if (_menuScreen == MenuScreen.Nickname)
            DrawNicknameScreen();
        else if (_menuScreen == MenuScreen.Role)
            DrawRoleScreen();
        else if (_menuScreen == MenuScreen.Main)
            DrawMainMenuScreen(totalSeconds);
        else if (_menuScreen == MenuScreen.ShipSelect)
            DrawShipSelectScreen();
        else if (_menuScreen == MenuScreen.ShipEditor)
            DrawShipEditorScreen();
        else if (_menuScreen == MenuScreen.Credits)
            DrawCreditsScreen(totalSeconds);
        else if (_menuScreen == MenuScreen.Settings)
            DrawSettingsScreen();
        else
            DrawJoinScreen();

        // A short black wipe over whatever was just drawn. Cheaper than animating each screen out
        // and in, and because it sits inside the post chain the grain and vignette ride over it too,
        // so the transition belongs to the same picture rather than looking pasted on top.
        var sinceChange = totalSeconds - _screenChangedAt;
        const float wipeSeconds = 0.22f;
        if (sinceChange < wipeSeconds)
        {
            var fade = 1f - sinceChange / wipeSeconds;
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, DesignWidth, DesignHeight + (int)LetterboxBelowDesign), Color.Black * (fade * fade));
        }
        _spriteBatch.End();
    }

    // A free-standing, mouse-driven front screen - every button exactly where the user placed it
    // with the Menu Layout Rig tool, not grouped under a header any more. Обучение/Создать
    // сервер/Авторы are new slots the user added there with nothing behind them yet - drawn
    // dimmed like Настройки already was, so they read as "not wired up yet" rather than broken.
    private void DrawMainMenuScreen(float totalSeconds)
    {
        DrawMainMenuBackdrop(totalSeconds);
        DrawMainMenuPanelPlate(totalSeconds);

        var sinceEnter = totalSeconds - (_mainMenuEnterTime ?? totalSeconds - 999f);
        var visibleIndex = 0;
        foreach (var (staticLabel, rect, action, icon) in MainMenuButtons)
        {
            if (!IsMainMenuButtonVisible(action))
                continue;
            var progress = MainMenuButtonProgress(sinceEnter, visibleIndex);
            visibleIndex++;
            DrawMainMenuButton(rect, ResolveMainMenuLabel(staticLabel, action), IsMainMenuButtonEnabled(action), progress, icon, totalSeconds);
        }
    }

    // Eased 0..1 arrival progress for the button at `index` in menu-arrival order, staggered so
    // each one starts a beat after the last rather than everything popping in together.
    private static float MainMenuButtonProgress(float sinceEnter, int index)
    {
        const float staggerSeconds = 0.06f;
        const float durationSeconds = 0.3f;
        var t = Math.Clamp((sinceEnter - index * staggerSeconds) / durationSeconds, 0f, 1f);
        return t * t * (3f - 2f * t); // smoothstep
    }

    // A small bordered plate per button rather than bare floating text - free-standing buttons
    // with nothing grouping them need their own edge to read as a discrete clickable thing.
    // `progress` drives both the fade-in and a slide-up on arrival, same convention every other
    // menu animation in this file already uses.
    private void DrawMainMenuButton(Rectangle rect, string label, bool enabled, float progress, MainMenuIcon icon, float totalSeconds)
    {
        var slide = (int)((1f - progress) * 14f);
        var drawRect = new Rectangle(rect.X, rect.Y + slide, rect.Width, rect.Height);
        var hovered = enabled && drawRect.Contains(_designMouse);
        // Held down over the button: the plate sinks a pixel and its face brightens, so a click
        // has a physical answer instead of the label simply changing colour on release.
        var held = hovered && Mouse.GetState().LeftButton == ButtonState.Pressed;
        if (held)
            drawRect = new Rectangle(drawRect.X + 1, drawRect.Y + 1, drawRect.Width, drawRect.Height);
        var accent = !enabled ? new Color(90, 96, 96) : hovered ? Color.Gold : new Color(90, 220, 195);

        _spriteBatch.Draw(_pixel, drawRect, new Color(14, 20, 20) * progress);
        if (hovered)
        {
            // A band of light crossing the plate, left to right, on a loop. Drawn as a handful of
            // one-pixel columns with a triangular falloff because there is no scissor rect here to
            // clip a wide quad against, and a gradient texture for one effect is not worth it.
            _spriteBatch.Draw(_pixel, drawRect, accent * (held ? 0.22f : 0.12f) * progress);
            var sweep = drawRect.X + (totalSeconds * 0.85f % 1f) * drawRect.Width;
            for (var i = -7; i <= 7; i++)
            {
                var x = (int)sweep + i;
                if (x < drawRect.X || x >= drawRect.Right)
                    continue;
                var falloff = (1f - MathF.Abs(i) / 8f) * 0.20f;
                _spriteBatch.Draw(_pixel, new Rectangle(x, drawRect.Y + 1, 1, drawRect.Height - 2), Color.White * falloff * progress);
            }
        }
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, drawRect, accent * progress, 1);

        var iconBoxSize = drawRect.Height - 8;
        var iconBox = new Rectangle(drawRect.X + 4, drawRect.Y + (drawRect.Height - iconBoxSize) / 2, iconBoxSize, iconBoxSize);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, iconBox, accent * (progress * 0.6f), 1);
        DrawMainMenuButtonIcon(icon, iconBox, accent * progress);

        var textColor = (!enabled ? Color.Gray : hovered ? Color.Gold : Color.LightGray) * progress;
        var textSize = _font.MeasureString(label) * 0.5f;
        var textX = iconBox.Right + 8;
        var textPos = new Vector2(textX, drawRect.Center.Y - textSize.Y / 2f);
        _spriteBatch.DrawString(_font, label, textPos, textColor, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    // Same glyph vocabulary the old grouped sections used before each header (HudIcons' ship/
    // signal/flag/medal/bars glyphs), plus a few quick vector icons for actions that never had
    // one - all drawn with the same line/circle/triangle primitives, no image assets.
    private void DrawMainMenuButtonIcon(MainMenuIcon icon, Rectangle box, Color color)
    {
        var center = new Vector2(box.Center.X, box.Center.Y);
        var scale = box.Width / 20f;
        switch (icon)
        {
            case MainMenuIcon.Play:
                Primitives.FillTriangle(_spriteBatch, _pixel,
                    center + new Vector2(6f * scale, 0),
                    center + new Vector2(-4f * scale, -6f * scale),
                    center + new Vector2(-4f * scale, 6f * scale),
                    color);
                break;
            case MainMenuIcon.Ship:
                HudIcons.DrawShipGlyph(_spriteBatch, _pixel, center, scale, color);
                break;
            case MainMenuIcon.Flag:
                HudIcons.DrawFlagGlyph(_spriteBatch, _pixel, center, scale * 0.8f, color);
                break;
            case MainMenuIcon.Signal:
                HudIcons.DrawSignalGlyph(_spriteBatch, _pixel, center, scale * 0.8f, color);
                break;
            case MainMenuIcon.Plug:
                HudIcons.DrawLine(_spriteBatch, _pixel, center + new Vector2(-7f * scale, 0), center + new Vector2(2f * scale, 0), color, 1.8f * scale);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(center.X + 2f * scale), (int)(center.Y - 5f * scale), (int)(5f * scale), (int)(10f * scale)), color);
                break;
            case MainMenuIcon.Wrench:
                HudIcons.DrawLine(_spriteBatch, _pixel, center + new Vector2(-6f * scale, 6f * scale), center + new Vector2(5f * scale, -5f * scale), color, 2.2f * scale);
                HudIcons.DrawRingArc(_spriteBatch, _pixel, center + new Vector2(6f * scale, -6f * scale), 3.2f * scale, 0f, 360f, color, 8, 1.6f * scale);
                break;
            case MainMenuIcon.Person:
                HudIcons.DrawPerson(_spriteBatch, _pixel, center + new Vector2(0, 7f * scale), scale * 0.85f, color);
                break;
            case MainMenuIcon.Bars:
                HudIcons.DrawBarsGlyph(_spriteBatch, _pixel, center, scale * 0.7f, color);
                break;
            case MainMenuIcon.Medal:
                HudIcons.DrawMedalGlyph(_spriteBatch, _pixel, center, scale * 0.8f, color);
                break;
            case MainMenuIcon.Exit:
                HudIcons.DrawLine(_spriteBatch, _pixel, center + new Vector2(-6f * scale, -6f * scale), center + new Vector2(6f * scale, 6f * scale), color, 2f * scale);
                HudIcons.DrawLine(_spriteBatch, _pixel, center + new Vector2(-6f * scale, 6f * scale), center + new Vector2(6f * scale, -6f * scale), color, 2f * scale);
                break;
        }
    }

    // The left panel's own material - the same armour-plate texture the hull itself uses
    // reads as flat near-black with just a whisper of a blueprint grid, not a repeating armour-
    // plate tile - a tiled hull texture at this scale read as "a bunch of plates" instead of a
    // moody backdrop, so this is a flat fill plus thin grid lines instead. Also carries a thin
    // animated status strip down the panel's right edge.
    private void DrawMainMenuPanelPlate(float totalSeconds)
    {
        var panelRect = new Rectangle(0, 0, 480, DesignHeight);
        _spriteBatch.Draw(_pixel, panelRect, new Color(7, 11, 12));

        const int cell = 28;
        var gridColor = new Color(90, 220, 195) * 0.05f;
        for (var x = panelRect.X; x < panelRect.Right; x += cell)
            _spriteBatch.Draw(_pixel, new Rectangle(x, panelRect.Y, 1, panelRect.Height), gridColor);
        for (var y = panelRect.Y; y < panelRect.Bottom; y += cell)
            _spriteBatch.Draw(_pixel, new Rectangle(panelRect.X, y, panelRect.Width, 1), gridColor);

        // A soft glow low in the corner the sections actually sit in, fading to nothing toward the
        // opposite edges - depth without a second competing pattern.
        HudIcons.FillCircle(_spriteBatch, _pixel, new Vector2(panelRect.X + 40, panelRect.Y + 260), 260f, new Color(40, 70, 65) * 0.10f);

        const int stripWidth = 3;
        var stripRect = new Rectangle(panelRect.Right - stripWidth, 0, stripWidth, DesignHeight);
        _spriteBatch.Draw(_pixel, stripRect, new Color(20, 26, 26));
        var pulse = 0.5f + 0.5f * MathF.Sin(totalSeconds * 1.6f);
        const int litHeight = 40;
        var litY = (int)(pulse * (DesignHeight - litHeight));
        _spriteBatch.Draw(_pixel, new Rectangle(stripRect.X, litY, stripWidth, litHeight), new Color(90, 220, 195) * 0.8f);
    }

    // Right-hand art pane - a planet on a slow orbit with the player's own ship circling it
    // (MenuPlanetScene), standing in for the reference screenshot's submarine photo since there
    // are no image assets anywhere in this project.
    private void DrawMainMenuBackdrop(float totalSeconds)
    {
        var pane = new Rectangle(480, 0, DesignWidth - 480, DesignHeight);
        MenuPlanetScene.Draw(_spriteBatch, _pixel, pane, totalSeconds);

        // A little caption riding along next to the orbiting Katyusha truck - purely a joke aside,
        // not a UI label anything reads, so it just follows MenuPlanetScene's own reported position
        // for it every frame rather than being pinned to one spot.
        var katyushaPosition = MenuPlanetScene.GetKatyushaScreenPosition(pane, totalSeconds);
        var caption = "P. S. это Катюша";
        var captionPosition = katyushaPosition + new Vector2(14, -6);
        _spriteBatch.DrawString(_font, caption, captionPosition + new Vector2(1, 1), Color.Black * 0.6f, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, caption, captionPosition, Color.LightGoldenrodYellow, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        // A soft cyan glow behind the title (several oversized, near-transparent copies offset in
        // a ring) plus a hard black drop shadow, then the crisp white face on top - the cheapest
        // way to fake a bloomed title with nothing but flat text draws.
        const string title = "ДУРАК ОНЛАЙН";
        const float titleScale = 1.7f;
        var titlePosition = new Vector2(pane.Right - 460, pane.Bottom - 92);
        var glow = new Color(90, 220, 195);
        // Three sine waves whose periods do not divide into each other, so the sign never settles
        // into a rhythm the eye can predict - that unevenness is the whole difference between a
        // tube that is failing and a light that is simply pulsing.
        var flicker = 0.86f
            + MathF.Sin(totalSeconds * 2.3f) * 0.05f
            + MathF.Sin(totalSeconds * 7.1f) * 0.03f
            + MathF.Sin(totalSeconds * 17.7f) * 0.02f;
        foreach (var offset in new[] { new Vector2(-2, 0), new Vector2(2, 0), new Vector2(0, -2), new Vector2(0, 2), new Vector2(-1.5f, -1.5f), new Vector2(1.5f, 1.5f) })
            _spriteBatch.DrawString(_font, title, titlePosition + offset, glow * (0.35f * flicker), 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, title, titlePosition + new Vector2(3, 3), Color.Black * 0.6f, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
        // Red and cyan faces a pixel either side of the white one: the sign's own colour fringing,
        // and it breathes with the flicker so the two read as one failing tube rather than two
        // separate effects.
        var split = 1f + (1f - flicker) * 6f;
        _spriteBatch.DrawString(_font, title, titlePosition - new Vector2(split, 0f), new Color(255, 90, 90) * 0.30f, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, title, titlePosition + new Vector2(split, 0f), new Color(90, 220, 255) * 0.30f, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, title, titlePosition, Color.White * flicker, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

        // A riveted rule under the title, same corner-rivet dressing every device housing already
        // wears (ShipRenderer.DrawRivets) - ties the front screen to the game it opens into.
        var ruleY = titlePosition.Y + 46;
        var ruleRect = new Rectangle((int)titlePosition.X, (int)ruleY, pane.Right - (int)titlePosition.X - 20, 2);
        _spriteBatch.Draw(_pixel, ruleRect, glow * 0.6f);
        for (var x = ruleRect.X + 6; x < ruleRect.Right; x += 24)
            HudIcons.FillCircle(_spriteBatch, _pixel, new Vector2(x, ruleY + 1), 1.4f, new Color(20, 24, 22));

        DrawTrafficTicker(pane, totalSeconds);

        const string tagline = "СВОЙ КОРАБЛЬ. СВОЙ ЭКИПАЖ. ГЛУБОКИЙ КОСМОС.";
        _spriteBatch.DrawString(_font, tagline, new Vector2(titlePosition.X + 2, ruleY + 8), new Color(190, 220, 215), 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
    }

    // Docking chatter crawling along the bottom edge. Nothing here is interactive and none of it
    // is real - it exists so the screen reads as a place with traffic in it rather than a poster.
    private static readonly string[] TrafficLines =
    {
        "БОРТ 'КАТЮША' - ЗАПРОС НА СТЫКОВКУ ПРИНЯТ, ПРИЧАЛ 4",
        "ГРУЗОВОЙ КОРИДОР 12 ЗАКРЫТ - РАБОТЫ НА ВНЕШНЕЙ ОБШИВКЕ",
        "ВНИМАНИЕ: НЕОПОЗНАННЫЙ СИГНАЛ В СЕКТОРЕ 7, СОБЛЮДАЙТЕ ОСТОРОЖНОСТЬ",
        "ТОПЛИВНЫЙ КОНВОЙ ПРИБЫВАЕТ ЧЕРЕЗ 6 ЧАСОВ",
        "НАПОМИНАНИЕ: СКАФАНДР ПРОВЕРЯЕТСЯ ПЕРЕД КАЖДЫМ ВЫХОДОМ",
        "МЕДОТСЕК: ПЛАНОВЫЙ ОСМОТР ЭКИПАЖА ПЕРЕНЕСЁН НА ЗАВТРА",
    };

    private void DrawTrafficTicker(Rectangle pane, float totalSeconds)
    {
        var line = TrafficLines[(int)(totalSeconds / 11f) % TrafficLines.Length];
        var width = _font.MeasureString(line).X * 0.5f;
        // Scrolls right to left across the whole pane and wraps with a gap, so there is never a
        // moment where the strip is empty and the effect visibly stops.
        var travel = (pane.Width + width + 120f);
        var x = pane.Right - totalSeconds % (travel / 34f) * 34f;
        var y = pane.Bottom - 16f;

        _spriteBatch.Draw(_pixel, new Rectangle(pane.X, (int)y - 3, pane.Width, 14), new Color(8, 14, 18) * 0.55f);
        _spriteBatch.Draw(_pixel, new Rectangle(pane.X, (int)y - 4, pane.Width, 1), new Color(90, 220, 195) * 0.25f);
        _spriteBatch.DrawString(_font, line, new Vector2(x, y), new Color(120, 200, 185) * 0.75f, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private static readonly string[] EulaLines =
    {
        "Подписав данное пользовательское соглашение вы соглашаеться стать тестировщиком игры.",
        "Если у вас в течении игры случится припадок, инсульт, эпилепсия, гипоксемия, отказ органов.",
        "Разработчик ответсвенности за это не несет.",
        "Также по просьбе создателя данной игры играть в дурака, вы обязательно должно повиноваться",
        "ему и идти играть в дурака, даже если это будет посреди боя с зараженными.",
        "",
        "Удачи в бета версии игры",
    };

    private static Rectangle GetEulaAcceptButtonRect() => new(DesignWidth / 2 - 130, 470, 260, 40);

    // A joke user agreement, styled like every other warning-tape surface in this game (hazard
    // stripes top and bottom, a flickering red title) rather than a plain text dump - the whole
    // point of "АТТЕШН!!!" is that it reads as an actual klaxon, not a EULA nobody looks at.
    private void DrawEulaScreen(float totalSeconds)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, DesignWidth, DesignHeight), new Color(10, 6, 6));

        const int stripeHeight = 14;
        DrawHazardCap(new Rectangle(0, 0, DesignWidth, stripeHeight), 1f);
        DrawHazardCap(new Rectangle(0, DesignHeight - stripeHeight, DesignWidth, stripeHeight), 1f);

        var flicker = 0.7f + 0.3f * MathF.Sin(totalSeconds * 9f);
        const string title = "АТТЕШН!!!";
        const float titleScale = 2.4f;
        var titleSize = _font.MeasureString(title) * titleScale;
        var titlePosition = new Vector2((DesignWidth - titleSize.X) / 2f, 34);
        _spriteBatch.DrawString(_font, title, titlePosition + new Vector2(3, 3), Color.Black * 0.7f, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, title, titlePosition, Color.OrangeRed * flicker, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

        const string subtitle = "ПОЛЬЗОВАТЕЛЬСКОЕ СОГЛАШЕНИЕ";
        var subtitleSize = _font.MeasureString(subtitle) * 0.75f;
        _spriteBatch.DrawString(_font, subtitle, new Vector2((DesignWidth - subtitleSize.X) / 2f, 96), Color.Gold, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

        var panelRect = new Rectangle(90, 140, DesignWidth - 180, 300);
        _spriteBatch.Draw(_pixel, panelRect, new Color(20, 16, 16) * 0.9f);
        DrawEulaPanelOutline(panelRect, new Color(150, 40, 30));

        for (var i = 0; i < EulaLines.Length; i++)
            _spriteBatch.DrawString(_font, EulaLines[i], new Vector2(panelRect.X + 20, panelRect.Y + 20 + i * 26),
                Color.LightGray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        var buttonRect = GetEulaAcceptButtonRect();
        var hovered = buttonRect.Contains(_designMouse);
        _spriteBatch.Draw(_pixel, buttonRect, (hovered ? new Color(120, 40, 20) : new Color(80, 26, 16)) * 0.95f);
        DrawEulaPanelOutline(buttonRect, hovered ? Color.Gold : new Color(150, 40, 30));
        const string accept = "[ПРИНИМАЮ]";
        var acceptSize = _font.MeasureString(accept) * 0.75f;
        _spriteBatch.DrawString(_font, accept, new Vector2(buttonRect.Center.X - acceptSize.X / 2f, buttonRect.Center.Y - acceptSize.Y / 2f),
            hovered ? Color.White : Color.LightGray, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

        _spriteBatch.DrawString(_font, "[Enter] принять условия", new Vector2(90, 522), Color.Gray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    private void DrawEulaPanelOutline(Rectangle rect, Color color)
    {
        const int thickness = 2;
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    private void DrawHazardCap(Rectangle rect, float alpha)
    {
        _spriteBatch.Draw(_pixel, rect, new Color(40, 34, 10) * alpha);
        const int stripeWidth = 16;
        for (var x = -rect.Height; x < rect.Width; x += stripeWidth * 2)
        {
            var p0 = new Vector2(rect.X + x, rect.Bottom);
            var p1 = new Vector2(rect.X + x + rect.Height, rect.Y);
            HudIcons.DrawLine(_spriteBatch, _pixel, p0, p1, Color.Black * (alpha * 0.85f), stripeWidth);
            HudIcons.DrawLine(_spriteBatch, _pixel, p0 + new Vector2(stripeWidth, 0), p1 + new Vector2(stripeWidth, 0), new Color(210, 170, 20) * alpha, stripeWidth);
        }
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
