using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client;

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

        for (var i = 0; i < names.Count; i++)
        {
            var rowRect = GetEditorLoadRowRect(i);
            var current = names[i] == _editorCurrentSlotName;
            _spriteBatch.Draw(_pixel, rowRect, current ? new Color(120, 92, 30) * 0.6f : Color.DimGray * 0.4f);
            DrawRectOutline(rowRect, current ? Color.White : Color.DimGray, 1f);
            _spriteBatch.DrawString(_font, names[i], new Vector2(rowRect.X + 8, rowRect.Y + 4), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

            var deleteRect = GetEditorLoadRowDeleteRect(i);
            _spriteBatch.Draw(_pixel, deleteRect, new Color(120, 50, 50));
            DrawRectOutline(deleteRect, Color.OrangeRed, 1f);
            _spriteBatch.DrawString(_font, "X", new Vector2(deleteRect.X + 14, deleteRect.Y + 4), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
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
        if (_editorTool == EditorTool.Door && _editorDoorWide)
            DrawEditorDoorDragPreview();
        else if (_editorTool == EditorTool.Door)
            DrawEditorDoorHoverPreview();
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
            if (cell.TerminalId is not null && cell.TerminalWallSide is { } side)
                DrawEditorTerminalMark(coord, side);
            if (cell.DeviceId is not null && _editorDeviceKinds.TryGetValue(coord, out var kind))
                DrawEditorDeviceAt(coord, kind);
        }

        foreach (var (control, facing) in _editorEngineFacing)
            DrawEditorEngineAt(control, facing);

        foreach (var zone in _editorZones)
            DrawEditorZone(zone);
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
    private static Color WallMaterialTint(WallMaterial material) => material switch
    {
        WallMaterial.Reinforced => new Color(150, 155, 165),
        WallMaterial.Window => new Color(150, 215, 235) * 0.75f,
        _ => Color.White,
    };

    private void DrawEditorWallTile(TileCoord coord)
    {
        bool HasWall(TileSide side) => _editorTiles.CellAt(side.Offset(coord)) is { Wall: TileWallKind.Solid or TileWallKind.Door };
        var north = HasWall(TileSide.North);
        var south = HasWall(TileSide.South);
        var east = HasWall(TileSide.East);
        var west = HasWall(TileSide.West);
        var rect = EditorTileRect(coord);
        var tint = WallMaterialTint(_editorTiles.CellAt(coord)?.WallMaterial ?? WallMaterial.Standard);

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

        _spriteBatch.Draw(_pixel, rect, tint == Color.White ? new Color(120, 130, 150) : tint);
        DrawRectOutline(rect, Color.Black, 1f);
    }

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
                _shipRenderer.DrawDoor(_spriteBatch, merged, isOpen: false);
                return;
            }
        }

        var rect = EditorTileRect(coord);
        _shipRenderer.DrawDoor(_spriteBatch, rect, isOpen: false);
    }

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

        TileSide? mountSide = null;
        if (_editorTiles.CellAt(coord) is { HasFloor: true, TerminalId: null } && !IsAtConstructionJunction(coord))
        {
            foreach (var candidateSide in TileSideExtensions.All)
            {
                if (_editorTiles.CellAt(candidateSide.Offset(coord)) is not { Wall: not TileWallKind.None })
                    continue;
                mountSide = candidateSide;
                break;
            }
        }

        var valid = mountSide is not null;
        _spriteBatch.Draw(_pixel, rect, (valid ? new Color(90, 160, 110) : new Color(160, 90, 90)) * 0.35f);
        DrawRectOutline(rect, valid ? Color.LightGreen : Color.OrangeRed, 2f);
        if (mountSide is { } side)
            DrawEditorTerminalMark(coord, side);
    }

    private void DrawEditorTerminalMark(TileCoord coord, TileSide side)
    {
        var rect = EditorTileRect(coord);
        var (from, to) = side switch
        {
            TileSide.North => (new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top)),
            TileSide.South => (new Vector2(rect.Left, rect.Bottom), new Vector2(rect.Right, rect.Bottom)),
            TileSide.East => (new Vector2(rect.Right, rect.Top), new Vector2(rect.Right, rect.Bottom)),
            _ => (new Vector2(rect.Left, rect.Top), new Vector2(rect.Left, rect.Bottom)),
        };
        HudIcons.DrawLine(_spriteBatch, _pixel, from, to, Color.Gold, 3f);
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
        var size = CustomDeviceFootprint.Size(_editorSelectedDeviceKind);
        var anchor = FootprintAnchorFor(hovered, size);
        var footprint = DeviceFootprintTiles(anchor, size).ToList();
        var valid = footprint.All(t => _editorTiles.CellAt(t) is { HasFloor: true, Wall: TileWallKind.None, DeviceId: null });

        var topLeft = EditorTileRect(anchor);
        var bottomRight = EditorTileRect(new TileCoord(anchor.X + size - 1, anchor.Y + size - 1));
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

        var topLeft = EditorTileRect(anchor);
        var bottomRight = EditorTileRect(new TileCoord(anchor.X + rotated.Width - 1, anchor.Y + rotated.Height - 1));
        var rect = new Rectangle(topLeft.X, topLeft.Y, bottomRight.Right - topLeft.X, bottomRight.Bottom - topLeft.Y);
        _spriteBatch.Draw(_pixel, rect, (valid ? new Color(90, 160, 110) : new Color(160, 90, 90)) * 0.35f);
        DrawRectOutline(rect, valid ? Color.LightGreen : Color.OrangeRed, 2f);
    }

    // A multi-tile device (today, only Reactor - CustomDeviceFootprint.Size) draws its own real texture
    // stretched across its WHOLE footprint instead of the plain small icon every other device still
    // gets - direct user request ("сама текстура должна занимать все 4 на 4 тайла").
    private void DrawEditorDeviceAt(TileCoord anchor, CustomDeviceKind kind)
    {
        var footprintSize = CustomDeviceFootprint.Size(kind);
        if (footprintSize > 1 && kind == CustomDeviceKind.Reactor && _editorReactorTexture is { } reactorTex)
        {
            var topLeft = EditorTileRect(anchor);
            var bottomRight = EditorTileRect(new TileCoord(anchor.X + footprintSize - 1, anchor.Y + footprintSize - 1));
            var fullRect = new Rectangle(topLeft.X, topLeft.Y, bottomRight.Right - topLeft.X, bottomRight.Bottom - topLeft.Y);
            _spriteBatch.Draw(reactorTex, fullRect, Color.White);
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
                _spriteBatch.Draw(_pixel, hoverRect, (hoverValid ? new Color(90, 160, 110) : new Color(160, 90, 90)) * 0.4f);
                DrawRectOutline(hoverRect, hoverValid ? Color.LightGreen : Color.OrangeRed, 2f);
            }
            return;
        }
        var endCell = GridCellAt(_designMouse) is { } ec ? new TileCoord(ec.X, ec.Y) : start;
        foreach (var coord in LineBetween(start, endCell))
        {
            var rect = EditorTileRect(coord);
            var valid = _editorTiles.CellAt(coord) is { HasFloor: true, DeviceId: null };
            _spriteBatch.Draw(_pixel, rect, (valid ? new Color(90, 160, 110) : new Color(160, 90, 90)) * 0.4f);
            DrawRectOutline(rect, valid ? Color.LightGreen : Color.OrangeRed, 2f);
        }
    }

    // Wide-door mode's own drag preview (direct user request) - same shape as the Wall tool's line
    // preview, but capped to the first 2 tiles only (HandleDoorToolInput never links more than that).
    private void DrawEditorDoorDragPreview()
    {
        if (_editorDoorDragStart is not { } start)
            return;
        var endCell = GridCellAt(_designMouse) is { } ec ? new TileCoord(ec.X, ec.Y) : start;
        foreach (var coord in LineBetween(start, endCell).Take(2))
        {
            var rect = EditorTileRect(coord);
            var valid = _editorTiles.CellAt(coord) is { HasFloor: true, DeviceId: null };
            _spriteBatch.Draw(_pixel, rect, (valid ? new Color(90, 160, 110) : new Color(160, 90, 90)) * 0.4f);
            DrawRectOutline(rect, valid ? Color.LightGreen : Color.OrangeRed, 2f);
        }
    }

    // Door tool has no drag, just a single hovered tile - shown even before a click (unlike Wall's
    // drag-only preview above) so hovering an occupied device tile reads as blocked right away, same
    // guard HandleDoorToolInput itself checks.
    private void DrawEditorDoorHoverPreview()
    {
        if (GridCellAt(_designMouse) is not { } cell)
            return;
        var coord = new TileCoord(cell.X, cell.Y);
        var rect = EditorTileRect(coord);
        var current = _editorTiles.CellAt(coord);
        var valid = current is { HasFloor: true, DeviceId: null };
        var removable = current is { Wall: TileWallKind.Door };
        if (!valid && !removable)
        {
            _spriteBatch.Draw(_pixel, rect, new Color(160, 90, 90) * 0.4f);
            DrawRectOutline(rect, Color.OrangeRed, 2f);
        }
        else
        {
            _spriteBatch.Draw(_pixel, rect, new Color(90, 160, 110) * 0.35f);
            DrawRectOutline(rect, Color.LightGreen, 2f);
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
            EditorTool.Door when _editorDoorWide => "Зажмите и протяните ровно 2 клетки - широкая дверь. ПКМ по двери - убрать.",
            EditorTool.Door => "Клик по полу или стене - дверь. ПКМ по двери - убрать.",
            EditorTool.Terminal => "Клик по полу рядом со стеной - терминал. ПКМ - убрать.",
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
