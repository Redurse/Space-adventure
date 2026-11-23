using System.Diagnostics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.War;

// Fast-forward of the galactic war, for looking at how it goes. Not a test: nothing is checked, a table is printed.
//   set WAR_HOURS=150            hours of war time to run (setting it switches this mode on)
//   set WAR_SEED=4242            seed of the galaxy and of the war (default 4242)
//   set WAR_SAMPLE_HOURS=5       how often a line of the table is printed (default 5)
//   dotnet run --project Anabiosis.Tests -c Release --no-build
internal static class WarFastForward
{
    public static int Run()
    {
        var hours = double.Parse(Environment.GetEnvironmentVariable("WAR_HOURS") ?? "100", System.Globalization.CultureInfo.InvariantCulture);
        var seed = int.Parse(Environment.GetEnvironmentVariable("WAR_SEED") ?? "4242");
        var sampleHours = double.Parse(Environment.GetEnvironmentVariable("WAR_SAMPLE_HOURS") ?? "5", System.Globalization.CultureInfo.InvariantCulture);

        var watch = Stopwatch.StartNew();
        var galaxy = GalaxyMap.CreateStarter(seed);
        Console.WriteLine($"создана карта: {watch.Elapsed.TotalSeconds:0.0} с");
        galaxy.EnsureAtLeast(GalaxyMap.MaxProceduralSystems);
        Console.WriteLine($"звёзд: {galaxy.Systems.Count}, сгенерированы за {watch.Elapsed.TotalSeconds:0.0} с");
        var config = new WarConfig { Seed = seed };
        // TEMP tuning knobs: WAR_KNOBS="VirusSupplyPerHour=3;VirusInfectionShare=0.08"
        foreach (var knob in (Environment.GetEnvironmentVariable("WAR_KNOBS") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = knob.Split('=');
            var property = typeof(WarConfig).GetProperty(parts[0])!;
            property.SetValue(config, Convert.ChangeType(parts[1], property.PropertyType, System.Globalization.CultureInfo.InvariantCulture));
            Console.WriteLine("knob " + knob);
        }
        var war = new WarSim(WarGalaxy.FromGalaxyMap(galaxy), config);
        var lanes = Enumerable.Range(0, war.Galaxy.Count).Sum(i => war.Galaxy.LanesOf(i).Count) / 2;
        Console.WriteLine($"Галактика: {war.Galaxy.Count} систем, {lanes} гиперпутей; столица: {war.Galaxy.Systems[war.CapitalIndex].Name}; готово за {watch.Elapsed.TotalSeconds:0.0} с");
        var mem = GC.GetTotalMemory(true) / 1048576; Console.WriteLine($"память: {mem} МБ");
        Console.WriteLine(WarReport.Header());
        if (war.NestIndex >= 0) Console.WriteLine($"гнездо {war.Galaxy.Systems[war.NestIndex].Name}: до столицы {war.Galaxy.Hops(war.CapitalIndex, war.NestIndex)} переходов, {war.Galaxy.PathLength(war.CapitalIndex, war.NestIndex):0} ед.");

        // A raid out of the dark, to see the defence work: WAR_INVASION_HOUR=40 WAR_INVASION_SUPPLY=150 (events are printed with it).
        var invasionHourText = Environment.GetEnvironmentVariable("WAR_INVASION_HOUR");
        if (invasionHourText is not null || Environment.GetEnvironmentVariable("WAR_EVENTS") == "1")
            war.OnEvent = Console.WriteLine;

        watch.Restart();
        void Print(WarSample sample) => Console.WriteLine(WarReport.Row(sample));
        if (invasionHourText is not null)
        {
            var invasionHour = double.Parse(invasionHourText, System.Globalization.CultureInfo.InvariantCulture);
            var supply = float.Parse(Environment.GetEnvironmentVariable("WAR_INVASION_SUPPLY") ?? "150", System.Globalization.CultureInfo.InvariantCulture);
            war.FastForward(invasionHour, sampleHours, Print);
            war.SpawnInvasion(war.FindFrontierNode(), supply);
            war.FastForward(hours - invasionHour, sampleHours, Print);
        }
        else
        {
            war.FastForward(hours, sampleHours, Print);
        }
        Console.WriteLine($"систем восстания: {war.Nodes.Count(n => n.Owner == WarFaction.Virus)}, Союза: {war.Nodes.Count(n => n.Owner == WarFaction.Union)}");
        if (war.Mother is { } mother)
            Console.WriteLine($"матка: {(mother.Alive ? $"жива, в {war.Galaxy.Systems[mother.Where].Name}, прочность {100 * mother.Hp / mother.MaxHp:0}%" : "УНИЧТОЖЕНА")}");
        if (war.NestIndex >= 0)
        {
            var nest = war.Nodes[war.NestIndex];
            Console.WriteLine($"гнездо {war.Galaxy.Systems[war.NestIndex].Name}: владелец {nest.Owner}, руда {nest.Stock:0}, очередь {nest.Queue.Count} (начато {nest.Queue.Count(o => o.Started)}), гарнизон {Anabiosis.Shared.War.CombatStats.Supply(nest.Garrison):0}, форт {nest.FortHp:0}, обороты {war.OreRate(nest) * 3600:0}/ч");
        }
        foreach (var line in war.DescribeOperations())
            Console.WriteLine(line);
        Console.WriteLine($"{hours:0.#} ч войны посчитаны за {watch.Elapsed.TotalSeconds:0.0} с (руды добыто {war.OreMined:0}, потеряно {war.OreWasted:0})");
        return 0;
    }
}
