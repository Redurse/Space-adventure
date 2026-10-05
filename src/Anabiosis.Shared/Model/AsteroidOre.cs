namespace Anabiosis.Shared.Model;

// Cosmoteer-style ore distribution for generated asteroids: most rocks carry common ore, a few
// carry rare ore, and the richest rocks hug the star inside the heat ring (CelestialBodyGenerator.
// SunZoneRadius) where mining costs hull HP. Deterministic per system id, same as the belts.
public static class AsteroidOre
{
    // Weighted like the Cosmoteer wiki's asteroid table: a rare ore is roughly an order of
    // magnitude less likely than a common one.
    private static readonly (ItemType Ore, float Weight)[] Table =
    {
        (ItemType.IronOre, 12f), (ItemType.CopperOre, 10f), (ItemType.Carbon, 7f), (ItemType.Silicon, 7f),
        (ItemType.ZincOre, 6f), (ItemType.AluminumOre, 5f), (ItemType.Hyperium, 4f),
        (ItemType.NickelOre, 3f), (ItemType.TitaniumOre, 2f), (ItemType.PlastalloyOre, 1.5f), (ItemType.UraniumOre, 1f),
    };

    private static readonly ItemType[] RichOres =
        { ItemType.UraniumOre, ItemType.TitaniumOre, ItemType.PlastalloyOre, ItemType.Hyperium };

    private const float BeltAsteroidOreChance = 0.35f;
    private const float DepositMaxHp = 100f;
    private const int HotAsteroidsMin = 3;
    private const int HotAsteroidsMax = 5;

    public static ItemType RollOre(Random random)
    {
        var total = Table.Sum(t => t.Weight);
        var roll = (float)random.NextDouble() * total;
        foreach (var (ore, weight) in Table)
        {
            roll -= weight;
            if (roll <= 0f)
                return ore;
        }
        return Table[0].Ore;
    }

    // Ore blocks for a subset of the given asteroids; bigger rocks carry more blocks.
    public static List<OreDeposit> DepositsFor(string systemId, IEnumerable<Asteroid> asteroids)
    {
        var random = new Random(AsteroidShape.StableHash(systemId + "-ore"));
        var result = new List<OreDeposit>();
        foreach (var rock in asteroids)
        {
            if (random.NextDouble() >= BeltAsteroidOreChance)
                continue;
            var blocks = rock.Radius >= 20f ? random.Next(2, 4) : random.Next(1, 3);
            AddBlocks(result, rock, blocks, random, () => RollOre(random));
        }
        return result;
    }

    // Small, ore-rich rocks sitting just outside the star's disc, inside the heat ring.
    public static (List<Asteroid> Rocks, List<OreDeposit> Deposits) HotAsteroids(string systemId)
    {
        var bodies = CelestialBodyGenerator.Generate(systemId);
        var star = bodies.Single(b => b.ParentId is null);
        var center = new Vec2(CelestialBodyGenerator.FieldSize(bodies) / 2f, CelestialBodyGenerator.FieldSize(bodies) / 2f);
        var random = new Random(AsteroidShape.StableHash(systemId + "-hot"));
        var rocks = new List<Asteroid>();
        var deposits = new List<OreDeposit>();
        var count = random.Next(HotAsteroidsMin, HotAsteroidsMax + 1);
        var inner = star.Radius * 1.08f;
        var outer = CelestialBodyGenerator.SunZoneRadius(star) * 0.95f;

        for (var i = 0; i < count; i++)
        {
            var angle = random.NextDouble() * 2 * Math.PI;
            var radius = inner + (float)random.NextDouble() * (outer - inner);
            var position = center + new Vec2(Math.Cos(angle), Math.Sin(angle)) * radius;
            var rock = new Asteroid($"hot-{systemId}-{i}", position.X, position.Y, 8f + (float)random.NextDouble() * 6f);
            rocks.Add(rock);
            AddBlocks(deposits, rock, 3, random, () => RichOres[random.Next(RichOres.Length)]);
        }
        return (rocks, deposits);
    }

    private static void AddBlocks(List<OreDeposit> into, Asteroid rock, int blocks, Random random, Func<ItemType> pickOre)
    {
        var firstAngle = random.NextDouble() * 2 * Math.PI;
        for (var b = 0; b < blocks; b++)
        {
            var angle = firstAngle + b * (2 * Math.PI / blocks);
            var nominal = rock.Position + new Vec2(Math.Cos(angle), Math.Sin(angle)) * rock.Radius;
            var surface = AsteroidShape.SurfacePoint(rock, nominal, 0f);
            into.Add(new OreDeposit($"ore-{rock.Id}-{b}", rock.Id, surface.X, surface.Y, DepositMaxHp, OreType: pickOre()));
        }
    }
}
