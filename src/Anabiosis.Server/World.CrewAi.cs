using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// What a hired bot actually does every tick (World.Recruiting.cs hires it, this runs it). Each
// role acts on the ship directly rather than through HandleInteract/ApplyCommand's proximity
// checks - the bot is already standing at its post by construction (CrewPostFor), so there is
// nothing to walk to. This is deliberately closer in spirit to World.EnemyAi.cs's dice-roll AI
// than to a real agent: fixed jobs, no perception, no pathing - "does its one job continuously",
// which is exactly what a crew slot with nobody to play it needs to stop being empty.
public sealed partial class World
{
    private const float MechanicActionCooldownSeconds = 4f;
    private const float ScientistHealPerSecond = 8f;
    private float _scientistBotActionTimer;
    private const float BotTurretAimToleranceDegrees = 2f;

    private readonly Dictionary<int, float> _mechanicCooldowns = new();

    private void StepCrewBots(double deltaSeconds)
    {
        foreach (var bot in _characters.Values.Where(c => c.IsBot && c.Role is not null && !c.IsDowned).ToList())
        {
            switch (bot.Role)
            {
                case CrewRole.Security:
                    StepSecurityBot(bot, deltaSeconds);
                    break;
                case CrewRole.Engineer:
                    StepEngineerBot(bot);
                    break;
                case CrewRole.Mechanic:
                    StepMechanicBot(bot, deltaSeconds);
                    break;
                case CrewRole.Scientist:
                    StepScientistBot(deltaSeconds);
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
        if (!IsInBattle || runtime.Damaged)
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

        var bearingDegrees = MathF.Atan2((float)toEnemy.Y, (float)toEnemy.X) * (180f / MathF.PI);
        var delta = ShortestAngle(bearingDegrees - mount.OutwardDegrees - runtime.AimDegrees); // full 360 degree traverse: shortest way round

        _turretAimInput[turretId] = MathF.Abs(delta) < 1f ? 0f : MathF.Sign(delta);
        if (MathF.Abs(delta) < BotTurretAimToleranceDegrees)
            TryFire(runtime); // no-ops itself if the cooldown/ammo/charge isn't ready
    }

    private static float ShortestAngle(float degrees) => ((degrees % 360f) + 540f) % 360f - 180f;

    // The Engineer used to nudge the distribution sliders; there are none any more - the ship's grid
    // feeds every wired device by itself (World.Power.cs, W2 of wiring-plan.md). Rewiring work for
    // the Engineer arrives with player-editable wiring (W5/W9).
    private static void StepEngineerBot(Character bot)
    {
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
    // has no consumable to run out of (it's a standing crew job, not an item). Health > 0 used to
    // be Health >= 0's mistaken twin: it excluded the one crewmate who needs it most - there is no
    // separate "dead" state (World.Injuries.cs), so a character at exactly 0 is incapacitated
    // (can't weld/cut) rather than gone, and this was the one path that could bring them back
    // without a MedKit in hand. Same healing job the role always did (M42 just renamed
    // Medic->Scientist and gave a live player at the console a scanner to run too, M44) - a hired
    // bot has no perception of the scanner side of the job, only this one.
    private void StepScientistBot(double deltaSeconds)
    {
        // A heart that has stopped comes first, then whoever is worst off (World.Treatment.cs has the human version of all this).
        var patient = _characters.Values
            .Where(c => !c.Body.Dead && c.Body.NeedsTreatment)
            .OrderByDescending(c => c.Body.InArrest)
            .ThenBy(c => c.Body.Vitality)
            .FirstOrDefault();
        if (patient is null)
        {
            _scientistBotActionTimer = 0f;
            return;
        }

        AdministerFirstAid(patient.Body, (float)deltaSeconds, ScientistHealPerSecond, ref _scientistBotActionTimer);
    }

    // A safety net, not a pilot: if nobody living is at the helm and the ship is coasting, brake it
    // rather than let it drift wherever its last commanded thrust was pointed. Doesn't touch the
    // helm at all while a real player is flying. Docked is excluded - a docked ship isn't drifting
    // anywhere, there's nothing to brake.
    private void StepCaptainBot()
    {
        if (_characters.Values.Any(c => !c.IsBot && c.IsAtHelm))
            return;
        if (IsDocked)
            return;
        if (_shipAutoStabilize || _shipVelocity.Length() < 0.5f)
            return;

        EngageAutoStabilize();
    }
}
