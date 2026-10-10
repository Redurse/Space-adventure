namespace Anabiosis.Shared.Model;

// Where a defender starts, what it does, what it fights with, and whether it is wearing a suit - runtime health and
// movement live server-side (World.Boarding.cs / EnemyCrewSim), same split as Turret/TurretRuntime. A suited defender
// goes on fighting in a vented compartment; an unsuited one is on a clock the moment its air goes.
public sealed record EnemyCrewSpawn(string Id, string Name, string RoomId, float X, float Y, ItemType Weapon, bool Suited = false,
    EnemyCrewRole Role = EnemyCrewRole.Fighter, string? PostId = null)
{
    public Vec2 Position => new(X, Y);
}
