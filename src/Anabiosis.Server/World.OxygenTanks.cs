using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Oxygen tanks: the first consumable that plugs into another item rather than being used on its
// own (OxygenTankDefinitions). A suit is a shell and a cutter is a torch - neither does anything
// without a charged tank in its socket, which is what turns "go outside" and "cut ore" from free
// actions into a trip you have to provision for.
//
// The socket is addressed by slot, not by item: a row slot for the cutter, Inventory.WornSuitSlot
// for the suit on your back.
public sealed partial class World
{
    // The tank is what a suit breathes when it leaves the ship: out in the field, or aboard a hull
    // that isn't yours and is open to space. Inside your own ship it costs nothing, and that is a
    // deliberate line rather than an oversight.
    //
    // Draining it in your own breached compartments was the honest simulation and it played badly:
    // a fight leaves rooms open to space for minutes at a time, so a crew that suited up correctly
    // still quietly suffocated somewhere in the middle of a long battle, which is a punishment for
    // doing the right thing. The rule that survives is the one worth teaching - the suit protects
    // you as long as it has a bottle, and the bottle is spent by going outside and by cutting.
    private void StepOxygenTanks(double deltaSeconds)
    {
        foreach (var character in _characters.Values)
        {
            if (!character.WearingSuit || !InVacuum(character))
                continue;
            character.Inventory.DrainTank(Inventory.WornSuitSlot, OxygenTankDefinitions.SuitDrainPerSecond * (float)deltaSeconds);
        }
    }

    private bool InVacuum(Character character)
    {
        if (character.IsOutside)
            return true;
        if (character.OnEnemyShip)
            return _enemyRoomOxygen.GetValueOrDefault(character.RoomId, FullOxygen) < OxygenSafeThreshold;
        return false;
    }

    // Docking tops every tank aboard back up, exactly as it refuels the reactor and welds the hull
    // shut (World.Voyage.cs's EnterStation). Air is a consumable a station sells by the pallet -
    // making the crew walk a tank to a rack after every trip would be bookkeeping, not gameplay.
    private void RefillOxygenTanks()
    {
        foreach (var character in _characters.Values)
        {
            var inventory = character.Inventory;
            if (inventory.TankCharge(Inventory.WornSuitSlot) is not null)
                inventory.RefillTank(Inventory.WornSuitSlot);
            for (var slot = 0; slot < Inventory.MainSlotCount; slot++)
                if (inventory.TankCharge(slot) is not null)
                    inventory.RefillTank(slot);
        }
    }

    private void TryAttachTank(Character character, int sourceSlotIndex, int targetSlotIndex) =>
        character.Inventory.TryAttachTank(sourceSlotIndex, targetSlotIndex);

    private void TryDetachTank(Character character, int slotIndex) =>
        character.Inventory.TryDetachTank(slotIndex);
}
