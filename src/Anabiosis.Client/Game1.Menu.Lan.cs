using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Anabiosis.Client.Networking;
using Anabiosis.Server;
using Anabiosis.Shared.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Anabiosis.Client;

// The join screen's list of servers on the local network (Shared/Networking/LanDiscovery.cs), and the "Создать сервер" screen's option to run the game
// as a separate dedicated server (Anabiosis.Server.exe) and join it.
public partial class Game1
{
    private LanBrowser? _lanBrowser;
    private const int JoinServerRowHeight = 30;
    private const int JoinServerRowsMax = 7;
    private const int JoinServerListTop = 380;

    // Listening only while the join screen is open.
    private void EnsureLanBrowser() => _lanBrowser ??= new LanBrowser();

    private void DisposeLanBrowserIfNotJoining()
    {
        if (_lanBrowser is null || _menuScreen == MenuScreen.Join)
            return;
        _lanBrowser.Dispose();
        _lanBrowser = null;
    }

    private static Rectangle GetJoinServerRowRect(int index) => new(60, JoinServerListTop + 28 + index * JoinServerRowHeight, 760, JoinServerRowHeight - 4);

    // Joins a server the same way pressing Enter on the typed address does.
    private void StartJoin(string host, int port)
    {
        _joinAddress = port == Wire.DefaultPort ? host : $"{host}:{port}";
        _joinError = null;
        _joinTask = Task.Run(() => NetworkSession.Join(host, port));
    }

    // Returns true when the click was on a server of the list (and the join has begun).
    private bool HandleJoinServerListClick()
    {
        EnsureLanBrowser();
        var mouse = Mouse.GetState();
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevMenuLeftMouseButton == ButtonState.Released;
        _prevMenuLeftMouseButton = mouse.LeftButton;
        if (!clicked)
            return false;
        var servers = _lanBrowser!.Servers;
        for (var i = 0; i < Math.Min(servers.Count, JoinServerRowsMax); i++)
        {
            if (!GetJoinServerRowRect(i).Contains(_designMouse))
                continue;
            PlayUiClick();
            StartJoin(servers[i].Address, servers[i].Port);
            return true;
        }
        return false;
    }

    private void DrawJoinServerList()
    {
        EnsureLanBrowser();
        _spriteBatch.DrawString(_font, "Серверы в локальной сети (нажмите, чтобы подключиться):", new Vector2(60, JoinServerListTop), Color.LightGray, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        var servers = _lanBrowser!.Servers;
        if (servers.Count == 0)
        {
            _spriteBatch.DrawString(_font, "Пока никого не видно: хост должен быть в той же сети (сервер объявляет себя раз в полторы секунды).",
                new Vector2(60, JoinServerListTop + 32), Color.Gray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            return;
        }
        for (var i = 0; i < Math.Min(servers.Count, JoinServerRowsMax); i++)
        {
            var server = servers[i];
            var rect = GetJoinServerRowRect(i);
            var hover = rect.Contains(_designMouse);
            _spriteBatch.Draw(_pixel, rect, (hover ? new Color(52, 60, 74) : new Color(30, 36, 44)) * 0.9f);
            DrawBevel(rect, raised: hover, strength: 0.5f);
            var full = server.Players >= server.MaxPlayers;
            _spriteBatch.DrawString(_font, server.Name, new Vector2(rect.X + 10, rect.Y + 5), SettingsAccentGold, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, $"{server.Players}/{server.MaxPlayers}", new Vector2(rect.X + 330, rect.Y + 5),
                full ? SettingsAccentRed : SettingsTextPrimary, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, server.InLobby ? "лобби" : "идёт раунд", new Vector2(rect.X + 410, rect.Y + 5),
                server.InLobby ? SettingsAccentTeal : SettingsTextDim, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_font, $"{server.Address}:{server.Port}", new Vector2(rect.X + 540, rect.Y + 5), SettingsTextDim, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }
    }

    // ---- "Создать сервер": the options that have a real mechanism behind them now ----

    private bool _createServerLan = true;
    private bool _createServerDedicated;
    private string? _createServerError;

    private static Rectangle GetCreateServerLanCheckboxRect(Vector2 panelOrigin) => GetCreateServerPublicCheckboxRect(panelOrigin);
    private static Rectangle GetCreateServerDedicatedCheckboxRect(Vector2 panelOrigin)
    { var p = CreateServerContentOrigin(panelOrigin); return new Rectangle((int)p.X, (int)p.Y + 302, 20, 20); }

    private void DrawCreateServerRealCheckbox(Rectangle rect, string label, bool on)
    {
        _spriteBatch.Draw(_pixel, rect, new Color(20, 22, 26) * 0.9f);
        DrawBevel(rect, raised: false, strength: 0.6f);
        if (on)
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X + 4, rect.Y + 4, rect.Width - 8, rect.Height - 8), SettingsAccentTeal);
        _spriteBatch.DrawString(_font, label, new Vector2(rect.Right + 8, rect.Y + 1), SettingsTextPrimary, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    // "НАЧАТЬ" with "отдельным сервером": starts Anabiosis.Server.exe (its console window is the server - closing it stops the server), waits for it to
    // listen, and joins it like any player; being the first to join, this player is the host of its waiting room.
    private void StartDedicatedServerAndJoin()
    {
        _createServerError = null;
        var exe = Path.Combine(AppContext.BaseDirectory, "Anabiosis.Server.exe");
        if (!File.Exists(exe))
        {
            _createServerError = "Не найден Anabiosis.Server.exe рядом с игрой.";
            return;
        }

        var port = Wire.DefaultPort;
        // Its own save slot: a dedicated run never overwrites the player's solo save.
        var savePath = Path.Combine(Path.GetDirectoryName(Anabiosis.Server.SaveStore.DefaultPath)!, "dedicated-save.json");
        var name = _createServerName.Replace('"', '\'').Trim();
        var args = $"--name \"{(name.Length == 0 ? "Сервер" : name)}\" --port {port} --max {_createServerMaxPlayers} --save \"{savePath}\"" + (_createServerLan ? "" : " --no-lan");
        try
        {
            Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = true, WorkingDirectory = AppContext.BaseDirectory });
        }
        catch (Exception e)
        {
            _createServerError = "Не удалось запустить сервер: " + e.Message;
            return;
        }

        _joinAddress = $"127.0.0.1:{port}";
        _joinError = null;
        _menuScreen = MenuScreen.Join; // the connecting popup and any failure are shown there
        _joinTask = Task.Run(() =>
        {
            // The server needs a moment to start listening: ask again for up to ten seconds.
            Exception? last = null;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    return NetworkSession.Join("127.0.0.1", port);
                }
                catch (Exception e)
                {
                    last = e;
                    System.Threading.Thread.Sleep(500);
                }
            }
            throw last ?? new Exception("сервер не ответил");
        });
    }
}
