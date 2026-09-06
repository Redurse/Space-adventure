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

    private readonly Dictionary<string, EnemyCrewRuntime> _enemyCrew = new();
    private readonly Dictionary<int, float> _weaponCooldowns = new();
    private float _crewAttackCooldown = CrewAttackIntervalSeconds;

    // The hull a boarding party would find themselves in: whichever ship of the squadron is still
    // flying. Falls back to the ordinary raider outside a fight, so callers that ask between
    // battles (the snapshot, the tests) always get a valid structure.
    public EnemyShipLayout EnemyShipLayout => BoardableEnemy?.Layout ?? EnemyShipLayout.CreateDefault();

    private void ResetEnemyCrew()
    {
        _enemyCrew.Clear();
        // No hull left to board means no crew - the list describes the people aboard a specific
        // ship. Rebuilding the fallback layout's crew here (which is what this used to do) invented
        // three defenders standing in empty space the moment a sector was cleared.
        if (BoardableEnemy is not null)
            foreach (var spawn in EnemyShipLayout.CrewSpawns)
                _enemyCrew[spawn.Id] = new EnemyCrewRuntime(spawn);
        _crewAttackCooldown = CrewAttackIntervalSeconds;
        ResetEnemyAtmosphere();
    }

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
        character.RoomId = Ship.AirlockOuterDoors.First().RoomId; // meaningless while outside, but valid for the trip home
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
        var heldWeapons = character.Inventory.HeldSlotIndices
            .Select(i => character.Inventory.MainSlots[i])
            .OfType<ItemType>()
            .Where(WeaponDefinitions.IsWeapon)
            .ToList();
        if (heldWeapons.Count == 0)
            return;
        var weapon = heldWeapons[0];

        if (_weaponCooldowns.TryGetValue(character.PlayerId, out var cooldown) && cooldown > 0)
            return;

        // Aimed, not auto-targeted: the shot leaves along the cursor direction and hits whatever it
        // reaches (World.PersonalShots.cs). Missing is now a thing that can happen, which is the
        // point - the old version picked the nearest defender in the room and could not miss.
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection : character.FacingDirection;
        _weaponCooldowns[character.PlayerId] = WeaponDefinitions.CooldownSeconds(weapon);
        FirePersonalShot(character, weapon, aim);
    }

    private void StepBoarding(double deltaSeconds)
    {
        StepEnemyAtmosphere(deltaSeconds);

        foreach (var playerId in _weaponCooldowns.Keys.ToList())
            _weaponCooldowns[playerId] = Math.Max(0, _weaponCooldowns[playerId] - (float)deltaSeconds);

        var boarders = _characters.Values.Where(c => c.OnEnemyShip && c.Health > 0).ToList();
        if (boarders.Count == 0)
            return;

        _crewAttackCooldown -= (float)deltaSeconds;
        if (_crewAttackCooldown > 0)
            return;
        _crewAttackCooldown = CrewAttackIntervalSeconds;

        // Defenders shoot back on the same interval-timer model the ship-scale enemy AI already
        // uses (World.EnemyAi.cs) - no pathing or movement, they hold their room.
        foreach (var crew in _enemyCrew.Values.Where(c => c.Alive))
        {
            var victim = boarders.FirstOrDefault(b =>
                b.RoomId == crew.Spawn.RoomId &&
                (crew.Spawn.Position - b.Position).Length() <= WeaponDefinitions.Range(crew.Spawn.Weapon));
            if (victim is null)
                continue;

            // They shoot the same way the boarders do now - a round that crosses the room and can
            // be walked out of, rather than damage applied at range.
            FireCrewShot(crew, victim);
        }
    }

    // Ship -> enemy ship now works exactly like ship -> vacuum on the player's own hull: magnetize
    // to it (World.Eva.cs's TryAutoAttach, EnemyShip branch), walk across the plating, and step
    // into a hatch or wall panel that's actually been cut open (StepEnemyShipAttachedWalk) - there's
    // no separate proximity-radius phase-in any more (that's TryBoardEnemyShip and
    // TryBoardThroughCutHullBreach, both removed; see World.Movement.cs and World.Eva.cs).

    // Enemy ship -> back outside, through whichever cut-open hatch or wall panel the character is
    // actually standing at - generalizes the old single-fixed-hatch check to any of the hull's two
    // AirlockOuterDoors or any breached WallBlock in the room being left, matching how many ways in
    // there now are. Puts the character back in free EVA flight (not attached to the hull) - they
    // still have to fly home, same as leaving used to work.
    private bool TryLeaveEnemyShip(Character character, Vec2 moveDelta)
    {
        if (!character.OnEnemyShip || BoardableEnemy is not { } enemy)
            return false;

        var next = character.Position + moveDelta;
        var outerDoor = EnemyShipLayout.AirlockOuterDoors.FirstOrDefault(d =>
            d.RoomId == character.RoomId && enemy.IsAirlockBreached(d.Id) && d.Contains(next));
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
        _enemyCrew.Values
            .Select(c => new EnemyCrewState(c.Spawn.Id, c.Spawn.Name, c.Spawn.RoomId, c.Spawn.X, c.Spawn.Y, c.Health, c.Alive))
            .ToArray();
}

internal sealed class EnemyCrewRuntime
{
    public const float MaxHealth = 60f;

    public EnemyCrewSpawn Spawn { get; }
    public float Health { get; set; } = MaxHealth;
    public bool Alive => Health > 0;

    public EnemyCrewRuntime(EnemyCrewSpawn spawn) => Spawn = spawn;
}
