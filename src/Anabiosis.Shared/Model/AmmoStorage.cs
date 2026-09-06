namespace Anabiosis.Shared.Model;

// A pickup point for ammo crates (game_design.md section 2 — "принести ящик снарядов со
// склада"). Just the static position - the finite stock actually held here lives in
// World.Ammo.cs/AmmoStorageState, the same split as Door/DoorState.
public sealed record AmmoStorage(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
