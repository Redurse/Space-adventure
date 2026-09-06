namespace Anabiosis.Shared.Model;

// Which tank type fits which socket. A suit or a cutter takes an oxygen tank
// (OxygenTankDefinitions); a welding tool takes its own welding tank (WeldingTankDefinitions) -
// the two never interchange, so a welding tank offered to a cutter (or vice versa) is rejected the
// same way on the client (drag-drop feedback) and the server (Inventory.TryAttachTank).
public static class TankSockets
{
    public static bool HasSocket(ItemType ownerType) => AcceptedTank(ownerType) is not null;

    public static ItemType? AcceptedTank(ItemType ownerType) => ownerType switch
    {
        ItemType.Spacesuit or ItemType.Cutter => ItemType.OxygenTank,
        ItemType.WeldingTool => ItemType.WeldingTank,
        _ => null,
    };

    public static bool IsTank(ItemType type) => type is ItemType.OxygenTank or ItemType.WeldingTank;

    public static float FullChargeOf(ItemType tankType) => tankType switch
    {
        ItemType.WeldingTank => WeldingTankDefinitions.FullCharge,
        _ => OxygenTankDefinitions.FullCharge,
    };
}
