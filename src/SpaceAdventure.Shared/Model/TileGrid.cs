namespace SpaceAdventure.Shared.Model;

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
    public string? DeviceId;    // at most one device per cell; occupies the floor slot, blocks movement
    public string? TerminalId;  // at most one terminal per cell; does NOT occupy the floor slot, does NOT block movement
    public TileSide? TerminalWallSide; // which neighbor direction must carry a wall for the terminal to mount against
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

    public TileCell? CellAt(TileCoord coord) => Cells.TryGetValue(coord, out var cell) ? cell : null;

    public int? RegionIdAt(TileCoord coord) => RegionIdOf.TryGetValue(coord, out var id) ? id : null;

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
        return cell.Wall switch
        {
            TileWallKind.None => true,
            TileWallKind.Door => cell.DoorOpen && cell.WallHp > 0 || cell.WallHp <= 0,
            TileWallKind.Solid => cell.WallHp <= 0,
            _ => false,
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
    public void SetWall(TileCoord coord, TileWallKind kind, float hp = 100f)
    {
        if (!Cells.TryGetValue(coord, out var cell) || !cell.HasFloor)
            throw new InvalidOperationException($"Cannot place a wall at {coord} without a floor there first.");

        var wasMember = IsRegionMember(cell);
        cell.Wall = kind;
        cell.WallHp = kind == TileWallKind.None ? 0f : hp;
        if (kind != TileWallKind.Door)
            cell.DoorOpen = false;
        var isMemberNow = IsRegionMember(cell);

        if (wasMember && !isMemberNow)
            OnTileLeftRegionMembership(coord);
        else if (!wasMember && isMemberNow)
            OnTileBecameRegionMember(coord);
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
            cell.DeviceId = null;
    }

    public void PlaceTerminal(TileCoord coord, TileSide wallSide, string terminalId)
    {
        if (!Cells.TryGetValue(coord, out var cell) || !cell.HasFloor)
            throw new InvalidOperationException($"Cannot place a terminal at {coord} without a floor there first.");
        var neighbor = wallSide.Offset(coord);
        if (!Cells.TryGetValue(neighbor, out var neighborCell) || neighborCell.Wall == TileWallKind.None)
            throw new InvalidOperationException($"Cannot place a terminal at {coord} facing {wallSide} - no wall there to mount against.");
        cell.TerminalId = terminalId;
        cell.TerminalWallSide = wallSide;
    }

    public void RemoveTerminal(TileCoord coord)
    {
        if (!Cells.TryGetValue(coord, out var cell))
            return;
        cell.TerminalId = null;
        cell.TerminalWallSide = null;
    }

    private IEnumerable<TileCoord> Neighbors(TileCoord coord)
    {
        foreach (var side in TileSideExtensions.All)
            yield return side.Offset(coord);
    }

    // A floor tile just became an open (non-walled) member of the region graph - either it's brand
    // new, or a wall/door on it was removed/breached. Union it with every neighboring region it
    // touches (there can be more than one, if it reconnects two previously-separate pockets).
    private void OnTileBecameRegionMember(TileCoord coord)
    {
        var neighborRegionIds = new HashSet<int>();
        foreach (var neighbor in Neighbors(coord))
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
            foreach (var otherId in neighborRegionIds)
            {
                if (otherId == survivorId)
                    continue;
                var other = Regions[otherId];
                foreach (var tile in other.Tiles)
                {
                    survivor.Tiles.Add(tile);
                    RegionIdOf[tile] = survivorId;
                }
                Regions.Remove(otherId);
            }
        }

        RecomputeLeak(survivorId);
        // Merging can also change whether NEIGHBORING regions still leak (a tile that used to be a
        // dead end bordering vacuum might now be interior) - but only tiles adjacent to the changed
        // one could possibly be affected, so just refresh this one region; the moved-in tiles came
        // from regions that no longer exist, and their leak status is superseded by the survivor's.
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

        var remaining = new HashSet<TileCoord>(oldRegion.Tiles);
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
                foreach (var neighbor in Neighbors(current))
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
            // Still one connected piece (removing this tile didn't actually disconnect anything) -
            // keep the same region id, its Tiles set is already correct from the removal above.
            RecomputeLeak(oldRegionId);
            return;
        }

        Regions.Remove(oldRegionId);
        foreach (var piece in pieces)
        {
            var region = new SealedRegion { Id = _nextRegionId++, Tiles = piece };
            Regions[region.Id] = region;
            foreach (var tile in piece)
                RegionIdOf[tile] = region.Id;
            RecomputeLeak(region.Id);
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
