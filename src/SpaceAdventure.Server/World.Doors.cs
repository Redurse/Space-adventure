using System;
using System.Collections.Generic;
using System.Linq;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Door open/close (game_design.md Phase 3, M16) — click any door to toggle it, no proximity
// re-check server-side (same trusted-client convention as WireLinkInteractId/PowerSystemIndex:
// the client only ever sends this when it detected a nearby click). Ids are unique across Doors
// and AirlockOuterDoors, so one dictionary covers both.
//
// Each of the player's own ship's doors also has its own hit points now (game_design.md) - the
// same "quiet number, invisible until it matters" shape WallBlock's own Hp already has
// (World.WallBlocks.cs). A destroyed door is jammed open rather than merely non-functional: its
// frame/motor can no longer hold a seal, so ToggleDoor simply stops answering it until the E-key
// repair minigame (World.SystemRepair.cs, same one a SystemDevice/Junction already uses) restores
// it to full health. Station and enemy-ship doors are never entered into _doorHp at all, so
// DoorHp's own default fallback quietly keeps them permanently undamageable.
public sealed partial class World
{
    // Populated in World's constructor (World.cs) from the real Ship instance, not a field
    // initializer here - partial-class field initializer ordering across files isn't guaranteed,
    // and Ship itself is what this depends on.
    private readonly Dictionary<string, bool> _doorOpen = new();

    public const float DoorMaxHp = 100f;
    private readonly Dictionary<string, float> _doorHp = new();

    public bool IsDoorOpen(string doorId) => _doorOpen.TryGetValue(doorId, out var open) && open;

    private float DoorHp(string doorId) => _doorHp.GetValueOrDefault(doorId, DoorMaxHp);
    public bool IsDoorDestroyed(string doorId) => DoorHp(doorId) <= 0f;

    // Every physical door on the PLAYER'S OWN ship, interior Doors and outer airlocks alike, paired
    // with a room-membership test suited to each: an interior Door connects two rooms (its own
    // Connects), an AirlockOuterDoor only ever borders the one room the vacuum sits behind. Shared
    // by the repair-proximity check (World.SystemRepair.cs) and the random combat-damage roll
    // (World.EnemyAi.cs) so neither can drift out of sync with which doors actually exist.
    private IEnumerable<(string Id, Func<string, bool> Connects, Vec2 Position)> AllShipDoors() =>
        Ship.Doors.Select(d => (d.Id, (Func<string, bool>)d.Connects, d.Position))
            .Concat(Ship.AirlockOuterDoors.Select(d => (d.Id, (Func<string, bool>)(roomId => roomId == d.RoomId), d.Position)));

    // One shot, same as a wall block's own combat damage (World.WallBlocks.cs's DamageWallBlock
    // call with WallBlockMaxHp) - a hit either misses entirely or wrecks the door outright, no
    // partially-damaged middle state. Forces it open immediately: a door that loses structural
    // integrity while sealed shut doesn't stay sealed. Public like CutWire/AddWire - the enemy AI's
    // own random-attack roll (World.EnemyAi.cs) and tests both call this directly.
    public void DamageDoor(string doorId)
    {
        _doorHp[doorId] = 0f;
        _doorOpen[doorId] = true;
    }

    // "ТОПОР ГОШИ ДЛЯ ЛОМАНИЯ ДВЕРЕЙ" - a hand tool that chops down a closed door two swings at a
    // time (AxeChopDamage is exactly half DoorMaxHp) rather than destroying it outright in one hit
    // like DamageDoor's combat-damage roll above. Partial damage, same "quiet number, invisible
    // until it matters" shape as everything else here - only forces the door open once it's
    // actually reached 0.
    public const float AxeChopDamage = DoorMaxHp / 2f;
    private const float AxeSwingCooldownSeconds = 0.6f;
    private readonly Dictionary<int, float> _axeCooldowns = new();

    public void ChopDoor(string doorId, float damage)
    {
        var next = Math.Max(0f, DoorHp(doorId) - damage);
        _doorHp[doorId] = next;
        if (next <= 0f)
            _doorOpen[doorId] = true; // same as a destroyed door taking full combat damage
    }

    // Swings whatever axe is in hand at the nearest closed, not-yet-destroyed door in reach - an
    // already-open door has nothing left to chop through, and a destroyed one just needs the
    // ordinary System-repair minigame, not more hits. Own per-player cooldown (not DoorId-keyed),
    // matching World.Boarding.cs's _weaponCooldowns - only one swing lands regardless of how many
    // doors happen to be in reach at once.
    private void TryChopDoor(Character character)
    {
        if (!character.Inventory.IsHolding(ItemType.Axe))
            return;
        if (_axeCooldowns.TryGetValue(character.PlayerId, out var cooldown) && cooldown > 0)
            return;

        var target = AllShipDoors().FirstOrDefault(d =>
            d.Connects(character.RoomId) && !IsDoorOpen(d.Id) && !IsDoorDestroyed(d.Id) &&
            (d.Position - character.Position).Length() < InteractionRadius);
        if (target.Id is null)
            return;

        ChopDoor(target.Id, AxeChopDamage);
        _axeCooldowns[character.PlayerId] = AxeSwingCooldownSeconds;
    }

    private void StepAxeCooldowns(double deltaSeconds)
    {
        foreach (var playerId in _axeCooldowns.Keys.ToList())
            _axeCooldowns[playerId] = Math.Max(0, _axeCooldowns[playerId] - (float)deltaSeconds);
    }

    public void ToggleDoor(string doorId)
    {
        // The reactor's door-lock lever (World.cs) overrides every door/airlock at once - it's a
        // policy check only, so DamageDoor above still forces a door open regardless of it.
        if (DoorsLocked || !_doorOpen.ContainsKey(doorId) || IsDoorDestroyed(doorId))
            return;
        _doorOpen[doorId] = !_doorOpen[doorId];
    }

    private IReadOnlyList<DoorState> CreateDoorStates() =>
        _doorOpen.Select(kv =>
        {
            var (percent, tickPosition) = GetSystemRepairDisplay(kv.Key);
            return new DoorState(kv.Key, kv.Value, DoorHp(kv.Key), DoorMaxHp, percent, tickPosition);
        }).ToArray();

    // What the client shows a health bar over while the cutter is lit and actually aimed at a
    // door - same "quiet number, shown only while it's being worked" shape as GetWallToolTargetId
    // (World.WallBlocks.cs). Goes through the exact same FindAimedCutTarget (World.Cutting.cs) the
    // cutting action itself uses, so the bar can never show while a different block is what's
    // actually about to take the damage. Welding never targets a door (World.SystemRepair.cs's
    // E-key minigame is how a destroyed one gets fixed), so there's no welding branch here.
    private string? GetDoorToolTargetId(Character character) =>
        character.OnEnemyShip || character.IsOutside || !IsCutting(character.PlayerId)
            ? null
            : FindAimedCutTarget(character).DoorId;
}
