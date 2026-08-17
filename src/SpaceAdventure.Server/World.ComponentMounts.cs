using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Purchasable logic/sensor/actuator parts (World.ComponentLogic.cs runs what they do once
// installed) - bought from a station's Trader like any other TradeGood, then plugged into one of
// the hull's fixed ComponentMount sockets. Mirrors ToggleReactorSlot's "if loaded, take back out;
// else if holding the right item, insert" shape almost exactly.
public sealed partial class World
{
    private Dictionary<string, string?> _mountedComponent = new();

    // Called from InitializeShipState (constructor + every ship purchase) - a bought hull starts
    // with every socket empty, same reset already applied to wiring/turrets/doors.
    private void InitializeComponentMounts()
    {
        _mountedComponent = Ship.ComponentMounts.ToDictionary(m => m.Id, _ => (string?)null);
    }

    private ComponentKind? HeldComponentKind(Character character) =>
        character.Inventory.HeldSlotIndices
            .Select(i => character.Inventory.MainSlots[i])
            .Select(item => item is { } t ? ComponentDefinitions.ComponentKindFor(t) : null)
            .FirstOrDefault(k => k is not null);

    private void RemoveComponentAndItsWires(string componentId)
    {
        _components.RemoveAll(c => c.Id == componentId);
        _wires.RemoveAll(w => w.FromPin.ComponentId == componentId || w.ToPin.ComponentId == componentId);
        _signalOutput.Remove(componentId);
    }

    // Three mutually exclusive rules at one physical mount: a wrench on an occupied mount takes the
    // part back out; the matching item held in hand at an empty mount installs it; empty hands at an
    // occupied Relay mount operates it like a button. A WireSpool held in hand makes the mount inert
    // either way, so mid-wiring-run clicks never get misread as install/uninstall/operate. The
    // screwdriver deliberately does NOT uninstall (unlike a damaged turret/system repair, where
    // either tool works) - it's reserved client-side for ConnectionsPanel's read-only "open the
    // panel" view, so wrench = touch the hardware and screwdriver = look at the wiring, same split
    // Barotrauma uses.
    private void HandleComponentMountInteract(Character character, string mountId)
    {
        if (!_mountedComponent.TryGetValue(mountId, out var installedId))
            return;
        if (character.Inventory.IsHolding(ItemType.WireSpool))
            return;

        var mount = Ship.ComponentMounts.First(m => m.Id == mountId);

        if (installedId is { } id)
        {
            var installed = _components.FirstOrDefault(c => c.Id == id);
            if (installed is null)
                return;

            if (character.Inventory.IsHolding(ItemType.Wrench))
            {
                if (ComponentDefinitions.ItemTypeFor(installed.Kind) is { } itemType && character.Inventory.TryAdd(itemType))
                {
                    RemoveComponentAndItsWires(id);
                    _mountedComponent[mountId] = null;
                }
                return;
            }

            if (installed.Kind == ComponentKind.Relay)
                ToggleRelay(id);
            return;
        }

        if (HeldComponentKind(character) is not { } kind)
            return;
        if (ComponentDefinitions.ItemTypeFor(kind) is not { } heldItemType || !character.Inventory.TryTakeHeldItem(heldItemType))
            return;

        var newComponentId = $"{mountId}-installed";
        var targetId = kind == ComponentKind.AutoDoorController ? mount.TargetDoorId : null;
        _components.Add(new Component(newComponentId, kind, mount.RoomId, mount.X, mount.Y, targetId));
        _mountedComponent[mountId] = newComponentId;
    }

    private IReadOnlyList<ComponentMountState> CreateComponentMountStates() =>
        Ship.ComponentMounts.Select(m => new ComponentMountState(m.Id, _mountedComponent.GetValueOrDefault(m.Id))).ToArray();
}
