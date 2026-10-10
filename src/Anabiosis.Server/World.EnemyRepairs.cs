using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// What a hostile crew's engineers fix, in priority order, the same things the player's engineer would: the reactor, the
// distribution block and the helm when they are broken, cut wires (a gun's or an engine's lead, a junction box's trunk), shot
// guns and engines, and finally the hull plating. Electrical repairs take a fixed time of standing at the thing.
public sealed partial class World
{
    private const float EnemyBlockRepairSeconds = 10f;
    private const float EnemyWireRepairSeconds = 5f;
    private const float EnemyFixtureRepairPerSecond = 12f;

    internal enum EnemyRepairKind { Block, Wire, Fixture, Wall }

    // Key identifies the job (shared between engineers, and the progress record); Id is what it acts on.
    internal readonly record struct EnemyRepairJob(string Key, Vec2 Position, EnemyRepairKind Kind, string Id, float Max = 0f);

    private static EnemyRepairJob? FindRepairJob(EnemyShipRuntime enemy, EnemyCrewRuntime crew)
    {
        var layout = enemy.Layout;
        var ship = layout.Ship;

        IEnumerable<EnemyRepairJob> Jobs()
        {
            if (enemy.Grid.Reactor.Broken && ship.Devices.FirstOrDefault(d => d.Kind == DeviceKind.Reactor) is { } reactor)
                yield return new EnemyRepairJob("block:reactor", reactor.Position, EnemyRepairKind.Block, "reactor");
            if (enemy.Grid.DistributionBroken && ship.Devices.FirstOrDefault(d => d.Kind == DeviceKind.Distribution) is { } distribution)
                yield return new EnemyRepairJob("block:distribution", distribution.Position, EnemyRepairKind.Block, "distribution");
            if (enemy.HelmBroken)
                yield return new EnemyRepairJob("block:helm", ship.HelmConsole.Position, EnemyRepairKind.Block, "helm");

            foreach (var wire in enemy.CutWires)
            {
                var position = WirePosition(enemy, wire);
                if (position is { } at)
                    yield return new EnemyRepairJob("wire:" + wire, at, EnemyRepairKind.Wire, wire);
            }

            foreach (var turret in ship.Turrets)
                if (enemy.FixtureHp(turret.Id, EnemyShipRuntime.TurretMaxHp) < EnemyShipRuntime.TurretMaxHp)
                    yield return new EnemyRepairJob("fix:" + turret.Id, TurretMount.For(layout.Rooms, ship.Turrets, turret).Position,
                        EnemyRepairKind.Fixture, turret.Id, EnemyShipRuntime.TurretMaxHp);
            foreach (var engine in ship.Engines)
            {
                if (enemy.EngineBulkheadHp(engine.Id) < EnginePartMaxHp)
                    yield return new EnemyRepairJob("fix:" + engine.Id + ":bulkhead", engine.BulkheadPosition, EnemyRepairKind.Fixture,
                        engine.Id + ":bulkhead", EnginePartMaxHp);
                if (enemy.EngineNozzleHp(engine.Id) < EnginePartMaxHp)
                    yield return new EnemyRepairJob("fix:" + engine.Id + ":nozzle", engine.NozzlePosition, EnemyRepairKind.Fixture,
                        engine.Id + ":nozzle", EnginePartMaxHp);
            }

            // Breached plating first, then the most dented.
            foreach (var block in layout.WallBlocks
                .Where(b => enemy.GetWallBlockHp(b.Id) < WallBlockMaxHp)
                .OrderBy(b => enemy.IsWallBlockBreached(b.Id) ? 0 : 1))
                yield return new EnemyRepairJob("wall:" + block.Id, block.Position, EnemyRepairKind.Wall, block.Id);
        }

        var all = Jobs().ToList();
        if (all.Count == 0)
            return null;

        // The most urgent tier that has work; within it, the nearest job wins.
        var tier = all.Min(Tier);
        return all.Where(j => Tier(j) == tier).OrderBy(j => (j.Position - crew.Position).Length()).First();
    }

    // Broken blocks, then cut wires, then guns/engines, then plating: a tier is only started once the one above it is done.
    private static int Tier(EnemyRepairJob job) => job.Kind switch
    {
        EnemyRepairKind.Block => 0,
        EnemyRepairKind.Wire => 1,
        EnemyRepairKind.Fixture => 2,
        _ => 3,
    };

    // Where the person stands to fix a cut wire: at its far end (the box or the device it feeds).
    private static Vec2? WirePosition(EnemyShipRuntime enemy, string wireId)
    {
        if (wireId.StartsWith("drop-"))
        {
            var consumer = wireId["drop-".Length..];
            return enemy.SystemOfConsumer.ContainsKey(consumer) ? enemy.ConsumerPosition(consumer) : null;
        }
        if (wireId.StartsWith("trunk-") && Enum.TryParse<PowerSystemId>(wireId["trunk-".Length..], out var system))
        {
            if (enemy.JunctionBoxOfSystem.TryGetValue(system, out var boxId)
                && enemy.Layout.Ship.JunctionBoxes.FirstOrDefault(b => b.Id == boxId) is { } box)
                return new Vec2(box.X, box.Y);
            return enemy.Layout.Ship.Devices.FirstOrDefault(d => d.Kind == DeviceKind.Distribution)?.Position;
        }
        return null;
    }

    // The standing-at-it part of a job.
    private static void WorkOn(EnemyShipRuntime enemy, EnemyRepairJob job, float dt)
    {
        switch (job.Kind)
        {
            case EnemyRepairKind.Wall:
                enemy.RepairWallBlock(job.Id, EnemyCrewRepairPerSecond * dt);
                break;

            case EnemyRepairKind.Fixture:
                enemy.RepairFixture(job.Id, job.Max, EnemyFixtureRepairPerSecond * dt);
                break;

            case EnemyRepairKind.Block:
            case EnemyRepairKind.Wire:
                var duration = job.Kind == EnemyRepairKind.Block ? EnemyBlockRepairSeconds : EnemyWireRepairSeconds;
                var progress = enemy.RepairProgress.GetValueOrDefault(job.Key) + dt;
                if (progress < duration)
                {
                    enemy.RepairProgress[job.Key] = progress;
                    break;
                }
                enemy.RepairProgress.Remove(job.Key);
                if (job.Kind == EnemyRepairKind.Wire)
                    enemy.CutWires.Remove(job.Id);
                else if (job.Id == "reactor")
                    enemy.Grid.Reactor.Broken = false;
                else if (job.Id == "distribution")
                    enemy.Grid.DistributionBroken = false;
                else if (job.Id == "helm")
                    enemy.HelmBroken = false;
                break;
        }
    }
}
