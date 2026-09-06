using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Anabiosis.Client.Rendering;

namespace Anabiosis.Client;

// Terminal panels can be dragged around the screen by their housing edge. They open centred, and
// wherever the player leaves one is where that kind of panel opens next time - a reactor readout
// parked out of the way stays out of the way.
//
// The grab band is the frame itself, not the whole housing: a panel full of clickable slots that
// also moves when you click a slot would be unusable, so only the metal around the content picks it
// up. That is also why this runs before UpdateItemDrag and can consume the press - otherwise
// grabbing the edge of the rack would start dragging whatever item happened to be under the cursor.
public partial class Game1
{
    // How wide the draggable band around the housing edge is, in design pixels. Wide enough to hit
    // without aiming, narrow enough that the outermost slot row is still clickable.
    private const int PanelGrabBand = 12;

    // Keyed by block kind, so each terminal remembers its own spot rather than all of them sharing
    // one. Empty until something is actually dragged: an unmoved panel stays centred, including
    // after the screen size changes.
    private readonly Dictionary<string, Vector2> _panelPositions = new();
    private string? _draggingPanel;
    private Vector2 _panelDragGrab;
    private ButtonState _prevPanelDragButton = ButtonState.Released;

    // Where a panel of `size` should lay its content out: wherever it was dragged to, or the middle
    // of the screen if it never was. Both the drawing and the hit testing call this, so they cannot
    // disagree about where the panel is.
    private Vector2 PanelOrigin(string key, Point size)
    {
        if (_panelPositions.TryGetValue(key, out var moved))
            return moved;
        return DevicePanelChrome.CentredOrigin(size, DesignScreen);
    }

    // The housing rectangle of whatever terminal is open, or null when none is. Used both for the
    // drag band and for deciding whether a click landed on the panel at all.
    private Rectangle? CurrentPanelHousing()
    {
        if (_openBlock.Kind == BlockKind.None)
            return null;

        var size = CurrentPanelSize;
        var origin = PanelOrigin(CurrentPanelKey, size);
        return new Rectangle(
            (int)origin.X - DevicePanelChrome.OriginInsetX,
            (int)origin.Y - DevicePanelChrome.OriginInsetY,
            size.X, size.Y);
    }

    // Is this point over any part of the interface, as opposed to the world? Used by the item drag:
    // letting go of an item over a panel means "never mind", while letting go over open floor means
    // "drop it there". Before this, releasing anywhere that was not a slot dropped the item - which
    // included the metal of the very panel you were rearranging, so a slightly-off release threw
    // your welder on the deck.
    private bool IsOverInterface(Point point)
    {
        if (CurrentPanelHousing() is { } panel && panel.Contains(point))
            return true;

        // The top bar of window buttons.
        for (var i = 0; i < 3; i++)
        {
            if (GetTopBarButtonRect(i).Contains(point))
                return true;
        }

        // The permanent HUD band along the bottom: hotbar, equipment row, role box. Taken as a full
        // width strip rather than each control's own rectangle - the gaps between them are not
        // somewhere anybody means to drop something either.
        return point.Y >= (int)HudBottom - InventoryPanel.SlotSize - 8;
    }

    // True when the press belongs to a panel drag and nothing else should look at it.
    private bool UpdatePanelDrag(MouseState mouse, string key, Point size)
    {
        var pressed = mouse.LeftButton == ButtonState.Pressed;
        var justPressed = pressed && _prevPanelDragButton == ButtonState.Released;
        _prevPanelDragButton = mouse.LeftButton;

        if (!pressed)
        {
            _draggingPanel = null;
            return false;
        }

        var origin = PanelOrigin(key, size);
        var housing = new Rectangle(
            (int)origin.X - DevicePanelChrome.OriginInsetX,
            (int)origin.Y - DevicePanelChrome.OriginInsetY,
            size.X, size.Y);

        if (justPressed && _draggingPanel is null)
        {
            // Inside the housing but not inside its content area - that ring is the grab band.
            var content = new Rectangle(housing.X + PanelGrabBand, housing.Y + PanelGrabBand,
                housing.Width - PanelGrabBand * 2, housing.Height - PanelGrabBand * 2);
            if (!housing.Contains(_designMouse) || content.Contains(_designMouse))
                return false;

            _draggingPanel = key;
            _panelDragGrab = new Vector2(_designMouse.X, _designMouse.Y) - origin;
        }

        if (_draggingPanel != key)
            return false;

        // Clamped so a panel can never be dragged far enough off screen to lose its own grab band,
        // which would leave it stranded with no way to bring it back.
        var wanted = new Vector2(_designMouse.X, _designMouse.Y) - _panelDragGrab;
        var minX = -size.X + PanelGrabBand * 3 + DevicePanelChrome.OriginInsetX;
        var maxX = DesignWidth - PanelGrabBand * 3 + DevicePanelChrome.OriginInsetX;
        var minY = DevicePanelChrome.OriginInsetY;
        var maxY = DesignHeight - PanelGrabBand * 3 + DevicePanelChrome.OriginInsetY;
        _panelPositions[key] = new Vector2(
            MathHelper.Clamp(wanted.X, minX, maxX),
            MathHelper.Clamp(wanted.Y, minY, maxY));
        return true;
    }

    // Window 2 of the helm redesign (M47 follow-up - "в виде мини виджета который можно двигать"):
    // its own small drag, separate from the _openBlock-keyed system above since this widget is
    // visible whenever the player is at the helm rather than tied to opening a console. Same
    // "only the title bar picks it up" reasoning as the housing-edge grab band above.
    private bool UpdateHelmWidgetDrag(MouseState mouse)
    {
        var pressed = mouse.LeftButton == ButtonState.Pressed;
        var justPressed = pressed && _prevHelmWidgetDragButton == ButtonState.Released;
        _prevHelmWidgetDragButton = mouse.LeftButton;

        if (!pressed)
        {
            _draggingHelmWidget = false;
            return false;
        }

        if (justPressed && !_draggingHelmWidget)
        {
            if (!HelmButtonsWidget.GetTitleBarRect(_helmWidgetPosition).Contains(_designMouse))
                return false;
            _draggingHelmWidget = true;
            _helmWidgetDragGrab = new Vector2(_designMouse.X, _designMouse.Y) - _helmWidgetPosition;
        }

        if (!_draggingHelmWidget)
            return false;

        var wanted = new Vector2(_designMouse.X, _designMouse.Y) - _helmWidgetDragGrab;
        _helmWidgetPosition = new Vector2(
            MathHelper.Clamp(wanted.X, 0, DesignWidth - HelmButtonsWidget.Size.X),
            MathHelper.Clamp(wanted.Y, 0, DesignHeight - HelmButtonsWidget.Size.Y));
        return true;
    }

    // The scanner console's own toggle-switch widget (M48 follow-up) - same drag mechanics as
    // window 2's button widget above, just its own independent position/state since it's shown
    // only to whoever is actually operating the console (BlockKind.Navigation), not at the helm.
    private bool UpdateScannerWidgetDrag(MouseState mouse)
    {
        var pressed = mouse.LeftButton == ButtonState.Pressed;
        var justPressed = pressed && _prevScannerWidgetDragButton == ButtonState.Released;
        _prevScannerWidgetDragButton = mouse.LeftButton;

        if (!pressed)
        {
            _draggingScannerWidget = false;
            return false;
        }

        if (justPressed && !_draggingScannerWidget)
        {
            if (!ScannerModeWidget.GetTitleBarRect(_scannerWidgetPosition).Contains(_designMouse))
                return false;
            _draggingScannerWidget = true;
            _scannerWidgetDragGrab = new Vector2(_designMouse.X, _designMouse.Y) - _scannerWidgetPosition;
        }

        if (!_draggingScannerWidget)
            return false;

        var wanted = new Vector2(_designMouse.X, _designMouse.Y) - _scannerWidgetDragGrab;
        _scannerWidgetPosition = new Vector2(
            MathHelper.Clamp(wanted.X, 0, DesignWidth - ScannerModeWidget.Size.X),
            MathHelper.Clamp(wanted.Y, 0, DesignHeight - ScannerModeWidget.Size.Y));
        return true;
    }
}
