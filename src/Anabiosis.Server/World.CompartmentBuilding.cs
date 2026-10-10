using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// In-game ship building from the compartment catalog (the Shipwright): a compartment is placed by a tile anchor and a quarter-turn
// rotation on the same tile grid, under the same rules, as in the Ship Editor (CompartmentBuilder), built over a timer like before
// (30 s, even in flight), and demolished for half its price back. Doors go on wall joints (JunctionDoorBuilder): single, double or
// triple, in a half-block wall with wall continuing past both ends.
public sealed partial class World
{
    private sealed class PendingCompartmentBuild
    {
        public required CompartmentPlan Plan;
        public required string RoomId;
        public double ElapsedSeconds;
    }

    private readonly List<PendingCompartmentBuild> _pendingCompartmentBuilds = new();

    // Tiles claimed by compartments still being built - a second one cannot be started on top of them.
    private HashSet<TileCoord> ReservedCompartmentTiles() =>
        _pendingCompartmentBuilds.SelectMany(p => p.Plan.Tiles).ToHashSet();

    public IReadOnlyList<CompartmentCatalogEntry> GetBuildableCompartments() => CompartmentCatalog.Entries;

    private void TryBuildCompartment(BuildCompartmentRequest request)
    {
        if (CompartmentCatalog.Find(request.CatalogId) is not { } entry)
            return;
        var price = CompartmentPricing.Price(entry);
        var plating = CompartmentPricing.PlatingCost(entry);
        if (Credits < price || _hullPlatingStock < plating)
            return;

        var rotation = ((request.Rotation % 4) + 4) % 4;
        var (plan, _) = CompartmentBuilder.Plan(Ship.Tiles, ReservedCompartmentTiles(), entry, new TileCoord(request.X, request.Y), rotation);
        if (plan is null)
            return;
        var def = Ship.ToDefinition();
        var roomId = CompartmentBuilder.NextRoomId(def.Rooms.Concat(_pendingCompartmentBuilds.Select(p => new CustomRoomDef(p.RoomId, "", 0, 0, 1, 1))).ToList());
        if (CompartmentBuilder.Merge(def, plan, roomId) is null)
            return; // the result would not be a valid ship

        Credits -= price;
        _hullPlatingStock -= plating;
        _pendingCompartmentBuilds.Add(new PendingCompartmentBuild { Plan = plan, RoomId = roomId });
    }

    private void StepCompartmentBuilds(double deltaSeconds)
    {
        for (var i = _pendingCompartmentBuilds.Count - 1; i >= 0; i--)
        {
            var pending = _pendingCompartmentBuilds[i];
            pending.ElapsedSeconds += deltaSeconds;
            if (pending.ElapsedSeconds < RoomBuildDurationSeconds)
                continue;

            _pendingCompartmentBuilds.RemoveAt(i);
            FinishCompartmentBuild(pending);
        }
    }

    // Re-checked against the hull as it is NOW (a fight can change it under a build in progress); a build that no longer fits is
    // dropped without a refund, the same deliberate consequence as before.
    private void FinishCompartmentBuild(PendingCompartmentBuild pending)
    {
        var plan = pending.Plan;
        var (fresh, _) = CompartmentBuilder.Plan(Ship.Tiles, ReservedCompartmentTiles(), plan.Entry, plan.Anchor, plan.Rotation);
        if (fresh is null)
            return;
        if (CompartmentBuilder.Merge(Ship.ToDefinition(), fresh, pending.RoomId) is not { } merged)
            return;
        ApplyShipDefinition(merged);
    }

    private IReadOnlyList<PendingRoomBuildState> CreatePendingCompartmentStates() =>
        _pendingCompartmentBuilds.Select(p => new PendingRoomBuildState(p.RoomId, p.Plan.Entry.DisplayName,
            p.Plan.Bounds.X, p.Plan.Bounds.Y, p.Plan.Bounds.Width, p.Plan.Bounds.Height,
            (float)Math.Min(1.0, p.ElapsedSeconds / RoomBuildDurationSeconds))).ToArray();

    // Test-only: finishes the compartments being built without waiting out their timers.
    public void DebugFastForwardCompartmentBuilds(double seconds)
    {
        foreach (var pending in _pendingCompartmentBuilds)
            pending.ElapsedSeconds += seconds;
    }

    // ---- demolishing ----

    // Taking a compartment off the hull for half its price back. Docked at a station with a Shipwright only (a deliberate, assisted
    // teardown). It must leave a ship that is still a ship (the validator: the only reactor, helm, airlock... cannot go) and must
    // not cut the hull in two - splitting off a whole part is what combat does, not what the shipwright will do for you.
    private void TryDemolishRoom(string roomId)
    {
        if (!IsDocked || Station.Npcs.All(n => n.Kind != NpcKind.Shipwright))
            return;
        if (Ship.Rooms.FirstOrDefault(r => r.Id == roomId) is not { } room)
            return;
        if (!ShipDetachment.TryCompute(Ship, roomId, out var shrunk, out var detached) || detached.Count != 1)
            return;

        var refund = CompartmentPricing.EntryForRoomName(room.Name) is { } entry ? CompartmentPricing.Refund(entry) : 0;
        ApplyShipDefinition(shrunk);
        Credits += refund;
    }

    // ---- doors ----

    private void TryPlaceDoor(PlaceDoorRequest request)
    {
        if (request.Passage is not (TileSide.East or TileSide.South) || request.Span is < 1 or > 3)
            return;
        var price = JunctionDoorBuilder.Price(request.Span);
        if (Credits < price)
            return;

        var def = Ship.ToDefinition();
        var (updated, _) = JunctionDoorBuilder.Place(Ship, def, new TileCoord(request.X, request.Y), request.Passage, request.Span,
            JunctionDoorBuilder.NextDoorId(def));
        if (updated is null || CustomShipValidator.Validate(updated).Count > 0)
            return;

        Credits -= price;
        ApplyShipDefinition(updated);
    }

    // Takes out a door that was placed in-game (the ones with remembered walls), for half its price back.
    private void TryRemoveDoor(string doorId)
    {
        var def = Ship.ToDefinition();
        var span = def.DoorEdges.Count(e => e.Id == doorId);
        if (JunctionDoorBuilder.Remove(def, doorId) is not { } updated || CustomShipValidator.Validate(updated).Count > 0)
            return;

        ApplyShipDefinition(updated);
        Credits += JunctionDoorBuilder.Price(Math.Clamp(span, 1, 3)) / 2;
    }

    // Test precondition: whether a junction door with this id exists on the ship.
    public bool DebugHasDoorEdge(string doorId) => Ship.DoorEdges.Any(e => e.Id == doorId);
}
