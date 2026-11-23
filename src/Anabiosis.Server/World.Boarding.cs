using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Boarding (game_design.md section 12, Phase 3): instead of only shelling the enemy from the
// turrets, suit up, EVA across during a battle, climb through the enemy hull's breach and clear it
// room by room with personal weapons - which is what finally gives Knife/Rifle/LaserRifle a
// gameplay effect at all (they existed as carryable items with no use since M3).
//
// Reuses wholesale what stations already established: a second walkable structure with its own
// Rooms/Doors (EnemyShipLayout, shared RoomLayout collision), a bool on Character saying which
// structure RoomId refers to, and a hatch crossing driven from StepCharacters. The one genuinely
// new mechanic is person-to-person combat, which lives here.
public sealed partial class World
{
    private const float CrewAttackIntervalSeconds = 2.5f;
    private const float EjectClearRadius = 6f; // clear of the hull entirely, not just past the plating

    private readonly Dictionary<int, float> _weaponCooldowns = new();
    // How far below zero a weapon cooldown is allowed to run: a little over one tick (see TryFirePersonalWeapon).
    private const float WeaponCooldownCarry = 0.04f;
    private float _crewAttackCooldown = CrewAttackIntervalSeconds;

    // The hull a boarding party would find themselves in: whichever ship of the squadron is still
    // flying. Falls back to the ordinary raider outside a fight, so callers that ask between
    // battles (the snapshot, the tests) always get a valid structure.
    public EnemyShipLayout EnemyShipLayout => BoardableEnemy?.Layout ?? EnemyShipLayout.Default;

    // Anyone still inside a hull that just died goes out through its breach. Their RoomId names a
    // compartment of a structure that no longer exists, and the next ship of the squadron is a
    // different plan entirely - keeping them "aboard" would leave them standing in a room the game
    // can no longer find. Called from ResolveEnemyLosses, which is what notices the change.
    private void EjectBoardersFromLostHull()
    {
        foreach (var character in _characters.Values.Where(c => c.OnEnemyShip).ToList())
            EjectFromEnemyShip(character);

        // Standing on the hull (magnetized, not yet through a hatch) when it goes down leaves you
        // clinging to a surface that no longer exists - same fix, just for the outside-only case.
        foreach (var character in _characters.Values.Where(c => c.EvaAttachedTo == EvaAttachment.EnemyShip))
        {
            character.EvaAttachedTo = EvaAttachment.None;
            character.EvaLocalOffset = EnemyShipFieldPosition - new Vec2(EjectClearRadius, 0);
            character.EvaVelocity = Vec2.Zero;
            character.PushedOffFrom = PushOffOrigin.None;
            character.BouncedOffFrom = PushOffOrigin.None;
        }
    }

    private void EjectFromEnemyShip(Character character)
    {
        character.OnEnemyShip = false;
        character.IsOutside = true;
        character.EvaAttachedTo = EvaAttachment.None;
        character.EvaAttachedAsteroidId = null;
        character.EvaLocalOffset = EnemyShipFieldPosition - new Vec2(EjectClearRadius, 0);
        character.EvaVelocity = Vec2.Zero;
        character.PushedOffFrom = PushOffOrigin.None;
        // Meaningless while outside, but valid for the trip home - falls back to any room at all
        // when the ship has no real airlock of either kind (ResolveShipAirlock, World.
        // StationDocking.cs), since this id only ever needs to resolve to a real Ship.Rooms entry,
        // never specifically the airlock's own room.
        character.RoomId = ResolveShipAirlock()?.RoomId ?? Ship.Rooms[0].Id;
    }

    // Where the boarding party is headed: the lead hull's actual place in the field, which now
    // moves as it chases the player (World.EnemyFleet.cs) rather than sitting at a fixed offset.
    // Falls back to a point off the bow when nothing is flying, so callers outside a fight still
    // get a sane answer.
    private Vec2 EnemyShipFieldPosition =>
        BoardableEnemy?.Position ?? _shipFieldPosition + new Vec2(EnemyStandoffDistance, 0);

    // Firing a held personal weapon at the nearest living defender in range. Same-room as well as
    // in-range, so a rifle can't shoot through a bulkhead - walls are the whole point of clearing
    // a ship room by room.
    private void TryFirePersonalWeapon(Character character)
    {
        if (character.IsDowned || !character.Body.HandsUsable)
            return; // out cold, or both arms broken - nothing to hold a weapon with
        var heldWeapons = character.Inventory.HeldSlotIndices
            .Select(i => character.Inventory.MainSlots[i])
            .OfType<ItemType>()
            .Where(WeaponDefinitions.IsWeapon)
            .ToList();
        if (heldWeapons.Count == 0)
            return;
        var weapon = heldWeapons[0];

        if (_weaponCooldowns.TryGetValue(character.PlayerId, out var cooldown) && cooldown > 0.0001f)
            return;

        // Aimed, not auto-targeted: the shot leaves along the cursor direction and hits whatever it
        // reaches (World.PersonalShots.cs). Missing is now a thing that can happen, which is the
        // point - the old version picked the nearest defender in the room and could not miss.
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection : character.FacingDirection;
        if (!FireHeldWeapon(character, weapon, aim))
            return;
        var pace = GunDefinitions.Of(weapon) is { } gun ? gun.ReloadFor(character.WeaponsSkill) : WeaponDefinitions.CooldownSeconds(weapon);
        // A trigger held down fires at exactly the pace of the gun, not at that pace rounded up to whole ticks: what the
        // last wait overshot by (it ran out part of the way through a tick) comes off the next one. A cooldown that
        // ran out longer ago than that carries nothing over.
        var overshoot = cooldown < 0f && cooldown > -WeaponCooldownCarry ? cooldown : 0f;
        _weaponCooldowns[character.PlayerId] = pace + overshoot;
    }

    private void StepBoarding(double deltaSeconds)
    {
        StepEnemyAtmospheres(deltaSeconds);
        StepEnemyCrews(deltaSeconds);

        foreach (var playerId in _weaponCooldowns.Keys.ToList())
            _weaponCooldowns[playerId] = Math.Max(-WeaponCooldownCarry, _weaponCooldowns[playerId] - (float)deltaSeconds);

        if (BoardableEnemy is not { } enemy)
            return;
        var boarders = DebugEnemyCrewHoldsFire
            ? new List<Character>()
            : _characters.Values.Where(c => c.OnEnemyShip && c.Health > 0 && !c.Body.Unconscious && !c.Body.InArrest).ToList();
        var dt = (float)deltaSeconds;

        // Defenders shoot back the way a gun is actually worked: each keeps its own pace - the pace of the gun in its
        // hands - takes a moment to bring it up on a fresh target, and stops to reload when the gun runs dry. They close
        // in by walking (World.EnemyCrew.cs) and fire at whoever is in range with nothing solid in between; a boarder
        // who is already down is left alone.
        foreach (var crew in enemy.Crew)
        {
            crew.FireCooldown = Math.Max(0f, crew.FireCooldown - dt);
            var victim = crew.Alive && !crew.Downed ? CrewTarget(crew, boarders) : null;
            if (victim is null)
            {
                crew.AimSeconds = 0f;
                continue;
            }
            crew.AimSeconds += dt;
            if (crew.AimSeconds < CrewAimSeconds || crew.FireCooldown > 0f)
                continue;

            FireCrewShot(crew, victim);
            if (GunDefinitions.Of(crew.Spawn.Weapon) is { } gun)
            {
                if (crew.RoundsLeft < 0)
                    crew.RoundsLeft = gun.Capacity;
                crew.RoundsLeft--;
                crew.FireCooldown = crew.RoundsLeft > 0 ? gun.ReloadFor(crew.WeaponsSkill) : CrewReloadSeconds;
                if (crew.RoundsLeft <= 0)
                    crew.RoundsLeft = gun.Capacity;
            }
            else
            {
                crew.FireCooldown = CrewAttackIntervalSeconds;
            }
        }
    }

    // How long a defender takes to bring its gun up on a target it has just got a line on, and to reload an empty gun.
    private const float CrewAimSeconds = 0.6f;
    // Tests that measure something else aboard a hostile ship (a knife fight, cutting a wall) switch the defenders off.
    public bool DebugEnemyCrewHoldsFire { get; set; }
    private const float CrewReloadSeconds = 2.5f;

    // The nearest boarder in range of this defender with a clear line to them: no wall and no closed door between.
    private Character? CrewTarget(EnemyCrewRuntime crew, IReadOnlyList<Character> boarders)
    {
        var range = WeaponDefinitions.Range(crew.Spawn.Weapon);
        Character? best = null;
        var bestDistance = double.MaxValue;
        foreach (var boarder in boarders)
        {
            var toBoarder = boarder.Position - crew.Position;
            var distance = toBoarder.Length();
            if (distance > range || distance >= bestDistance)
                continue;
            if (distance > 0.05 && ShotTrace.ToWall(EnemyShipLayout.Tiles, crew.Position, toBoarder * (1.0 / distance), (float)distance).Distance < distance - ShotTrace.Step)
                continue;
            best = boarder;
            bestDistance = distance;
        }
        return best;
    }

    // Ship -> enemy ship now works exactly like ship -> vacuum on the player's own hull: magnetize
    // to it (World.Eva.cs's TryAutoAttach, EnemyShip branch), walk across the plating, and step
    // into a hatch or wall panel that's actually been cut open (StepEnemyShipAttachedWalk) - there's
    // no separate proximity-radius phase-in any more (that's TryBoardEnemyShip and
    // TryBoardThroughCutHullBreach, both removed; see World.Movement.cs and World.Eva.cs).

    // Enemy ship -> back outside, through whichever cut-open hatch or wall panel the character is
    // actually standing at - generalizes the old single-fixed-hatch check to any of the hull's two
    // OuterHatches or any breached WallBlock in the room being left, matching how many ways in
    // there now are. Puts the character back in free EVA flight (not attached to the hull) - they
    // still have to fly home, same as leaving used to work.
    private bool TryLeaveEnemyShip(Character character, Vec2 moveDelta)
    {
        if (!character.OnEnemyShip || BoardableEnemy is not { } enemy)
            return false;

        var next = character.Position + moveDelta;
        var outerDoor = EnemyShipLayout.OuterHatches.FirstOrDefault(d =>
            d.RoomAId == character.RoomId && enemy.IsWallBlockBreached(d.Id) && d.Contains(next));
        var breachBlock = outerDoor is null
            ? EnemyShipLayout.WallBlocks.FirstOrDefault(b => b.RoomId == character.RoomId && !b.IsInterior &&
                enemy.IsWallBlockBreached(b.Id) && (b.Position - next).Length() <= RoomLayout.BreachCrossingRadius)
            : null;
        if (outerDoor is null && breachBlock is null)
            return false;

        // Climbing out through a hole leaves you floating beside a hull that isn't magnetizable
        // anyway, so nothing has to be held off - the boots only ever catch on your own ship or a
        // rock (World.Eva.cs).
        EjectFromEnemyShip(character);
        return true;
    }

    private IReadOnlyList<EnemyCrewState> CreateEnemyCrewStates() =>
        BoardableEnemy is { } enemy
            ? enemy.Crew
                .Select(c => new EnemyCrewState(c.Spawn.Id, c.Spawn.Name, c.RoomId, (float)c.Position.X, (float)c.Position.Y, c.Health, c.Alive, c.Spawn.Role, c.Downed))
                .ToArray()
            : Array.Empty<EnemyCrewState>();
}

