using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// What a hired bot actually does every tick (World.Recruiting.cs hires it, this runs it). Each
// role acts on the ship directly rather than through HandleInteract/ApplyCommand's proximity
// checks - the bot is already standing at its post by construction (CrewPostFor), so there is
// nothing to walk to. This is deliberately closer in spirit to World.EnemyAi.cs's dice-roll AI
// than to a real agent: fixed jobs, no perception, no pathing - "does its one job continuously",
// which is exactly what a crew slot with nobody to play it needs to stop being empty.
public sealed partial class World
{
    private const float MechanicActionCooldownSeconds = 4f;
    private const float MedicHealPerSecond = 8f;
    private const float BotTurretAimToleranceDegrees = 2f;

    private readonly Dictionary<int, float> _mechanicCooldowns = new();

    private void StepCrewBots(double deltaSeconds)
    {
        foreach (var bot in _characters.Values.Where(c => c.IsBot && c.Role is not null).ToList())
        {
            switch (bot.Role)
            {
                case CrewRole.Security:
                    StepSecurityBot(bot, deltaSeconds);
                    break;
                case CrewRole.Engineer:
                    StepEngineerBot();
                    break;
                case CrewRole.Mechanic:
                    StepMechanicBot(bot, deltaSeconds);
                    break;
                case CrewRole.Medic:
                    StepMedicBot(deltaSeconds);
                    break;
                case CrewRole.Captain:
                    StepCaptainBot();
                    break;
            }
        }
    }

    // Claims a turret the moment one frees up (traded away, or simply never got one at hire time
    // because every gun was already staffed), then aims and fires at whatever hull is in front of
    // the guns right now - the same target a live gunner would see on their periscope.
    private void StepSecurityBot(Character bot, double deltaSeconds)
    {
        if (bot.ManningTurretId is null)
            TryAssignSecurityBotToTurret(bot);

        if (bot.ManningTurretId is not { } turretId)
            return;

        var runtime = _turretRuntimes[turretId];
        if (Phase != VoyagePhase.Battle || runtime.Damaged)
        {
            _turretAimInput[turretId] = 0f;
            return;
        }

        var turret = Ship.Turrets.First(t => t.Id == turretId);
        var mount = TurretMount.For(Ship.Rooms, Ship.Turrets, turret);
        var (hullLocalCenter, _) = GetHullLocalBounds();

        // Same bearing math TryFire uses to send a shot out of the mount, run in reverse: fold the
        // enemy's field position into the mount's unrotated local frame, then read the angle off
        // to it - the frame OutwardDegrees/aim are already defined in.
        var enemyLocal = RotateWorldToLocal(EnemyShipFieldPosition - _shipFieldPosition, _shipRotationDegrees) + hullLocalCenter;
        var toEnemy = enemyLocal - mount.Position;
        if (toEnemy.Length() < 0.1f)
            return;

        var bearingDegrees = MathF.Atan2(toEnemy.Y, toEnemy.X) * (180f / MathF.PI);
        var wanted = Math.Clamp(ShortestAngle(bearingDegrees - mount.OutwardDegrees),
            turret.MinAimDegrees, turret.MaxAimDegrees);
        var delta = wanted - runtime.AimDegrees;

        _turretAimInput[turretId] = MathF.Abs(delta) < 1f ? 0f : MathF.Sign(delta);
        if (MathF.Abs(delta) < BotTurretAimToleranceDegrees)
            TryFire(runtime); // no-ops itself if the cooldown/ammo/charge isn't ready
    }

    private static float ShortestAngle(float degrees) => ((degrees % 360f) + 540f) % 360f - 180f;

    // Keeps life support and defense fed before anything else, nudging the slider the same way a
    // player's held key would (PowerGrid's single adjust slot) - only when nobody else touched it
    // this tick, since a live Engineer's own input already went through ApplyCommand earlier in
    // the same tick and this would otherwise immediately overwrite it.
    private static readonly PowerSystemId[] EngineerPriority =
        { PowerSystemId.Oxygen, PowerSystemId.Shields, PowerSystemId.Engine, PowerSystemId.WeaponCharger };

    private void StepEngineerBot()
    {
        const float TargetShare = 12f; // a modest, sustainable allocation per system - not "max everything"
        foreach (var system in EngineerPriority)
        {
            if (PowerGrid.GetAllocation(system) < TargetShare)
            {
                PowerGrid.ApplyInput((int)system, 1f);
                return;
            }
        }
        PowerGrid.ApplyInput(-1, 0f); // everything it cares about is already fed - hands off the slider
    }

    // Keeps the reactor topped up and, on the same slow cadence, clears one broken thing ship-wide
    // (a jammed turret or a cut wire) - throttled so hiring a Mechanic doesn't make damage stop
    // mattering, only recover from on its own over time instead of needing a player to notice it.
    private void StepMechanicBot(Character bot, double deltaSeconds)
    {
        var cooldown = _mechanicCooldowns.GetValueOrDefault(bot.PlayerId) - (float)deltaSeconds;
        if (cooldown > 0)
        {
            _mechanicCooldowns[bot.PlayerId] = cooldown;
            return;
        }

        for (var slot = 0; slot < Reactor.RodSlotCount; slot++)
        {
            if (PowerGrid.Reactor.IsRodLoaded(slot))
                continue;
            PowerGrid.Reactor.InsertRod(slot);
            _mechanicCooldowns[bot.PlayerId] = MechanicActionCooldownSeconds;
            return;
        }

        var damagedTurret = _turretRuntimes.Values.FirstOrDefault(t => t.Damaged);
        if (damagedTurret is not null)
        {
            damagedTurret.Damaged = false;
            _mechanicCooldowns[bot.PlayerId] = MechanicActionCooldownSeconds;
            return;
        }

        var brokenDevice = Ship.SystemDevices.FirstOrDefault(d => !IsDeviceConnected(d.Id));
        if (brokenDevice is not null)
        {
            RepairDeviceWiring(brokenDevice.Id);
            _mechanicCooldowns[bot.PlayerId] = MechanicActionCooldownSeconds;
        }
    }

    // Tends whoever's worst off first - continuous rather than a one-shot MedKit, since the bot
    // has no consumable to run out of (it's a standing crew job, not an item).
    private void StepMedicBot(double deltaSeconds)
    {
        var patient = _characters.Values
            .Where(c => c.Health < Character.MaxHealth && c.Health > 0)
            .OrderBy(c => c.Health)
            .FirstOrDefault();
        if (patient is null)
            return;

        patient.Health = Math.Min(Character.MaxHealth, patient.Health + MedicHealPerSecond * (float)deltaSeconds);
    }

    // A safety net, not a pilot: if nobody living is at the helm and the ship is coasting, brake it
    // rather than let it drift wherever its last commanded thrust was pointed. Doesn't touch the
    // helm at all while a real player is flying.
    private void StepCaptainBot()
    {
        if (_characters.Values.Any(c => !c.IsBot && c.IsAtHelm))
            return;
        if (Phase is not (VoyagePhase.Traveling or VoyagePhase.Battle or VoyagePhase.AsteroidField or VoyagePhase.StationApproach))
            return;
        if (_shipAutoStabilize || _shipVelocity.Length() < 0.5f)
            return;

        EngageAutoStabilize();
    }
}
