using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed class Character
{
    public const float MaxHealth = 100f; // Phase1 MVP: just a general health pool, no injury types yet

    public int PlayerId { get; }
    public Vec2 Position { get; set; }
    public string RoomId { get; set; }
    public string? ManningTurretId { get; set; }
    public Inventory Inventory { get; } = new();
    public bool CarryingAmmoCrate => Inventory.Has(ItemType.AmmoCrate);
    public float Health { get; set; } = MaxHealth;
    public bool WearingSuit => Inventory.Equipped[EquipSlot.Clothing] == ItemType.Spacesuit;
    public float SuitActionRemaining { get; set; } // >0 while mid-equip/unequip, movement locked
    public bool SuitActionEquipping { get; set; } // target state the in-progress action will set
    public Vec2 FacingDirection { get; set; } = new Vec2(-1, 0); // last nonzero move direction

    public Character(int playerId, Vec2 position, string roomId)
    {
        PlayerId = playerId;
        Position = position;
        RoomId = roomId;
    }
}
