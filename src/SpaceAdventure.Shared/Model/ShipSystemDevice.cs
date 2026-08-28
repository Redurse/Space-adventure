namespace SpaceAdventure.Shared.Model;

// A physical, damageable block for one of the power grid's systems (game_design.md section 1 —
// "можно потерять систему целиком... повреждена локальная коробка"). One device per
// PowerSystemId; damage state lives in PowerGrid, repair uses the same wrench/screwdriver as
// turrets.
public sealed record ShipSystemDevice(string Id, string RoomId, float X, float Y, PowerSystemId System, float SizeScale = 1f,
    // Content-каталог отсеков - an Engine-system device's own contribution to ship-wide thrust
    // (marching engine rooms) or turn rate (RCS rooms), summed in World.ShipBuilding.cs's
    // RecomputeDeviceBonuses. 0 for every hand-authored hull's own engine devices.
    float ThrustBonus = 0f, float TurnBonus = 0f,
    // Shields-system device's own contribution to ShieldSystem.MaxPoints (CustomDeviceDef's own doc
    // comment explains why this can't just be "count of Shields-system devices").
    float CapacityBonus = 0f)
{
    public Vec2 Position => new(X, Y);
}
