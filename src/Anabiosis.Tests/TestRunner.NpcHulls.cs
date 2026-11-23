using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Networking;
using Anabiosis.Shared.Protocol;

// Ambient ships as real ships (World.NpcHulls.cs): a hull with compartments once somebody is near, damage from the player and
// from asteroids, and flying round rocks instead of through them.
internal static partial class TestRunner
{
    private static World NpcHullWorld(out string cargoId, out NpcShipFieldState start)
    {
        var world = new World();
        world.SpawnCharacter(1);
        world.Step(RealtimeStep);
        start = world.CreateSnapshot().NpcShips.First(n => n.Kind == NpcShipKind.Cargo);
        cargoId = start.Id;
        return world;
    }

    // Far from anyone an ambient ship is only a point; once the player is near it has real compartments and full health.
    private static bool World_NpcShips_HullBuiltOnlyWhenPlayerIsNear()
    {
        var world = NpcHullWorld(out var id, out var start);
        var farAway = world.CreateSnapshot().NpcShips.Where(n => n.Rooms is null).Any();

        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        world.DebugPlaceShip(new Vec2(start.X, start.Y));
        world.Step(RealtimeStep);
        var near = world.CreateSnapshot().NpcShips.First(n => n.Id == id);
        return farAway && near.Rooms is { Count: > 5 } && near.Hp > 0f && near.Hp >= near.MaxHp - 0.01f && !near.IsProvoked;
    }

    // A shell through an ambient ship tears its plating and provokes it; a shell that misses does neither.
    private static bool World_NpcShips_PlayerShotDamagesHullAndProvokesIt()
    {
        var world = NpcHullWorld(out var id, out var start);
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        // Parked far enough off that the ship is not yet a fight, but close enough that its hull is built.
        var npcAt = new Vec2(start.X, start.Y);
        world.DebugPlaceShip(npcAt + new Vec2(300, 0));
        world.Step(RealtimeStep);
        var now = world.CreateSnapshot().NpcShips.First(n => n.Id == id);
        var centre = new Vec2(now.X, now.Y);

        var missed = world.DebugShootAtShips(centre + new Vec2(0, 400), centre + new Vec2(0, 380), 10f);
        var untouched = !world.CreateSnapshot().NpcShips.First(n => n.Id == id).IsProvoked && world.DebugNpcWallDamage(id) == 0.0;

        var hit = false;
        for (var i = 0; i < 40 && !hit; i++)
            hit = world.DebugShootAtShips(centre + new Vec2(-60, i * 0.5 - 10), centre + new Vec2(60, i * 0.5 - 10), 20f);
        var after = world.CreateSnapshot().NpcShips.FirstOrDefault(n => n.Id == id);
        return !missed && untouched && hit && after is { IsProvoked: true } && world.DebugNpcWallDamage(id) > 0.0;
    }

    // A provoked ship that has closed becomes an ordinary raider - the same hull, wounds and crew carried into the fight.
    private static bool World_NpcShips_ProvokedShipBecomesRaiderWithItsWounds()
    {
        var world = NpcHullWorld(out var id, out var start);
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        var npcAt = new Vec2(start.X, start.Y);
        world.DebugPlaceShip(npcAt + new Vec2(300, 0));
        world.Step(RealtimeStep);
        var centre = new Vec2(world.CreateSnapshot().NpcShips.First(n => n.Id == id).X, world.CreateSnapshot().NpcShips.First(n => n.Id == id).Y);
        var hit = false;
        for (var i = 0; i < 40 && !hit; i++)
            hit = world.DebugShootAtShips(centre + new Vec2(-60, i * 0.5 - 10), centre + new Vec2(60, i * 0.5 - 10), 20f);
        if (!hit)
            return false;
        var woundsBefore = world.DebugNpcWallDamage(id);

        // It flies at the player now; let it arrive (it is a few hundred units away at a few units a second).
        for (var i = 0; i < 30 * 200 && !world.IsInBattle; i++)
            world.Step(RealtimeStep);
        var snapshot = world.CreateSnapshot();
        return world.IsInBattle
            && snapshot.NpcShips.All(n => n.Id != id)
            && snapshot.EnemyShip.Ships.Any(e => e.Id == id)
            && woundsBefore > 0.0;
    }

    // Left alone, a ship whose straight line to its waypoint runs through an asteroid goes round it.
    private static bool World_NpcShips_FlyRoundAsteroidsInsteadOfThroughThem()
    {
        var world = NpcHullWorld(out var id, out var start);
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        var rock = world.AsteroidField.Asteroids.OrderByDescending(a => a.Radius).First();
        var from = rock.Position + new Vec2(-260, 0);
        var to = rock.Position + new Vec2(260, 0);
        world.DebugPlaceNpc(id, from, Vec2.Zero, to);
        world.DebugPlaceShip(from + new Vec2(0, 400)); // near enough for a hull, far enough not to be a fight
        var closest = double.MaxValue;
        for (var i = 0; i < 30 * 160; i++)
        {
            world.Step(RealtimeStep);
            var npc = world.CreateSnapshot().NpcShips.FirstOrDefault(n => n.Id == id);
            if (npc is null)
                return false;
            closest = Math.Min(closest, (new Vec2(npc.X, npc.Y) - rock.Position).Length());
            if (npc.X > rock.X + 100)
                break;
        }
        // Never touched the rock (the hull's body reaches about 0.7 of its radius) and took no damage from it.
        return closest > rock.Radius + 10 && world.DebugNpcWallDamage(id) == 0.0;
    }

    // Driven into a rock anyway, it takes the blow on its plating, not for free.
    private static bool World_NpcShips_AsteroidImpactTearsPlating()
    {
        var world = NpcHullWorld(out var id, out var start);
        world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
        world.Step(RealtimeStep);
        var rock = world.AsteroidField.Asteroids.OrderByDescending(a => a.Radius).First();
        world.DebugPlaceShip(rock.Position + new Vec2(-200, 400));
        world.DebugPlaceNpc(id, rock.Position + new Vec2(-rock.Radius - 8, 0), new Vec2(4, 0), rock.Position);
        world.Step(RealtimeStep); // builds the hull and runs the first scrape at once
        for (var i = 0; i < 20; i++)
            world.Step(RealtimeStep);
        return world.DebugNpcWallDamage(id) > 0.0;
    }
}