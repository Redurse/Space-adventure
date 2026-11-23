namespace Anabiosis.Shared.War;

// The war between the Colonial Union and the virus, played on the galactic map (see memory anabiosis-galactic-war-design).
// Everything under Anabiosis.Shared/War is plain logic with no dependency on the ship simulation: it runs on a graph of
// star systems, takes a clock, and can be fast-forwarded for hours without drawing anything.

public enum WarFaction { None, Union, Virus }

// What stands on a system's planet. The capital is the Union's fortress world.
public enum PlanetKind { None, Colony, Capital }

public enum YardTier { None, Yard, MegaYard }

public enum ShipClass { Drone, Miner, Freighter, Outposter, Corvette, Frigate, Cruiser, Dreadnought }

// Things built on the spot out of a node's own ore: a colony on an outpost's planet, a yard, a megayard.
public enum ProjectKind { Colony, Yard, MegaYard }

public static class ShipStats
{
    public const int ClassCount = 8;

    // Ore it costs, seconds it takes to build, and how much of the fleet-supply limit it uses.
    public static float Cost(ShipClass ship) => ship switch
    {
        ShipClass.Drone => 25f,
        ShipClass.Miner => 70f,
        ShipClass.Freighter => 90f,
        ShipClass.Outposter => 300f,
        ShipClass.Corvette => 110f,
        ShipClass.Frigate => 280f,
        ShipClass.Cruiser => 800f,
        _ => 4500f, // dreadnought
    };

    public static float BuildSeconds(ShipClass ship) => ship switch
    {
        ShipClass.Drone => 45f,
        ShipClass.Miner => 150f,
        ShipClass.Freighter => 240f,
        ShipClass.Outposter => 900f,
        ShipClass.Corvette => 360f,
        ShipClass.Frigate => 900f,
        ShipClass.Cruiser => 2400f,
        _ => 10800f,
    };

    public static int Supply(ShipClass ship) => ship switch
    {
        ShipClass.Corvette => 1,
        ShipClass.Frigate => 2,
        ShipClass.Cruiser => 4,
        ShipClass.Dreadnought => 12,
        _ => 0,
    };

    public static bool IsWarship(ShipClass ship) => ship >= ShipClass.Corvette;

    // Drones and miners are made by an outpost's own workshop; the rest need a yard (dreadnoughts a megayard).
    public static bool NeedsYard(ShipClass ship) => ship >= ShipClass.Freighter;

    public static YardTier RequiredYard(ShipClass ship) => ship == ShipClass.Dreadnought ? YardTier.MegaYard : YardTier.Yard;
}

public static class ProjectStats
{
    public static float Cost(ProjectKind kind) => kind switch
    {
        ProjectKind.Colony => 700f,
        ProjectKind.Yard => 1200f,
        _ => 9000f,
    };

    public static float Seconds(ProjectKind kind) => kind switch
    {
        ProjectKind.Colony => 1800f,
        ProjectKind.Yard => 2400f,
        _ => 21600f,
    };
}

// How the warships fight and travel. A round is ten seconds of war time. Attack is the damage a ship does in a round, Hp
// what it takes to destroy it; a fleet strength is the square root of its total attack times its total hp, which is what
// decides a battle between two fleets (the larger product wins, and wins by more than the ratio suggests).
public static class CombatStats
{
    public static float Attack(ShipClass ship) => ship switch
    {
        ShipClass.Corvette => 6f,
        ShipClass.Frigate => 18f,
        ShipClass.Cruiser => 70f,
        ShipClass.Dreadnought => 420f,
        _ => 0f,
    };

    public static float Hp(ShipClass ship) => ship switch
    {
        ShipClass.Corvette => 40f,
        ShipClass.Frigate => 140f,
        ShipClass.Cruiser => 600f,
        ShipClass.Dreadnought => 4200f,
        _ => 0f,
    };

    // Map units per second: a fleet moves at the pace of its slowest ship.
    public static float Speed(ShipClass ship) => ship switch
    {
        ShipClass.Corvette => 1.3f,
        ShipClass.Frigate => 1.1f,
        ShipClass.Cruiser => 0.9f,
        ShipClass.Dreadnought => 0.6f,
        _ => 0f,
    };

    public const int FirstWarship = (int)ShipClass.Corvette;

    public static float Attack(float[] ships)
    {
        var total = 0f;
        for (var c = FirstWarship; c < ShipStats.ClassCount; c++)
            total += ships[c] * Attack((ShipClass)c);
        return total;
    }

    public static float Hp(float[] ships)
    {
        var total = 0f;
        for (var c = FirstWarship; c < ShipStats.ClassCount; c++)
            total += ships[c] * Hp((ShipClass)c);
        return total;
    }

    public static float Supply(float[] ships)
    {
        var total = 0f;
        for (var c = FirstWarship; c < ShipStats.ClassCount; c++)
            total += ships[c] * ShipStats.Supply((ShipClass)c);
        return total;
    }

    public static float Strength(float attack, float hp) => MathF.Sqrt(Math.Max(0f, attack) * Math.Max(0f, hp));
    public static float Strength(float[] ships) => Strength(Attack(ships), Hp(ships));
}
