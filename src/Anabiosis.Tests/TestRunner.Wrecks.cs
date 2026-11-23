using Anabiosis.Server;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

// The blown-up ship place (World.Wrecks.cs): a dead hull that appears when the ship comes near, can be
// entered through a hole and searched for salvage.
internal static partial class TestRunner
{
    private const int WreckWorldSeed = 4242;

    private static (World World, PointOfInterest Wreck) WorldBesideAWreck()
    {
        var world = new World(worldSeed: WreckWorldSeed);
        world.SpawnCharacter(1);
        world.DebugOpenAllDoors();
        EquipSuit(world, 1);
        if (world.IsDocked)
        {
            world.ApplyCommand(1, new ClientCommand(1, DockPressed: true));
            world.Step(RealtimeStep);
        }
        var wreck = world.GalaxyMap.GetSystem("sol").Pois.First(p => p.Kind == PoiKind.Wreck);
        world.DebugPlaceShip(wreck.Position + new Vec2(160, 0));
        world.Step(RealtimeStep);
        return (world, wreck);
    }

    // Flying up to the place puts a dead hull in the field: no crew, no power, holed plating, salvage on
    // its decks - and it is not a fight. Flying off puts it away again.
    private static bool World_Wreck_AppearsOnApproach_DeadAndHoled_AndGoesAwayAgain()
    {
        var (world, wreck) = WorldBesideAWreck();
        var state = world.DebugWreck();
        var snapshot = world.CreateSnapshot();
        var ship = snapshot.EnemyShip.Ships.SingleOrDefault();
        if (state is not { } s || ship is null)
            return false;
        var looksRight = s.Crew == 0 && s.ReactorBroken && s.BreachedOuterBlocks > 0 && s.Loot.Count > 0
            && ship.IsDerelict && ship.IsBoardable && snapshot.EnemyShip.IsDerelict
            && snapshot.EnemyShip.Loot!.Count == s.Loot.Count
            && !world.IsInBattle
            && Math.Abs(ship.X - wreck.X) < 0.01 && Math.Abs(ship.Y - wreck.Y) < 0.01;

        // It never moves or shoots: a few seconds on, the player ship is untouched and the hull is where it was.
        var hullBefore = world.CreateSnapshot().RoomHp!.Sum(r => r.Hp);
        StepFor(world, 5 * 30);
        var later = world.CreateSnapshot();
        var inert = Math.Abs(later.EnemyShip.Ships.Single().X - wreck.X) < 0.01
            && Math.Abs(later.RoomHp!.Sum(r => r.Hp) - hullBefore) < 0.01f;

        world.DebugPlaceShip(wreck.Position + new Vec2(900, 0));
        world.Step(RealtimeStep);
        return looksRight && inert && !world.WreckInField && world.CreateSnapshot().EnemyShip.Ships.Count == 0;
    }

    // In through a hole in a suit, over to a piece of salvage, pick it up: it is in the pockets, gone from
    // the deck, and the save remembers it was taken.
    private static bool World_Wreck_BoardThroughAHole_AndPickUpSalvage()
    {
        var (world, _) = WorldBesideAWreck();
        var layout = world.EnemyShipLayout;
        var breached = world.CreateSnapshot().EnemyShip.WallBlockStates.Where(b => b.Hp <= 0f).Select(b => b.Id).ToHashSet();

        // The hole with the shortest walk to a piece of salvage in the compartment it opens into (devices wall
        // parts of the hull off, and a long route through half-blocked tiles is not what this test is about).
        WallBlock? entry = null;
        WreckLootState? target = null;
        var shortest = int.MaxValue;
        foreach (var block in layout.WallBlocks.Where(b => !b.IsInterior && breached.Contains(b.Id)))
        {
            if (TilePathfinder.NearestPassable(layout.Tiles, TilePathfinder.TileAt(block.Position), null, 2) is not { } inside)
                continue;
            foreach (var loot in world.DebugWreck()!.Value.Loot.Where(l => l.RoomId == block.RoomId))
            {
                var tile = TilePathfinder.TileAt(loot.Position);
                var steps = tile == inside ? 0 : TilePathfinder.FindPath(layout.Tiles, inside, tile)?.Count ?? int.MaxValue;
                if (steps >= shortest)
                    continue;
                shortest = steps;
                entry = block;
                target = loot;
            }
        }
        if (entry is null || target is null)
            return false;

        var localCenter = layout.GetLocalBounds().Center;
        world.DebugPlaceEvaCharacter(1, EnemyHullBlockWorldPosition(world, entry.Position - localCenter), attachToEnemyShip: true);
        var inward = (localCenter - entry.Position).Normalized();
        for (var i = 0; i < 40 && !world.CreateSnapshot().Characters.Single().OnEnemyShip; i++)
        {
            var hull = world.CreateSnapshot().EnemyShip.Ships.Single();
            var dir = RotateLocalToWorld(inward, hull.RotationDegrees);
            world.ApplyCommand(1, new ClientCommand(1, MoveX: (float)dir.X, MoveY: (float)dir.Y));
            world.Step(RealtimeStep);
        }
        if (!world.CreateSnapshot().Characters.Single().OnEnemyShip)
            return false;

        for (var i = 0; i < 40 * 30; i++)
        {
            var me = world.CreateSnapshot().Characters.Single();
            var mePos = new Vec2(me.X, me.Y);
            if ((target.Position - mePos).Length() < 0.8)
                break;
            var path = TilePathfinder.FindPath(layout.Tiles, TilePathfinder.TileAt(mePos), TilePathfinder.TileAt(target.Position));
            var waypoint = path is { Count: > 1 } ? TilePathfinder.CenterOf(path[0]) : target.Position;
            var dir = (waypoint - mePos).Normalized();
            world.ApplyCommand(1, new ClientCommand(1, MoveX: (float)dir.X, MoveY: (float)dir.Y));
            world.Step(RealtimeStep);
        }

        var lootBefore = world.DebugWreck()!.Value.Loot.Count;
        var carriedBefore = world.CreateSnapshot().Characters.Single().Inventory!.MainSlots.Count(s => s == target.Item);
        world.ApplyCommand(1, new ClientCommand(1, InteractPressed: true));
        world.Step(RealtimeStep);

        var after = world.DebugWreck()!.Value.Loot;
        var carriedAfter = world.CreateSnapshot().Characters.Single().Inventory!.MainSlots.Count(s => s == target.Item);
        var saved = world.CreateSave().Exploration!.Single(e => e.SystemId == "sol").TakenLootIds;
        return after.Count == lootBefore - 1 && carriedAfter == carriedBefore + 1
            && saved is not null && saved.Count == 1 && after.All(l => l.Id != saved[0]);
    }

    // The same wreck every time: put away and brought back, it has the same salvage in the same places.
    private static bool World_Wreck_IsTheSameWreckWhenTheShipComesBack()
    {
        var (world, wreck) = WorldBesideAWreck();
        var first = world.DebugWreck()!.Value;
        world.DebugPlaceShip(wreck.Position + new Vec2(900, 0));
        world.Step(RealtimeStep);
        world.DebugPlaceShip(wreck.Position + new Vec2(-200, 0));
        world.Step(RealtimeStep);
        var second = world.DebugWreck();
        return second is { } again && again.Loot.SequenceEqual(first.Loot) && again.BreachedOuterBlocks == first.BreachedOuterBlocks;
    }
}
