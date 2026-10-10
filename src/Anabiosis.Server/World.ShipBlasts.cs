using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// What happens when a compartment is destroyed (World.RoomHp.cs decides WHEN): a large explosion where it
// stood that hurts everything around it, its place left as open space, and the hull possibly breaking into pieces.
// And when it was the reactor's compartment - the whole ship goes up, room after room, and the crew wakes
// at the last station on a restored ship (there was no defeat flow before; this is the same "last dock is
// the save point" rule game_design.md section 5 already gives the autosave).
//
// Damage from a blast can finish a neighbouring compartment, which then explodes in its turn: chains are
// staggered by ChainDelaySeconds so a run of explosions reads as a chain instead of one flash, and so one
// destruction never recurses into the next inside a single call.
public sealed partial class World
{
    // The blast's wall damage at its very centre, falling off linearly to nothing at the edge - a standard wall
    // block holds 100, so the closest walls are mostly gone, the rest of the neighbourhood is dented.
    private const float BlastWallDamage = 70f;
    private const float BlastCrewDamage = 80f;
    private const float ChainDelaySeconds = 0.35f;
    private const float BlastLifetimeSeconds = 2.6f;
    // The whole-ship explosion: rooms go one after another outward from the reactor over this long, and the
    // crew comes back after this long in total.
    private const float ShipExplosionSpreadSeconds = 2.4f;
    private const float ShipExplosionTotalSeconds = 4.5f;

    private sealed class ShipBlast
    {
        public required string Id;
        public Vec2 Position;     // ship-local (layout) frame, like every room/wall coordinate
        public float Radius;
        public float Age;
        public ShipBlastKind Kind;
        public bool InField;      // Position is in field (world) space, not the ship's frame
    }

    private sealed class ShipExplosion
    {
        public float Elapsed;
        public required List<(Room Room, float At, bool Fired)> Rooms;
    }

    private readonly List<ShipBlast> _shipBlasts = new();
    private int _nextBlastId;
    // Rooms whose destruction has happened, or is queued, or failed to change the hull - never exploded twice.
    private readonly HashSet<string> _doomedRooms = new();
    private readonly List<(string RoomId, float Delay)> _pendingDestructions = new();
    private bool _applyingBlast;
    private ShipExplosion? _shipExplosion;

    // Where the ship is put back after it blows up: the hull as it was at the last dock, and that station.
    private (CustomShipDefinition Definition, string PointId)? _dockCheckpoint;

    private void RecordDockCheckpoint(string pointId) => _dockCheckpoint = (_customShipDefinition, pointId);

    // THE reactor: the ship's primary one (the same anchor World.ShipDebris.cs measures reachability from). A bonus
    // reactor compartment built later is just a compartment - it explodes like any other without taking the ship along.
    private bool IsReactorRoom(string roomId) =>
        Ship.Devices.FirstOrDefault(d => d.Kind == DeviceKind.Reactor)?.RoomId == roomId;

    public bool IsShipExploding => _shipExplosion is not null;

    private static float BlastRadiusFor(Room room) => Math.Clamp(Math.Max(room.Width, room.Height) * 0.75f + 2.5f, 4f, 10f);

    // Entry point from World.RoomHp.cs. The first compartment killed by a hit goes at once; one killed by
    // another explosion's blast is queued so it follows a moment later.
    private void DestroyRoom(string roomId)
    {
        if (_shipExplosion is not null || Ship.Rooms.All(r => r.Id != roomId))
            return;
        _doomedRooms.Add(roomId);
        if (_applyingBlast)
        {
            _pendingDestructions.Add((roomId, ChainDelaySeconds));
            return;
        }
        DetonateRoom(roomId);
    }

    private void DetonateRoom(string roomId)
    {
        var room = Ship.Rooms.FirstOrDefault(r => r.Id == roomId);
        if (room is null)
        {
            _doomedRooms.Remove(roomId);
            return;
        }
        if (IsReactorRoom(roomId))
        {
            BeginShipExplosion(room);
            return;
        }

        var center = room.Center;
        var radius = BlastRadiusFor(room);
        AddBlast(center, radius, ShipBlastKind.Compartment);
        KillCharactersIn(room.Id);
        if (ExplodeRoom(roomId, center))
        {
            _doomedRooms.Remove(roomId); // gone for good - ids are not reused
            ApplyBlastDamage(center, radius);
            return;
        }

        // The compartment cannot be taken off the hull because what is left would not be a ship any more (it held the
        // only helm / engine / oxygen generator / ... that the ship needs, or it was the last compartment): a ship
        // that has lost something vital to staying one goes up completely, like it does when the reactor goes.
        BeginShipExplosion(room);
    }

    private void AddBlast(Vec2 position, float radius, ShipBlastKind kind, bool inField = false) =>
        _shipBlasts.Add(new ShipBlast { Id = $"blast-{_nextBlastId++}", Position = position, Radius = radius, Kind = kind, InField = inField });

    private void KillCharactersIn(string roomId)
    {
        foreach (var character in _characters.Values)
            if (!character.OnStation && !character.IsOutside && !character.OnEnemyShip && character.RoomId == roomId)
                character.Health = 0f;
    }

    // Walls and crew near the centre are hurt in proportion to how close they are. A wall that falls to
    // nothing may finish ITS compartment, which is then queued as the next link of the chain.
    private void ApplyBlastDamage(Vec2 center, float radius)
    {
        _applyingBlast = true;
        try
        {
            foreach (var block in Ship.WallBlocks.ToList())
            {
                var distance = (float)(block.Position - center).Length();
                if (distance < radius)
                    DamageWallBlock(block.Id, BlastWallDamage * (1f - distance / radius));
            }
            foreach (var engine in Ship.Engines.ToList())
            {
                var bulkhead = (float)(engine.BulkheadPosition - center).Length();
                if (bulkhead < radius)
                    DamageEngineBulkhead(engine.Id, BlastWallDamage * (1f - bulkhead / radius));
                var nozzle = (float)(engine.NozzlePosition - center).Length();
                if (nozzle < radius)
                    DamageEngineNozzle(engine.Id, BlastWallDamage * (1f - nozzle / radius));
            }
            foreach (var character in _characters.Values)
            {
                if (character.OnStation || character.IsOutside || character.OnEnemyShip || character.IsDead)
                    continue;
                var distance = (float)(character.Position - center).Length();
                if (distance < radius)
                    character.Health = Math.Max(0f, character.Health - BlastCrewDamage * (1f - distance / radius));
            }
        }
        finally
        {
            _applyingBlast = false;
        }
    }

    // ---- the reactor goes: the whole ship ----

    private void BeginShipExplosion(Room reactorRoom)
    {
        var origin = reactorRoom.Center;
        var rooms = Ship.Rooms.OrderBy(r => (r.Center - origin).Length()).ToList();
        AddBlast(origin, BlastRadiusFor(reactorRoom) * 1.7f, ShipBlastKind.Reactor);
        _shipExplosion = new ShipExplosion
        {
            Rooms = rooms
                .Select((room, i) => (room, ShipExplosionSpreadSeconds * i / Math.Max(1, rooms.Count), false))
                .ToList(),
        };
        _pendingDestructions.Clear();
    }

    // Ticks the dying ship: each room blows in its turn, taking its crew with it, and once it is all over the
    // crew is back on a restored hull.
    private void StepShipExplosion(double deltaSeconds)
    {
        if (_shipExplosion is not { } explosion)
            return;
        explosion.Elapsed += (float)deltaSeconds;
        for (var i = 0; i < explosion.Rooms.Count; i++)
        {
            var (room, at, fired) = explosion.Rooms[i];
            if (fired || explosion.Elapsed < at)
                continue;
            explosion.Rooms[i] = (room, at, true);
            AddBlast(room.Center, BlastRadiusFor(room), ShipBlastKind.Compartment);
            KillCharactersIn(room.Id);
        }
        if (explosion.Elapsed >= ShipExplosionTotalSeconds)
            RecoverFromShipExplosion();
    }

    // The ship is back at the last station it docked at, as it was then, with its crew alive in the cockpit. The
    // wallet, quests and standing are untouched - only the hull and its state are rewound.
    private void RecoverFromShipExplosion()
    {
        var (definition, pointId) = _dockCheckpoint ?? (ShipDefaultHull.Definition, GalaxyMap.HomePointId);

        CurrentShipKind = ShipKind.Custom;
        _customShipDefinition = definition;
        Ship = Ship.FromCustomDefinition(definition);
        _turretRuntimes.Clear();
        foreach (var turret in Ship.Turrets)
            _turretRuntimes[turret.Id] = new TurretRuntime(turret);
        _turretAimInput.Clear();
        _cardGame = null;
        InitializeShipState();
        RecomputeDeviceBonuses();

        _shipDebris.Clear();
        _doomedRooms.Clear();
        _pendingDestructions.Clear();
        _shipExplosion = null;
        _shipVelocity = Vec2.Zero;
        EnterStation(pointId);

        foreach (var character in _characters.Values)
            RespawnCharacter(character);
    }

    // ---- per-tick ----

    private void StepShipBlasts(double deltaSeconds)
    {
        var dt = (float)deltaSeconds;
        foreach (var blast in _shipBlasts)
            blast.Age += dt;
        _shipBlasts.RemoveAll(b => b.Age > BlastLifetimeSeconds);

        StepShipExplosion(deltaSeconds);

        if (_pendingDestructions.Count == 0)
            return;
        for (var i = 0; i < _pendingDestructions.Count; i++)
            _pendingDestructions[i] = (_pendingDestructions[i].RoomId, _pendingDestructions[i].Delay - dt);
        var due = _pendingDestructions.Where(p => p.Delay <= 0f).ToList();
        _pendingDestructions.RemoveAll(p => p.Delay <= 0f);
        foreach (var (roomId, _) in due)
            if (_shipExplosion is null)
                DetonateRoom(roomId);
    }

    // The explosions of the last couple of seconds, for the client to draw (kept in every snapshot while they
    // are fresh rather than sent once, so a dropped snapshot cannot lose one).
    private IReadOnlyList<ShipBlastState> CreateShipBlastStates() =>
        _shipBlasts.Select(b => new ShipBlastState(b.Id, (float)b.Position.X, (float)b.Position.Y, b.Radius, b.Age, b.Kind, b.InField)).ToArray();
}
