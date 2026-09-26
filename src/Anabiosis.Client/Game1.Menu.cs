using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Anabiosis.Client.Networking;
using Anabiosis.Client.Rendering;
using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

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
        Prologue,
        Join,
        ShipEditor,
        Credits,
        Settings,
        // Direct user request (screenshots of a Barotrauma-style "Создать сервер" screen) - name/
        // player-cap feed StartHostedLobby's own real GameServer lobby constructor; password/
        // language/server-executable/checkboxes are shown but inert (direct user confirmation,
        // "показать визуально, но неактивно") - this game has none of the backing mechanics
        // (password auth, a public server list, karma) those Barotrauma controls represent.
        CreateServer,
        // Direct user request (screenshot 3 of the Barotrauma reference set) - a real pre-game
        // waiting room: everyone connected sees the live player list/roles/ready state, only the
        // host can pick the ship and press "НАЧАТЬ", and the round only actually begins once that
        // command reaches GameServer (its own SessionPhase.Lobby). Reached from CreateServer's own
        // "НАЧАТЬ" (hosting) or from Join (joining someone else's lobby) alike.
        Lobby,
    }

    private static readonly CrewRole[] RoleChoices = { CrewRole.Captain, CrewRole.Engineer, CrewRole.Mechanic, CrewRole.Security, CrewRole.Scientist };
    private const int RoleIconBoxSize = 70;
    private const int RoleIconGap = 30;
    private const int RoleIconsY = 220;

    // Where a launch lands.
    //
    // The introductions are a first-run thing. Someone who has already told the game who they are is
    // being asked a question they have answered, and the answer has not changed since they closed it
    // five minutes ago - so they go straight to the menu, where the buttons for changing both the
    // callsign and the role are sitting anyway.
    //
    // A saved nickname is the marker rather than the settings file existing: the file appears the
    // moment anyone touches the graphics options, which is not the same thing as having introduced
    // yourself. On a fresh machine there is no nickname, and the full Nickname -> Role -> Main chain
    // runs exactly once.
    private MenuScreen _menuScreen =
        PlayerSettingsStore.LoadNickname() is null ? MenuScreen.Nickname : MenuScreen.Main;
    private float _screenChangedAt = -99f;

    // Where the button column ends and the art pane begins. One constant rather than the same
    // number written into both the backdrop and the plate behind the buttons - those two have to
    // agree exactly or a seam shows between them.
    //
    // 340 is close to the floor: the rightmost buttons in MainMenuButtons (the x=144 column, 160
    // wide) end at 304, so anything below ~312 would push buttons out over the artwork.
    private const int MenuPaneX = 340;
    // "Игрок" rather than "" for a fresh machine - with the nickname screen no longer shown at
    // startup, nothing else would ever give an empty nickname a real value before it's sent.
    private string _nickname = PlayerSettingsStore.LoadNickname() ?? "Игрок";
    // "Always ask, never skip" for Role specifically, since there's no wrong answer and no need to
    // force a choice - Enter with nothing picked just continues without one, same as today's
    // default for a player who never opens the crew panel.
    private CrewRole? _selectedRole = PlayerSettingsStore.LoadRole();
    private bool _openToNetwork;
    private string _joinAddress = "127.0.0.1";
    private string? _joinError;

    // Direct user request (screenshots of a Barotrauma-style "Создать сервер" screen) - Название
    // сервера/Макс. игроков are the only 2 fields here with anything real behind them (a display
    // name shown next to the host's own port on ShipSelect, and a real cap enforced by
    // NetworkHost/GameServer.PlayerCount via ServerMessageKind.Rejected); every other
    // control on the screen (password, language, server executable, the 3 checkboxes) is drawn but
    // inert, direct user confirmation ("показать визуально, но неактивно") - this game has none of
    // Barotrauma's own backing mechanics (password auth, a public server browser, karma) for them.
    private string _createServerName = "Сервер";
    private const int DefaultCreateServerMaxPlayers = 4;
    private const int MinCreateServerMaxPlayers = 1;
    private const int MaxCreateServerMaxPlayers = 8;
    private int _createServerMaxPlayers = DefaultCreateServerMaxPlayers;
    private int _hostMaxPlayers = int.MaxValue;
    // Barotrauma-style pre-game lobby (MenuScreen.Lobby) - a background SoloSession(lobbyServerName,
    // maxPlayers, port) construction in flight, same "poll every frame, don't block Update" shape
    // _pendingSession already uses for the non-lobby path. A SEPARATE field (not reused) so the two
    // paths can never be confused with each other and FinishPendingSessionIfReady stays completely
    // untouched.
    private System.Threading.Tasks.Task<SoloSession>? _pendingLobbySession;
    // This client's own ready toggle while sitting in a lobby - reset every time a new lobby is
    // entered (hosting or joining). The server's own LobbyPlayerState.IsReady is the source of
    // truth once a snapshot arrives; this is only what the very next click should flip it to.
    private bool _lobbyReady;
    // Host-only, purely for highlighting which row is picked - GameServer's own LobbySnapshot.
    // CustomShipName already carries the authoritative answer once it arrives.
    private string? _lobbySelectedShipName;
    // Purely cosmetic - cycles a couple of canned blurbs under the carousel's own arrows, the same
    // "Игровой стиль" flavor text the reference screen shows, never read by anything else.
    private int _createServerStyleIndex;
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

        // Редактор корабля в духе Cosmoteer + несколько сохранённых кораблей (humble-soaring-cat.md,
        // Step 6) - "Сохранить как"'s own text box, same free-typing rule the Nickname screen uses
        // above (a ship's save-slot name isn't restricted to the Join screen's host/port alphabet).
        if (_menuScreen == MenuScreen.ShipEditor && _editorSaveAsPrompting)
        {
            if (e.Character == '\b')
            {
                if (_editorSaveAsInput.Length > 0)
                    _editorSaveAsInput = _editorSaveAsInput[..^1];
                return;
            }
            if (!char.IsControl(e.Character) && _editorSaveAsInput.Length < 30)
                _editorSaveAsInput += e.Character;
            return;
        }

        // Tile-painting redo (M76 follow-up) - the Zone tool's own naming prompt, same free-typing
        // text box as "Сохранить как" above.
        if (_menuScreen == MenuScreen.ShipEditor && _editorZoneNamePrompting)
        {
            // Hand-editing the text after picking a type quick-select (direct user request's own
            // "своё имя" case) drops the pending type - the name and the type can no longer be
            // trusted to agree once the player starts typing over it.
            _editorZonePendingKind = null;
            if (e.Character == '\b')
            {
                if (_editorZoneNameInput.Length > 0)
                    _editorZoneNameInput = _editorZoneNameInput[..^1];
                return;
            }
            if (!char.IsControl(e.Character) && _editorZoneNameInput.Length < 30)
                _editorZoneNameInput += e.Character;
            return;
        }

        if (_menuScreen == MenuScreen.CreateServer)
        {
            if (e.Character == '\b')
            {
                if (_createServerName.Length > 0)
                    _createServerName = _createServerName[..^1];
                return;
            }
            if (!char.IsControl(e.Character) && _createServerName.Length < 30)
                _createServerName += e.Character;
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

    private void HandleMenu(KeyboardState keyboard, float deltaSeconds)
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
        else if (_menuScreen == MenuScreen.CreateServer)
            HandleCreateServerScreen(keyboard);
        else if (_menuScreen == MenuScreen.Lobby)
            HandleLobbyScreen();
        else if (_menuScreen == MenuScreen.Prologue)
            HandlePrologueScreen(keyboard, deltaSeconds);
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

    // No longer a startup screen - reached only on demand (MainMenuAction.ChangeNick, Settings'
    // "ИЗМЕНИТЬ" button), pre-filled with whatever's already saved. Enter confirms whatever's
    // typed (blank falls back to "Игрок" rather than sending an empty name to the server) and
    // remembers it for next launch.
    private void HandleNicknameScreen(KeyboardState keyboard)
    {
        if (!Pressed(keyboard, Keys.Enter))
            return;

        _nickname = _nickname.Trim();
        if (_nickname.Length == 0)
            _nickname = "Игрок";

        PlayerSettingsStore.SaveNickname(_nickname);
        _menuScreen = MenuScreen.Main;
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
        if (!clicked)
            return;

        for (var i = 0; i < RoleChoices.Length; i++)
        {
            if (!GetRoleChoiceRect(i).Contains(_designMouse))
                continue;
            _selectedRole = RoleChoices[i];
            PlayerSettingsStore.SaveRole(_selectedRole);
            // The campaign has been waiting on this since the prologue faded out. Direct user
            // request ("удали все текущие корабли... полностью удалить из кода") - _prologuePendingShipKind
            // is only ever set by BeginPrologue, which nothing calls any more (the fixed 6-class
            // ship-select branch that used to trigger it is gone) - always null here now, falling
            // back to Custom, which StartHostedSession/World's own constructor resolve to the
            // frozen default hull (ShipDefaultHull.cs) whenever no CustomShipDefinition is given.
            var shipKind = _prologuePendingShipKind ?? ShipKind.Custom;
            _prologuePendingShipKind = null;
            StartHostedSession(shipKind, loadFrom: null);
            return;
        }
    }

    private bool Pressed(KeyboardState keyboard, Keys key) => keyboard.IsKeyDown(key) && _prevMenuKeyboard.IsKeyUp(key);

    // Called by Update when Escape comes in: true means "handled, stay in the game" (steps back one
    // screen toward Main rather than quitting outright). Main itself has nowhere further back to
    // go, so Escape there falls through to Exit(), same as it always has.
    private bool LeaveSubScreen()
    {
        // Редактор корабля в духе Cosmoteer + несколько сохранённых кораблей (humble-soaring-cat.md,
        // Step 6) - Escape while a "Сохранить как"/"Загрузить" overlay is open closes just the
        // overlay, same as it closes any other sub-screen; falling through to the ShipEditor case
        // below would kick the player all the way back to the main menu instead.
        if (_menuScreen == MenuScreen.ShipEditor && (_editorSaveAsPrompting || _editorLoadListOpen))
        {
            _editorSaveAsPrompting = false;
            _editorLoadListOpen = false;
            return true;
        }
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
        if (_menuScreen == MenuScreen.CreateServer)
        {
            _menuScreen = MenuScreen.Main;
            return true;
        }
        if (_menuScreen == MenuScreen.Lobby)
        {
            // Leaving a lobby (hosted or joined) tears the connection down the same way the pause
            // menu's own "ГЛАВНОЕ МЕНЮ" already does mid-round - there's no lighter-weight "just
            // disconnect" path anywhere in this project, and building one only for this one screen
            // isn't worth it when the heavier one already does the right thing here too.
            ReturnToMainMenu();
            return true;
        }
        if (_menuScreen == MenuScreen.Role)
        {
            _prologuePendingShipKind = null;
            _menuScreen = MenuScreen.Main;
            return true;
        }
        if (_menuScreen == MenuScreen.Prologue)
        {
            SkipPrologue();
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
        if (_menuScreen == MenuScreen.Settings && _awaitingRebindAction is not null)
        {
            // A pending Controls-tab rebind capture is one layer deeper than the settings screen
            // itself - Escape cancels just that (rather than rebinding the action to Escape) before
            // it's ever allowed to leave the screen underneath it, same priority the in-session
            // pause-menu path (Game1.cs) already gives its own capture.
            _awaitingRebindAction = null;
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
        CreateServer,
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
        ("СОЗДАТЬ СЕРВЕР", new Rectangle(76, 168, 160, 26), MainMenuAction.CreateServer, MainMenuIcon.Signal),
        ("ПРИСОЕДИНИТЬСЯ", new Rectangle(76, 200, 160, 24), MainMenuAction.Join, MainMenuIcon.Plug),
        ("РЕДАКТОР КОРАБЛЯ", new Rectangle(144, 316, 160, 24), MainMenuAction.ShipEditor, MainMenuIcon.Wrench),
        ("СМЕНИТЬ НИК", new Rectangle(144, 348, 160, 24), MainMenuAction.ChangeNick, MainMenuIcon.Person),
        ("НАСТРОЙКИ", new Rectangle(76, 420, 160, 24), MainMenuAction.Settings, MainMenuIcon.Bars),
        ("АВТОРЫ", new Rectangle(76, 456, 160, 26), MainMenuAction.Credits, MainMenuIcon.Medal),
        ("ВЫХОД", new Rectangle(76, 488, 160, 24), MainMenuAction.Exit, MainMenuIcon.Exit),
    };

    // A plate before each group of buttons, the way the reference screen marks its sections.
    //
    // Not lined up in a single column: the groups themselves start from two different left edges, and
    // a column of plates beside a staggered list reads as a separate thing standing next to the menu
    // rather than as part of it. Each plate takes its own group's indent, so the stagger is shared.
    //
    // The x for the near groups is 8 rather than 76-12-64=0 - at zero the plate is welded to the
    // screen edge. Vertical centres are the middle of each group's own span, so a group growing a
    // button moves its plate with it.
    private const int SectionPlateSize = 64;
    private static readonly (MenuSection Section, Rectangle Box)[] MainMenuSections =
    {
        (MenuSection.Campaign, new Rectangle(68, 44, SectionPlateSize, SectionPlateSize)),
        (MenuSection.Network, new Rectangle(8, 164, SectionPlateSize, SectionPlateSize)),
        (MenuSection.Shipyard, new Rectangle(68, 312, SectionPlateSize, SectionPlateSize)),
        (MenuSection.Systems, new Rectangle(8, 434, SectionPlateSize, SectionPlateSize)),
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

    // Direct user request ("в главном меню около версии маленькую кнопку... расписаны вкратце все
    // изменения... новый список начинается когда будет новая версия"; later "во всех новых больших
    // патчах в менюшке изменения пиши новые изменения") - kept brief on purpose (ChangelogPanel's
    // own 2-column layout only fits so much even at half-screen); everything since GameVersionText/
    // the version footer itself first appeared (bee4d7b), including this whole session's own still-
    // uncommitted work - resets to a fresh, empty list the day GameVersionText's own literal changes
    // to a new number. STANDING INSTRUCTION: append one short line here for every subsequent big
    // patch/feature landed this version, rather than waiting to be asked again.
    private static readonly string[] ChangelogEntries =
    {
        "Автопилот: клик — курс, зажатая ПКМ — наведение носом, облёт астероидов",
        "Двигатели включаются, только когда реально толкают в нужную сторону",
        "HP по отсекам — пробитый отсек теряет герметичность",
        "Двери на корабле теперь изначально закрыты",
        "Убраны фиксированные корпуса — только гибкий «Custom»",
        "Новый звуковой движок: выбор устройств, микрофон, направленный голос",
        "Редактор корабля: составные комнаты, отсеки, полублочные стены, повороты",
        "Новые устройства: Монитор состояния корабля, Консоль связи",
        "Штурвал/Навигация/Монитор/Связь/Щитки — теперь можно ставить по несколько",
        "Новые отсеки: Кокпит 4, Реакторный отсек 2, Щитовая 3",
        "Щиток: новая текстура и размер 1.5×1",
        "Турели: настоящая узкая форма крепления вместо квадрата 3×3",
        "Ускорена подсветка корабля у крупных станций",
        "Панорамирование карты у края экрана при управлении кораблём",
        "Кнопка «Изменения» рядом с версией в главном меню (эта менюшка)",
        "Смерть и режим наблюдения: полная видимость без тумана войны",
        "Наблюдатель: зум колёсиком (от 1/3× до 3×) и панорамирование у края экрана",
        "Скорость камеры наблюдателя зависит от зума — медленнее вблизи, быстрее издали",
        "Чит-панель (Ё): кнопка входа в режим наблюдателя без смерти",
        "При смерти модель пропадает, через 10 секунд возрождение в кокпите с таймером",
        "ESC-меню теперь работает в режиме наблюдателя, даже умерев за штурвалом/турелью",
        "Список кораблей для новой игры показывает только те, на которых реально можно играть",
        "Новый экран «Создать сервер»: название, макс. игроков, игровой стиль",
        "Лимит игроков на сервере теперь работает по-настоящему — лишним отказывает при входе",
        "Подключение к серверу: маленькое окошко «Подключение…» вместо строки в адресе",
        "Настоящее лобби перед стартом: список игроков, роли, готовность, выбор корабля хостом",
        "Редактор корабля: понятное сообщение, если устройство некуда поставить (вместо тишины)",
        "Голосовой чат: убраны помехи/треск от захвата микрофона в режиме опроса",
        "Призрак автопилота (вид снаружи) больше не вращается вместе с кораблём",
        "Автопилот (корабли с боковыми двигателями): курс прибытия фиксируется один раз, а не плывёт по пути",
        "Система достижений: 20 достижений, всплывающее окно при получении, вкладка «Достижения» в настройках",
        "2 корабля (cosmoteer1, рабочий корабль) теперь вшиты в игру - доступны даже после удаления в редакторе",
    };

    // The main menu's own click targets - mouse-driven (unlike the keyboard-shortcut screens either
    // side of it). Iterates the same MainMenuButtons list DrawMainMenuScreen draws, so a click can
    // never land on a rect the drawing doesn't actually show (or vice versa).
    private void HandleMainMenuClick()
    {
        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        var held = mouse.LeftButton == ButtonState.Pressed;
        _prevMenuLeftMouseButton = mouse.LeftButton;

        var point = _designMouse;

        // The changelog popup (Game1.cs's own DrawVersionFooter/ChangelogPanel) - modal while open,
        // the same "only its own close button/scrollbar does anything" shape ShipStatusMonitor/
        // CommsConsole's full-screen overlays already use, just for a small popup instead of the
        // whole screen. Runs BEFORE the `!clicked` early-return below (unlike every other button
        // here) - direct user request ("прямоугольничек... тянешь его вниз... листается список") -
        // dragging the scrollbar thumb needs to keep tracking every frame the button stays held,
        // not just the one frame it was first pressed.
        if (_changelogOpen)
        {
            if (!held)
                _changelogThumbDragLastMouseY = null;
            else if (_changelogThumbDragLastMouseY is { } lastY)
            {
                _changelogScrollOffset = Math.Clamp(_changelogScrollOffset + (mouse.Position.Y - lastY),
                    0f, ChangelogPanel.MaxScroll(ChangelogEntries.Length));
                _changelogThumbDragLastMouseY = mouse.Position.Y;
            }
            else if (clicked && ChangelogPanel.GetScrollThumbRect(ChangelogPanelOrigin, ChangelogEntries.Length, _changelogScrollOffset)
                     is { } thumbRect && thumbRect.Contains(point))
            {
                _changelogThumbDragLastMouseY = mouse.Position.Y;
            }
            else if (clicked && ChangelogPanel.GetCloseButtonRect(ChangelogPanelOrigin).Contains(point))
            {
                PlayUiClick();
                _changelogOpen = false;
            }
            return;
        }

        if (!clicked)
            return;
        if (ChangelogButtonRect(new Vector2(8, DesignHeight - 16)).Contains(point))
        {
            PlayUiClick();
            _changelogOpen = true;
            _changelogScrollOffset = 0f;
            return;
        }
        foreach (var (_, rect, action, _) in MainMenuButtons)
        {
            if (!IsMainMenuButtonVisible(action) || !IsMainMenuButtonEnabled(action) || !rect.Contains(point))
                continue;

            PlayUiClick();
            switch (action)
            {
                case MainMenuAction.NewGame:
                    _menuScreen = MenuScreen.ShipSelect;
                    break;
                case MainMenuAction.Tutorial:
                    // Always the same layout, no ship-select step - the tutorial's own room ids
                    // (World.Tutorial.cs) are hardcoded to it. That layout used to be the fixed
                    // Frigate hull; direct user request ("удали все текущие корабли... полностью
                    // удалить из кода") removed the hull class itself, but its exact shape survives
                    // as ShipDefaultHull.cs's frozen default - Custom (with no CustomShipDefinition)
                    // resolves to that same layout, so the tutorial's own hardcoded room ids stay valid.
                    StartHostedSession(ShipKind.Custom, loadFrom: null, isTutorial: true);
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
                case MainMenuAction.CreateServer:
                    // Defaults refreshed every time this screen is entered, same "start clean, not
                    // wherever a previous visit left it" convention the ship-overview camera uses -
                    // a stale server name from a prior session reads as a bug, not a remembered
                    // preference.
                    _createServerName = $"{_nickname} — сервер";
                    _createServerMaxPlayers = DefaultCreateServerMaxPlayers;
                    _menuScreen = MenuScreen.CreateServer;
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
        // Редактор корабля в духе Cosmoteer + несколько сохранённых кораблей (humble-soaring-cat.md,
        // Step 7) - the only mouse-driven part of an otherwise keyboard-only screen, since a saved
        // ship is identified by name, not a single digit key the way the 4 fixed classes are.
        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        _prevMenuLeftMouseButton = mouse.LeftButton;
        if (clicked)
            HandleShipSelectCustomShipClick(_designMouse);

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

    }

    // Редактор корабля в духе Cosmoteer + несколько сохранённых кораблей (humble-soaring-cat.md,
    // Step 7) - same entry point the editor's own "Играть" button uses (Game1.ShipEditor.cs's
    // HandleShipEditorPlayClicked): no prologue, straight into a session with ShipKind.Custom. An
    // invalid design (broken by hand-editing the JSON, or never finished) is simply not clickable -
    // no error message, exactly like a disabled "Играть" button in the editor itself.
    private void HandleShipSelectCustomShipClick(Point point)
    {
        var names = PlayableCustomShipNames();
        for (var i = 0; i < names.Count; i++)
        {
            if (!GetShipSelectCustomRowRect(i).Contains(point))
                continue;
            // Re-loaded rather than trusting the filter's own moment-ago result - the file could in
            // principle have been deleted/broken out from under this list between the two (hand-
            // editing the JSON, or another process touching the same save folder).
            if (LoadPlayableCustomShip(names[i]) is not { } definition || CustomShipValidator.Validate(definition).Count > 0)
                return;
            SaveStore.Delete();
            StartHostedSession(ShipKind.Custom, loadFrom: null, customShip: definition);
            return;
        }
    }

    // Direct user request ("отображались только те, на которых реально можно поиграть") - a saved
    // design that never finished (missing a required system, CustomShipValidator's own list)
    // doesn't even appear here any more, rather than showing up dimmed/disabled the way it used to -
    // the SAME filtered list DrawShipSelectCustomShipList/HandleShipSelectCustomShipClick both read,
    // so a click can never land on a row the drawing doesn't actually show as playable (or vice
    // versa), the same "one true list" shape MainMenuButtons already established.
    //
    // Direct user request ("сделай чтобы 2 текущих корабля... были вшиты в игру и были всегда
    // доступны даже если их удалить из редактора") - ShipBuiltInFleet.Names always leads the list,
    // regardless of what's actually in CustomShipStore right now; a local save reusing one of
    // those 2 names is filtered out of the local half below so it never appears twice.
    private static IReadOnlyList<string> PlayableCustomShipNames() =>
        ShipBuiltInFleet.Names
            .Concat(CustomShipStore.ListShips()
                .Where(name => !ShipBuiltInFleet.Names.Contains(name))
                .Where(name => CustomShipStore.LoadShip(name) is { } definition && CustomShipValidator.Validate(definition).Count == 0))
            .ToList();

    // The one place that resolves a name from PlayableCustomShipNames() back into a definition -
    // a built-in name (ShipBuiltInFleet.Names) always resolves to the frozen copy, never to
    // CustomShipStore, so these 2 ships are provably immune to the local file being deleted,
    // renamed, or hand-edited into something broken.
    private static CustomShipDefinition? LoadPlayableCustomShip(string name) =>
        ShipBuiltInFleet.TryGet(name) ?? CustomShipStore.LoadShip(name);

    private static Rectangle GetShipSelectCustomRowRect(int index) => new(650, 110 + index * 30, 300, 26);

    // ---- "Создать сервер" (screenshots of Barotrauma's own server-config screen) ----
    private const int CreateServerPanelWidth = 820;
    private const int CreateServerPanelHeight = 520;
    private const int CreateServerHeaderHeight = 40;

    private static Vector2 CreateServerPanelOrigin =>
        new((DesignWidth - CreateServerPanelWidth) / 2f, (DesignHeight - CreateServerPanelHeight) / 2f);
    private static Vector2 CreateServerContentOrigin(Vector2 panelOrigin) => panelOrigin + new Vector2(28, CreateServerHeaderHeight + 20);

    private static Rectangle GetCreateServerNameFieldRect(Vector2 panelOrigin)
    { var p = CreateServerContentOrigin(panelOrigin); return new Rectangle((int)p.X, (int)p.Y + 18, 560, 28); }
    private static Rectangle GetCreateServerMaxPlayersMinusRect(Vector2 panelOrigin)
    { var p = CreateServerContentOrigin(panelOrigin); return new Rectangle((int)p.X, (int)p.Y + 78, 26, 26); }
    private static Rectangle GetCreateServerMaxPlayersPlusRect(Vector2 panelOrigin)
    { var p = CreateServerContentOrigin(panelOrigin); return new Rectangle((int)p.X + 90, (int)p.Y + 78, 26, 26); }
    private static Rectangle GetCreateServerStylePrevRect(Vector2 panelOrigin)
    { var p = CreateServerContentOrigin(panelOrigin); return new Rectangle((int)p.X, (int)p.Y + 140, 26, 26); }
    private static Rectangle GetCreateServerStyleNextRect(Vector2 panelOrigin)
    { var p = CreateServerContentOrigin(panelOrigin); return new Rectangle((int)p.X + 300, (int)p.Y + 140, 26, 26); }
    // Decorative only (direct user confirmation, "показать визуально, но неактивно") - drawn but
    // deliberately never hit-tested by HandleCreateServerScreen below, so a click here is a no-op
    // rather than silently flipping a field nothing else in the game ever reads.
    private static Rectangle GetCreateServerPasswordFieldRect(Vector2 panelOrigin)
    { var p = CreateServerContentOrigin(panelOrigin); return new Rectangle((int)p.X, (int)p.Y + 262, 260, 26); }
    private static Rectangle GetCreateServerPublicCheckboxRect(Vector2 panelOrigin)
    { var p = CreateServerContentOrigin(panelOrigin); return new Rectangle((int)p.X, (int)p.Y + 346, 20, 20); }
    private static Rectangle GetCreateServerLockCheckboxRect(Vector2 panelOrigin)
    { var p = CreateServerContentOrigin(panelOrigin); return new Rectangle((int)p.X, (int)p.Y + 380, 20, 20); }
    private static Rectangle GetCreateServerKarmaCheckboxRect(Vector2 panelOrigin)
    { var p = CreateServerContentOrigin(panelOrigin); return new Rectangle((int)p.X, (int)p.Y + 414, 20, 20); }

    private static Rectangle GetCreateServerStartButtonRect(Vector2 panelOrigin) =>
        new((int)panelOrigin.X + CreateServerPanelWidth / 2 - 220, (int)panelOrigin.Y + CreateServerPanelHeight - 52, 200, 36);
    private static Rectangle GetCreateServerBackButtonRect(Vector2 panelOrigin) =>
        new((int)panelOrigin.X + CreateServerPanelWidth / 2 + 20, (int)panelOrigin.Y + CreateServerPanelHeight - 52, 200, 36);

    // Purely cosmetic flavor text under the carousel's own arrows (_createServerStyleIndex) - this
    // game has exactly one real game mode (the co-op campaign), so "cycling" here never changes
    // anything about the session that actually starts; it only mirrors the reference screenshot's
    // own carousel row.
    private static readonly (string Name, string Description)[] CreateServerStyles =
    {
        ("Кооператив", "Экипаж вместе чинит корабль, летает по системам и выполняет задания."),
    };

    private void HandleCreateServerScreen(KeyboardState keyboard)
    {
        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        _prevMenuLeftMouseButton = mouse.LeftButton;
        if (!clicked)
            return;

        PlayUiClick();
        var origin = CreateServerPanelOrigin;
        var point = _designMouse;

        if (GetCreateServerMaxPlayersMinusRect(origin).Contains(point))
            _createServerMaxPlayers = Math.Max(MinCreateServerMaxPlayers, _createServerMaxPlayers - 1);
        else if (GetCreateServerMaxPlayersPlusRect(origin).Contains(point))
            _createServerMaxPlayers = Math.Min(MaxCreateServerMaxPlayers, _createServerMaxPlayers + 1);
        else if (CreateServerStyles.Length > 1 && GetCreateServerStylePrevRect(origin).Contains(point))
            _createServerStyleIndex = (_createServerStyleIndex - 1 + CreateServerStyles.Length) % CreateServerStyles.Length;
        else if (CreateServerStyles.Length > 1 && GetCreateServerStyleNextRect(origin).Contains(point))
            _createServerStyleIndex = (_createServerStyleIndex + 1) % CreateServerStyles.Length;
        else if (GetCreateServerStartButtonRect(origin).Contains(point))
        {
            // The real Barotrauma-style lobby (screenshot 3) - StartHostedLobby builds a GameServer
            // that stays in SessionPhase.Lobby until this host later presses "НАЧАТЬ" a second time,
            // from inside the lobby screen itself.
            StartHostedLobby();
        }
        else if (GetCreateServerBackButtonRect(origin).Contains(point))
            _menuScreen = MenuScreen.Main;
        // Пароль/Язык/Исполняемый файл/3 чекбокса - no hit-test at all, by design (see the rect
        // helpers' own doc comment above).
    }

    private void DrawCreateServerScreen()
    {
        var origin = CreateServerPanelOrigin;
        var panelRect = new Rectangle((int)origin.X, (int)origin.Y, CreateServerPanelWidth, CreateServerPanelHeight);

        DrawVerticalGradient(panelRect, SettingsPanelTop, SettingsPanelBottom);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, panelRect, SettingsBorderSteel, 2);
        var trimRect = new Rectangle(panelRect.X + 4, panelRect.Y + 4, panelRect.Width - 8, panelRect.Height - 8);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, trimRect, SettingsBorderGold * 0.55f, 1);
        ShipRenderer.DrawRivets(_spriteBatch, _pixel, panelRect);

        var headerRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, CreateServerHeaderHeight);
        DrawVerticalGradient(headerRect, SettingsHeaderTop, SettingsHeaderBottom);
        _spriteBatch.Draw(_pixel, new Rectangle(headerRect.X, headerRect.Bottom - 2, headerRect.Width, 2), SettingsBorderGold);
        _spriteBatch.DrawString(_font, "Создать сервер", origin + new Vector2(17, 11), Color.Black * 0.5f, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, "Создать сервер", origin + new Vector2(16, 10), SettingsAccentGold, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);

        var content = CreateServerContentOrigin(origin);

        _spriteBatch.DrawString(_font, "Название сервера", content, SettingsTextDim, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        var nameRect = GetCreateServerNameFieldRect(origin);
        _spriteBatch.Draw(_pixel, nameRect, new Color(10, 12, 16) * 0.85f);
        DrawBevel(nameRect, raised: false);
        _spriteBatch.DrawString(_font, _createServerName + "_", new Vector2(nameRect.X + 8, nameRect.Y + 6), SettingsAccentTeal, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        _spriteBatch.DrawString(_font, "Макс. игроков", content + new Vector2(0, 62), SettingsTextDim, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        var minusRect = GetCreateServerMaxPlayersMinusRect(origin);
        var plusRect = GetCreateServerMaxPlayersPlusRect(origin);
        DrawCreateServerStepperButton(minusRect, "-");
        DrawCreateServerStepperButton(plusRect, "+");
        var countSize = _font.MeasureString(_createServerMaxPlayers.ToString()) * 0.7f;
        _spriteBatch.DrawString(_font, _createServerMaxPlayers.ToString(),
            new Vector2(minusRect.Right + (plusRect.X - minusRect.Right) / 2f - countSize.X / 2f, minusRect.Y + 2),
            SettingsTextPrimary, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        _spriteBatch.DrawString(_font, "Игровой стиль", content + new Vector2(0, 124), SettingsTextDim, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        var stylePrev = GetCreateServerStylePrevRect(origin);
        var styleNext = GetCreateServerStyleNextRect(origin);
        DrawCreateServerStepperButton(stylePrev, "<");
        DrawCreateServerStepperButton(styleNext, ">");
        var style = CreateServerStyles[_createServerStyleIndex];
        var styleLabelSize = _font.MeasureString(style.Name) * 0.65f;
        _spriteBatch.DrawString(_font, style.Name,
            new Vector2(stylePrev.Right + (styleNext.X - stylePrev.Right) / 2f - styleLabelSize.X / 2f, stylePrev.Y + 2),
            SettingsAccentGold, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, style.Description, content + new Vector2(0, 172), SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

        // Everything below this line is drawn but permanently inert (direct user confirmation,
        // "показать визуально, но неактивно") - this game has no password auth, no public server
        // list, no karma system and no locale files, so none of these could be wired to anything
        // real without building those systems first, which is out of scope for this screen.
        _spriteBatch.DrawString(_font, "Параметры сервера (пока недоступны)", content + new Vector2(0, 216), SettingsAccentRed * 0.85f, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        var dimLabel = SettingsTextDim * 0.7f;
        _spriteBatch.DrawString(_font, "Пароль", content + new Vector2(0, 244), dimLabel, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        var pwRect = GetCreateServerPasswordFieldRect(origin);
        _spriteBatch.Draw(_pixel, pwRect, new Color(10, 12, 16) * 0.5f);
        DrawBevel(pwRect, raised: false, strength: 0.4f);
        _spriteBatch.DrawString(_font, "--------", new Vector2(pwRect.X + 8, pwRect.Y + 6), dimLabel, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        _spriteBatch.DrawString(_font, "Язык: Русский", content + new Vector2(300, 244), dimLabel, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, "Исполняемый файл: Anabiosis.Server.exe", content + new Vector2(0, 306), dimLabel, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        DrawCreateServerInertCheckbox(GetCreateServerPublicCheckboxRect(origin), "Публичный сервер", dimLabel);
        DrawCreateServerInertCheckbox(GetCreateServerLockCheckboxRect(origin), "Блокировка новых участников", dimLabel);
        DrawCreateServerInertCheckbox(GetCreateServerKarmaCheckboxRect(origin), "Карма", dimLabel);

        var startRect = GetCreateServerStartButtonRect(origin);
        var startHover = SettingsHoverEase("createserver:start", startRect.Contains(_designMouse));
        DrawVerticalGradient(startRect, Color.Lerp(SettingsAccentGold, Color.White, 0.15f * startHover), SettingsAccentGold * 0.75f);
        DrawBevel(startRect, raised: true);
        var startLabelSize = _font.MeasureString("НАЧАТЬ") * 0.7f;
        _spriteBatch.DrawString(_font, "НАЧАТЬ",
            new Vector2(startRect.Center.X - startLabelSize.X / 2f, startRect.Center.Y - startLabelSize.Y / 2f),
            new Color(30, 24, 10), 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        var backRect = GetCreateServerBackButtonRect(origin);
        var backHover = SettingsHoverEase("createserver:back", backRect.Contains(_designMouse));
        _spriteBatch.Draw(_pixel, backRect, Color.Lerp(new Color(40, 44, 52), new Color(60, 64, 74), backHover));
        DrawBevel(backRect, raised: backHover > 0.05f);
        var backLabelSize = _font.MeasureString("НАЗАД") * 0.7f;
        _spriteBatch.DrawString(_font, "НАЗАД",
            new Vector2(backRect.Center.X - backLabelSize.X / 2f, backRect.Center.Y - backLabelSize.Y / 2f),
            SettingsTextPrimary, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    private void DrawCreateServerStepperButton(Rectangle rect, string glyph)
    {
        var hover = SettingsHoverEase($"createserver:step:{rect.X}:{rect.Y}", rect.Contains(_designMouse));
        _spriteBatch.Draw(_pixel, rect, Color.Lerp(new Color(34, 40, 50), new Color(52, 60, 72), hover));
        DrawBevel(rect, raised: true, strength: 0.7f + hover * 0.3f);
        var size = _font.MeasureString(glyph) * 0.6f;
        _spriteBatch.DrawString(_font, glyph, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f), SettingsTextPrimary, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    // Drawn checked-looking but dimmed, and never hit-tested by HandleCreateServerScreen - see that
    // method's own doc comment for why (no backing mechanic exists for any of the 3 callers).
    private void DrawCreateServerInertCheckbox(Rectangle rect, string label, Color dimLabel)
    {
        _spriteBatch.Draw(_pixel, rect, new Color(20, 22, 26) * 0.6f);
        DrawBevel(rect, raised: false, strength: 0.35f);
        _spriteBatch.DrawString(_font, label, new Vector2(rect.Right + 8, rect.Y + 1), dimLabel, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
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
            _lobbyReady = false;
            _lobbySelectedShipName = null;
            // Routes through the lobby screen rather than setting _sessionStarted straight away -
            // this joiner has no way to know in advance whether the host is still waiting in their
            // own lobby (GameServer's SessionPhase.Lobby) or the round is already running (every
            // OTHER way to host - solo's own [H]-toggle, tutorial, editor "Играть" - never has a
            // lobby at all). HandleLobbyScreen's own per-frame poll notices a real WorldSnapshot
            // arrive either way and flips _sessionStarted itself; for an already-running host that
            // happens within a tick or two, an imperceptible flash rather than a real wait.
            _client.Send(new ClientCommand(_client.PlayerId, Nickname: _nickname, SetOwnRoleTo: _selectedRole));
            _menuScreen = MenuScreen.Lobby;
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
            : (text, Anabiosis.Shared.Networking.Wire.DefaultPort);
    }

    // The host is a player like any other - their own session is the same SoloSession solo mode
    // uses, with the listen socket as the only difference.
    //
    // Constructing SoloSession means constructing GameServer means constructing World means
    // GalaxyMap.CreateStarter() (World.Voyage.cs's own field initializer) - which, since real
    // celestial bodies/belt asteroids (M50) replaced the near-free stub every procedural system
    // used to be, now does genuinely non-trivial work generating however many systems it takes for
    // every hand-authored system to have its own starting neighbours. Done inline this used to
    // freeze the window (no repaint at all - Update/Draw simply don't run again until the
    // constructor returns) for however long that took. Run on a background Task instead - Update/
    // Draw keep running every frame on a loading screen (Game1.cs) while _pendingSession is live,
    // polling GalaxyMap.Current's own live progress, exactly the way the embedded server's tick
    // loop already runs on its own background thread once the session exists.
    private System.Threading.Tasks.Task<SoloSession>? _pendingSession;

    // Direct user request ("если играешь в редакторе... вернуться в редактор и продолжать
    // собирать корабль") - true only for a session started from the Ship Editor's own "Играть"
    // button (HandleShipEditorPlayClicked), never for a normal НОВАЯ ИГРА/ПРОДОЛЖИТЬ/ПРИСОЕДИНИТЬСЯ/
    // a saved custom ship picked on ShipSelect - those have no editor canvas to go back to. Gates
    // the pause menu's 5th button (PauseMenuPanel) and ReturnToShipEditor below.
    private bool _sessionStartedFromEditor;

    private void StartHostedSession(ShipKind shipKind, SaveGame? loadFrom, CustomShipDefinition? customShip = null, bool isTutorial = false, bool fromShipEditor = false)
    {
        _sessionStartedFromEditor = fromShipEditor;
        var openToNetwork = _openToNetwork;
        var maxPlayers = _hostMaxPlayers;
        _pendingSession = Task.Run(() => new SoloSession(shipKind, loadFrom,
            openToNetwork ? Anabiosis.Shared.Networking.Wire.DefaultPort : null, customShip, isTutorial, maxPlayers));
    }

    // Polled every frame from Update while _pendingSession is running (Game1.cs) - finishes the
    // exact same setup StartHostedSession used to do synchronously, the instant the background
    // construction completes.
    private void FinishPendingSessionIfReady()
    {
        if (_pendingSession is not { IsCompleted: true } task)
            return;
        _pendingSession = null;

        if (task.IsFaulted)
        {
            // Whatever actually failed (a corrupt save, most plausibly) already has nowhere good to
            // surface to mid-load - falling back to the main menu is the same recovery a network
            // join failure already gets (Game1.Menu.cs's HandleJoinScreen), rather than taking the
            // whole process down on an exception that happened on a background thread.
            return;
        }

        var session = task.Result;
        _session = session;
        _client = new GameClient(session.Connection, session.PlayerId);
        _sessionStarted = true;
        // Rides the same _pendingSetOwnRoleTo the in-game crew panel's own click handler uses
        // (Game1.Input.cs) - picked up on the very next Update, exactly like a manual click.
        _pendingSetOwnRoleTo = _selectedRole;
    }

    // "Создать сервер"'s own "НАЧАТЬ" - same background-Task shape as StartHostedSession above
    // (GameServer's lobby constructor is cheap, no GalaxyMap/World work happens until the round
    // actually starts, but running it off the game thread anyway keeps this one code path rather
    // than a second, only-sometimes-needed synchronous variant).
    private void StartHostedLobby()
    {
        var serverName = _createServerName;
        var maxPlayers = _createServerMaxPlayers;
        _pendingLobbySession = Task.Run(() => new SoloSession(serverName, maxPlayers, Anabiosis.Shared.Networking.Wire.DefaultPort));
    }

    // Polled every frame from Update while _pendingLobbySession is running (Game1.cs) - unlike
    // FinishPendingSessionIfReady, this does NOT set _sessionStarted: the round hasn't begun, only
    // the waiting room has. HandleLobbyScreen's own per-frame poll is what actually flips
    // _sessionStarted, once a real WorldSnapshot shows this host pressed the lobby's own "НАЧАТЬ".
    private void FinishPendingLobbySessionIfReady()
    {
        if (_pendingLobbySession is not { IsCompleted: true } task)
            return;
        _pendingLobbySession = null;

        if (task.IsFaulted)
            return; // same silent fall-back-to-menu reasoning as FinishPendingSessionIfReady above

        var session = task.Result;
        _session = session;
        _client = new GameClient(session.Connection, session.PlayerId);
        _lobbyReady = false;
        _lobbySelectedShipName = null;
        _menuScreen = MenuScreen.Lobby;
        // Own nickname/role reach the roster as an ordinary lobby command right away, rather than
        // waiting for the client's regular per-tick resend to eventually get around to it - the
        // host would otherwise see their own row blank for the first several ticks.
        _client.Send(new ClientCommand(_client.PlayerId, Nickname: _nickname, SetOwnRoleTo: _selectedRole));
    }
    // ---- Barotrauma-style lobby (screenshot 3) - player list, own role/ready, host-only ship pick
    // and start button ----
    private const int LobbyPanelWidth = 900;
    private const int LobbyPanelHeight = 520;
    private static Vector2 LobbyPanelOrigin => new((DesignWidth - LobbyPanelWidth) / 2f, (DesignHeight - LobbyPanelHeight) / 2f);
    private static Vector2 LobbyContentOrigin(Vector2 origin) => origin + new Vector2(28, 76);
    private const int LobbyRowHeight = 30;

    private static Rectangle GetLobbyPlayerRowRect(Vector2 origin, int index)
    { var p = LobbyContentOrigin(origin); return new Rectangle((int)p.X, (int)p.Y + index * LobbyRowHeight, 360, 26); }
    private static Rectangle GetLobbyOwnRoleRect(Vector2 origin)
    { var p = LobbyContentOrigin(origin); return new Rectangle((int)p.X, (int)p.Y - 42, 220, 28); }
    private static Rectangle GetLobbyReadyButtonRect(Vector2 origin) =>
        new((int)origin.X + LobbyPanelWidth - 220, (int)origin.Y + LobbyPanelHeight - 52, 190, 36);
    private static Rectangle GetLobbyStartButtonRect(Vector2 origin) =>
        new((int)origin.X + 20, (int)origin.Y + LobbyPanelHeight - 52, 190, 36);
    private static Rectangle GetLobbyShipRowRect(Vector2 origin, int index)
    { var p = LobbyContentOrigin(origin); return new Rectangle((int)p.X + 420, (int)p.Y + index * 26, 420, 22); }

    private void HandleLobbyScreen()
    {
        _client?.PollSnapshots();

        // The round actually began - either this host just pressed the lobby's own start button,
        // or (every non-lobby way to end up here - joining an already-running old-style host) a
        // real WorldSnapshot was always going to arrive within a tick or two regardless. Either
        // way, gameplay takes over exactly like it always has for every other entry point.
        if (_client?.LatestSnapshot is not null)
        {
            _sessionStarted = true;
            _pendingSetOwnRoleTo = _selectedRole;
            return;
        }

        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        _prevMenuLeftMouseButton = mouse.LeftButton;
        if (!clicked || _client is not { LatestLobby: { } lobby })
            return;

        PlayUiClick();
        var origin = LobbyPanelOrigin;
        var point = _designMouse;
        var isHost = lobby.Players.FirstOrDefault(p => p.PlayerId == _client.PlayerId)?.IsHost ?? false;

        if (GetLobbyReadyButtonRect(origin).Contains(point))
        {
            _lobbyReady = !_lobbyReady;
            _client.Send(new ClientCommand(_client.PlayerId, LobbyReady: _lobbyReady));
        }
        else if (GetLobbyOwnRoleRect(origin).Contains(point))
        {
            var currentIndex = Array.IndexOf(RoleChoices, _selectedRole ?? RoleChoices[0]);
            var nextRole = RoleChoices[(currentIndex + 1) % RoleChoices.Length];
            _selectedRole = nextRole;
            PlayerSettingsStore.SaveRole(nextRole);
            _client.Send(new ClientCommand(_client.PlayerId, SetOwnRoleTo: nextRole));
        }
        else if (isHost && GetLobbyStartButtonRect(origin).Contains(point))
        {
            _client.Send(new ClientCommand(_client.PlayerId, LobbyStartRoundPressed: true));
        }
        else if (isHost)
        {
            var names = PlayableCustomShipNames();
            for (var i = 0; i < names.Count; i++)
            {
                if (!GetLobbyShipRowRect(origin, i).Contains(point))
                    continue;
                // Re-loaded rather than trusting the filter's own moment-ago result, same defensive
                // re-check HandleShipSelectCustomShipClick already does - the file could in
                // principle have been deleted/broken out from under this list in the meantime.
                if (LoadPlayableCustomShip(names[i]) is not { } definition || CustomShipValidator.Validate(definition).Count > 0)
                    return;
                _lobbySelectedShipName = names[i];
                _client.Send(new ClientCommand(_client.PlayerId, LobbySelectCustomShip: definition, LobbySelectCustomShipName: names[i]));
                return;
            }
        }
    }

    private void DrawLobbyScreen()
    {
        var origin = LobbyPanelOrigin;
        var panelRect = new Rectangle((int)origin.X, (int)origin.Y, LobbyPanelWidth, LobbyPanelHeight);

        DrawVerticalGradient(panelRect, SettingsPanelTop, SettingsPanelBottom);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, panelRect, SettingsBorderSteel, 2);
        ShipRenderer.DrawRivets(_spriteBatch, _pixel, panelRect);

        var lobby = _client?.LatestLobby;
        var title = lobby?.ServerName ?? "Ожидание ответа хоста...";
        _spriteBatch.DrawString(_font, title, origin + new Vector2(16, 10), SettingsAccentGold, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);

        if (lobby is null)
            return;

        var isHost = lobby.Players.FirstOrDefault(p => p.PlayerId == _client!.PlayerId)?.IsHost ?? false;

        var ownRoleRect = GetLobbyOwnRoleRect(origin);
        _spriteBatch.Draw(_pixel, ownRoleRect, new Color(30, 37, 46) * 0.9f);
        DrawBevel(ownRoleRect, raised: true, strength: 0.6f);
        var ownRoleLabel = "Роль: " + (_selectedRole is { } r ? CrewRoles.Name(r) : "не выбрана");
        _spriteBatch.DrawString(_font, ownRoleLabel, new Vector2(ownRoleRect.X + 8, ownRoleRect.Y + 6), SettingsTextPrimary, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        _spriteBatch.DrawString(_font, $"Игроки ({lobby.Players.Count}/{lobby.MaxPlayers})", LobbyContentOrigin(origin) + new Vector2(0, -20),
            SettingsTextDim, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        for (var i = 0; i < lobby.Players.Count; i++)
        {
            var player = lobby.Players[i];
            var rowRect = GetLobbyPlayerRowRect(origin, i);
            _spriteBatch.Draw(_pixel, rowRect, new Color(24, 28, 34) * 0.8f);
            DrawBevel(rowRect, raised: false, strength: 0.35f);

            var name = (player.Nickname ?? "...") + (player.IsHost ? " (хост)" : "");
            _spriteBatch.DrawString(_font, name, new Vector2(rowRect.X + 8, rowRect.Y + 5), player.IsHost ? SettingsAccentGold : SettingsTextPrimary,
                0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            var roleLabel = player.Role is { } role ? CrewRoles.Name(role) : "-";
            _spriteBatch.DrawString(_font, roleLabel, new Vector2(rowRect.X + 200, rowRect.Y + 5), SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, player.IsReady ? "ГОТОВ" : "...", new Vector2(rowRect.Right - 60, rowRect.Y + 5),
                player.IsReady ? SettingsAccentTeal : SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        if (isHost)
        {
            _spriteBatch.DrawString(_font, "Корабль (выбирает хост):", LobbyContentOrigin(origin) + new Vector2(420, -20),
                SettingsTextDim, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            var names = PlayableCustomShipNames();
            if (lobby.CustomShipName is null)
                _spriteBatch.DrawString(_font, "По умолчанию (выбрано)", LobbyContentOrigin(origin) + new Vector2(420, 4),
                    SettingsAccentTeal, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            for (var i = 0; i < names.Count; i++)
            {
                var rowRect = GetLobbyShipRowRect(origin, i);
                var selected = lobby.CustomShipName == names[i];
                _spriteBatch.Draw(_pixel, rowRect, (selected ? SettingsAccentGold : new Color(24, 28, 34)) * (selected ? 0.4f : 0.7f));
                _spriteBatch.DrawString(_font, names[i], new Vector2(rowRect.X + 6, rowRect.Y + 3),
                    selected ? SettingsAccentGold : SettingsTextPrimary, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            }

            var startRect = GetLobbyStartButtonRect(origin);
            var startHover = SettingsHoverEase("lobby:start", startRect.Contains(_designMouse));
            DrawVerticalGradient(startRect, Color.Lerp(SettingsAccentGold, Color.White, 0.15f * startHover), SettingsAccentGold * 0.75f);
            DrawBevel(startRect, raised: true);
            var startLabelSize = _font.MeasureString("НАЧАТЬ") * 0.7f;
            _spriteBatch.DrawString(_font, "НАЧАТЬ", new Vector2(startRect.Center.X - startLabelSize.X / 2f, startRect.Center.Y - startLabelSize.Y / 2f),
                new Color(30, 24, 10), 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        }
        else
        {
            var startRect = GetLobbyStartButtonRect(origin);
            _spriteBatch.DrawString(_font, "Ожидание хоста...", new Vector2(startRect.X, startRect.Y + 8), SettingsTextDim, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        }

        var readyRect = GetLobbyReadyButtonRect(origin);
        var readyHover = SettingsHoverEase("lobby:ready", readyRect.Contains(_designMouse));
        _spriteBatch.Draw(_pixel, readyRect, Color.Lerp(_lobbyReady ? SettingsAccentTeal * 0.6f : new Color(40, 44, 52), _lobbyReady ? SettingsAccentTeal : new Color(60, 64, 74), readyHover));
        DrawBevel(readyRect, raised: true);
        var readyLabel = _lobbyReady ? "ГОТОВ" : "НЕ ГОТОВ";
        var readyLabelSize = _font.MeasureString(readyLabel) * 0.6f;
        _spriteBatch.DrawString(_font, readyLabel, new Vector2(readyRect.Center.X - readyLabelSize.X / 2f, readyRect.Center.Y - readyLabelSize.Y / 2f),
            SettingsTextPrimary, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        _spriteBatch.DrawString(_font, "[Esc] выйти из лобби - нажмите на свою строку, чтобы сменить роль",
            origin + new Vector2(16, LobbyPanelHeight - 18), SettingsTextDim * 0.8f, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
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
        _inGameSettingsOpen = false;
        _openBlock = ClickTarget.None;
        _crewPanelOpen = false;
        _infoPanelOpen = false;
        _shipEditorOpen = false;
        _talkingToNpcId = null;
        _sessionStartedFromEditor = false;
        _spectatorMode = false;

        _menuScreen = MenuScreen.Main;
    }

    // The pause menu's "ВЕРНУТЬСЯ В РЕДАКТОР" (direct user request) - only ever reachable while
    // _sessionStartedFromEditor is true. Tears the live test session down exactly like
    // ReturnToMainMenu does, but lands on MenuScreen.ShipEditor instead of Main - and, critically,
    // does NOT call EnterShipEditor(): that reloads the canvas from CustomShipStore.Load()/
    // LoadTileCanvas(), which for a just-Play-tested design is stale (HandleShipEditorPlayClicked
    // never calls SaveTileCanvas) or blank. Every _editor* field (_editorTiles and its sibling
    // dictionaries) is a plain Game1 instance field that a hosted session never touches at all, so
    // leaving them alone is exactly "resume exactly where Играть left off".
    private void ReturnToShipEditor()
    {
        _session?.Dispose();
        _session = null;
        _client = null!;
        _sessionStarted = false;
        _existingSave = SaveStore.Load();

        _pauseMenuOpen = false;
        _inGameSettingsOpen = false;
        _openBlock = ClickTarget.None;
        _crewPanelOpen = false;
        _infoPanelOpen = false;
        _shipEditorOpen = false;
        _talkingToNpcId = null;
        _sessionStartedFromEditor = false;
        _spectatorMode = false;

        _menuScreen = MenuScreen.ShipEditor;
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

        // The backdrop goes down in its own batch, before the main one, because it needs PointClamp
        // and the rest of the menu wants the default filtering. It is pixel art authored at 286x186,
        // so any smoothing on the way up to the pane would destroy the whole reason for drawing it
        // that way. Cheaper and clearer than ending and restarting the main batch mid-screen.
        if (_menuScreen == MenuScreen.Main && _menuBackdrop is not null)
        {
            var backdropPane = new Rectangle(MenuPaneX, 0, DesignWidth - MenuPaneX, DesignHeight);
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp,
                transformMatrix: _renderScale);
            _spriteBatch.Draw(_menuBackdrop, backdropPane, Color.White);
            _spriteBatch.End();

            // The moving half of the scene: the planet turning and the engines burning, both drawn
            // over the still image rather than baked into it.
            DrawMenuScene(totalSeconds);
        }

        _spriteBatch.Begin(transformMatrix: _renderScale);
        if (_menuScreen == MenuScreen.Nickname)
            DrawNicknameScreen();
        else if (_menuScreen == MenuScreen.Role)
            DrawRoleScreen();
        else if (_menuScreen == MenuScreen.Main)
            DrawMainMenuScreen(totalSeconds);
        else if (_menuScreen == MenuScreen.ShipSelect)
            DrawShipSelectScreen();
        else if (_menuScreen == MenuScreen.CreateServer)
            DrawCreateServerScreen();
        else if (_menuScreen == MenuScreen.Lobby)
            DrawLobbyScreen();
        else if (_menuScreen == MenuScreen.Prologue)
            DrawPrologueScreen(totalSeconds);
        else if (_menuScreen == MenuScreen.ShipEditor)
            DrawShipEditorScreen();
        else if (_menuScreen == MenuScreen.Credits)
            DrawCreditsScreen(totalSeconds);
        else if (_menuScreen == MenuScreen.Settings)
            DrawSettingsScreen(totalSeconds);
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

        // The plates arrive first, on the same stagger the buttons use - a heading that appears after
        // the things under it has the order backwards.
        for (var i = 0; i < MainMenuSections.Length; i++)
        {
            var (section, box) = MainMenuSections[i];
            var plateProgress = MainMenuButtonProgress(sinceEnter, i);
            MenuSectionIcons.Draw(_spriteBatch, _pixel, section, box,
                new Color(90, 220, 195) * (0.85f * plateProgress), totalSeconds);
        }

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
    // Amber, and only here. The reference screen gets its punch from having exactly one saturated
    // colour in the whole frame, appearing four or five times against cold near-black. Spend it
    // anywhere else and it stops being an accent.
    private static readonly Color MenuAmber = new(196, 116, 16);
    private static readonly Color MenuAmberInk = new(16, 18, 17);

    private void DrawMainMenuButton(Rectangle rect, string label, bool enabled, float progress, MainMenuIcon icon, float totalSeconds)
    {
        var slide = (int)((1f - progress) * 14f);
        var drawRect = new Rectangle(rect.X, rect.Y + slide, rect.Width, rect.Height);
        var hovered = enabled && drawRect.Contains(_designMouse);
        // Held down over the button: the bar sinks a pixel, so a click has a physical answer instead
        // of the label simply changing colour on release.
        var held = hovered && Mouse.GetState().LeftButton == ButtonState.Pressed;
        if (held)
            drawRect = new Rectangle(drawRect.X + 1, drawRect.Y + 1, drawRect.Width, drawRect.Height);

        var labelWidth = _font.MeasureString(label).X * 0.5f;

        // No plate and no outline when idle. A box around every item is the thing that makes a menu
        // look like a form; without it the items sit on the surface, which is the whole point of
        // having given the surface a material.
        //
        // The bar is sized to the label rather than to the button. Stretched to the full hit area it
        // left the longer labels running off its chewed end into the dark, which reads as clipping;
        // hugging the text is also what the reference does. The button's hit area is untouched.
        if (hovered)
        {
            // Not clamped to the button. "ПРОДОЛЖИТЬ (Корвет, 300 кред.)" is already wider than its
            // own hit area - it was overflowing before any of this, the old border just made it look
            // deliberate - and a bar that stops short of its own label reads as clipping. Drawing
            // wider changes nothing about where the button is or what it catches.
            var barWidth = (int)(drawRect.Height + 4 + labelWidth) + 14;
            DrawAmberBar(new Rectangle(drawRect.X, drawRect.Y, Math.Max(drawRect.Width, barWidth), drawRect.Height),
                progress, held, totalSeconds);
        }

        var accent = !enabled ? new Color(78, 84, 84)
            : hovered ? MenuAmberInk
            : new Color(90, 220, 195);

        var iconBoxSize = drawRect.Height - 4;
        var iconBox = new Rectangle(drawRect.X + 4, drawRect.Y + (drawRect.Height - iconBoxSize) / 2, iconBoxSize, iconBoxSize);
        DrawMenuIconTile(iconBox, accent, progress, hovered);
        DrawMainMenuButtonIcon(icon, iconBox, accent * progress);

        var textColor = (!enabled ? new Color(96, 102, 102)
            : hovered ? MenuAmberInk
            : new Color(206, 214, 212)) * progress;
        var textSize = _font.MeasureString(label) * 0.5f;
        var textPos = new Vector2(iconBox.Right + 8, drawRect.Center.Y - textSize.Y / 2f);
        _spriteBatch.DrawString(_font, label, textPos, textColor, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    // The selected item, as a solid bar whose right end has been eaten away.
    //
    // The ragged end is the signature, and it has to be deterministic per row or the edge crawls
    // every frame - which reads as a rendering fault, not as corrosion. A hash of the row does that
    // and costs nothing to keep.
    private void DrawAmberBar(Rectangle bar, float progress, bool held, float totalSeconds)
    {
        var colour = MenuAmber * progress;
        const int chew = 22;
        for (var y = bar.Y; y < bar.Bottom; y++)
        {
            var noise = MenuEdgeNoise(y);
            // Squared, so most rows are barely bitten and a few are bitten deeply. A uniform spread
            // gives a fuzzy edge; this gives a broken one.
            var bite = (int)(chew * noise * noise);
            _spriteBatch.Draw(_pixel, new Rectangle(bar.X, y, Math.Max(4, bar.Width - bite), 1), colour);
        }

        // Flecks that have come off the end entirely. Three or four is enough to say the edge is
        // damage rather than a shape.
        for (var i = 0; i < 5; i++)
        {
            var n = MenuEdgeNoise(bar.Y * 31 + i * 17);
            if (n < 0.45f)
                continue;
            var fx = bar.Right - (int)(chew * 0.4f) + (int)(n * 16f);
            var fy = bar.Y + (int)(MenuEdgeNoise(i * 977 + bar.Y) * (bar.Height - 2));
            _spriteBatch.Draw(_pixel, new Rectangle(fx, fy, 1 + (int)(n * 2f), 1 + (int)(n * 2f)), colour);
        }

        // The sweep survives, dimmed: on a bright bar it only has to suggest a scan, and at the old
        // strength it fought the amber instead of riding over it.
        var sweep = bar.X + (totalSeconds * 0.85f % 1f) * bar.Width;
        for (var i = -7; i <= 7; i++)
        {
            var x = (int)sweep + i;
            if (x < bar.X || x >= bar.Right - 6)
                continue;
            var falloff = (1f - MathF.Abs(i) / 8f) * 0.10f;
            _spriteBatch.Draw(_pixel, new Rectangle(x, bar.Y + 1, 1, bar.Height - 2), Color.White * falloff * progress);
        }
    }

    private static float MenuEdgeNoise(int n)
    {
        var h = (uint)(n * 374761393 + 668265263);
        h ^= h >> 13;
        h *= 1274126177u;
        return ((h ^ (h >> 16)) & 0xffff) / 65535f;
    }

    // The icon square, drawn as a plate off a schematic rather than a border: a thin frame, a hint of
    // fill so it is a surface and not a hole, and tick marks along the bottom edge - the small
    // draughting detail that makes a box read as an instrument panel.
    private void DrawMenuIconTile(Rectangle box, Color accent, float progress, bool hovered)
    {
        if (!hovered)
            _spriteBatch.Draw(_pixel, box, new Color(24, 46, 44) * (0.55f * progress));
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, box, accent * (progress * 0.75f), 1);

        const int ticks = 5;
        for (var i = 0; i < ticks; i++)
        {
            var x = box.X + 3 + (int)((box.Width - 6) * (i / (float)(ticks - 1)));
            var height = i % 2 == 0 ? 3 : 2;
            _spriteBatch.Draw(_pixel, new Rectangle(x, box.Bottom + 1, 1, height), accent * (progress * 0.5f));
        }
    }

    // The left panel's own material - painted rather than filled. A flat fill is exactly what an
    // interface looks like; a surface with marks on it is a place that happens to have an interface
    // sitting on it. MenuPlateTexture lays the strokes and bakes the result once.
    //
    // There is also no frame and no edge strip. A box says "widget", and the reference this is
    // chasing has no box at all - only a surface with the items lying directly on it.
    private Texture2D? _menuPlate;

    private void DrawMainMenuPanelPlate(float totalSeconds)
    {
        var panelRect = new Rectangle(0, 0, MenuPaneX, DesignHeight);
        _menuPlate ??= MenuPlateTexture.Create(GraphicsDevice, MenuPaneX, DesignHeight);
        _spriteBatch.Draw(_menuPlate, panelRect, Color.White);

        // The blueprint grid stays, but barely. On a flat fill it was the only texture there was and
        // had to carry the panel; over a surface with its own grain it only has to be a hint that
        // something was drafted here.
        const int cell = 28;
        var gridColor = new Color(90, 220, 195) * 0.025f;
        for (var x = panelRect.X; x < panelRect.Right; x += cell)
            _spriteBatch.Draw(_pixel, new Rectangle(x, panelRect.Y, 1, panelRect.Height), gridColor);
        for (var y = panelRect.Y; y < panelRect.Bottom; y += cell)
            _spriteBatch.Draw(_pixel, new Rectangle(panelRect.X, y, panelRect.Width, 1), gridColor);

        DrawMenuSeam(panelRect, totalSeconds);
    }

    // The join between the plate and the art behind it.
    //
    // It was a ruler-straight line with a soft shadow bled over the art - honest, and the dullest
    // thing on the screen. A cut edge is never straight, and an edge with nothing on it has no scale:
    // there is no way to tell whether the plate is a millimetre thick or a hand's width until
    // something sits on it. So the line is torn, the cut catches light, bolts hold the plate down,
    // and three brackets straddle the edge - the brackets matter most, because a line the eye can
    // follow uninterrupted from top to bottom reads as a border no matter what shape it is.
    private void DrawMenuSeam(Rectangle panelRect, float totalSeconds)
    {
        // The shadow first, under everything, and still straight: it falls on the art from a plate
        // whose edge is only a few pixels ragged, and at this distance that raggedness would not show
        // in a shadow anyway.
        const int bleed = 26;
        for (var i = 0; i < bleed; i++)
        {
            var fade = 1f - i / (float)bleed;
            _spriteBatch.Draw(_pixel, new Rectangle(panelRect.Right + i, 0, 1, DesignHeight),
                new Color(4, 7, 8) * (fade * fade * 0.85f));
        }

        // The tear, drawn in runs rather than per row. Rows that share a tear depth are contiguous
        // because the noise sits on a lattice, so merging them turns five hundred draws into a few
        // dozen for exactly the same picture.
        var voidColour = new Color(3, 5, 7);
        var lipColour = new Color(122, 158, 168) * 0.42f;
        var runStart = 0;
        var runTear = SeamTear(0);
        for (var y = 1; y <= DesignHeight; y++)
        {
            var tear = y < DesignHeight ? SeamTear(y) : -1;
            if (tear == runTear)
                continue;

            var height = y - runStart;
            if (runTear > 0)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(panelRect.Right - runTear, runStart, runTear, height), voidColour);
                // The inner face of the cut, one pixel of it. A bite that is flat black is a hole in
                // a sheet of paper; a bite with a wall inside it is a hole in something that has
                // thickness, and thickness is the whole reason to have an edge at all.
                if (runTear > 4)
                    _spriteBatch.Draw(_pixel, new Rectangle(panelRect.Right - runTear + 1, runStart, 1, height),
                        new Color(30, 38, 42) * 0.7f);
            }
            // The lit edge of the cut - but only where the cut actually faces the light. Running it
            // at full strength down the whole edge was the other half of what made this read as a
            // drawn line: a continuous highlight is a stroke, while metal glints in short pieces
            // where a facet happens to turn upward. The edge is near-vertical, so its normal points
            // away from the light almost everywhere; it catches light only where the tear steps
            // outward, which is the top lip of each bite.
            var slope = runTear - SeamTear(Math.Max(0, runStart - 4));
            var glint = MathHelper.Clamp(slope / 4f, 0f, 1f);
            _spriteBatch.Draw(_pixel, new Rectangle(panelRect.Right - runTear - 1, runStart, 1, height),
                lipColour * (0.18f + glint * 0.82f));

            runStart = y;
            runTear = tear;
        }

        // Rust, running down from each bolt. Drawn before the bolts so the head sits on top of its
        // own stain, and this is what ties the hardware to the surface: a bolt with nothing bleeding
        // out of it looks laid on the panel rather than driven into it.
        const int bolts = 9;
        for (var i = 0; i < bolts; i++)
        {
            var y = (int)((i + 0.5f) / bolts * DesignHeight);
            var x = panelRect.Right - 15 - (int)(SeamNoise(i * 37, 1) * 3f);
            var runLength = 9 + (int)(SeamNoise(i * 91, 1) * 17f);
            for (var d = 0; d < runLength; d++)
            {
                var fade = (1f - d / (float)runLength);
                var wide = d < 3 ? 3 : 2;
                _spriteBatch.Draw(_pixel, new Rectangle(x, y + 3 + d, wide, 1),
                    new Color(96, 58, 30) * (fade * fade * 0.38f));
            }
        }

        // The cable run. The edge of a plate is where wiring goes, and giving the join a job is worth
        // more than any amount of extra dirt on it. It sags between its clamps, because a cable that
        // runs dead straight is a drawn line and not a cable.
        var clampYs = new[] { 24, 132, 256, 372, 494 };
        var cableX = panelRect.Right - 24;
        for (var i = 0; i < clampYs.Length - 1; i++)
        {
            var y0 = clampYs[i];
            var y1 = clampYs[i + 1];
            for (var y = y0; y < y1; y++)
            {
                var t = (y - y0) / (float)(y1 - y0);
                var sag = MathF.Sin(t * MathF.PI) * (4f + SeamNoise(i * 53, 1) * 3f);
                var x = cableX - (int)sag;
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, 3, 1), new Color(10, 12, 15));
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, 1), new Color(64, 80, 88) * 0.26f);
            }
        }
        foreach (var y in clampYs)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(cableX - 2, y - 2, 7, 5), new Color(31, 39, 43));
            _spriteBatch.Draw(_pixel, new Rectangle(cableX - 2, y - 2, 7, 1), new Color(104, 132, 140) * 0.28f);
        }

        // Bolt heads last, over their stains.
        for (var i = 0; i < bolts; i++)
        {
            var y = (int)((i + 0.5f) / bolts * DesignHeight);
            var x = panelRect.Right - 15 - (int)(SeamNoise(i * 37, 1) * 3f);
            _spriteBatch.Draw(_pixel, new Rectangle(x - 1, y - 1, 5, 5), new Color(8, 11, 13) * 0.7f);
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 3, 3), new Color(34, 43, 48));
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 2, 1), new Color(118, 148, 158) * 0.75f);
            _spriteBatch.Draw(_pixel, new Rectangle(x, y + 1, 1, 1), new Color(96, 122, 130) * 0.35f);
        }

        // Three brackets across the join. Deliberately unevenly spaced: at equal intervals they read
        // as a repeating decoration rather than as hardware someone bolted on where it was needed.
        // Each one is given a different job, so the eye is not looking at the same object three times.
        var brackets = new[] { 86, 271, 448 };
        for (var b = 0; b < brackets.Length; b++)
        {
            var y = brackets[b];
            var plate = new Rectangle(panelRect.Right - 10, y, 24, 9);
            _spriteBatch.Draw(_pixel, plate, new Color(26, 33, 37));

            // Hazard stripes on the middle one. A stencil is the strongest industrial cue there is
            // and it costs a loop - but only one of the three gets it, because a marking repeated on
            // every bracket stops being a marking and becomes a pattern.
            if (b == 1)
            {
                for (var i = 0; i < plate.Width + plate.Height; i += 6)
                {
                    for (var k = 0; k < 3; k++)
                    {
                        var sx = plate.X + i - k;
                        var sy = plate.Y + k;
                        if (sx < plate.X || sx >= plate.Right || sy >= plate.Bottom)
                            continue;
                        _spriteBatch.Draw(_pixel, new Rectangle(sx, sy, 1, 3), new Color(150, 104, 24) * 0.55f);
                    }
                }
            }

            _spriteBatch.Draw(_pixel, new Rectangle(plate.X, plate.Y, plate.Width, 1), new Color(104, 132, 140) * 0.45f);
            _spriteBatch.Draw(_pixel, new Rectangle(plate.X, plate.Bottom - 1, plate.Width, 1), new Color(6, 9, 11));
            _spriteBatch.Draw(_pixel, new Rectangle(plate.X + 3, plate.Y + 3, 2, 2), new Color(8, 11, 13));
            _spriteBatch.Draw(_pixel, new Rectangle(plate.Right - 5, plate.Y + 3, 2, 2), new Color(8, 11, 13));

            // One indicator, on the top bracket only. The single live thing on the whole join: a slow
            // breath rather than a blink, because a blinking light demands attention and this is
            // meant to be noticed on the second look, not the first.
            if (b != 0)
                continue;
            var pulse = 0.35f + 0.65f * (0.5f + 0.5f * MathF.Sin(totalSeconds * 1.1f));
            _spriteBatch.Draw(_pixel, new Rectangle(plate.X + 11, plate.Y + 3, 2, 2), new Color(120, 220, 170) * pulse);
        }
    }

    // How deep the tear bites into the plate at this row.
    //
    // The first version summed two lattices and got a wobble of near-constant amplitude all the way
    // down - which is a sine, not damage, and read as a squiggle drawn over the screen. Torn metal is
    // the opposite shape: long stretches that are almost straight, punctuated by a few deep bites.
    // Cubing the coarse term is what produces that - it pushes most rows towards nothing and leaves
    // the occasional one high - and the fine term only roughens what is already there.
    private static int SeamTear(int y)
    {
        var coarse = SeamNoise(y, 61);
        var bite = coarse * coarse * coarse * 15f;
        return (int)(bite + SeamNoise(y + 500, 6) * 1.6f);
    }

    private static float SeamNoise(int y, int cell)
    {
        var i0 = y / cell;
        var t = (y - i0 * cell) / (float)cell;
        t = t * t * (3f - 2f * t);
        return Hash01(i0) * (1f - t) + Hash01(i0 + 1) * t;
    }

    private static float Hash01(int n)
    {
        var h = (uint)(n * 374761393 + 668265263);
        h ^= h >> 13;
        h *= 1274126177u;
        return ((h ^ (h >> 16)) & 0xffff) / 65535f;
    }

    // Right-hand art pane - a planet on a slow orbit with the player's own ship circling it
    // (MenuPlanetScene), standing in for the reference screenshot's submarine photo since there
    // are no image assets anywhere in this project.
    private void DrawMainMenuBackdrop(float totalSeconds)
    {
        var pane = new Rectangle(MenuPaneX, 0, DesignWidth - MenuPaneX, DesignHeight);
        // Only when there is no backdrop image. The two would fight: the procedural scene paints its
        // own starfield and planet straight over the art.
        if (_menuBackdrop is null)
            MenuPlanetScene.Draw(_spriteBatch, _pixel, pane, totalSeconds);

        var glow = new Color(90, 220, 195);

        // The wordmark, drawn rather than typed. It used to be the debug spritefont at scale 1.7
        // under a cyan glow with chromatic fringing - a lot of dressing on what was still a UI font
        // stretched up, and it read as one. MenuLogo bakes the letters as plate.
        //
        // Width is fixed rather than measured: the mark is one image of one word, so there is
        // nothing to measure against, and the rule and tagline below hang off the same left edge.
        const int logoWidth = 360;
        var blockRight = pane.Right - 110;
        var blockLeft = MathF.Min(pane.X + 360, blockRight - logoWidth);

        var logo = MenuLogo.Get(GraphicsDevice);
        var logoHeight = (int)(logo.Height * (logoWidth / (float)logo.Width));
        // Anchored by the letters, not by the texture: the pupa on the seam overruns the cap line
        // and the baseline, so the image is taller than the word and lining the two up by their
        // tops would hang the whole mark too low.
        // Direct user request ("сдвинь голубую линию и название игры ниже") - the tagline that used
        // to sit right under the rule is gone now, so both the logo and rule shift down by that same
        // gap to fill the space instead of leaving it empty above the ticker.
        const int loweredByRemovedTagline = 18;
        var lettersTop = pane.Bottom - 122 + loweredByRemovedTagline;
        var logoTop = lettersTop - (int)(MenuLogo.LetterInset * (logoWidth / (float)logo.Width));
        _spriteBatch.Draw(logo, new Rectangle((int)blockLeft, logoTop, logoWidth, logoHeight), Color.White);

        // A riveted rule under the title, same corner-rivet dressing every device housing already
        // wears (ShipRenderer.DrawRivets) - ties the front screen to the game it opens into.
        var ruleY = (float)(pane.Bottom - 46 + loweredByRemovedTagline);
        var ruleRect = new Rectangle((int)blockLeft, (int)ruleY, pane.Right - (int)blockLeft - 20, 2);
        _spriteBatch.Draw(_pixel, ruleRect, glow * 0.6f);
        for (var x = ruleRect.X + 6; x < ruleRect.Right; x += 24)
            HudIcons.FillCircle(_spriteBatch, _pixel, new Vector2(x, ruleY + 1), 1.4f, new Color(20, 24, 22));

        DrawTrafficTicker(pane, totalSeconds);
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
        "ДИСПЕТЧЕР: ОЧЕРЕДЬ НА ШЛЮЗ 2 - ОЖИДАНИЕ ОКОЛО 20 МИНУТ",
        "БОРТ 'ПОЛЫНЬ' ЗАПРАШИВАЕТ ПРИОРИТЕТНУЮ ЗАПРАВКУ",
        "СТАНЦИЯ: ЦЕНЫ НА РУДУ ОБНОВЛЕНЫ, СМ. ТЕРМИНАЛ ТОРГОВЦА",
        "ВНИМАНИЕ: В СЕКТОРЕ ЗАФИКСИРОВАНО ПОВЫШЕННОЕ ГРАВИТАЦИОННОЕ ВОЗМУЩЕНИЕ",
        "ОТДЕЛ КАДРОВ: ВАКАНСИИ МЕХАНИКА И МЕДИКА ОТКРЫТЫ ДО КОНЦА СМЕНЫ",
        "ПАТРУЛЬ СООБЩАЕТ О ЧИСТОМ КОРИДОРЕ В КВАДРАНТЕ 9",
        "СКЛАД: ПОСТАВКА ЗАПАСНЫХ ЩИТКОВ ЗАДЕРЖИВАЕТСЯ НА СУТКИ",
        "НАПОМИНАНИЕ: НЕСАНКЦИОНИРОВАННАЯ СВАРКА В ШЛЮЗОВОЙ ЗАПРЕЩЕНА",
        "БОРТ 'ЗАРЯ' ДОКЛАДЫВАЕТ О НЕЗНАЧИТЕЛЬНОЙ ПРОБОИНЕ, УГРОЗЫ НЕТ",
        "ДИСПЕТЧЕР: ВНЕШНИЙ ПРИЧАЛ 7 ВРЕМЕННО НЕДОСТУПЕН ДЛЯ ШВАРТОВКИ",
        "ВНИМАНИЕ: В ПОЯСЕ АСТЕРОИДОВ ЗАМЕЧЕНА ПОВЫШЕННАЯ АКТИВНОСТЬ ДОБЫТЧИКОВ",
        "АДМИНИСТРАЦИЯ: НОВЫЕ ГРУЗОВЫЕ ЗАКАЗЫ ДОСТУПНЫ НА ДОСКЕ ОБЪЯВЛЕНИЙ",
        "РЕАКТОРНЫЙ ОТСЕК: ПЛАНОВАЯ ПРОВЕРКА ТОПЛИВНЫХ СТЕРЖНЕЙ ЗАВЕРШЕНА",
        "БОРТ 'ГОРИЗОНТ' БЛАГОДАРИТ ЗА ПОМОЩЬ ПРИ ШВАРТОВКЕ",
        "ВНИМАНИЕ: РАДИАЦИОННЫЙ ФОН В НОРМЕ, ДАТЧИКИ ПРОВЕРЕНЫ",
        "СТАНЦИЯ: ПОТЕРЯННЫЙ ГРУЗ С БОРТА 'ВЕГА' ЖДЁТ ВЛАДЕЛЬЦА НА СКЛАДЕ",
        "ДИСПЕТЧЕР: ВНИМАНИЕ ЭКИПАЖАМ - УЧЕБНАЯ ТРЕВОГА В 14:00 ПО СТАНЦИОННОМУ",
        "НАПОМИНАНИЕ: ПРОСРОЧЕННЫЕ АПТЕЧКИ МЕНЯЮТСЯ В МЕДОТСЕКЕ БЕСПЛАТНО",
        "БОРТ 'СКИТАЛЕЦ' ЗАПРАШИВАЕТ РАЗРЕШЕНИЕ НА ВЫХОД В ОТКРЫТЫЙ КОСМОС",
        "СЛУЖБА БЕЗОПАСНОСТИ: НЕОПЛАЧЕННЫЕ ДОЛГИ ПЕРЕДАЮТСЯ ВЗЫСКАТЕЛЯМ",
        "БОРТ 'НАБАТ' ДОКЛАДЫВАЕТ О ПОЛНОЙ ГОТОВНОСТИ К ОТБЫТИЮ",
        "ДИСПЕТЧЕР: ПРИЧАЛ 3 ОСВОБОДИЛСЯ, ОЧЕРЕДЬ СДВИНУЛАСЬ НА ОДНОГО",
        "ВНИМАНИЕ: КОРОТКОЕ ЗАМЫКАНИЕ НА ТОРГОВОЙ ПЛОЩАДКЕ, ОСВЕЩЕНИЕ ВРЕМЕННОЕ",
        "СТАНЦИЯ: ЛОТ РЕДКОГО МИНЕРАЛА ВЫСТАВЛЕН НА ТЕРМИНАЛЕ ТОРГОВЦА",
        "НАПОМИНАНИЕ: КАРТОЧКА ЭКИПАЖА ОБЯЗАТЕЛЬНА ПРИ ПРОХОДЕ ЧЕРЕЗ ШЛЮЗ",
        "БОРТ 'ЗАТИШЬЕ' СООБЩАЕТ О СТОЛКНОВЕНИИ С МЕЛКИМ МУСОРОМ, ПОВРЕЖДЕНИЙ НЕТ",
        "ОТДЕЛ КАДРОВ: РЕКРУТЁР ПРИНИМАЕТ ЗАЯВКИ НА СВОБОДНЫЕ МЕСТА В ЭКИПАЖЕ",
        "ВНИМАНИЕ: ОКНО СВЯЗИ С ЦЕНТРОМ УПРАВЛЕНИЯ ЗАКРЫВАЕТСЯ ЧЕРЕЗ ЧАС",
        "ПАТРУЛЬ ЗАПРАШИВАЕТ ПОДКРЕПЛЕНИЕ В КВАДРАНТЕ 4, УГРОЗА НЕВЫСОКАЯ",
        "СКЛАД: ЛИШНИЕ ОБРЕЗКИ ОБШИВКИ ПРИНИМАЮТСЯ НА ПЕРЕПЛАВКУ",
        "БОРТ 'ПОЛУНОЧНИК' БЛАГОДАРИТ ДИСПЕТЧЕРА ЗА ТОЧНЫЙ КОРИДОР ЗАХОДА",
        "НАПОМИНАНИЕ: ЛИЧНОЕ ОРУЖИЕ ХРАНИТЬ РАЗРЯЖЕННЫМ ВНЕ БОЕВОЙ ОБСТАНОВКИ",
        "ВНИМАНИЕ: ПЫЛЕВОЕ ОБЛАКО СНИЖАЕТ ВИДИМОСТЬ НА ВНЕШНИХ КАМЕРАХ",
        "АДМИНИСТРАЦИЯ: ПРЕМИЯ ЗА СДАННЫЙ ГРУЗ НАЧИСЛЕНА НА СЧЁТ СТАНЦИИ",
        "БОРТ 'ШИРОТА' ЗАПРАШИВАЕТ ТЕХНИЧЕСКУЮ ПОМОЩЬ С ДВИГАТЕЛЕМ",
        "ДИСПЕТЧЕР: ВНЕШНИЙ КОНТУР ОСВЕЩЕНИЯ СТАНЦИИ ПЕРЕВЕДЁН В НОЧНОЙ РЕЖИМ",
        "МЕДОТСЕК: ЗАПАС ОБЕЗБОЛИВАЮЩИХ ВОСПОЛНЕН ДО ПОЛНОЙ НОРМЫ",
        "ВНИМАНИЕ: ПРОБНАЯ ТРАНСЛЯЦИЯ АВАРИЙНОЙ ЧАСТОТЫ В 18:00, ЭТО УЧЕНИЯ",
        "СТАНЦИЯ: НАЙДЕННЫЕ ЛИЧНЫЕ ВЕЩИ СДАЮТСЯ НА ПОСТ ДИСПЕТЧЕРА",
        "АМОГУС!",
    };

    private const float TickerSpeed = 34f; // pixels/second, unchanged from before
    // The empty space kept between one line's trailing edge and the next line's leading edge -
    // constant regardless of either line's own width, which is exactly what spacing spawns a fixed
    // time apart couldn't guarantee (a long line's tail could still be on screen when a short gap
    // let the next one already catch up to it, i.e. the overlap the old timing produced).
    private const float TickerLineGap = 80f;

    // Per-line width in pixels, indexed exactly like TrafficLines - lazily built from the real font
    // metrics the first time this draws, then reused every frame after. The lap length is just the
    // sum of these plus one gap per line, which holds regardless of what order they play in within
    // a lap - only ShuffleLap below cares about order.
    private float[]? _trafficLineWidths;
    private float _trafficCycleLength;

    private void EnsureTrafficLineWidths()
    {
        if (_trafficLineWidths is not null)
            return;

        _trafficLineWidths = new float[TrafficLines.Length];
        var total = 0f;
        for (var i = 0; i < TrafficLines.Length; i++)
        {
            _trafficLineWidths[i] = _font.MeasureString(TrafficLines[i]).X * 0.5f;
            total += _trafficLineWidths[i] + TickerLineGap;
        }
        _trafficCycleLength = total;
    }

    // Every full lap through TrafficLines plays in its own random order (a fresh shuffle seeded by
    // the lap number, so it's deterministic within a frame but different lap to lap) - reusing the
    // same fixed order every time was exactly what read as repetitive, and a lap boundary is a
    // natural point to reshuffle since nothing is ever mid-line there. Returns both the shuffled
    // line order and each entry's cumulative start offset within this lap (same running-sum shape
    // the old fixed-order table had, just built for whichever order this particular lap drew).
    private (int[] Order, float[] Offsets) ShuffleLap(int lap)
    {
        var count = TrafficLines.Length;
        var order = new int[count];
        for (var i = 0; i < count; i++)
            order[i] = i;

        var rng = new Random(lap);
        for (var i = count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var offsets = new float[count];
        var offset = 0f;
        for (var i = 0; i < count; i++)
        {
            offsets[i] = offset;
            offset += _trafficLineWidths![order[i]] + TickerLineGap;
        }
        return (order, offsets);
    }

    private void DrawTrafficTicker(Rectangle pane, float totalSeconds)
    {
        EnsureTrafficLineWidths();
        var count = TrafficLines.Length;

        var y = pane.Bottom - 16f;
        _spriteBatch.Draw(_pixel, new Rectangle(pane.X, (int)y - 3, pane.Width, 14), new Color(8, 14, 18) * 0.55f);
        _spriteBatch.Draw(_pixel, new Rectangle(pane.X, (int)y - 4, pane.Width, 1), new Color(90, 220, 195) * 0.25f);

        // Every line's own start distance is lap * cycleLength + that lap's own offset for it -
        // strictly increasing within a lap (however it got shuffled) and across the lap boundary
        // too, so walking backward through a lap's slots, then into the previous lap's once this
        // one runs out, still makes age increase monotonically - the moment one line is fully off
        // the left edge, every older one (this lap's remainder, or the whole lap before it) is too.
        var traveled = totalSeconds * TickerSpeed;
        var lap = (int)MathF.Floor(traveled / _trafficCycleLength);
        var remainder = traveled - lap * _trafficCycleLength;

        var (order, offsets) = ShuffleLap(lap);
        var index = 0;
        for (var i = count - 1; i >= 0; i--)
        {
            if (offsets[i] <= remainder)
            {
                index = i;
                break;
            }
        }

        while (true)
        {
            var lineIndex = order[index];
            var spawnDistance = lap * _trafficCycleLength + offsets[index];
            var line = TrafficLines[lineIndex];
            var width = _trafficLineWidths![lineIndex];
            var age = traveled - spawnDistance;
            var x = pane.Right - age;
            if (x + width < pane.X)
                break;

            _spriteBatch.DrawString(_font, line, new Vector2(x, y), new Color(120, 200, 185) * 0.75f, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

            index--;
            if (index < 0)
            {
                lap--;
                (order, offsets) = ShuffleLap(lap);
                index = count - 1;
            }
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
        _spriteBatch.DrawString(_font, "Выбирается один раз на кампанию и потом не меняется. Ограничений на действия не даёт.",
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

        _spriteBatch.DrawString(_font, "Нажмите на роль, чтобы начать", new Vector2(60, 380), Color.LightSteelBlue, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
    }

    private void DrawShipSelectScreen()
    {
        _spriteBatch.DrawString(_font, "Выберите корабль", new Vector2(60, 40), Color.White, 0f, Vector2.Zero, 1.4f, SpriteEffects.None, 0f);

        // Direct user request ("удали все текущие корабли в разделе начать новую игру... полностью
        // удалить из кода") - the fixed 6-class list that used to fill this left column (keyboard
        // keys 1-6) is gone; DrawShipSelectCustomShipList's own column is untouched and is now the
        // only way to pick a hull here.
        DrawShipSelectCustomShipList();

        // Moved back up now that the fixed class list above them is gone (used to sit at
        // 424/448/482/508, tuned to clear that list's own last description line).
        if (_existingSave is { } save)
        {
            _spriteBatch.DrawString(_font, $"[C] Продолжить: {ShipCatalog.Name(save.ShipKind)}, {save.Credits} кред.",
                new Vector2(60, 140), Color.LightGreen, 0f, Vector2.Zero, 1.0f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, "Выбор корабля начнёт новую игру и сотрёт сохранение.",
                new Vector2(80, 164), Color.Gray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
        }

        // The player cap only shows up here when it's actually enforced (Начать on "Создать
        // сервер" set _hostMaxPlayers for real) - the quick [H]-toggle path below it never sets
        // it, so showing a stale "4 мест" while hosting is genuinely uncapped would be a lie.
        var capSuffix = _hostMaxPlayers < int.MaxValue ? $" — «{_createServerName}» ({_hostMaxPlayers} мест)" : "";
        var hostLine = _openToNetwork
            ? $"[H] Кооп: ОТКРЫТ{capSuffix}, порт {Anabiosis.Shared.Networking.Wire.DefaultPort} — друзья вводят {LocalAddresses()}"
            : "[H] Кооп: закрыт (игра только для вас)";
        _spriteBatch.DrawString(_font, hostLine, new Vector2(60, 198),
            _openToNetwork ? Color.LightGreen : Color.LightGray, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, "[J] Присоединиться к чужому кораблю", new Vector2(60, 224),
            Color.LightSkyBlue, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
    }

    // Редактор корабля в духе Cosmoteer + несколько сохранённых кораблей (humble-soaring-cat.md,
    // Step 7) - the only hull-selection list on this screen now (direct user request, "удали все
    // текущие корабли... полностью удалить из кода" removed the 6 fixed classes that used to sit
    // to its left). An invalid design still shows up here (so the player can see it exists and go
    // fix it in the editor) but greyed out and unclickable, rather than hidden entirely.
    private void DrawShipSelectCustomShipList()
    {
        _spriteBatch.DrawString(_font, "Ваши корабли:", new Vector2(650, 80), Color.White, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);

        // Direct user request ("отображались только те, на которых реально можно поиграть") - an
        // unfinished/broken design (CustomShipValidator.Validate finds at least one missing
        // required system) is filtered out entirely rather than shown dimmed and unclickable.
        var names = PlayableCustomShipNames();
        if (names.Count == 0)
        {
            _spriteBatch.DrawString(_font, "(соберите свой в редакторе корабля)", new Vector2(650, 110), Color.Gray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            return;
        }

        for (var i = 0; i < names.Count; i++)
        {
            var rect = GetShipSelectCustomRowRect(i);
            var hovered = rect.Contains(_designMouse);
            _spriteBatch.Draw(_pixel, rect, hovered ? new Color(120, 92, 30) : Color.DimGray * 0.5f);
            _spriteBatch.DrawString(_font, names[i], new Vector2(rect.X + 8, rect.Y + 4),
                Color.Gold, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        }
    }

    private void DrawJoinScreen()
    {
        _spriteBatch.DrawString(_font, "Подключение к кораблю", new Vector2(60, 60), Color.White, 0f, Vector2.Zero, 1.4f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, "Адрес хоста (можно адрес:порт):", new Vector2(60, 140), Color.LightGray, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, _joinAddress + "_", new Vector2(60, 178), Color.Gold, 0f, Vector2.Zero, 1.2f, SpriteEffects.None, 0f);

        _spriteBatch.DrawString(_font, "[Enter] подключиться    [Esc] назад", new Vector2(60, 250), Color.LightSteelBlue, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, $"Хост должен включить кооп ([H] в меню) и открыть порт {Anabiosis.Shared.Networking.Wire.DefaultPort}.",
            new Vector2(60, 280), Color.Gray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        if (_joinError is { } error)
            _spriteBatch.DrawString(_font, $"Не удалось: {error}", new Vector2(60, 330), Color.OrangeRed, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

        // Direct user request (2nd of the 8 reference screenshots) - a small centered modal while
        // the handshake is in flight, instead of the address line above just growing "— подключение…"
        // text onto itself in place.
        if (_joinTask is not null)
            DrawJoinConnectingPopup();
    }

    private static Rectangle GetJoinConnectingPopupRect()
    {
        const int width = 380, height = 130;
        return new Rectangle((DesignWidth - width) / 2, (DesignHeight - height) / 2, width, height);
    }

    private void DrawJoinConnectingPopup()
    {
        var rect = GetJoinConnectingPopupRect();
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X - 6, rect.Y - 6, rect.Width + 12, rect.Height + 12), Color.Black * 0.5f);
        DrawVerticalGradient(rect, SettingsPanelTop, SettingsPanelBottom);
        ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, rect, SettingsBorderGold * 0.7f, 2);

        var titleSize = _font.MeasureString("Подключение") * 0.75f;
        _spriteBatch.DrawString(_font, "Подключение", new Vector2(rect.Center.X - titleSize.X / 2f, rect.Y + 22),
            SettingsAccentGold, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);

        var addressSize = _font.MeasureString(_joinAddress) * 0.6f;
        _spriteBatch.DrawString(_font, _joinAddress, new Vector2(rect.Center.X - addressSize.X / 2f, rect.Y + 58),
            SettingsTextPrimary, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        var waitSize = _font.MeasureString("Ожидание ответа хоста…") * 0.5f;
        _spriteBatch.DrawString(_font, "Ожидание ответа хоста…", new Vector2(rect.Center.X - waitSize.X / 2f, rect.Y + 90),
            SettingsTextDim, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }
}
