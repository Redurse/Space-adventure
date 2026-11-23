using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// What a station's guards do once the station is on alert (World.StationCrime.cs): they shoot, with the assault rifle
// they carry, the way a hostile ship's defenders do (World.Boarding.cs) - real rounds that leave a tracer, stop at
// walls and closed doors, and wound by the rules of the gun (World.GunShots.cs). Both kinds of guard fire: the
// ones posted at a station's counters (NpcKind.Security) and the ones that patrol it on foot
// (ResidentRole.Guard, World.StationResidents.cs), which until now rushed the crew but never fired at all.
// A guard takes a moment to bring the gun up on a target it has just got a line on, keeps its own pace of fire,
// counts the rounds left in the magazine and stops to reload when it runs dry. Its rounds go through the people of
// the station - they are not shooting at them - so a guard never hits a civilian or a colleague, and a player is
// never charged for a death that was a guard's own bullet.
public sealed partial class World
{
    // How far a guard opens fire from. The rifle's own reach is shorter: a guard sees across a room and shoots.
    private const float GuardFireRange = 9f;
    // The weapons skill of a guard: the security officer's.
    private static readonly float GuardWeaponsSkill = WeaponSkills.Starting(CrewRole.Security);

    private sealed class GuardFireState
    {
        public float AimSeconds;
        public float Cooldown;
        public int RoundsLeft = -1;
    }

    private readonly Dictionary<string, GuardFireState> _guardFire = new();

    // Test hook: guards stand down (for tests that measure something else on a station with the alert up).
    public bool DebugGuardsHoldFire { get; set; }

    private void StepGuardFire(List<Character> onStation, double deltaSeconds)
    {
        if (!_stationAlerted || DebugGuardsHoldFire)
            return;
        var gun = GunDefinitions.Of(ItemType.Rifle)!;
        var dt = (float)deltaSeconds;
        var targets = onStation.Where(c => !c.IsDead && !c.Body.Unconscious && !c.Body.InArrest).ToList();

        void Step(string id, Vec2 position)
        {
            if (!_guardFire.TryGetValue(id, out var state))
                _guardFire[id] = state = new GuardFireState();
            state.Cooldown = Math.Max(0f, state.Cooldown - dt);

            var target = NearestVisibleTarget(position, targets);
            if (target is null)
            {
                state.AimSeconds = 0f;
                return;
            }
            state.AimSeconds += dt;
            if (state.AimSeconds < CrewAimSeconds || state.Cooldown > 0f)
                return;

            if (state.RoundsLeft < 0)
                state.RoundsLeft = gun.Capacity;
            var roomId = Station.Rooms.FirstOrDefault(r => r.Contains(position))?.Id ?? "";
            FireGunRound(new Shooter(ShotScene.Station, FromEnemy: true, PlayerId: null, CrewId: id, GuardWeaponsSkill, DamageBonus: 0f),
                position, roomId, gun, target.Position - position);
            state.RoundsLeft--;
            state.Cooldown = state.RoundsLeft > 0 ? gun.ReloadFor(GuardWeaponsSkill) : CrewReloadSeconds;
            if (state.RoundsLeft <= 0)
                state.RoundsLeft = gun.Capacity;
        }

        foreach (var guard in Station.Npcs.Where(n => n.Kind == NpcKind.Security && IsGuardAlive(n.Id)))
            Step(guard.Id, guard.Position);
        if (IsDocked && !DockedAtAbandonedStation)
            foreach (var (id, position) in ResidentSim().LivingGuards.ToList())
                Step(id, position);
    }

    // The nearest of the crew within a guard's range with nothing solid between: no wall and no closed door.
    private Character? NearestVisibleTarget(Vec2 from, IReadOnlyList<Character> targets)
    {
        Character? best = null;
        var bestDistance = double.MaxValue;
        foreach (var target in targets)
        {
            var toTarget = target.Position - from;
            var distance = toTarget.Length();
            if (distance > GuardFireRange || distance >= bestDistance)
                continue;
            if (distance > 0.05 && ShotTrace.ToWall(Station.Tiles, from, toTarget * (1.0 / distance), (float)distance).Distance < distance - ShotTrace.Step)
                continue;
            best = target;
            bestDistance = distance;
        }
        return best;
    }
}
