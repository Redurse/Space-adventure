using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Shared.Model;

public sealed record CharacterState(
    int PlayerId,
    float X,
    float Y,
    bool CarryingAmmoCrate = false,
    float Health = 100f,
    bool WearingSuit = false,
    float SuitActionRemaining = 0f,
    float FacingX = -1f,
    float FacingY = 0f,
    InventoryState? Inventory = null,
    bool IsBleeding = false,
    bool IsAtHelm = false,
    bool IsOutside = false,
    float JetpackFuel = 100f,
    bool IsEvaAttached = false,
    bool OnStation = false,
    bool OnEnemyShip = false,
    // Oxygen left in the tank socketed into the worn suit and into the held cutter, null for "no
    // tank in it". Both are read straight off the inventory, but they're the two the HUD has to
    // show constantly - a suit running dry in vacuum is the thing you must not have to go looking
    // for (OxygenTankDefinitions).
    float? SuitTank = null,
    float? CutterTank = null,
    // The cutting flame is lit this tick: what the client draws, and what other players see.
    bool Cutting = false,
    // Hired crew (World.Recruiting.cs) - null Role means an ordinary player. The client draws a bot
    // with its given name and role instead of another anonymous crew member.
    bool IsBot = false,
    string? BotName = null,
    CrewRole? Role = null,
    // Same story as SuitTank/CutterTank above, for the tank socketed into a held welding tool
    // (WeldingTankDefinitions). Welding is lit this tick: what the client draws (a yellow-orange
    // flame, distinct from the cutter's blue one) and what other players see.
    float? WelderTank = null,
    bool Welding = false,
    // Which pin a wire-lay is anchored at, null when not laying (World.Wiring.cs's
    // HandlePinInteract) - lets every client, not just this one, draw the trailing wire from that
    // pin to wherever this character currently stands.
    PinRef? LayingWireFromPin = null,
    // Typed once at the menu and remembered client-side (PlayerSettingsStore), null for a hired
    // bot (BotName is its name instead). Shown as an always-on nameplate (ShipRenderer/
    // FieldRenderer's DrawCharacter) and in the crew roster (CrewPanel/InfoPanel).
    string? Nickname = null,
    // Round-trip time in ms, measured server-side off the client's own echoed timestamp
    // (World.cs's ApplyCommand, ClientCommand.LastServerTimestampMs) - needs no clock sync between
    // machines since both ends of the measurement are the server's own clock.
    float PingMs = 0f,
    // Which wall block this character's welder/cutter is currently lit and aimed at, null when
    // neither tool is lit or nothing is in reach (World.WallBlocks.cs's GetWallToolTargetId) - lets
    // every client, not just this one, show that block's own health bar while it's being worked.
    string? WallToolTargetBlockId = null,
    // Bend points fixed so far along an in-progress wire lay (World.Wiring.cs's HandleWireBend) -
    // null/empty when not laying or none fixed yet. Lets every client, not just this one, draw the
    // trailing wire as the same bent path the layer is actually shaping, not just a straight line
    // to wherever they currently stand.
    IReadOnlyList<Vec2>? LayingWireBends = null,
    // Which door this character's cutter is currently lit and aimed at, null when the cutter isn't
    // lit or no door is in reach (World.Doors.cs's GetDoorToolTargetId) - same "quiet number, shown
    // only while it's actually being worked" shape as WallToolTargetBlockId above, just for cutting
    // a door open instead of cutting into the hull.
    string? DoorToolTargetId = null,
    // Off by default - whether touching the hull/a rock while outside actually grabs on
    // (World.Eva.cs's TryAutoAttach) or just bounces the character back. F toggles it whenever
    // there's nothing closer to pick up instead (World.Mining.cs's HandleEvaInteract).
    bool MagneticBootsOn = false);
