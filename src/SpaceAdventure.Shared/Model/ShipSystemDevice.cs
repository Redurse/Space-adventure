namespace SpaceAdventure.Shared.Model;

// A physical, damageable block for one of the power grid's systems (game_design.md section 1 —
// "можно потерять систему целиком... повреждена локальная коробка"). One device per
// PowerSystemId; damage state lives in PowerGrid, repair uses the same wrench/screwdriver as
// turrets.
public sealed record ShipSystemDevice(string Id, string RoomId, float X, float Y, PowerSystemId System, float SizeScale = 1f)
{
    public Vec2 Position => new(X, Y);
}
