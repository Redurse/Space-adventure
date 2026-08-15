namespace SpaceAdventure.Shared.Model;

// A fixed pickup point for a hand tool or personal weapon (game_design.md section 7 — "шкафы:
// мелкие предметы... гаечные ключи, отвёртка..."). Supply is unlimited, same convention as
// AmmoStorage/SuitLocker — a finite stockpile is a later concern.
public sealed record ToolStation(string Id, string RoomId, float X, float Y, ItemType Item)
{
    public Vec2 Position => new(X, Y);
}
