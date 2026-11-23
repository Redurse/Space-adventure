using Anabiosis.Server;
using Anabiosis.Shared.War;

// The galactic war reaching the player ship (World.WarBattle.cs).
internal static partial class TestRunner
{
    // Virus ships standing in the system the ship is in find it after a short while and attack: a battle starts that is not
    // a sector battle and costs no faction standing.
    private static bool World_War_VirusFleetInThePlayerSystem_StartsABattle()
    {
        var world = new World(worldSeed: 4242);
        world.SpawnCharacter(1);
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new Anabiosis.Shared.Protocol.ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }
        world.EnableWar();
        var war = world.War!;
        if (!war.Galaxy.TryIndexOf("sol", out var sol))
            return false;
        war.SpawnInvasion(sol, 6000f); // big enough to outlast the capital fortress for the half minute it takes to be noticed
        var before = world.IsInBattle;
        StepFor(world, 40 * 30);
        return !before && world.IsInBattle;
    }

    // What the player destroys comes off the virus in that system, from the fleets standing there.
    private static bool WarSim_PlayerStrike_RemovesVirusShipsFromASystem()
    {
        var galaxy = Anabiosis.Shared.Model.GalaxyMap.CreateStarter(4242);
        galaxy.EnsureAtLeast(194);
        var war = new WarSim(WarGalaxy.FromGalaxyMap(galaxy), new WarConfig { Seed = 4242, VirusEnabled = false, MotherEnabled = false });
        var node = war.FindFrontierNode();
        war.SpawnInvasion(node, 100f);
        var before = war.VirusPresenceAt(node);
        war.StrikeVirusAt(node, 40f);
        var after = war.VirusPresenceAt(node);
        war.StrikeVirusAt(node, 1000f);
        return before > 95f && after is > 55f and < 65f && war.VirusPresenceAt(node) < 0.5f;
    }

    // The war survives a save: written out as JSON and read back into a fresh war on the same galaxy, it goes on exactly as the
    // original does (the simulation has no randomness, so the two must stay identical).
    private static bool WarSim_SaveAndRestore_ContinuesIdentically()
    {
        var galaxy = Anabiosis.Shared.Model.GalaxyMap.CreateStarter(4242);
        galaxy.EnsureAtLeast(194);
        var original = new WarSim(WarGalaxy.FromGalaxyMap(galaxy), new WarConfig { Seed = 4242 });
        original.FastForward(75, 75); // past the first virus attack, so fleets, operations and the mother ship are all in motion

        var options = new System.Text.Json.JsonSerializerOptions { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };
        var json = System.Text.Json.JsonSerializer.Serialize(original.Capture(), options);
        var loaded = System.Text.Json.JsonSerializer.Deserialize<WarSave>(json, options)!;

        var copy = new WarSim(WarGalaxy.FromGalaxyMap(galaxy), new WarConfig { Seed = 4242 });
        if (!copy.Restore(loaded))
            return false;

        original.FastForward(25, 25);
        copy.FastForward(25, 25);
        var a = WarReport.Row(original.TakeSample());
        var b = WarReport.Row(copy.TakeSample());
        return a == b && original.Fleets.Count == copy.Fleets.Count && original.Mother!.Where == copy.Mother!.Where;
    }

    // Through the whole game save: a world saved mid-war and loaded into a new one carries the war clock and the war over.
    private static bool World_Save_CarriesTheWar()
    {
        var first = new World(worldSeed: 4242);
        first.EnableWar();
        first.War!.FastForward(30, 30);
        var save = first.CreateSave();

        var second = new World(worldSeed: 4242);
        second.ApplySave(save);
        second.EnableWar();
        return save.War is not null && second.War is { } war && Math.Abs(war.Clock - first.War.Clock) < 1.0 && war.NestIndex == first.War.NestIndex;
    }
}
