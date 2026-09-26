namespace Anabiosis.Shared.Model;

// Direct user request (screenshot: wall tiles arranged in the shape a turret mount should have -
// "в сумме форм всех стен так должно выглядеть устройство... реальные такие границы при установке
// в редакторе и при наведении мышкой в игре") - every turret kind's own real tile reservation
// shrinks from a plain 3x3 square (CustomDeviceFootprint.Size) to a single 1-wide, 3-tall column
// (the gun's own mount, where a crew member actually stands at Turret.PeriscopePosition - nothing
// to do with TurretMount.cs's own OUTWARD firing mount, an entirely separate, hull-edge-derived
// concept this never touches); the two former side columns become a real mount "skirt" - a solid
// corner block top and bottom, a half-block armor plate in the middle (its solid half facing
// outward, open half facing the gun, matching a player being able to walk right up beside the
// mount) - built from ORDINARY wall tiles (TileGrid.SetWall/SetWallOpenSide), the exact same
// primitives the free-tile editor's own Wall tool already uses, so the half-block strip interacts
// with an adjacent player-painted half-block wall exactly the way any other half-block wall would
// (TileGrid.SetWallOpenSide has no neighbor-aware rule at all to begin with - nothing was ever
// blocking that).
//
// Single source of truth, called from two places that both need to agree on the exact same shape:
// Game1.ShipEditor.cs (stamps the skirt into the live editor canvas immediately at placement time,
// so the border is visible while designing, not just after a build) and Ship.Custom.cs (derives the
// skirt fresh from every turret's own real position on EVERY Ship build - editor-drawn hull, saved
// custom ship, the frozen default hull, or a CompartmentCatalog room alike - rather than storing it
// as one-shot data that a later build/demolish round-trip could silently drop, the same fragility
// CustomShipDefinition.SupplementalWallTiles/WallOpenSides already warn about for OTHER one-shot
// tile corrections).
public static class TurretMountSkirt
{
    // A skirt tile's own position (anchor-relative in Game1.ShipEditor.cs's tile-index space,
    // absolute wherever Ship.Custom.cs consumes this) plus which kind of wall it is - a corner block
    // (OpenSide null) or a half-block armor plate (OpenSide names the SOLID side, TileCell.
    // WallOpenSide's own convention).
    public readonly record struct SkirtTile(TileCoord Position, TileSide? OpenSide);

    // `anchor` is the device's own top-left footprint tile (same convention CustomDeviceFootprint/
    // Game1.ShipEditor.cs's own _editorDeviceFootprint already use), `rotated` is the device's own
    // Rotated flag (unrotated: 1 wide x 3 tall, gun column at anchor.X; rotated: 3 wide x 1 tall,
    // gun row at anchor.Y - CustomDeviceFootprint.Size(TurretKind) is (1,3), swapped to (3,1) when
    // Rotated, same generic convention every other rotatable kind already follows).
    public static IEnumerable<SkirtTile> SkirtTiles(TileCoord anchor, bool rotated)
    {
        if (!rotated)
        {
            yield return new SkirtTile(new TileCoord(anchor.X - 1, anchor.Y), null);
            yield return new SkirtTile(new TileCoord(anchor.X - 1, anchor.Y + 1), TileSide.West);
            yield return new SkirtTile(new TileCoord(anchor.X - 1, anchor.Y + 2), null);
            yield return new SkirtTile(new TileCoord(anchor.X + 1, anchor.Y), null);
            yield return new SkirtTile(new TileCoord(anchor.X + 1, anchor.Y + 1), TileSide.East);
            yield return new SkirtTile(new TileCoord(anchor.X + 1, anchor.Y + 2), null);
        }
        else
        {
            yield return new SkirtTile(new TileCoord(anchor.X, anchor.Y - 1), null);
            yield return new SkirtTile(new TileCoord(anchor.X + 1, anchor.Y - 1), TileSide.North);
            yield return new SkirtTile(new TileCoord(anchor.X + 2, anchor.Y - 1), null);
            yield return new SkirtTile(new TileCoord(anchor.X, anchor.Y + 1), null);
            yield return new SkirtTile(new TileCoord(anchor.X + 1, anchor.Y + 1), TileSide.South);
            yield return new SkirtTile(new TileCoord(anchor.X + 2, anchor.Y + 1), null);
        }
    }

    // Ship.Custom.cs only has each turret's own CENTER position (device.X/Y, the convention every
    // rendered device uses - ShipRenderer.GetBlockRect treats its own `worldPosition` as a center,
    // not a corner) plus its Rotated flag - recovers the same top-left anchor TileShipBuilder.cs's
    // own device-export step originally computed it FROM (coord + footprintWidth/2f), so
    // SkirtTiles above sees the exact tile-index frame it was designed in.
    public static TileCoord AnchorFromCenter(float centerX, float centerY, bool rotated)
    {
        var (width, height) = CustomDeviceFootprint.Size(CustomDeviceKind.TurretBallistic);
        if (rotated)
            (width, height) = (height, width);
        return new TileCoord((int)MathF.Round(centerX - width / 2f), (int)MathF.Round(centerY - height / 2f));
    }
}
