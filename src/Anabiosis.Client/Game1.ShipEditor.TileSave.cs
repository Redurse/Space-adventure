using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client;

// Round-trips the tile canvas (_editorTiles/_editorDeviceKinds/_editorZones) through
// CustomShipTileCanvas - direct user request ("сохранять построенные корабли между сессиями и
// потом их загружать"). Separate from Game1.ShipEditor.TileBridge.cs's CustomShipDefinition export:
// that conversion is lossy (Room rectangles can't remember individual wall/door/terminal tiles or
// zone names), so restoring a design faithfully on Load replays the SAME tile canvas data instead
// of trying to reconstruct it from the derived rooms.
public partial class Game1
{
    private CustomShipTileCanvas BuildEditorTileCanvas()
    {
        var tiles = _editorTiles.Cells
            .Select(kv => new CustomShipTileCanvas.TileRecord(
                kv.Key.X, kv.Key.Y, kv.Value.HasFloor, kv.Value.Wall, kv.Value.DoorOpen,
                kv.Value.WallDeviceId, kv.Value.WallDeviceMountSide, kv.Value.WallMaterial, kv.Value.DoorGroupId,
                kv.Value.WallFromCompartment, kv.Value.WallDeviceRecessed, kv.Value.WallOpenSide, kv.Value.WallDeviceKind))
            .ToList();
        var devices = _editorDeviceKinds
            .Select(kv => new CustomShipTileCanvas.DeviceRecord(kv.Key.X, kv.Key.Y, kv.Value,
                _editorDeviceRotation.TryGetValue(kv.Key, out var rotated) && rotated,
                _editorDeviceHalfSides.TryGetValue(kv.Key, out var halfSide) ? halfSide : null))
            .ToList();
        var zones = _editorZones
            .Select(z => new CustomShipTileCanvas.ZoneRecord(
                z.Name, z.Tiles.Select(t => new CustomShipTileCanvas.TilePos(t.X, t.Y)).ToList(), z.Kind))
            .ToList();
        var engines = _editorEngineFacing
            .Select(kv => new CustomShipTileCanvas.EngineRecord(kv.Key.X, kv.Key.Y, kv.Value))
            .ToList();
        // Direct user request ("двойной двигатель... общая клетка в основании") - saved as TWO plain
        // EngineRecords sharing the same (X,Y), same "no format change needed" property TileShipBuilder's
        // own export already relies on (ShipEngine.cs's Bulkhead/Nozzle are computed from Facing, never
        // stored) - ApplyEditorTileCanvas below groups by (X,Y) on load to tell a single engine (group of
        // 1) apart from a double engine (group of 2).
        foreach (var kv in _editorDoubleEngineFacings)
        {
            engines.Add(new CustomShipTileCanvas.EngineRecord(kv.Key.X, kv.Key.Y, kv.Value.First));
            engines.Add(new CustomShipTileCanvas.EngineRecord(kv.Key.X, kv.Key.Y, kv.Value.Second));
        }
        var doorEdges = _editorTiles.DoorEdges
            .Select(kv => new CustomShipTileCanvas.DoorEdgeRecord(kv.Key.Coord.X, kv.Key.Coord.Y, kv.Key.Side, kv.Value.Id))
            .ToList();
        // Any WallOpenSide on _editorTiles right now only ever got there via the new Wall tool's
        // own half-block toggle (HandleWallToolInput) - the old always-on auto-inference that used
        // to write it on the player's behalf is gone - so this save's flags are always deliberate.
        // Direct user request ("я хочу чтобы ты сделал отсек таким каким я его сохранил") - every
        // placed compartment instance, by EntryId+Anchor+RotationSteps (CustomShipTileCanvas.
        // CompartmentInstanceRecord's own doc comment on why that's enough to restore on load).
        var compartmentInstances = _editorCompartmentPlacement
            .Where(kv => _editorCompartmentEntryId.ContainsKey(kv.Key))
            .Select(kv => new CustomShipTileCanvas.CompartmentInstanceRecord(
                kv.Key, _editorCompartmentEntryId[kv.Key], kv.Value.Anchor.X, kv.Value.Anchor.Y, kv.Value.RotationSteps))
            .ToList();
        return new CustomShipTileCanvas(tiles, devices, zones, engines, ManualHalfBlockWalls: true, DoorEdgesRaw: doorEdges,
            CompartmentInstancesRaw: compartmentInstances);
    }

    // Replays the saved data through the SAME TileGrid mutators the editor's own tools use (floors
    // first, then walls/doors - a wall/device/terminal's own precondition needs the floor already
    // there - then devices via the same anchor+footprint helper HandleDeviceToolInput uses, then
    // terminals, which need their mount-side wall neighbour already placed). Wall HP isn't restored
    // - every reloaded wall comes back full-health, same as a freshly-painted one would.
    private void ApplyEditorTileCanvas(CustomShipTileCanvas canvas)
    {
        _editorTiles = new TileGrid();
        _editorDeviceKinds.Clear();
        _editorDeviceFootprint.Clear();
        _editorDeviceRotation.Clear();
        _editorDeviceHalfSides.Clear();
        _editorZones.Clear();
        _editorEngineFacing.Clear();
        _editorDoubleEngineFacings.Clear();
        _editorEngineFootprint.Clear();
        _editorCompartmentAt.Clear();
        _editorCompartmentTiles.Clear();
        _editorCompartmentProtected.Clear();
        _editorCompartmentEntryId.Clear();
        _editorCompartmentPlacement.Clear();

        foreach (var t in canvas.Tiles)
            _editorTiles.SetFloor(new TileCoord(t.X, t.Y), true);
        foreach (var t in canvas.Tiles)
        {
            if (t.Wall == TileWallKind.None)
                continue;
            var coord = new TileCoord(t.X, t.Y);
            _editorTiles.SetWall(coord, t.Wall, material: t.WallMaterial, fromCompartment: t.FromCompartment);
            // A save without ManualHalfBlockWalls predates the manual-only half-block system - its
            // WallOpenSide values are leftovers from the old, now-deleted auto-inference, not a
            // deliberate player choice, so they're dropped rather than faithfully replayed.
            if (canvas.ManualHalfBlockWalls && t.WallOpenSide is { } openSide)
                _editorTiles.SetWallOpenSide(coord, openSide);
            if (t.Wall == TileWallKind.Door && t.DoorOpen)
                _editorTiles.SetDoorOpen(coord, true);
        }
        // Re-link "wide" door pairs (direct user request - "дверь занимающая 1 на 2 тайла") - both
        // tiles must already be Door (set above) before TileGrid.LinkDoors will accept them. Groups
        // this save format never produced (every save before wide doors existed) simply have none.
        foreach (var group in canvas.Tiles.Where(t => t.DoorGroupId is not null).GroupBy(t => t.DoorGroupId))
        {
            var members = group.Select(t => new TileCoord(t.X, t.Y)).ToList();
            if (members.Count == 2)
                _editorTiles.LinkDoors(members[0], members[1]);
        }
        // M-doors-as-edges - both flanking tiles are already plain floor by this point (the floor
        // loop at the very top ran for every tile this save has, and neither flank of a genuine edge
        // door ever carries a Wall entry at all), so CanPlaceDoorEdge's guard always passes here for
        // any legitimately-saved edge; AddDoorEdge is skipped rather than thrown for a
        // stale/corrupted coordinate instead of crashing the whole load.
        _editorNextDoorEdgeId = 0;
        foreach (var e in canvas.DoorEdges)
        {
            var coord = new TileCoord(e.X, e.Y);
            if (_editorTiles.CanPlaceDoorEdge(coord, e.Side))
                _editorTiles.AddDoorEdge(coord, e.Side, e.Id);
            // Keeps freshly-placed ids (HandleNarrowDoorEdgeToolInput) from ever colliding with one
            // this same save already used, regardless of what a future save format change might name
            // them - only the "door-edge-N" ids THIS session's own placement ever produces matter.
            if (e.Id.StartsWith("door-edge-") && int.TryParse(e.Id.Substring("door-edge-".Length), out var n) && n >= _editorNextDoorEdgeId)
                _editorNextDoorEdgeId = n + 1;
        }
        foreach (var d in canvas.Devices)
        {
            var anchor = new TileCoord(d.X, d.Y);
            var deviceId = $"device-{d.X}-{d.Y}";
            var (width, height) = DeviceFootprintSize(d.Kind, d.Rotated);
            var footprint = DeviceFootprintTiles(anchor, width, height).ToList();
            // Helm/Navigation's own half tile (PlaceDeviceFootprint, shared with fresh placement in
            // Game1.ShipEditor.cs's HandleDeviceToolInput) - a save from a wall-adjacent placement
            // replays its wall tile first (the Tiles loop above already ran), so the exact same
            // half-block-wall-coexistence check that allowed the original placement still passes here.
            // d.HalfSide falls back to d.Rotated's old East/South-only mapping for a save from before
            // the 4-way HalfSide field existed (CustomDeviceFootprint.ResolveHalfSide).
            var halfSide = CustomDeviceFootprint.ResolveHalfSide(d.HalfSide, d.Rotated);
            PlaceDeviceFootprint(d.Kind, footprint, anchor, halfSide, deviceId);
            foreach (var occupied in footprint)
                _editorDeviceFootprint[occupied] = anchor;
            _editorDeviceKinds[anchor] = d.Kind;
            if (d.Rotated)
                _editorDeviceRotation[anchor] = true;
            if (CustomDeviceFootprint.IsHalfWidthKind(d.Kind))
                _editorDeviceHalfSides[anchor] = halfSide;
        }
        // Grouped by (X,Y) rather than replayed one record at a time - a double engine save (above)
        // put TWO EngineRecords at the same coordinate on purpose, and only grouping first tells that
        // apart from two entirely unrelated single engines that happen to load in the same pass.
        foreach (var group in canvas.Engines.GroupBy(e => new TileCoord(e.X, e.Y)))
        {
            var control = group.Key;
            var facings = group.Select(e => e.Facing).ToList();
            if (facings.Count == 2)
            {
                var deviceId = $"doubleengine-{control.X}-{control.Y}";
                _editorTiles.PlaceDevice(control, deviceId);
                _editorDoubleEngineFacings[control] = (facings[0], facings[1]);
                foreach (var occupied in DoubleEngineFootprintTiles(control, facings[0], facings[1]))
                    _editorEngineFootprint[occupied] = control;
            }
            else
            {
                var facing = facings[0];
                var deviceId = $"engine-{control.X}-{control.Y}";
                _editorTiles.PlaceDevice(control, deviceId);
                _editorEngineFacing[control] = facing;
                foreach (var occupied in EngineFootprintTiles(control, facing))
                    _editorEngineFootprint[occupied] = control;
            }
        }
        // Replayed via RestoreWallDevice rather than PlaceWallDevice/PlaceRecessedWallDevice: this is
        // a RELOAD of previously-valid data (already checked once at placement time), not a fresh
        // placement. Only Terminal/WallLamp ever reach WallDeviceId at all - Helm/Navigation are
        // ordinary devices, replayed by the canvas.Devices loop above instead.
        foreach (var t in canvas.Tiles)
        {
            if (t.WallDeviceId is not { } deviceId)
                continue;
            var coord = new TileCoord(t.X, t.Y);
            // A save from before WallLamp existed never had a WallDeviceKind at all - Terminal was
            // the only wall-mountable kind back then, so that's the safe fallback here.
            var kind = t.WallDeviceKind ?? CustomDeviceKind.Terminal;
            var mountSide = t.WallDeviceMountSide ?? TileSide.North;
            _editorTiles.RestoreWallDevice(coord, kind, deviceId, mountSide, t.WallDeviceRecessed);
        }
        foreach (var z in canvas.Zones)
            _editorZones.Add(new EditorZone(z.Name, z.Tiles.Select(p => new TileCoord(p.X, p.Y)).ToHashSet(), z.Kind));

        // Direct user request ("я хочу чтобы ты сделал отсек таким каким я его сохранил") - restores
        // _editorCompartmentAt/Tiles/Protected the same way HandleCompartmentToolInput's own
        // placement-time bookkeeping does, WITHOUT re-stamping onto this already-loaded grid (every
        // tile/device this compartment ever placed already replayed above, from the ordinary
        // Tiles/Devices lists) - stamps a THROWAWAY scratch grid purely to recover ProtectedTiles
        // the same real Stamp call would have produced, reusing that logic instead of duplicating
        // it. Skipped (not failed) for a stale/corrupted instance record - CompartmentCatalog.Find
        // returning null (a since-removed catalog entry) or a doomed Stamp on the scratch grid both
        // just leave that one instance's bookkeeping absent, same "annoying, not fatal" tolerance
        // every other loader here already has for bad data.
        foreach (var instance in canvas.CompartmentInstances)
        {
            if (CompartmentCatalog.Find(instance.EntryId) is not { } entry)
                continue;
            var anchor = new TileCoord(instance.AnchorX, instance.AnchorY);
            var scratch = new TileGrid();
            var result = CompartmentPlacer.Stamp(scratch, entry, anchor, instance.RotationSteps, instance.InstanceId);
            if (!result.Success)
                continue;

            var rotated = CompartmentPlacer.Rotate(entry, instance.RotationSteps);
            var allTiles = new HashSet<TileCoord>();
            foreach (var footprintRect in rotated.FootprintRects)
                for (var x = (int)footprintRect.X; x < (int)footprintRect.Right; x++)
                    for (var y = (int)footprintRect.Y; y < (int)footprintRect.Bottom; y++)
                        allTiles.Add(new TileCoord(anchor.X + x, anchor.Y + y));

            foreach (var t in allTiles)
                _editorCompartmentAt[t] = instance.InstanceId;
            _editorCompartmentTiles[instance.InstanceId] = allTiles;
            _editorCompartmentProtected[instance.InstanceId] = new HashSet<TileCoord>(result.ProtectedTiles);
            _editorCompartmentEntryId[instance.InstanceId] = instance.EntryId;
            _editorCompartmentPlacement[instance.InstanceId] = (anchor, instance.RotationSteps);

            if (int.TryParse(instance.InstanceId.Replace("compartment-", ""), out var n) && n >= _editorNextCompartmentInstance)
                _editorNextCompartmentInstance = n + 1;
        }
    }
}
