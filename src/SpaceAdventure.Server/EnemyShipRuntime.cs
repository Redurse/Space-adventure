using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// One hostile hull in the field: the abstract HP that combat already used, plus the place it
// occupies and the state its gunner needs. Kept separate from EnemyShip so the HP model stays the
// small testable thing it was.
public sealed class EnemyShipRuntime
{
    public string Id { get; }
    public EnemyShip Ship { get; }
    // Which hull this is, and therefore what a boarding party finds inside it (EnemyShipClass).
    public EnemyShipLayout Layout { get; }
    public Vec2 Position { get; set; }
    public Vec2 Velocity { get; set; }
    public float RotationDegrees { get; set; }
    public float FireCooldown { get; set; }
    public bool Alive => Ship.Hp > 0;

    public EnemyShipRuntime(string id, float maxHp, Vec2 position, EnemyShipLayout layout)
    {
        Id = id;
        Ship = new EnemyShip(maxHp);
        Position = position;
        Layout = layout;
    }
}
