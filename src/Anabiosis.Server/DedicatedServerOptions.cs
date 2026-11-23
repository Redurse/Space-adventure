using Anabiosis.Shared.Networking;

namespace Anabiosis.Server;

// The command line of Anabiosis.Server.exe, the dedicated server (no window, no local player: whoever joins first is the host of the waiting room).
//
//   Anabiosis.Server.exe [--name "Мой сервер"] [--port 47624] [--max 4] [--save путь] [--continue] [--no-lan]
//
//   --name      the name shown in the server list and the lobby;
//   --port      the TCP port players join on;
//   --max       how many players at most (1-8);
//   --save      where the run is saved (autosave on every docking); without it nothing is saved and the world is the fixed test one;
//   --continue  with --save: carry on the saved run instead of opening a new lobby;
//   --no-lan    do not announce the server on the local network (players type the address).
public sealed record DedicatedServerOptions(string Name, int Port, int MaxPlayers, string? SavePath, bool Continue, bool Lan, bool Help, string? Error)
{
    public const int MinPlayers = 1;
    public const int MaxPlayersLimit = 8;

    public static string Usage =>
        "Anabiosis.Server.exe [--name \"Мой сервер\"] [--port " + Wire.DefaultPort + "] [--max 4] [--save путь] [--continue] [--no-lan]\n" +
        "  --name      название сервера (в списке серверов и в лобби)\n" +
        "  --port      TCP-порт, на котором принимаются игроки\n" +
        "  --max       максимум игроков (1-8)\n" +
        "  --save      куда сохранять забег (автосохранение при стыковке); без него ничего не сохраняется\n" +
        "  --continue  вместе с --save: продолжить сохранённый забег вместо нового лобби\n" +
        "  --no-lan    не объявлять сервер в локальной сети\n" +
        "Команды в консоли: help, status, kick <id>, start, quit";

    public static DedicatedServerOptions Parse(IReadOnlyList<string> args)
    {
        var name = "Сервер Anabiosis";
        var port = Wire.DefaultPort;
        var max = 4;
        string? save = null;
        var cont = false;
        var lan = true;
        var help = false;

        for (var i = 0; i < args.Count; i++)
        {
            string Next(string option) =>
                i + 1 < args.Count ? args[++i] : throw new ArgumentException($"после {option} нужно значение");
            try
            {
                switch (args[i])
                {
                    case "--help" or "-h" or "/?":
                        help = true;
                        break;
                    case "--name":
                        name = Next("--name");
                        break;
                    case "--port":
                        port = int.TryParse(Next("--port"), out var p) && p is >= 1 and <= 65535 ? p : throw new ArgumentException("порт должен быть числом от 1 до 65535");
                        break;
                    case "--max":
                        max = int.TryParse(Next("--max"), out var m) && m is >= MinPlayers and <= MaxPlayersLimit
                            ? m
                            : throw new ArgumentException($"игроков должно быть от {MinPlayers} до {MaxPlayersLimit}");
                        break;
                    case "--save":
                        save = Next("--save");
                        break;
                    case "--continue":
                        cont = true;
                        break;
                    case "--no-lan":
                        lan = false;
                        break;
                    default:
                        throw new ArgumentException($"неизвестный параметр {args[i]}");
                }
            }
            catch (ArgumentException e)
            {
                return new DedicatedServerOptions(name, port, max, save, cont, lan, help, e.Message);
            }
        }

        if (cont && save is null)
            return new DedicatedServerOptions(name, port, max, save, cont, lan, help, "--continue работает только вместе с --save");
        return new DedicatedServerOptions(name, port, max, save, cont, lan, help, null);
    }
}
