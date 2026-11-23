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

    // Called from InitializeShipState (constructor, loading a save, building a compartment, a
    // section breaking off). W4 of wiring-plan.md: the wiring is no longer thrown away and
    // regenerated each time - it's reconciled against the hull as it now stands:
    //  - backbone components (reactor, battery, distribution, junction boxes, devices) that still
    //    exist in the same place are kept, with every wire on them - including the player's own;
    //  - ones that are gone (or whose id now names something somewhere else - ids are positional)
    //    are dropped along with their wires;
    //  - anything new gets the standard wiring (WireGraphFactory) - a brand-new hull is simply the
    //    case where everything is new;
    //  - an installed logic part survives as long as its mount does (InitializeComponentMounts).
    // Damage stays on the wires that keep existing.
    private void InitializeWiring()
    {
        var (generatedComponents, generatedWires) = WireGraphFactory.CreateDefaultForHull(Ship);
        var generated = generatedComponents.ToDictionary(c => c.Id);
        var mountIds = Ship.ComponentMounts.Select(m => m.Id).ToHashSet();

        bool StillStands(Component c) => ComponentDefinitions.IsShipFixture(c.Kind)
            ? generated.TryGetValue(c.Id, out var g) && g.Kind == c.Kind && g.RoomId == c.RoomId
                && Math.Abs(g.X - c.X) < 0.01f && Math.Abs(g.Y - c.Y) < 0.01f
            : c.Id.EndsWith(InstalledSuffix) && mountIds.Contains(c.Id[..^InstalledSuffix.Length]);

        var kept = _components.Where(StillStands).ToList();
        var keptIds = kept.Select(c => c.Id).ToHashSet();
        var added = generatedComponents.Where(c => !keptIds.Contains(c.Id)).ToList();
        var allIds = keptIds.Concat(added.Select(c => c.Id)).ToHashSet();
        var addedIds = added.Select(c => c.Id).ToHashSet();

        var wires = _wires.Where(w => keptIds.Contains(w.FromPin.ComponentId) && keptIds.Contains(w.ToPin.ComponentId)).ToList();
        var wireIds = wires.Select(w => w.Id).ToHashSet();
        foreach (var wire in generatedWires)
        {
            var touchesNew = addedIds.Contains(wire.FromPin.ComponentId) || addedIds.Contains(wire.ToPin.ComponentId);
            if (touchesNew && allIds.Contains(wire.FromPin.ComponentId) && allIds.Contains(wire.ToPin.ComponentId) && wireIds.Add(wire.Id))
                wires.Add(wire);
        }

        var freshlyWired = _wires.Count == 0; // nothing was wired before: a new ship (or a save from before wiring was saved)
        _components = kept.Concat(added).ToList();
        _wires = wires;
        if (freshlyWired && Ship.CustomWires.Count > 0)
            ApplyEditorWires();
        var damaged = _wireDamaged.Where(kv => kv.Value).Select(kv => kv.Key).ToHashSet();
        _wireDamaged.Clear();
        foreach (var wire in _wires)
            _wireDamaged[wire.Id] = damaged.Contains(wire.Id);
        _nextWireId = Math.Max(_nextWireId, _wires
            .Select(w => w.Id.StartsWith("wire-") && int.TryParse(w.Id[5..], out var n) ? n + 1 : 0)
            .DefaultIfEmpty(0).Max());
    }

    // How close a character has to be to work a component's pins. The reactor is a big block, so
    // its reach is measured from its edge rather than its centre - the same rule the client already
    // uses to open its panels (Game1.Interactables.cs's ReactorRectIfNear: radius + half its drawn
    // size), which the server's own rod/lever/start checks now share too (W5 - they used to accept
    // only the bare radius from the centre, so a click from the reactor's edge could silently fail).
    private float ReactorReach => InteractionConstants.ReactorReach(Ship.ReactorBlock.SizeScale);

    // Repairing a broken block is done from the same spot its panel is used from.
    private float BlockReach(DeviceKind kind) => kind == DeviceKind.Reactor ? ReactorReach : InteractionRadius;

    private float WiringReach(Component component) =>
        component.Kind == ComponentKind.Reactor ? ReactorReach : InteractionRadius + 0.5f;

    private const string InstalledSuffix = "-installed"; // World.ComponentMounts.cs's id for an installed part

    // The ship's wiring as a save carries it (W4) - restored by LoadWiring before InitializeShipState
    // reconciles it against the loaded hull.
    private ShipWiringSave CreateWiringSave() => new(
        _components.ToArray(),
        _wires.ToArray(),
        _wireDamaged.Where(kv => kv.Value).Select(kv => kv.Key).ToArray());

    private void LoadWiring(ShipWiringSave? saved)
    {
        _components = saved?.Components.ToList() ?? new List<Component>();
        _wires = saved?.Wires.ToList() ?? new List<Wire>();
        _wireDamaged.Clear();
        var damaged = saved?.DamagedWireIds.ToHashSet() ?? new HashSet<string>();
        foreach (var wire in _wires)
            _wireDamaged[wire.Id] = damaged.Contains(wire.Id);
    }

    // A device (by its ShipSystemDevice.Id, reused directly as its Component.Id) only actually
    // receives power if its own input pin traces back to Distribution through an unbroken chain of
    // wires. Public - the client's HUD indicator (ShipSystemState) and tests both ask this directly.
    public bool IsDeviceConnected(string deviceId) => IsPinPowered(new PinRef(deviceId, "in"));

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
    //
    // W4: the chain now runs all the way back to a source - the reactor's output, or the battery's
    // (a ship whose reactor feed is cut still has its battery, as in Barotrauma) - with the
    // distribution block passing its input through to its outputs the way a junction does. Player
    // wiring can close loops now, hence the visited set.
    //
    // W5: power wires are undirected, as in Barotrauma (a player can wire a junction's output into
    // another's output and it simply joins the two grids), so this is a plain search outward from
    // the sources over intact power wires, with junction boxes and the distribution block joining
    // all of their own pins together.
    private bool IsPinPowered(PinRef pin)
    {
        var sources = _components
            .Where(c => c.Kind is ComponentKind.Reactor or ComponentKind.Battery)
            .Select(c => new PinRef(c.Id, c.Kind == ComponentKind.Reactor ? ComponentDefinitions.ReactorOutPin().Id : ComponentDefinitions.BatteryOutPin().Id));
        var reached = new HashSet<PinRef>();
        var frontier = new Queue<PinRef>();
        foreach (var source in sources)
            if (reached.Add(source))
                frontier.Enqueue(source);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == pin)
                return true;

            var neighbours = new List<PinRef>();
            foreach (var wire in _wires)
            {
                if (_wireDamaged.GetValueOrDefault(wire.Id))
                    continue;
                if (wire.FromPin == current)
                    neighbours.Add(wire.ToPin);
                else if (wire.ToPin == current)
                    neighbours.Add(wire.FromPin);
            }

            // A box conducts between everything wired to it: every other pin of it that has a wire.
            if (_components.FirstOrDefault(c => c.Id == current.ComponentId)?.Kind is ComponentKind.Junction or ComponentKind.Distribution)
                foreach (var wire in _wires)
                {
                    if (wire.FromPin.ComponentId == current.ComponentId)
                        neighbours.Add(wire.FromPin);
                    if (wire.ToPin.ComponentId == current.ComponentId)
                        neighbours.Add(wire.ToPin);
                }

            foreach (var next in neighbours)
                if (GetPinKind(next) is { } kind && IsPower(kind) && reached.Add(next))
                    frontier.Enqueue(next);
        }
        return false;
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

        // W6: a device's signal pins come first - they sit alongside its power pins.
        if (ComponentDefinitions.DeviceSignalPins(owner.Kind).FirstOrDefault(p => p.Id == pin.PinId) is { } signalPin)
            return signalPin.Kind;

        return owner.Kind switch
        {
            ComponentKind.Distribution => pin.PinId == ComponentDefinitions.DistributionInPin().Id ? PinKind.PowerIn : PinKind.PowerOut,
            ComponentKind.Reactor => PinKind.PowerOut,
            ComponentKind.Battery => pin.PinId == ComponentDefinitions.BatteryInPin().Id ? PinKind.PowerIn : PinKind.PowerOut,
            ComponentKind.Junction => pin.PinId == ComponentDefinitions.JunctionInPin().Id ? PinKind.PowerIn : PinKind.PowerOut,
            ComponentKind.Device => PinKind.PowerIn, // its only pin, "in"
            _ => ComponentDefinitions.PinsFor(owner.Kind).FirstOrDefault(p => p.Id == pin.PinId)?.Kind,
        };
    }

    private static bool IsPower(PinKind kind) => kind is PinKind.PowerIn or PinKind.PowerOut;
    private static bool IsOutputPin(PinKind kind) => kind is PinKind.PowerOut or PinKind.SignalOut;

    // W5 of wiring-plan.md - Barotrauma's rules: any pin takes up to WireRules.MaxWiresPerPin wires
    // (WireRules.CanConnect), at both ends. Power pins join with power pins in any direction - a
    // grid is just whatever is wired together, so output-to-output simply merges two grids; signal
    // still has a direction, an output into an input. A second wire alongside an existing one is allowed - a backup if one gets cut.
    // Normalizes which end is which regardless of which pin the player clicked first.
    private bool CanConnect(PinRef a, PinRef b, out PinRef outputPin, out PinRef inputPin)
    {
        outputPin = default;
        inputPin = default;
        if (GetPinKind(a) is not { } kindA || GetPinKind(b) is not { } kindB)
            return false;
        if (IsPower(kindA) != IsPower(kindB))
            return false;
        if (!IsPower(kindA) && IsOutputPin(kindA) == IsOutputPin(kindB))
            return false;
        if (a.ComponentId == b.ComponentId && IsPower(kindA))
            return false; // a box already joins its own pins

        (outputPin, inputPin) = IsOutputPin(kindA) || !IsOutputPin(kindB) ? (a, b) : (b, a);
        return WireRules.CanConnect(_wires, a, b);
    }

    // W5: taking a wire off with a screwdriver (the connection panel) - the whole wire comes away
    // and goes back into the hand as a spool, Barotrauma-style, whatever it was wired to: a
    // backbone wire is just as removable as one the player laid. Needs the screwdriver and to be
    // standing at either end of it.
    private void HandleRemoveWire(Character character, string wireId)
    {
        if (!character.Inventory.IsHolding(ItemType.Screwdriver))
            return;
        var wire = _wires.FirstOrDefault(w => w.Id == wireId);
        if (wire is null)
            return;
        bool Near(PinRef pin) => _components.FirstOrDefault(c => c.Id == pin.ComponentId) is { } owner
            && (owner.Position - character.Position).Length() < WiringReach(owner);
        if (!Near(wire.FromPin) && !Near(wire.ToPin))
            return;

        ElectrocuteIfLive(character, wire); // W9: a wire with current in it bites
        _wires.Remove(wire);
        _wireDamaged.Remove(wire.Id);
        if (!character.Inventory.TryAdd(ItemType.WireSpool))
            _droppedItems.Add(new DroppedItem($"drop-{_nextDroppedItemId++}", ItemType.WireSpool,
                character.Position.X, character.Position.Y, character.RoomId));
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
        if (owner is null || (owner.Position - character.Position).Length() >= WiringReach(owner))
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
        _components.Where(c => !ComponentDefinitions.IsShipFixture(c.Kind))
            .Select(c => new ComponentState(c.Id, _signalOutput.GetValueOrDefault(c.Id)))
            .ToArray();
}
