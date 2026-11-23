using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Test hooks for the gunfire rules (World.GunShots.cs).
public sealed partial class World
{
    // Puts an item straight into the slot it is worn in, the way dragging it there would.
    public void DebugEquip(int playerId, ItemType item)
    {
        if (EquipSlotDefinitions.SlotFor(item) is { } slot)
            _characters[playerId].Inventory.Equipped[slot] = item;
    }

    public float DebugWeaponsSkill(int playerId) => _characters[playerId].WeaponsSkill;

    // The station goes on alert, as if the crew had just fired on it.
    public void DebugRaiseStationAlert() => _stationAlerted = true;

    public int DebugResidentGuardCount() => IsDocked ? ResidentSim().GuardCount : 0;

    public void DebugKillStationNpc(string npcId) => _guardHealth[npcId] = 0f;
}
