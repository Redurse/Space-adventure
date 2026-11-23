using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// What a round from a gun does once it has left the barrel, after Barotrauma:
//   * a bullet is hitscan - it arrives the instant it is fired. It goes in a straight line through open floor
//     and open doors and stops in the first standing wall, at a closed door, or in the first body on the way
//     (ShotTrace.cs). Only the harpoon is a thing that flies, and it is stopped by the same things.
//   * there is friendly fire: the first body on the line is hit, whoever it belongs to - a crewmate standing in
//     front of the target takes the round. Only the shooter is exempt.
//   * a round that stops in a wall chips it (GunSpec.StructureDamage); enough of them make a hole.
//   * armour on the part that is hit cuts the wound and the bleeding, less so for a round with penetration
//     (ArmorDefinitions.cs).
// Which part of the body is hit comes from where the line crosses the body: the outer quarter of its width is
// an arm on that side, the rest is the head, the torso or a leg. This game looks straight down on its crew, so
// the height of a hit cannot be seen and that last choice is a roll in the proportions of a standing figure.
// The wound itself - what a hit to a part does to the body - is not Barotrauma's: World.Medical.cs.
public sealed partial class World
{
    // A bullet goes until something stops it; this only bounds the walk.
    private const float HitscanReach = 60f;
    // How fast the streak drawn for a bullet crosses the deck. Cosmetic: the hit is already decided.
    private const float TracerSpeed = 90f;
    // Barotrauma's hull walls take about 200 damage, a wall block here takes 100.
    private const float StructureDamageScale = 0.5f;
    // The outer part of a body's width that counts as an arm.
    private const float ArmEdge = 0.75f;

    // Who fired: so a shot cannot hit its own shooter, the spread follows their skill, and their skill can grow.
    internal readonly record struct Shooter(ShotScene Scene, bool FromEnemy, int? PlayerId, string? CrewId, float Skill, float DamageBonus);

    private TileGrid? TilesOf(ShotScene scene) => scene switch
    {
        ShotScene.EnemyShip => BoardableEnemy is null ? null : EnemyShipLayout.Tiles,
        ShotScene.Station => Station.Tiles,
        _ => Ship.Tiles,
    };

    private static ArmorWorn ArmorOf(Character character) => new(
        character.Inventory.Equipped[EquipSlot.Suit] == ItemType.BodyArmor,
        character.Inventory.Equipped[EquipSlot.Headwear] == ItemType.BallisticHelmet);

    private void FireGunRound(Shooter shooter, Vec2 from, string roomId, GunSpec gun, Vec2 aim)
    {
        if (aim.Length() < 0.01f)
            return;
        var direction = aim.Normalized();
        var roll = _nextPersonalShotId;
        // One roll places the shot inside the cone of the shooter; each pellet then leaves in a cone of its own.
        var shotAngle = SpreadRoll(roll, 0) * gun.SpreadFor(shooter.Skill);
        for (var pellet = 0; pellet < gun.Pellets; pellet++)
        {
            var degrees = shotAngle + (gun.Pellets > 1 ? SpreadRoll(roll, pellet + 1) * gun.PelletSpreadDegrees : 0f);
            var radians = degrees * (MathF.PI / 180f);
            var cos = MathF.Cos(radians);
            var sin = MathF.Sin(radians);
            var flight = new Vec2(direction.X * cos - direction.Y * sin, direction.X * sin + direction.Y * cos);
            var serial = _nextPersonalShotId++;
            var shot = new PersonalShotRuntime(
                $"shot-{serial}",
                from,
                flight * (gun.Hitscan ? TracerSpeed : gun.ShotSpeed),
                gun.HitWound + shooter.DamageBonus / gun.Pellets,
                roomId,
                shooter.Scene,
                shooter.FromEnemy,
                gun.Weapon) { Gun = gun, Shooter = shooter, Serial = serial };
            if (gun.Hitscan)
            {
                // Decided now; what stays in the list is only the streak, flying as far as the bullet got.
                shot.Spent = true;
                var travelled = ResolveGunSegment(shot, from, flight, HitscanReach).Distance;
                shot.MaxAge = travelled / TracerSpeed;
                // The line the bullet took, shown for a moment the way the laser shows its beam - a streak that lives
                // for one snapshot is too brief to read as a shot.
                _laserBeams.Add(new LaserBeamRuntime
                {
                    Id = $"beam-{_nextLaserBeamId++}",
                    Scene = shooter.Scene,
                    Start = from,
                    End = from + flight * travelled,
                    Tracer = true,
                    FromEnemy = shooter.FromEnemy,
                    Weight = gun.Pellets > 1 ? 0.5f : gun.HitWound >= 50f ? 1.35f : gun.HitWound >= 20f ? 1f : 0.8f,
                });
            }
            _personalShots.Add(shot);
        }
    }

    // Walks one straight stretch of a round and applies whatever it runs into first. Stopped is false only when
    // the whole stretch was clear.
    private (bool Stopped, float Distance) ResolveGunSegment(PersonalShotRuntime shot, Vec2 from, Vec2 direction, float length)
    {
        var tiles = TilesOf(shot.Scene);
        var wall = tiles is null ? new ShotTrace.Result(length, null) : ShotTrace.ToWall(tiles, from, direction, length);
        var nearest = wall.Distance;
        Action? hit = null;

        foreach (var character in _characters.Values)
        {
            if (character.IsDead || character.IsOutside || character.PlayerId == shot.Shooter.PlayerId || SceneOf(character) != shot.Scene)
                continue;
            if (EntryDistance(from, direction, character.Position, nearest) is not { } at)
                continue;
            nearest = at;
            var victim = character;
            hit = () => ShootBody(victim.Body, shot.Damage, shot.Gun!.HitBleeding, RollStun(shot.Gun, shot.Serial),
                HitPartFor(from, direction, victim.Position, victim.FacingDirection, shot.Serial), shot.Gun.Penetration, ArmorOf(victim), shot.Gun.Burn);
        }

        if (shot.Scene == ShotScene.EnemyShip && BoardableEnemy is { } hull)
            foreach (var crew in hull.Crew)
            {
                if (!crew.Alive || crew.Spawn.Id == shot.Shooter.CrewId)
                    continue;
                if (EntryDistance(from, direction, crew.Position, nearest) is not { } at)
                    continue;
                nearest = at;
                var target = crew;
                hit = () =>
                {
                    var before = target.Body.Vitality;
                    // A defender has no facing of its own here; it is taken to be looking back along the shot.
                    ShootBody(target.Body, shot.Damage, shot.Gun!.HitBleeding, RollStun(shot.Gun, shot.Serial),
                        HitPartFor(from, direction, target.Position, direction * -1.0, shot.Serial), shot.Gun.Penetration, target.Armor, shot.Gun.Burn);
                    if (shot.Shooter.PlayerId is { } playerId && _characters.TryGetValue(playerId, out var gunner))
                        gunner.GainWeaponsSkill(Math.Max(0f, before - target.Body.Vitality));
                    CaptureIfCrewWiped(hull);
                };
            }

        // A guard's round goes through the people of the station: it is not shooting at them.
        if (shot.Scene == ShotScene.Station && !shot.Shooter.FromEnemy)
        {
            // Everyone on a station can be hit: its guards, the people behind the counters, the people walking about.
            foreach (var npc in Station.Npcs)
            {
                if (!IsNpcAlive(npc))
                    continue;
                if (EntryDistance(from, direction, npc.Position, nearest) is not { } at)
                    continue;
                nearest = at;
                var struck = npc;
                hit = () => HitGuard(struck, shot.Damage);
            }
            if (IsDocked && !DockedAtAbandonedStation)
                foreach (var (residentId, position) in ResidentSim().Living)
                {
                    if (EntryDistance(from, direction, position, nearest) is not { } at)
                        continue;
                    nearest = at;
                    hit = () => HitResident(residentId, shot.Damage);
                }
        }

        if (hit is not null)
        {
            hit();
            return (true, nearest);
        }
        if (wall.Distance >= length)
            return (false, length);
        if (wall.Wall is not null)
            ChipWall(shot, from + direction * wall.Distance);
        return (true, wall.Distance);
    }

    // How far along the line a body of BulletRadius around this centre is first touched; null if it is not
    // touched within reach.
    private static float? EntryDistance(Vec2 from, Vec2 direction, Vec2 center, float reach)
    {
        var toCenter = center - from;
        var along = toCenter.X * direction.X + toCenter.Y * direction.Y;
        var distanceSquared = toCenter.X * toCenter.X + toCenter.Y * toCenter.Y;
        if (distanceSquared <= BulletRadius * BulletRadius)
            return 0f; // point blank: the muzzle is already against the body
        var lateralSquared = distanceSquared - along * along;
        if (along < 0 || lateralSquared > BulletRadius * BulletRadius)
            return null;
        var entry = Math.Max(0.0, along - Math.Sqrt(BulletRadius * BulletRadius - lateralSquared));
        return entry <= reach ? (float)entry : null;
    }

    private static BodyPart HitPartFor(Vec2 from, Vec2 direction, Vec2 center, Vec2 facing, int serial)
    {
        var toCenter = center - from;
        // How far to the side of the body's centre the line passes, and on which side.
        var lateral = direction.X * toCenter.Y - direction.Y * toCenter.X;
        var passX = direction.Y * lateral;
        var passY = -direction.X * lateral;
        var onRight = passX * -facing.Y + passY * facing.X > 0; // the body's right hand, given where it faces
        if (Math.Abs(lateral) > BulletRadius * ArmEdge)
            return onRight ? BodyPart.RightArm : BodyPart.LeftArm;
        var roll = HashUnit(serial, 23);
        if (roll < 0.15f)
            return BodyPart.Head;
        if (roll < 0.70f)
            return BodyPart.Torso;
        return onRight ? BodyPart.RightLeg : BodyPart.LeftLeg;
    }

    // The stun a round carries: its own (which for some rounds only lands now and then) plus, rarely, a longer one.
    private static float RollStun(GunSpec gun, int serial)
    {
        var stun = gun.StunChance >= 1f || HashUnit(serial, 7) < gun.StunChance ? gun.StunSeconds : 0f;
        if (gun.BonusStunChance > 0f && HashUnit(serial, 11) < gun.BonusStunChance)
            stun += gun.BonusStunSeconds;
        return stun;
    }

    // A fixed roll in [0, 1) for a given shot: the same shot always rolls the same, which keeps tests stable.
    private static float HashUnit(int serial, int salt)
    {
        var n = unchecked((uint)(serial * 374761393 + salt * 668265263));
        n = unchecked((n ^ (n >> 13)) * 1274126177u);
        n ^= n >> 16;
        return (n & 0xFFFFFF) / (float)0x1000000;
    }

    private void ChipWall(PersonalShotRuntime shot, Vec2 point)
    {
        var amount = (shot.Gun?.StructureDamage ?? 0f) * StructureDamageScale;
        if (amount <= 0f)
            return;
        if (shot.Scene == ShotScene.Ship)
        {
            var block = Ship.WallBlocks.MinBy(b => (b.Position - point).Length());
            if (block is not null && (block.Position - point).Length() <= 1.0)
                DamageWallBlock(block.Id, amount);
        }
        else if (shot.Scene == ShotScene.EnemyShip && BoardableEnemy is { } enemy)
        {
            var block = EnemyShipLayout.WallBlocks.MinBy(b => (b.Position - point).Length());
            if (block is not null && (block.Position - point).Length() <= 1.0)
                enemy.DamageWallBlock(block.Id, amount);
        }
    }
}
