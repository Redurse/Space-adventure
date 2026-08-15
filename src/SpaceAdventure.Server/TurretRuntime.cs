using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Live state for a Turret: who's manning it, current aim, and shot cooldown.
public sealed class TurretRuntime
{
    public Turret Definition { get; }
    public float AimDegrees { get; set; }
    public int? MannedByPlayerId { get; set; }
    public float CooldownRemaining { get; set; }
    public float PendingShotDamage { get; set; } // resolved and cleared on the next Step
    public int AmmoRemaining { get; set; } // ballistic
    public float Charge { get; set; } // laser
    public bool Damaged { get; set; } // local system damage — needs a wrench, blocks firing

    public TurretRuntime(Turret definition)
    {
        Definition = definition;
        AmmoRemaining = definition.MagazineCapacity;
        Charge = definition.MaxCharge;
    }
}
