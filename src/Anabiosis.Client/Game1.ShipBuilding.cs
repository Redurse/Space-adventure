using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// The Shipwright's build tools, client side. Three tools (the tabs of StationBuildPanel):
//   * Compartments - pick a compartment from the catalog, then place it on the ship's tile grid: the ghost follows the cursor snapped
//     to tiles (green where it can go, red where it cannot), R turns it a quarter, a click confirms (the server re-checks);
//   * Doors - single/double/triple junction door on a half-block wall joint (R turns it, 1-3 or the wheel pick the width), a right
//     click on a door placed in-game takes it out;
//   * Demolish - click a compartment to take it down for half its price back (docked at a Shipwright).
// The placement rules are the editor's own (CompartmentBuilder / JunctionDoor), evaluated here on the client's copy of the tile grid
// purely for instant feedback; the server never trusts them.
public partial class Game1
{
    private BuildTool _buildTool = BuildTool.Compartments;
    private string? _placingCompartmentId;
    private int _placingRotation;
    private int _doorSpan = 1;
    private TileSide _doorPassage = TileSide.East;
    private BuildCompartmentRequest? _pendingBuildCompartment;
    private PlaceDoorRequest? _pendingPlaceDoor;
    private string? _pendingRemoveDoorId;
    private ShipBuildOverlay _buildOverlay = null!;
    private TileGrid? _buildTiles;
    private int _buildTilesKey;
    private string? _buildMessage;
    private string? _buildMessageToShow;
    private bool _prevBuildRotateKeyDown;

    // Any build tool is in use: a compartment picked, or the doors / demolish tab open.
    private bool BuildToolActive => _placingCompartmentId is not null || _buildTool != BuildTool.Compartments;

    private bool BuildPanelShowing(WorldSnapshot snapshot) =>
        BuildToolActive || snapshot.Station.Npcs.FirstOrDefault(n => n.Id == _talkingToNpcId)?.Kind == NpcKind.Shipwright;

    private void LeaveBuildTool()
    {
        _placingCompartmentId = null;
        _placingRotation = 0;
        _buildTool = BuildTool.Compartments;
        _buildMessage = null;
    }

    // The client's copy of the ship's tile grid, rebuilt only when the hull's structure changes.
    private TileGrid BuildTilesFor(WorldSnapshot snapshot)
    {
        var key = HashCode.Combine(ClientTileGrid.ComputeStructuralFingerprint(snapshot.Rooms, snapshot.Doors),
            snapshot.DoorEdges?.Count ?? 0, snapshot.ForcedFloorTiles?.Count ?? 0, snapshot.WallOpenSideOverrides?.Count ?? 0,
            snapshot.SupplementalWallTiles?.Count ?? 0);
        if (_buildTiles is null || key != _buildTilesKey)
        {
            _buildTiles = ClientTileGrid.Build(snapshot);
            _buildTilesKey = key;
        }
        return _buildTiles;
    }

    private static IReadOnlySet<TileCoord> PendingBuildTiles(WorldSnapshot snapshot)
    {
        var tiles = new HashSet<TileCoord>();
        foreach (var pending in snapshot.PendingRoomBuilds ?? Array.Empty<PendingRoomBuildState>())
            for (var x = (int)MathF.Round(pending.X); x < (int)MathF.Round(pending.X + pending.Width); x++)
                for (var y = (int)MathF.Round(pending.Y); y < (int)MathF.Round(pending.Y + pending.Height); y++)
                    tiles.Add(new TileCoord(x, y));
        return tiles;
    }

    private static TileCoord TileUnder(Vec2 shipLocal) => new((int)MathF.Floor((float)shipLocal.X), (int)MathF.Floor((float)shipLocal.Y));

    // R turns what is being placed, 1/2/3 and the wheel pick a door's width. Called once a frame from Update.
    private void UpdateBuildToolInput(KeyboardState keyboard, int scrollDelta)
    {
        var rDown = keyboard.IsKeyDown(Keys.R);
        if (BuildToolActive && rDown && !_prevBuildRotateKeyDown)
        {
            if (_buildTool == BuildTool.Doors)
                _doorPassage = _doorPassage == TileSide.East ? TileSide.South : TileSide.East;
            else if (_placingCompartmentId is not null)
                _placingRotation = (_placingRotation + 1) % 4;
        }
        _prevBuildRotateKeyDown = rDown;

        if (_buildTool != BuildTool.Doors)
            return;
        if (keyboard.IsKeyDown(Keys.D1)) _doorSpan = 1;
        else if (keyboard.IsKeyDown(Keys.D2)) _doorSpan = 2;
        else if (keyboard.IsKeyDown(Keys.D3)) _doorSpan = 3;
        if (scrollDelta != 0)
            _doorSpan = Math.Clamp(_doorSpan + (scrollDelta > 0 ? 1 : -1), 1, 3);
    }

    // ---- clicks ----

    // A click on the build panel itself (tabs, modules). True when it was the panel's.
    private bool HandleBuildPanelClick(WorldSnapshot snapshot)
    {
        if (!BuildPanelShowing(snapshot))
            return false;

        for (var i = 0; i < StationBuildPanel.TabCount; i++)
        {
            if (!StationBuildPanel.GetTabRect(i, StationBuildPanelOrigin).Contains(_designMouse))
                continue;
            _placingCompartmentId = null;
            _placingRotation = 0;
            _buildMessage = null;
            if (i < StationBuildPanel.Categories.Length)
            {
                _buildTool = BuildTool.Compartments;
                _buildPanelCategory = StationBuildPanel.Categories[i].Category;
            }
            else
                _buildTool = i == StationBuildPanel.DoorsTabIndex ? BuildTool.Doors : BuildTool.Demolish;
            return true;
        }

        if (_buildTool == BuildTool.Compartments)
        {
            var entries = StationBuildPanel.EntriesInCategory(_buildPanelCategory);
            for (var i = 0; i < entries.Count; i++)
                if (StationBuildPanel.GetModuleRect(i, StationBuildPanelOrigin).Contains(_designMouse))
                {
                    _placingCompartmentId = entries[i].Id;
                    _placingRotation = 0;
                    return true;
                }
        }
        else if (_buildTool == BuildTool.Doors)
        {
            for (var span = 1; span <= 3; span++)
                if (StationBuildPanel.GetModuleRect(span - 1, StationBuildPanelOrigin).Contains(_designMouse))
                {
                    _doorSpan = span;
                    return true;
                }
        }

        // Padding between the buttons still belongs to the panel.
        return StationBuildPanel.PanelRect(StationBuildPanelOrigin).Contains(_designMouse);
    }

    // A click on the ship (or station overview) while a build tool is active. True when the tool consumed it.
    private bool HandleBuildWorldClick(WorldSnapshot snapshot, Vector2 origin)
    {
        if (!BuildToolActive)
            return false;
        var local = ScreenToShipLocal(new Vector2(_designMouse.X, _designMouse.Y), origin, SceneZoom(snapshot));
        var tile = TileUnder(local);

        switch (_buildTool)
        {
            case BuildTool.Demolish:
                if (snapshot.Rooms.FirstOrDefault(r => r.Contains(local)) is { } room)
                    _pendingDemolishRoomId = room.Id;
                return true;

            case BuildTool.Doors:
            {
                var plan = JunctionDoor.Plan(BuildTilesFor(snapshot), tile, _doorPassage, _doorSpan, out var failure);
                if (plan is null)
                {
                    _buildMessage = JunctionFailureText(failure);
                    return true;
                }
                if (snapshot.Credits < JunctionDoorBuilder.Price(_doorSpan))
                {
                    _buildMessage = "Не хватает кредитов.";
                    return true;
                }
                _pendingPlaceDoor = new PlaceDoorRequest(tile.X, tile.Y, _doorPassage, _doorSpan);
                _buildMessage = null;
                return true;
            }

            default:
                if (_placingCompartmentId is not { } id || CompartmentCatalog.Find(id) is not { } entry)
                    return false;
                var anchor = CompartmentBuilder.AnchorForCursor(entry, _placingRotation, tile);
                var (compartmentPlan, error) = CompartmentBuilder.Plan(BuildTilesFor(snapshot), PendingBuildTiles(snapshot), entry, anchor, _placingRotation);
                if (compartmentPlan is null)
                {
                    _buildMessage = error;
                    return true;
                }
                if (snapshot.Credits < CompartmentPricing.Price(entry) || snapshot.HullPlatingStock < CompartmentPricing.PlatingCost(entry))
                {
                    _buildMessage = "Не хватает кредитов или обшивки.";
                    return true;
                }
                _pendingBuildCompartment = new BuildCompartmentRequest(id, anchor.X, anchor.Y, _placingRotation);
                _buildMessage = null;
                return true;
        }
    }

    // Right click: take out the in-game door under the cursor, or else leave the tool. True when it did something.
    private bool HandleBuildRightClick(WorldSnapshot snapshot, Vector2 origin)
    {
        if (!BuildToolActive)
            return false;
        if (_buildTool == BuildTool.Doors)
        {
            var local = ScreenToShipLocal(new Vector2(_designMouse.X, _designMouse.Y), origin, SceneZoom(snapshot));
            var door = (snapshot.DoorEdges ?? Array.Empty<ShipDoorEdge>())
                .Where(e => e.Id.StartsWith("jdoor-"))
                .FirstOrDefault(e => (new Vec2(e.Coord.X + 0.5, e.Coord.Y + 0.5) - local).Length() < 0.9);
            if (door is not null)
            {
                _pendingRemoveDoorId = door.Id;
                return true;
            }
        }
        LeaveBuildTool();
        return true;
    }

    // ---- drawing ----

    // Drawn in the ship's frame over the real geometry, so it lines up exactly with what a click means.
    private void DrawBuildOverlay(WorldSnapshot snapshot, Vector2 origin, float zoom)
    {
        if (!BuildToolActive)
        {
            _buildMessageToShow = null;
            return;
        }
        if (StationBuildPanel.PanelRect(StationBuildPanelOrigin).Contains(_designMouse))
            return;
        var local = ScreenToShipLocal(new Vector2(_designMouse.X, _designMouse.Y), origin, zoom);
        var tile = TileUnder(local);
        var tiles = BuildTilesFor(snapshot);
        string? message = _buildMessage;
        _buildMessageToShow = null;

        switch (_buildTool)
        {
            case BuildTool.Demolish:
                if (snapshot.Rooms.FirstOrDefault(r => r.Contains(local)) is { } room)
                    _buildOverlay.DrawRoomHighlight(_spriteBatch, room, origin);
                break;

            case BuildTool.Doors:
                _buildOverlay.DrawGrid(_spriteBatch, tile, origin);
                var plan = JunctionDoor.Plan(tiles, tile, _doorPassage, _doorSpan, out var failure);
                _buildOverlay.DrawDoorPreview(_spriteBatch, tile, plan, origin);
                _buildOverlay.DrawRemovableDoor(_spriteBatch, (snapshot.DoorEdges ?? Array.Empty<ShipDoorEdge>()).Where(e => e.Id.StartsWith("jdoor-")), origin);
                message ??= plan is null && failure != JunctionDoorFailure.NotAWall ? JunctionFailureText(failure) : null;
                break;

            default:
                if (_placingCompartmentId is not { } id || CompartmentCatalog.Find(id) is not { } entry)
                    break;
                _buildOverlay.DrawGrid(_spriteBatch, tile, origin);
                var anchor = CompartmentBuilder.AnchorForCursor(entry, _placingRotation, tile);
                var (compartmentPlan, error) = CompartmentBuilder.Plan(tiles, PendingBuildTiles(snapshot), entry, anchor, _placingRotation);
                _buildOverlay.DrawCompartmentGhost(_spriteBatch, CompartmentBuilder.FootprintTiles(entry, anchor, _placingRotation), compartmentPlan is not null, origin);
                message ??= error;
                break;
        }

        _buildMessageToShow = message; // drawn in the HUD pass, in screen coordinates
    }
}
