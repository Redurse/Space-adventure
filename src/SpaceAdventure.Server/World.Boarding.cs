using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

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
    private const float BoardingEntryX = 1.5f; // just inside enemy-breach, past the hatch rect
    private const float ShipAirlockReturnX = 25f; // matches World.StationDocking.cs's own re-entry nudge
    private const float BoardingRowY = 3f;
    private const float CrewAttackIntervalSeconds = 2.5f;

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
    }

    private void EjectFromEnemyShip(Character character)
    {
        character.OnEnemyShip = false;
        character.IsOutside = true;
        character.EvaAttachedTo = EvaAttachment.None;
        character.EvaAttachedAsteroidId = null;
        character.EvaLocalOffset = EnemyShipFieldPosition - new Vec2(BoardingReachRadius, 0);
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

    // Ship -> enemy ship. Mirrors TryCrossIntoStation, but requires a suit (you cross open vacuum
    // to get there, unlike a station's sealed connector) and only during a battle.
    private bool TryBoardEnemyShip(Character character, Vec2 moveDelta)
    {
        if (character.OnEnemyShip || !character.IsOutside || Phase != VoyagePhase.Battle)
            return false;

        var worldPos = GetEvaWorldPosition(character) + moveDelta;
        if ((EnemyShipFieldPosition - worldPos).Length() > BoardingReachRadius)
            return false;

        character.IsOutside = false;
        character.EvaAttachedTo = EvaAttachment.None;
        character.EvaAttachedAsteroidId = null;
        character.EvaVelocity = Vec2.Zero;
        character.OnEnemyShip = true;
        character.RoomId = EnemyShipLayout.BoardingRoomId;
        character.Position = new Vec2(BoardingEntryX, BoardingRowY);
        return true;
    }

    private const float BoardingReachRadius = 6f; // how close the EVA character has to drift to the enemy hull

    // Enemy ship -> back outside, through the same breach. Puts the character back in EVA
    // free-flight (not straight into their own ship) - they still have to fly home.
    private bool TryLeaveEnemyShip(Character character, Vec2 moveDelta)
    {
        if (!character.OnEnemyShip || character.RoomId != EnemyShipLayout.BoardingRoomId)
            return false;

        var next = character.Position + moveDelta;
        if (!EnemyShipLayout.BoardingHatch.Contains(next))
            return false;

        // Climbing out of the enemy's breach leaves you floating beside a hull that isn't
        // magnetizable anyway, so nothing has to be held off - the boots only ever catch on your
        // own ship or a rock (World.Eva.cs).
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
