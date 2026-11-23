using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;

// The dedicated server: a window-less process that hosts a game for others. No local player - whoever joins first is the host of the waiting room
// (picks the ship, changes the settings, starts the round); if they leave, the one who has been there longest takes over. Options and the console
// commands are in DedicatedServerOptions.cs.

var options = DedicatedServerOptions.Parse(args);
if (options.Help)
{
    Console.WriteLine(DedicatedServerOptions.Usage);
    return;
}
if (options.Error is not null)
{
    Console.WriteLine("Ошибка в параметрах: " + options.Error);
    Console.WriteLine(DedicatedServerOptions.Usage);
    return;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

GameServer server;
if (options.Continue && options.SavePath is not null && SaveStore.Load(options.SavePath) is { } save)
{
    server = new GameServer(ShipKind.Custom, save, options.SavePath) { Dedicated = true };
    Console.WriteLine($"Продолжаю сохранённый забег из {options.SavePath}.");
}
else
{
    server = new GameServer(options.Name, options.MaxPlayers, options.SavePath) { Dedicated = true };
}

var serverThread = new Thread(() => server.Run(cts.Token)) { Name = "dedicated-server" };
serverThread.Start();

using var host = new NetworkHost(server, options.Port, options.MaxPlayers);
using var announcer = options.Lan
    ? new LanAnnouncer(() => server.Settings.OpenServer ? new LanAnnouncement(server.LobbyServerName, host.Port, server.PlayerCount, options.MaxPlayers, server.InLobby) : null)
    : null;

Console.WriteLine($"Anabiosis.Server: «{options.Name}» принимает игроков на порту {host.Port} (до {options.MaxPlayers}).");
Console.WriteLine(options.Lan ? "Сервер виден в локальной сети." : "Объявление в локальной сети выключено (--no-lan).");
Console.WriteLine("Команды: help, status, kick <id>, start, quit. Ctrl+C - выход.");

// The console runs on the main thread; every command that touches the game goes through the server's own admin queue, so it runs between ticks.
var input = Task.Run(() =>
{
    while (!cts.IsCancellationRequested)
    {
        var line = Console.ReadLine();
        if (line is null)
            return; // the console is closed (the process was started without one): the server just keeps running
        var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            continue;
        switch (parts[0].ToLowerInvariant())
        {
            case "help":
                Console.WriteLine("help - эта справка; status - кто на сервере; kick <id> - убрать игрока из лобби; start - начать раунд; quit - выключить сервер");
                break;
            case "status":
                Console.WriteLine(server.StatusText);
                break;
            case "kick" when parts.Length == 2 && int.TryParse(parts[1], out var id):
                server.EnqueueAdmin(() => Console.WriteLine(server.AdminKick(id) ? $"Игрок {id} убран из лобби." : $"Игрока {id} нет в лобби (или это хост)."));
                break;
            case "start":
                server.EnqueueAdmin(() =>
                {
                    if (server.InLobby)
                    {
                        server.AdminStartRound();
                        Console.WriteLine("Раунд начат.");
                    }
                    else
                    {
                        Console.WriteLine("Раунд уже идёт.");
                    }
                });
                break;
            case "quit" or "exit" or "stop":
                cts.Cancel();
                return;
            default:
                Console.WriteLine("Неизвестная команда. help - справка.");
                break;
        }
    }
});

cts.Token.WaitHandle.WaitOne();
serverThread.Join();
Console.WriteLine("Anabiosis.Server: остановлен.");
