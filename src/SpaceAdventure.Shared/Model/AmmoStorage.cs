namespace SpaceAdventure.Shared.Model;

// A pickup point for ammo crates (game_design.md section 2 — "принести ящик снарядов со
// склада"). Supply is treated as unlimited for M6; a finite stockpile is a later concern.
public sealed record AmmoStorage(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
