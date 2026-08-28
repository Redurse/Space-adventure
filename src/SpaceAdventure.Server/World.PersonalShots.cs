using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Personal weapons fire something you can see. They used to resolve instantly against the nearest
// defender in the room - point at nothing in particular, press Space, the closest enemy lost
// health - which made a firefight a damage race with no aiming in it at all.
//
// Now a shot is a body that leaves the muzzle along the cursor direction, crosses the room at its
// own speed and hits whatever is actually in the way. Missing is possible, and a wall still stops
// everything: a bullet dies at the edge of the compartment it was fired in, which is the same rule
// the old same-room check enforced, just expressed as physics instead of as a filter.
public sealed partial class World
{
    private const float BulletRadius = 0.35f; // a body is about this wide, so this is "close enough to hit"
    private const float ShotLifetimeSeconds = 3f;

    private readonly List<PersonalShotRuntime> _personalShots = new();
    private int _nextPersonalShotId = 1;

    private static float ShotSpeed(ItemType weapon) => weapon switch
    {
        ItemType.LaserRifle => 40f, // a bolt: fast enough to feel instant at room scale
        ItemType.Rifle => 26f,
        _ => 14f, // a thrown knife's worth of reach, and slow enough to dodge
    };

    private void FirePersonalShot(Character shooter, ItemType weapon, Vec2 direction)
    {
        if (direction.Length() < 0.01f)
            return;

        _personalShots.Add(new PersonalShotRuntime(
            $"shot-{_nextPersonalShotId++}",
            shooter.Position,
            direction.Normalized() * ShotSpeed(weapon),
            WeaponDefinitions.DamagePerHit(weapon) + WeaponDamageBonus,
            shooter.RoomId,
            SceneOf(shooter),
            fromEnemy: false,
            weapon));
    }

    // A defender shooting back, drawn and travelling exactly like the boarders' own fire - the
    // fight reads as an exchange rather than as two health bars ticking down.
    private void FireCrewShot(EnemyCrewRuntime crew, Character victim)
    {
        var direction = victim.Position - crew.Spawn.Position;
        if (direction.Length() < 0.01f)
            return;

        _personalShots.Add(new PersonalShotRuntime(
            $"shot-{_nextPersonalShotId++}",
            crew.Spawn.Position,
            direction.Normalized() * ShotSpeed(crew.Spawn.Weapon),
            WeaponDefinitions.DamagePerHit(crew.Spawn.Weapon),
            crew.Spawn.RoomId,
            scene: ShotScene.EnemyShip,
            fromEnemy: true,
            crew.Spawn.Weapon));
    }

    private void StepPersonalShots(double deltaSeconds)
    {
        for (var i = _personalShots.Count - 1; i >= 0; i--)
        {
            var shot = _personalShots[i];
            var from = shot.Position;
            shot.Position += shot.Velocity * (float)deltaSeconds;
            shot.Age += (float)deltaSeconds;

            if (shot.Age > ShotLifetimeSeconds || ResolvePersonalShot(shot, from) || LeftItsRoom(shot))
                _personalShots.RemoveAt(i);
        }
    }

    // Sampled along the step rather than only at its endpoint: a bullet crossing a room in a few
    // ticks would otherwise step straight over a body standing between the two positions.
    private bool ResolvePersonalShot(PersonalShotRuntime shot, Vec2 from)
    {
        var travelled = (shot.Position - from).Length();
        var samples = Math.Max(1, (int)MathF.Ceiling((float)travelled / (BulletRadius / 2f)));
        for (var i = 1; i <= samples; i++)
        {
            var point = from + (shot.Position - from) * (i / (float)samples);
            if (shot.FromEnemy)
            {
                var victim = _characters.Values.FirstOrDefault(c =>
                    c.OnEnemyShip && c.Health > 0 && c.RoomId == shot.RoomId && (c.Position - point).Length() <= BulletRadius);
                if (victim is null)
                    continue;
                victim.Health = Math.Max(0, victim.Health - shot.Damage);
                return true;
            }

            if (shot.Scene == ShotScene.EnemyShip)
            {
                var target = _enemyCrew.Values.FirstOrDefault(c =>
                    c.Alive && c.Spawn.RoomId == shot.RoomId && (c.Spawn.Position - point).Length() <= BulletRadius);
                if (target is null)
                    continue;

                target.Health = Math.Max(0, target.Health - shot.Damage);
                // Clearing the last defender captures the ship, the same as suffocating them does
                // (World.EnemyAtmosphere.cs).
                if (_enemyCrew.Values.All(c => !c.Alive))
                    Enemy.ApplyDamage(Enemy.Hp);
                return true;
            }

            // Aboard your own ship there is nobody to hit - the round just flies until it reaches a
            // bulkhead. Firing indoors is allowed anyway: a weapon you can only use in someone
            // else's ship isn't a weapon you can learn to use.
            if (shot.Scene == ShotScene.Station && TryHitGuardAt(point, shot.RoomId, shot.Damage))
                return true;
        }
        return false;
    }

    // Walls stop bullets: once the shot is outside the compartment it was fired in, it's gone. The
    // structure it belongs to decides which set of rooms to measure against.
    private bool LeftItsRoom(PersonalShotRuntime shot)
    {
        var rooms = shot.Scene switch
        {
            ShotScene.EnemyShip => EnemyShipLayout.Rooms,
            ShotScene.Station => Station.Rooms,
            _ => Ship.Rooms,
        };
        var room = rooms.FirstOrDefault(r => r.Id == shot.RoomId);
        return room is null || !room.Contains(shot.Position);
    }

    private static ShotScene SceneOf(Character character) => character switch
    {
        { OnEnemyShip: true } => ShotScene.EnemyShip,
        { OnStation: true } => ShotScene.Station,
        _ => ShotScene.Ship,
    };

    private IReadOnlyList<PersonalShotState> CreatePersonalShotStates() =>
        _personalShots
            .Select(s => new PersonalShotState(s.Id, (float)s.Position.X, (float)s.Position.Y, s.FromEnemy, s.Scene, s.Weapon))
            .ToArray();
}

internal sealed class PersonalShotRuntime
{
    public string Id { get; }
    public Vec2 Position { get; set; }
    public Vec2 Velocity { get; }
    public float Damage { get; }
    public string RoomId { get; }
    public ShotScene Scene { get; }
    public bool FromEnemy { get; }
    public ItemType Weapon { get; }
    public float Age { get; set; }

    public PersonalShotRuntime(string id, Vec2 position, Vec2 velocity, float damage, string roomId,
        ShotScene scene, bool fromEnemy, ItemType weapon)
    {
        Id = id;
        Position = position;
        Velocity = velocity;
        Damage = damage;
        RoomId = roomId;
        Scene = scene;
        FromEnemy = fromEnemy;
        Weapon = weapon;
    }
}
