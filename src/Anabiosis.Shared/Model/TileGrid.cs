namespace Anabiosis.Shared.Model;

// M70 (humble-soaring-cat.md) - hull-local integer tile at 1x1 unit scale, replacing Room.X/Y's
// float rectangle origin. Tile (X,Y) occupies the world square [X, X+1) x [Y, Y+1) in the same
// hull-local frame Room/Door/WallBlock use today (World.ShipField.cs still owns the rigid-body
// transform into world space - nothing here changes that).
public readonly record struct TileCoord(int X, int Y);

// The four cardinal directions a wall/door/terminal can face on a tile.
public enum TileSide
{
    North,
    South,
    East,
    West,
}

public static class TileSideExtensions
{
    // North = -Y, South = +Y, matching the project's existing screen-space convention (Y grows
    // downward - see Room.Top/Bottom already meaning min-Y/max-Y).
    public static TileCoord Offset(this TileSide side, TileCoord origin) => side switch
    {
        TileSide.North => origin with { Y = origin.Y - 1 },
        TileSide.South => origin with { Y = origin.Y + 1 },
        TileSide.East => origin with { X = origin.X + 1 },
        TileSide.West => origin with { X = origin.X - 1 },
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    public static TileSide Opposite(this TileSide side) => side switch
    {
        TileSide.North => TileSide.South,
        TileSide.South => TileSide.North,
        TileSide.East => TileSide.West,
        TileSide.West => TileSide.East,
        _ => throw new ArgumentOutOfRangeException(nameof(side)),
    };

    public static readonly TileSide[] All = { TileSide.North, TileSide.South, TileSide.East, TileSide.West };
}

// M70 - a wall tile is either solid or a door; a door is a toggleable variant of a wall, not a
// separate rectangle type the way Door.cs is today. TileWallKind.None means "no wall here" (bare
// floor, or vacuum if HasFloor is also false).
public enum TileWallKind
{
    None,
    Solid,
    Door,
}

// M70 - one 1x1 cell of hull. A cell only exists in TileGrid.Cells once it has a floor; removing
// the floor removes the cell entirely (wall/device/terminal cannot outlive their floor - see
// TileGrid.SetFloor). Mutable class (not a record) because HP/open-state/occupancy change far more
// often than they're replaced wholesale, and TileGrid already indexes cells by TileCoord identity.
public sealed class TileCell
{
    public bool HasFloor;
    public TileWallKind Wall;
    public bool DoorOpen; // meaningful only when Wall == Door
    public float WallHp;  // meaningful only when Wall != None; <= 0 means breached (see TileGrid.IsBlockingForRegion)
    // Wall "skin" (direct user request, humble-soaring-cat.md M76 follow-up) - meaningful only when
    // Wall == Solid; a Door is never itself Reinforced/Window (see WallMaterial.cs), it stays
    // Standard regardless of what's set here.
    public WallMaterial WallMaterial;
    // Links exactly two adjacent Door tiles into one player-authored "wide" door (direct user
    // request: "дверь занимающая 1 на 2 тайла") - meaningful only when Wall == Door, null for an
    // ordinary single-tile door. Purely an editor/persistence bookkeeping field: the real ship's own
    // Door model (Ship.Custom.cs) already auto-widens a door up to Door.StandardSpanUnits (2 units)
    // from the room-pair overlap alone, so this doesn't need to reach the server at all - it only
    // drives how the editor RENDERS the pair (one merged sprite instead of two narrow ones) and keeps
    // them paired through remove/save/load.
    public string? DoorGroupId;
    public string? DeviceId;    // at most one device per cell; occupies the floor slot, blocks movement
    // Direct user request (confirmed geometry, doubly-confirmed via 2 rounds of clarifying
    // questions - this feature had been implemented twice before and rejected twice) - meaningful
    // only when DeviceId is also set, and only ever set for a Helm/Navigation "half" tile
    // (TileGrid.PlaceHalfWidthDevice); null for every ordinary device kind, unchanged. Mirrors
    // WallOpenSide's own EXACT effective convention (verified empirically against a real, passing
    // test - TestRunner.HalfThickWalls.cs): the named side ends up BLOCKED, the opposite side stays
    // walkable - see TileGrid.IsWalkable's position-aware overload for the combined truth table,
    // including the "this tile is ALSO a half-block wall's own open half" coexistence case (both
    // fields can be set on the very same TileCell at once - that's the whole point of this feature).
    public TileSide? DeviceOpenSide;
    // A wall-mounted device (direct user request - "на стену размером с полублок можно крепить
    // только терминал и настенную лампу"; TileGrid.PlaceWallDevice/PlaceRecessedWallDevice enforce
    // that restriction) - at most one per cell. WallDeviceKind is null exactly when WallDeviceId is.
    public string? WallDeviceId;
    public CustomDeviceKind? WallDeviceKind;
    // Which side of THIS tile the device's own half-block visual sits on - meaningful whenever
    // WallDeviceId is set, in EITHER mode: for a recessed device (WallDeviceRecessed true) that's
    // the free half, the OPPOSITE of this same tile's own WallOpenSide; for a floor-adjacent device
    // (false) it's the neighbor direction that actually carries the wall it's mounted on. Purely
    // which way the visual faces - see WallDeviceRecessed's own doc comment for COLLISION.
    public TileSide? WallDeviceMountSide;
    // Direct user request ("не угловые клетки занимали только половину блока") - meaningful only
    // when Wall == Solid, and only for a STRAIGHT wall run (exactly one outward-facing side, as if
    // this room stood alone - TileGridRasterizer.FromRooms computes it per-edge at generation time).
    // Null means either no wall, a Door, a breached-irrelevant wall, or a genuine CORNER (two edges
    // met here) - all of those stay full-thickness, unchanged from before this field existed. When
    // set, the SOLID half of the tile sits on this side; the free/walkable half is the opposite side
    // (TileSideExtensions.Opposite) - see TileGrid.IsWalkable's position-aware overload.
    public TileSide? WallOpenSide;
    // True when the wall-mounted device sits recessed in THIS tile's own free half
    // (PlaceRecessedWallDevice) rather than protruding onto an ordinary adjacent floor tile
    // (PlaceWallDevice/WallDeviceMountSide). Both modes fully block their own tile once placed
    // (IsWalkable's own WallDeviceId check) - a recessed one fills its own free half while the
    // wall's own solid half already blocks the rest of the same tile; a floor-adjacent one occupies
    // its whole floor tile like any ordinary device. Only the VISUAL differs: both still draw as a
    // half-block on WallDeviceMountSide's own side, never the full tile (ShipRenderer's own draw).
    public bool WallDeviceRecessed;
    // Direct user request ("система отсеков по-другому") - true when this wall was placed by the
    // Ship Editor's "place a whole compartment" tool (CompartmentPlacer.Stamp) rather than painted by
    // hand with the free-tile Wall tool. No gameplay meaning at all (WallMaterial already carries
    // every HP/skin consequence that survives into the real ship) - purely drives the editor canvas's
    // own distinct wall colour, so a wholly separate field rather than overloading WallMaterial.
    public bool WallFromCompartment;
}

// M70 - an emergent, computed "room": a connected group of open floor tiles bounded by walls
// and/or doors (open or closed - a door never merges two regions, see TileGrid's recompute logic).
// Nothing authors a SealedRegion directly; TileGrid derives and maintains it incrementally as the
// wall/floor layers change.
public sealed class SealedRegion
{
    public int Id;
    public HashSet<TileCoord> Tiles = new();
    public bool LeaksToVacuum; // true if any member tile borders true vacuum (no cell at all) rather than a wall/door
}

// Direct user request ("я хочу полностью переделать двери" - the narrow/1-tile door specifically,
// humble-soaring-cat.md) - a door as a barrier that lives on the EDGE between two adjacent floor
// tiles, rather than occupying a tile of its own the way TileWallKind.Door does. Both flanking
// tiles stay ordinary open floor (HasFloor:true, Wall:None) at all times - only this edge-level
// object decides whether movement/sight/region-connectivity crosses between them. Mirrors exactly
// what a Door tile already carries (an open flag plus HP/breach) - see TileGrid's own edge API
// below for how it plugs into region topology/collision/occlusion the same way a Door tile does.
public sealed class TileDoorEdge
{
    public string Id = "";
    public bool Open;
    public float Hp;
    public float MaxHp;
}

// M70 - the tile grid itself: sparse (Dictionary<TileCoord, TileCell>, not a 2D array) because a
// hull grows in any direction as it's built, including negative coordinates, with no known bounding
// box up front - the same "quiet dictionary keyed by id" pattern World.WallBlocks.cs and
// RoomGraphConnectivity.cs already use instead of an array.
//
// Region topology is recomputed incrementally, not by a full flood-fill every tick: adding an open
// floor tile unions it with whichever neighboring regions it touches; removing one (or sealing it
// with a wall/door) can only ever split ITS OLD region, so the BFS that re-labels the pieces is
// bounded by that one region's size, not the whole grid - the same cost RoomGraphConnectivity.
// ReachableFrom already pays per call today.
public sealed class TileGrid
{
    private int _nextRegionId = 1;

    public Dictionary<TileCoord, TileCell> Cells { get; } = new();
    public Dictionary<int, SealedRegion> Regions { get; } = new();
    public Dictionary<TileCoord, int> RegionIdOf { get; } = new();
    // Door-edge barriers (see TileDoorEdge's own doc comment) - keyed by a CANONICAL (coord, side)
    // pair so the same edge is never stored twice: always the tile with the smaller coordinate,
    // always East or South (CanonicalEdgeKey below), regardless of which of the two flanking tiles
    // a caller happens to ask from.
    public Dictionary<(TileCoord Coord, TileSide Side), TileDoorEdge> DoorEdges { get; } = new();

    public TileCell? CellAt(TileCoord coord) => Cells.TryGetValue(coord, out var cell) ? cell : null;

    // Normalizes (coord, side) to whichever of the two equivalent forms is canonical - a caller on
    // the "far" side of an edge (e.g. asking from the East neighbor about its own West side) gets
    // the same key a caller on the "near" side would. Only East/West and North/South pairs are
    // ever equivalent to each other (an edge has exactly 2 sides it can be described from).
    private static (TileCoord Coord, TileSide Side) CanonicalEdgeKey(TileCoord coord, TileSide side) => side switch
    {
        TileSide.West => (side.Offset(coord), TileSide.East),
        TileSide.North => (side.Offset(coord), TileSide.South),
        _ => (coord, side),
    };

    // The door edge between `coord` and its neighbor on `side`, if any - looked up canonically, so
    // it doesn't matter which of the two flanking tiles/sides a caller asks from.
    public TileDoorEdge? DoorEdgeAt(TileCoord coord, TileSide side) =>
        DoorEdges.TryGetValue(CanonicalEdgeKey(coord, side), out var edge) ? edge : null;

    // An edge blocks region connectivity/movement/sight exactly like an intact wall/door tile does
    // (IsBlockingForRegion's own "regardless of open/closed, only breach matters" rule) - only a
    // breached (Hp<=0) edge stops mattering.
    private bool IsBlockingEdge(TileCoord coord, TileSide side) => DoorEdgeAt(coord, side) is { Hp: > 0 };

    // Both flanking tiles must already be ordinary, empty open floor - no wall, no door, no device
    // on either one (direct user request: "чтобы блоки рядом не удалялись" - this door never
    // converts or clears anything, it only ever occupies the seam between two tiles that were
    // already exactly what they needed to be) - and there must be no door edge there yet.
    //
    // Direct user request ("сделай возможным поставить дверь если 1 клетка это пол а вторая
    // космос") - an edge with bare floor on exactly one side and genuinely nothing at all on the
    // other (no cell there, or a cell that was never floored) is a door onto open space, i.e. an
    // airlock (Ship.Custom.cs's doorEdges builder turns it into one by leaving that side's RoomId
    // null instead of throwing) - allowed alongside the original floor/floor case, never floor/
    // wall or space/space.
    public bool CanPlaceDoorEdge(TileCoord coord, TileSide side)
    {
        bool IsFreeFloor(TileCoord c) => CellAt(c) is { HasFloor: true, Wall: TileWallKind.None, DeviceId: null };
        bool IsOpenSpace(TileCoord c) => CellAt(c) is null or { HasFloor: false, Wall: TileWallKind.None, DeviceId: null };

        var neighbor = side.Offset(coord);
        var valid = (IsFreeFloor(coord) && IsFreeFloor(neighbor))
            || (IsFreeFloor(coord) && IsOpenSpace(neighbor))
            || (IsFreeFloor(neighbor) && IsOpenSpace(coord));
        return valid && DoorEdgeAt(coord, side) is null;
    }

    public int? RegionIdAt(TileCoord coord) => RegionIdOf.TryGetValue(coord, out var id) ? id : null;

    // M77 (humble-soaring-cat.md) - a deep, independent copy: World.ShipDebris.cs's DestroyRoomAndDetach
    // needs to ask "what would connectivity look like if this room's tiles were actually gone" without
    // mutating the live, already-synced Ship.Tiles other systems read this same tick. Copies every
    // TileCell by value (it's a mutable class, not a record) and carries the private region-id counter
    // forward too, so a region freshly split/created on the clone can never collide with an id that
    // still means something different back on the original.
    public TileGrid Clone()
    {
        var clone = new TileGrid { _nextRegionId = _nextRegionId };
        foreach (var (coord, cell) in Cells)
            clone.Cells[coord] = new TileCell
            {
                HasFloor = cell.HasFloor,
                Wall = cell.Wall,
                DoorOpen = cell.DoorOpen,
                WallHp = cell.WallHp,
                WallMaterial = cell.WallMaterial,
                DoorGroupId = cell.DoorGroupId,
                DeviceId = cell.DeviceId,
                DeviceOpenSide = cell.DeviceOpenSide,
                WallDeviceId = cell.WallDeviceId,
                WallDeviceKind = cell.WallDeviceKind,
                WallDeviceMountSide = cell.WallDeviceMountSide,
                WallFromCompartment = cell.WallFromCompartment,
                WallOpenSide = cell.WallOpenSide,
                WallDeviceRecessed = cell.WallDeviceRecessed,
            };
        foreach (var (id, region) in Regions)
            clone.Regions[id] = new SealedRegion { Id = region.Id, Tiles = new HashSet<TileCoord>(region.Tiles), LeaksToVacuum = region.LeaksToVacuum };
        foreach (var (coord, id) in RegionIdOf)
            clone.RegionIdOf[coord] = id;
        foreach (var (key, edge) in DoorEdges)
            clone.DoorEdges[key] = new TileDoorEdge { Id = edge.Id, Open = edge.Open, Hp = edge.Hp, MaxHp = edge.MaxHp };
        return clone;
    }

    // A door counts as a wall for region purposes regardless of open/closed state - only the
    // floor/wall LAYER matters for "is this pocket sealed," not the door's momentary state (see
    // Context in humble-soaring-cat.md: "дверь ... НИКОГДА не сливает два отсека в один").
    private static bool IsBlockingForRegion(TileCell cell) => cell.Wall != TileWallKind.None && cell.WallHp > 0;

    // Separate from region topology: a character can walk through an OPEN door but never through a
    // device, and a terminal never blocks anything (it's mounted to a wall's side, not standing in
    // the cell's walkable space). Exposed now as a pure query for the M73 collision milestone to
    // reuse - M70 itself doesn't call this.
    public static bool IsWalkable(TileCell cell)
    {
        if (!cell.HasFloor || cell.DeviceId != null)
            return false;
        // A wall-mounted device (Terminal/WallLamp) always fully blocks its own tile now (direct
        // user request, "в таком случае будет полностью заполнен тайл") - a recessed one already
        // fills its own free half while the wall's own solid half blocks the rest; a floor-adjacent
        // one occupies its whole floor tile like any ordinary device, just drawn as a half-block
        // flush against the wall it mounts on (ShipRenderer's own wall-device draw).
        if (cell.WallDeviceId != null)
            return false;
        return cell.Wall switch
        {
            TileWallKind.None => true,
            TileWallKind.Door => cell.DoorOpen && cell.WallHp > 0 || cell.WallHp <= 0,
            TileWallKind.Solid => cell.WallHp <= 0,
            _ => false,
        };
    }

    // Direct user request ("не угловые клетки занимали только половину блока... через неё можно
    // ходить") - position-aware overload used only by TileMovement's per-corner sampling. Every
    // other case (corner/door/breached/no-wall/no-floor) delegates straight to the tile-level
    // IsWalkable(cell) above, unchanged - this only special-cases an intact, half-thick, non-corner
    // Solid wall, which is the one situation where "walkable" genuinely depends on WHERE inside the
    // tile the point falls, not just which tile it's in.
    // Extended (direct user request, confirmed geometry via 2 rounds of clarifying questions - a
    // feature implemented twice before and rejected twice) to also fold in TileCell.DeviceOpenSide -
    // Helm/Navigation's own genuine 1.5-tile collision, mirroring WallOpenSide's exact convention
    // (do not invert it, see DeviceOpenSide's own doc comment) via the SAME IsInSolidHalf helper.
    // The combined truth table, verified against real TileMovement.MoveAlongAxis calls (this file's
    // own test suite), not just by inspection:
    //  - No wall, DeviceId set, DeviceOpenSide null -> blocked everywhere (every ordinary device,
    //    completely unchanged).
    //  - No wall, DeviceId set, DeviceOpenSide = X -> blocked only in half X, walkable in the
    //    opposite half (Helm/Navigation standing alone on open floor).
    //  - Solid wall with WallOpenSide = O, no DeviceId -> blocked only in half O (existing
    //    half-block wall, completely unchanged).
    //  - Solid wall with WallOpenSide = O, WallDeviceId set (Terminal/WallLamp) -> blocked
    //    everywhere (existing behavior, unrelated field, completely unchanged).
    //  - Solid wall with WallOpenSide = O, DeviceId set (Helm/Navigation's half tile, placed via
    //    PlaceHalfWidthDevice which only ever allows this when DeviceOpenSide already equals the
    //    wall's own O) -> blocked everywhere: the wall's own half O is blocked by the wall check
    //    below exactly as it always was, and the tile's remaining (formerly open) half is now ALSO
    //    claimed by the device - by construction, not a special-cased "if openSide==O" shortcut,
    //    since simply reaching this branch with any DeviceId present already means the device
    //    occupies whatever's left of this tile once the wall's own half is accounted for. This is
    //    the "console uses the wall's own already-open half, the wall itself is never destroyed"
    //    case (previously wrong: TileGrid.PlaceDeviceWithHalfBlockWallLeniency used to delete the
    //    wall outright to make room - deleted along with this bug).
    public static bool IsWalkable(TileCell cell, TileCoord coord, Vec2 position)
    {
        if (cell.Wall == TileWallKind.Solid && cell.WallHp > 0 && cell.WallOpenSide is { } wallOpenSide)
        {
            if (cell.WallDeviceId is not null)
                return false;
            if (IsInSolidHalf(coord, position, wallOpenSide))
                return false;
            return cell.DeviceId is null;
        }
        if (cell.DeviceId is not null)
            return cell.DeviceOpenSide is { } deviceOpenSide && !IsInSolidHalf(coord, position, deviceOpenSide);
        return IsWalkable(cell);
    }

    // Direct user request ("я хочу полностью переделать двери") - used by TileMovement's own
    // corner-sampling alongside the per-tile IsWalkable checks above: is stepping from `from` into
    // the adjacent tile `to` blocked by an intact, closed door edge between them? Same "open OR
    // breached is walkable, otherwise not" contract TileWallKind.Door's own IsWalkable already has -
    // an instance method (not static like IsWalkable above) since edges live on THIS grid's own
    // DoorEdges dictionary, not on a single TileCell a caller could pass in directly.
    public bool IsWalkableAcrossEdge(TileCoord from, TileCoord to)
    {
        if (SideTo(from, to) is not { } side)
            return true; // not actually adjacent (e.g. same tile, or a diagonal corner check) - nothing to check
        if (DoorEdgeAt(from, side) is not { } edge)
            return true;
        return edge.Open && edge.Hp > 0 || edge.Hp <= 0;
    }

    private static TileSide? SideTo(TileCoord from, TileCoord to) => (to.X - from.X, to.Y - from.Y) switch
    {
        (1, 0) => TileSide.East,
        (-1, 0) => TileSide.West,
        (0, 1) => TileSide.South,
        (0, -1) => TileSide.North,
        _ => null,
    };

    // North = -Y, South = +Y, East = +X, West = -X (TileSideExtensions' own convention) - the solid
    // half sits on `solidSide`, occupying the [0, 0.5) or [0.5, 1) fraction of the tile's own local
    // square on the matching axis.
    private static bool IsInSolidHalf(TileCoord coord, Vec2 position, TileSide solidSide)
    {
        var fx = position.X - coord.X;
        var fy = position.Y - coord.Y;
        return solidSide switch
        {
            TileSide.North => fy < 0.5,
            TileSide.South => fy >= 0.5,
            TileSide.West => fx < 0.5,
            TileSide.East => fx >= 0.5,
            _ => throw new ArgumentOutOfRangeException(nameof(solidSide)),
        };
    }

    private static bool IsRegionMember(TileCell cell) => cell.HasFloor && !IsBlockingForRegion(cell);

    public void SetFloor(TileCoord coord, bool hasFloor)
    {
        if (hasFloor)
        {
            if (Cells.TryGetValue(coord, out var existing) && existing.HasFloor)
                return; // already floored, nothing to do
            var cell = existing ?? new TileCell();
            cell.HasFloor = true;
            Cells[coord] = cell;
            if (IsRegionMember(cell))
                OnTileBecameRegionMember(coord);
        }
        else
        {
            if (!Cells.TryGetValue(coord, out var cell) || !cell.HasFloor)
                return; // nothing to remove
            // M-doors-as-edges (humble-soaring-cat.md) - a door edge is NOT part of the TileCell
            // being removed below (it lives in the separate DoorEdges dictionary, keyed by coord+
            // side rather than riding along on either flanking tile's own cell), so it would
            // otherwise survive this tile's own floor being erased entirely, left pointing at a
            // coordinate that no longer has one. Removed on every side BEFORE the cell itself goes,
            // same "can't outlive their floor" rule every other dependent here already follows.
            foreach (var side in TileSideExtensions.All)
                if (DoorEdgeAt(coord, side) is not null)
                    RemoveDoorEdge(coord, side);
            // Wall/device/terminal cannot outlive their floor - pull the tile out of its region
            // first (if it was a member), then drop the cell entirely.
            if (IsRegionMember(cell))
                OnTileLeftRegionMembership(coord);
            Cells.Remove(coord);
        }
    }

    // kind == None clears the wall (and any door-open state); Solid/Door install a fresh,
    // full-health wall of that kind. Requires a floor already at `coord` - walls cannot be placed
    // in open space (mirrors the "floor is the mandatory substrate" rule from the plan).
    public void SetWall(TileCoord coord, TileWallKind kind, float hp = 100f, WallMaterial material = WallMaterial.Standard, bool fromCompartment = false)
    {
        if (!Cells.TryGetValue(coord, out var cell) || !cell.HasFloor)
            throw new InvalidOperationException($"Cannot place a wall at {coord} without a floor there first.");

        // M-doors-as-edges - a door edge's own placement guard (CanPlaceDoorEdge) requires both
        // flanking tiles to stay bare, wall-free floor for as long as the edge stands; painting an
        // actual wall/door TILE directly onto one of those flanks (the editor's Wall tool has no
        // reason to know about a neighboring edge) would silently leave that invariant broken, so any
        // edge still anchored to `coord` is removed first - same "can't outlive its precondition"
        // rule SetFloor's own removal branch already follows for this same dictionary.
        if (kind != TileWallKind.None)
            foreach (var side in TileSideExtensions.All)
                if (DoorEdgeAt(coord, side) is not null)
                    RemoveDoorEdge(coord, side);

        var wasMember = IsRegionMember(cell);
        cell.Wall = kind;
        cell.WallHp = kind == TileWallKind.None ? 0f : hp;
        // A Door is never itself a material variant (WallMaterial.cs) - only a Solid wall keeps
        // whatever was requested; clearing a wall or replacing it with a door both reset to Standard.
        cell.WallMaterial = kind == TileWallKind.Solid ? material : WallMaterial.Standard;
        cell.WallFromCompartment = kind != TileWallKind.None && fromCompartment;
        // WallOpenSide is only ever meaningful for an intact Solid wall (see its own doc comment) -
        // clearing the wall or replacing it with a Door both reset it to null, same "not a material
        // variant any more" rule WallMaterial follows above. Callers that DO want a fresh Solid wall
        // to carry an open side call SetWallOpenSide separately afterwards (TileGridRasterizer only
        // knows corner-vs-straight once every wall tile in the pass has been stamped).
        if (kind != TileWallKind.Solid)
            cell.WallOpenSide = null;
        if (kind != TileWallKind.Door)
        {
            cell.DoorOpen = false;
            cell.DoorGroupId = null;
        }
        var isMemberNow = IsRegionMember(cell);

        if (wasMember && !isMemberNow)
            OnTileLeftRegionMembership(coord);
        else if (!wasMember && isMemberNow)
            OnTileBecameRegionMember(coord);
    }

    // Pairs two already-placed Door tiles into one player-authored "wide" door (see TileCell.
    // DoorGroupId's own doc comment) - both must already be Door tiles; a fresh id is generated so
    // each call produces its own independent pair.
    public void LinkDoors(TileCoord a, TileCoord b)
    {
        if (CellAt(a) is not { Wall: TileWallKind.Door } cellA || CellAt(b) is not { Wall: TileWallKind.Door } cellB)
            throw new InvalidOperationException($"Cannot link {a} and {b} into a wide door - both must already be door tiles.");
        var groupId = Guid.NewGuid().ToString("N");
        cellA.DoorGroupId = groupId;
        cellB.DoorGroupId = groupId;
    }

    // Direct user request ("я хочу полностью переделать двери" - the narrow/1-tile door) - installs
    // a fresh, full-health door edge between `coord` and its `side` neighbor. Both tiles were
    // already ordinary open floor and STAY that way - unlike SetWall, this never touches either
    // tile's own Wall/DeviceId. If the two tiles are (as they normally are, being adjacent open
    // floor) currently in the same region, that region may now split into two - the exact same kind
    // of split placing a wall/door TILE on a floor tile already causes, just triggered from an edge
    // instead of a cell (SplitRegionIfDisconnected handles the "is there another path around" case
    // correctly on its own, same as it always has).
    public void AddDoorEdge(TileCoord coord, TileSide side, string id, float hp = 100f, float maxHp = 100f)
    {
        if (!CanPlaceDoorEdge(coord, side))
            throw new InvalidOperationException($"Cannot place a door edge at {coord}/{side} - both flanking tiles must already be empty open floor.");
        var key = CanonicalEdgeKey(coord, side);
        DoorEdges[key] = new TileDoorEdge { Id = id, Open = false, Hp = hp, MaxHp = maxHp };
        TrySplitAcrossEdge(coord, side);
    }

    // Removes a door edge entirely (as opposed to breaching it - see DamageDoorEdge/RepairDoorEdge
    // below for HP-driven, repairable breaches). If the two flanking tiles ended up in different
    // regions while the edge stood, removing it reunites them - same MergeRegionsInto step
    // OnTileBecameRegionMember already uses for an ordinary floor tile reconnecting two pockets.
    public void RemoveDoorEdge(TileCoord coord, TileSide side)
    {
        var key = CanonicalEdgeKey(coord, side);
        if (!DoorEdges.Remove(key))
            return;
        TryMergeAcrossEdge(coord, side);
    }

    // Never changes region topology - same "open/closed is purely cosmetic for sealing purposes"
    // contract SetDoorOpen already has for a tile-based door (IsBlockingForRegion only cares about
    // Hp, not Open).
    public void SetDoorEdgeOpen(TileCoord coord, TileSide side, bool open)
    {
        if (DoorEdgeAt(coord, side) is { } edge)
            edge.Open = open;
    }

    // Mirrors DamageWall/RepairWall/SetWallHp - reducing Hp to zero or below breaches the edge,
    // which (for region purposes only) behaves exactly like removing it outright; repairing it back
    // above zero re-seals it. Movement/sight already read Hp directly (IsBlockingEdge), so those
    // follow along with no extra bookkeeping.
    public void DamageDoorEdge(TileCoord coord, TileSide side, float amount)
    {
        if (DoorEdgeAt(coord, side) is not { } edge)
            return;
        var wasBlocking = edge.Hp > 0;
        edge.Hp = MathF.Max(0f, edge.Hp - amount);
        if (wasBlocking && edge.Hp <= 0)
            TryMergeAcrossEdge(coord, side);
    }

    public void RepairDoorEdge(TileCoord coord, TileSide side, float amount)
    {
        if (DoorEdgeAt(coord, side) is not { } edge)
            return;
        var wasBlocking = edge.Hp > 0;
        edge.Hp = MathF.Min(edge.MaxHp, edge.Hp + amount);
        if (!wasBlocking && edge.Hp > 0)
            TrySplitAcrossEdge(coord, side);
    }

    // M72-style reconciliation entry point (World.TileSync.cs mirrors an authoritative HP value
    // every tick rather than replaying individual damage/repair deltas) - same shape as SetWallHp.
    public void SetDoorEdgeHp(TileCoord coord, TileSide side, float hp)
    {
        if (DoorEdgeAt(coord, side) is not { } edge)
            return;
        var wasBlocking = edge.Hp > 0;
        edge.Hp = MathF.Max(0f, hp);
        var isBlockingNow = edge.Hp > 0;
        if (wasBlocking && !isBlockingNow)
            TryMergeAcrossEdge(coord, side);
        else if (!wasBlocking && isBlockingNow)
            TrySplitAcrossEdge(coord, side);
    }

    // A just-breached (or just-removed) edge no longer blocks ConnectedNeighbors - if that leaves
    // its two flanking tiles in different regions, merge them (mirrors OnTileBecameRegionMember's
    // own reunion logic, just triggered from an edge disappearing instead of a tile appearing).
    private void TryMergeAcrossEdge(TileCoord coord, TileSide side)
    {
        var neighbor = side.Offset(coord);
        if (RegionIdOf.TryGetValue(coord, out var regionId) && RegionIdOf.TryGetValue(neighbor, out var neighborRegionId) && regionId != neighborRegionId)
        {
            MergeRegionsInto(regionId, new[] { neighborRegionId });
            RecomputeLeak(regionId);
        }
    }

    // A just-repaired (or just-added) edge might now cut its two flanking tiles' shared region in
    // two (mirrors AddDoorEdge's own split trigger).
    private void TrySplitAcrossEdge(TileCoord coord, TileSide side)
    {
        var neighbor = side.Offset(coord);
        if (RegionIdOf.TryGetValue(coord, out var regionId) && RegionIdOf.TryGetValue(neighbor, out var neighborRegionId) && regionId == neighborRegionId)
            SplitRegionIfDisconnected(regionId);
    }

    // M72 follow-up (World.TileSync.cs) - mirrors a live WallBlock's own Material onto its tile, the
    // same "reconcile against an already-known authoritative value" shape SetWallHp already uses.
    // Never changes region topology (material is purely cosmetic/HP-cap, like WallHp itself).
    public void SetWallMaterial(TileCoord coord, WallMaterial material)
    {
        if (Cells.TryGetValue(coord, out var cell) && cell.Wall == TileWallKind.Solid)
            cell.WallMaterial = material;
    }

    // Direct user request ("не угловые клетки занимали только половину блока") - separate from
    // SetWall itself because "is this tile a corner" is only knowable once every wall tile in a
    // whole generation pass has been stamped (TileGridRasterizer.FromRooms collects claims across
    // all 4 edge-loops before calling this) - same no-op-if-not-Solid guard SetWallMaterial uses.
    public void SetWallOpenSide(TileCoord coord, TileSide? side)
    {
        if (Cells.TryGetValue(coord, out var cell) && cell.Wall == TileWallKind.Solid)
            cell.WallOpenSide = side;
    }

    public void SetDoorOpen(TileCoord coord, bool open)
    {
        if (!Cells.TryGetValue(coord, out var cell) || cell.Wall != TileWallKind.Door)
            throw new InvalidOperationException($"No door at {coord} to open/close.");
        cell.DoorOpen = open; // never changes region topology - see IsBlockingForRegion
    }

    // Reducing a wall/door's HP to zero or below breaches it, which - for region purposes only -
    // behaves exactly like removing the wall (regions merge back together); repairing it above zero
    // re-seals it (regions can split again). Movement/atmosphere leak-RATE consequences of partial
    // damage belong to later milestones (M72/M73), not this core data structure.
    public void DamageWall(TileCoord coord, float amount)
    {
        if (!Cells.TryGetValue(coord, out var cell) || cell.Wall == TileWallKind.None)
            return;
        var wasMember = IsRegionMember(cell);
        cell.WallHp = MathF.Max(0f, cell.WallHp - amount);
        var isMemberNow = IsRegionMember(cell);
        if (!wasMember && isMemberNow)
            OnTileBecameRegionMember(coord);
    }

    public void RepairWall(TileCoord coord, float amount, float maxHp = 100f)
    {
        if (!Cells.TryGetValue(coord, out var cell) || cell.Wall == TileWallKind.None)
            return;
        var wasMember = IsRegionMember(cell);
        cell.WallHp = MathF.Min(maxHp, cell.WallHp + amount);
        var isMemberNow = IsRegionMember(cell);
        if (wasMember && !isMemberNow)
            OnTileLeftRegionMembership(coord);
    }

    // M72 - sets HP to an absolute value rather than applying a delta, for reconciling against an
    // already-known authoritative value (World.TileSync.cs mirrors World's own _doorHp/_wallBlockHp
    // dictionaries here every tick) instead of replaying every individual damage/repair event.
    public void SetWallHp(TileCoord coord, float hp)
    {
        if (!Cells.TryGetValue(coord, out var cell) || cell.Wall == TileWallKind.None)
            return;
        var wasMember = IsRegionMember(cell);
        cell.WallHp = MathF.Max(0f, hp);
        var isMemberNow = IsRegionMember(cell);
        if (!wasMember && isMemberNow)
            OnTileBecameRegionMember(coord);
        else if (wasMember && !isMemberNow)
            OnTileLeftRegionMembership(coord);
    }

    // Devices/terminals never affect region topology at all (only the floor/wall layer does) - see
    // Core data structures in humble-soaring-cat.md. These are plain occupancy setters.
    public void PlaceDevice(TileCoord coord, string deviceId)
    {
        if (!Cells.TryGetValue(coord, out var cell) || !cell.HasFloor)
            throw new InvalidOperationException($"Cannot place a device at {coord} without a floor there first.");
        if (cell.Wall != TileWallKind.None)
            throw new InvalidOperationException($"Cannot place a device at {coord} - a wall/door already occupies that slot.");
        cell.DeviceId = deviceId;
    }

    public void RemoveDevice(TileCoord coord)
    {
        if (Cells.TryGetValue(coord, out var cell))
        {
            cell.DeviceId = null;
            // Direct user request - clearing DeviceOpenSide too (harmless no-op for every ordinary
            // device, which never had it set) is what lets a removed Helm/Navigation "half" tile's
            // own coexisting half-block wall come back into full, ordinary use - RemoveDevice never
            // touches Wall/WallHp/WallOpenSide at all, so a wall that was coexisting here is left
            // completely undisturbed, exactly as before this device ever claimed half its tile.
            cell.DeviceOpenSide = null;
        }
    }

    // Direct user request ("на стену размером с полублок можно крепить только терминал и настенную
    // лампу") - only these two kinds mount to a wall at all; everything else still goes through
    // PlaceDevice's own ordinary open-floor footprint instead.
    private static readonly IReadOnlySet<CustomDeviceKind> WallMountableKinds =
        new HashSet<CustomDeviceKind> { CustomDeviceKind.Terminal, CustomDeviceKind.WallLamp };

    private static void RequireWallMountable(CustomDeviceKind kind)
    {
        if (!WallMountableKinds.Contains(kind))
            throw new InvalidOperationException($"{kind} cannot be wall-mounted - only Terminal and WallLamp can.");
    }

    // Protrudes onto an ordinary floor tile from a neighboring FULL-thickness wall only (a
    // half-thick one already has its own free half to embed into instead - PlaceRecessedWallDevice
    // below - so protruding from it too would be redundant, direct user report: "стены в пол блока
    // являются стенами и на них якобы можно крепить лампу... для таких стен это должно быть
    // невозможно и это можно было сделать только в том же тайле что и стена"). Direct user request
    // ("в таком случае будет полностью заполнен тайл") - occupies its WHOLE tile for collision
    // (IsWalkable's own WallDeviceId check), same as any ordinary device, even though it still
    // visually reads as a half-block flush against the wall (ShipRenderer's own draw).
    public void PlaceWallDevice(TileCoord coord, TileSide wallSide, CustomDeviceKind kind, string deviceId)
    {
        RequireWallMountable(kind);
        if (!IsFloorAdjacentMountable(coord, wallSide))
            throw new InvalidOperationException($"Cannot place {kind} at {coord} facing {wallSide} - needs a floor tile with a full-thickness wall neighbor there.");
        RestoreWallDevice(coord, kind, deviceId, wallSide, recessed: false);
    }

    // Removes a wall device from `coord` - Terminal/WallLamp are the only two kinds that ever reach
    // here (Helm/Navigation went back to being plain ordinary Device-tool devices, removed via
    // RemoveDevice like any other), so a single-tile clear is always correct.
    public void RemoveWallDevice(TileCoord coord)
    {
        if (!Cells.TryGetValue(coord, out var cell))
            return;
        cell.WallDeviceId = null;
        cell.WallDeviceKind = null;
        cell.WallDeviceMountSide = null;
        cell.WallDeviceRecessed = false;
    }

    // Direct user request/persistence support - sets every wall-device field directly with NO
    // validation, for the one legitimate case that isn't a fresh placement: reloading a previously-
    // placed device from a save (Game1.ShipEditor.TileSave.cs's ApplyEditorTileCanvas). The data was
    // already valid when first placed, so re-running PlaceWallDevice/PlaceRecessedWallDevice's own
    // checks would be redundant at best. Every public Place* method below validates first, then calls
    // this to actually apply the change.
    public void RestoreWallDevice(TileCoord coord, CustomDeviceKind kind, string deviceId, TileSide mountSide, bool recessed)
    {
        if (!Cells.TryGetValue(coord, out var cell))
            return;
        cell.WallDeviceId = deviceId;
        cell.WallDeviceKind = kind;
        cell.WallDeviceMountSide = mountSide;
        cell.WallDeviceRecessed = recessed;
    }

    // Direct user request ("на пустой стороне можно поставить терминал и он будет занимать весь
    // полублок") - a device embedded directly in a half-thick wall tile's own free half, rather
    // than on an adjacent floor tile mounted against a neighbor (PlaceWallDevice above). Both modes
    // now fully block their own tile (IsWalkable's own WallDeviceId check). WallDeviceMountSide
    // always means "which side of THIS tile the device's own half-block visual sits on" - for a
    // recessed device that's the free half, the OPPOSITE of the wall's own solid WallOpenSide (not
    // WallOpenSide itself), so rendering never needs to branch on WallDeviceRecessed at all.
    public void PlaceRecessedWallDevice(TileCoord coord, CustomDeviceKind kind, string deviceId)
    {
        RequireWallMountable(kind);
        if (!IsRecessedMountableAnySide(coord, out var mountSide))
            throw new InvalidOperationException($"Cannot recess {kind} at {coord} - needs a non-corner Solid wall tile.");
        RestoreWallDevice(coord, kind, deviceId, mountSide, recessed: true);
    }

    // A floor tile with a genuine FULL-thickness wall/door neighbor on `mountSide` - PlaceWallDevice's
    // own single-tile precondition.
    private bool IsFloorAdjacentMountable(TileCoord coord, TileSide mountSide) =>
        CellAt(coord) is { HasFloor: true, WallDeviceId: null } &&
        CellAt(mountSide.Offset(coord)) is { Wall: not TileWallKind.None, WallOpenSide: null };

    // A non-corner half-thick Solid wall tile whose own free half (WallOpenSide.Opposite()) is
    // `mountSide` - PlaceRecessedWallDevice's own single-tile precondition, factored out the same way.
    private bool IsRecessedMountable(TileCoord coord, TileSide mountSide) =>
        CellAt(coord) is { Wall: TileWallKind.Solid, WallDeviceId: null } cell && cell.WallOpenSide == mountSide.Opposite();

    // PlaceRecessedWallDevice's own original single-tile form - any qualifying free half, not a
    // specific caller-chosen one - expressed in terms of IsRecessedMountable above so both call sites
    // share one true definition of "recess-mountable."
    private bool IsRecessedMountableAnySide(TileCoord coord, out TileSide mountSide)
    {
        if (CellAt(coord) is { Wall: TileWallKind.Solid, WallDeviceId: null, WallOpenSide: { } openSide })
        {
            mountSide = openSide.Opposite();
            return true;
        }
        mountSide = default;
        return false;
    }

    // Direct user request (confirmed geometry via 2 rounds of clarifying questions, doubly-confirmed -
    // this feature had already been implemented twice before and rejected twice) - Helm/Navigation are
    // ordinary rotatable FLOOR-STANDING furniture (same shape as Bed/StorageRack), but their real
    // collision/reservation is genuinely 1.5 tiles wide (or tall, when Rotated), not a full 2 - this is
    // the ONE tile of a Helm/Navigation footprint that's only half-claimed (the other, "full" tile of
    // the pair goes through ordinary PlaceDevice, unchanged). `openSide` names the half of THIS tile
    // the device's own body occupies - always the side facing the footprint's other ("full") tile
    // (TileCell.DeviceOpenSide's own doc comment has the exact convention), so the two halves sit
    // directly against each other with no gap and read as one continuous 1.5-tile object (direct user
    // bug report - "они должны быть вплотную" - an earlier version of this put the occupied half on
    // the FAR side instead, leaving a walkable sliver, and a visible seam, between the two pieces).
    //
    // Two placement shapes are accepted, replacing the old CanPlaceFootprintWithHalfBlockWallLeniency/
    // PlaceDeviceWithHalfBlockWallLeniency (deleted - that mechanism was confirmed WRONG: it destroyed
    // a real half-block wall's own Wall/WallOpenSide/WallHp to make room, direct user rejection,
    // "стена остаётся, консоль просто использует её открытую половину"):
    //  - Standalone: `coord` is ordinary bare floor (Wall:None, no device yet) - same precondition
    //    PlaceDevice already has for every other kind.
    //  - Wall-adjacent: `coord` is ALREADY a half-block wall tile (Wall:Solid, WallHp>0) on the SAME
    //    AXIS as `openSide` (both East/West, or both North/South) - direct user bug report ("почему я
    //    не могу вот так вот поставить прибор", a real hand-authored hull's own boundary wall had
    //    WallOpenSide=West while the console needed East there). An EARLIER version of this required
    //    the exact same TileSide value, not just the same axis - but that's stricter than the actual
    //    geometry needs: whichever of the wall's own two halves is solid, the device's rendered body
    //    (ShipRenderer.Devices.cs's own HalfSide-driven shift, independent of the wall entirely) either
    //    lands on top of it (same value - the wall's own solid material, covered by the console's art)
    //    or beside it, in the wall's own already-open half (opposite value, same axis - "the console
    //    uses the wall's own open half", the ORIGINAL design intent, "стена остаётся, консоль просто
    //    использует её открытую половину") - both read as a perfectly ordinary, un-broken visual, and
    //    TileGrid.IsWalkable's own combined truth table blocks the WHOLE tile either way once a
    //    DeviceId coexists with a Solid+WallOpenSide wall, regardless of which specific side matched.
    //    Only a genuinely DIFFERENT axis (a North/South wall under an East/West console, or vice
    //    versa) is rejected below - that combination really would carve the tile up inconsistently.
    //    Wall/WallHp/WallOpenSide/WallMaterial are NEVER touched in this case - the wall stays
    //    completely intact, coexisting with the new DeviceId/DeviceOpenSide on the very same TileCell.
    public bool CanPlaceHalfWidthDevice(TileCoord coord, TileSide openSide)
    {
        if (CellAt(coord) is not { DeviceId: null } cell)
            return false;
        if (cell.Wall == TileWallKind.None)
            // Standalone case only - needs genuine bare floor, same precondition PlaceDevice already has.
            return cell.HasFloor;
        // Wall-adjacent case - direct user bug report ("почему я не могу вот так вот поставить прибор",
        // a hand-authored hull's own boundary half-block wall has HasFloor:false, since it was never
        // interior floor a player painted - it's still a perfectly intact half-block wall (IsWalkable's
        // own combined truth table above never looks at HasFloor either, only Wall/WallOpenSide/
        // WallHp/DeviceId), so requiring HasFloor here too only ever blocked coexistence with a
        // hand-authored/procedural wall, never a player-drawn one, for no actual collision reason.
        if (cell.Wall != TileWallKind.Solid || cell.WallHp <= 0 || cell.WallDeviceId is not null || cell.WallOpenSide is not { } wallOpenSide)
            return false;
        var isHorizontal = openSide is TileSide.East or TileSide.West;
        var wallIsHorizontal = wallOpenSide is TileSide.East or TileSide.West;
        return isHorizontal == wallIsHorizontal;
    }

    public void PlaceHalfWidthDevice(TileCoord coord, TileSide openSide, string deviceId)
    {
        if (!CanPlaceHalfWidthDevice(coord, openSide))
            throw new InvalidOperationException($"Cannot place a half-width device at {coord} facing {openSide} - needs bare floor or a matching half-block wall there.");
        var cell = Cells[coord];
        cell.DeviceId = deviceId;
        cell.DeviceOpenSide = openSide;
    }

    private IEnumerable<TileCoord> Neighbors(TileCoord coord)
    {
        foreach (var side in TileSideExtensions.All)
            yield return side.Offset(coord);
    }

    // Same as Neighbors, but skips any side that has an intact door-edge barrier on it - region
    // topology (and, by the same token, movement/sight elsewhere) never crosses a closed-or-open-
    // but-unbreached edge door, exactly like it never crosses an intact wall/door TILE
    // (IsBlockingForRegion's own doc comment: "regardless of open/closed state, only breach
    // matters"). This is the ONE place region connectivity needs to know edges exist at all - every
    // other region-topology method below already goes through here via Neighbors' two call sites.
    private IEnumerable<TileCoord> ConnectedNeighbors(TileCoord coord)
    {
        foreach (var side in TileSideExtensions.All)
            if (!IsBlockingEdge(coord, side))
                yield return side.Offset(coord);
    }

    // A floor tile just became an open (non-walled) member of the region graph - either it's brand
    // new, or a wall/door on it was removed/breached. Union it with every neighboring region it
    // touches (there can be more than one, if it reconnects two previously-separate pockets).
    private void OnTileBecameRegionMember(TileCoord coord)
    {
        var neighborRegionIds = new HashSet<int>();
        foreach (var neighbor in ConnectedNeighbors(coord))
            if (RegionIdOf.TryGetValue(neighbor, out var id))
                neighborRegionIds.Add(id);

        int survivorId;
        if (neighborRegionIds.Count == 0)
        {
            var region = new SealedRegion { Id = _nextRegionId++ };
            region.Tiles.Add(coord);
            Regions[region.Id] = region;
            RegionIdOf[coord] = region.Id;
            survivorId = region.Id;
        }
        else
        {
            survivorId = neighborRegionIds.First();
            var survivor = Regions[survivorId];
            survivor.Tiles.Add(coord);
            RegionIdOf[coord] = survivorId;
            MergeRegionsInto(survivorId, neighborRegionIds.Where(id => id != survivorId));
        }

        RecomputeLeak(survivorId);
        // Merging can also change whether NEIGHBORING regions still leak (a tile that used to be a
        // dead end bordering vacuum might now be interior) - but only tiles adjacent to the changed
        // one could possibly be affected, so just refresh this one region; the moved-in tiles came
        // from regions that no longer exist, and their leak status is superseded by the survivor's.
    }

    // Extracted from OnTileBecameRegionMember's own merge step so RemoveDoorEdge (a door edge no
    // longer separating what turn out to be two different regions) can reuse the exact same
    // absorb-and-remove logic without a coord of its own to add first.
    private void MergeRegionsInto(int survivorId, IEnumerable<int> otherIds)
    {
        var survivor = Regions[survivorId];
        foreach (var otherId in otherIds)
        {
            var other = Regions[otherId];
            foreach (var tile in other.Tiles)
            {
                survivor.Tiles.Add(tile);
                RegionIdOf[tile] = survivorId;
            }
            Regions.Remove(otherId);
        }
    }

    // A floor tile just stopped being an open region member - either its floor was removed, or a
    // wall/door was placed/repaired on it. Pull it out of its old region, which may now be split
    // into several disconnected pieces; the search for those pieces is bounded by the old region's
    // own tile count, never the whole grid.
    private void OnTileLeftRegionMembership(TileCoord coord)
    {
        if (!RegionIdOf.TryGetValue(coord, out var oldRegionId))
            return;
        var oldRegion = Regions[oldRegionId];
        oldRegion.Tiles.Remove(coord);
        RegionIdOf.Remove(coord);

        if (oldRegion.Tiles.Count == 0)
        {
            Regions.Remove(oldRegionId);
            return;
        }

        SplitRegionIfDisconnected(oldRegionId);
    }

    // Re-partitions whatever tiles are CURRENTLY in Regions[regionId] by connectivity
    // (ConnectedNeighbors, which already respects door edges) and, if that comes back as more than
    // one piece, replaces the single region with one fresh region per piece. Shared by
    // OnTileLeftRegionMembership (a tile was removed from the region first) and AddDoorEdge (no
    // tile removed - the region's own membership is unchanged, but a NEW edge inside it may now cut
    // it into two, exactly the same kind of split just triggered a different way).
    private void SplitRegionIfDisconnected(int regionId)
    {
        var region = Regions[regionId];
        var remaining = new HashSet<TileCoord>(region.Tiles);
        var pieces = new List<HashSet<TileCoord>>();
        while (remaining.Count > 0)
        {
            var start = remaining.First();
            var piece = new HashSet<TileCoord> { start };
            remaining.Remove(start);
            var queue = new Queue<TileCoord>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in ConnectedNeighbors(current))
                {
                    if (remaining.Remove(neighbor))
                    {
                        piece.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
            pieces.Add(piece);
        }

        if (pieces.Count == 1)
        {
            // Still one connected piece (whatever triggered this didn't actually disconnect
            // anything - e.g. a loop/ring shape with another path around the new edge) - keep the
            // same region id, its Tiles set is already correct.
            RecomputeLeak(regionId);
            return;
        }

        Regions.Remove(regionId);
        foreach (var piece in pieces)
        {
            var newRegion = new SealedRegion { Id = _nextRegionId++, Tiles = piece };
            Regions[newRegion.Id] = newRegion;
            foreach (var tile in piece)
                RegionIdOf[tile] = newRegion.Id;
            RecomputeLeak(newRegion.Id);
        }
    }

    // A region leaks to vacuum if any member tile borders a coordinate with no cell at all (true
    // open space - no hull plating there). A neighbor that exists but is walled/doored is sealed;
    // a neighbor that exists and is itself an open floor tile is necessarily already part of the
    // same region (regions are exactly the connected components of open floor), so it can never be
    // the cause of a leak.
    private void RecomputeLeak(int regionId)
    {
        var region = Regions[regionId];
        foreach (var tile in region.Tiles)
        {
            foreach (var side in TileSideExtensions.All)
            {
                var neighbor = side.Offset(tile);
                if (!Cells.TryGetValue(neighbor, out var neighborCell) || !neighborCell.HasFloor)
                {
                    region.LeaksToVacuum = true;
                    return;
                }
            }
        }
        region.LeaksToVacuum = false;
    }
}
