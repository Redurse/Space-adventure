using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

// Custom wire routing (game_design.md section 1, M14): "на старте корабль уже полностью разведён
// проводкой, но игрок может проложить дополнительные провода (резерв на случай повреждения
// магистрали...)". WireNetwork (Shared) is the fixed topology; this file is the runtime state on
// top of it — which links are cut, which have a player-laid backup.
//
// Simplification from the literal "click two arbitrary points to draw a wire" framing: laying a
// backup only ever reinforces one of the topology's existing required links (you can't invent a
// brand new connection between two unrelated points), and both repairing and laying a backup
// happen via a single click on that link's line in the wiring panel rather than a separate
// two-point drag - the tool/item currently held decides which action happens. This keeps the
// mechanic testable without a general graph-connectivity solver, while still delivering the two
// things the design doc actually cares about: a visual schematic, and the ability to reinforce a
// connection before (or after) it gets cut.
public sealed partial class World
{
    public WireNetwork WireNetwork { get; } = WireNetwork.CreateDefault();

    private readonly Dictionary<string, (bool PrimaryDamaged, bool HasBackup, bool BackupDamaged)> _wireLinkStates =
        WireNetwork.CreateDefault().Links.ToDictionary(l => l.Id, _ => (PrimaryDamaged: false, HasBackup: false, BackupDamaged: false));

    public bool IsLinkConnected(string linkId)
    {
        var state = _wireLinkStates[linkId];
        return !state.PrimaryDamaged || (state.HasBackup && !state.BackupDamaged);
    }

    private WireLink TrunkLinkFor(PowerSystemId system) => WireNetwork.Links.First(l => l.System == system && l.FromNodeId == "node-distribution");

    private bool IsTrunkConnected(PowerSystemId system) => IsLinkConnected(TrunkLinkFor(system).Id);

    // A device (by its ShipSystemDevice.Id) only actually receives power if both its own drop
    // link AND its system's shared trunk link are connected. Public - the client's HUD indicator
    // (ShipSystemState) and tests both need to ask this directly.
    public bool IsDeviceConnected(string deviceId)
    {
        var dropLink = WireNetwork.Links.FirstOrDefault(l => l.ToNodeId == deviceId);
        if (dropLink is null)
            return true; // no wire link defined for this device — treat as always-on (shouldn't happen for real system devices)

        return IsTrunkConnected(dropLink.System) && IsLinkConnected(dropLink.Id);
    }

    // What the rest of the simulation should actually use instead of PowerGrid.GetAllocation
    // directly — folds in wiring connectivity. Shields is the one system with two devices, so its
    // effective power scales with how many of its two drops are actually connected (game_design.md
    // section 1's "два уровня отказа": lose the whole system via the trunk, or just part of it via
    // one drop).
    public float GetEffectivePower(PowerSystemId system)
    {
        if (!IsTrunkConnected(system))
            return 0f;

        var allocation = PowerGrid.GetAllocation(system);
        var drops = WireNetwork.Links.Where(l => l.System == system && l.FromNodeId != "node-distribution").ToList();
        if (drops.Count <= 1)
            return allocation;

        var connectedFraction = (float)drops.Count(l => IsLinkConnected(l.Id)) / drops.Count;
        return allocation * connectedFraction;
    }

    // Cuts whichever half is currently live (the backup if the primary's already down and the
    // backup is what's actually carrying it, otherwise the primary) — used by both the enemy AI
    // and tests (the direct equivalent of the old PowerGrid.SetDamaged shortcut). Named distinctly
    // from "damage a device" since a link isn't a physical block.
    public void CutWireLink(string linkId)
    {
        var state = _wireLinkStates[linkId];
        if (!state.PrimaryDamaged)
            _wireLinkStates[linkId] = state with { PrimaryDamaged = true };
        else if (state.HasBackup && !state.BackupDamaged)
            _wireLinkStates[linkId] = state with { BackupDamaged = true };
    }

    // Repairing at a physical device (World.Interact.cs, wrench/screwdriver F) fixes whatever's
    // actually keeping power from reaching it: the device's own drop link first (you're standing
    // right at it), falling back to the shared trunk if the drop's fine but the trunk isn't.
    private void RepairDeviceWiring(string deviceId)
    {
        var dropLink = WireNetwork.Links.FirstOrDefault(l => l.ToNodeId == deviceId);
        if (dropLink is null)
            return;

        if (!IsLinkConnected(dropLink.Id))
        {
            RepairWireLink(dropLink.Id);
            return;
        }

        var trunkLink = TrunkLinkFor(dropLink.System);
        if (!IsLinkConnected(trunkLink.Id))
            RepairWireLink(trunkLink.Id);
    }

    // Repairs whichever half is currently damaged (primary takes priority, matching how you'd
    // naturally fix the original wiring before touching a backup).
    public void RepairWireLink(string linkId)
    {
        var state = _wireLinkStates[linkId];
        if (state.PrimaryDamaged)
            _wireLinkStates[linkId] = state with { PrimaryDamaged = false };
        else if (state.HasBackup && state.BackupDamaged)
            _wireLinkStates[linkId] = state with { BackupDamaged = false };
    }

    // One link can only ever have a single backup laid at a time - no-op if it already has one.
    public void LayBackupWire(string linkId)
    {
        var state = _wireLinkStates[linkId];
        if (!state.HasBackup)
            _wireLinkStates[linkId] = state with { HasBackup = true, BackupDamaged = false };
    }

    // Single click on a wire's line in the panel (World.cs ApplyCommand): the currently-held tool
    // decides what happens. A wrench/screwdriver repairs; a WireSpool lays a backup (consuming
    // it) if there isn't one already. Anything else held - no-op.
    private void HandleWireLinkInteract(Character character, string linkId)
    {
        if (!_wireLinkStates.ContainsKey(linkId))
            return;

        if (character.Inventory.IsHolding(ItemType.Wrench) || character.Inventory.IsHolding(ItemType.Screwdriver))
        {
            RepairWireLink(linkId);
            return;
        }

        if (character.Inventory.IsHolding(ItemType.WireSpool) && !_wireLinkStates[linkId].HasBackup)
        {
            LayBackupWire(linkId);
            character.Inventory.TryTakeHeldItem(ItemType.WireSpool);
        }
    }

    private IReadOnlyList<WireLinkState> CreateWireLinkStates() =>
        WireNetwork.Links.Select(l =>
        {
            var s = _wireLinkStates[l.Id];
            return new WireLinkState(l.Id, s.PrimaryDamaged, s.HasBackup, s.BackupDamaged);
        }).ToArray();
}
