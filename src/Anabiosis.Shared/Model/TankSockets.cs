namespace Anabiosis.Shared.Model;

// Which tank type fits which socket. A suit or a cutter takes an oxygen tank
// (OxygenTankDefinitions); a welding tool takes its own welding tank (WeldingTankDefinitions) -
// the two never interchange, so a welding tank offered to a cutter (or vice versa) is rejected the
// same way on the client (drag-drop feedback) and the server (Inventory.TryAttachTank).
// A gun is a socket too: its ammunition (GunDefinitions) plugs in the same way, one charge per round.
public static class TankSockets
{
    public static bool HasSocket(ItemType ownerType) => AcceptedTank(ownerType) is not null;

    public static ItemType? AcceptedTank(ItemType ownerType) => ownerType switch
    {
        ItemType.Spacesuit or ItemType.Cutter => ItemType.OxygenTank,
        ItemType.WeldingTool => ItemType.WeldingTank,
        _ => GunDefinitions.Of(ownerType)?.Ammo,
    };

    // Whether this owner takes this tank: its one kind of tank, or - for a gun - any round it can fire.
    public static bool Accepts(ItemType ownerType, ItemType tankType) =>
        AcceptedTank(ownerType) == tankType || (GunDefinitions.Of(ownerType) is { } gun && GunDefinitions.Accepts(gun, tankType));

    public static bool IsTank(ItemType type) => type is ItemType.OxygenTank or ItemType.WeldingTank || GunDefinitions.IsAmmo(type);

    // Loose rounds (GunDefinitions.IsStackAmmo): they top a gun up and the rest stays in the stack.
    public static bool IsStack(ItemType tankType) => GunDefinitions.IsStackAmmo(tankType);

    // What one full item of this kind holds: a tank, a magazine, or a full stack of loose rounds.
    public static float FullChargeOf(ItemType tankType) => tankType switch
    {
        ItemType.WeldingTank => WeldingTankDefinitions.FullCharge,
        _ when GunDefinitions.IsAmmo(tankType) => GunDefinitions.StackSize(tankType),
        _ => OxygenTankDefinitions.FullCharge,
    };

    // How much the socket itself takes. A gun holds its own number of rounds, which is not always one
    // stack of them (a shotgun holds six shells out of a stack of twelve, an automatic one two stacks).
    public static float CapacityOf(ItemType ownerType) =>
        GunDefinitions.Of(ownerType) is { } gun ? gun.Capacity
        : AcceptedTank(ownerType) is { } tank ? FullChargeOf(tank) : 0f;
}
