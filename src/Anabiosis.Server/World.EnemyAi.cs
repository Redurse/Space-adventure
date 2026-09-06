using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

public sealed partial class World
{
    // Seeded from a counter rather than a constant, so both properties hold: a whole run of the
    // suite is reproducible (Worlds are built in a fixed order), while two Worlds built in a row
    // still roll differently - which is what the tests that retry a scenario until it lands are
    // relying on. A real session builds exactly one World, so its fights are as varied as the
    // sequence of rolls within them. Nothing here still rolls a hit location (that's positional
    // now, see ApplyEnemyAttack below) - this stays for whatever else in combat still wants a die
    // roll (dodge decisions, World.EnemyFleet.cs).
    private static int _seedCounter;
    private readonly Random _random = new(DebugNextSeedComponent(ref _seedCounter) * 104729);

    // How wide a target box each kind of hittable fixture presents to an incoming shot - all in the
    // same ship-local ("layout") frame as WallBlock.Position/Turret.PeriscopePosition/
    // ShipSystemDevice.Position/door positions themselves, since that's the frame
    // World.Projectiles.cs converts an enemy shot's world-space travel segment into before calling
    // ApplyEnemyAttack.
    private const float WallHitRadius = 0.6f;
    private const float DeviceHitRadius = 0.6f;

    // M74 (humble-soaring-cat.md) - the five "boxes" (reactor/distribution/battery/helm/navigation)
    // collapse into one RepairableBlock candidate kind, generalized over World.SystemRepair.cs's
    // RepairableBlockKinds/RepairableBlock/IsBlockBroken/SetBlockBroken instead of five separately
    // hardcoded HitCandidateKind members - see that file's own doc comment on why this stays
    // "one instance per kind" rather than iterating every Devices entry of a repairable kind.
    // EngineBulkhead/EngineNozzle (Cosmoteer-style marching engines, direct user request) - the two
    // exterior-facing tiles of a ShipEngine, hittable exactly like a WallBlock; Control never is
    // (it's genuinely interior, past both of those).
    private enum HitCandidateKind { WallBlock, Turret, SystemDevice, Door, RepairableBlock, EngineBulkhead, EngineNozzle }
    private readonly record struct HitCandidate(Vec2 LocalPosition, float Radius, HitCandidateKind Kind, string Id);

    // Every physical thing an enemy shell can run into once it's past the shield, in one flat list:
    // outer hull, turret mounts, system-device boxes, doors, and the reactor's own trio of "boxes"
    // (Reactor/Distribution/Battery). Rebuilt each call rather than cached - there are only a few
    // dozen of these per ship, cheap enough to walk fresh every tick a shot is still crossing the
    // hull (World.Projectiles.cs steps a live shot's segment once per tick).
    private IEnumerable<HitCandidate> CollectHitCandidates()
    {
        foreach (var block in Ship.WallBlocks)
            yield return new HitCandidate(block.Position, WallHitRadius, HitCandidateKind.WallBlock, block.Id);

        foreach (var turret in Ship.Turrets)
        {
            var mount = TurretMount.For(Ship.Rooms, Ship.Turrets, turret);
            yield return new HitCandidate(mount.Position, DeviceHitRadius, HitCandidateKind.Turret, turret.Id);
        }

        foreach (var device in Ship.SystemDevices)
            yield return new HitCandidate(device.Position, DeviceHitRadius, HitCandidateKind.SystemDevice, device.Id);

        foreach (var door in AllShipDoors())
            yield return new HitCandidate(door.Position, DeviceHitRadius, HitCandidateKind.Door, door.Id);

        foreach (var kind in RepairableBlockKinds)
        {
            var block = RepairableBlock(kind);
            yield return new HitCandidate(block.Position, DeviceHitRadius, HitCandidateKind.RepairableBlock, block.Id);
        }

        // Cosmoteer-style marching engines (direct user request) - Nozzle sits furthest out (an
        // incoming shot reaches it first), Bulkhead one tile behind it; Control is never a target,
        // it's genuinely interior.
        foreach (var engine in Ship.Engines)
        {
            yield return new HitCandidate(engine.NozzlePosition, WallHitRadius, HitCandidateKind.EngineNozzle, engine.Id);
            yield return new HitCandidate(engine.BulkheadPosition, WallHitRadius, HitCandidateKind.EngineBulkhead, engine.Id);
        }
    }

    // What an enemy shell does once it's past the shield (World.Projectiles.cs) - resolved by where
    // it actually crossed the hull, not a random lottery anymore: an intact wall block stops the
    // shot and takes exactly damage worth of Hp off that one block (each weapon its own number,
    // TurretBalance) rather than an instant full breach - a magnetic cannon's quick weak hits chew
    // through a wall over several shots, a laser's single heavy hit can punch clean through; a wall
    // block already breached is a hole, not a wall, so the shot keeps flying straight through it
    // into the interior. The first still-intact turret mount, system device, door or reactor-room
    // box it comes close to after that takes the hit outright (one shell, no partial damage - those
    // are all plain on/off fixtures, not Hp pools). Same "already broken is a hole, not an obstacle"
    // rule as the wall blocks applies to all of those too - a wrecked turret is debris, a
    // disconnected device has nothing left to cut, a forced-open door is a gap, an already-broken
    // reactor/distribution/battery box is just dead weight - so a shot that keeps hitting an
    // already-dead fixture on the way to something behind it isn't stuck wasting itself there
    // forever, it keeps going and can still reach whatever's deeper in.
    //
    // localFrom/localTo are this tick's short travel segment, not the shot's whole flight - a
    // projectile is stepped as a from-to segment once per tick (World.Projectiles.cs's
    // StepProjectiles), already converted into the same ship-local frame every fixture above is
    // laid out in. This gets called again on the following tick's segment if nothing stopped the
    // shot this tick (an already-open breach, empty space between fixtures), which is what lets a
    // shell travel all the way through the ship over several ticks and either wreck something deep
    // inside or reach the far hull and either breach that or - if that wall's already open too -
    // sail on out the other side and keep flying in open space.
    private bool ApplyEnemyAttack(Vec2 localFrom, Vec2 localTo, float damage)
    {
        var segment = localTo - localFrom;
        var lengthSquared = segment.X * segment.X + segment.Y * segment.Y;
        if (lengthSquared < 1e-6f)
            return false;

        var ordered = CollectHitCandidates()
            .Select(c => (Candidate: c,
                T: ((c.LocalPosition.X - localFrom.X) * segment.X + (c.LocalPosition.Y - localFrom.Y) * segment.Y) / lengthSquared))
            .Where(x => x.T >= 0f && x.T <= 1f)
            .Where(x => (localFrom + segment * x.T - x.Candidate.LocalPosition).Length() <= x.Candidate.Radius)
            .OrderBy(x => x.T);

        foreach (var (candidate, _) in ordered)
        {
            switch (candidate.Kind)
            {
                case HitCandidateKind.WallBlock:
                    if (IsWallBlockBreached(candidate.Id))
                        continue; // already open - the shot passes straight through
                    DamageWallBlock(candidate.Id, damage);
                    return true;

                case HitCandidateKind.Turret:
                    if (_turretRuntimes.TryGetValue(candidate.Id, out var turret) && !turret.Damaged)
                    {
                        turret.Damaged = true;
                        return true;
                    }
                    continue; // already wrecked - debris, not a wall; the shot passes through it

                case HitCandidateKind.SystemDevice:
                    var dropWire = _wires.FirstOrDefault(w => w.ToPin.ComponentId == candidate.Id);
                    if (dropWire is not null && !_wireDamaged[dropWire.Id])
                    {
                        CutWire(dropWire.Id);
                        return true;
                    }
                    continue; // already disconnected - nothing left here to stop it

                case HitCandidateKind.Door:
                    if (!IsDoorDestroyed(candidate.Id))
                    {
                        DamageDoor(candidate.Id);
                        return true;
                    }
                    continue; // forced open already - a gap, not an obstacle

                case HitCandidateKind.RepairableBlock:
                    var blockKind = Ship.Devices.First(d => d.Id == candidate.Id).Kind;
                    if (!IsBlockBroken(blockKind))
                    {
                        SetBlockBroken(blockKind, true);
                        return true;
                    }
                    continue;

                case HitCandidateKind.EngineNozzle:
                    if (IsEngineNozzleBroken(candidate.Id))
                        continue; // already blown open - a hole, not an obstacle
                    DamageEngineNozzle(candidate.Id, damage);
                    return true;

                case HitCandidateKind.EngineBulkhead:
                    if (IsEngineBulkheadBroken(candidate.Id))
                        continue;
                    DamageEngineBulkhead(candidate.Id, damage);
                    return true;
            }
        }

        return false; // nothing in this tick's slice stopped it - still in flight
    }
}
