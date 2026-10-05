using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// What a held Rifle / LaserRifle does when the trigger (right mouse) is down.
//
//   * Rifle: a burst. One pull releases WeaponDefinitions.BurstRounds bullets a few hundredths of a
//     second apart, and every bullet wears one round out of the magazine plugged into the rifle
//     (MagazineDefinitions, TankSockets) - no magazine or an empty one means no shot at all.
//     The extra bullets are for the look of it for now: the burst's full damage rides on its first
//     bullet, exactly what a single shot did before, so balance did not move.
//   * LaserRifle: a beam. It is not a travelling bolt - it is a straight line from the muzzle in the
//     direction the player is looking that ends where it hit somebody or ran into a wall, and it is
//     drawn for a fraction of a second. No magazine.
public sealed partial class World
{
    private const float LaserBeamLifetimeSeconds = 0.2f;
    private const float LaserBeamStep = 0.2f;
    private const int LaserBeamMaxSteps = 220; // 44 units - longer than any room
    // Fixed fan of angles (degrees) the bullets of one burst leave at, so a burst looks like a burst and
    // not a single dotted line. Fixed rather than random: it must not consume the world's seeded RNG
    // stream (enemy rolls would shift), and a test can predict it.
    private static readonly float[] BurstSpreadDegrees = { 0f, 1.6f, -1.8f, 2.8f, -1.2f, 0.8f, -2.6f, 2.0f };

    private sealed class BurstRuntime
    {
        public int Fired;
        public float Timer;
    }

    private readonly Dictionary<int, BurstRuntime> _bursts = new();
    private readonly List<LaserBeamRuntime> _laserBeams = new();
    private int _nextLaserBeamId = 1;

    private sealed class LaserBeamRuntime
    {
        public required string Id { get; init; }
        public required ShotScene Scene { get; init; }
        public required Vec2 Start { get; init; }
        public required Vec2 End { get; init; }
        public float Age { get; set; }
    }

    private IReadOnlyList<LaserBeamState> CreateLaserBeamStates() =>
        _laserBeams.Select(b => new LaserBeamState(b.Id, b.Scene, (float)b.Start.X, (float)b.Start.Y,
            (float)b.End.X, (float)b.End.Y, 1f - b.Age / LaserBeamLifetimeSeconds)).ToArray();

    // The row slot a held weapon sits in, or -1.
    private static int HeldWeaponSlot(Character character, ItemType weapon) => character.Inventory.HeldSlotOf(weapon);

    private static bool HasRoundsLoaded(Character character, ItemType weapon)
    {
        var slot = HeldWeaponSlot(character, weapon);
        return slot >= 0 && character.Inventory.TankCharge(slot) is { } charge && charge >= MagazineDefinitions.RoundsPerBullet;
    }

    // Called once per pull of the trigger, after the cooldown check in TryFirePersonalWeapon.
    // Returns whether the weapon actually fired (so an empty rifle does not start its cooldown).
    private bool FireHeldWeapon(Character character, ItemType weapon, Vec2 aim)
    {
        if (WeaponDefinitions.IsAutomatic(weapon))
        {
            if (!HasRoundsLoaded(character, weapon) || _bursts.ContainsKey(character.PlayerId))
                return false;
            _bursts[character.PlayerId] = new BurstRuntime();
            return true;
        }

        if (WeaponDefinitions.IsBeam(weapon))
        {
            FireLaserBeam(character, aim);
            return true;
        }

        FirePersonalShot(character, weapon, aim);
        return true;
    }

    // Releases the bullets of every burst in progress, one at a time on the burst's own clock. A burst
    // stops early if the magazine runs dry, the rifle leaves the hand, or its owner dies.
    private void StepBursts(double deltaSeconds)
    {
        foreach (var playerId in _bursts.Keys.ToList())
        {
            var burst = _bursts[playerId];
            if (!_characters.TryGetValue(playerId, out var character) || character.Health <= 0
                || !character.Inventory.IsHolding(ItemType.Rifle))
            {
                _bursts.Remove(playerId);
                continue;
            }

            burst.Timer -= (float)deltaSeconds;
            while (burst.Timer <= 0f && burst.Fired < WeaponDefinitions.BurstRounds)
            {
                if (!HasRoundsLoaded(character, ItemType.Rifle))
                {
                    burst.Fired = WeaponDefinitions.BurstRounds; // magazine dry: the rest of the burst never leaves the barrel
                    break;
                }

                character.Inventory.DrainTank(HeldWeaponSlot(character, ItemType.Rifle), MagazineDefinitions.RoundsPerBullet);
                FireBurstBullet(character, burst.Fired);
                burst.Fired++;
                burst.Timer += WeaponDefinitions.BurstIntervalSeconds;
            }

            if (burst.Fired >= WeaponDefinitions.BurstRounds)
                _bursts.Remove(playerId);
        }
    }

    private void FireBurstBullet(Character shooter, int index)
    {
        var aim = shooter.LookDirection.Length() > 0.01f ? shooter.LookDirection : shooter.FacingDirection;
        if (aim.Length() < 0.01f)
            return;

        var angle = BurstSpreadDegrees[index % BurstSpreadDegrees.Length] * (MathF.PI / 180f);
        var cos = MathF.Cos(angle);
        var sin = MathF.Sin(angle);
        var direction = new Vec2(aim.X * cos - aim.Y * sin, aim.X * sin + aim.Y * cos).Normalized();
        var damage = index == 0 ? WeaponDefinitions.DamagePerHit(ItemType.Rifle) + WeaponDamageBonus : 0f;

        _personalShots.Add(new PersonalShotRuntime(
            $"shot-{_nextPersonalShotId++}",
            shooter.Position,
            direction * ShotSpeed(ItemType.Rifle),
            damage,
            shooter.RoomId,
            SceneOf(shooter),
            fromEnemy: false,
            ItemType.Rifle));
    }

    // Marches a probe along the aim until it hits a defender or leaves the compartment (a wall) - the
    // same two things that stop a bolt (ResolvePersonalShot / LeftItsRoom), just resolved instantly
    // instead of over several ticks.
    private void FireLaserBeam(Character shooter, Vec2 direction)
    {
        if (direction.Length() < 0.01f)
            return;
        var dir = direction.Normalized();
        var probe = new PersonalShotRuntime("beam", shooter.Position, dir, WeaponDefinitions.DamagePerHit(ItemType.LaserRifle) + WeaponDamageBonus,
            shooter.RoomId, SceneOf(shooter), fromEnemy: false, ItemType.LaserRifle);

        var end = shooter.Position;
        for (var i = 0; i < LaserBeamMaxSteps; i++)
        {
            var from = probe.Position;
            probe.Position = from + dir * LaserBeamStep;
            if (ResolvePersonalShot(probe, from))
            {
                end = probe.Position;
                break;
            }
            if (LeftItsRoom(probe))
            {
                end = from;
                break;
            }
            end = probe.Position;
        }

        _laserBeams.Add(new LaserBeamRuntime
        {
            Id = $"beam-{_nextLaserBeamId++}",
            Scene = SceneOf(shooter),
            Start = shooter.Position,
            End = end,
        });
    }

    private void StepLaserBeams(double deltaSeconds)
    {
        for (var i = _laserBeams.Count - 1; i >= 0; i--)
        {
            _laserBeams[i].Age += (float)deltaSeconds;
            if (_laserBeams[i].Age >= LaserBeamLifetimeSeconds)
                _laserBeams.RemoveAt(i);
        }
    }
}
