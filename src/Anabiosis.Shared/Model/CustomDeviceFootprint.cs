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
//   - The one gap those tile-level tests can't see on their own: TWO half-width devices placed near
//     each other, where the second one's own half would land on a tile the FIRST one already claims
//     (TestRunner.HelmNavigationConsole.cs's own TileGrid_CanPlaceHalfWidthDevice_
//     RejectsTileAlreadyClaimedByNeighborsHalf) - CanPlaceHalfWidthDevice's own very first check
//     (DeviceId: null) already refuses this correctly, but the VISUAL result is easy to misread as a
//     bug: only the neighbor's own BLOCKED half gets its baked icon drawn over it
//     (DrawEditorDeviceAt/ShipRenderer.Devices.cs), so the neighbor's WALKABLE half looks like plain
//     bare floor even though its own TileCell is fully claimed - see
//     spaceadventure-halfwidth-device-collision-ux (project memory) for the real bug report this
//     traces back to, and Game1.ShipEditor.cs's DeviceRejectionToastMessage for the player-facing
//     fix (a clear reason on a rejected click, replacing what used to be silence).
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
        CustomDeviceKind.TurretBallistic => (1, 3),
        CustomDeviceKind.TurretLaser => (1, 3),
        CustomDeviceKind.TurretMachineGun => (1, 3),
        CustomDeviceKind.DefensiveTurret => (1, 3),
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
}
