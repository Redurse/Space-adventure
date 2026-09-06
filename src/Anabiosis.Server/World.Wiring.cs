using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Replaces the old fixed WireNetwork/WireLink topology with a generic Component/Wire graph
// (Shared/Model). The built-in power backbone (Reactor -> Distribution -> Junction -> Device) is
// generated once per hull by WireGraphFactory and is not player-editable - only its wires can be
// damaged and repaired, matching today's actual gameplay contract (game_design.md section 1: "на
// старте корабль уже полностью разведён проводкой").
//
// Player-authored wiring (M20): walk to one pin holding a WireSpool, walk to a second, compatible
// one - the same two-point-interaction shape as everything else physical in this game, just
// server-checked for proximity both ends (a real in-world action, not a trusted panel click the
// old WireLinkInteractId was). M21+ will give Signal pins something new to connect to; this file
// already treats Power and Signal uniformly via PinKind, so nothing here needs to change when they
// arrive.
public sealed partial class World
{
    private List<Component> _components = new();
    private List<Wire> _wires = new();
    private readonly Dictionary<string, bool> _wireDamaged = new();
    private int _nextWireId;

    public IReadOnlyList<Component> Components => _components;
    public IReadOnlyList<Wire> Wires => _wires;

    // Called once from InitializeShipState (constructor + every ship purchase) - a bought hull
    // starts with its own fresh, undamaged backbone, same reset already applied to turret runtimes
    // and door state.
    private void InitializeWiring()
    {
        var (components, wires) = WireGraphFactory.CreateDefaultForHull(Ship);
        _components = components.ToList();
        _wires = wires.ToList();
        _wireDamaged.Clear();
        foreach (var wire in _wires)
            _wireDamaged[wire.Id] = false;
    }

    // A device (by its ShipSystemDevice.Id, reused directly as its Component.Id) only actually
    // receives power if its own input pin traces back to Distribution through an unbroken chain of
    // wires. Public - the client's HUD indicator (ShipSystemState) and tests both ask this directly.
    public bool IsDeviceConnected(string deviceId) => IsPinPowered(new PinRef(deviceId, "in"));

    // What the rest of the simulation should use instead of PowerGrid.GetAllocation directly - folds
    // in wiring connectivity. A system with more than one device (Shields/Engine) scales its
    // effective power by the fraction of its devices actually connected - each device now has its
    // own dedicated junction+trunk (WireGraphFactory), so this is a plain per-device tally rather
    // than a single shared "is the whole system's one trunk still up" gate.
    public float GetEffectivePower(PowerSystemId system)
    {
        var devices = Ship.SystemDevices.Where(d => d.System == system).ToList();
        if (devices.Count == 0)
            return 0f; // this hull has no device on this system at all

        var connected = devices.Count(d => IsPinPowered(new PinRef(d.Id, "in")));
        return PowerGrid.GetAllocation(system) * connected / devices.Count;
    }

    // Recursive rather than iterative: the Power sub-graph is a fixed tree by construction (no
    // player-authored Power wire exists yet - only the built-in backbone), so it terminates
    // trivially with no visited-set needed. Distribution's own pins are the unconditional base case -
    // how much power a system actually has to carry is PowerGrid's call, not wiring's; this only
    // answers "is there an unbroken path all the way back."
    //
    // A Junction's OUTPUT pins aren't fed by any wire of their own (nothing has a Junction output as
    // its ToPin - a Junction is the source for its own drops, not a sink) - a junction just passes
    // its single input straight through to every output, so asking about one of its outputs really
    // means asking about its one input instead. Distribution and Device only ever get asked about
    // pins that DO have a feeding wire (Distribution has no inputs, Device has no outputs), so this
    // is the only pass-through case that needs special-casing.
    //
    // Deliberately not the same algorithm the Signal logic graph will use (M21's bounded-pass
    // relaxation) - a plain reachability question and a settling boolean circuit with possible
    // feedback are different enough problems that forcing them through one solver would only make
    // both worse.
    private bool IsPinPowered(PinRef pin)
    {
        if (pin.ComponentId == "distribution")
            return true;

        var owner = _components.FirstOrDefault(c => c.Id == pin.ComponentId);
        if (owner?.Kind == ComponentKind.Junction && pin.PinId != ComponentDefinitions.JunctionInPin().Id)
            return IsPinPowered(new PinRef(pin.ComponentId, ComponentDefinitions.JunctionInPin().Id));

        return _wires.Where(w => w.ToPin == pin).Any(w => !_wireDamaged[w.Id] && IsPinPowered(w.FromPin));
    }

    // A Junction's own "damage" is its trunk wire (Distribution->Junction) being cut - the same
    // random wire-damage roll (World.EnemyAi.cs's CutWire) that already can and does target it today,
    // it just had no name of its own before now. RepairDeviceWiring already handles a Junction's id
    // correctly with zero changes: its "drop wire" lookup finds the trunk itself, since the trunk's
    // ToPin IS the Junction's own input pin.
    public bool IsJunctionDamaged(string junctionId) =>
        _wires.FirstOrDefault(w => w.ToPin.ComponentId == junctionId) is { } trunk && _wireDamaged[trunk.Id];

    // Junction boxes now get their own client-visible repair state (World.Interact.cs's E-key
    // pickup/repair, World.SystemRepair.cs's minigame) - reusing ShipSystemState's exact shape rather
    // than inventing a parallel record, since "device id / system / damaged / repair progress" is
    // exactly what a Junction needs too. System is recovered from whichever device this junction's
    // own drop wire actually feeds, rather than parsed from the id string - a single-device system's
    // junction is still named "junction-oxygen" etc., but a multi-device one is now named after its
    // own device ("junction-system-engine-2"), which a PowerSystemId.Parse could never round-trip.
    private IReadOnlyList<ShipSystemState> CreateJunctionStates() =>
        _components.Where(c => c.Kind == ComponentKind.Junction).Select(c =>
        {
            var deviceId = _wires.FirstOrDefault(w => w.FromPin.ComponentId == c.Id)?.ToPin.ComponentId;
            // A camera's own junction isn't in Ship.SystemDevices at all (WireGraphFactory's own
            // comment on ConnectDeviceNode) - it still feeds off the Secondary trunk, so falling
            // through to PowerSystemId's default (Oxygen) would mislabel it in the Connections panel.
            var system = Ship.SystemDevices.FirstOrDefault(d => d.Id == deviceId)?.System
                ?? (Ship.Cameras.Any(cam => cam.Id == deviceId) ? PowerSystemId.Secondary : default);
            return new ShipSystemState(c.Id, system, IsJunctionDamaged(c.Id), GetSystemRepairDisplay(c.Id));
        }).ToArray();

    // Damages a specific wire (the enemy AI's system-damage roll, World.EnemyAi.cs, and tests use
    // this directly - the equivalent of the old CutWireLink). If the cut wire happened to be a
    // reinforcing second wire into an already-covered input, IsPinPowered above still finds the
    // other, still-intact one on its own - no separate "which half is currently live" bookkeeping
    // is needed the way the old PrimaryDamaged/HasBackup/BackupDamaged tri-state required.
    public void CutWire(string wireId)
    {
        if (_wireDamaged.ContainsKey(wireId))
            _wireDamaged[wireId] = true;
    }

    // Creates a wire directly - the primitive the real player-facing "walk to one pin, then the
    // other" flow (ClientCommand.PinInteractId, M20) will call through once it lands; used directly
    // by tests until then.
    public void AddWire(string id, PinRef fromPin, PinRef toPin)
    {
        _wires.Add(new Wire(id, fromPin, toPin));
        _wireDamaged[id] = false;
    }

    // Adds a component directly - the primitive the real purchase-and-install flow
    // (World.ComponentMounts.cs, M23) will call through once it exists; used directly by tests
    // until then, same reasoning as AddWire above.
    public void AddComponent(Component component) => _components.Add(component);

    // In-person repair at a damaged device (World.Interact.cs's E-key proximity check, and
    // World.CrewAi.cs's Mechanic bot) - unchanged behavior from before: fixes the device's own drop
    // wire first, falling back to the shared trunk wire if the drop's fine but the trunk isn't.
    private void RepairDeviceWiring(string deviceId)
    {
        var dropWire = _wires.FirstOrDefault(w => w.ToPin.ComponentId == deviceId);
        if (dropWire is null)
            return;

        if (_wireDamaged[dropWire.Id])
        {
            _wireDamaged[dropWire.Id] = false;
            return;
        }

        var junctionId = dropWire.FromPin.ComponentId;
        var trunkWire = _wires.FirstOrDefault(w => w.ToPin.ComponentId == junctionId);
        if (trunkWire is not null && _wireDamaged[trunkWire.Id])
            _wireDamaged[trunkWire.Id] = false;
    }

    // Gosha's screwdriver's whole gimmick (ItemType.GoshaScrewdriver, Game1.Input.cs's own
    // left-click branch for it): instead of fixing a device's drop wire, it cuts it - the exact
    // reverse of RepairDeviceWiring's own first branch. Only ever a SystemDevice (not a Junction/
    // door), matching what "прибор" actually names in this game; a no-op if it's already damaged.
    private void HandleSabotageDevice(Character character, string deviceId)
    {
        if (!character.Inventory.IsHolding(ItemType.GoshaScrewdriver))
            return;
        if (Ship.SystemDevices.All(d => d.Id != deviceId))
            return;

        var dropWire = _wires.FirstOrDefault(w => w.ToPin.ComponentId == deviceId);
        if (dropWire is not null)
            _wireDamaged[dropWire.Id] = true;
    }

    // What pin kind a PinRef actually names - Distribution/Junction have hull-dependent pins so
    // they're resolved structurally rather than via ComponentDefinitions.PinsFor (which only knows
    // the fixed-arity kinds, M21+'s gates/sensors/etc). Null for a pin that doesn't exist.
    private PinKind? GetPinKind(PinRef pin)
    {
        var owner = _components.FirstOrDefault(c => c.Id == pin.ComponentId);
        if (owner is null)
            return null;

        return owner.Kind switch
        {
            ComponentKind.Distribution => PinKind.PowerOut, // every pin on Distribution is an output
            ComponentKind.Junction => pin.PinId == ComponentDefinitions.JunctionInPin().Id ? PinKind.PowerIn : PinKind.PowerOut,
            ComponentKind.Device => PinKind.PowerIn, // its only pin, "in"
            _ => ComponentDefinitions.PinsFor(owner.Kind).FirstOrDefault(p => p.Id == pin.PinId)?.Kind,
        };
    }

    private const int PowerInputWireCap = 2; // the generalized "backup" mechanic - see Wire.cs
    private const int SignalInputWireCap = 1; // no combine rule for >1 source - see Wire.cs

    private static bool IsPower(PinKind kind) => kind is PinKind.PowerIn or PinKind.PowerOut;
    private static bool IsOutputPin(PinKind kind) => kind is PinKind.PowerOut or PinKind.SignalOut;

    // True only for one real output paired with one real input of the same category (both Power or
    // both Signal), with the input not already at its wire-count cap. Normalizes which end is which
    // regardless of which pin the player clicked first.
    private bool CanConnect(PinRef a, PinRef b, out PinRef outputPin, out PinRef inputPin)
    {
        outputPin = default;
        inputPin = default;
        if (GetPinKind(a) is not { } kindA || GetPinKind(b) is not { } kindB)
            return false;
        if (IsPower(kindA) != IsPower(kindB) || IsOutputPin(kindA) == IsOutputPin(kindB))
            return false;

        (outputPin, inputPin) = IsOutputPin(kindA) ? (a, b) : (b, a);
        var cap = IsPower(kindA) ? PowerInputWireCap : SignalInputWireCap;
        var input = inputPin;
        return _wires.Count(w => w.ToPin == input) < cap;
    }

    // The physical wire-lay: first click on a pin (holding a WireSpool) anchors it; a second click
    // on the same pin cancels; a second click on a different, compatible, not-yet-full pin
    // completes the wire and spends the spool; a second click on an incompatible/full pin restarts
    // the lay from there instead of leaving the player stuck (forgiving UX, no dead end). Any bend
    // points fixed along the way (HandleWireBend) are purely cosmetic and get carried onto the
    // finished Wire, or dropped on the floor the moment the lay ends any other way.
    private void HandlePinInteract(Character character, PinRef pin)
    {
        if (!character.Inventory.IsHolding(ItemType.WireSpool))
        {
            character.LayingWireFromPin = null; // no spool in hand - dropping it cancels any lay
            character.LayingWireBends.Clear();
            return;
        }

        var owner = _components.FirstOrDefault(c => c.Id == pin.ComponentId);
        if (owner is null || (owner.Position - character.Position).Length() >= InteractionRadius)
            return;

        if (character.LayingWireFromPin is not { } start)
        {
            character.LayingWireFromPin = pin;
            return;
        }

        if (start == pin)
        {
            character.LayingWireFromPin = null; // clicking the anchor again cancels
            character.LayingWireBends.Clear();
            return;
        }

        if (!CanConnect(start, pin, out var outputPin, out var inputPin))
        {
            character.LayingWireFromPin = pin; // restart from here rather than dead-ending
            character.LayingWireBends.Clear(); // the old bends shaped a path to an abandoned target
            return;
        }

        var wireId = $"wire-{_nextWireId++}";
        _wires.Add(new Wire(wireId, outputPin, inputPin, character.LayingWireBends.ToArray()));
        _wireDamaged[wireId] = false;
        character.Inventory.TryTakeHeldItem(ItemType.WireSpool);
        character.LayingWireFromPin = null;
        character.LayingWireBends.Clear();
    }

    private const int MaxWireBends = 32; // generous headroom for a routed path, bounded against a spammed command

    // A LMB click mid-lay that missed every pin fixes a bend at that spot instead (World.cs's
    // WireBendAtX/Y) - purely cosmetic routing, never touches connectivity. Silently ignored unless
    // actually mid-lay with the spool still in hand, same trust level as the pin clicks themselves.
    private void HandleWireBend(Character character, Vec2 worldPosition)
    {
        if (character.LayingWireFromPin is null || !character.Inventory.IsHolding(ItemType.WireSpool))
            return;
        if (character.LayingWireBends.Count >= MaxWireBends)
            return;
        character.LayingWireBends.Add(worldPosition);
    }

    // Right-click backs out one step at a time now: the last fixed bend if there is one, the whole
    // anchor otherwise - so undoing a routing mistake doesn't force starting the entire lay over.
    private void HandleWireLayCancel(Character character)
    {
        if (character.LayingWireBends.Count > 0)
            character.LayingWireBends.RemoveAt(character.LayingWireBends.Count - 1);
        else
            character.LayingWireFromPin = null;
    }

    private IReadOnlyList<WireState> CreateWireStates() =>
        _wires.Select(w => new WireState(w.Id, _wireDamaged[w.Id])).ToArray();

    // Power-only components (Distribution/Junction/Device) don't get one - their state is already
    // covered by ShipSystemState. Everything else (gates/timer/memory/relay, and sensors/actuators
    // from M22) has exactly one signal value worth showing: its "out" pin, or its sole input for a
    // pure sink like an actuator (World.ComponentLogic.cs's _signalOutput covers both the same way).
    private IReadOnlyList<ComponentState> CreateComponentStates() =>
        _components.Where(c => c.Kind is not (ComponentKind.Distribution or ComponentKind.Junction or ComponentKind.Device))
            .Select(c => new ComponentState(c.Id, _signalOutput.GetValueOrDefault(c.Id)))
            .ToArray();
}
