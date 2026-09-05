using System;
using System.Linq;
using Microsoft.Xna.Framework;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client;

// What the mouse is currently hovering, for GameCursor's shape and the highlight outline drawn
// around it (direct user request, "как в баротравме" - hover a clickable object, it highlights,
// the cursor changes to a hand). Deliberately reads the SAME rect-computing helpers
// (ShipRenderer.GetBlockRect/GetDoorRect/GetDroppedItemRect, TileGridRasterizer.DoorTileRect) that
// HandleMouseClick (Game1.Input.cs) itself calls for each of these devices, rather than a separate
// guess at where they are - a click rect and its own hover rect quietly disagreeing is exactly the
// bug already found and fixed once this session (a door's click hit-test used the door's raw,
// pre-tile-alignment rect while the sprite itself had already moved to the tile-aligned one).
public partial class Game1
{
    // Every device a click can currently do something to (humble-soaring-cat.md - "Полный переход
    // на клик как в Baro"), covering both the always-click-based blocks (Reactor/Distribution/
    // Battery/Jukebox/CardTable/Doors/Racks/DroppedItems) and the ones converted from [E] this same
    // milestone (Turret/AmmoStorage/SuitLocker/repairable devices/station crates).
    private Rectangle? ComputeHoveredInteractable(WorldSnapshot? snapshot)
    {
        if (snapshot is null || _pauseMenuOpen || _cheatPanelOpen || _infoPanelOpen)
            return null;
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        if (me is null || me.IsAtHelm)
            return null;
        if (snapshot.TurretStates.Any(t => t.MannedByPlayerId == _client.PlayerId))
            return null;
        // Only the ship-interior scene has world-space device rects to test against - the helm/
        // turret/navigation views replace the whole screen with their own panels instead (same
        // exemptions BuildVisibilityMask already makes for the same reason, Game1.Lighting.cs).
        if (_shipInteriorOrigin is not { } origin)
            return null;

        var myPosition = new Vec2(me.X, me.Y);
        bool NearEnough(Vec2 p) => (p - myPosition).Length() < TurretInteractionRadius;

        if (me.OnStation)
        {
            foreach (var crate in snapshot.Station.Crates)
            {
                if (snapshot.Station.CrateStates.FirstOrDefault(s => s.CrateId == crate.Id)?.Looted ?? false)
                    continue;
                if (!NearEnough(crate.Position))
                    continue;
                var rect = ShipRenderer.GetBlockRect(crate.Position, 20, origin);
                if (rect.Contains(_designMouse))
                    return rect;
            }
            return null; // nothing else on this list is reachable while standing on the station
        }

        if (NearEnough(snapshot.HelmConsole.Position))
        {
            var rect = ShipRenderer.GetBlockRect(snapshot.HelmConsole.Position, ShipRenderer.MediumBlockSize, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        if (NearEnough(snapshot.NavigationConsole.Position))
        {
            var rect = ShipRenderer.GetBlockRect(snapshot.NavigationConsole.Position, ShipRenderer.MediumBlockSize, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        foreach (var turret in snapshot.Turrets)
        {
            if (!NearEnough(turret.PeriscopePosition))
                continue;
            var rect = ShipRenderer.GetBlockRect(turret.PeriscopePosition, ShipRenderer.MediumBlockSize, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        foreach (var storage in snapshot.AmmoStorages)
        {
            if (!NearEnough(storage.Position))
                continue;
            var rect = ShipRenderer.GetBlockRect(storage.Position, ShipRenderer.NormalBlockSize, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        foreach (var locker in snapshot.SuitLockers)
        {
            if (!NearEnough(locker.Position))
                continue;
            var rect = ShipRenderer.GetBlockRect(locker.Position, ShipRenderer.NormalBlockSize, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        foreach (var device in snapshot.SystemDevices)
        {
            if (!NearEnough(device.Position))
                continue;
            var size = device.System == PowerSystemId.Engine ? ShipRenderer.BigBlockSize : ShipRenderer.NormalBlockSize;
            var rect = ShipRenderer.GetBlockRect(device.Position, size, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        foreach (var camera in snapshot.Cameras)
        {
            if (!NearEnough(camera.InteriorPosition))
                continue;
            var rect = ShipRenderer.GetBlockRect(camera.InteriorPosition, ShipRenderer.NormalBlockSize, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        foreach (var engine in snapshot.EngineStates ?? Array.Empty<EngineState>())
        {
            var controlPosition = new Vec2(engine.X, engine.Y);
            if (!NearEnough(controlPosition))
                continue;
            var rect = ShipRenderer.GetBlockRect(controlPosition, (int)ShipRenderer.PixelsPerUnit, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        foreach (var junction in snapshot.Wiring.Components.Where(c => c.Kind == ComponentKind.Junction))
        {
            if (!NearEnough(junction.Position))
                continue;
            var rect = ShipRenderer.GetBlockRect(junction.Position, ShipRenderer.NormalBlockSize, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }

        if (NearEnough(snapshot.ReactorBlock.Position))
        {
            var rect = ShipRenderer.GetBlockRect(snapshot.ReactorBlock.Position, (int)(ShipRenderer.BigBlockSize * snapshot.ReactorBlock.SizeScale), origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        if (NearEnough(snapshot.DistributionBlock.Position))
        {
            var rect = ShipRenderer.GetBlockRect(snapshot.DistributionBlock.Position, ShipRenderer.MediumBlockSize, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        if (NearEnough(snapshot.BatteryBlock.Position))
        {
            var rect = ShipRenderer.GetBlockRect(snapshot.BatteryBlock.Position, ShipRenderer.MediumBlockSize, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        if (NearEnough(snapshot.CardTable.Position))
        {
            var rect = ShipRenderer.GetBlockRect(snapshot.CardTable.Position, ShipRenderer.MediumBlockSize, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        if (snapshot.Jukebox is { } jukebox && NearEnough(jukebox.Block.Position))
        {
            var rect = ShipRenderer.GetBlockRect(jukebox.Block.Position, ShipRenderer.MediumBlockSize, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        foreach (var rack in snapshot.StorageRacks)
        {
            if (!NearEnough(rack.Position))
                continue;
            var rect = ShipRenderer.GetBlockRect(rack.Position, ShipRenderer.MediumBlockSize, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        foreach (var door in snapshot.Doors)
        {
            if (!NearEnough(door.Position))
                continue;
            var (left, top, width, height) = TileGridRasterizer.DoorTileRect(snapshot.Rooms, door.X, door.Y, door.Width, door.Height);
            var rect = ShipRenderer.GetDoorRect(left, top, width, height, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        foreach (var outerDoor in snapshot.AirlockOuterDoors)
        {
            if (!NearEnough(outerDoor.Position))
                continue;
            var ownRoom = new[] { snapshot.Rooms.First(r => r.Id == outerDoor.RoomId) };
            var (left, top, width, height) = TileGridRasterizer.DoorTileRect(ownRoom, outerDoor.X, outerDoor.Y, outerDoor.Width, outerDoor.Height);
            var rect = ShipRenderer.GetDoorRect(left, top, width, height, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        foreach (var dropped in snapshot.DroppedItems.Where(d => d.RoomId is not null))
        {
            if (!NearEnough(dropped.Position))
                continue;
            var rect = ShipRenderer.GetDroppedItemRect(dropped, origin);
            if (rect.Contains(_designMouse))
                return rect;
        }
        return null;
    }
}
