using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

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
    // Hired crew (World.Recruiting.cs, game_design.md section 10): a bot lives in the same
    // _characters dictionary as a live player, keyed by a negative id no GameServer connection can
    // ever hand out, and is driven entirely by World.CrewAi.cs rather than ApplyCommand - nothing
    // ever sends it a ClientCommand. BotName/Role are null for an ordinary player.
    public bool IsBot { get; init; }
    public string? BotName { get; init; }
    // set, not init, unlike IsBot/BotName above - a bot's Role is fixed for its whole time aboard
    // (set once at hire, World.Recruiting.cs), but a live player can pick/clear their own from the
    // crew panel at any time (ApplyCommand's SetOwnRoleTo/ClearOwnRolePressed) - purely a
    // self-identification label, not a job assignment, so nothing else needs to react to it changing.
    public CrewRole? Role { get; set; }
    // Typed at the menu, echoed on every ClientCommand (World.cs's ApplyCommand) - null until the
    // first command arrives, and always null for a bot (BotName is its name instead).
    public string? Nickname { get; set; }
    // Round-trip time in ms, updated in ApplyCommand off the client's own echoed timestamp
    // (CharacterState.PingMs's own doc comment has the full reasoning).
    public float PingMs { get; set; }
    public Vec2 Position { get; set; }
    public string RoomId { get; set; }
    public string? ManningTurretId { get; set; }
    public bool IsAtHelm { get; set; }
    public Inventory Inventory { get; } = new();
    public bool CarryingAmmoCrate => Inventory.Has(ItemType.AmmoCrate);
    public float Health { get; set; } = MaxHealth;
    public const float BleedingThreshold = 50f;
    public bool IsBleeding => Health > 0 && Health < BleedingThreshold;
    public bool WearingSuit => Inventory.Equipped[EquipSlot.Suit] == ItemType.Spacesuit;

    // Wearing a suit and being safe in one are two different things now: the suit is a shell, and
    // what keeps anyone alive inside it is the oxygen tank socketed into it (OxygenTankDefinitions).
    // An empty suit still looks like a suit, which is exactly the mistake it lets a player make.
    public bool SuitSealed => WearingSuit && Inventory.HasWorkingTank(Inventory.WornSuitSlot);
    public float SuitActionRemaining { get; set; } // >0 while mid-equip/unequip, movement locked
    public bool SuitActionEquipping { get; set; } // target state the in-progress action will set
    // Which locker the in-progress action started at - taking a suit out empties it, putting one
    // back fills it, resolved once the action finishes (World.Movement.cs).
    public string? SuitActionLockerId { get; set; }
    public Vec2 FacingDirection { get; set; } = new Vec2(-1, 0); // last nonzero move direction
    // Where the head is turned, which is a different question from where the feet are going: the
    // suit lamp shines along it (the client's vision cone), and it's aimed with the mouse. Zero
    // means the player isn't aiming, and the body's own heading stands in.
    public Vec2 LookDirection { get; set; }

    // Which pin a wire-lay is anchored at (World.Wiring.cs's HandlePinInteract) - null when not
    // laying. Walking to the second pin is just ordinary movement, no special mode.
    public PinRef? LayingWireFromPin { get; set; }

    // Bend points fixed so far along an in-progress wire lay (World.Wiring.cs's HandleWireBend) -
    // purely cosmetic routing for the eventual Wire.Bends, never read by anything connectivity-
    // related. Cleared whenever the lay starts fresh, restarts at a new anchor, completes, or is
    // cancelled outright (RemoveAt-ing the last one instead is HandleWireLayCancel's job).
    public List<Vec2> LayingWireBends { get; } = new();

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
    // How long this character has been in vacuum without a sealed suit. Zero whenever they are
    // inside, or outside in a working suit - see World.Eva.cs's UnsuitedGraceSeconds.
    public double UnsuitedVacuumSeconds { get; set; }

    public const float JetpackMaxFuel = 500f; // 5x the original 100 - more room to correct a bad jump before drifting forever
    public float JetpackFuel { get; set; } = JetpackMaxFuel;
    // What was pushed away from, so that one body stops catching the drifter until they're clear
    // of it - and everything else still does (World.Eva.cs).
    public PushOffOrigin PushedOffFrom { get; set; } = PushOffOrigin.None;
    public string? PushedOffAsteroidId { get; set; }
    // Off by default (game_design.md) - touching the hull/a rock with these off just bounces you
    // back rather than grabbing on (World.Eva.cs's TryAutoAttach); F toggles them while outside
    // and nothing's close enough to pick up instead (World.Mining.cs's HandleEvaInteract).
    public bool MagneticBootsOn { get; set; }
    // A separate "just bounced off this one" immunity, deliberately not the same field as
    // PushedOffFrom above: that one exists so a deliberate push-off isn't immediately undone by
    // re-attaching, and should still block re-attaching even once boots are back on. This one
    // only stops the *bounce* itself from re-triggering every single tick a boots-off character
    // rests against the same surface (which would otherwise flip an outward jetpack burn straight
    // back inward before it ever built up any real escape speed) - it must NOT also block a
    // flick of the boots back on from grabbing on right where they're already touching.
    public PushOffOrigin BouncedOffFrom { get; set; } = PushOffOrigin.None;
    public string? BouncedOffAsteroidId { get; set; }

    public Character(int playerId, Vec2 position, string roomId)
    {
        PlayerId = playerId;
        Position = position;
        RoomId = roomId;
    }
}
