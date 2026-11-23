using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// What a held firearm does when the trigger (right mouse) is down.
//
//   * A gun (GunDefinitions - Barotrauma numbers): one round per shot out of whatever is loaded in it,
//     at the pace of its reload time for as long as the trigger is held. A shell throws its pellets all
//     at once. Every round leaves inside the spread cone of the gun - a tight one in trained hands (the
//     security officer), a wide one for anyone else - and carries the wound, the bleeding and the stun
//     of that round to whoever it reaches (World.PersonalShots.cs).
//     With nothing loaded a gun that takes loose rounds reloads from a stack in the pockets on the next
//     pull (it takes a moment); a gun that takes magazines only clicks until a fresh one is put in.
//   * LaserRifle: a beam. It is not a travelling bolt - it is a straight line from the muzzle in the
//     direction the player is looking that ends where it hit somebody or ran into a wall, and it is
//     drawn for a fraction of a second. No magazine.
public sealed partial class World
{
    private const float LaserBeamLifetimeSeconds = 0.2f;
    private const float LaserBeamStep = 0.2f;
    private const int LaserBeamMaxSteps = 220; // 44 units - longer than any room

    private readonly List<LaserBeamRuntime> _laserBeams = new();
    private int _nextLaserBeamId = 1;

    private sealed class LaserBeamRuntime
    {
        public required string Id { get; init; }
        public required ShotScene Scene { get; init; }
        public required Vec2 Start { get; init; }
        public required Vec2 End { get; init; }
        public float Age { get; set; }
        public bool Tracer { get; init; }
        public bool FromEnemy { get; init; }
        public float Weight { get; init; } = 1f;
    }

    private IReadOnlyList<LaserBeamState> CreateLaserBeamStates() =>
        _laserBeams.Select(b => new LaserBeamState(b.Id, b.Scene, (float)b.Start.X, (float)b.Start.Y,
            (float)b.End.X, (float)b.End.Y, 1f - b.Age / LaserBeamLifetimeSeconds, b.Tracer, b.FromEnemy, b.Weight)).ToArray();

    // The row slot a held weapon sits in, or -1.
    private static int HeldWeaponSlot(Character character, ItemType weapon) => character.Inventory.HeldSlotOf(weapon);

    // Cheat panel: nothing runs out (turret shells, laser charge, rounds in a gun).
    private bool _debugInfiniteAmmo;

    // Weapons training, the way Barotrauma has a weapons skill: the security officer handles a gun
    // properly, everybody else sprays (GunSpec.SpreadFor) and fumbles a bolt (GunSpec.ReloadFor).

    // Called once per pull of the trigger, after the cooldown check in TryFirePersonalWeapon.
    // Returns whether the weapon actually fired (so an empty gun does not start its cooldown).
    private bool FireHeldWeapon(Character character, ItemType weapon, Vec2 aim)
    {
        if (GunDefinitions.Of(weapon) is { } gun)
            return FireGun(character, gun, aim);

        if (WeaponDefinitions.IsBeam(weapon))
        {
            FireLaserBeam(character, aim);
            return true;
        }

        FirePersonalShot(character, weapon, aim);
        return true;
    }

    // How long loading loose rounds from the pockets into an empty gun takes (the pull that finds it empty).
    public const float GunReloadSeconds = 1.2f;
    public const float RevolverReloadSeconds = GunReloadSeconds;

    private bool FireGun(Character character, GunSpec gun, Vec2 aim)
    {
        var slot = HeldWeaponSlot(character, gun.Weapon);
        if (slot < 0)
            return false;
        // What leaves the barrel is the round that is loaded: the ordinary one, or a variant of it.
        var loaded = GunDefinitions.Loaded(gun, character.Inventory.LoadedAmmo(slot));
        if (!_debugInfiniteAmmo)
        {
            var inventory = character.Inventory;
            if (inventory.TankCharge(slot) is not >= 1f)
            {
                if (gun.StackFed)
                    for (var i = 0; i < inventory.MainSlots.Length; i++)
                        if (inventory.MainSlots[i] is { } spare && GunDefinitions.Accepts(gun, spare) && inventory.TryAttachTank(i, slot))
                        {
                            _weaponCooldowns[character.PlayerId] = GunReloadSeconds;
                            break;
                        }
                return false;
            }
            inventory.DrainTank(slot, 1f);
            // The last loose round gone leaves nothing in the gun; an empty magazine stays in until it is swapped.
            if (gun.StackFed && inventory.TankCharge(slot) is not > 0f)
                inventory.ClearTank(slot);
        }
        FireGunRound(new Shooter(SceneOf(character), FromEnemy: false, character.PlayerId, CrewId: null, character.WeaponsSkill, WeaponDamageBonus),
            character.Position, character.RoomId, loaded, aim);
        return true;
    }

    // One shot of a gun leaving a muzzle: a round, or every pellet of a shell. The whole shot is thrown off
    // the aim by up to half the spread cone either way, each pellet by up to half the pellet cone around that.
    // A fraction of the spread cone, -0.5..0.5, off the number of the shot. Not the seeded RNG of the world:
    // spraying a magazine must not shift what the enemy rolls next, and a test can predict where a shot goes.
    private static float SpreadRoll(int shot, int salt)
    {
        var n = unchecked((uint)(shot * 374761393 + salt * 668265263));
        n = unchecked((n ^ (n >> 13)) * 1274126177u);
        n ^= n >> 16;
        return (n & 0xFFFF) / 65535f - 0.5f;
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
            shooter.RoomId, SceneOf(shooter), fromEnemy: false, ItemType.LaserRifle)
            { Shooter = new Shooter(SceneOf(shooter), FromEnemy: false, shooter.PlayerId, CrewId: null, shooter.WeaponsSkill, DamageBonus: 0f) };

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
