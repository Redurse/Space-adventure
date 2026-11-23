using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// The comms console as a map with stations to hail (direct user request: "теперь это будет карта... ПКМ по станции - связь... менюшка как в Cosmoteer").
// Rendering/CommsMapPanel.cs draws the map and the radio waves, Rendering/CommsStationWindow.cs the station's window; this is the state and the input between them.
public partial class Game1
{
    private CommsMapPanel _commsMapPanel = null!;
    private CommsStationWindow _commsStationWindow = null!;
    private Vector2 _commsPan = Vector2.Zero;               // the map's own move: zero = the ship in the middle
    private float _commsZoom = CommsMapPanel.DefaultZoom;
    private bool _commsRightDown;
    private Point _commsRightDownAt;
    private bool _commsDragged;
    private CommsStationWindow.Tab _commsTab = CommsStationWindow.Tab.Trade;
    private string? _commsShownLinkId;                      // which line the tab was last chosen for
    private string? _pendingHailPointId;
    private bool _pendingEndComms;
    private bool _commsConsoleWasOpen;

    private static readonly Rectangle CommsArea = new(0, 0, DesignWidth, DesignHeight);
    private const int CommsDragThresholdPixels = 6;

    // A line is open to a station: its window is up over the map.
    private static bool CommsLinked(WorldSnapshot? snapshot) => snapshot?.Comms is { LinkedPointId: not null };

    // Whether the store is the tab showing (and so the cart has to be kept, not reset every frame).
    private bool CommsTradeShown(WorldSnapshot? snapshot) =>
        _commsConsoleOpen && snapshot?.Comms is { LinkedPointId: not null } && _commsTab == CommsStationWindow.Tab.Trade;

    // Every frame: the map's right-drag move and right-click hail, a fresh tab for a new line, and hanging up when the console is shut.
    private void UpdateCommsMapInput(MouseState mouse)
    {
        var snapshot = _client.LatestSnapshot;
        var point = _designMouse;

        if (_commsConsoleWasOpen && !_commsConsoleOpen)
        {
            _pendingEndComms = true;           // leaving the console hangs up
            _commsPan = Vector2.Zero;
            _commsZoom = CommsMapPanel.DefaultZoom;
            _commsRightDown = false;
        }
        _commsConsoleWasOpen = _commsConsoleOpen;
        if (!_commsConsoleOpen)
            return;

        // A line opened (or changed): the first tab the station can serve.
        if (snapshot?.Comms is { LinkedPointId: { } linkedId } comms && linkedId != _commsShownLinkId)
        {
            _commsShownLinkId = linkedId;
            _commsTab = CommsStationWindow.FirstAvailable(comms);
            _tradeWindow.Reset();
        }
        else if (snapshot?.Comms?.LinkedPointId is null)
            _commsShownLinkId = null;

        if (CommsLinked(snapshot))
        {
            _commsRightDown = false;
            return; // the window is up: the map under it is not moved or hailed
        }

        var rightDown = mouse.RightButton == ButtonState.Pressed;
        if (rightDown && !_commsRightDown)
        {
            _commsRightDown = true;
            _commsRightDownAt = point;
            _commsDragged = false;
        }
        else if (rightDown && _commsRightDown)
        {
            var moved = new Vector2(point.X - _commsRightDownAt.X, point.Y - _commsRightDownAt.Y);
            if (_commsDragged || moved.Length() > CommsDragThresholdPixels)
            {
                _commsPan += new Vector2(point.X - _commsLastMouse.X, point.Y - _commsLastMouse.Y);
                _commsDragged = true;
            }
        }
        else if (!rightDown && _commsRightDown)
        {
            _commsRightDown = false;
            // A click, not a drag: right-clicking a station hails it.
            if (!_commsDragged && snapshot is not null && _commsMapPanel.StationAt(CommsArea, snapshot, _commsPan, _commsZoom, point) is { } stationId)
                _pendingHailPointId = stationId;
        }
        _commsLastMouse = point;
    }

    private Point _commsLastMouse;   // where the cursor was last frame, in design coordinates

    private (int, int, ItemType?, int, bool, bool, ShipUpgradeTrack?, string?) HandleCommsClick(WorldSnapshot? snapshot)
    {
        var none = (-1, -1, (ItemType?)null, -1, false, false, (ShipUpgradeTrack?)null, (string?)null);
        if (snapshot is null)
            return none;

        if (snapshot.Comms is not { LinkedPointId: not null } comms)
        {
            if (CommsMapPanel.RecenterButtonRect(CommsArea).Contains(_designMouse))
            {
                _commsPan = Vector2.Zero;
                _commsZoom = CommsMapPanel.DefaultZoom;
            }
            return none;
        }

        if (CommsStationWindow.EndRect.Contains(_designMouse))
        {
            _pendingEndComms = true;
            return none;
        }
        for (var i = 0; i < 3; i++)
            if (CommsStationWindow.TabRect(i).Contains(_designMouse))
            {
                if (CommsStationWindow.Available(comms, (CommsStationWindow.Tab)i))
                    _commsTab = (CommsStationWindow.Tab)i;
                return none;
            }

        switch (_commsTab)
        {
            case CommsStationWindow.Tab.Trade:
                _tradeWindow.PointOverride = comms.LinkedPointId;
                switch (_tradeWindow.HandleClick(snapshot, _client.PlayerId, _designMouse))
                {
                    case TradeClick.Close:
                        _pendingEndComms = true;
                        break;
                    case TradeClick.Confirm:
                        foreach (var command in _tradeWindow.TakeCommands())
                            _tradeQueue.Enqueue(command);
                        break;
                }
                break;
            case CommsStationWindow.Tab.Hire:
                if (CommsStationWindow.HireIndexAt(snapshot, _designMouse) is >= 0 and var hire)
                    _pendingHireCandidateId = snapshot.RecruitCandidates[hire].Id;
                break;
            default:
                if (snapshot.ActiveQuest is null && CommsStationWindow.JobIndexAt(_designMouse) is >= 0 and var job)
                {
                    _pendingQuestKind = StationPanel.OfferedQuestKinds[job];
                    return (-1, -1, null, -1, true, false, null, null);
                }
                break;
        }
        return none;
    }

    private void DrawCommsConsole(WorldSnapshot snapshot, float seconds)
    {
        var linked = CommsLinked(snapshot);
        _commsMapPanel.Draw(_spriteBatch, snapshot, CommsArea, _commsPan, _commsZoom, _designMouse, seconds, overWindow: linked);
        if (snapshot.Comms is not { LinkedPointId: not null } comms)
            return;

        var name = comms.LinkedName ?? "станция";
        switch (_commsTab)
        {
            case CommsStationWindow.Tab.Trade:
                _tradeWindow.PointOverride = comms.LinkedPointId;
                _tradeWindow.Draw(_spriteBatch, snapshot, _client.PlayerId, name, _designMouse);
                break;
            case CommsStationWindow.Tab.Hire:
                _commsStationWindow.DrawHire(_spriteBatch, snapshot, name, _designMouse);
                break;
            default:
                _commsStationWindow.DrawJobs(_spriteBatch, snapshot, name, _designMouse);
                break;
        }
        _commsStationWindow.DrawChrome(_spriteBatch, comms, _commsTab, _designMouse);
    }
}
