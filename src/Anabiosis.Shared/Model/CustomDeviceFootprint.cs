namespace Anabiosis.Shared.Model;

// How many tiles a catalog device occupies in the tile-model ship editor/builder - 1x1 for most
// kinds, a real 4x4-tile footprint for the Reactor (ShipRenderer.ReactorBlockSize). Shared between
// Game1.ShipEditor.cs (the editor's own placement/removal) and TileShipBuilder.cs (converting a
// saved tile grid into a CustomShipDefinition) - both used to keep their own identical copy of this
// one mapping, the same "must match" drift risk InteractionConstants/ScannerConstants already fixed
// elsewhere.
//
// RULEBOOK for the "half-width" mechanic (Helm/Navigation originally, generalized since to
// ShipStatusMonitor/CommsConsole/Fabricator/Deconstructor/Junction - IsHalfWidthKind below) - the
// individual methods below and their own call sites each carry the specific reasoning for their
// own piece; this is the map between them, for whoever next needs to touch this feature:
//   - Size() still returns a plain 2-whole-tile bounding box (never a fractional TileCoord) - the
//     REAL collision is only ever 1.5 tiles, computed elsewhere (below), not by shrinking Size().
//   - IsHalfWidthKind() says which kinds opt in; HalfSide (a TileSide) says which of the anchor's
//     4 neighbors is genuinely the "half" one for a given placement - IsTileOfHalfWidthFootprint
//     turns that into "is THIS specific TileCoord the half one, for THIS anchor+side".
//   - HalfOpenSideForHalfWidthDevice() says which half of that neighbor tile the device's own body
//     actually occupies (always facing the anchor, so the two halves sit flush with no gap/seam).
//   - TileGrid.CanPlaceHalfWidthDevice/PlaceHalfWidthDevice (Shared/Model/TileGrid.cs) are the
//     actual per-TILE precondition/mutation - bare floor, OR an already-compatible half-block wall
//     the device then coexists with (never destroys) - by far the most heavily tested piece
//     (TestRunner.HelmNavigationConsole.cs's own (a)-(n) walkthrough covers nearly every angle:
//     standalone floor, matching/mismatched wall axis, wall+device coexistence, removal, a full
//     TileShipBuilder->Ship round trip, and the frozen-default-hull backward-compat guarantee).
//   - Game1.ShipEditor.cs's CanPlaceDeviceFootprint/PlaceDeviceFootprint are the EDITOR's own
//     per-FOOTPRINT (both tiles at once) wrapper around the two points above - not separately unit
//     tested (a Game1 instance needs a real GraphicsDevice), but thin enough that the TileGrid-level
//     tests above are the real coverage for its own logic.
//   - TWO half-width devices placed near each other, where the second one's own half would land on a
//     tile the FIRST one already claims (TestRunner.HelmNavigationConsole.cs's own TileGrid_
//     CanPlaceHalfWidthDevice_RejectsTileAlreadyClaimedByNeighborsHalf/_AllowsOppositeSide/
//     _RejectsPerpendicularSide) - CanPlaceHalfWidthDevice's second branch (below the `cell.DeviceId
//     is null` split) allows a SECOND device to claim this same tile's remaining half, but ONLY the
//     exact opposite side of the same axis (TileCell.DeviceId2's own doc comment has the geometry:
//     a perpendicular pairing would overlap in one shared corner and is still refused, same as the
//     same-side case always was). Direct user request (screenshot - two mirrored device racks each
//     reaching a half tile into one shared middle column). The VISUAL result before this existed was
//     easy to misread as a bug even for the still-refused cases: only the neighbor's own BLOCKED
//     half gets its baked icon drawn over it (DrawEditorDeviceAt/ShipRenderer.Devices.cs), so the
//     neighbor's WALKABLE half looks like plain bare floor even though its own TileCell is claimed -
//     see spaceadventure-halfwidth-device-collision-ux (project memory) for the real bug report this
//     traces back to, and Game1.ShipEditor.cs's DeviceRejectionToastMessage for the player-facing
//     fix (a clear reason on a rejected click, replacing what used to be silence).
//
// SEPARATE mechanic, same underlying per-tile primitive: IsShapedFootprintKind/ShapedFootprint/
// RotateShapedFootprint below describe a device (so far just the 4 turret kinds) whose real
// footprint is a whole per-tile SHAPE (some tiles full, some half on a specific side, some not part
// of the footprint at all) rather than one uniform rectangle with at most one shared half axis -
// still built entirely from the SAME TileGrid.PlaceDevice/PlaceHalfWidthDevice primitives above,
// just driven by a per-tile plan (Game1.ShipEditor.cs's own BuildFootprintPlan) instead of a single
// footprint+halfSide pair.
public static class CustomDeviceFootprint
{
    public static (int Width, int Height) Size(CustomDeviceKind kind) => kind switch
    {
        CustomDeviceKind.Reactor => (4, 4),
        // Direct user request (confirmed geometry via 2 rounds of clarifying questions, doubly-
        // confirmed - this feature had been implemented twice before and rejected twice) - the
        // ANCHOR-RELATIVE bounding box still spans 2 whole-tile coordinates on each axis (there's no
        // fractional TileCoord), but the console's REAL collision/reservation is genuinely only 1.5
        // tiles along whichever axis is the halved one: the second tile along that axis is only
        // half-claimed (TileGrid.PlaceHalfWidthDevice sets DeviceOpenSide on it), not a second
        // ordinary fully-blocked device tile the way it used to be. See TileGrid.IsWalkable's own
        // combined truth table for the actual walkable/blocked area - this Size() value is now purely
        // "how many TileCoord cells the footprint touches", not "how much of them are actually solid"
        // (VisualSize, which used to carry that distinction separately as a purely-cosmetic drawn box
        // centered inside an oversized reservation, is gone - rendering now reads the same real
        // geometry directly, see ShipRenderer.Devices.cs/Game1.ShipEditor.Draw.cs).
        CustomDeviceKind.Helm => (2, 2),
        CustomDeviceKind.Navigation => (2, 2),
        // Direct user request ("размером с навигационную панель") - same half-width console
        // footprint as Navigation/Helm above, just a different pair of kinds.
        CustomDeviceKind.ShipStatusMonitor => (2, 2),
        CustomDeviceKind.CommsConsole => (2, 2),
        // Direct user request ("сделай чтобы он занимал размер полтора на 1 блок, как делались все
        // новые блоки") - same half-width mechanic as Helm/Navigation above, just a 1-tall "full"
        // axis instead of 2 - the Junction ("Щиток") fixture is flatter than a console.
        CustomDeviceKind.Junction => (2, 1),
        // Direct user request - real footprints for the "производство" tab's own workbenches
        // (previously all (1,1), same placeholder every other not-yet-functional kind still has).
        CustomDeviceKind.ConstructionBench => (2, 3),
        // Direct user request ("сделай по аналогии фабрикатор и деконструктор, только чтобы они
        // занимали полтора на 3 клетки") - same half-width mechanic as Helm/Navigation above, just a
        // 3-tall "full" axis instead of 2-tall: the anchor column is ordinary floor, the second
        // column is only half-claimed (IsHalfWidthKind marks both kinds as eligible for that
        // mechanic, generalized from what used to be Helm/Navigation-only code).
        CustomDeviceKind.Fabricator => (2, 3),
        CustomDeviceKind.Deconstructor => (2, 3),
        CustomDeviceKind.WeaponWorkbench => (2, 4),
        // Direct user request (screenshot of a turret mount built out of wall tiles - "в сумме форм
        // всех стен так должно выглядеть устройство") - the gun's own real reservation is a single
        // column, 1 wide x 3 tall: the flanking corner-block/half-block "skirt" either side of it
        // (TurretMountSkirt.cs's own doc comment) is built from real wall tiles, not folded into this
        // Size itself - Size only ever describes the DEVICE's own claimed tiles, same as every other
        // kind here.
        // Direct user request (exact shape decoded from their own legend: 0 empty, 1 half-right,
        // 2 half-top, 3 half-left, 4 half-bottom, 5 full) - was (1, 3) (a bare 1-wide gun column,
        // with a separate wall-tile "skirt" flanking it, TurretMountSkirt.cs, since deleted). The
        // skirt is folded directly into the device's own footprint now - see IsShapedFootprintKind/
        // ShapedFootprint below for the actual per-tile shape this (4, 3) bounding box only outlines.
        CustomDeviceKind.TurretBallistic => (4, 3),
        CustomDeviceKind.TurretLaser => (4, 3),
        CustomDeviceKind.TurretMachineGun => (4, 3),
        CustomDeviceKind.DefensiveTurret => (4, 3),
        // Direct user request - a bed reads as furniture you lie down IN, not a single tile.
        CustomDeviceKind.Bed => (1, 2),
        // Direct user request - a shuttle hangar is a genuinely large bay.
        CustomDeviceKind.ShuttleHangar => (5, 6),
        // Direct user request ("тройная дверь") - spans 3 tiles like a real wide doorway; rotation
        // (R, already free for every device kind) gives the 3x1 orientation with no extra code.
        CustomDeviceKind.TripleDoor => (1, 3),
        _ => (1, 1),
    };

    // Direct user bug report ("при повороте они не поворачиваются на все 4 стороны") - Helm/
    // Navigation's half tile can sit on any of the 4 sides of the anchor (not just the original
    // East/South convention), so the old single `Rotated` bool (which only ever picked between those
    // two) isn't enough to describe it on its own. `stored` is the new, explicit HalfSide - present on
    // anything saved after this fix. `rotated` is the OLD flag, kept as the fallback for every save
    // from before HalfSide existed (hand-authored hulls included) - reproduces exactly their old
    // behaviour (false -> East, true -> South) so nothing already saved changes shape.
    public static TileSide ResolveHalfSide(TileSide? stored, bool rotated) => stored ?? (rotated ? TileSide.South : TileSide.East);

    // Every kind whose footprint's own halved axis (always the Width component of Size() above,
    // unrotated) is genuinely 1.5 tiles rather than a full 2 - Helm/Navigation originally, now also
    // Fabricator/Deconstructor (direct user request, "по аналогии... полтора на 3"). The OTHER axis
    // (Size().Height, unrotated) is what actually varies per kind - 2 for Helm/Navigation, 3 for
    // Fabricator/Deconstructor - everything downstream (Game1.ShipEditor.cs/.Draw.cs,
    // ShipRenderer.Devices.cs) reads that straight from Size() rather than hardcoding it.
    public static bool IsHalfWidthKind(CustomDeviceKind kind) =>
        kind is CustomDeviceKind.Helm or CustomDeviceKind.Navigation or CustomDeviceKind.Fabricator or CustomDeviceKind.Deconstructor
            or CustomDeviceKind.ShipStatusMonitor or CustomDeviceKind.CommsConsole or CustomDeviceKind.Junction;

    // Direct user bug report ("при повороте они не поворачиваются на все 4 стороны") - within a
    // half-width kind's anchor-relative footprint, `halfSide` names which side of the anchor its
    // "half" tile sits on (East/South put it at anchor+1 along that axis - the original, only-ever-
    // reachable convention before this fix; West/North put it AT the anchor's own coordinate
    // instead, with the "full" tile now the one at +1). Only ever checks the halved axis's own
    // column/row equality, so it's unaffected by how long the OTHER axis is - 2 tiles for Helm/
    // Navigation, 3 for Fabricator/Deconstructor. Moved here from Game1.ShipEditor.cs (its own
    // former private copy) so CompartmentPlacer.cs can share the exact same rule instead of risking
    // a second copy drifting out of sync - the same "one true definition" reasoning
    // TileGrid.IsRecessedMountableAnySide already gets for its own two call sites.
    public static bool IsHalfTileOfHalfWidthFootprint(TileCoord coord, TileCoord anchor, TileSide halfSide) => halfSide switch
    {
        TileSide.East => coord.X == anchor.X + 1,
        TileSide.West => coord.X == anchor.X,
        TileSide.South => coord.Y == anchor.Y + 1,
        _ => coord.Y == anchor.Y, // North
    };

    // Direct user bug report ("они должны быть вплотную это раз, а во вторых само устройство должно
    // быть таких размеров а не состоять из двух элементов") - the half tile's own DeviceOpenSide
    // (the side TileGrid.IsWalkable treats as blocked) must face the anchor/full tile, not away
    // from it - an earlier version used the far side, leaving a walkable gap (and a visible seam in
    // the art) between the two halves instead of one flush object. The full tile is always the side
    // OPPOSITE the half tile, regardless of which of the 4 it is.
    public static TileSide HalfOpenSideForHalfWidthDevice(TileSide halfSide) => halfSide.Opposite();

    // Direct user request (turret kinds' new (4, 3) footprint, decoded from their own legend: 0
    // empty, 1 half-right(East), 2 half-top(North), 3 half-left(West), 4 half-bottom(South), 5
    // full) - a SHAPED footprint has its own per-tile reservation instead of one uniform rectangle:
    // Offset is anchor-relative (matching Size(kind)'s own unrotated Width/Height), HalfSide null
    // means the tile is fully reserved (ordinary TileGrid.PlaceDevice), a HalfSide value means only
    // that side of the tile is reserved (TileGrid.PlaceHalfWidthDevice/CanPlaceHalfWidthDevice - the
    // SAME per-tile mechanism two independently-placed half-width DEVICES already share, see
    // TileCell.DeviceId2's own doc comment - nothing new needed there for this). Distinct from
    // IsHalfWidthKind (which only ever describes ONE shared half-tile axis for the whole device,
    // e.g. Helm/Navigation) - a shaped kind can have any number of independently-sided half tiles,
    // and tiles the shape omits entirely (the old rectangle's corners) aren't part of the footprint
    // at ALL - nothing is reserved there, exactly like open floor.
    public readonly record struct ShapedFootprintTile(TileCoord Offset, TileSide? HalfSide);

    // Replaces the former (1, 3) gun column + a separately-stamped wall-tile "skirt" flanking it
    // (TurretMountSkirt.cs, deleted) - the skirt is now simply part of this one footprint instead of
    // a second, independently-placed/removed set of tiles. Kept to a single hand-authored list (the
    // UNROTATED orientation only) - RotateShapedFootprint below derives the rotated version from
    // this alone, so there is exactly one copy of the actual shape to keep in sync when it changes.
    public static bool IsShapedFootprintKind(CustomDeviceKind kind) => kind is
        CustomDeviceKind.TurretBallistic or CustomDeviceKind.TurretLaser
        or CustomDeviceKind.TurretMachineGun or CustomDeviceKind.DefensiveTurret;

    private static readonly ShapedFootprintTile[] TurretShape =
    {
        new(new TileCoord(1, 0), TileSide.South),
        new(new TileCoord(2, 0), TileSide.South),
        new(new TileCoord(0, 1), TileSide.East),
        new(new TileCoord(1, 1), null),
        new(new TileCoord(2, 1), null),
        new(new TileCoord(3, 1), TileSide.West),
        new(new TileCoord(0, 2), TileSide.East),
        new(new TileCoord(3, 2), TileSide.West),
    };

    // The UNROTATED shape for `kind` - null for every kind IsShapedFootprintKind doesn't cover, so a
    // caller can use this alone to decide which footprint model applies (shaped if non-null, the
    // ordinary rectangle/IsHalfWidthKind model otherwise).
    public static IReadOnlyList<ShapedFootprintTile>? ShapedFootprint(CustomDeviceKind kind) =>
        IsShapedFootprintKind(kind) ? TurretShape : null;

    // Rotates a shaped footprint 90 degrees, matching DeviceFootprintSize's own convention for every
    // OTHER rotatable kind (Size().Height/Width swap) - transposes each offset (dx, dy) -> (dy, dx)
    // and relabels each HalfSide the same way a compass direction's own (x, y) unit vector would
    // transpose (West=(-1,0)->(0,-1)=North, East=(1,0)->(0,1)=South, and the reverse for North/
    // South) - the exact transform TurretMountSkirt.cs's own former hand-written rotated branch
    // already established for this same device family, just expressed generically here instead of
    // as a second hand-authored list that could drift out of sync with the unrotated one.
    // A shaped footprint's orientation as the direction its authored top points: North is the shape exactly
    // as written (the narrow barrel side up), East/South/West are that shape turned 90/180/270 degrees
    // clockwise. A save that only knows the older two-state Rotated flag means North or, rotated, West
    // (the transpose RotateShapedFootprint below, which for the mirror-symmetric turret shape is the same
    // picture as a quarter turn counter-clockwise).
    public static TileSide ShapedFacing(bool rotated, TileSide? stored) => stored ?? (rotated ? TileSide.West : TileSide.North);

    public static IReadOnlyList<ShapedFootprintTile>? ShapedFootprint(CustomDeviceKind kind, TileSide facing)
    {
        if (ShapedFootprint(kind) is not { } shape)
            return null;
        var steps = facing switch { TileSide.East => 1, TileSide.South => 2, TileSide.West => 3, _ => 0 };
        var (width, height) = Size(kind);
        var current = shape;
        for (var i = 0; i < steps; i++)
        {
            // 90 degrees clockwise in a width x height box: (x, y) -> (height - 1 - y, x); the box becomes height x width.
            current = current.Select(t => new ShapedFootprintTile(
                new TileCoord(height - 1 - t.Offset.Y, t.Offset.X),
                t.HalfSide switch
                {
                    TileSide.North => TileSide.East,
                    TileSide.East => TileSide.South,
                    TileSide.South => TileSide.West,
                    TileSide.West => TileSide.North,
                    _ => (TileSide?)null,
                })).ToList();
            (width, height) = (height, width);
        }
        return current;
    }

    public static IReadOnlyList<ShapedFootprintTile> RotateShapedFootprint(IReadOnlyList<ShapedFootprintTile> shape) =>
        shape.Select(t => new ShapedFootprintTile(
            new TileCoord(t.Offset.Y, t.Offset.X),
            t.HalfSide switch
            {
                TileSide.West => TileSide.North,
                TileSide.East => TileSide.South,
                TileSide.North => TileSide.West,
                TileSide.South => TileSide.East,
                null => null,
                _ => throw new ArgumentOutOfRangeException(nameof(shape)),
            })).ToList();
}
