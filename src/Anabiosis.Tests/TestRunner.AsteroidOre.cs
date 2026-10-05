using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

internal static partial class TestRunner
{
    private static bool AsteroidOre_HotAsteroids_SitInsideTheSunZoneCarryingRichOre()
    {
        var rich = new[] { ItemType.UraniumOre, ItemType.TitaniumOre, ItemType.PlastalloyOre, ItemType.Hyperium };
        foreach (var systemId in new[] { "sol", "sys-001", "sys-017" })
        {
            var bodies = CelestialBodyGenerator.Generate(systemId);
            var star = bodies.Single(b => b.ParentId is null);
            var half = CelestialBodyGenerator.FieldSize(bodies) / 2f;
            var (rocks, deposits) = AsteroidOre.HotAsteroids(systemId);
            if (rocks.Count < 3 || deposits.Count != rocks.Count * 3)
                return false;
            foreach (var rock in rocks)
            {
                var distance = (rock.Position - new Vec2(half, half)).Length();
                if (distance <= star.Radius || distance >= CelestialBodyGenerator.SunZoneRadius(star))
                    return false;
            }
            if (deposits.Any(d => !rich.Contains(d.OreType)))
                return false;
        }
        return true;
    }

    private static bool AsteroidField_CreateForSystem_OreIsDeterministicAnchoredToRocksAndUnique()
    {
        for (var n = 1; n <= 30; n++)
        {
            var systemId = $"sys-{n:000}";
            var field = AsteroidField.CreateForSystem(systemId);
            var again = AsteroidField.CreateForSystem(systemId);
            if (field.OreDeposits.Count == 0 || !field.OreDeposits.Select(d => d.Id).SequenceEqual(again.OreDeposits.Select(d => d.Id)))
                return false;
            if (field.OreDeposits.Select(d => d.Id).Distinct().Count() != field.OreDeposits.Count)
                return false;
            var rockIds = field.Asteroids.Select(a => a.Id).ToHashSet();
            if (field.OreDeposits.Any(d => !rockIds.Contains(d.AsteroidId)))
                return false;
        }
        return true;
    }

    private static bool World_Mining_OreInAnotherSystem_StartsAtFullHp()
    {
        var world = new World();
        world.SpawnCharacter(1);
        FlyToSolWarpZoneAndStop(world);
        if (!world.CanWarpNow)
            return false;

        world.ApplyCommand(1, new ClientCommand(1, WarpToSystemId: "alpha-centauri"));
        var snapshot = world.CreateSnapshot();
        return snapshot.CurrentSystemId == "alpha-centauri"
            && snapshot.Field.OreDeposits.Count > 0
            && snapshot.Field.OreDepositStates.All(s => s.Hp == s.MaxHp);
    }
}
