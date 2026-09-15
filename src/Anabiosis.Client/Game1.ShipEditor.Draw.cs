using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client;

public partial class Game1
{
    private void DrawShipEditorScreen()
    {
        var title = "Редактор корабля" + (_editorCurrentSlotName is { } slot ? $" - {slot}" : "");
        _spriteBatch.DrawString(_font, title, new Vector2(20, 8), Color.White, 0f, Vector2.Zero, 1.1f, SpriteEffects.None, 0f);

        DrawEditorCanvas();
        DrawEditorSidebar();
        DrawEditorBottomBar();

        if (_editorZoneNamePrompting)
            DrawEditorZoneNamePrompt();
        else if (_editorSaveAsPrompting)
            DrawEditorSaveAsPrompt();
        else if (_editorLoadListOpen)
            DrawEditorLoadList();

        DrawEditorToast();
    }

    // Direct user request ("чтобы игра говорила что так делать нельзя") - a rejected compartment
    // placement gets a real, visible message instead of the silent no-op every other tool's own
    // precondition check still uses, since overlapping another compartment is easy to click into by
    // accident. Centered over the canvas, on top of everything (same "last thing drawn wins" spot the
    // pause menu/cheat panel already claim in the main session) so it's impossible to miss.
    private void DrawEditorToast()
    {
        if (_editorToastMessage is not { } message || Environment.TickCount64 >= _editorToastUntilTicks)
            return;

        var size = _font.MeasureString(message) * 0.65f;
        var boxWidth = size.X + 32;
        var boxHeight = size.Y + 20;
        var box = new Rectangle(
            ShipEditorCanvas.X + (ShipEditorCanvas.Width - (int)boxWidth) / 2,
            ShipEditorCanvas.Y + 16,
            (int)boxWidth, (int)boxHeight);

        // Fades out over its own last half-second rather than popping off abruptly.
        var remainingMs = _editorToastUntilTicks - Environment.TickCount64;
        var alpha = MathHelper.Clamp(remainingMs / 500f, 0f, 1f);

        _spriteBatch.Draw(_pixel, box, new Color(40, 20, 20) * (0.92f * alpha));
        DrawRectOutline(box, new Color(220, 80, 70) * alpha, 2f);
        _spriteBatch.DrawString(_font, message, new Vector2(box.X + 16, box.Y + 10),
            new Color(255, 210, 205) * alpha, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
    }

    // Редактор корабля в духе Cosmoteer + несколько сохранённых кораблей (humble-soaring-cat.md,
    // Step 6) - a small modal over everything else, same idea as InventoryPanel's own tooltip box
    // (a PanelFrame-bordered rect anchored at a fixed spot, not draggable).
    private void DrawEditorSaveAsPrompt()
    {
        var box = GetEditorSaveAsBoxRect();
        _spriteBatch.Draw(_pixel, box, new Color(24, 26, 34));
        DrawRectOutline(box, Color.LightGray, 2f);
        _spriteBatch.DrawString(_font, "Сохранить как:", new Vector2(box.X + 16, box.Y + 12), Color.LightGray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        var inputRect = GetEditorSaveAsInputRect();
        _spriteBatch.Draw(_pixel, inputRect, new Color(40, 44, 54));
        DrawRectOutline(inputRect, Color.White, 1f);
        _spriteBatch.DrawString(_font, _editorSaveAsInput, new Vector2(inputRect.X + 6, inputRect.Y + 5), Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        DrawEditorModalButton(GetEditorSaveAsConfirmRect(), "СОХРАНИТЬ", true);
        DrawEditorModalButton(GetEditorSaveAsCancelRect(), "ОТМЕНА", true);
    }

    // Tile-painting redo - the Zone tool's own naming prompt, same small-modal convention as
    // "Сохранить как" above (they never show at the same time, so sharing screen position is fine).
    private void DrawEditorZoneNamePrompt()
    {
        var box = GetEditorZoneNameBoxRect();
        _spriteBatch.Draw(_pixel, box, new Color(24, 26, 34));
        DrawRectOutline(box, Color.LightGray, 2f);
        _spriteBatch.DrawString(_font, "Название отсека:", new Vector2(box.X + 16, box.Y + 12), Color.LightGray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        // Zone-type quick-select (direct user request - all 4 described zone types): picking one
        // fills the name field with its canonical label and records the type; typing over the field
        // afterward drops back to an untyped, purely cosmetic zone (Game1.Menu.cs's own text handler).
        for (var i = 0; i < ShipZoneKinds.All.Length; i++)
        {
            var kind = ShipZoneKinds.All[i];
            var rect = GetEditorZoneTypeButtonRect(i);
            var selected = _editorZonePendingKind == kind;
            _spriteBatch.Draw(_pixel, rect, selected ? new Color(90, 130, 90) : new Color(40, 44, 54));
            DrawRectOutline(rect, selected ? Color.LightGreen : Color.LightGray, 1f);
            var label = ShipZoneKinds.CanonicalName(kind);
            var labelSize = _font.MeasureString(label) * 0.42f;
            _spriteBatch.DrawString(_font, label, new Vector2(rect.Center.X - labelSize.X / 2, rect.Center.Y - labelSize.Y / 2),
                Color.White, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
        }

        var inputRect = GetEditorZoneNameInputRect();
        _spriteBatch.Draw(_pixel, inputRect, new Color(40, 44, 54));
        DrawRectOutline(inputRect, Color.White, 1f);
        _spriteBatch.DrawString(_font, _editorZoneNameInput, new Vector2(inputRect.X + 6, inputRect.Y + 5), Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        DrawEditorModalButton(GetEditorZoneNameConfirmRect(), "OK", true);
        DrawEditorModalButton(GetEditorZoneNameCancelRect(), "ОТМЕНА", true);
    }

    private void DrawEditorLoadList()
    {
        var box = GetEditorLoadBoxRect();
        _spriteBatch.Draw(_pixel, box, new Color(24, 26, 34));
        DrawRectOutline(box, Color.LightGray, 2f);
        _spriteBatch.DrawString(_font, "Сохранённые корабли:", new Vector2(box.X + 16, box.Y + 10), Color.LightGray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        var names = CustomShipStore.ListShips();
        if (names.Count == 0)
            _spriteBatch.DrawString(_font, "(пока ничего не сохранено)", new Vector2(box.X + 16, GetEditorLoadRowRect(0).Y), Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

        // Direct user request ("сделай возможность листать сохраненные чертежи в редакторе") - one
        // page of EditorLoadRowsPerPage names at a time instead of every saved design cramming into
        // (and eventually overflowing past) a single fixed-height list.
        var pageCount = Math.Max(1, (names.Count + EditorLoadRowsPerPage - 1) / EditorLoadRowsPerPage);
        var page = Math.Clamp(_editorLoadListPage, 0, pageCount - 1);
        var firstIndex = page * EditorLoadRowsPerPage;
        var pageNames = names.Skip(firstIndex).Take(EditorLoadRowsPerPage).ToList();

        for (var i = 0; i < pageNames.Count; i++)
        {
            var rowRect = GetEditorLoadRowRect(i);
            var current = pageNames[i] == _editorCurrentSlotName;
            _spriteBatch.Draw(_pixel, rowRect, current ? new Color(120, 92, 30) * 0.6f : Color.DimGray * 0.4f);
            DrawRectOutline(rowRect, current ? Color.White : Color.DimGray, 1f);
            _spriteBatch.DrawString(_font, pageNames[i], new Vector2(rowRect.X + 8, rowRect.Y + 4), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

            var deleteRect = GetEditorLoadRowDeleteRect(i);
            _spriteBatch.Draw(_pixel, deleteRect, new Color(120, 50, 50));
            DrawRectOutline(deleteRect, Color.OrangeRed, 1f);
            _spriteBatch.DrawString(_font, "X", new Vector2(deleteRect.X + 14, deleteRect.Y + 4), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        if (pageCount > 1)
        {
            DrawEditorModalButton(GetEditorLoadPrevPageRect(), "<", page > 0);
            DrawEditorModalButton(GetEditorLoadNextPageRect(), ">", page < pageCount - 1);
            var pageLabel = $"Стр. {page + 1}/{pageCount}";
            var labelRect = GetEditorLoadPageLabelRect();
            var labelSize = _font.MeasureString(pageLabel) * 0.5f;
            _spriteBatch.DrawString(_font, pageLabel,
                new Vector2(labelRect.Center.X - labelSize.X / 2, labelRect.Center.Y - labelSize.Y / 2),
                Color.LightGray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        DrawEditorModalButton(GetEditorLoadCloseRect(), "ЗАКРЫТЬ", true);
    }

    private void DrawEditorModalButton(Rectangle rect, string label, bool enabled)
    {
        var hovered = enabled && rect.Contains(_designMouse);
        _spriteBatch.Draw(_pixel, rect, hovered ? new Color(120, 92, 30) : Color.DimGray * 0.6f);
        DrawRectOutline(rect, Color.LightGray, 1f);
        _spriteBatch.DrawString(_font, label, new Vector2(rect.X + 8, rect.Y + 6), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private void DrawEditorCanvas()
    {
        _spriteBatch.Draw(_pixel, ShipEditorCanvas, new Color(18, 20, 28));

        // Panning (direct user request) means grid lines/tiles can now compute to a screen position
        // outside ShipEditorCanvas - without a scissor clip that would bleed straight over the
        // sidebar/bottom bar, since SpriteBatch draws aren't clipped to any rectangle on their own.
        // Same nested End/scissor/Begin convention ExternalCameraPanel.DrawOneCamera already
        // established for its own clipped viewport. Also switches to point (nearest-neighbor)
        // filtering here - SpriteBatch's own default is linear/bilinear, which reads as a soft,
        // "мыльный" blur on this flat painted pixel-art wall/floor style the moment a 60px tile
        // texture gets stretched to any zoom level that isn't an exact 1:1 match (direct user
        // report) - the rest of the editor screen (buttons/text) is untouched, still on the default
        // sampler, which is fine for those.
        var previousScissor = GraphicsDevice.ScissorRectangle;
        GraphicsDevice.ScissorRectangle = EditorCanvasDeviceRect();
        _spriteBatch.End();
        _spriteBatch.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true },
            samplerState: SamplerState.PointClamp, transformMatrix: _renderScale);

        // Line count derived from the fixed canvas pixel size divided by the current (zoomed) cell
        // size, not the old fixed ShipEditorGridCols/Rows - so the grid still fully covers the
        // canvas at any zoom level rather than stopping short (zoomed in) or leaving a gap (zoomed
        // out). Starting column/row is whichever world tile the pan offset currently lines up with
        // the canvas's own left/top edge, with a one-tile margin on each side so a partially panned
        // tile at the border still gets its line drawn (the scissor rect crops the rest).
        var startCol = FloorDiv(_editorPanOffset.X, EditorCellSize) - 1;
        var startRow = FloorDiv(_editorPanOffset.Y, EditorCellSize) - 1;
        var cols = ShipEditorCanvas.Width / EditorCellSize + 2;
        var rows = ShipEditorCanvas.Height / EditorCellSize + 2;
        for (var i = 0; i <= cols; i++)
        {
            var x = ShipEditorCanvas.X + (startCol + i) * EditorCellSize - _editorPanOffset.X;
            HudIcons.DrawLine(_spriteBatch, _pixel,
                new Vector2(x, ShipEditorCanvas.Top), new Vector2(x, ShipEditorCanvas.Bottom),
                new Color(50, 54, 64), 1f);
        }
        for (var i = 0; i <= rows; i++)
        {
            var y = ShipEditorCanvas.Y + (startRow + i) * EditorCellSize - _editorPanOffset.Y;
            HudIcons.DrawLine(_spriteBatch, _pixel,
                new Vector2(ShipEditorCanvas.Left, y), new Vector2(ShipEditorCanvas.Right, y),
                new Color(50, 54, 64), 1f);
        }

        DrawEditorTiles();

        if (_editorTool == EditorTool.Floor)
            DrawEditorFloorDragPreview();
        if (_editorTool == EditorTool.Wall)
            DrawEditorWallDragPreview();
        if (_editorTool == EditorTool.Door)
            DrawEditorDoorPlacementPreview();
        if (_editorTool == EditorTool.Zone)
            DrawEditorZoneDragPreview();
        if (_editorTool == EditorTool.Terminal)
            DrawEditorTerminalPlacementPreview();
        if (_editorTool == EditorTool.Device)
            DrawEditorDevicePlacementPreview();
        if (_editorTool == EditorTool.Engine)
            DrawEditorEnginePlacementPreview();
        if (_editorTool == EditorTool.Compartment)
            DrawEditorCompartmentPlacementPreview();

        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = previousScissor;
        _spriteBatch.Begin(transformMatrix: _renderScale);
    }

    // ScissorRectangle is always in real backbuffer pixels, unlike every other coordinate this whole
    // screen works in (design-space, mapped up by _renderScale) - transform the canvas's own corners
    // through that same matrix rather than assuming design space equals device space (same technique
    // ExternalCameraPanel.DeviceSpaceRect already uses for its own clipped viewport).
    private Rectangle EditorCanvasDeviceRect()
    {
        var topLeft = Vector2.Transform(new Vector2(ShipEditorCanvas.X, ShipEditorCanvas.Y), _renderScale);
        var bottomRight = Vector2.Transform(new Vector2(ShipEditorCanvas.Right, ShipEditorCanvas.Bottom), _renderScale);
        var viewport = GraphicsDevice.Viewport;
        var x = Math.Clamp((int)MathF.Round(topLeft.X), 0, viewport.Width);
        var y = Math.Clamp((int)MathF.Round(topLeft.Y), 0, viewport.Height);
        var right = Math.Clamp((int)MathF.Round(bottomRight.X), 0, viewport.Width);
        var bottom = Math.Clamp((int)MathF.Round(bottomRight.Y), 0, viewport.Height);
        return new Rectangle(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }

    // Same rectangle the release will actually fill (HandleFloorToolInput) - a line-shaped drag
    // previews as a thin rectangle, a square-shaped drag as a square, no separate code path for either.
    private void DrawEditorFloorDragPreview()
    {
        if (_editorFloorDragStart is not { } start)
        {
            // Direct user request ("при размещении вообще всех блоков подсвечивалось область") -
            // a single-cell version of the same highlight below, shown before any drag has actually
            // started, so hovering alone already previews what one click would place.
            if (GridCellAt(_designMouse) is { } hover)
            {
                var hoverRect = EditorTileRect(new TileCoord(hover.X, hover.Y));
                _spriteBatch.Draw(_pixel, hoverRect, new Color(90, 160, 110) * 0.35f);
                DrawRectOutline(hoverRect, Color.LightGreen, 2f);
            }
            return;
        }
        var endCell = GridCellAt(_designMouse) ?? start;
        var minX = Math.Min(start.X, endCell.X);
        var minY = Math.Min(start.Y, endCell.Y);
        var maxX = Math.Max(start.X, endCell.X);
        var maxY = Math.Max(start.Y, endCell.Y);
        var rect = new Rectangle(
            ShipEditorCanvas.X + minX * EditorCellSize - _editorPanOffset.X,
            ShipEditorCanvas.Y + minY * EditorCellSize - _editorPanOffset.Y,
            (maxX - minX + 1) * EditorCellSize, (maxY - minY + 1) * EditorCellSize);
        _spriteBatch.Draw(_pixel, rect, new Color(90, 160, 110) * 0.35f);
        DrawRectOutline(rect, Color.LightGreen, 2f);
    }

    private Rectangle EditorTileRect(TileCoord coord) => new(
        ShipEditorCanvas.X + coord.X * EditorCellSize - _editorPanOffset.X,
        ShipEditorCanvas.Y + coord.Y * EditorCellSize - _editorPanOffset.Y,
        EditorCellSize, EditorCellSize);

    // Tile-painting redo (humble-soaring-cat.md, M76 follow-up) - floors first (so a wall/device/
    // terminal drawn after always sits visibly on top, same "floor then walls" order the real ship
    // renderer uses), then walls/doors/terminals/devices, then zone overlays on top of everything so
    // a zone's translucent tint reads clearly even over a busy tile.
    private void DrawEditorTiles()
    {
        foreach (var (coord, cell) in _editorTiles.Cells)
        {
            if (!cell.HasFloor)
                continue;
            var rect = EditorTileRect(coord);
            if (_editorFloorTexture is { } floorTex)
                _spriteBatch.Draw(floorTex, rect, Color.White);
            else
                _spriteBatch.Draw(_pixel, rect, new Color(46, 52, 66));
        }

        foreach (var (coord, cell) in _editorTiles.Cells)
        {
            if (cell.Wall == TileWallKind.Solid)
                DrawEditorWallTile(coord);
            else if (cell.Wall == TileWallKind.Door)
                DrawEditorDoorTile(coord);
            if (cell.WallDeviceId is not null && cell.WallDeviceKind is { } wallDeviceKind && cell.WallDeviceMountSide is { } mountSide)
                DrawEditorWallDeviceHalfBlock(coord, wallDeviceKind, mountSide);
            if (cell.DeviceId is not null && _editorDeviceKinds.TryGetValue(coord, out var kind))
                DrawEditorDeviceAt(coord, kind);
        }

        foreach (var (control, facing) in _editorEngineFacing)
            DrawEditorEngineAt(control, facing);

        // M-doors-as-edges (humble-soaring-cat.md) - drawn as its own pass rather than folded into
        // the Wall/Door-tile loop above, since an edge has no Cells entry of its own to iterate.
        foreach (var (key, edge) in _editorTiles.DoorEdges)
            DrawEditorDoorEdgeAt(key.Coord, key.Side, edge.Open);

        foreach (var zone in _editorZones)
            DrawEditorZone(zone);
    }

    // Mirrors ShipRenderer.DrawDoorEdge's own "short bar centered on the seam" geometry exactly,
    // just in the editor canvas's own pixel space (EditorTileRect/EditorCellSize/_editorPanOffset)
    // instead of the live game's PixelsPerUnit/origin - editor canvases have no HP/destroyed state
    // to show (walls never take damage in the editor), so this only ever needs the open/closed color.
    private void DrawEditorDoorEdgeAt(TileCoord coord, TileSide side, bool open)
    {
        const float thicknessFraction = 0.22f;
        var thickness = Math.Max(4, (int)(thicknessFraction * EditorCellSize));
        var anchorRect = EditorTileRect(coord);

        Rectangle bar, frame;
        if (side == TileSide.East)
        {
            var seamX = anchorRect.Right;
            frame = new Rectangle(seamX - thickness / 2 - 2, anchorRect.Y, thickness + 4, anchorRect.Height);
            bar = new Rectangle(seamX - thickness / 2, anchorRect.Y, thickness, anchorRect.Height);
        }
        else
        {
            var seamY = anchorRect.Bottom;
            frame = new Rectangle(anchorRect.X, seamY - thickness / 2 - 2, anchorRect.Width, thickness + 4);
            bar = new Rectangle(anchorRect.X, seamY - thickness / 2, anchorRect.Width, thickness);
        }

        _spriteBatch.Draw(_pixel, frame, new Color(90, 68, 46));
        _spriteBatch.Draw(_pixel, bar, open ? new Color(90, 230, 120) : new Color(255, 90, 90));
    }

    // Same neighbor-based orientation ShipRenderer.DrawWallTile uses in the real game (M75,
    // humble-soaring-cat.md) - opposite wall-kind neighbors read as a straight run, perpendicular
    // ones as a corner (rotated 0/90/180/270deg from the corner art's own South+East base
    // orientation). Kept as a separate copy rather than shared with ShipRenderer since the two work
    // at different pixel scales (ShipRenderer.PixelsPerUnit=48 vs EditorCellSize, base 24) and read
    // from different data (a live WorldSnapshot's rooms vs this editor's own in-memory TileGrid).
    // Reinforced/Window (direct user request) reuse the same wall textures, just tinted - no bespoke
    // art exists for either variant yet, same convention Game1.ShipEditor.DeviceTabs.cs's palette
    // icons already use. Standard stays plain white (no tint at all).
    //
    // Direct user request ("убери эту смену цветов") - an ALREADY-PLACED wall used to get one
    // distinct brownish tint the moment it came from the Compartment tool (TileCell.
    // WallFromCompartment, still editor-only bookkeeping used elsewhere for removal/restoration -
    // see that field's own doc comment - just no longer read here) regardless of its own
    // WallMaterial. Now tinted purely by WallMaterial, same as any hand-painted wall - a
    // compartment's own wall ring is indistinguishable from one drawn tile by tile with the Wall
    // tool, which is what the user asked for. DrawEditorCompartmentPlacementPreview's own
    // CompartmentPreviewWallTint below is unrelated - a live, temporary ghost highlighting where a
    // compartment's walls WOULD land before the player commits, not a persistent per-tile colour.
    private static Color WallMaterialTint(TileCell? cell) => (cell?.WallMaterial ?? WallMaterial.Standard) switch
    {
        WallMaterial.Reinforced => new Color(150, 155, 165),
        WallMaterial.Window => new Color(150, 215, 235) * 0.75f,
        _ => Color.White,
    };

    // Only for the live Compartment-tool placement preview just below (DrawEditorCompartmentPlacement
    // Preview) - distinguishes the ring-tiles-to-be from the interior-tiles-to-be in the ghost, a
    // temporary UI affordance, not the persistent wall tint WallMaterialTint governs above.
    private static readonly Color CompartmentPreviewWallTint = new(196, 154, 92);

    private void DrawEditorWallTile(TileCoord coord)
    {
        bool HasWall(TileSide side) => _editorTiles.CellAt(side.Offset(coord)) is { Wall: TileWallKind.Solid or TileWallKind.Door };
        var north = HasWall(TileSide.North);
        var south = HasWall(TileSide.South);
        var east = HasWall(TileSide.East);
        var west = HasWall(TileSide.West);
        var rect = EditorTileRect(coord);
        var tint = WallMaterialTint(_editorTiles.CellAt(coord));
        var openSide = _editorTiles.CellAt(coord)?.WallOpenSide;

        // Direct user bug report ("у него неправильная текстура и на нём не работает большинство
        // правил связанных со стенами") - a wall tile CAN be "neighborCount==1"/"==3" below (an
        // end-cap/T-junction by wall-CONNECTIVITY) while still genuinely having exactly one missing-
        // FLOOR neighbor (WallOpenSide != null, a completely independent classification) - those
        // sprites are rotated draws that don't crop cleanly by resizing their destination rect, so
        // every one of them falls through to the same plain half-rect fallback the straight-run
        // cases now use whenever WallOpenSide is actually set, rather than showing a full,
        // un-thinned sprite that disagrees with the tile's own real collision.
        if (openSide is { } forcedHalf)
        {
            var forcedRect = EditorHalfRect(rect, forcedHalf);
            _spriteBatch.Draw(_pixel, forcedRect, tint == Color.White ? new Color(120, 130, 150) : tint);
            DrawRectOutline(forcedRect, Color.Black, 1f);
            return;
        }

        // A T-junction (exactly 3 wall-kind neighbors - a straight tile-drawn wall can meet another
        // one at 3 sides in a way no rectangular hand-authored hull ever produced) has to be checked
        // BEFORE the plain straight-run tests below, since 3 neighbors always include one opposite
        // pair and would otherwise silently read as a plain straight tile, ignoring the third branch.
        var neighborCount = (north ? 1 : 0) + (south ? 1 : 0) + (east ? 1 : 0) + (west ? 1 : 0);
        if (neighborCount == 3 && _editorWallTJunctionTexture is { } tTex)
        {
            // Base art has the missing/open side facing North (a horizontal run continuing East+West
            // with a spur branching South) - rotate 90° per step clockwise to whichever side is
            // actually the open one here, same convention as the corner/end-cap rotations below.
            var tRotation = !north ? 0f : !east ? MathHelper.PiOver2 : !south ? MathHelper.Pi : -MathHelper.PiOver2;
            var tOrigin = new Vector2(tTex.Width / 2f, tTex.Height / 2f);
            _spriteBatch.Draw(tTex, new Rectangle(rect.Center.X, rect.Center.Y, EditorCellSize, EditorCellSize),
                null, tint, tRotation, tOrigin, SpriteEffects.None, 0f);
            return;
        }
        if (north && south && _editorWallVerticalTexture is { } vTex)
        {
            _spriteBatch.Draw(vTex, rect, tint);
            return;
        }
        if (east && west && _editorWallHorizontalTexture is { } hTex)
        {
            _spriteBatch.Draw(hTex, rect, tint);
            return;
        }
        // A dead end (exactly one wall-kind neighbor) reads wrong with the corner texture (a "turn"
        // where the wall actually just stops) - direct user report. Base end-cap art connects South,
        // caps at North; rotate the same 90°-per-step clockwise convention the corner uses.
        if (neighborCount == 1 && _editorWallEndCapTexture is { } capTex)
        {
            var capRotation = south ? 0f : west ? MathHelper.PiOver2 : north ? MathHelper.Pi : -MathHelper.PiOver2;
            var capOrigin = new Vector2(capTex.Width / 2f, capTex.Height / 2f);
            _spriteBatch.Draw(capTex, new Rectangle(rect.Center.X, rect.Center.Y, EditorCellSize, EditorCellSize),
                null, tint, capRotation, capOrigin, SpriteEffects.None, 0f);
            return;
        }
        if (_editorWallCornerTexture is { } cTex)
        {
            var rotation = (south, east, west, north) switch
            {
                (true, true, _, _) => 0f,
                (true, _, true, _) => MathHelper.PiOver2,
                (_, _, true, true) => MathHelper.Pi,
                (_, true, _, true) => -MathHelper.PiOver2,
                _ => 0f,
            };
            var texOrigin = new Vector2(cTex.Width / 2f, cTex.Height / 2f);
            _spriteBatch.Draw(cTex, new Rectangle(rect.Center.X, rect.Center.Y, EditorCellSize, EditorCellSize),
                null, tint, rotation, texOrigin, SpriteEffects.None, 0f);
            return;
        }

        // openSide is always null here - the half-thick case already returned above.
        _spriteBatch.Draw(_pixel, rect, tint == Color.White ? new Color(120, 130, 150) : tint);
        DrawRectOutline(rect, Color.Black, 1f);
    }

    // Editor-scale counterpart to ShipRenderer.HalfRect - same "half the tile, flush to `half`"
    // rectangle, just against an already-built EditorTileRect instead of a center+unit pair.
    private static Rectangle EditorHalfRect(Rectangle full, TileSide half) => half switch
    {
        TileSide.North => new Rectangle(full.X, full.Y, full.Width, full.Height / 2),
        TileSide.South => new Rectangle(full.X, full.Y + full.Height / 2, full.Width, full.Height / 2),
        TileSide.West => new Rectangle(full.X, full.Y, full.Width / 2, full.Height),
        TileSide.East => new Rectangle(full.X + full.Width / 2, full.Y, full.Width / 2, full.Height),
        _ => throw new ArgumentOutOfRangeException(nameof(half)),
    };

    // A wide door (direct user request - "дверь занимающая 1 на 2 тайла", TileCell.DoorGroupId)
    // draws as ONE merged rectangle spanning both tiles, not two separate narrow ones - only the
    // tile that sorts first (by X then Y) actually draws it, so the pair isn't rendered twice.
    // The rect itself is handed straight to ShipRenderer's own door art (direct user request -
    // "своей моделькой, а не голым квадратом") instead of a flat placeholder fill - same closed
    // look the ship interior view shows, since a door being built has no open/closed state yet.
    private void DrawEditorDoorTile(TileCoord coord)
    {
        var cell = _editorTiles.CellAt(coord);
        if (cell?.DoorGroupId is { } groupId)
        {
            var partnerEntry = _editorTiles.Cells.FirstOrDefault(kv => kv.Key != coord && kv.Value.DoorGroupId == groupId);
            if (partnerEntry.Value is not null)
            {
                var partner = partnerEntry.Key;
                if (partner.X < coord.X || (partner.X == coord.X && partner.Y < coord.Y))
                    return; // the partner tile owns this pair's draw
                var merged = Rectangle.Union(EditorTileRect(coord), EditorTileRect(partner));
                // Same X, differing Y - the pair is stacked in a column, i.e. sits on a VERTICAL
                // shared wall (TileShipBuilder.cs's own direction==East/Vertical convention).
                _shipRenderer.DrawDoor(_spriteBatch, merged, vertical: partner.X == coord.X, isOpen: false);
                return;
            }
        }

        var rect = EditorTileRect(coord);
        _shipRenderer.DrawDoor(_spriteBatch, rect, vertical: ResolveDoorTileVertical(coord), isOpen: false);
    }

    // Direct user bug report ("после поворота двери при выставлении она всё равно ставится под
    // одним и тем же углом") - a lone narrow-door barrier tile has no partner to derive orientation
    // from the way a wide door's pair does (this file's own `partner.X == coord.X` check above), so
    // this used to always fall back to guessing from floor-neighbors alone - silently ignoring
    // whatever the player actually chose with R whenever a tile happened to have floor on every
    // side (both orientations geometrically "valid" there). _editorDoorVertical (populated at
    // placement time, Game1.ShipEditor.cs's own HandleDoorToolInput) is checked FIRST now; the
    // geometric guess only remains as a fallback for a door tile placed before this session (e.g.
    // loaded from an old save, where this transient dictionary was never populated).
    private bool ResolveDoorTileVertical(TileCoord coord) =>
        _editorDoorVertical.TryGetValue(coord, out var vertical) ? vertical : InferDoorTileVertical(coord);

    // Export hasn't run yet, so there's no Door.IsVertical to read - inferred straight from which
    // sides of this barrier tile already have floor (humble-soaring-cat.md "Дверь как устройство
    // со своим footprint'ом"): floor to the West+East means the barrier sits in a column between
    // two rooms side by side along X (a vertical wall); floor to the North+South means a row
    // between rooms stacked along Y. No new stored state needed - purely geometric, same kind of
    // neighbor-based check RecomputeWallOpenSide's own claim logic already uses elsewhere.
    private bool InferDoorTileVertical(TileCoord coord) =>
        _editorTiles.CellAt(TileSide.West.Offset(coord)) is { HasFloor: true } &&
        _editorTiles.CellAt(TileSide.East.Offset(coord)) is { HasFloor: true };

    // Direct user request ("при размещении вообще всех блоков подсвечивалось область") - the one
    // tool that had no ghost preview at all before this (every other tool already had one: Floor/
    // Wall/Zone their own drag previews, Door/Device/Engine/Compartment the green/red valid-
    // placement convention DrawEditorDevicePlacementPreview established). Mirrors
    // HandleTerminalToolInput's own validity checks exactly (floor, no terminal yet, not a
    // construction junction, and at least one walled side to mount to) rather than a second,
    // independently-drifting copy of that logic.
    private void DrawEditorTerminalPlacementPreview()
    {
        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var coord = new TileCoord(cell.X, cell.Y);
        var rect = EditorTileRect(coord);
        var targetCell = _editorTiles.CellAt(coord);
        var kind = _editorSelectedWallDeviceKind;

        // Direct user request (a device can mount into a half-thick wall's own free half) -
        // hovering directly over a non-corner half-thick wall tile previews the recessed mode;
        // otherwise falls back to the floor-adjacent mount-side search, unchanged.
        if (targetCell is { Wall: TileWallKind.Solid, WallOpenSide: { } recessSide, WallDeviceId: null })
        {
            _spriteBatch.Draw(_pixel, rect, new Color(90, 160, 110) * 0.35f);
            DrawRectOutline(rect, Color.LightGreen, 2f);
            DrawEditorWallDeviceHalfBlock(coord, kind, recessSide.Opposite());
            return;
        }

        var mountSide = targetCell is { HasFloor: true, WallDeviceId: null } && !IsAtConstructionJunction(coord)
            ? FindWallDeviceMountSide(coord)
            : null;

        var valid = mountSide is not null;
        _spriteBatch.Draw(_pixel, rect, (valid ? new Color(90, 160, 110) : new Color(160, 90, 90)) * 0.35f);
        DrawRectOutline(rect, valid ? Color.LightGreen : Color.OrangeRed, 2f);
        if (mountSide is { } side)
            DrawEditorWallDeviceHalfBlock(coord, kind, side);
    }

    // Direct user request ("занимал половину блока и визуально выглядел в соответствии с
    // полублоком") - a real filled half-block in the device's own catalog tint (distinct per kind -
    // Terminal's cyan vs WallLamp's warm yellow, CustomDeviceCatalog.Tint), not just a placeholder
    // line. `mountSide` already means "which side of THIS tile the half-block sits on" uniformly for
    // both the recessed and floor-adjacent modes (TileCell.WallDeviceMountSide's own doc comment) -
    // no branching needed here at all.
    private void DrawEditorWallDeviceHalfBlock(TileCoord coord, CustomDeviceKind kind, TileSide mountSide)
    {
        var rect = EditorHalfRect(EditorTileRect(coord), mountSide);
        var tint = CustomDeviceCatalog.Tint(kind);
        _spriteBatch.Draw(_pixel, rect, tint * 0.85f);
        DrawRectOutline(rect, Color.Gold, 2f);
        var glyph = CustomDeviceCatalog.ShortGlyph(kind);
        var scale = MathF.Min(1f, (rect.Width - 4) / _font.MeasureString(glyph).X);
        var textSize = _font.MeasureString(glyph) * scale;
        _spriteBatch.DrawString(_font, glyph, new Vector2(rect.Center.X - textSize.X / 2f, rect.Center.Y - textSize.Y / 2f),
            Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    // Direct user request ("подсвечивалась его площадь как в rimworld") - a live ghost outline over
    // whatever the Device tool would actually place at the cursor right now, green where every tile
    // qualifies (TileGrid.PlaceDevice's own precondition, same check HandleDeviceToolInput itself
    // makes before committing) and red the instant any tile in the footprint doesn't - exactly the
    // valid/invalid colour convention the Wall tool's own drag preview already uses. Centred on the
    // cursor's own tile (FootprintAnchorFor), not cornered on it, per the user's own follow-up.
    private void DrawEditorDevicePlacementPreview()
    {
        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var hovered = new TileCoord(cell.X, cell.Y);
        var (width, height) = DeviceFootprintSize(_editorSelectedDeviceKind, _editorDevicePendingRotated);
        var anchor = FootprintAnchorFor(hovered, width, height);
        var footprint = DeviceFootprintTiles(anchor, width, height).ToList();
        var valid = footprint.All(t => _editorTiles.CellAt(t) is { HasFloor: true, Wall: TileWallKind.None, DeviceId: null });

        var topLeft = EditorTileRect(anchor);
        var bottomRight = EditorTileRect(new TileCoord(anchor.X + width - 1, anchor.Y + height - 1));
        var rect = new Rectangle(topLeft.X, topLeft.Y, bottomRight.Right - topLeft.X, bottomRight.Bottom - topLeft.Y);
        _spriteBatch.Draw(_pixel, rect, (valid ? new Color(90, 160, 110) : new Color(160, 90, 90)) * 0.35f);
        DrawRectOutline(rect, valid ? Color.LightGreen : Color.OrangeRed, 2f);
    }

    // Live ghost preview for the Engine tool, same green/red valid-placement convention
    // DrawEditorDevicePlacementPreview already uses - one box per tile of the pending 3-tile line
    // (Control/Bulkhead/Nozzle) rather than one merged rectangle, since each tile has a genuinely
    // different precondition (see HandleEngineToolInput's own doc comment) and a single shared colour
    // would hide which specific tile is the problem.
    private void DrawEditorEnginePlacementPreview()
    {
        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var control = new TileCoord(cell.X, cell.Y);
        var facing = _editorEnginePendingFacing;
        var bulkhead = facing.Offset(control);
        var nozzle = facing.Offset(bulkhead);

        var controlValid = _editorTiles.CellAt(control) is { HasFloor: true, Wall: TileWallKind.None, DeviceId: null };
        var bulkheadValid = _editorTiles.CellAt(bulkhead) is { Wall: TileWallKind.Solid };
        var nozzleValid = _editorTiles.CellAt(nozzle) is not { HasFloor: true };
        var noOverlap = !EngineFootprintTiles(control, facing).Any(_editorEngineFootprint.ContainsKey);

        void DrawTile(TileCoord coord, bool valid)
        {
            var rect = EditorTileRect(coord);
            _spriteBatch.Draw(_pixel, rect, (valid && noOverlap ? new Color(90, 160, 110) : new Color(160, 90, 90)) * 0.35f);
            DrawRectOutline(rect, valid && noOverlap ? Color.LightGreen : Color.OrangeRed, 2f);
        }
        DrawTile(control, controlValid);
        DrawTile(bulkhead, bulkheadValid);
        DrawTile(nozzle, nozzleValid);
    }

    // M81 - live ghost preview for the Compartment tool, same green/red valid-placement convention
    // DrawEditorDevicePlacementPreview/DrawEditorEnginePlacementPreview already use, but checked
    // against a throwaway TileGrid.Clone() (M77's own clone, never the real _editorTiles) via a real
    // speculative CompartmentPlacer.Stamp call each frame rather than hand-duplicating Stamp's own
    // overlap/nozzle-clearance rules here - one simple rectangle over the whole rotated W x H
    // footprint, not per-tile boxes (unlike the Engine tool's 3 separately-validated tiles, a
    // compartment's placement is a single all-or-nothing Stamp call, so one box reads correctly).
    private void DrawEditorCompartmentPlacementPreview()
    {
        if (_editorSelectedCompartmentId is not { } compartmentId)
            return;
        if (CompartmentCatalog.Find(compartmentId) is not { } entry)
            return;
        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var hovered = new TileCoord(cell.X, cell.Y);

        var rotated = CompartmentPlacer.Rotate(entry, _editorCompartmentPendingRotation);
        var anchor = new TileCoord(hovered.X - rotated.Width / 2, hovered.Y - rotated.Height / 2);

        var speculative = _editorTiles.Clone();
        var result = CompartmentPlacer.Stamp(speculative, entry, anchor, _editorCompartmentPendingRotation, "preview");
        var valid = result.Success;

        var fill = (valid ? new Color(90, 160, 110) : new Color(160, 90, 90)) * 0.35f;

        // Direct user request - the preview used to draw one plain bounding-box rectangle, which
        // for a non-rectangular entry (e.g. reactor-d's cut corners) covered tiles the compartment
        // wouldn't actually occupy. Draws each of the entry's own FootprintRects pieces instead, so
        // the preview matches the real shape CompartmentPlacer.Stamp above just placed.
        //
        // Fill only here, no per-piece outline: stroking each piece's own rectangle separately drew
        // a stray line straight across the interior at every internal seam between two pieces (e.g.
        // reactor-d's top band meeting its middle band) - there is no real wall there, so a visible
        // line reads as a false "cut" right through open floor. The wall-tile pass below already
        // outlines every genuine wall tile, which is the whole exterior boundary by definition - a
        // separate outline here would only ever duplicate that or draw a false one on a seam.
        foreach (var footprintRect in rotated.FootprintRects)
        {
            var pieceTopLeft = new TileCoord(anchor.X + (int)footprintRect.X, anchor.Y + (int)footprintRect.Y);
            var pieceBottomRight = new TileCoord(
                anchor.X + (int)footprintRect.Right - 1,
                anchor.Y + (int)footprintRect.Bottom - 1);
            var topLeft = EditorTileRect(pieceTopLeft);
            var bottomRight = EditorTileRect(pieceBottomRight);
            var rect = new Rectangle(topLeft.X, topLeft.Y, bottomRight.Right - topLeft.X, bottomRight.Bottom - topLeft.Y);
            _spriteBatch.Draw(_pixel, rect, fill);
        }

        // Direct user request - two clearly DIFFERENT colours in the preview, one for wall tiles and
        // one for the interior, not two shades of the same tint. Reads the same speculative Stamp
        // result already computed above (real rotation/overlap/airlock rules included, not
        // re-derived here) tile by tile: a wall tile's own fill is drawn near-opaque specifically so
        // it visually overrides the green/red interior fill underneath instead of just tinting it,
        // reading as an unmistakably different colour rather than a subtle blend.
        foreach (var footprintRect in rotated.FootprintRects)
            for (var x = (int)footprintRect.X; x < (int)footprintRect.Right; x++)
                for (var y = (int)footprintRect.Y; y < (int)footprintRect.Bottom; y++)
                {
                    var coord = new TileCoord(anchor.X + x, anchor.Y + y);
                    if (speculative.CellAt(coord) is not { Wall: TileWallKind.Solid or TileWallKind.Door })
                        continue;
                    var tileRect = EditorTileRect(coord);
                    _spriteBatch.Draw(_pixel, tileRect, CompartmentPreviewWallTint * 0.9f);
                    DrawRectOutline(tileRect, Color.White, 1.5f);
                }
    }

    // A device with its own real texture draws that texture stretched across its WHOLE footprint
    // instead of the plain tinted-box+glyph icon every other device still gets - direct user request
    // ("сама текстура должна занимать все 4 на 4 тайла", then generalized: "сделай чтобы текстуры
    // устройств у которых они есть выводились в редакторе как это сделано с реактором"). Three
    // tiers, checked in order: (1) Reactor's own bespoke big texture (_editorReactorTexture,
    // ReactorTexture.cs - a different system entirely from DeviceSkin, unique art baked once at load);
    // (2) any OTHER kind FaceForKind maps to a real DeviceSkin face (the same palette-icon lookup
    // DrawItemArt uses, just baked at THIS footprint's own pixel size instead of a fixed palette-
    // button size, and stretched non-uniformly if the footprint isn't square - an accepted minor
    // trade-off, same as Reactor never needing it since its footprint is always 4x4); (3) still
    // Generic (no real face exists yet) - the original flat tinted-panel+glyph fallback, unchanged.
    private void DrawEditorDeviceAt(TileCoord anchor, CustomDeviceKind kind)
    {
        var rotated = _editorDeviceRotation.TryGetValue(anchor, out var rotatedFlag) && rotatedFlag;
        var (width, height) = DeviceFootprintSize(kind, rotated);
        Rectangle FullFootprintRect()
        {
            var topLeft = EditorTileRect(anchor);
            var bottomRight = EditorTileRect(new TileCoord(anchor.X + width - 1, anchor.Y + height - 1));
            return new Rectangle(topLeft.X, topLeft.Y, bottomRight.Right - topLeft.X, bottomRight.Bottom - topLeft.Y);
        }

        if ((width > 1 || height > 1) && kind == CustomDeviceKind.Reactor && _editorReactorTexture is { } reactorTex)
        {
            _spriteBatch.Draw(reactorTex, FullFootprintRect(), Color.White);
            return;
        }

        var face = FaceForKind(kind);
        if (face != DeviceSkin.Face.Generic)
        {
            var fullRect = FullFootprintRect();
            // Direct user request ("текстура была в соответствии с этой формой") - baked at the
            // footprint's OWN width/height now, not a square stretched to fit it.
            var baked = DeviceIconSkin.Get(face, fullRect.Width, fullRect.Height, lit: true);
            _spriteBatch.Draw(baked, fullRect, Color.White);
            return;
        }

        if (width > 1 || height > 1)
        {
            // A multi-tile device with no real face yet - a tinted panel spanning the WHOLE
            // footprint plus a centered glyph, rather than a tiny icon sitting on just its own
            // anchor tile leaving the rest of its own footprint looking like bare, unexplained floor.
            var fullRect = FullFootprintRect();
            _spriteBatch.Draw(_pixel, fullRect, CustomDeviceCatalog.Tint(kind) * 0.55f);
            DrawRectOutline(fullRect, Color.Black, 1f);
            var bigGlyph = CustomDeviceCatalog.ShortGlyph(kind);
            var bigGlyphSize = _font.MeasureString(bigGlyph) * 0.7f;
            _spriteBatch.DrawString(_font, bigGlyph, new Vector2(fullRect.Center.X - bigGlyphSize.X / 2f, fullRect.Center.Y - bigGlyphSize.Y / 2f),
                Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
            return;
        }

        var rect = EditorTileRect(anchor);
        const int size = 18;
        var box = new Rectangle(rect.Center.X - size / 2, rect.Center.Y - size / 2, size, size);
        _spriteBatch.Draw(_pixel, box, CustomDeviceCatalog.Tint(kind));
        DrawRectOutline(box, Color.Black, 1f);
        var glyph = CustomDeviceCatalog.ShortGlyph(kind);
        var glyphSize = _font.MeasureString(glyph) * 0.5f;
        _spriteBatch.DrawString(_font, glyph, new Vector2(box.Center.X - glyphSize.X / 2f, box.Center.Y - glyphSize.Y / 2f),
            Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    // The engine's 3-tile line (Control/Bulkhead/Nozzle, see HandleEngineToolInput's own doc comment)
    // needs no NEW drawing for the Bulkhead tile - it's already rendered as an ordinary wall by
    // DrawEditorWallTile above, since placing an engine never touches that tile's own Wall field.
    // Control gets the same flat tinted-box+glyph style every non-Reactor device already uses
    // (DrawEditorDeviceAt); Nozzle gets a small warm "exhaust" swatch so the facing reads at a glance
    // without needing bespoke rotated art (direct user decision - a plain tint is good enough here).
    private void DrawEditorEngineAt(TileCoord control, TileSide facing)
    {
        var controlRect = EditorTileRect(control);
        const int size = 18;
        var box = new Rectangle(controlRect.Center.X - size / 2, controlRect.Center.Y - size / 2, size, size);
        _spriteBatch.Draw(_pixel, box, new Color(90, 160, 220));
        DrawRectOutline(box, Color.Black, 1f);
        var glyph = "Дв";
        var glyphSize = _font.MeasureString(glyph) * 0.4f;
        _spriteBatch.DrawString(_font, glyph, new Vector2(box.Center.X - glyphSize.X / 2f, box.Center.Y - glyphSize.Y / 2f),
            Color.White, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);

        var nozzleCoord = facing.Offset(facing.Offset(control));
        var nozzleRect = EditorTileRect(nozzleCoord);
        var nozzleBox = new Rectangle(nozzleRect.Center.X - 6, nozzleRect.Center.Y - 6, 12, 12);
        _spriteBatch.Draw(_pixel, nozzleBox, new Color(220, 140, 60));
        DrawRectOutline(nozzleBox, Color.Black, 1f);
    }

    private void DrawEditorZone(EditorZone zone)
    {
        if (zone.Tiles.Count == 0)
            return;
        // A typed zone (direct user request - all 4 described types) tints differently per kind so
        // the player can tell them apart on the canvas at a glance; an untyped/custom-named zone
        // keeps the original amber tint.
        var tint = (zone.Kind switch
        {
            ShipZoneKind.ReactorRoom => new Color(255, 140, 90),
            ShipZoneKind.MedicalBay => new Color(120, 220, 160),
            ShipZoneKind.EngineeringBay => new Color(140, 170, 255),
            ShipZoneKind.ControlRoom => new Color(230, 200, 90),
            _ => new Color(255, 200, 90),
        }) * 0.15f;
        foreach (var coord in zone.Tiles)
            _spriteBatch.Draw(_pixel, EditorTileRect(coord), tint);

        var avgX = (float)zone.Tiles.Average(t => t.X + 0.5f);
        var avgY = (float)zone.Tiles.Average(t => t.Y + 0.5f);
        var pos = WorldToEditorScreen(avgX, avgY);
        var labelSize = _font.MeasureString(zone.Name) * 0.5f;
        _spriteBatch.DrawString(_font, zone.Name, pos - labelSize / 2f, Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    // Live preview while dragging - green where the tile has floor (and no device already sitting on
    // it, direct user request - "на месте которое занимает устройство уже ничего нельзя было
    // построить") and would actually take a wall, red where it wouldn't, same colour convention the
    // old Room-rectangle preview used for "would this placement be valid."
    private void DrawWallPlacementHighlight(Rectangle rect, bool valid)
    {
        var color = valid ? Color.LightGreen : Color.OrangeRed;
        if (!_editorWallHalfBlock)
        {
            _spriteBatch.Draw(_pixel, rect, (valid ? new Color(90, 160, 110) : new Color(160, 90, 90)) * 0.4f);
            DrawRectOutline(rect, color, 2f);
            return;
        }
        var solidHalf = EditorHalfRect(rect, _editorWallHalfBlockSide);
        DrawRectOutline(rect, color * 0.35f, 1f);
        _spriteBatch.Draw(_pixel, solidHalf, (valid ? new Color(90, 160, 110) : new Color(160, 90, 90)) * 0.55f);
        DrawRectOutline(solidHalf, color, 2f);
    }

    // Direct user request ("сделай чтобы при её выставлении было видно как она повернута") - both
    // branches below now special-case _editorWallHalfBlock: instead of highlighting the WHOLE tile
    // (which never told the player which half was actually about to become solid until AFTER
    // clicking), only the solid half (per _editorWallHalfBlockSide) gets the strong valid/invalid
    // fill+outline, with a faint full-tile outline underneath so the free half still reads as part
    // of the same placement rather than looking untouched.
    private void DrawEditorWallDragPreview()
    {
        if (_editorWallDragStart is not { } start)
        {
            // Same single-cell hover fallback as the Floor tool's own preview above.
            if (GridCellAt(_designMouse) is { } hover)
            {
                var hoverCoord = new TileCoord(hover.X, hover.Y);
                var hoverRect = EditorTileRect(hoverCoord);
                var hoverValid = _editorTiles.CellAt(hoverCoord) is { HasFloor: true, DeviceId: null };
                DrawWallPlacementHighlight(hoverRect, hoverValid);
            }
            return;
        }
        var endCell = GridCellAt(_designMouse) is { } ec ? new TileCoord(ec.X, ec.Y) : start;
        foreach (var coord in LineBetween(start, endCell))
        {
            var rect = EditorTileRect(coord);
            var valid = _editorTiles.CellAt(coord) is { HasFloor: true, DeviceId: null };
            DrawWallPlacementHighlight(rect, valid);
        }
    }

    // Wide-door mode's own drag preview (direct user request) - same shape as the Wall tool's line
    // preview, but capped to the first 2 tiles only (HandleDoorToolInput never links more than that).
    // Direct user request ("расставлял их как устройства со своим размером") - same live per-tile
    // green/red preview Engine's own DrawEditorEnginePlacementPreview uses, shown before any click
    // (no drag any more) so hovering alone already previews exactly what DoorSpanTiles/
    // HandleDoorToolInput would place - or, over an existing door, what a right-click would remove.
    private void DrawEditorDoorPlacementPreview()
    {
        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var anchor = new TileCoord(cell.X, cell.Y);

        // Span==2 alone still tries the OLD tile-based compartment-boundary hover-removal preview
        // first (direct user request - "расставлял их как устройства со своим размером", same
        // convenience _editorDoorFootprint gives the right-click itself) - every other span never
        // creates one of these tiles at all, so this can never fire for them.
        if (_editorDoorSpanTiles == 2
            && (_editorDoorFootprint.TryGetValue(anchor, out var doorAnchor) || _editorTiles.CellAt(anchor) is { Wall: TileWallKind.Door }))
        {
            var removeAnchor = _editorDoorFootprint.TryGetValue(anchor, out var resolved) ? resolved : anchor;
            var removeSpan = _editorTiles.CellAt(removeAnchor)?.DoorGroupId is { } groupId
                ? _editorTiles.Cells.Where(kv => kv.Value.DoorGroupId == groupId).Select(kv => kv.Key)
                : new[] { removeAnchor };
            foreach (var barrier in removeSpan)
                foreach (var coord in DoorFlankingTiles(barrier, ResolveDoorTileVertical(barrier)).Append(barrier))
                {
                    var removeRect = EditorTileRect(coord);
                    _spriteBatch.Draw(_pixel, removeRect, new Color(90, 160, 110) * 0.35f);
                    DrawRectOutline(removeRect, Color.LightGreen, 2f);
                }
            return;
        }

        // M-doors-as-edges - hovering an already-placed edge door (narrow/wide/triple, N segments
        // sharing one Id - DoorEdgeGroupAt's own doc comment) previews removing every segment at
        // once, same priority the OLD-tile hover-removal check just above already gives its own kind
        // of door.
        var existingGroup = DoorEdgeGroupAt(anchor);
        if (existingGroup.Count > 0)
        {
            foreach (var (coord, side) in existingGroup)
            {
                DrawRectOutline(EditorTileRect(coord), Color.LightGreen, 2f);
                DrawRectOutline(EditorTileRect(side.Offset(coord)), Color.LightGreen, 2f);
            }
            return;
        }

        // Ordinary placement preview - mirrors PlaceEdgeDoor's own geometry exactly (a genuine
        // spanTiles-by-2 free-floor footprint, never a tile of its own - the OLD "click on floor or
        // wall" tile-conversion preview is gone along with the placement it used to describe).
        var side2 = _editorDoorPendingVertical ? TileSide.South : TileSide.East;
        var spanOffset = side2 is TileSide.East or TileSide.West ? new TileCoord(0, 1) : new TileCoord(1, 0);
        var anchors = Enumerable.Range(0, _editorDoorSpanTiles)
            .Select(i => new TileCoord(anchor.X + spanOffset.X * i, anchor.Y + spanOffset.Y * i))
            .ToList();
        var valid = anchors.All(a => _editorTiles.CanPlaceDoorEdge(a, side2));
        var color = valid ? Color.LightGreen : Color.OrangeRed;
        var fill = (valid ? new Color(90, 160, 110) : new Color(160, 90, 90)) * 0.35f;
        foreach (var a in anchors)
            foreach (var t in new[] { a, side2.Offset(a) })
            {
                var rect = EditorTileRect(t);
                _spriteBatch.Draw(_pixel, rect, fill);
                DrawRectOutline(rect, color, 2f);
            }

        const float thicknessFraction = 0.22f;
        var thickness = Math.Max(4, (int)(thicknessFraction * EditorCellSize));
        var barColor = (valid ? new Color(90, 230, 120) : new Color(255, 90, 90)) * 0.8f;
        foreach (var a in anchors)
        {
            var anchorRect = EditorTileRect(a);
            var bar = side2 == TileSide.East
                ? new Rectangle(anchorRect.Right - thickness / 2, anchorRect.Y, thickness, anchorRect.Height)
                : new Rectangle(anchorRect.X, anchorRect.Bottom - thickness / 2, anchorRect.Width, thickness);
            _spriteBatch.Draw(_pixel, bar, barColor);
        }
    }

    private void DrawEditorZoneDragPreview()
    {
        if (_editorZoneDragStart is not { } start)
        {
            // Same single-cell hover fallback as the Floor/Wall tools' own previews above.
            if (GridCellAt(_designMouse) is { } hover)
            {
                var hoverRect = EditorTileRect(new TileCoord(hover.X, hover.Y));
                _spriteBatch.Draw(_pixel, hoverRect, new Color(255, 200, 90) * 0.25f);
                DrawRectOutline(hoverRect, Color.Gold, 2f);
            }
            return;
        }
        var endCell = GridCellAt(_designMouse) ?? start;
        var minX = Math.Min(start.X, endCell.X);
        var minY = Math.Min(start.Y, endCell.Y);
        var maxX = Math.Max(start.X, endCell.X);
        var maxY = Math.Max(start.Y, endCell.Y);
        var rect = new Rectangle(
            ShipEditorCanvas.X + minX * EditorCellSize - _editorPanOffset.X,
            ShipEditorCanvas.Y + minY * EditorCellSize - _editorPanOffset.Y,
            (maxX - minX + 1) * EditorCellSize, (maxY - minY + 1) * EditorCellSize);
        _spriteBatch.Draw(_pixel, rect, new Color(255, 200, 90) * 0.25f);
        DrawRectOutline(rect, Color.Gold, 2f);
    }

    private void DrawRectOutline(Rectangle rect, Color color, float thickness)
    {
        HudIcons.DrawLine(_spriteBatch, _pixel, new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top), color, thickness);
        HudIcons.DrawLine(_spriteBatch, _pixel, new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Right, rect.Bottom), color, thickness);
        HudIcons.DrawLine(_spriteBatch, _pixel, new Vector2(rect.Left, rect.Top), new Vector2(rect.Left, rect.Bottom), color, thickness);
        HudIcons.DrawLine(_spriteBatch, _pixel, new Vector2(rect.Right, rect.Top), new Vector2(rect.Right, rect.Bottom), color, thickness);
    }

    // Direct user request ("сделай чтобы игрок всегда находился в режиме строительства где все
    // блоки, а текущие 6 вкладок справа полностью удали") - the old vertical Пол/Стена/Дверь/
    // Терминал/Устройства/Зоны tool-picker sidebar is gone; the bottom device-tab panel (Game1.
    // ShipEditor.DeviceTabs.cs) is now the ONLY way to pick what gets placed, always visible, never
    // gated behind selecting "Устройства" first. Floor and Zone (which never had a CustomDeviceKind
    // or even a tab of their own before) now live under that panel's own "МОДИФИКАЦИИ" mode instead
    // of "ОБЪЕКТЫ" - see DrawDeviceTabs's own doc comment for why the split.
    private void DrawEditorSidebar()
    {
        var hint = _editorTool switch
        {
            EditorTool.Floor => "Клик - поставить пол. ПКМ - убрать.",
            EditorTool.Wall => "Клик - стена (нужен пол под ней). Зажать и протянуть - линия стен. ПКМ - убрать.",
            EditorTool.Door when _editorDoorSpanTiles == 2 => "R - выбрать ориентацию. Клик на стыке 2х2 свободных клеток пола - широкая дверь; клик на стыке 2 отсеков - тоже дверь. ПКМ по двери - убрать.",
            EditorTool.Door when _editorDoorSpanTiles == 3 => "R - выбрать ориентацию. Клик на стыке 3х2 свободных клеток пола - тройная дверь. ПКМ по двери - убрать.",
            EditorTool.Door => "R - выбрать ориентацию. Клик на стыке 2 свободных клеток пола - дверь. ПКМ по двери - убрать.",
            EditorTool.Terminal => "R - выбрать сторону крепления. Клик на полутолщинной стене - врезать в неё. Клик по полу рядом со стеной - поставить снаружи. ПКМ - убрать.",
            EditorTool.Device => "Клик внутри отсека - поставить устройство. ПКМ рядом - убрать.",
            EditorTool.Zone => "Зажмите и протяните по клеткам с полом, затем впишите название отсека.",
            EditorTool.Engine => $"R - повернуть (сейчас: {EngineFacingLabel(_editorEnginePendingFacing)}). " +
                "Клик - поставить (нужна стена в сторону сопла). ПКМ - убрать.",
            EditorTool.Compartment => "R - повернуть отсек. Клик - поставить целиком (пол+стены+устройства). ПКМ по отсеку - убрать целиком.",
            _ => "",
        };
        _spriteBatch.DrawString(_font, hint, new Vector2(DevicePanelLeft, DeviceItemsTop - 22), Color.Gray, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

        DrawDeviceTabs();
    }

    private static readonly string[] EditorForwardArrowLabels = { "→", "↓", "←", "↑" };

    private static string EngineFacingLabel(TileSide side) => side switch
    {
        TileSide.North => "Север",
        TileSide.South => "Юг",
        TileSide.East => "Восток",
        _ => "Запад",
    };

    // Direct user request ("часть меню на скрине была сверху, а менюшка со всеми блоками в самом
    // низу") - Название/Нос/статус/action buttons moved up near the title, out of the device-tab
    // panel's way at the bottom (ShipEditorCanvas's own doc comment). Still called
    // "DrawEditorBottomBar" for history's sake - nothing about what it draws changed, only where.
    private void DrawEditorBottomBar()
    {
        _spriteBatch.DrawString(_font, $"Название: {_editorShipName}", new Vector2(20, 34), Color.LightGray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        _spriteBatch.DrawString(_font, "Нос:", new Vector2(300, 34), Color.Gray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        for (var i = 0; i < EditorForwardOptions.Length; i++)
        {
            var rect = GetEditorForwardArrowRect(i);
            var selected = MathHelperNearlyEqual(_editorForwardDegrees, EditorForwardOptions[i]);
            _spriteBatch.Draw(_pixel, rect, selected ? new Color(120, 92, 30) : Color.DimGray * 0.6f);
            _spriteBatch.DrawString(_font, EditorForwardArrowLabels[i], new Vector2(rect.X + 9, rect.Y + 2),
                Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        }

        var (_, errors) = BuildAndValidateEditorDefinition();
        var status = errors.Count == 0 ? "Готов к игре!" : errors[0];
        _spriteBatch.DrawString(_font, status, new Vector2(500, 34),
            errors.Count == 0 ? Color.LightGreen : Color.OrangeRed, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        DrawEditorActionButton(EditorAction.Back, "НАЗАД", enabled: true);
        DrawEditorActionButton(EditorAction.New, "НОВЫЙ", enabled: true);
        DrawEditorActionButton(EditorAction.Save, "СОХРАНИТЬ", enabled: true);
        DrawEditorActionButton(EditorAction.SaveAs, "СОХР. КАК", enabled: true);
        DrawEditorActionButton(EditorAction.Load, "ЗАГРУЗИТЬ", enabled: true);
        DrawEditorActionButton(EditorAction.Play, "ИГРАТЬ", enabled: errors.Count == 0);
    }

    private void DrawEditorActionButton(EditorAction action, string label, bool enabled)
    {
        var rect = GetEditorActionRect(action);
        var hovered = enabled && rect.Contains(_designMouse);
        _spriteBatch.Draw(_pixel, rect, !enabled ? Color.DimGray * 0.3f : hovered ? new Color(120, 92, 30) : Color.DimGray * 0.6f);
        _spriteBatch.DrawString(_font, label, new Vector2(rect.X + 8, rect.Y + 5),
            enabled ? Color.White : Color.Gray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    private static bool MathHelperNearlyEqual(float a, float b) => Math.Abs(a - b) < 0.01f;
}
