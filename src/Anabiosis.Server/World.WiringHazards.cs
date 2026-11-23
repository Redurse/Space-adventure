using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// W9 of wiring-plan.md - what the wiring can do to the ship and to the crew, the way Barotrauma's does:
//
//  * A junction box that is asked for more than it can carry heats up. Keep it up for JunctionOverheatSeconds and the box burns out - its
//    trunk wire goes (IsJunctionDamaged), everything on it loses power until somebody repairs it. Cooling takes as long as heating.
//    "Asked for" is what the devices wired straight to the box draw (a box with one device on it never comes near the limit; hang three
//    engines on one and it will).
//  * Taking a wire off that is live (the screwdriver in the connection panel, HandleRemoveWire) shocks the one doing it: a burn on the hand
//    and a short stun. A dead wire - nothing behind it, or already burnt through - is safe.
//  * Hits on the hull already cut wires (World.EnemyAi.cs) and repair already mends them (RepairDeviceWiring); nothing changes there.
public sealed partial class World
{
    // What one junction box carries: two engines' worth (EngineDemand * 2) and no more.
    public const float JunctionCapacity = 40f;
    public const float JunctionOverheatSeconds = 4f;
    // A junction box in a room with less air than this (a breach has emptied it) heats up like an overloaded one.
    public const float VacuumShortOxygen = 5f;
    // A box this hot (share of the way to burning out) shows sparks on the client.
    public const float JunctionWarningHeat = 0.25f;

    public const float ShockBurn = 6f;
    public const float ShockStunSeconds = 1.2f;

    private readonly Dictionary<string, float> _junctionHeat = new();

    private void StepWiringHazards(double deltaSeconds)
    {
        var dt = (float)deltaSeconds;
        foreach (var junction in _components.Where(c => c.Kind == ComponentKind.Junction))
        {
            var load = JunctionLoad(junction.Id);
            var heat = _junctionHeat.GetValueOrDefault(junction.Id);
            // Too much on it, or no air round it: a box in a room open to space shorts out the way Barotrauma's does in water.
            var shorting = load > JunctionCapacity || (_roomOxygen.TryGetValue(junction.RoomId, out var air) && air < VacuumShortOxygen);
            heat = shorting ? heat + dt : Math.Max(0f, heat - dt);

            if (heat >= JunctionOverheatSeconds)
            {
                BurnOutJunction(junction.Id);
                heat = 0f;
            }

            if (heat > 0f)
                _junctionHeat[junction.Id] = heat;
            else
                _junctionHeat.Remove(junction.Id);
        }
    }

    // The draw of every device wired directly to this box through an intact power wire.
    private float JunctionLoad(string junctionId)
    {
        var load = 0f;
        foreach (var wire in _wires)
        {
            if (_wireDamaged.GetValueOrDefault(wire.Id))
                continue;
            var other = wire.FromPin.ComponentId == junctionId ? wire.ToPin.ComponentId
                : wire.ToPin.ComponentId == junctionId ? wire.FromPin.ComponentId : null;
            if (other is not null && _deviceDemand.TryGetValue(other, out var demand))
                load += demand;
        }
        return load;
    }

    // The box's own "damage" is its trunk wire being cut (see IsJunctionDamaged), so burning out is exactly that.
    private void BurnOutJunction(string junctionId)
    {
        if (_wires.FirstOrDefault(w => w.ToPin.ComponentId == junctionId) is { } trunk)
            _wireDamaged[trunk.Id] = true;
    }

    // W10 of wiring-plan.md - the wires the ship's author drew in the editor (Ship.CustomWires) replace the standard wiring at the pins they
    // reach: a machine wired by hand loses its automatic feed, one the author left alone keeps it. Done once, on a freshly wired ship; a
    // saved game or an in-game rebuild keeps whatever wires it has.
    private void ApplyEditorWires()
    {
        var authored = new HashSet<string>();
        foreach (var def in Ship.CustomWires)
        {
            if (ResolveEditorPin(def.FromX, def.FromY, def.FromPin) is not { } from
                || ResolveEditorPin(def.ToX, def.ToY, def.ToPin) is not { } to || from == to)
                continue;

            // The author's wire takes the place of the standard one at its input pin.
            _wires.RemoveAll(w => !authored.Contains(w.Id) && (w.FromPin == to || w.ToPin == to));
            if (!WireRules.CanConnect(_wires, from, to))
                continue;
            var id = $"wire-{_nextWireId++}";
            _wires.Add(new Wire(id, from, to));
            authored.Add(id);
        }
    }

    // The component standing at these editor coordinates that really has this pin (a "out-*" asks for any free distribution output).
    private PinRef? ResolveEditorPin(float x, float y, string pin)
    {
        const float Reach = 0.75f;
        var candidates = _components
            .Where(c => (c.Position - new Vec2(x, y)).Length() <= Reach)
            .OrderBy(c => (c.Position - new Vec2(x, y)).Length());
        foreach (var component in candidates)
        {
            if (pin != EditorWiring.AnyDistributionOutput)
            {
                var exact = new PinRef(component.Id, pin);
                if (GetPinKind(exact) is not null)
                    return exact;
                continue;
            }

            foreach (var system in Enum.GetValues<PowerSystemId>())
            {
                var output = new PinRef(component.Id, $"out-{system}".ToLowerInvariant());
                if (GetPinKind(output) is PinKind.PowerOut && WireRules.CanConnect(_wires, output, new PinRef("none", "in")))
                    return output;
            }
        }
        return null;
    }

    // Is there current in this wire right now? A power wire whose either end is on the live grid and which is not itself cut.
    private bool IsWireLive(Wire wire) =>
        !_wireDamaged.GetValueOrDefault(wire.Id)
        && GetPinKind(wire.FromPin) is { } fromKind && IsPower(fromKind)
        && GetPinKind(wire.ToPin) is { } toKind && IsPower(toKind)
        && (IsPinPowered(wire.FromPin) || IsPinPowered(wire.ToPin));

    private void ElectrocuteIfLive(Character character, Wire wire)
    {
        if (!IsWireLive(wire))
            return;
        DamageCharacter(character, DamageKind.Burn, ShockBurn, BodyPart.RightArm);
        character.Body.StunSeconds = Math.Max(character.Body.StunSeconds, ShockStunSeconds);
    }

    // For the snapshot: boxes that are hot enough to show it.
    private IReadOnlyList<string> CreateOverheatingJunctions() =>
        _junctionHeat.Where(kv => kv.Value / JunctionOverheatSeconds >= JunctionWarningHeat).Select(kv => kv.Key).ToArray();

    // Test-only: the air in a room, as if a breach had emptied it (the generator refills it unless it is set again each tick).
    public void DebugSetRoomOxygen(string roomId, float oxygen) => _roomOxygen[roomId] = oxygen;

    // Test-only: the room a junction box stands in.
    public string DebugJunctionRoom(string junctionId) => _components.First(c => c.Id == junctionId).RoomId;

    // Test-only: no junction box has burnt out.
    public bool DebugAllJunctionsIntact() =>
        _components.Where(c => c.Kind == ComponentKind.Junction).All(c => !IsJunctionDamaged(c.Id));

    // Test-only: takes a wire away without a screwdriver or a spool.
    public void DebugRemoveWire(string wireId)
    {
        _wires.RemoveAll(w => w.Id == wireId);
        _wireDamaged.Remove(wireId);
    }

    // Test-only: how far a box is on its way to burning out, 0..1.
    public float DebugJunctionHeat(string junctionId) => _junctionHeat.GetValueOrDefault(junctionId) / JunctionOverheatSeconds;
}
