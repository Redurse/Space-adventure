using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Theft and arrest aboard a station (game_design.md section 10 - "на станции можно воровать вещи;
// если поймает охрана — арест: штраф, конфискация награбленного, потеря репутации с фракцией.
// Сопротивление аресту (стрельба) превращает конфликт в перестрелку с охраной станции").
//
// The whole loop rides on machinery that already exists: crates are picked up with the same [F]
// as everything else (World.Interact.cs), the guard is an ordinary StationNpc, and resisting reuses
// the boarding weapon code (World.Boarding.cs) rather than inventing a second combat system. What's
// new here is only the consequence: carrying stolen goods past a guard gets you caught.
public sealed partial class World
{
    private const float GuardSightRadius = 4f; // same room and roughly across it
    private const float ArrestCheckIntervalSeconds = 1.5f;
    private const int FinePerStolenItem = 60;
    private const int StandingPenaltyPerArrest = -25;
    private const int StandingPenaltyForKillingGuard = -50; // far worse than being caught

    private readonly HashSet<string> _lootedCrateIds = new();
    private readonly Dictionary<int, int> _stolenItemCount = new();
    private readonly Dictionary<string, float> _guardHealth = new();
    private float _arrestCheckCooldown = ArrestCheckIntervalSeconds;

    // Guards only shoot once shot at - a thief who comes quietly is arrested, not gunned down.
    private bool _stationAlerted;

    public int GetStolenItemCount(int playerId) => _stolenItemCount.GetValueOrDefault(playerId);
    public bool IsCrateLooted(string crateId) => _lootedCrateIds.Contains(crateId);
    public bool IsStationAlerted => _stationAlerted;

    private const float GuardMaxHealth = 80f;

    private float GuardHealthOf(string npcId) =>
        _guardHealth.TryGetValue(npcId, out var health) ? health : GuardMaxHealth;

    private bool IsGuardAlive(string npcId) => GuardHealthOf(npcId) > 0;

    // Anyone on a station can be hurt, not only its guards (direct user request): the people behind the counters
    // share the guards' health table, with less health of their own. Shooting any of them puts the station on
    // alert, and killing one costs the same standing as killing a guard. A dead one serves nobody until the next visit.
    private const float StaffMaxHealth = 60f;
    private static float NpcMaxHealth(StationNpc npc) => npc.Kind == NpcKind.Security ? GuardMaxHealth : StaffMaxHealth;
    private float NpcHealthOf(StationNpc npc) => _guardHealth.TryGetValue(npc.Id, out var health) ? health : NpcMaxHealth(npc);
    private bool IsNpcAlive(StationNpc npc) => NpcHealthOf(npc) > 0;
    private bool HasLivingNpc(NpcKind kind) => Station.Npcs.Any(n => n.Kind == kind && IsNpcAlive(n));

    // Taking a crate. Called from HandleInteract's station branch, so it competes with nothing -
    // there's nothing else to interact with in a station room.
    private bool TryStealCrate(Character character)
    {
        var crate = Station.Crates.FirstOrDefault(c =>
            c.RoomId == character.RoomId &&
            !_lootedCrateIds.Contains(c.Id) &&
            (c.Position - character.Position).Length() < InteractionRadius);
        if (crate is null)
            return false;

        if (!character.Inventory.TryAdd(crate.Item))
            return true; // reached it but had nowhere to put it - still counts as handled

        _lootedCrateIds.Add(crate.Id);
        _stolenItemCount[character.PlayerId] = GetStolenItemCount(character.PlayerId) + 1;
        return true;
    }

    // Shooting a guard turns a theft into a firefight (game_design.md section 10). Routed here
    // from TryFirePersonalWeapon so the two combat contexts share one weapon model.
    // A round that reached a guard (World.PersonalShots.cs). Same consequences as the old
    // fire-at-range version had: the station stops treating it as an arrest and starts treating it
    // as a fight, and killing one is what really costs standing.
    private bool TryHitGuardAt(Vec2 point, string roomId, float damage)
    {
        var guard = Station.Npcs.FirstOrDefault(n =>
            IsNpcAlive(n) &&
            Station.Rooms.FirstOrDefault(r => r.Contains(n.Position))?.Id == roomId &&
            (n.Position - point).Length() <= BulletRadius);
        if (guard is not null)
        {
            HitGuard(guard, damage);
            return true;
        }
        return TryHitResidentAt(point, damage);
    }

    // The people walking about the station (World.StationResidents.cs) can be hit like anyone else.
    private bool TryHitResidentAt(Vec2 point, float damage)
    {
        if (!IsDocked || DockedAtAbandonedStation)
            return false;
        foreach (var (id, position) in ResidentSim().Living)
        {
            if ((position - point).Length() > BulletRadius)
                continue;
            HitResident(id, damage);
            return true;
        }
        return false;
    }

    private void HitResident(string residentId, float damage)
    {
        _stationAlerted = true;
        if (ResidentSim().Damage(residentId, damage))
            AdjustStanding(DockedFaction, StandingPenaltyForKillingGuard);
    }

    private void HitGuard(StationNpc guard, float damage)
    {
        _stationAlerted = true;
        var health = Math.Max(0, NpcHealthOf(guard) - damage);
        _guardHealth[guard.Id] = health;
        if (health <= 0)
            AdjustStanding(DockedFaction, StandingPenaltyForKillingGuard);
    }

    private bool TryFireAtGuard(Character character, ItemType weapon)
    {
        // StationNpc carries no RoomId of its own, so "same room" is resolved by which room rect
        // contains it - the same way the client derives a boarder's room (Game1.ComputeHint).
        var guard = Station.Npcs.FirstOrDefault(n =>
            n.Kind == NpcKind.Security &&
            IsGuardAlive(n.Id) &&
            Station.Rooms.FirstOrDefault(r => r.Contains(n.Position))?.Id == character.RoomId &&
            (n.Position - character.Position).Length() <= WeaponDefinitions.Range(weapon));
        if (guard is null)
            return false;

        _stationAlerted = true; // resisting arrest - the guard fights back from now on
        var health = Math.Max(0, GuardHealthOf(guard.Id) - (WeaponDefinitions.DamagePerHit(weapon) + WeaponDamageBonus));
        _guardHealth[guard.Id] = health;

        if (health <= 0)
            AdjustStanding(DockedFaction, StandingPenaltyForKillingGuard);
        return true;
    }

    private void StepStationCrime(double deltaSeconds)
    {
        var onStation = _characters.Values.Where(c => c.OnStation && c.Health > 0).ToList();
        if (onStation.Count == 0)
        {
            // Leaving the station ends the standoff; the goods are away clean.
            _stationAlerted = false;
            return;
        }

        StepGuardFire(onStation, deltaSeconds);
        StepArrestChecks(onStation, deltaSeconds);
        StepStationResidents(deltaSeconds, onStation);
    }

    private void StepArrestChecks(List<Character> onStation, double deltaSeconds)
    {
        _arrestCheckCooldown -= (float)deltaSeconds;
        if (_arrestCheckCooldown > 0)
            return;
        _arrestCheckCooldown = ArrestCheckIntervalSeconds;

        foreach (var character in onStation)
        {
            if (GetStolenItemCount(character.PlayerId) == 0)
                continue;

            var spotted = Station.Npcs.Any(n =>
                n.Kind == NpcKind.Security &&
                IsGuardAlive(n.Id) &&
                Station.Rooms.FirstOrDefault(r => r.Contains(n.Position))?.Id == character.RoomId &&
                (n.Position - character.Position).Length() <= GuardSightRadius);
            if (spotted)
                Arrest(character);
        }
    }

    // Fine, confiscation, reputation hit - all three, per the design doc. The fine is capped at
    // whatever the crew actually has rather than pushing them into debt, since there's no debt
    // mechanic for that to mean anything.
    private void Arrest(Character character)
    {
        var stolen = GetStolenItemCount(character.PlayerId);
        Credits = Math.Max(0, Credits - FinePerStolenItem * stolen);

        foreach (var crate in Station.Crates.Where(c => _lootedCrateIds.Contains(c.Id)))
            character.Inventory.TryRemove(crate.Item);

        _stolenItemCount[character.PlayerId] = 0;
        AdjustStanding(DockedFaction, StandingPenaltyPerArrest);
    }

    // Crates restock and the guard stands down between visits - a station the player left in a
    // firefight isn't permanently ruined, it just remembers them through their reputation.
    private void ResetStationCrimeState()
    {
        _lootedCrateIds.Clear();
        _stolenItemCount.Clear();
        _guardHealth.Clear();
        _stationAlerted = false;
        _arrestCheckCooldown = ArrestCheckIntervalSeconds;
        _guardFire.Clear();
    }

    private IReadOnlyList<StationCrateState> CreateStationCrateStates() =>
        Station.Crates.Select(c => new StationCrateState(c.Id, _lootedCrateIds.Contains(c.Id))).ToArray();

    private IReadOnlyList<StationGuardState> CreateStationGuardStates() =>
        Station.Npcs
            // every guard, and anyone else on the staff who has been hurt (the client needs their health too)
            .Where(n => n.Kind == NpcKind.Security || _guardHealth.ContainsKey(n.Id))
            .Select(n => new StationGuardState(n.Id, NpcHealthOf(n), NpcMaxHealth(n), _stationAlerted))
            .ToArray();
}
