namespace Anabiosis.Shared.Model;

// A card table's physical position - the same shape as ReactorBlock, but nothing to click: two
// crew members simply standing here together starts a hand of Дурак переводной
// (World.CardGame.cs's StepCardGame). Exactly one per hull, in the cockpit/bridge room.
public sealed record CardTable(string Id, string RoomId, float X, float Y)
{
    public Vec2 Position => new(X, Y);
}
