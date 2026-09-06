using Anabiosis.Server;

Console.WriteLine("Anabiosis.Server: старт (standalone), Ctrl+C для выхода.");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var server = new GameServer();
server.Run(cts.Token);
