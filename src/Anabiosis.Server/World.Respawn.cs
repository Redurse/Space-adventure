using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

public sealed partial class World
{
    // Direct user request ("через 10 секунд... в игре сверху пишется таймер... игрок спанился на
    // корабле в кокпите") - Character.RespawnSecondsRemaining is 0 while alive (same "0 means idle"
    // convention ScannerCooldownRemaining/ProductionActionRemaining already use), armed to this the
    // first tick IsDead is found true (StepCharacters, World.Movement.cs), then counted down every
    // tick after until it reaches zero, at which point the character comes back.
    private const float RespawnSeconds = 10f;

    private void StepRespawn(Character character, double deltaSeconds)
    {
        character.RespawnSecondsRemaining = character.RespawnSecondsRemaining > 0f
            ? character.RespawnSecondsRemaining - (float)deltaSeconds
            : RespawnSeconds;
        if (character.RespawnSecondsRemaining <= 0f)
            RespawnCharacter(character);
    }

    // Brings an existing Character back rather than replacing it (unlike SpawnCharacter, which
    // makes a brand new one for a freshly-joined player) - inventory/nickname/role/ping all survive
    // a respawn exactly as they were the moment death hit, only the things that made them a corpse
    // lying somewhere get reset. Ship.SpawnPoint/SpawnRoomId is the same "кокпит" every fresh
    // character already starts at (Ship.Custom.cs's own Ship constructor call - both are literally
    // HelmConsole.Position/RoomId), so this needs no separate "find the cockpit" logic of its own.
    private void RespawnCharacter(Character character)
    {
        character.Health = Character.MaxHealth;
        character.RespawnSecondsRemaining = 0f;
        character.Position = Ship.SpawnPoint;
        character.RoomId = Ship.SpawnRoomId;

        // Whatever seat/location death happened to freeze them at (World.Movement.cs's own
        // IsDead gate never force-leaves one) has to actually clear now - reappearing in the
        // cockpit while still nominally "at the helm" of wherever they died, or still flagged
        // OnStation/OnEnemyShip/outside, would be a contradictory state nothing else here expects.
        character.IsAtHelm = false;
        character.IsOutside = false;
        character.OnStation = false;
        character.OnEnemyShip = false;
        character.EvaAttachedTo = EvaAttachment.None;
        character.EvaAttachedAsteroidId = null;
        character.EvaLocalOffset = Vec2.Zero;
        character.EvaVelocity = Vec2.Zero;
        character.UnsuitedVacuumSeconds = 0;
        character.MagneticBootsOn = false;

        // Same turret-runtime cleanup RemoveCharacter already does for a player who disconnects
        // while manning one - a seat nobody is sitting in any more must not stay shown as manned.
        if (character.ManningTurretId is { } turretId)
        {
            if (_turretRuntimes.TryGetValue(turretId, out var runtime))
            {
                runtime.MannedByPlayerId = null;
                _turretAimInput.Remove(runtime.Definition.Id);
            }
            character.ManningTurretId = null;
        }
    }
}
