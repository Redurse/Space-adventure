using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client;

// W10 of wiring-plan.md - the editor's wiring mode. With the "Провода" tool the canvas dims, the devices that take power stay lit, and
// the author joins two of them with a click on each (the pins are worked out from what the two are - EditorWiring.Link); a right click on a
// device takes its wires off, a right click on empty floor drops a half-made wire. The wires are kept per device anchor (so they follow
// the devices through every other edit), saved with the tile canvas, and exported into CustomShipDefinition.Wires, where the server uses
// them in place of the standard wiring at the pins they reach (World.WiringHazards.cs's ApplyEditorWires).
public partial class Game1
{
    // Two devices, by their anchor tiles (the key _editorDeviceKinds uses). Order is just the order the author clicked them in.
    private readonly List<(TileCoord A, TileCoord B)> _editorWires = new();
    private TileCoord? _editorWireStart;

    private static readonly Color EditorWireColor = new(255, 200, 70);

    private void ClearEditorWires()
    {
        _editorWires.Clear();
        _editorWireStart = null;
    }

    // The wires whose two devices are still there (a device removed with the Device tool takes its wires with it).
    private IReadOnlyList<(TileCoord A, TileCoord B)> LiveEditorWires() =>
        _editorWires.Where(w => _editorDeviceKinds.ContainsKey(w.A) && _editorDeviceKinds.ContainsKey(w.B)).ToList();

    // Where the device sits, in tile units: the centre of its footprint, which is exactly where TileShipBuilder exports it.
    private Vec2 EditorDeviceCenter(TileCoord anchor)
    {
        var kind = _editorDeviceKinds[anchor];
        var (width, height) = CustomDeviceFootprint.Size(kind);
        if (_editorDeviceRotation.TryGetValue(anchor, out var rotated) && rotated)
            (width, height) = (height, width);
        return new Vec2(anchor.X + width / 2f, anchor.Y + height / 2f);
    }

    // The wires as the definition wants them.
    private IReadOnlyList<CustomWireDef> BuildEditorWireDefs()
    {
        var defs = new List<CustomWireDef>();
        foreach (var (a, b) in LiveEditorWires())
        {
            var ca = EditorDeviceCenter(a);
            var cb = EditorDeviceCenter(b);
            defs.AddRange(EditorWiring.Wires(_editorDeviceKinds[a], (float)ca.X, (float)ca.Y, _editorDeviceKinds[b], (float)cb.X, (float)cb.Y));
        }
        return defs;
    }

    private TileCoord? EditorDeviceAnchorUnderMouse()
    {
        if (GridCellAt(_designMouse) is not { } cell)
            return null;
        return _editorDeviceFootprint.TryGetValue(new TileCoord(cell.X, cell.Y), out var anchor) ? anchor : null;
    }

    private void HandleWireToolInput(bool leftClicked, bool rightClicked)
    {
        if (rightClicked)
        {
            if (EditorDeviceAnchorUnderMouse() is { } anchor && _editorWires.RemoveAll(w => w.A == anchor || w.B == anchor) > 0)
                ShowEditorToast("Провода устройства сняты.");
            _editorWireStart = null;
            return;
        }
        if (!leftClicked)
            return;

        if (EditorDeviceAnchorUnderMouse() is not { } clicked)
        {
            _editorWireStart = null;
            return;
        }

        var kind = _editorDeviceKinds[clicked];
        if (!EditorWiring.IsWireable(kind))
        {
            ShowEditorToast("К этому устройству провод не подключается: питание берут реактор, батарея, распределитель, щитки и приборы.");
            return;
        }
        if (_editorWireStart is not { } start)
        {
            _editorWireStart = clicked;
            return;
        }
        if (start == clicked)
        {
            _editorWireStart = null; // a second click on the same one lets go of it
            return;
        }

        if (EditorWiring.Link(_editorDeviceKinds[start], kind).Count == 0)
        {
            ShowEditorToast($"{CustomDeviceCatalog.Name(_editorDeviceKinds[start])} и {CustomDeviceCatalog.Name(kind)} друг к другу не подключаются.");
            _editorWireStart = clicked;
            return;
        }
        if (_editorWires.Any(w => (w.A == start && w.B == clicked) || (w.A == clicked && w.B == start)))
        {
            // The same pair again takes the wire off - the quickest way to undo one.
            _editorWires.RemoveAll(w => (w.A == start && w.B == clicked) || (w.A == clicked && w.B == start));
            _editorWireStart = null;
            return;
        }
        _editorWires.Add((start, clicked));
        _editorWireStart = null;
    }

    // The wires as lines between the devices - thin and faint under any other tool, bold in wiring mode.
    private void DrawEditorWireLines(float alpha, float thickness)
    {
        foreach (var (a, b) in LiveEditorWires())
        {
            var ca = EditorDeviceCenter(a);
            var cb = EditorDeviceCenter(b);
            HudIcons.DrawLine(_spriteBatch, _pixel, WorldToEditorScreen((float)ca.X, (float)ca.Y), WorldToEditorScreen((float)cb.X, (float)cb.Y),
                EditorWireColor * alpha, thickness);
        }
    }

    // Wiring mode: everything but the devices that take power is dimmed, the wires are drawn over the top, and the one being laid follows the mouse.
    private void DrawEditorWiring()
    {
        // Dim the whole canvas, then draw the wireable devices' tiles lighter again so they stand out.
        _spriteBatch.Draw(_pixel, ShipEditorCanvas, Color.Black * 0.45f);
        foreach (var (anchor, kind) in _editorDeviceKinds)
        {
            if (!EditorWiring.IsWireable(kind))
                continue;
            var center = EditorDeviceCenter(anchor);
            var screen = WorldToEditorScreen((float)center.X, (float)center.Y);
            var radius = EditorCellSize * 0.55f;
            var tint = _editorWireStart == anchor ? EditorWireColor : new Color(120, 200, 255);
            HudIcons.DrawRingArc(_spriteBatch, _pixel, screen, radius, 0f, 360f, tint * 0.9f, 20, 2f);
        }

        DrawEditorWireLines(1f, 3f);

        if (_editorWireStart is { } pending && _editorDeviceKinds.ContainsKey(pending))
        {
            var from = EditorDeviceCenter(pending);
            HudIcons.DrawLine(_spriteBatch, _pixel, WorldToEditorScreen((float)from.X, (float)from.Y),
                new Vector2(_designMouse.X, _designMouse.Y), EditorWireColor * 0.6f, 2f);
        }
    }
}
