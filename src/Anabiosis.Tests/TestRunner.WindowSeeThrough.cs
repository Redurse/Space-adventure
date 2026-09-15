using System.Collections.Generic;
using System.Linq;
using Anabiosis.Client.Rendering;
using Anabiosis.Server;
using Anabiosis.Shared.Model;

// Direct user bug report ("не вижу ничего через стену являющейся иллюминатором") - end-to-end
// proof that a Window wall material actually reaches the CLIENT's own tile grid (not just the
// materialByTile tint dictionary DrawShipWalls already reads) so TileOccluders' Window exception
// has something real to act on, for the common case (a Window on a plain Room-rect boundary, backed
// by a real WallBlock - not one of the T-junction/half-block-notch residue tiles covered by
// World_HalfBlockNotch_ClientRenderMatchesServerCollisionAtEveryNotchTile instead).
internal static partial class TestRunner
{
    private static CustomShipDefinition BuildSimpleCustomShipDefinitionWithWindow() =>
        // Sets WallMaterials (the derived property) directly, not WallMaterialsRaw - a record's
        // `with` copies property VALUES, it doesn't re-run WallMaterials' own constructor-time
        // initializer off a re-assigned Raw parameter.
        BuildSimpleCustomShipDefinition() with
        {
            WallMaterials = new List<CustomWallMaterialDef> { new(0, 1, WallMaterial.Window) },
        };

    private static bool World_CustomShipWithWindow_ClientTilesTreatItAsNonOccluding()
    {
        var world = new World(ShipKind.Custom, BuildSimpleCustomShipDefinitionWithWindow());
        var snapshot = world.CreateSnapshot();

        // Setup check: the server's own WallBlock at (0,1.5) really did pick up the Window
        // material - Ship.Tiles itself never carries WallMaterial at all (ApplyWallMaterials only
        // ever writes it onto the WallBlock list, never onto the tile grid directly - occlusion is
        // a purely client-side concern, so the server-side grid was never meant to know about it).
        var windowTile = new TileCoord(0, 1);
        if (world.Ship.WallBlocks.FirstOrDefault(b => b.X == 0f && b.Y == 1.5f)?.Material != WallMaterial.Window)
            return false;

        var clientTiles = ClientTileGrid.Build(snapshot);
        if (clientTiles.CellAt(windowTile)?.WallMaterial != WallMaterial.Window)
            return false; // the bug: material never reached the client's own grid at all

        var segments = TileOccluders.Build(clientTiles, new List<SightGap>());
        // (0,1)'s West face sits on the vertical line x=0, spanning y=[1,2) - must be gone, while
        // its Standard neighbor right below it, (0,2), on the same line, must still occlude.
        return !AnyVerticalCovers(segments, 0f, 1.5f) && AnyVerticalCovers(segments, 0f, 2.5f);
    }
}
