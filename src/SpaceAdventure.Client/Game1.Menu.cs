using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceAdventure.Client.Networking;
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
        ShipSelect,
        Join,
    }

    private static readonly ShipKind[] SelectableShipKinds = { ShipKind.Scout, ShipKind.Frigate, ShipKind.Cruiser, ShipKind.Corvette };

    private MenuScreen _menuScreen = MenuScreen.ShipSelect;
    private bool _openToNetwork;
    private string _joinAddress = "127.0.0.1";
    private string? _joinError;
    // The join handshake talks to a machine that may not answer; running it on the game thread would
    // freeze the window for the whole timeout, so the menu keeps drawing while this is in flight.
    private Task<NetworkSession>? _joinTask;
    private KeyboardState _prevMenuKeyboard;
    private string? _localAddresses;

    // Registered once from Initialize: MonoGame's TextInput is the only way to read typed characters
    // with the keyboard layout applied, which an IP address entry needs.
    private void OnMenuTextInput(object? sender, TextInputEventArgs e)
    {
        if (_sessionStarted || _menuScreen != MenuScreen.Join || _joinTask is not null)
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

        if (_menuScreen == MenuScreen.ShipSelect)
            HandleShipSelect(keyboard);
        else
            HandleJoinScreen(keyboard);

        _prevMenuKeyboard = keyboard;
    }

    private bool Pressed(KeyboardState keyboard, Keys key) => keyboard.IsKeyDown(key) && _prevMenuKeyboard.IsKeyUp(key);

    // Called by Update when Escape comes in: true means "handled, stay in the game".
    private bool LeaveJoinScreen()
    {
        if (_menuScreen != MenuScreen.Join || _joinTask is not null)
            return false;
        _menuScreen = MenuScreen.ShipSelect;
        _joinError = null;
        return true;
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
            : (text, Wire.DefaultPort);
    }

    // The host is a player like any other - their own session is the same SoloSession solo mode
    // uses, with the listen socket as the only difference.
    private void StartHostedSession(ShipKind shipKind, SaveGame? loadFrom)
    {
        var session = new SoloSession(shipKind, loadFrom, _openToNetwork ? Wire.DefaultPort : null);
        _session = session;
        _client = new GameClient(session.Connection, session.PlayerId);
        _sessionStarted = true;
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

    private void DrawMenu()
    {
        _spriteBatch.Begin(transformMatrix: _renderScale);
        if (_menuScreen == MenuScreen.ShipSelect)
            DrawShipSelectScreen();
        else
            DrawJoinScreen();
        _spriteBatch.End();
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
            ? $"[H] Кооп: ОТКРЫТ, порт {Wire.DefaultPort} — друзья вводят {LocalAddresses()}"
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
        _spriteBatch.DrawString(_font, $"Хост должен включить кооп ([H] в меню) и открыть порт {Wire.DefaultPort}.",
            new Vector2(60, 280), Color.Gray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        if (_joinError is { } error)
            _spriteBatch.DrawString(_font, $"Не удалось: {error}", new Vector2(60, 330), Color.OrangeRed, 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
    }
}
