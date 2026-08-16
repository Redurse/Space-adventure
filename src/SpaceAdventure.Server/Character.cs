using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed class Character
{
    public const float MaxHealth = 100f;

    // Simplified injury model (game_design.md section 4, M12 scope): once Health drops below
    // this, the character is "bleeding" - it keeps draining on its own (World.Injuries.cs) even
    // after the original cause (e.g. decompression) is gone, until treated with a MedKit. No
    // named wound types (burns, etc.) yet - Health has always been "just a general pool" and
    // stays that way, this only adds one extra passive drain state on top of it.

    public int PlayerId { get; }
    public Vec2 Position { get; set; }
    public string RoomId { get; set; }
    public string? ManningTurretId { get; set; }
    public bool IsAtHelm { get; set; }
    public Inventory Inventory { get; } = new();
    public bool CarryingAmmoCrate => Inventory.Has(ItemType.AmmoCrate);
    public float Health { get; set; } = MaxHealth;
    public const float BleedingThreshold = 50f;
    public bool IsBleeding => Health > 0 && Health < BleedingThreshold;
    public bool WearingSuit => Inventory.Equipped[EquipSlot.Clothing] == ItemType.Spacesuit;

    // Wearing a suit and being safe in one are two different things now: the suit is a shell, and
    // what keeps anyone alive inside it is the oxygen tank socketed into it (OxygenTankDefinitions).
    // An empty suit still looks like a suit, which is exactly the mistake it lets a player make.
    public bool SuitSealed => WearingSuit && Inventory.HasWorkingTank(Inventory.WornSuitSlot);
    public float SuitActionRemaining { get; set; } // >0 while mid-equip/unequip, movement locked
    public bool SuitActionEquipping { get; set; } // target state the in-progress action will set
    public Vec2 FacingDirection { get; set; } = new Vec2(-1, 0); // last nonzero move direction
    // Where the head is turned, which is a different question from where the feet are going: the
    // suit lamp shines along it (the client's vision cone), and it's aimed with the mouse. Zero
    // means the player isn't aiming, and the body's own heading stands in.
    public Vec2 LookDirection { get; set; }

    // EVA state (game_design.md Phase 3, M17) - only meaningful while IsOutside. EvaLocalOffset's
    // meaning depends on EvaAttachedTo: relative to the ship's hull center in its own unrotated
    // frame when Ship, relative to that asteroid's center (asteroids don't rotate) when Asteroid,
    // or an absolute AsteroidField world position when None (free-floating - the only case
    // EvaVelocity/JetpackFuel matter, since attached movement is direct, not physics-driven).
    public bool IsOutside { get; set; }
    // Physically walked through the docked ship's outer airlock onto the station (World.
    // StationDocking.cs) - RoomId then refers to Station.Rooms instead of Ship.Rooms.
    public bool OnStation { get; set; }
    // Boarded the enemy ship through its hull breach (World.Boarding.cs) - RoomId then refers to
    // EnemyShipLayout.Rooms. Mutually exclusive with OnStation and IsOutside.
    public bool OnEnemyShip { get; set; }
    public EvaAttachment EvaAttachedTo { get; set; } = EvaAttachment.None;
    public string? EvaAttachedAsteroidId { get; set; }
    public Vec2 EvaLocalOffset { get; set; }
    public Vec2 EvaVelocity { get; set; }
    public const float JetpackMaxFuel = 100f;
    public float JetpackFuel { get; set; } = JetpackMaxFuel;
    // What was pushed away from, so that one body stops catching the drifter until they're clear
    // of it - and everything else still does (World.Eva.cs).
    public PushOffOrigin PushedOffFrom { get; set; } = PushOffOrigin.None;
    public string? PushedOffAsteroidId { get; set; }

    public Character(int playerId, Vec2 position, string roomId)
    {
        PlayerId = playerId;
        Position = position;
        RoomId = roomId;
    }
}
