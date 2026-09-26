using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Room floor/wall/hull rendering, the tile grid overlay, wall-tool/door-tool target bars and the pending-room-build/placement overlays - split out of ShipRenderer.cs to keep that file to its own topic.
public sealed partial class ShipRenderer
{
    // Tint scales with how low the room's oxygen actually is (game_design.md section 1 —
    // Barotrauma-style atmosphere) rather than a flat breached/not-breached flag: a single
    // holding-steady breach barely shows, a room actually suffocating goes visibly red. The floor
    // gets a paneled-grating pattern instead of a flat fill (this project has no image assets, so
    // the "texture" is drawn as a grid of seams rather than an actual sprite).
    // accentOverride: station rooms pass their own warm "commercial" accent here instead of the
    // per-room-id lookup below (StationRenderer.Draw) - everything else about the compartment
    // (grating, oxygen tint, name plate) stays exactly as it is on the ship, only the accent-tinted
    // decor (deck markings, light pool, wall lamps, corner fillets) shifts, which is enough for the
    // station to read as a different kind of place without forking this whole method.
    // The floor normal map, drawn into ScenePost's normals target with exactly the geometry and
    // tile size DrawRoomFloor uses for the visible plate below, so the two line up texel for
    // texel. Floors only: they are most of the lit surface on screen, and unlike the walls their
    // geometry is one rectangle per room that GetRoomRect already hands out - the walls would
    // mean duplicating the band layout and then keeping the duplicate in step with it.
    internal void DrawFloorNormals(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        foreach (var room in snapshot.Rooms)
            TileTextures.DrawTiled(spriteBatch, _floorNormals, TileTextures.FloorTileSize, GetRoomRect(room, origin), Color.White);
    }

    // The hull's own true normals, stamped into the same target right alongside the floor's -
    // same room rect HullSkin.Draw already tiles the visible plate texture across (its own
    // RoomRect, identical formula to GetRoomRect here), so the normal map lines up texel for
    // texel with what's actually on screen. The cut-corner plate polygon HullSkin fills is not
    // replicated here on purpose: a normal map is read-only input to the lighting pass, not
    // something a player can see the silhouette of directly, so the handful of stray normal
    // texels just past the true corner cut (covered by the plate edge stroke/interior geometry
    // anyway) cost nothing to leave in, the same tradeoff HullSkin's own albedo tiling already
    // makes at line 62 of HullSkin.cs.
    internal void DrawHullNormals(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        foreach (var room in snapshot.Rooms)
            TileTextures.DrawTiled(spriteBatch, _hullNormals, TileTextures.HullTileSize, GetRoomRect(room, origin), Color.White);
    }

    internal void DrawRoomFloor(SpriteBatch spriteBatch, Room room, float oxygen, Vector2 origin, Color? accentOverride = null)
    {
        var rect = GetRoomRect(room, origin);
        var accent = accentOverride ?? RoomDecor.Accent(room.Id, room.Name);

        // Content-каталог отсеков - a catalog room with real reference art draws that instead of
        // the procedural stack below: the whole floor/walls/equipment are already baked into the
        // one image. Falls through to the ordinary procedural room for every hand-authored hull
        // (room.Name never matches a catalog entry there) and for the 2 plain "empty" catalog
        // shells, which never got reference art.
        if (!RoomDecor.TryDrawCatalogTexture(spriteBatch, rect, room.Name))
        {
            // Plates rather than one repeated stamp, and the seams are cut into them rather than
            // drawn over the top - which is why DrawFloorGrating is gone: its hairline grid had no
            // depth, so it read as printed on. The gunnery and reactor compartments get their own
            // field inside the same frame, so they are recognisably different rooms on recognisably
            // the same ship.
            //
            // Indexed from the ship's own origin, so which plate lands where belongs to the ship and
            // the pattern does not crawl across the deck when the camera moves.
            DeckPlates.DrawTiled(spriteBatch, _deckPlates[DeckPlates.For(room.Id)], rect, Color.White,
                new Point((int)origin.X, (int)origin.Y));
            // Dirt across the plates, which is the one thing that hides the tile grid - nothing
            // painted inside a tile can, because it repeats with the tile.
            DeckPlates.DrawGrime(spriteBatch, _deckGrime, rect, room.Id);
            RoomDecor.DrawLightPool(spriteBatch, _pixel, rect, accent);
            RoomDecor.DrawFurniture(spriteBatch, _pixel, rect, room.Id, accent);
        }

        var deficit = Math.Clamp((100f - oxygen) / 100f, 0f, 1f);
        if (deficit > 0f)
            spriteBatch.Draw(_pixel, rect, Color.Red * (deficit * 0.5f));

        // Compartment name on a painted plate in the department's own colour, the way a bulkhead is
        // actually stencilled - a bare label floating on the deck reads as a debug overlay.
        //
        // Sized from the font's own real measurement, not a per-character guess (the old
        // `34 + room.Name.Length * 9` under- or over-shot depending on the actual glyph widths) - a
        // narrow room with a long name used to spill text straight past its own plate into whatever
        // drew next (a wall, the neighbouring room), leaving only a fragment of the name legible.
        // If it still doesn't fit even at the smallest useful size, the name shrinks to match instead
        // of overflowing - a slightly smaller label beats a truncated-looking one.
        var nameScale = 0.7f;
        var textSize = _font.MeasureString(room.Name) * nameScale;
        var maxTextWidth = rect.Width - 24f;
        if (textSize.X > maxTextWidth && textSize.X > 0f)
        {
            nameScale *= MathHelper.Clamp(maxTextWidth / textSize.X, 0.5f, 1f);
            textSize = _font.MeasureString(room.Name) * nameScale;
        }
        var plate = new Rectangle(rect.X + 8, rect.Y + 8, (int)Math.Min(rect.Width - 16, textSize.X + 20), 20);
        spriteBatch.Draw(_pixel, plate, accent * 0.22f);
        spriteBatch.Draw(_pixel, new Rectangle(plate.X, plate.Y, 3, plate.Height), accent * 0.8f);
        spriteBatch.DrawString(_font, room.Name, new Vector2(rect.X + 14, rect.Y + 10), Color.LightSteelBlue, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);

        var oxygenColor = oxygen >= 50f ? Color.LightSteelBlue : oxygen >= 20f ? Color.Orange : Color.OrangeRed;
        spriteBatch.DrawString(_font, $"O2: {oxygen:0}", new Vector2(rect.X + 10, rect.Y + 30), oxygenColor, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
    }

    // Thick, plated bulkheads rather than a 3px outline: a slab centred on the room's boundary
    // (so two neighbouring rooms share one wall instead of stacking two), with a lit inner edge, a
    // shadowed outer one, ribs every RibSpacing pixels, a service conduit running down the middle
    // and bolted plates over the corners. VisibilityMask raycasts against that same boundary line,
    // so what blocks sight is exactly what's drawn here.
    //
    // Every band carries the same hull-plate tile HullSkin paints on the true exterior of the
    // ship, on every side of every room - not just the sides that happen to face open space. Only
    // texturing the genuinely exterior sides once read as inconsistent (interior corridors kept
    // the plain, flatter wall pattern right next to compartments that got the heavier plate look),
    // so the one texture that actually reads well wins everywhere a wall is drawn.
    // Kept fully intact for BoardingRenderer/StationRenderer (an enemy hull's/station's own rooms
    // aren't part of the client's per-tile grid - ClientTileGrid.Build only ever rasterizes the
    // player's OWN Ship.Rooms/Doors from WorldSnapshot). The player's own ship no
    // longer calls this for its walls (M75, humble-soaring-cat.md) - see DrawRoomWallLamps/
    // DrawShipWalls below and this method's own call site in Draw().
    internal void DrawRoomWalls(SpriteBatch spriteBatch, Room room, float oxygen, Vector2 origin, Color? accentOverride = null)
    {
        var rect = GetRoomRect(room, origin);
        var alarmed = oxygen < 70f;
        var accent = accentOverride ?? RoomDecor.Accent(room.Id, room.Name);
        const int half = WallThickness / 2;

        RoomDecor.DrawWallLamps(spriteBatch, _pixel, rect, accent, alarmed);

        DrawWallBand(spriteBatch, new Rectangle(rect.X - half, rect.Y - half, rect.Width + WallThickness, WallThickness), true, alarmed, origin);
        DrawWallBand(spriteBatch, new Rectangle(rect.X - half, rect.Bottom - half, rect.Width + WallThickness, WallThickness), true, alarmed, origin);
        DrawWallBand(spriteBatch, new Rectangle(rect.X - half, rect.Y - half, WallThickness, rect.Height + WallThickness), false, alarmed, origin);
        DrawWallBand(spriteBatch, new Rectangle(rect.Right - half, rect.Y - half, WallThickness, rect.Height + WallThickness), false, alarmed, origin);

        DrawCornerPlate(spriteBatch, rect.X, rect.Y);
        DrawCornerPlate(spriteBatch, rect.Right, rect.Y);
        DrawCornerPlate(spriteBatch, rect.X, rect.Bottom);
        DrawCornerPlate(spriteBatch, rect.Right, rect.Bottom);
    }

    // M75 - the player's own ship's wall LAMPS only (still per-room decor, unaffected by the tile
    // rework); the actual wall art is now DrawShipWalls below, driven by a real per-tile grid instead
    // of one band per room edge.
    internal void DrawRoomWallLamps(SpriteBatch spriteBatch, Room room, float oxygen, Vector2 origin, Color? accentOverride = null)
    {
        var rect = GetRoomRect(room, origin);
        var alarmed = oxygen < 70f;
        var accent = accentOverride ?? RoomDecor.Accent(room.Id, room.Name);
        RoomDecor.DrawWallLamps(spriteBatch, _pixel, rect, accent, alarmed);
    }

    // M75 (humble-soaring-cat.md) - real per-tile wall rendering: rebuilds the exact same tile shape
    // Ship.Tiles has (ClientTileGrid.Build, a pure function of Rooms/Doors - no new
    // protocol field needed) and draws one tile-sized square per Solid wall cell, oriented by which
    // of its 4 neighbors are also wall-kind (a door counts as "wall" for orientation - same material
    // either side of it). Door tiles themselves are skipped entirely - DrawDoor already draws them,
    // unchanged. Breached walls are also skipped nothing special here either - DrawBreachedWallBlock
    // already punches its own hole/hazard-stripe visual on top of whatever's drawn underneath, at the
    // WallBlock's own position, so it reads correctly over the new art with no changes on its side.
    // Reinforced/Window (direct user request, humble-soaring-cat.md M76 follow-up "варианты стен")
    // reuse the same wall textures, just tinted - no bespoke art exists for either variant yet, same
    // convention the Ship Editor's own canvas/palette already use for it.
    private static Color WallMaterialTint(WallMaterial material) => material switch
    {
        WallMaterial.Reinforced => new Color(150, 155, 165),
        WallMaterial.Window => new Color(150, 215, 235) * 0.75f,
        _ => Color.White,
    };

    // Direct user report ("проблема из-за низкого фпс") - ClientTileGrid.Build (a real rasterization
    // + region flood-fill pass, not a cheap lookup) used to run fresh here AND again in Game1.
    // Lighting.cs's own mask-building AND again in Game1.cs's voice-muffling check - three full
    // rebuilds of the exact same ship every single frame, even though the ship's actual layout only
    // changes on the rare tick a compartment is built/removed. Cached here (the one place already
    // shaped as a per-session-persistent instance every one of those three call sites already holds
    // a reference to), keyed by ClientTileGrid.ComputeStructuralFingerprint - a cheap enough check to
    // run every frame that it beats a full rebuild by a wide margin whenever the layout hasn't
    // actually changed. Door open/closed state (which DOES change often) is deliberately excluded
    // from the fingerprint - ApplyLiveDoorState is re-applied to the cached grid on every call
    // regardless, so that part always reflects the current tick.
    private TileGrid? _cachedShipTiles;
    private int? _cachedShipTilesFingerprint;

    internal TileGrid GetLiveShipTiles(WorldSnapshot snapshot)
    {
        var fingerprint = ClientTileGrid.ComputeStructuralFingerprint(snapshot.Rooms, snapshot.Doors);
        if (_cachedShipTiles is null || _cachedShipTilesFingerprint != fingerprint)
        {
            _cachedShipTiles = TileGridRasterizer.FromRooms(snapshot.Rooms, snapshot.Doors);
            // Direct user bug report ("стены отображаются не на своих местах, а коллизии там же") -
            // a Ship Editor-built hull's own post-rasterization corrections (a T-junction's residual
            // wall, or a half-block notch at a region's edge) live outside Room.Rects entirely, so
            // the naive FromRooms call above can't reproduce them on its own - replay the same three
            // corrections Ship.Custom.cs already applied to the SERVER's Tiles, or this cached copy
            // silently reverts to the wrong geometry the server corrected past. Structural (tied to
            // the same fingerprint as Rooms/Doors/Airlocks), so this only needs to run on a cache
            // rebuild, same as the base rasterization just above it.
            ClientTileGrid.ApplySupplementalTiles(_cachedShipTiles,
                snapshot.SupplementalWallTiles ?? Array.Empty<TileCoord>(),
                snapshot.ForcedFloorTiles ?? Array.Empty<TileCoord>(),
                snapshot.WallOpenSideOverrides ?? Array.Empty<CustomWallOpenSideDef>(),
                snapshot.WallMaterialOverrides ?? Array.Empty<CustomWallMaterialDef>());
            _cachedShipTilesFingerprint = fingerprint;
        }
        ClientTileGrid.ApplyLiveDoorState(_cachedShipTiles, snapshot.Rooms, snapshot.Doors, snapshot.DoorStates);
        ClientTileGrid.ApplyWallOpenSides(_cachedShipTiles, snapshot.Rooms, snapshot.WallBlocks);
        // Direct user bug report ("не вижу ничего через стену являющейся иллюминатором") - the real
        // material has to land on the cell itself (not just the separate materialByTile dictionary
        // DrawShipWalls already reads for tinting) before TileOccluders' own Window exception can see
        // it at all.
        ClientTileGrid.ApplyWallMaterials(_cachedShipTiles, snapshot.Rooms, snapshot.WallBlocks);
        return _cachedShipTiles;
    }

    internal void DrawShipWalls(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        var tiles = GetLiveShipTiles(snapshot);
        // Material lives on the WallBlock itself (WallBlock.cs), not on the tile grid (a pure
        // projection of Rooms/Doors, no WallBlock input - see ClientTileGrid's own
        // doc comment) - matched back to a tile coordinate via the same WallBlockTileCoord mapping
        // World.TileSync.cs already uses server-side, so this can never disagree with which block
        // actually owns that position.
        var materialByTile = snapshot.WallBlocks
            .Where(b => b.Material != WallMaterial.Standard)
            .Select(b => (Coord: TileGridRasterizer.WallBlockTileCoord(b, snapshot.Rooms, snapshot.Rooms.First(r => r.Id == b.RoomId)), b.Material))
            .ToDictionary(x => x.Coord, x => x.Material);
        foreach (var (coord, cell) in tiles.Cells)
        {
            if (cell.Wall != TileWallKind.Solid)
                continue; // None = no wall; Door is drawn separately by the existing DrawDoor calls
            DrawWallTile(spriteBatch, tiles, coord, origin, materialByTile.GetValueOrDefault(coord, WallMaterial.Standard));
        }
    }

    private void DrawWallTile(SpriteBatch spriteBatch, TileGrid tiles, TileCoord coord, Vector2 origin, WallMaterial material = WallMaterial.Standard)
    {
        var tint = WallMaterialTint(material);
        bool HasWall(TileSide side) => tiles.CellAt(side.Offset(coord)) is { Wall: TileWallKind.Solid or TileWallKind.Door };

        var north = HasWall(TileSide.North);
        var south = HasWall(TileSide.South);
        var east = HasWall(TileSide.East);
        var west = HasWall(TileSide.West);

        var unit = (int)PixelsPerUnit;
        var center = origin + new Vector2((coord.X + 0.5f) * PixelsPerUnit, (coord.Y + 0.5f) * PixelsPerUnit);
        var openSide = tiles.CellAt(coord)?.WallOpenSide;

        // Direct user bug report ("у него неправильная текстура и на нём не работает большинство
        // правил связанных со стенами") - a wall tile CAN be "neighborCount==1"/"==3" in the
        // sprite-selection sense below (an end-cap/T-junction by wall-CONNECTIVITY) while still
        // genuinely having exactly one missing-FLOOR neighbor (WallOpenSide != null) - these are two
        // independent classifications (one counts adjacent WALL tiles for art, the other counts
        // adjacent FLOOR for collision), and the corner/end-cap/T-junction sprites below are rotated
        // draws that don't crop cleanly by just resizing their destination rect. Rather than let a
        // half-thick tile keep showing a full, un-thinned sprite (collision and art disagreeing,
        // and a recessed device on it looking stuck inside solid wall art), every one of those three
        // branches falls through to the same plain half-rect fallback the "no art loaded" case
        // already uses whenever WallOpenSide is actually set - only a genuine corner (WallOpenSide
        // null) still gets the dedicated sprite. The vertical/horizontal straight-run textures below
        // are unaffected either way (no rotation - a resized destination rect crops them correctly).
        if (openSide is { } forcedHalf)
        {
            // Direct user request - match the Ship Editor's own look for this half-thick case
            // (Game1.ShipEditor.Draw.cs's DrawEditorWallTile) instead of the detailed hull-plate
            // texture every other wall tile gets: a flat tinted rectangle with a thin outline.
            var forcedRect = HalfRect(center, unit, forcedHalf);
            spriteBatch.Draw(_pixel, forcedRect, tint == Color.White ? new Color(120, 130, 150) : tint);
            DrawRectOutline(spriteBatch, forcedRect, Color.Black, 1);
            return;
        }

        // A T-junction (exactly 3 wall-kind neighbors - the free-form tile editor can produce these
        // even though no rectangular hand-authored hull ever did) has to be checked BEFORE the plain
        // straight-run tests below, since 3 neighbors always include one opposite pair and would
        // otherwise silently read as a plain straight tile, ignoring the third branch entirely.
        var neighborCount = (north ? 1 : 0) + (south ? 1 : 0) + (east ? 1 : 0) + (west ? 1 : 0);
        if (neighborCount == 3 && _wallTJunctionTexture is { } tTex)
        {
            // Base art has the missing/open side facing North (a horizontal run continuing East+West
            // with a spur branching South) - rotate 90° per step clockwise to whichever side is
            // actually the open one here, same convention as the corner/end-cap rotations above.
            var tRotation = !north ? 0f : !east ? MathHelper.PiOver2 : !south ? MathHelper.Pi : -MathHelper.PiOver2;
            var tOrigin = new Vector2(tTex.Width / 2f, tTex.Height / 2f);
            spriteBatch.Draw(tTex, new Rectangle((int)center.X, (int)center.Y, unit, unit), null, tint,
                tRotation, tOrigin, SpriteEffects.None, 0f);
            return;
        }
        if (north && south && _wallVerticalTexture is { } vTex)
        {
            spriteBatch.Draw(vTex, new Rectangle((int)center.X - unit / 2, (int)center.Y - unit / 2, unit, unit), tint);
            return;
        }
        if (east && west && _wallHorizontalTexture is { } hTex)
        {
            spriteBatch.Draw(hTex, new Rectangle((int)center.X - unit / 2, (int)center.Y - unit / 2, unit, unit), tint);
            return;
        }
        // A dead end (exactly one wall-kind neighbor) reads wrong with the corner texture (a "turn"
        // where the wall actually just stops) - direct user report. Base end-cap art connects South,
        // caps at North; rotate the same 90°-per-step clockwise convention the corner uses.
        if (neighborCount == 1 && _wallEndCapTexture is { } capTex)
        {
            var capRotation = south ? 0f : west ? MathHelper.PiOver2 : north ? MathHelper.Pi : -MathHelper.PiOver2;
            var capOrigin = new Vector2(capTex.Width / 2f, capTex.Height / 2f);
            spriteBatch.Draw(capTex, new Rectangle((int)center.X, (int)center.Y, unit, unit), null, tint,
                capRotation, capOrigin, SpriteEffects.None, 0f);
            return;
        }
        if (_wallCornerTexture is { } cTex)
        {
            // Base art turns South-then-East (a room's own top-left corner, per the corner tile's
            // own construction - vertical texture bottom-left, horizontal top-right). Rotate 90° per
            // corner clockwise from there; a fully isolated tile (zero neighbors - vanishingly rare)
            // has no better single answer yet, so it falls back to the same base orientation.
            var rotation = (south, east, west, north) switch
            {
                (true, true, _, _) => 0f,
                (true, _, true, _) => MathHelper.PiOver2,
                (_, _, true, true) => MathHelper.Pi,
                (_, true, _, true) => -MathHelper.PiOver2,
                _ => 0f,
            };
            var texOrigin = new Vector2(cTex.Width / 2f, cTex.Height / 2f);
            spriteBatch.Draw(cTex, new Rectangle((int)center.X, (int)center.Y, unit, unit), null, tint,
                rotation, texOrigin, SpriteEffects.None, 0f);
            return;
        }

        // No new art loaded at all (Content missing) - a single procedural tile-sized square, same
        // material HullSkin/the old wall band used, just without the per-room alarm/conduit overlay
        // that band drawing had (this path is only ever reached if the .mgcb build is broken, so it's
        // not worth threading room/alarm context through a tile loop for it). openSide is always
        // null here - the half-thick case already returned above.
        var fallbackRect = new Rectangle((int)center.X - unit / 2, (int)center.Y - unit / 2, unit, unit);
        TileTextures.DrawSquares(spriteBatch, _hullPlates, TileTextures.HullTileSize, unit, fallbackRect, tint, new Point((int)origin.X, (int)origin.Y));
    }

    // Direct user request ("не угловые клетки занимали только половину блока которая была ближе к
    // космосу") - the rectangle of the tile-sized square centered on `tileCenter` that lies on
    // `half` (North=top, South=bottom, West=left, East=right). Shared by DrawWallTile (the solid
    // half of a half-thick wall) and DrawTerminal (a recessed terminal's own free half, the
    // opposite side).
    private static Rectangle HalfRect(Vector2 tileCenter, int unit, TileSide half) => half switch
    {
        TileSide.North => new Rectangle((int)tileCenter.X - unit / 2, (int)tileCenter.Y - unit / 2, unit, unit / 2),
        TileSide.South => new Rectangle((int)tileCenter.X - unit / 2, (int)tileCenter.Y, unit, unit / 2),
        TileSide.West => new Rectangle((int)tileCenter.X - unit / 2, (int)tileCenter.Y - unit / 2, unit / 2, unit),
        TileSide.East => new Rectangle((int)tileCenter.X, (int)tileCenter.Y - unit / 2, unit / 2, unit),
        _ => throw new ArgumentOutOfRangeException(nameof(half)),
    };

    // Debug aid (M74 follow-up, humble-soaring-cat.md) - draws a bold outline around every 1-unit
    // tile cell within each room, held up by the Ъ key (Game1.cs). Computed straight from Room
    // rectangles (1 tile = 1 world unit, by design) rather than reading Ship.Tiles itself, which the
    // client has no access to at all - WorldSnapshot never sends it, nothing outside tests reads it
    // yet (Ship.cs's own doc comment on Tiles) - so this stays a pure visualization, not a read of
    // the real tile grid's actual wall/floor/device content.
    internal void DrawTileGridOverlay(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        const int thickness = 3;
        var unit = (int)PixelsPerUnit;
        foreach (var room in snapshot.Rooms)
        {
            var rect = GetRoomRect(room, origin);
            for (var x = rect.X; x <= rect.Right; x += unit)
                spriteBatch.Draw(_pixel, new Rectangle(x - thickness / 2, rect.Y, thickness, rect.Height), Color.Black);
            for (var y = rect.Y; y <= rect.Bottom; y += unit)
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, y - thickness / 2, rect.Width, thickness), Color.Black);
        }
    }

    // M75 (humble-soaring-cat.md) - one full panel per REAL 1-unit tile along the band's length,
    // not a cosmetic repeat period of its own (the old TileTextures.DrawTiled(..., WallThickness, ...)
    // call repeated every 28px, which doesn't correspond to anything - visually the panel motif never
    // lined up with an actual game tile). A room's own edges always start on an exact tile boundary
    // (Room.X/Y are whole or half units, scaled by PixelsPerUnit), so starting the repeat at the
    // band's own origin - no extra phase correction - already lines up with the real grid. Each cell
    // samples the texture's FULL source square stretched to fill the destination, same "whole design
    // in miniature" idea TileTextures.DrawSquares already uses for the procedural plate.
    private void DrawWallPanels(SpriteBatch spriteBatch, Texture2D texture, Rectangle band, bool horizontal)
    {
        var unit = (int)PixelsPerUnit;
        var source = new Rectangle(0, 0, texture.Width, texture.Height);
        if (horizontal)
        {
            for (var x = band.X; x < band.Right; x += unit)
            {
                var w = Math.Min(unit, band.Right - x);
                spriteBatch.Draw(texture, new Rectangle(x, band.Y, w, band.Height), source, Color.White);
            }
        }
        else
        {
            for (var y = band.Y; y < band.Bottom; y += unit)
            {
                var h = Math.Min(unit, band.Bottom - y);
                spriteBatch.Draw(texture, new Rectangle(band.X, y, band.Width, h), source, Color.White);
            }
        }
    }

    private void DrawWallBand(SpriteBatch spriteBatch, Rectangle band, bool horizontal, bool alarmed, Vector2 origin)
    {
        // Hand-made panel art, once loaded (SetWallTextures) - drawn as its own complete design, no
        // alarm/conduit/rib overlay (that dressing was built for the flat procedural plate below, and
        // would just clutter artwork that already carries its own detail).
        var wallTexture = horizontal ? _wallHorizontalTexture : _wallVerticalTexture;
        if (wallTexture is not null)
        {
            DrawWallPanels(spriteBatch, wallTexture, band, horizontal);
            return;
        }

        // Untinted, same as HullSkin's own use of this texture - it already bakes its real
        // gunmetal colour in, so multiplying it by an alarmed/normal wall tint would just darken
        // it towards black instead of recolouring it. The alarmed conduit/rib overlay drawn below
        // still carries the alarm state on this band.
        var cellOrigin = new Point((int)origin.X, (int)origin.Y);
        TileTextures.DrawSquares(spriteBatch, _hullPlates, TileTextures.HullTileSize, WallThickness, band, Color.White, cellOrigin);
        var conduit = (alarmed ? Color.OrangeRed : Color.SteelBlue) * 0.45f;

        if (horizontal)
        {
            spriteBatch.Draw(_pixel, new Rectangle(band.X, band.Y, band.Width, 2), Color.White * 0.16f);
            spriteBatch.Draw(_pixel, new Rectangle(band.X, band.Bottom - 2, band.Width, 2), Color.Black * 0.5f);
            spriteBatch.Draw(_pixel, new Rectangle(band.X, band.Center.Y - 1, band.Width, 2), conduit);
            for (var x = band.X + RibSpacing / 2; x < band.Right; x += RibSpacing)
            {
                spriteBatch.Draw(_pixel, new Rectangle(x, band.Y, 2, band.Height), Color.Black * 0.45f);
                spriteBatch.Draw(_pixel, new Rectangle(x + 2, band.Y, 1, band.Height), Color.White * 0.12f);
            }
        }
        else
        {
            spriteBatch.Draw(_pixel, new Rectangle(band.X, band.Y, 2, band.Height), Color.White * 0.16f);
            spriteBatch.Draw(_pixel, new Rectangle(band.Right - 2, band.Y, 2, band.Height), Color.Black * 0.5f);
            spriteBatch.Draw(_pixel, new Rectangle(band.Center.X - 1, band.Y, 2, band.Height), conduit);
            for (var y = band.Y + RibSpacing / 2; y < band.Bottom; y += RibSpacing)
            {
                spriteBatch.Draw(_pixel, new Rectangle(band.X, y, band.Width, 2), Color.Black * 0.45f);
                spriteBatch.Draw(_pixel, new Rectangle(band.X, y + 2, band.Width, 1), Color.White * 0.12f);
            }
        }
    }

    private void DrawCornerPlate(SpriteBatch spriteBatch, int x, int y)
    {
        if (_wallCornerTexture is not null)
        {
            // M75 - sized to one full real tile (matching DrawWallPanels's straight-run pitch), not
            // the old procedural corner's own smaller WallThickness+6 footprint, so the corner reads
            // as the same size tile as the straight runs either side of it. A single stamp, not
            // tiled - a corner only ever appears once per corner.
            var unit = (int)PixelsPerUnit;
            var texRect = new Rectangle(x - unit / 2, y - unit / 2, unit, unit);
            spriteBatch.Draw(_wallCornerTexture, texRect, Color.White);
            return;
        }

        const int size = WallThickness + 6;
        var rect = new Rectangle(x - size / 2, y - size / 2, size, size);
        TileTextures.DrawSquares(spriteBatch, _hullPlates, TileTextures.HullTileSize, size, rect, Color.White, new Point(x, y));
        DrawRectOutline(spriteBatch, rect, Color.Black * 0.45f, 1);
        DrawRivets(spriteBatch, rect);
    }

    // A metal frame around a deliberately blank pane - see the comment at this method's call site
    // in Draw() for why the inside is left plain rather than painted with any backdrop of its own.
    private void DrawWindowPane(SpriteBatch spriteBatch, CockpitWindows.Pane worldPane, Vector2 origin)
    {
        var pane = new Rectangle(
            (int)(origin.X + worldPane.Left * PixelsPerUnit), (int)(origin.Y + worldPane.Top * PixelsPerUnit),
            (int)((worldPane.Right - worldPane.Left) * PixelsPerUnit), (int)((worldPane.Bottom - worldPane.Top) * PixelsPerUnit));
        if (pane.Width <= 0 || pane.Height <= 0)
            return;

        var bezel = new Rectangle(pane.X - 2, pane.Y - 2, pane.Width + 4, pane.Height + 4);
        TileTextures.DrawTiled(spriteBatch, _wallPlate, TileTextures.WallTileSize, bezel, new Color(96, 104, 116));
        DrawRectOutline(spriteBatch, bezel, Color.Black * 0.45f, 1);
        DrawRivets(spriteBatch, bezel);

        // Plain black, matching GraphicsDevice.Clear - not a fake starfield of its own. Whatever
        // FieldRenderer paints there next (a real asteroid/ship/EVA character, or nothing) is what
        // actually shows.
        spriteBatch.Draw(_pixel, pane, Color.Black);
        // A faint blue glass tint - what tells "window" apart from "hole" when there's nothing out
        // there to see; a real object drawn over it afterward reads normally, same as breach holes.
        spriteBatch.Draw(_pixel, pane, new Color(80, 160, 210) * 0.08f);

        var mullion = Color.Black * 0.55f;
        const int panes = 4;
        for (var i = 1; i < panes; i++)
        {
            if (worldPane.HorizontalBand)
            {
                var x = pane.X + pane.Width * i / panes;
                spriteBatch.Draw(_pixel, new Rectangle(x, pane.Y, 2, pane.Height), mullion);
            }
            else
            {
                var y = pane.Y + pane.Height * i / panes;
                spriteBatch.Draw(_pixel, new Rectangle(pane.X, y, pane.Width, 2), mullion);
            }
        }
    }

    // The camera's own fraction is dropped *before* the room's offset is added, not after.
    //
    // (int)(origin + offset) - (int)origin is not a constant as the camera glides: it flips by a
    // pixel with origin's fractional part. Every tiled surface picks its plate variant from exactly
    // that difference divided by the tile size, so for any room sitting at a whole world coordinate
    // - which is most of them - the two possible values land either side of a multiple of 48 and the
    // entire compartment reindexes. That is the deck and the wall plating visibly reshuffling while
    // the camera pans from one compartment to the next.
    //
    // Splitting the truncation makes the difference exactly (int)(room.X * PixelsPerUnit), which
    // does not depend on where the camera is at all. Costs at most a pixel of placement.
    private static Rectangle GetRoomRect(Room room, Vector2 origin) => new(
        (int)origin.X + (int)(room.X * PixelsPerUnit),
        (int)origin.Y + (int)(room.Y * PixelsPerUnit),
        (int)(room.Width * PixelsPerUnit),
        (int)(room.Height * PixelsPerUnit));

    // Direct user request ("на месте взорванного отсека будет всякие обломки") - Ship.WreckPatches
    // (World.RoomHp.cs's ExplodeRoom), one static, non-flying decal per exploded room footprint.
    // Same dark-debris palette as FieldRenderer.DrawShipDebris's own flying fragment, but drawn with
    // the plain, unrotated GetRoomRect-style conversion every other ship-local decoration here uses -
    // a wreck patch rides along with the ship's own frame (rotates/moves with it via origin, like a
    // room), it does not tumble independently the way a detached ShipDebrisFragment does.
    private void DrawWreckPatches(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        if (snapshot.WreckPatches is not { Count: > 0 } patches)
            return;
        foreach (var patch in patches)
        {
            var rect = new Rectangle(
                (int)origin.X + (int)(patch.X * PixelsPerUnit),
                (int)origin.Y + (int)(patch.Y * PixelsPerUnit),
                (int)(patch.Width * PixelsPerUnit),
                (int)(patch.Height * PixelsPerUnit));
            spriteBatch.Draw(_pixel, rect, new Color(70, 62, 58));
            DrawRectOutline(spriteBatch, rect, new Color(120, 108, 100), 2);
        }
    }

    // M62 - the "ghost" for a room still under construction: a translucent cyan fill (deliberately
    // not the hazard-red DrawBreachedWallBlock's pulse uses, since an in-progress build isn't a
    // problem to fix) plus a dashed-looking border (drawn as short segments rather than one solid
    // line, cheap enough with just _pixel and reads as a blueprint/holographic outline) and a
    // percentage readout centered in the footprint.
    private void DrawPendingRoomBuild(SpriteBatch spriteBatch, PendingRoomBuildState pending, Vector2 origin)
    {
        var rect = new Rectangle(
            (int)origin.X + (int)(pending.X * PixelsPerUnit),
            (int)origin.Y + (int)(pending.Y * PixelsPerUnit),
            (int)(pending.Width * PixelsPerUnit),
            (int)(pending.Height * PixelsPerUnit));

        spriteBatch.Draw(_pixel, rect, Color.CornflowerBlue * 0.28f);

        const int dash = 10, gap = 6, thickness = 2;
        for (var x = rect.Left; x < rect.Right; x += dash + gap)
        {
            var w = Math.Min(dash, rect.Right - x);
            spriteBatch.Draw(_pixel, new Rectangle(x, rect.Top, w, thickness), Color.CornflowerBlue);
            spriteBatch.Draw(_pixel, new Rectangle(x, rect.Bottom - thickness, w, thickness), Color.CornflowerBlue);
        }
        for (var y = rect.Top; y < rect.Bottom; y += dash + gap)
        {
            var h = Math.Min(dash, rect.Bottom - y);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Left, y, thickness, h), Color.CornflowerBlue);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, y, thickness, h), Color.CornflowerBlue);
        }

        var text = $"{pending.Name}\n{(int)(pending.ProgressFraction * 100)}%";
        var size = _font.MeasureString(text) * 0.6f;
        var center = new Vector2(rect.Center.X, rect.Center.Y) - size * 0.5f;
        spriteBatch.DrawString(_font, text, center, Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    // Content-каталог отсеков - click-to-place UI's own overlay while a module is selected
    // (Game1.cs's own _placingRoomCatalogId): a light 1-tile (3-unit) grid across the hull's own
    // footprint, every currently valid attach spot (RoomPlacementPreview.FindCandidates) outlined
    // faintly, and whichever one is closest to the cursor right now filled solid green - the one a
    // click would actually confirm.
    public void DrawPlacementOverlay(SpriteBatch spriteBatch, WorldSnapshot snapshot,
        IReadOnlyList<RoomPlacementPreview.Candidate> candidates, RoomPlacementPreview.Candidate? nearest, Vector2 origin)
    {
        const float tileUnits = 3f;
        var minX = snapshot.Rooms.Min(r => r.X) - tileUnits;
        var maxX = snapshot.Rooms.Max(r => r.X + r.Width) + tileUnits;
        var minY = snapshot.Rooms.Min(r => r.Y) - tileUnits;
        var maxY = snapshot.Rooms.Max(r => r.Y + r.Height) + tileUnits;

        Color gridLine = new(120, 160, 190, 60);
        for (var x = MathF.Floor(minX / tileUnits) * tileUnits; x <= maxX; x += tileUnits)
        {
            var screenX = (int)origin.X + (int)(x * PixelsPerUnit);
            spriteBatch.Draw(_pixel, new Rectangle(screenX, (int)origin.Y + (int)(minY * PixelsPerUnit), 1, (int)((maxY - minY) * PixelsPerUnit)), gridLine);
        }
        for (var y = MathF.Floor(minY / tileUnits) * tileUnits; y <= maxY; y += tileUnits)
        {
            var screenY = (int)origin.Y + (int)(y * PixelsPerUnit);
            spriteBatch.Draw(_pixel, new Rectangle((int)origin.X + (int)(minX * PixelsPerUnit), screenY, (int)((maxX - minX) * PixelsPerUnit), 1), gridLine);
        }

        foreach (var candidate in candidates)
        {
            var rect = new Rectangle(
                (int)origin.X + (int)(candidate.X * PixelsPerUnit), (int)origin.Y + (int)(candidate.Y * PixelsPerUnit),
                (int)(candidate.Width * PixelsPerUnit), (int)(candidate.Height * PixelsPerUnit));
            var isNearest = nearest is { } n && n.X == candidate.X && n.Y == candidate.Y;
            spriteBatch.Draw(_pixel, rect, Color.LightGreen * (isNearest ? 0.35f : 0.1f));
            DrawRectOutline(spriteBatch, _pixel, rect, Color.LightGreen * (isNearest ? 1f : 0.4f), isNearest ? 2 : 1);
        }
    }

    private static float RoomOxygen(WorldSnapshot snapshot, string roomId) =>
        snapshot.RoomOxygen.FirstOrDefault(o => o.RoomId == roomId)?.Oxygen ?? 100f;

    // Pulses via totalSeconds instead of a flat red square — reads as an active hazard light
    // rather than a static marker (SS13's breach warning strobe).
    // A real hole, not just a warning marker - sized to fully punch through the wall band's own
    // thickness (DrawRoomWalls' WallThickness) rather than sitting half-buried in it, filled with
    // dark space and a few fixed stars (seeded off the block's own position so they don't shimmer
    // frame to frame) so the plating genuinely reads as breached, with a thin pulsing hazard frame
    // around the opening for the "this is damage, not empty space" read at a glance.
    // Punches an actual hole rather than painting a fake starfield over it: this game has no
    // background starfield layer at all, and FieldRenderer draws every asteroid/ship/EVA character
    // unconditionally at its real position with no regard for the hull's own opacity (it only ever
    // reads as "outside the ship" because those things are normally positioned away from the hull's
    // footprint). FieldRenderer.Draw runs after this method in Game1's own draw order, so as long as
    // this leaves the block's own screen rect plain black (matching GraphicsDevice.Clear), whatever
    // FieldRenderer paints there next - a nearby asteroid, an enemy ship, empty void if nothing's
    // close - shows through exactly as it would look through a real gap in the plating, without any
    // extra portal/render-target machinery. Barotrauma's own breaches work the same way: they don't
    // fake the ocean, they just stop drawing the hull over it.
    // internal: also called by BoardingRenderer for the boarded enemy hull's own breached interior
    // wall blocks, the same visual language as the player's own ship's.
    internal void DrawBreachedWallBlock(SpriteBatch spriteBatch, WallBlock block, Room room, Vector2 origin, float totalSeconds)
    {
        var center = origin + new Vector2(block.X, block.Y) * PixelsPerUnit;
        var onTopOrBottom = MathF.Abs(block.Y - room.Top) < 0.01f || MathF.Abs(block.Y - room.Bottom) < 0.01f;
        const int along = 40; // short of the full 48px block pitch, so adjacent holes still read as separate bites
        var width = onTopOrBottom ? along : WallThickness;
        var height = onTopOrBottom ? WallThickness : along;
        var rect = new Rectangle((int)center.X - width / 2, (int)center.Y - height / 2, width, height);

        spriteBatch.Draw(_pixel, rect, Color.Black);

        const int frame = 3;
        DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Y, rect.Width, frame), horizontal: onTopOrBottom);
        DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Bottom - frame, rect.Width, frame), horizontal: onTopOrBottom);
        DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Y, frame, rect.Height), horizontal: onTopOrBottom);
        DrawHazardStripes(spriteBatch, new Rectangle(rect.Right - frame, rect.Y, frame, rect.Height), horizontal: onTopOrBottom);

        var flicker = 0.5f + 0.5f * MathF.Sin(totalSeconds * 6f);
        DrawRectOutline(spriteBatch, rect, Color.OrangeRed * flicker, 1);
    }

    // Same black-backing + colour-scaled-by-fraction bar every other Hp readout in this project
    // uses (InventoryPanel.DrawChargeBar, FieldRenderer's ore deposit bar) - a wall's own Hp is
    // otherwise invisible, this is what a lit welder/cutter aimed at one reveals.
    // internal, called from Game1's HUD batch rather than from this class's own Draw() - the scene
    // batch it used to live in gets multiplied by the sight-cone/room-lighting mask
    // (BuildVisibilityMask), which hid the bar the instant the block itself fell into a blind spot;
    // the HUD batch is drawn after that composite, same exemption InfoPanel/CrewPanel already get.
    internal void DrawWallToolTargetBar(SpriteBatch spriteBatch, WallBlock block, WallBlockState state, Vector2 origin) =>
        DrawToolTargetBar(spriteBatch, new Vector2(block.X, block.Y), state.Fraction, origin);

    // Same bar, over a door being cut open (World.Cutting.cs's CutIndoorAlongFlame) instead of a
    // hull block - worldPosition is whichever Door matched (character.DoorToolTargetId), interior
    // or vacuum-facing alike.
    internal void DrawDoorToolTargetBar(SpriteBatch spriteBatch, Vector2 worldPosition, DoorState state, Vector2 origin) =>
        DrawToolTargetBar(spriteBatch, worldPosition, state.Fraction, origin);

    // internal: Game1's HUD batch also calls this directly for an enemy hull's own hatch target
    // (a WallBlockState like a wall block's, since a hatch's own Hp is folded into the same
    // dictionary now - EnemyShipRuntime, humble-soaring-cat.md) - the two typed wrappers above only
    // cover the player's own ship's WallBlock/Door shapes.
    internal void DrawToolTargetBar(SpriteBatch spriteBatch, Vector2 worldPosition, float fraction, Vector2 origin)
    {
        const int width = 32;
        const int height = 6;
        var center = origin + worldPosition * PixelsPerUnit;
        var bar = new Rectangle((int)center.X - width / 2, (int)center.Y - 22, width, height);
        var fill = fraction > 0.6f ? Color.LimeGreen : fraction > 0.25f ? Color.Orange : Color.OrangeRed;
        spriteBatch.Draw(_pixel, bar, Color.Black * 0.7f);
        spriteBatch.Draw(_pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * fraction), bar.Height), fill);
        DrawRectOutline(spriteBatch, bar, Color.LightGray * 0.7f, 1);
    }
}
