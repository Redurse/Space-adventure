using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// One "is the player near this device, and if so where does it sit on screen" test per device kind
// (humble-soaring-cat.md's own "общий разделяемый слой" idea) - the single place ComputeHint
// (Game1.Input.cs, the hint text), ComputeHoveredInteractable below (the hover highlight/cursor),
// and HandleMouseClick's own per-device hit-test (Game1.Input.cs) all get their "near enough, and
// here's its rect" answer from, instead of each re-deriving the same radius/rect condition on its
// own. A rect/condition quietly drifting out of sync between two of these three copies is exactly
// the bug already found and fixed once this session (a door's click hit-test used the door's raw,
// pre-tile-alignment rect while the sprite - and this file's own hover rect - had already moved to
// the tile-aligned one). What happens on click (open a panel vs. repair vs. an instant action) and
// the exact hint wording still live where they always did - only the shared "is it in reach, and
// what's its rect" gate moved here.
public partial class Game1
{
    private static bool NearEnough(Vec2 a, Vec2 b) => (a - b).Length() < InteractionConstants.DeviceInteractionRadius;

    private static Rectangle? BlockRectIfNear(Vec2 devicePosition, Vec2 myPosition, int size, Vector2 origin) =>
        NearEnough(devicePosition, myPosition) ? ShipRenderer.GetBlockRect(devicePosition, size, origin) : null;

    // Non-square hit-rect (Helm/Navigation and any other footprint from CustomDeviceFootprint.Size
    // that isn't 1x1) - same "near enough, and here's its rect" gate as the size-only overload above,
    // just forwarding into ShipRenderer's own width/height GetBlockRect instead of the square one.
    private static Rectangle? BlockRectIfNear(Vec2 devicePosition, Vec2 myPosition, int width, int height, Vector2 origin) =>
        NearEnough(devicePosition, myPosition) ? ShipRenderer.GetBlockRect(devicePosition, width, height, origin) : null;

    // The reactor's own console can be much bigger than the hand-authored default (Ship.Custom.cs
    // scales SizeScale to roughly fill half its room) - standing anywhere near that footprint has to
    // count as "near enough", not just the one exact point ReactorBlock.Position sits at.
    private static Rectangle? ReactorRectIfNear(ReactorBlock reactor, Vec2 myPosition, Vector2 origin)
    {
        var halfWorldSize = ShipRenderer.BigBlockSize * reactor.SizeScale / ShipRenderer.PixelsPerUnit / 2f;
        if ((myPosition - reactor.Position).Length() >= InteractionConstants.DeviceInteractionRadius + halfWorldSize)
            return null;
        return ShipRenderer.GetBlockRect(reactor.Position, (int)(ShipRenderer.BigBlockSize * reactor.SizeScale), origin);
    }

    // Direct user report ("не могу нажать на рычаги... хитбокс текстуры как с остальными
    // устройствами") - the 3 reactor levers (light/emergency/doors-locked, HandleMouseClick's own
    // click-test) had no entry here at all, so hovering one never got the gold highlight/hand
    // cursor every other device already gives - the one visible cue for "you're actually over the
    // clickable spot" that every other device relies on to be aimable. Same GetReactorLeverRect the
    // click-test and the drawing itself already share, so this can never disagree with either.
    private static Rectangle? ReactorLeverRectIfNear(ReactorBlock reactor, Vec2 myPosition, Vector2 origin, int index) =>
        NearEnough(reactor.Position, myPosition) ? ShipRenderer.GetReactorLeverRect(index, reactor, origin) : null;

    private static Rectangle? SystemDeviceRectIfNear(ShipSystemDevice device, Vec2 myPosition, Vector2 origin) =>
        BlockRectIfNear(device.Position, myPosition,
            device.System == PowerSystemId.Engine ? ShipRenderer.BigBlockSize : ShipRenderer.NormalBlockSize, origin);

    // Direct user bug report ("текстура двигателя съехала") - this used to hit-test a full 48px
    // tile (PixelsPerUnit) while DrawShipEngine's own Control box actually renders at the smaller
    // BigBlockSize (40px, "same size/style as any other system-device box" - that method's own doc
    // comment), the same class of hover/click-vs-texture drift this session already found and fixed
    // for the reactor's own levers. Never caught before now because a real, catalog-placed marching
    // engine had never actually been built and looked at in a live session until "Двигатель 1".
    private static Rectangle? EngineControlRectIfNear(EngineState engine, Vec2 myPosition, Vector2 origin) =>
        BlockRectIfNear(new Vec2(engine.X, engine.Y), myPosition, ShipRenderer.BigBlockSize, origin);

    // World.Mining.cs's TryPickupDroppedItem actually allows PickupRadius (1.5), wider than the
    // ordinary DeviceInteractionRadius (1.0) every other device here uses - this used to be a real,
    // silent drift (hint/hover/click all gated on the tighter radius, refusing a pickup up to 0.5
    // units before the server actually would have).
    private static Rectangle? DroppedItemRectIfNear(DroppedItem dropped, Vec2 myPosition, Vector2 origin) =>
        (dropped.Position - myPosition).Length() < InteractionConstants.PickupRadius ? ShipRenderer.GetDroppedItemRect(dropped, origin) : null;

    private static Rectangle? StationCrateRectIfNear(StationCrate crate, Vec2 myPosition, Vector2 origin) =>
        BlockRectIfNear(crate.Position, myPosition, 20, origin);

    // Doors need TileGridRasterizer's own tile-aligned rect, not the door's raw (boundary-centred)
    // Left/Top/Width/Height - see this method's own doc comment above for why that distinction is
    // load-bearing, not cosmetic.
    // Covers both interior and vacuum-facing doors now (humble-soaring-cat.md, "убрать
    // AirlockOuterDoor как отдельный тип") - RoomsForDoor scopes the rasterization to just the
    // door's own room when it LeadsToVacuum, same as ShipRenderer's own draw loop.
    private static Rectangle? DoorRectIfNear(IReadOnlyList<Room> rooms, Door door, Vec2 fromPosition, Vector2 origin)
    {
        if ((door.Position - fromPosition).Length() >= InteractionConstants.DeviceInteractionRadius)
            return null;
        var doorRooms = TileGridRasterizer.RoomsForDoor(rooms, door);
        var (left, top, width, height) = TileGridRasterizer.DoorTileRect(doorRooms, door.X, door.Y, door.Width, door.Height);
        return ShipRenderer.GetDoorRect(left, top, width, height, origin);
    }

    // M-doors-as-edges - proximity is measured from the seam's own midpoint (not either flanking
    // tile's center) so standing in either tile reads as roughly the same distance; the actual
    // hitbox returned is still the union of both full tiles (ShipRenderer.GetDoorEdgeHitRect),
    // matching the direct user request ("клик по хитбоксу этих 2 клеток").
    private static Rectangle? DoorEdgeRectIfNear(ShipDoorEdge edge, Vec2 fromPosition, Vector2 origin)
    {
        var seamCenter = edge.Side == TileSide.East
            ? new Vec2(edge.Coord.X + 1, edge.Coord.Y + 0.5)
            : new Vec2(edge.Coord.X + 0.5, edge.Coord.Y + 1);
        if ((seamCenter - fromPosition).Length() >= InteractionConstants.DeviceInteractionRadius)
            return null;
        return ShipRenderer.GetDoorEdgeHitRect(edge.Coord, edge.Side, origin);
    }

    // Every device a click can currently do something to (humble-soaring-cat.md - "Полный переход
    // на клик как в Baro"), covering both the always-click-based blocks (Reactor/Distribution/
    // Battery/Jukebox/Terminal/CardTable/Doors/Racks/DroppedItems) and the ones converted from [E]
    // this same milestone (Turret/AmmoStorage/SuitLocker/repairable devices/station crates). Reads
    // the exact same *RectIfNear helpers above that HandleMouseClick's own hit-tests do.
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

        if (me.OnStation)
        {
            foreach (var crate in snapshot.Station.Crates)
            {
                if (snapshot.Station.CrateStates.FirstOrDefault(s => s.CrateId == crate.Id)?.Looted ?? false)
                    continue;
                if (StationCrateRectIfNear(crate, myPosition, origin) is { } rect && rect.Contains(_designMouse))
                    return rect;
            }
            return null; // nothing else on this list is reachable while standing on the station
        }

        // Checked before the reactor's own big rect, same priority HandleMouseClick's click-test
        // already gives them (a lever sits just outside that rect, but this keeps both agreeing).
        for (var leverIndex = 0; leverIndex < 3; leverIndex++)
            if (ReactorLeverRectIfNear(snapshot.ReactorBlock, myPosition, origin, leverIndex) is { } leverRect && leverRect.Contains(_designMouse))
                return leverRect;
        if (ReactorRectIfNear(snapshot.ReactorBlock, myPosition, origin) is { } reactorRect && reactorRect.Contains(_designMouse))
            return reactorRect;
        var (distWidth, distHeight) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.Distribution);
        if (BlockRectIfNear(snapshot.DistributionBlock.Position, myPosition, distWidth, distHeight, origin) is { } distributionRect && distributionRect.Contains(_designMouse))
            return distributionRect;
        var (batteryWidth, batteryHeight) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.Battery);
        if (BlockRectIfNear(snapshot.BatteryBlock.Position, myPosition, batteryWidth, batteryHeight, origin) is { } batteryRect && batteryRect.Contains(_designMouse))
            return batteryRect;
        var (helmWidth, helmHeight) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.Helm, snapshot.HelmConsole.Rotated);
        if (BlockRectIfNear(snapshot.HelmConsole.Position, myPosition, helmWidth, helmHeight, origin) is { } helmRect && helmRect.Contains(_designMouse))
            return helmRect;
        var (navWidth, navHeight) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.Navigation, snapshot.NavigationConsole.Rotated);
        if (BlockRectIfNear(snapshot.NavigationConsole.Position, myPosition, navWidth, navHeight, origin) is { } navRect && navRect.Contains(_designMouse))
            return navRect;
        foreach (var extraNav in snapshot.ExtraNavigationConsoles ?? Array.Empty<NavigationConsole>())
        {
            var (width, height) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.Navigation, extraNav.Rotated);
            if (BlockRectIfNear(extraNav.Position, myPosition, width, height, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        }
        foreach (var extraHelm in snapshot.ExtraHelmConsoles ?? Array.Empty<HelmConsole>())
        {
            var (width, height) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.Helm, extraHelm.Rotated);
            if (BlockRectIfNear(extraHelm.Position, myPosition, width, height, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        }
        var (cardTableWidth, cardTableHeight) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.CardTable);
        if (BlockRectIfNear(snapshot.CardTable.Position, myPosition, cardTableWidth, cardTableHeight, origin) is { } cardTableRect && cardTableRect.Contains(_designMouse))
            return cardTableRect;
        var (jukeboxWidth, jukeboxHeight) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.Jukebox);
        if (snapshot.Jukebox is { } jukebox && BlockRectIfNear(jukebox.Block.Position, myPosition, jukeboxWidth, jukeboxHeight, origin) is { } jukeboxRect && jukeboxRect.Contains(_designMouse))
            return jukeboxRect;
        // Direct user bug report ("не могу нажать... не отображается их коллизия при наведении") -
        // ShipStatusMonitor/CommsConsole were missing from this shared hover-highlight layer
        // entirely (added alongside Navigation/Helm above at the time, this pair just got missed) -
        // no visible "you're over something clickable" cue, which read as broken hit-testing even
        // though the E-based open itself (Game1.cs's own proximity check) already worked. Now
        // genuinely many instances - hovering ANY one of them highlights it.
        foreach (var shipStatusMonitor in snapshot.ShipStatusMonitors ?? Array.Empty<ShipStatusMonitor>())
        {
            var (width, height) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.ShipStatusMonitor, shipStatusMonitor.Rotated);
            if (BlockRectIfNear(shipStatusMonitor.Position, myPosition, width, height, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        }
        foreach (var commsConsole in snapshot.CommsConsoles ?? Array.Empty<CommsConsole>())
        {
            var (width, height) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.CommsConsole, commsConsole.Rotated);
            if (BlockRectIfNear(commsConsole.Position, myPosition, width, height, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        }
        // Direct user request ("сделай щитку свою собственную текстуру") - JunctionBox is now a real
        // half-width fixture with its own bespoke face, not the flat unclickable swatch it used to be.
        foreach (var junctionBox in snapshot.JunctionBoxes ?? Array.Empty<JunctionBox>())
        {
            var (width, height) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.Junction, junctionBox.Rotated);
            if (BlockRectIfNear(junctionBox.Position, myPosition, width, height, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        }
        foreach (var terminal in snapshot.Terminals ?? Array.Empty<TerminalState>())
            if (BlockRectIfNear(terminal.Block.Position, myPosition, ShipRenderer.MediumBlockSize, origin) is { } rect && rect.Contains(_designMouse))
                return rect;

        // Same 1x3-tile mount footprint CustomDeviceFootprint.Size gives every turret kind in the
        // Ship Editor (direct user request, screenshot of a turret mount built out of wall tiles -
        // "реальные такие границы... при наведении мышкой в игре"), extended to the live game's own
        // hit-rect. TurretBallistic is just a stand-in kind here - every turret kind shares the same
        // size; Turret.Rotated (added alongside this) is per-INSTANCE, unlike the kind itself.
        foreach (var turret in snapshot.Turrets)
        {
            var (turretWidth, turretHeight) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.TurretBallistic, turret.Rotated);
            if (BlockRectIfNear(turret.PeriscopePosition, myPosition, turretWidth, turretHeight, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        }
        var (ammoWidth, ammoHeight) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.AmmoStorage);
        foreach (var storage in snapshot.AmmoStorages)
            if (BlockRectIfNear(storage.Position, myPosition, ammoWidth, ammoHeight, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        var (lockerWidth, lockerHeight) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.SuitLocker);
        foreach (var locker in snapshot.SuitLockers)
            if (BlockRectIfNear(locker.Position, myPosition, lockerWidth, lockerHeight, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        var (rackWidth, rackHeight) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.StorageRack);
        foreach (var rack in snapshot.StorageRacks)
            if (BlockRectIfNear(rack.Position, myPosition, rackWidth, rackHeight, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        foreach (var device in snapshot.SystemDevices)
            if (SystemDeviceRectIfNear(device, myPosition, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        var (camWidth, camHeight) = ShipRenderer.FootprintPixelSize(CustomDeviceKind.Camera);
        foreach (var camera in snapshot.Cameras)
            if (BlockRectIfNear(camera.InteriorPosition, myPosition, camWidth, camHeight, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        foreach (var engine in snapshot.EngineStates ?? Array.Empty<EngineState>())
            if (EngineControlRectIfNear(engine, myPosition, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        foreach (var junction in snapshot.Wiring.Components.Where(c => c.Kind == ComponentKind.Junction))
            if (BlockRectIfNear(junction.Position, myPosition, ShipRenderer.NormalBlockSize, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        // A door's own Position is always the ship's local, unrotated interior frame, but IsOutside
        // switches CharacterState's own X/Y to AsteroidField world-space (World.cs's CreateSnapshot) -
        // the same conversion HandleMouseClick's own door click already applies (doorClickPosition),
        // needed here too or a suited character near an open airlock would never see it highlight
        // even though clicking it does work.
        var doorProximityPosition = ShipLocalFrame.CharacterToLocal(me, snapshot.ShipField, ShipLocalFrame.GetHullCenter(snapshot.Rooms));
        foreach (var door in snapshot.Doors)
            if (DoorRectIfNear(snapshot.Rooms, door, doorProximityPosition, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        // Direct user request ("чтобы у двойной и тройной двери... высвечивался весь хитбокс а не 1
        // на 1 на каждую клетку") - a wide/triple door is several parallel edges sharing one Id
        // (DoorSpanTiles, PlaceEdgeDoor), each with its own narrow GetDoorEdgeHitRect; hovering over
        // just the one segment under the cursor used to highlight only that segment's own 2-tile
        // rect instead of the door's whole footprint. Once ANY segment of a group is near enough and
        // under the cursor, the highlight is every segment's rect unioned together - the same door
        // toggles as one unit either way (ToggleDoor keys off the shared Id), this only changes what
        // the outline shows.
        foreach (var group in (snapshot.DoorEdges ?? Array.Empty<ShipDoorEdge>()).GroupBy(e => e.Id))
        {
            var groupList = group.ToList();
            if (!groupList.Any(edge => DoorEdgeRectIfNear(edge, doorProximityPosition, origin) is { } r && r.Contains(_designMouse)))
                continue;
            var union = ShipRenderer.GetDoorEdgeHitRect(groupList[0].Coord, groupList[0].Side, origin);
            foreach (var edge in groupList.Skip(1))
                union = Rectangle.Union(union, ShipRenderer.GetDoorEdgeHitRect(edge.Coord, edge.Side, origin));
            return union;
        }
        foreach (var dropped in snapshot.DroppedItems.Where(d => d.RoomId is not null))
            if (DroppedItemRectIfNear(dropped, myPosition, origin) is { } rect && rect.Contains(_designMouse))
                return rect;

        // Direct user request ("некоторые устройства... чтобы у них был хитбокс при наведении, но у
        // тех что нет функции при нажатии на них просто ничего не делали") - DecorativeDevice.Kinds
        // now render (ShipRenderer.Devices.cs's own DrawDecorativeDevice), so they need the same
        // hover highlight every other device gets; deliberately NOT added to HandleMouseClick's own
        // per-device checks below it in that file, so clicking one falls through and does nothing,
        // exactly like clicking empty floor.
        foreach (var device in snapshot.DecorativeDevices ?? Array.Empty<DecorativeDevice>())
        {
            var (width, height) = ShipRenderer.FootprintPixelSize(device.Kind, device.Rotated);
            if (BlockRectIfNear(device.Position, myPosition, width, height, origin) is { } rect && rect.Contains(_designMouse))
                return rect;
        }

        return null;
    }
}
