using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// A hostile ship is a real ship, so it takes damage the way the player's does (World.RoomHp.cs / World.ShipBlasts.cs):
// a shell that reaches it is traced through its hull in the ship's own frame and hurts the first intact thing it meets
// (a wall block, an engine, a gun, the reactor); a compartment that has lost a third of its walls is destroyed in a blast,
// whatever that cuts off from the reactor breaks away as debris; and the reactor's compartment - or the reactor itself -
// going takes the whole ship. Its Ship.Hp readout is just the share of the hull still standing.
public sealed partial class World
{
    // The player's guns deal abstract damage per shot (TurretBalance: 3 per magnetic slug); against real walls of
    // WallBlockMaxHp this is the exchange rate.
    public const float PlayerShotWallDamageScale = 6f;
    private const float EnemyReactorHitRadius = 1.1f;
    // Directly shooting the reactor is the quick way to end a fight, so it hurts more than a wall does.
    private const float EnemyReactorDamageScale = 1.5f;

    // ---- the frame between a hostile ship's field position and its own layout ----

    private static Vec2 EnemyWorldToLocal(EnemyShipRuntime enemy, Vec2 world) =>
        RotateWorldToLocal(world - enemy.Position, enemy.RotationDegrees) + EnemyHullLocalCenter(enemy.Layout);

    private static Vec2 EnemyLocalToWorld(EnemyShipRuntime enemy, Vec2 local) =>
        enemy.Position + RotateLocalToWorld(local - EnemyHullLocalCenter(enemy.Layout), enemy.RotationDegrees);

    // ---- a player's shell hits a hostile ship ----

    private bool TryHitEnemyShips(Vec2 from, Vec2 to, float shotDamage)
    {
        foreach (var enemy in _enemyShips.Where(e => e.Alive))
        {
            if (!SegmentHitsCircle(from, to, enemy.Position, enemy.HullRadius))
                continue;
            if (ApplyAttackToEnemy(enemy, EnemyWorldToLocal(enemy, from), EnemyWorldToLocal(enemy, to), shotDamage * PlayerShotWallDamageScale))
                return true;
        }
        return false;
    }

    private enum EnemyHitKind { Wall, EngineNozzle, EngineBulkhead, Turret, Reactor, Distribution, Helm, JunctionBox, Device }
    private readonly record struct EnemyHit(Vec2 Position, float Radius, EnemyHitKind Kind, string Id);

    // The first intact thing along this tick's slice of the shot (ship-local frame) takes the damage and stops it; a wall that
    // is already breached, or an engine/gun/reactor already dead, is a hole the shell flies on through.
    private bool ApplyAttackToEnemy(EnemyShipRuntime enemy, Vec2 localFrom, Vec2 localTo, float damage)
    {
        var segment = localTo - localFrom;
        var lengthSquared = segment.X * segment.X + segment.Y * segment.Y;
        if (lengthSquared < 1e-6)
            return false;

        var layout = enemy.Layout;
        IEnumerable<EnemyHit> Candidates()
        {
            foreach (var block in layout.WallBlocks)
                if (!enemy.IsWallBlockBreached(block.Id))
                    yield return new EnemyHit(block.Position, WallHitRadius, EnemyHitKind.Wall, block.Id);
            foreach (var engine in layout.Ship.Engines)
            {
                if (enemy.EngineNozzleHp(engine.Id) > 0f)
                    yield return new EnemyHit(engine.NozzlePosition, WallHitRadius, EnemyHitKind.EngineNozzle, engine.Id);
                if (enemy.EngineBulkheadHp(engine.Id) > 0f)
                    yield return new EnemyHit(engine.BulkheadPosition, WallHitRadius, EnemyHitKind.EngineBulkhead, engine.Id);
            }
            foreach (var turret in layout.Ship.Turrets)
                if (enemy.IsTurretWorking(turret.Id))
                    yield return new EnemyHit(TurretMount.For(layout.Rooms, layout.Ship.Turrets, turret).Position, DeviceHitRadius,
                        EnemyHitKind.Turret, turret.Id);
            foreach (var device in layout.Ship.Devices.Where(d => d.Kind == DeviceKind.Reactor))
                if (enemy.FixtureHp(device.Id, EnemyShipRuntime.ReactorMaxHp) > 0f)
                    yield return new EnemyHit(device.Position, EnemyReactorHitRadius, EnemyHitKind.Reactor, device.Id);
            // The rest of the electrics: distribution block, helm console, the junction boxes (щитки) and every wired device.
            foreach (var device in layout.Ship.Devices.Where(d => d.Kind == DeviceKind.Distribution))
                if (!enemy.Grid.DistributionBroken)
                    yield return new EnemyHit(device.Position, DeviceHitRadius, EnemyHitKind.Distribution, device.Id);
            if (!enemy.HelmBroken)
                yield return new EnemyHit(layout.Ship.HelmConsole.Position, DeviceHitRadius, EnemyHitKind.Helm, layout.Ship.HelmConsole.Id);
            foreach (var (system, boxId) in enemy.JunctionBoxOfSystem)
                if (!enemy.CutWires.Contains(EnemyShipRuntime.TrunkWireId(system))
                    && layout.Ship.JunctionBoxes.FirstOrDefault(b => b.Id == boxId) is { } box)
                    yield return new EnemyHit(new Vec2(box.X, box.Y), DeviceHitRadius, EnemyHitKind.JunctionBox, system.ToString());
            foreach (var device in layout.Ship.SystemDevices)
                if (!enemy.CutWires.Contains(EnemyShipRuntime.DropWireId(device.Id)))
                    yield return new EnemyHit(device.Position, DeviceHitRadius, EnemyHitKind.Device, device.Id);
        }

        var ordered = Candidates()
            .Select(c => (Hit: c, T: ((c.Position.X - localFrom.X) * segment.X + (c.Position.Y - localFrom.Y) * segment.Y) / lengthSquared))
            .Where(x => x.T >= 0.0 && x.T <= 1.0)
            .Where(x => (localFrom + segment * x.T - x.Hit.Position).Length() <= x.Hit.Radius)
            .OrderBy(x => x.T)
            .FirstOrDefault();
        if (ordered.Hit.Id is null)
            return false;

        var hit = ordered.Hit;
        switch (hit.Kind)
        {
            case EnemyHitKind.Wall:
                enemy.DamageWallBlock(hit.Id, damage);
                break;
            case EnemyHitKind.EngineNozzle:
                enemy.DamageFixture(hit.Id + ":nozzle", EnginePartMaxHp, damage);
                break;
            case EnemyHitKind.EngineBulkhead:
                enemy.DamageFixture(hit.Id + ":bulkhead", EnginePartMaxHp, damage);
                break;
            case EnemyHitKind.Turret:
                enemy.DamageFixture(hit.Id, EnemyShipRuntime.TurretMaxHp, damage);
                enemy.CutDrop(hit.Id); // the gun's own power lead goes with the hit
                break;
            case EnemyHitKind.Reactor:
                enemy.Grid.Reactor.Broken = true; // the first hit puts it out of action (an engineer can bring it back); more can destroy it
                enemy.DamageFixture(hit.Id, EnemyShipRuntime.ReactorMaxHp, damage * EnemyReactorDamageScale);
                break;
            case EnemyHitKind.Distribution:
                enemy.Grid.DistributionBroken = true;
                break;
            case EnemyHitKind.Helm:
                enemy.HelmBroken = true;
                break;
            case EnemyHitKind.JunctionBox:
                enemy.CutTrunk(Enum.Parse<PowerSystemId>(hit.Id));
                break;
            case EnemyHitKind.Device:
                enemy.CutDrop(hit.Id);
                break;
        }
        return true;
    }

    // ---- compartments of a hostile hull: integrity, destruction, the reactor going up ----

    private void StepEnemyHulls(double deltaSeconds)
    {
        var dt = (float)deltaSeconds;
        foreach (var enemy in _enemyShips.Where(e => e.Alive).ToList())
        {
            if (enemy.ShipExplosion is not null)
            {
                StepEnemyExplosion(enemy, dt);
                continue;
            }

            enemy.Grid.Step(dt); // reactor output, battery, what each system is allocated
            // Nobody works the sliders aboard a hostile ship: once the reactor is back after an outage, power is shared out evenly again.
            if (enemy.Grid.Reactor.CurrentOutput <= 0f)
                enemy.WasWithoutReactorPower = true;
            else if (enemy.WasWithoutReactorPower)
            {
                enemy.WasWithoutReactorPower = false;
                enemy.Grid.SplitEvenly();
            }

            // The reactor itself shot to pieces is as final as losing its compartment.
            foreach (var reactor in enemy.Layout.Ship.Devices.Where(d => d.Kind == DeviceKind.Reactor))
                if (enemy.FixtureHp(reactor.Id, EnemyShipRuntime.ReactorMaxHp) <= 0f
                    && enemy.Layout.Rooms.FirstOrDefault(r => r.Id == reactor.RoomId) is { } reactorRoom)
                {
                    BeginEnemyExplosion(enemy, reactorRoom);
                    break;
                }
            if (enemy.ShipExplosion is not null)
                continue;

            QueueDestroyedEnemyRooms(enemy);
            StepPendingEnemyDestructions(enemy, dt);
            if (enemy.ShipExplosion is null)
                enemy.Ship.SetStructure(EnemyStructure(enemy));
        }
    }

    // 1 = every compartment it started with is standing and whole; the lost ones count as nothing.
    private float EnemyStructure(EnemyShipRuntime enemy) =>
        enemy.Layout.Rooms.Sum(r => EnemyRoomIntegrity(enemy, r.Id)) / Math.Max(1, enemy.InitialRoomCount);

    // 1 = every wall of the compartment intact, 0 = a third of its wall hit points are gone (same rule as the player's).
    private float EnemyRoomIntegrity(EnemyShipRuntime enemy, string roomId)
    {
        var total = 0f;
        var lost = 0f;
        foreach (var block in enemy.Layout.WallBlocks)
        {
            if (block.RoomId != roomId)
                continue;
            total += WallBlockMaxHp;
            lost += WallBlockMaxHp - Math.Clamp(enemy.GetWallBlockHp(block.Id), 0f, WallBlockMaxHp);
        }
        foreach (var engine in enemy.Layout.Ship.Engines)
        {
            if (engine.RoomId != roomId)
                continue;
            total += 2f * EnginePartMaxHp;
            lost += EnginePartMaxHp - Math.Clamp(enemy.EngineBulkheadHp(engine.Id), 0f, EnginePartMaxHp);
            lost += EnginePartMaxHp - Math.Clamp(enemy.EngineNozzleHp(engine.Id), 0f, EnginePartMaxHp);
        }
        return total <= 0f ? 1f : Math.Clamp(1f - lost / (total * RoomDestructionWallShare), 0f, 1f);
    }

    private void QueueDestroyedEnemyRooms(EnemyShipRuntime enemy)
    {
        foreach (var room in enemy.Layout.Rooms)
            if (!enemy.DoomedRooms.Contains(room.Id) && EnemyRoomIntegrity(enemy, room.Id) <= 0f)
            {
                enemy.DoomedRooms.Add(room.Id);
                enemy.PendingDestructions.Add((room.Id, enemy.PendingDestructions.Count == 0 ? 0f : ChainDelaySeconds));
            }
    }

    private void StepPendingEnemyDestructions(EnemyShipRuntime enemy, float dt)
    {
        if (enemy.PendingDestructions.Count == 0)
            return;
        for (var i = 0; i < enemy.PendingDestructions.Count; i++)
            enemy.PendingDestructions[i] = (enemy.PendingDestructions[i].RoomId, enemy.PendingDestructions[i].Delay - dt);
        var due = enemy.PendingDestructions.Where(p => p.Delay <= 0f).ToList();
        enemy.PendingDestructions.RemoveAll(p => p.Delay <= 0f);
        foreach (var (roomId, _) in due)
        {
            if (enemy.ShipExplosion is not null)
                return;
            DetonateEnemyRoom(enemy, roomId);
        }
    }

    private static bool IsEnemyReactorRoom(EnemyShipRuntime enemy, string roomId) =>
        enemy.Layout.Ship.Devices.FirstOrDefault(d => d.Kind == DeviceKind.Reactor)?.RoomId == roomId;

    private void DetonateEnemyRoom(EnemyShipRuntime enemy, string roomId)
    {
        var layout = enemy.Layout;
        var room = layout.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null)
        {
            enemy.DoomedRooms.Remove(roomId);
            return;
        }
        if (IsEnemyReactorRoom(enemy, roomId))
        {
            BeginEnemyExplosion(enemy, room);
            return;
        }

        var center = room.Center;
        var radius = BlastRadiusFor(room);
        AddBlast(EnemyLocalToWorld(enemy, center), radius, ShipBlastKind.Compartment, inField: true);
        KillEnemyPeopleIn(enemy, roomId);

        if (!ShipDetachment.TryCompute(layout.Ship, roomId, out var shrunk, out var detached))
        {
            // What is left would not be a ship any more (it held the only helm / engine / generator it needs): it goes up whole.
            BeginEnemyExplosion(enemy, room);
            return;
        }

        var detachedIds = detached.Select(r => r.Id).ToHashSet();
        foreach (var id in detachedIds.Where(id => id != roomId))
            KillEnemyPeopleIn(enemy, id, ejectBoarders: true);

        // Whatever the blast cut loose from the reactor flies off as its own piece.
        var cutOff = detached.Where(r => r.Id != roomId).ToList();
        if (cutOff.Count > 0)
            SpawnDebrisFragment(cutOff, center, EnemyHullLocalCenter(layout), enemy.Position, enemy.RotationDegrees, enemy.Velocity);

        var oldCenter = EnemyHullLocalCenter(layout);
        var newLayout = EnemyShipLayout.FromDefinition(shrunk, layout.Name);
        var shift = EnemyHullLocalCenter(newLayout) - oldCenter;
        // The hull's reference point is the centre of what is left: move it so every surviving wall stays exactly where it was.
        enemy.Position += RotateLocalToWorld(shift, enemy.RotationDegrees);
        foreach (var character in _characters.Values.Where(c => c.EvaAttachedTo == EvaAttachment.EnemyShip))
            character.EvaLocalOffset -= shift;
        enemy.ReplaceLayout(newLayout);
        enemy.DoomedRooms.RemoveWhere(id => detachedIds.Contains(id));

        ApplyEnemyBlastDamage(enemy, center, radius);
    }

    // Everyone caught in a compartment that is gone: crew die, a boarder inside dies with it (or, for a piece that merely
    // breaks away, is thrown clear into space).
    private void KillEnemyPeopleIn(EnemyShipRuntime enemy, string roomId, bool ejectBoarders = false)
    {
        foreach (var crew in enemy.Crew.Where(c => c.Alive && c.RoomId == roomId))
            crew.Health = 0f;
        foreach (var character in _characters.Values.Where(c => c.OnEnemyShip && c.RoomId == roomId).ToList())
        {
            if (ejectBoarders)
                EjectFromEnemyShip(character);
            else
                character.Health = 0f;
        }
        CaptureIfCrewWiped(enemy);
    }

    // Walls and people near the blast are hurt in proportion to how close they are; a wall that falls may finish its
    // compartment, which the next integrity check queues as the next link of the chain.
    private void ApplyEnemyBlastDamage(EnemyShipRuntime enemy, Vec2 center, float radius)
    {
        var layout = enemy.Layout;
        foreach (var block in layout.WallBlocks)
        {
            var distance = (float)(block.Position - center).Length();
            if (distance < radius)
                enemy.DamageWallBlock(block.Id, BlastWallDamage * (1f - distance / radius));
        }
        foreach (var engine in layout.Ship.Engines)
        {
            var bulkhead = (float)(engine.BulkheadPosition - center).Length();
            if (bulkhead < radius)
                enemy.DamageFixture(engine.Id + ":bulkhead", EnginePartMaxHp, BlastWallDamage * (1f - bulkhead / radius));
            var nozzle = (float)(engine.NozzlePosition - center).Length();
            if (nozzle < radius)
                enemy.DamageFixture(engine.Id + ":nozzle", EnginePartMaxHp, BlastWallDamage * (1f - nozzle / radius));
        }
        foreach (var crew in enemy.Crew.Where(c => c.Alive))
        {
            var distance = (float)(crew.Position - center).Length();
            if (distance < radius)
                crew.Health = Math.Max(0f, crew.Health - BlastCrewDamage * (1f - distance / radius));
        }
        foreach (var character in _characters.Values.Where(c => c.OnEnemyShip && !c.IsDead))
        {
            var distance = (float)(character.Position - center).Length();
            if (distance < radius)
                character.Health = Math.Max(0f, character.Health - BlastCrewDamage * (1f - distance / radius));
        }
        CaptureIfCrewWiped(enemy);
    }

    // ---- the reactor goes: the whole ship ----

    private void BeginEnemyExplosion(EnemyShipRuntime enemy, Room reactorRoom)
    {
        if (enemy.ShipExplosion is not null)
            return;
        var origin = reactorRoom.Center;
        var rooms = enemy.Layout.Rooms.OrderBy(r => (r.Center - origin).Length()).ToList();
        AddBlast(EnemyLocalToWorld(enemy, origin), BlastRadiusFor(reactorRoom) * 1.7f, ShipBlastKind.Reactor, inField: true);
        enemy.ShipExplosion = new EnemyShipRuntime.Explosion
        {
            Rooms = rooms
                .Select((room, i) => (room, ShipExplosionSpreadSeconds * i / Math.Max(1, rooms.Count), false))
                .ToList(),
        };
        enemy.PendingDestructions.Clear();
    }

    // Each room blows in its turn, taking everyone in it; when it is over the ship is gone.
    private void StepEnemyExplosion(EnemyShipRuntime enemy, float dt)
    {
        var explosion = enemy.ShipExplosion!;
        explosion.Elapsed += dt;
        for (var i = 0; i < explosion.Rooms.Count; i++)
        {
            var (room, at, fired) = explosion.Rooms[i];
            if (fired || explosion.Elapsed < at)
                continue;
            explosion.Rooms[i] = (room, at, true);
            AddBlast(EnemyLocalToWorld(enemy, room.Center), BlastRadiusFor(room), ShipBlastKind.Compartment, inField: true);
            KillEnemyPeopleIn(enemy, room.Id);
        }
        if (explosion.Elapsed >= ShipExplosionTotalSeconds)
            enemy.Ship.ApplyDamage(enemy.Ship.MaxHp);
    }

    // ---- test preconditions (same convention as the other Debug* setters) ----

    // Shoots the boardable hull along a segment given in ITS OWN frame, as a shell of the given wall damage would.
    public bool DebugShootEnemyLocal(Vec2 localFrom, Vec2 localTo, float wallDamage) =>
        BoardableEnemy is { } enemy && ApplyAttackToEnemy(enemy, localFrom, localTo, wallDamage);

    // Fully breaches every wall of one compartment of the boardable hull, the way a long fight eventually would.
    public void DebugDestroyEnemyRoomWallBlocks(string roomId)
    {
        if (BoardableEnemy is not { } enemy)
            return;
        foreach (var block in enemy.Layout.WallBlocks.Where(b => b.RoomId == roomId))
            enemy.DamageWallBlock(block.Id, WallBlockMaxHp);
    }

    // Knocks out every engine of the boardable hull.
    public void DebugBreakEnemyEngines()
    {
        if (BoardableEnemy is not { } enemy)
            return;
        foreach (var engine in enemy.Layout.Ship.Engines)
        {
            enemy.DamageFixture(engine.Id + ":bulkhead", EnginePartMaxHp, EnginePartMaxHp);
            enemy.DamageFixture(engine.Id + ":nozzle", EnginePartMaxHp, EnginePartMaxHp);
        }
    }

    // Kills the boardable hull's crew of one role (a gunner, the captain...).
    public void DebugKillEnemyCrewWithRole(EnemyCrewRole role)
    {
        if (BoardableEnemy is not { } enemy)
            return;
        foreach (var crew in enemy.Crew.Where(c => c.Spawn.Role == role))
            crew.Health = 0f;
    }

    public (bool ReactorBroken, bool DistributionBroken, bool HelmBroken, int CutWires, int GunsFiring, int EnginesRunning) DebugEnemyPower() =>
        BoardableEnemy is not { } enemy
            ? default
            : (enemy.Grid.Reactor.Broken, enemy.Grid.DistributionBroken, enemy.HelmBroken, enemy.CutWires.Count,
                enemy.Layout.Ship.Turrets.Count(t => enemy.IsTurretFiring(t.Id)),
                enemy.Layout.Ship.Engines.Count(e => enemy.IsEngineRunning(e.Id)));

    // Test observable: all hit points of the boardable hull that shots can take (walls, engine parts, guns, reactor).
    public float DebugEnemyHullHp()
    {
        if (BoardableEnemy is not { } enemy)
            return 0f;
        var ship = enemy.Layout.Ship;
        return enemy.Layout.WallBlocks.Sum(b => enemy.GetWallBlockHp(b.Id))
            + ship.Engines.Sum(e => enemy.EngineBulkheadHp(e.Id) + enemy.EngineNozzleHp(e.Id))
            + ship.Turrets.Sum(t => enemy.FixtureHp(t.Id, EnemyShipRuntime.TurretMaxHp))
            + ship.Devices.Where(d => d.Kind == DeviceKind.Reactor).Sum(d => enemy.FixtureHp(d.Id, EnemyShipRuntime.ReactorMaxHp));
    }

    public int DebugEnemyShipsInField => _enemyShips.Count(e => e.Alive);

    // ---- what the client needs to draw the inside of a hostile ship like the player's ----

    // The hull's definition changes rarely, so it is sent every Nth tick only; the client keeps the last one (by LayoutVersion).
    private const int EnemyDefinitionSendEveryTicks = 15;

    private CustomShipDefinition? CreateEnemyDefinition() =>
        BoardableEnemy is { } enemy && Tick % EnemyDefinitionSendEveryTicks == 0 ? enemy.Layout.Definition : null;

    // Guns, engines and system devices that are broken or without power right now (their lamps are dark).
    private IReadOnlyList<string> CreateEnemyNotWorkingIds()
    {
        if (BoardableEnemy is not { } enemy)
            return Array.Empty<string>();
        var ship = enemy.Layout.Ship;
        return ship.Turrets.Where(t => !enemy.IsTurretFiring(t.Id)).Select(t => t.Id)
            .Concat(ship.Engines.Where(e => !enemy.IsEngineRunning(e.Id)).Select(e => e.Id))
            .Concat(ship.SystemDevices.Where(d => !enemy.HasPower(d.Id)).Select(d => d.Id))
            .ToArray();
    }
}
