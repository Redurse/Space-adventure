using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceAdventure.Client.Networking;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Server;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client;

// Mouse/keyboard reading, click routing (HandleMouseClick), item drag-and-drop, and the hint
// text these gate (ComputeHint/WiringHint) - everything that turns raw input into pending
// actions for Update to apply. Game1.cs owns the fields these read and write, plus the
// lifecycle methods (Update/Draw) that call into them.
public partial class Game1
{
    private static Vec2 ReadMoveInput(KeyboardState keyboard)
    {
        float x = 0, y = 0;
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left)) x -= 1;
        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right)) x += 1;
        if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up)) y -= 1;
        if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down)) y += 1;
        return new Vec2(x, y);
    }

    // Which way to swing the barrel so it ends up pointing at the cursor. Returns a traverse
    // direction rather than an angle, so the server stays the one authority on how fast a gun can
    // slew and how far its arc goes; the deadband stops the barrel hunting back and forth by
    // fractions of a degree once it's on target.
    private float ReadTurretAimTowardCursor()
    {
        if (_client.LatestSnapshot is not { } snapshot ||
            snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId) is not { } me ||
            MannedTurret(snapshot) is not { } manned)
            return 0f;

        var mount = TurretMount.For(snapshot.Rooms, snapshot.Turrets, manned.Turret);
        var origin = ComputeCamera(snapshot, me).Origin;
        // Through the same scale the scene batch draws with, or the cursor and the barrel would
        // disagree about where "over there" is.
        var mountOnScreen = (origin + new Vector2(mount.Position.X, mount.Position.Y) * ShipRenderer.PixelsPerUnit)
            * SceneZoom(snapshot);

        // The scene batch is also spun around the screen's center pivot while manning a turret
        // (TurretViewRotationDegrees, Draw's sceneTransform) - undo that same rotation on the
        // cursor first, or the aim would track a fixed screen direction instead of the mouse.
        var pivot = WorldViewportOrigin + WorldViewportSize / 2f;
        var rotationRadians = MathHelper.ToRadians(TurretViewRotationDegrees(snapshot));
        var designMouse = new Vector2(_designMouse.X, _designMouse.Y);
        var cursorUnrotated = pivot + Vector2.Transform(designMouse - pivot, Matrix.CreateRotationZ(-rotationRadians));

        var toCursor = new Vector2(cursorUnrotated.X - mountOnScreen.X, cursorUnrotated.Y - mountOnScreen.Y);
        if (toCursor.LengthSquared() < 1f)
            return 0f;

        var cursorDegrees = MathF.Atan2(toCursor.Y, toCursor.X) * (180f / MathF.PI);
        var wanted = Math.Clamp(ShortestAngle(cursorDegrees - mount.OutwardDegrees),
            manned.Turret.MinAimDegrees, manned.Turret.MaxAimDegrees);
        var delta = wanted - manned.State.AimDegrees;
        return MathF.Abs(delta) < 1f ? 0f : MathF.Sign(delta);
    }

    private static float ShortestAngle(float degrees) => ((degrees % 360f) + 540f) % 360f - 180f;

    // Reused for aim while manning a turret — movement is locked server-side at that point.
    private static float ReadAimDirection(KeyboardState keyboard)
    {
        float dir = 0;
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left)) dir -= 1;
        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right)) dir += 1;
        return dir;
    }

    // The field view centers its camera on the local player (FieldRenderer), so the direction from
    // screen-center to the cursor is exactly the world-space aim direction, no camera offset needed.
    // Mouse drag-and-drop between item slots (game_design.md section 13). Press on a slot that has
    // something in it picks it up; release over another slot moves it there (the server swaps, so
    // dropping onto an occupied slot exchanges the two). Release anywhere else - not a slot, not a
    // socket - drops the item on the floor where the character is standing (World.Storage.cs).
    private (SlotRef? From, SlotRef? To, bool ConsumedPress) UpdateItemDrag(MouseState mouse, double nowSeconds)
    {
        var pressed = mouse.LeftButton == ButtonState.Pressed;
        var justPressed = pressed && _prevDragButton == ButtonState.Released;
        var justReleased = !pressed && _prevDragButton == ButtonState.Pressed;
        _prevDragButton = mouse.LeftButton;

        if (_client.LatestSnapshot is not { } snapshot)
        {
            _dragFrom = null;
            _dragHighlightSlot = null;
            return (null, null, false);
        }

        if (justPressed)
        {
            if (HitTestItemSlot(snapshot) is { } slot && ItemInSlot(snapshot, slot) is not null)
            {
                var doubleClicked = _lastClickedSlot == slot && nowSeconds - _lastSlotClickSeconds < DoubleClickSeconds;
                _lastClickedSlot = slot;
                _lastSlotClickSeconds = doubleClicked ? double.NegativeInfinity : nowSeconds; // never chain a third click into a second move
                if (doubleClicked && QuickMoveTarget(snapshot, slot) is { } quickTarget)
                {
                    _dragFrom = null;
                    return (slot, quickTarget, true);
                }

                _dragFrom = slot;
                return (null, null, true);
            }
            return (null, null, false);
        }

        if (pressed && _dragFrom is { } dragging)
        {
            // Live feedback while the drag is in flight - only the valid case lights up, per
            // game_design.md section 13; an invalid hover just stays unmarked until release.
            var hovered = ResolveDropTarget(snapshot, dragging);
            _dragHighlightSlot = hovered is { Valid: true } ? hovered.Value.Slot : null;
            return (null, null, false);
        }

        if (justReleased && _dragFrom is { } from)
        {
            _dragFrom = null;
            _dragHighlightSlot = null;
            var resolved = ResolveDropTarget(snapshot, from);
            if (resolved is not { } target)
            {
                // Released over nothing - not a slot, not a socket: the item falls to the floor at
                // the character's own feet instead of just snapping back (server re-checks
                // reachability itself, same trust level as an ordinary slot-to-slot move).
                _pendingDropItemFrom = from;
                return (null, null, true);
            }

            if (target.TankAttach is { } attach)
            {
                _pendingTankAttach = attach;
                return (null, null, true);
            }

            if (!target.Valid)
            {
                _invalidDropSlot = target.Slot;
                _invalidDropFlashUntil = nowSeconds + InvalidDropFlashSeconds;
                return (null, null, true); // rejected - the item snaps back to `from`
            }

            return (from, target.Slot, true);
        }

        return (null, null, false);
    }

    private readonly record struct DropTarget(SlotRef Slot, bool Valid, (int From, int To)? TankAttach);

    // What releasing the drag over the current mouse position would do: null means "over nothing,
    // put it back silently"; TankAttach means the drop lands on a compatible tank socket (either
    // its hover-revealed band, or the tool's own icon) rather than swapping two slots; otherwise
    // Valid says whether the ordinary slot-to-slot move the server would perform is actually
    // reachable from here.
    private DropTarget? ResolveDropTarget(WorldSnapshot snapshot, SlotRef from)
    {
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        if (me?.Inventory is not { } inventory)
            return null;

        if (ItemInSlot(snapshot, from) is not { } draggedItem)
            return null;

        if (from.Kind == ItemSlotKind.Main && TankSockets.IsTank(draggedItem))
        {
            var toolSlot = HoveredToolSlotIndex(inventory, InventoryRowOrigin(inventory.MainSlots.Count));
            if (toolSlot is { } ts && ts != from.Index && inventory.MainSlots[ts] is { } toolItem &&
                TankSockets.AcceptedTank(toolItem) == draggedItem && inventory.MainSlotTanks[ts] is null)
                return new DropTarget(new SlotRef(ItemSlotKind.Main, ts), true, (from.Index, ts));
        }

        if (HitTestItemSlot(snapshot) is not { } to || to == from)
            return null;

        var valid = IsClientReachable(me, from) && IsClientReachable(me, to);
        return new DropTarget(to, valid, null);
    }

    private bool IsClientReachable(CharacterState me, SlotRef slot) => slot.Kind switch
    {
        ItemSlotKind.Main => !me.OnEnemyShip && !me.IsOutside,
        ItemSlotKind.Rack => _openBlock.Kind == BlockKind.Rack && !me.OnStation && !me.OnEnemyShip && !me.IsOutside,
        _ => false,
    };

    // Which row slot's tool socket the mouse is currently close enough to reveal - hovering either
    // the slot itself or the socket band that then appears above it (InventoryPanel.GetSocketRect
    // with above: true). Both count as "hovering this slot" so the socket doesn't wink out the
    // moment the cursor reaches for it.
    private int? HoveredToolSlotIndex(InventoryState inventory, Vector2 rowOrigin)
    {
        for (var i = 0; i < inventory.MainSlots.Count; i++)
        {
            if (inventory.MainSlots[i] is not { } item || !TankSockets.HasSocket(item))
                continue;
            var slotRect = InventoryPanel.GetMainSlotRect(i, rowOrigin);
            var socketRect = InventoryPanel.GetSocketRect(slotRect, above: true);
            if (slotRect.Contains(_designMouse) || socketRect.Contains(_designMouse))
                return i;
        }
        return null;
    }

    // targetSlot: a row index, or -1 for the suit being worn (Inventory.WornSuitSlot).
    private void QueueSocketClick(InventoryState inventory, int targetSlot)
    {
        var charge = targetSlot < 0 ? inventory.WornSuitTank : inventory.MainSlotTanks[targetSlot];
        if (charge is not null)
        {
            _pendingTankDetach = targetSlot;
            return;
        }

        var targetItem = targetSlot < 0
            ? (inventory.Equipped.TryGetValue(EquipSlot.Clothing, out var worn) ? worn : null)
            : inventory.MainSlots[targetSlot];
        if (targetItem is not { } owner || TankSockets.AcceptedTank(owner) is not { } accepted)
            return;

        // The tank has to be in hand, not merely carried - plugging one in is a two-handed job at
        // the same level of ceremony as everything else in this inventory. It also has to be the
        // kind this socket actually takes (TankSockets) - a welding tank never fits a cutter.
        foreach (var held in inventory.HeldMainSlotIndices)
            if (inventory.MainSlots[held] == accepted)
            {
                _pendingTankAttach = (held, targetSlot);
                return;
            }
    }

    private bool HoldingCutter() =>
        _client.LatestSnapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory is { } inventory
        && inventory.HeldMainSlotIndices.Any(i => inventory.MainSlots[i] == ItemType.Cutter);

    private bool HoldingWelder() =>
        _client.LatestSnapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory is { } inventory
        && inventory.HeldMainSlotIndices.Any(i => inventory.MainSlots[i] == ItemType.WeldingTool);

    private bool HoldingWireSpool() =>
        _client.LatestSnapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory is { } inventory
        && inventory.HeldMainSlotIndices.Any(i => inventory.MainSlots[i] == ItemType.WireSpool);

    // Gates ConnectionsPanel everywhere it opens (Junction/Distribution/SystemDevice/ComponentMount) -
    // wrench touches the hardware, screwdriver reads the wiring (World.ComponentMounts.cs).
    private bool HoldingScrewdriver() =>
        _client.LatestSnapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory is { } inventory
        && inventory.HeldMainSlotIndices.Any(i => inventory.MainSlots[i] == ItemType.Screwdriver);

    private const double DoubleClickSeconds = 0.4;

    // A hull carries two shelves (game_design.md section 13); SlotRef itself never needed a
    // "which shelf" field for this - a Rack slot's index is global across every shelf
    // (World.Storage.cs's RackFor), and this is the one place the client maps the shelf that's
    // actually open right now back to where its own 30-slot band starts in that global array.
    private int CurrentOpenRackOffset(WorldSnapshot snapshot)
    {
        if (_openBlock.TargetComponentId is not { } rackId)
            return 0;
        for (var i = 0; i < snapshot.StorageRacks.Count; i++)
            if (snapshot.StorageRacks[i].Id == rackId)
                return i * StorageRack.Capacity;
        return 0;
    }

    // Double-clicking an item sends it straight across to the container you have open, into the
    // first free slot. Clearing your hands into the rack is the common case, and dragging items
    // across one at a time to do it is busywork. Only armed while a rack is open — "across" has
    // to have somewhere to mean.
    private SlotRef? QuickMoveTarget(WorldSnapshot snapshot, SlotRef from)
    {
        if (_openBlock.Kind != BlockKind.Rack)
            return null;

        if (from.Kind == ItemSlotKind.Main)
        {
            var offset = CurrentOpenRackOffset(snapshot);
            for (var i = 0; i < StorageRack.Capacity; i++)
            {
                var globalIndex = offset + i;
                if (globalIndex < snapshot.RackSlots.Count && snapshot.RackSlots[globalIndex] is null)
                    return new SlotRef(ItemSlotKind.Rack, globalIndex);
            }
            return null; // this shelf is full — better to do nothing than to swap with an arbitrary slot
        }

        var inventory = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory;
        if (inventory is null)
            return null;

        for (var i = 0; i < inventory.MainSlots.Count; i++)
            if (inventory.MainSlots[i] is null)
                return new SlotRef(ItemSlotKind.Main, i);
        return null;
    }

    private SlotRef? HitTestItemSlot(WorldSnapshot snapshot)
    {
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        if (me?.Inventory is { } inventory)
            for (var i = 0; i < inventory.MainSlots.Count; i++)
                if (InventoryPanel.GetMainSlotRect(i, InventoryRowOrigin(inventory.MainSlots.Count)).Contains(_designMouse))
                    return new SlotRef(ItemSlotKind.Main, i);

        if (_openBlock.Kind == BlockKind.Rack)
        {
            var offset = CurrentOpenRackOffset(snapshot);
            for (var i = 0; i < StorageRack.Capacity; i++)
                if (RackPanel.GetSlotRect(i, PowerPanelOrigin).Contains(_designMouse))
                    return new SlotRef(ItemSlotKind.Rack, offset + i);
        }

        return null;
    }

    private ItemType? ItemInSlot(WorldSnapshot snapshot, SlotRef slot)
    {
        if (slot.Kind == ItemSlotKind.Rack)
            return slot.Index < snapshot.RackSlots.Count ? snapshot.RackSlots[slot.Index] : null;

        var inventory = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory;
        return inventory is not null && slot.Index < inventory.MainSlots.Count ? inventory.MainSlots[slot.Index] : null;
    }

    private Rectangle GetSlotScreenRect(SlotRef slot, Vector2 rowOrigin) => slot.Kind == ItemSlotKind.Main
        ? InventoryPanel.GetMainSlotRect(slot.Index, rowOrigin)
        : RackPanel.GetSlotRect(slot.Index - CurrentOpenRackOffset(_client.LatestSnapshot!), PowerPanelOrigin);

    // A translucent fill plus a bright outline - visible over a slot's own contents without hiding
    // what's in it, unlike the opaque colours DrawSlot uses for the item itself.
    private void DrawSlotHighlight(Rectangle rect, Color color)
    {
        _spriteBatch.Draw(_pixel, rect, color * 0.35f);
        const int thickness = 2;
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    private Vec2 ReadPushOffDirection()
    {
        var screenCenter = WorldViewportOrigin + WorldViewportSize / 2f;
        var offset = new Vector2(_designMouse.X - screenCenter.X, _designMouse.Y - screenCenter.Y);
        var vec = new Vec2(offset.X, offset.Y);
        return vec.Length() > 0.0001f ? vec.Normalized() : Vec2.Zero;
    }

    private static int? ReadPowerSystemSelection(KeyboardState keyboard)
    {
        if (keyboard.IsKeyDown(Keys.D1)) return 0;
        if (keyboard.IsKeyDown(Keys.D2)) return 1;
        if (keyboard.IsKeyDown(Keys.D3)) return 2;
        if (keyboard.IsKeyDown(Keys.D4)) return 3;
        if (keyboard.IsKeyDown(Keys.D5)) return 4;
        return null;
    }

    private static float ReadPowerDirection(KeyboardState keyboard)
    {
        float direction = 0;
        if (keyboard.IsKeyDown(Keys.Q)) direction -= 1;
        if (keyboard.IsKeyDown(Keys.E)) direction += 1;
        return direction;
    }

    // One left click handles, in priority order: (1) the Barotrauma-style hold strip under an
    // inventory slot, (2) a reactor fuel-rod slot while the reactor is open, (3) a galaxy map
    // point while the navigation console is open, (4) the Trader's buy/sell lists, the
    // Administrator's quest button, or the Mechanic's upgrade list while talking to them, (4.5)
    // a wire's line while the wiring panel is open, (4.6) the helm's stabilize button while
    // manning it, (5) opening/closing a block by clicking it on the ship view (requires standing
    // close), (6) clicking empty space closes whatever's open. Edge-triggered so a held button
    // doesn't spam. The helm joystick's continuous drag is handled separately (UpdateHelmThrustDrag)
    // since it isn't an edge-triggered click.
    private (int ToggleHoldSlotIndex, int ToggleReactorSlotIndex, string? TravelToPointId, ItemType? BuyItemType, int SellSlotIndex, bool AcceptCargoQuestPressed, bool TurnInCargoQuestPressed, ShipUpgradeTrack? PurchaseUpgradeTrack, bool HelmStabilizePressed, string? DoorToggleId) HandleMouseClick(MouseState mouse)
    {
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevLeftMouseButton == ButtonState.Released;
        _prevLeftMouseButton = mouse.LeftButton;
        if (!clicked)
            return (-1, -1, null, null, -1, false, false, null, false, null);

        var snapshot = _client.LatestSnapshot;
        var me = snapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);

        // Behind a periscope the mouse is the gunsight and nothing else. The scene is drawn from
        // out at the gun and at half scale, so every world-space hit test below would be pointing
        // at whatever used to be under the cursor rather than what's there now.
        if (snapshot is not null && MannedTurret(snapshot) is not null)
            return (-1, -1, null, null, -1, false, false, null, false, null);

        var slotCount = me?.Inventory?.MainSlots.Count ?? 0;
        for (var i = 0; i < slotCount; i++)
        {
            if (InventoryPanel.GetHoldStripRect(i, InventoryRowOrigin(slotCount)).Contains(_designMouse))
                return (i, -1, null, null, -1, false, false, null, false, null);
        }

        if (snapshot is null || me is null)
            return (-1, -1, null, null, -1, false, false, null, false, null);

        if (me.IsAtHelm && HelmPanel.GetStabilizeButtonRect(HelmPanelOrigin).Contains(_designMouse))
            return (-1, -1, null, null, -1, false, false, null, true, null);

        // Only armed while the server says the ship is actually alongside the berth - clicking a
        // dimmed "distance to port" readout does nothing.
        if (me.IsAtHelm && snapshot.CanDock && HelmPanel.GetDockButtonRect(HelmPanelOrigin).Contains(_designMouse))
        {
            _pendingDock = true;
            return (-1, -1, null, null, -1, false, false, null, false, null);
        }

        if (_openBlock.Kind == BlockKind.Reactor)
        {
            for (var i = 0; i < snapshot.Reactor.RodCharges.Count; i++)
            {
                if (ReactorPanel.GetSlotRect(i, PowerPanelOrigin).Contains(_designMouse))
                    return (-1, i, null, null, -1, false, false, null, false, null);
            }
        }

        if (_openBlock.Kind == BlockKind.Navigation)
        {
            var mapOrigin = GalaxyMapPanel.ComputeMapOrigin(GalaxyMapPanelOrigin, snapshot.GalaxyPoints, _mapZoom, _mapPanOffset);
            foreach (var point in snapshot.GalaxyPoints)
            {
                if (GalaxyMapPanel.GetPointRect(point, mapOrigin, _mapZoom).Contains(_designMouse))
                    return (-1, -1, point.Id, null, -1, false, false, null, false, null);
            }

            // Warp target - rides as a field like the other Administrator/Recruiter actions
            // (GalaxyMapPanel's own tuple is already at its practical limit).
            var otherSystems = snapshot.StarSystems.Where(s => s.Id != snapshot.CurrentSystemId).ToList();
            for (var i = 0; i < otherSystems.Count; i++)
            {
                if (!GalaxyMapPanel.GetSystemRect(i, GalaxyMapPanelOrigin).Contains(_designMouse))
                    continue;
                _pendingWarpToSystemId = otherSystems[i].Id;
                return (-1, -1, null, null, -1, false, false, null, false, null);
            }
        }

        if (_openBlock.Kind == BlockKind.Station)
        {
            var talkingToKind = snapshot.StationNpcs.FirstOrDefault(n => n.Id == _talkingToNpcId)?.Kind;

            if (talkingToKind == NpcKind.Trader)
            {
                for (var i = 0; i < TradeCatalog.Goods.Count; i++)
                {
                    if (StationPanel.GetGoodRect(i, StationPanelOrigin).Contains(_designMouse))
                        return (-1, -1, null, TradeCatalog.Goods[i].Item, -1, false, false, null, false, null);
                }

                for (var i = 0; i < slotCount; i++)
                {
                    if (StationPanel.GetSellRect(i, StationPanelOrigin).Contains(_designMouse))
                        return (-1, -1, null, null, i, false, false, null, false, null);
                }
            }

            if (talkingToKind == NpcKind.Administrator)
            {
                if (snapshot.ActiveQuest is not { } quest)
                {
                    // Job board: one clickable row per kind on offer (StationPanel).
                    for (var i = 0; i < StationPanel.OfferedQuestKinds.Length; i++)
                    {
                        if (!StationPanel.GetQuestOfferRect(i, StationPanelOrigin).Contains(_designMouse))
                            continue;
                        _pendingQuestKind = StationPanel.OfferedQuestKinds[i];
                        return (-1, -1, null, null, -1, true, false, null, false, null);
                    }
                }
                else if (StationPanel.GetAdminActionRect(StationPanelOrigin).Contains(_designMouse))
                {
                    // Mirrors StationPanel.DrawAdminQuest's own turn-in test - deliveries hand in
                    // at the destination, everything else back where it was issued.
                    var turnInHere = quest.Kind == QuestKind.Delivery
                        ? quest.DestinationPointId == snapshot.Voyage.DockedPointId
                        : quest.IssuedByPointId == snapshot.Voyage.DockedPointId;
                    if (turnInHere)
                        return (-1, -1, null, null, -1, false, true, null, false, null);

                    // Same button, opposite offer, when the job can't be finished here (Station
                    // Panel's own tuple is already at its practical limit, so this rides as a
                    // field like the other Administrator/Recruiter actions above).
                    _pendingAbandonQuest = true;
                    return (-1, -1, null, null, -1, false, false, null, false, null);
                }
            }

            if (talkingToKind == NpcKind.Mechanic)
            {
                for (var i = 0; i < ShipUpgradeCatalog.Tracks.Count; i++)
                {
                    if (StationPanel.GetUpgradeRect(i, StationPanelOrigin).Contains(_designMouse))
                        return (-1, -1, null, null, -1, false, false, ShipUpgradeCatalog.Tracks[i].Track, false, null);
                }
            }

            if (talkingToKind == NpcKind.Shipwright)
            {
                for (var i = 0; i < StationPanel.PurchasableShipKinds.Length; i++)
                {
                    if (!StationPanel.GetShipRect(i, StationPanelOrigin).Contains(_designMouse))
                        continue;
                    _pendingShipPurchase = StationPanel.PurchasableShipKinds[i];
                    return (-1, -1, null, null, -1, false, false, null, false, null);
                }
            }

            if (talkingToKind == NpcKind.Recruiter)
            {
                for (var i = 0; i < snapshot.RecruitCandidates.Count; i++)
                {
                    if (!StationPanel.GetCandidateRect(i, StationPanelOrigin).Contains(_designMouse))
                        continue;
                    _pendingHireCandidateId = snapshot.RecruitCandidates[i].Id;
                    return (-1, -1, null, null, -1, false, false, null, false, null);
                }
            }
        }

        // Physically standing on the station (game_design.md section 10 - walk up and click an
        // NPC in their own room). Same camera and coordinates as the ship's own interior now, but
        // none of the ship-block clicks below are reachable from over here anyway.
        if (me.OnStation)
        {
            var stationOrigin = ComputeCamera(snapshot, me).Origin;
            foreach (var npc in snapshot.StationNpcs)
            {
                if (npc.Kind == NpcKind.Security)
                    continue; // there's nothing to discuss with the guard - only to avoid them
                if (!StationRenderer.GetNpcRect(npc, stationOrigin).Contains(_designMouse))
                    continue;
                _talkingToNpcId = _talkingToNpcId == npc.Id ? null : npc.Id;
                _openBlock = _talkingToNpcId is null ? ClickTarget.None : ClickTarget.Station;
                return (-1, -1, null, null, -1, false, false, null, false, null);
            }

            _openBlock = ClickTarget.None;
            _talkingToNpcId = null;
            return (-1, -1, null, null, -1, false, false, null, false, null);
        }

        var myPosition = new Vec2(me.X, me.Y);
        bool NearEnough(Vec2 blockPosition) => (blockPosition - myPosition).Length() < TurretInteractionRadius;
        var origin = ComputeCamera(snapshot, me).Origin;

        // Screwdriver "open the panel" view (World.Wiring.cs's component graph, ConnectionsPanel) -
        // a second click on the same component closes it again, same as every other block below.
        ClickTarget ToggleConnections(string componentId) =>
            _openBlock.Kind == BlockKind.Connections && _openBlock.TargetComponentId == componentId
                ? ClickTarget.None
                : ClickTarget.ForConnections(componentId);

        if (NearEnough(snapshot.ReactorBlock.Position) &&
            ShipRenderer.GetBlockRect(snapshot.ReactorBlock.Position, ShipRenderer.BigBlockSize, origin).Contains(_designMouse))
        {
            _openBlock = _openBlock.Kind == BlockKind.Reactor ? ClickTarget.None : ClickTarget.Reactor;
            return (-1, -1, null, null, -1, false, false, null, false, null);
        }

        if (NearEnough(snapshot.DistributionBlock.Position) &&
            ShipRenderer.GetBlockRect(snapshot.DistributionBlock.Position, ShipRenderer.MediumBlockSize, origin).Contains(_designMouse))
        {
            if (HoldingScrewdriver() && snapshot.Components.FirstOrDefault(c => c.Kind == ComponentKind.Distribution) is { } distribution)
                _openBlock = ToggleConnections(distribution.Id);
            else
                _openBlock = _openBlock.Kind == BlockKind.Distribution ? ClickTarget.None : ClickTarget.Distribution;
            return (-1, -1, null, null, -1, false, false, null, false, null);
        }

        if (NearEnough(snapshot.NavigationConsole.Position) &&
            ShipRenderer.GetBlockRect(snapshot.NavigationConsole.Position, ShipRenderer.MediumBlockSize, origin).Contains(_designMouse))
        {
            _openBlock = _openBlock.Kind == BlockKind.Navigation ? ClickTarget.None : ClickTarget.Navigation;
            return (-1, -1, null, null, -1, false, false, null, false, null);
        }

        foreach (var rack in snapshot.StorageRacks)
        {
            if (!NearEnough(rack.Position) || !ShipRenderer.GetBlockRect(rack.Position, ShipRenderer.MediumBlockSize, origin).Contains(_designMouse))
                continue;
            _openBlock = _openBlock.Kind == BlockKind.Rack && _openBlock.TargetComponentId == rack.Id
                ? ClickTarget.None
                : ClickTarget.ForRack(rack.Id);
            return (-1, -1, null, null, -1, false, false, null, false, null);
        }

        foreach (var device in snapshot.SystemDevices)
        {
            var size = device.System == PowerSystemId.Engine ? ShipRenderer.BigBlockSize : ShipRenderer.NormalBlockSize;
            if (NearEnough(device.Position) && ShipRenderer.GetBlockRect(device.Position, size, origin).Contains(_designMouse))
            {
                _openBlock = HoldingScrewdriver()
                    ? ToggleConnections(device.Id)
                    : _openBlock.Kind == BlockKind.System && _openBlock.System == device.System
                        ? ClickTarget.None
                        : ClickTarget.ForSystem(device.System);
                return (-1, -1, null, null, -1, false, false, null, false, null);
            }
        }

        // Junction boxes ("щитки") have no function of their own to click for - only screwdriver
        // opens anything here, unlike Distribution/SystemDevice above which fall back to their
        // normal panel otherwise.
        if (HoldingScrewdriver())
        {
            foreach (var junction in snapshot.Components.Where(c => c.Kind == ComponentKind.Junction))
            {
                if (!NearEnough(junction.Position) || !ShipRenderer.GetBlockRect(junction.Position, ShipRenderer.NormalBlockSize, origin).Contains(_designMouse))
                    continue;
                _openBlock = ToggleConnections(junction.Id);
                return (-1, -1, null, null, -1, false, false, null, false, null);
            }
        }

        // Tank sockets, before anything in the world: they sit over/under the inventory row, so a
        // click there is never meant for the deck behind it. Empty socket + a matching tank in hand
        // plugs it in; a filled one gives the tank back. A cutter/welder's socket only exists to
        // click once the row has revealed it by hovering (InventoryPanel.Draw's hoveredMainSlotIndex).
        if (snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory is { } tankInventory)
        {
            for (var i = 0; i < tankInventory.MainSlots.Count; i++)
            {
                var item = tankInventory.MainSlots[i];
                if (item is not { } carried || !TankSockets.HasSocket(carried))
                    continue;
                if (!InventoryPanel.GetSocketRect(InventoryPanel.GetMainSlotRect(i, InventoryRowOrigin(tankInventory.MainSlots.Count)), above: true).Contains(_designMouse))
                    continue;
                QueueSocketClick(tankInventory, i);
                return (-1, -1, null, null, -1, false, false, null, false, null);
            }

            for (var i = 0; i < InventoryPanel.EquipSlots.Length; i++)
            {
                var worn = tankInventory.Equipped.TryGetValue(InventoryPanel.EquipSlots[i].Id, out var e) ? e : null;
                if (worn is not { } wornItem || !TankSockets.HasSocket(wornItem))
                    continue;
                if (!InventoryPanel.GetSocketRect(InventoryPanel.GetSlotRect(i, EquipSlotsOrigin), above: true).Contains(_designMouse))
                    continue;
                QueueSocketClick(tankInventory, -1); // Inventory.WornSuitSlot
                return (-1, -1, null, null, -1, false, false, null, false, null);
            }
        }

        // Component pins/mounts (World.Wiring.cs/World.ComponentMounts.cs, M19-M23): a pin only
        // responds while a WireSpool is actually held (laying or finishing a wire); a mount's body
        // only responds while one is NOT held, so a mid-lay click on a body never gets misread as
        // install/uninstall/operate. NearEnough still gates both, same as every other physical
        // fixture - the server re-checks pin proximity itself (a real in-world action, unlike the
        // old panel), but the mount interact is server-trusted like DoorToggleId.
        if (HoldingWireSpool())
        {
            foreach (var (pin, rect) in ComponentRenderer.AllPinHitRects(snapshot, origin))
            {
                if (!rect.Contains(_designMouse))
                    continue;
                var owner = snapshot.Components.FirstOrDefault(c => c.Id == pin.ComponentId);
                if (owner is null || !NearEnough(owner.Position))
                    continue;
                _pendingPinInteract = pin;
                return (-1, -1, null, null, -1, false, false, null, false, null);
            }
        }
        else
        {
            foreach (var mount in snapshot.ComponentMounts)
            {
                if (!NearEnough(mount.Position) || !ComponentRenderer.GetMountBodyRect(mount, origin).Contains(_designMouse))
                    continue;

                // Screwdriver on an occupied mount opens Connections locally instead of sending the
                // interact command - it's a pure client-side view, no server round-trip needed, and
                // it must not also fire the wrench-only uninstall on the server.
                var installedId = snapshot.ComponentMountStates.FirstOrDefault(s => s.MountId == mount.Id)?.InstalledComponentId;
                if (HoldingScrewdriver() && installedId is not null)
                    _openBlock = ToggleConnections(installedId);
                else
                    _pendingComponentMountInteractId = mount.Id;
                return (-1, -1, null, null, -1, false, false, null, false, null);
            }
        }

        // Dropped items (World.Storage.cs's drag-to-floor, World.Mining.cs's ore chunks): ship and
        // station floors share this scene's ordinary origin, same as doors/mounts above. EVA-space
        // ones need the same world->local fold FieldRenderer's own WorldToScreen closure uses, since
        // they live in the asteroid field, not this ship-local frame.
        foreach (var dropped in snapshot.DroppedItems.Where(d => d.RoomId is not null))
        {
            if (!NearEnough(dropped.Position) || !ShipRenderer.GetDroppedItemRect(dropped, origin).Contains(_designMouse))
                continue;
            _pendingPickupDroppedItemId = dropped.Id;
            return (-1, -1, null, null, -1, false, false, null, false, null);
        }

        if (me.IsOutside)
        {
            var hullCenter = ComputeCamera(snapshot, me).HullCenter;
            foreach (var dropped in snapshot.DroppedItems.Where(d => d.RoomId is null))
            {
                var local = ShipLocalFrame.ToLocal(dropped.Position, snapshot.ShipField, hullCenter);
                var screenPos = origin + new Vector2(local.X, local.Y) * ShipRenderer.PixelsPerUnit;
                var rect = new Rectangle(
                    (int)screenPos.X - ShipRenderer.DroppedItemHitSize / 2, (int)screenPos.Y - ShipRenderer.DroppedItemHitSize / 2,
                    ShipRenderer.DroppedItemHitSize, ShipRenderer.DroppedItemHitSize);
                if (!rect.Contains(_designMouse))
                    continue;
                _pendingPickupDroppedItemId = dropped.Id;
                return (-1, -1, null, null, -1, false, false, null, false, null);
            }
        }

        // Doors toggle directly on click - no panel to open, just an immediate flip
        // (game_design.md Phase 3, M16).
        foreach (var door in snapshot.Doors)
        {
            if (NearEnough(door.Position) && ShipRenderer.GetDoorRect(door.Left, door.Top, door.Width, door.Height, origin).Contains(_designMouse))
                return (-1, -1, null, null, -1, false, false, null, false, door.Id);
        }

        foreach (var outerDoor in snapshot.AirlockOuterDoors)
        {
            if (NearEnough(outerDoor.Position) && ShipRenderer.GetDoorRect(outerDoor.Left, outerDoor.Top, outerDoor.Width, outerDoor.Height, origin).Contains(_designMouse))
                return (-1, -1, null, null, -1, false, false, null, false, outerDoor.Id);
        }

        // Aboard a boarded hull the doors are the fight: they start closed, and opening one lets
        // the breach through into the next compartment (World.EnemyAtmosphere.cs). Same click, same
        // proximity rule - the character's own coordinates are that structure's while aboard it.
        foreach (var door in snapshot.EnemyShipDoors)
        {
            if (NearEnough(door.Position) && ShipRenderer.GetDoorRect(door.Left, door.Top, door.Width, door.Height, origin).Contains(_designMouse))
                return (-1, -1, null, null, -1, false, false, null, false, door.Id);
        }

        _openBlock = ClickTarget.None;
        _talkingToNpcId = null;
        return (-1, -1, null, null, -1, false, false, null, false, null);
    }

    // Flying the ship: W ahead, X astern, A/D swing the bow, S brakes. The mouse used to drag a
    // joystick that set a world-space thrust vector, which meant the pilot could aim the ship's
    // course but never its heading - and on a hull whose guns and airlock face particular
    // directions, heading is the thing you actually steer.
    private static (float Throttle, float Turn) ReadHelmInput(KeyboardState keyboard)
    {
        var throttle = 0f;
        if (keyboard.IsKeyDown(Keys.W)) throttle += 1f;
        if (keyboard.IsKeyDown(Keys.X)) throttle -= 1f;

        var turn = 0f;
        if (keyboard.IsKeyDown(Keys.A)) turn -= 1f;
        if (keyboard.IsKeyDown(Keys.D)) turn += 1f;

        return (throttle, turn);
    }

    // Walking out of interaction range auto-closes whatever's open — matches the same radius
    // that gated opening it in the first place, so you can't keep adjusting a slider you've
    // wandered away from.
    private void CloseBlockIfWalkedAway(WorldSnapshot? snapshot)
    {
        if (_openBlock.Kind == BlockKind.None || snapshot is null)
            return;

        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        if (me is null)
            return;

        // Station dialogue closes as soon as you're not next to the NPC you were talking to (or
        // not on the station at all any more) - a separate coordinate space from every other
        // block below, so it can't share their myPosition-based distance check.
        if (_openBlock.Kind == BlockKind.Station)
        {
            var talkingTo = snapshot.StationNpcs.FirstOrDefault(n => n.Id == _talkingToNpcId);
            var stillNear = me.OnStation && talkingTo is not null &&
                (talkingTo.Position - new Vec2(me.X, me.Y)).Length() < TurretInteractionRadius;
            if (!stillNear)
            {
                _openBlock = ClickTarget.None;
                _talkingToNpcId = null;
            }
            return;
        }

        var myPosition = new Vec2(me.X, me.Y);
        var blockPosition = _openBlock.Kind switch
        {
            BlockKind.Reactor => snapshot.ReactorBlock.Position,
            BlockKind.Distribution => snapshot.DistributionBlock.Position,
            BlockKind.Navigation => snapshot.NavigationConsole.Position,
            BlockKind.Rack => _openBlock.TargetComponentId is { } rackId
                ? snapshot.StorageRacks.FirstOrDefault(r => r.Id == rackId)?.Position ?? myPosition
                : myPosition,
            BlockKind.Connections => _openBlock.TargetComponentId is { } targetId
                ? snapshot.Components.FirstOrDefault(c => c.Id == targetId)?.Position ?? myPosition
                : myPosition,
            BlockKind.System => snapshot.SystemDevices.First(d => d.System == _openBlock.System).Position,
            _ => myPosition,
        };

        if ((blockPosition - myPosition).Length() >= TurretInteractionRadius)
        {
            _openBlock = ClickTarget.None;
            _talkingToNpcId = null;
        }
    }

    // Mirrors HandleMouseClick's own gating exactly (pins only respond holding a spool, mounts only
    // when not) so the hint never promises a click that the click handler would then ignore.
    private string? WiringHint(WorldSnapshot snapshot, CharacterState me)
    {
        var myPosition = new Vec2(me.X, me.Y);
        bool NearEnough(Vec2 p) => (p - myPosition).Length() < TurretInteractionRadius;
        var origin = ComputeCamera(snapshot, me).Origin;

        PinRef? HoveredPin()
        {
            foreach (var (pin, rect) in ComponentRenderer.AllPinHitRects(snapshot, origin))
            {
                if (!rect.Contains(_designMouse))
                    continue;
                var owner = snapshot.Components.FirstOrDefault(c => c.Id == pin.ComponentId);
                if (owner is not null && NearEnough(owner.Position))
                    return pin;
            }
            return null;
        }

        if (me.LayingWireFromPin is { } start)
        {
            var hovered = HoveredPin();
            if (hovered is { } h && h == start)
                return $"[ЛКМ]/[ПКМ] отменить провод от {ComponentRenderer.PinLabel(snapshot, start)}";
            if (hovered is { } target)
                return $"[ЛКМ] закончить провод: {ComponentRenderer.PinLabel(snapshot, start)} → {ComponentRenderer.PinLabel(snapshot, target)}";
            return $"Ведём провод от {ComponentRenderer.PinLabel(snapshot, start)} — наведите на контакт  [ПКМ] отменить";
        }

        if (HoldingWireSpool())
        {
            if (HoveredPin() is { } pin)
                return $"[ЛКМ] начать провод: {ComponentRenderer.PinLabel(snapshot, pin)}";
            return null;
        }

        foreach (var mount in snapshot.ComponentMounts)
        {
            if (!NearEnough(mount.Position) || !ComponentRenderer.GetMountBodyRect(mount, origin).Contains(_designMouse))
                continue;

            var installedId = snapshot.ComponentMountStates.FirstOrDefault(s => s.MountId == mount.Id)?.InstalledComponentId;
            if (installedId is null)
            {
                var heldKind = HeldItemTypes(me.Inventory).Select(ComponentDefinitions.ComponentKindFor).FirstOrDefault(k => k is not null);
                return heldKind is { } kind
                    ? $"[ЛКМ] установить: {ComponentDefinitions.DisplayName(kind)}"
                    : "Нужен компонент в руке";
            }

            var installed = snapshot.Components.FirstOrDefault(c => c.Id == installedId);
            if (installed is null)
                return null;

            // Screwdriver wins over Relay's own empty-hands toggle - it's still reachable by letting
            // go of the tool, and "what's this wired to" is worth asking about a Relay too.
            if (HoldingScrewdriver())
                return $"[ЛКМ] подключения: {ComponentDefinitions.DisplayName(installed.Kind)}";

            if (installed.Kind == ComponentKind.Relay)
                return "[ЛКМ] нажать реле";

            return HeldItemTypes(me.Inventory).Contains(ItemType.Wrench)
                ? $"[ЛКМ] снять: {ComponentDefinitions.DisplayName(installed.Kind)}"
                : $"{ComponentDefinitions.DisplayName(installed.Kind)}  (гаечный ключ — снять, отвёртка — подключения)";
        }

        return null;
    }

    private string ComputeHint(WorldSnapshot snapshot, int playerId)
    {
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId);
        if (me is null)
            return string.Empty;

        if (me.SuitActionRemaining > 0)
            return $"Экипировка... {me.SuitActionRemaining:0.0}с";

        var seatedAtTurret = snapshot.TurretStates.Any(t => t.MannedByPlayerId == playerId);
        if (!me.OnStation && !me.OnEnemyShip && !me.IsOutside && !me.IsAtHelm && !seatedAtTurret &&
            WiringHint(snapshot, me) is { } wiringHint)
            return wiringHint;

        if (snapshot.TurretStates.FirstOrDefault(t => t.MannedByPlayerId == playerId) is { } manned)
            return $"Наводка мышью ({manned.AimDegrees:0}°)  [Space] огонь  [F] встать";

        if (me.IsAtHelm)
            return "[W] ход  [X] назад  [A/D] поворот  [S] стабилизация  [F] встать";

        if (me.OnEnemyShip)
        {
            var boardingPosition = new Vec2(me.X, me.Y);
            var weapon = HeldItemTypes(me.Inventory).FirstOrDefault(WeaponDefinitions.IsWeapon);
            if (!WeaponDefinitions.IsWeapon(weapon))
                return "Нужно оружие в руках!  [WASD] отступить к пробоине";

            // CharacterState carries no RoomId, so the hint derives the room the same way the
            // interior hint already does for breaches - by which room rect contains the position.
            var boardingRoom = snapshot.EnemyShipRooms.FirstOrDefault(r => r.Contains(boardingPosition));
            var inRange = snapshot.EnemyCrew.Any(c => c.Alive && c.RoomId == boardingRoom?.Id &&
                (new Vec2(c.X, c.Y) - boardingPosition).Length() <= WeaponDefinitions.Range(weapon));
            var remaining = snapshot.EnemyCrew.Count(c => c.Alive);
            return inRange
                ? $"[Space] огонь ({ItemDefinitions.DisplayName(weapon)})  Осталось врагов: {remaining}"
                : $"Абордаж. Осталось врагов: {remaining}";
        }

        if (me.OnStation)
        {
            var stationPosition = new Vec2(me.X, me.Y);

            if (snapshot.StationGuards.Any(g => g.Alive && g.Alerted))
                return "Охрана открыла огонь!  [Space] отстреливаться  [WASD] к шлюзу";

            var nearCrate = snapshot.StationCrates.FirstOrDefault(c =>
                !(snapshot.StationCrateStates.FirstOrDefault(s => s.CrateId == c.Id)?.Looted ?? false) &&
                (c.Position - stationPosition).Length() < TurretInteractionRadius);
            if (nearCrate is not null)
                return $"[F] украсть: {ItemDefinitions.DisplayName(nearCrate.Item)} (охрана не должна увидеть)";

            var nearNpc = snapshot.StationNpcs.FirstOrDefault(n =>
                n.Kind != NpcKind.Security && (n.Position - stationPosition).Length() < TurretInteractionRadius);
            if (nearNpc is not null)
                return $"[ЛКМ] поговорить: {nearNpc.Name}";

            var nearGuard = snapshot.StationNpcs.Any(n =>
                n.Kind == NpcKind.Security && (n.Position - stationPosition).Length() < 4f);
            return nearGuard ? "Рядом охрана" : "На станции";
        }

        if (me.IsOutside)
        {
            var evaPosition = new Vec2(me.X, me.Y);
            var holdingCutter = HeldItemTypes(me.Inventory).Contains(ItemType.Cutter);

            var nearbyDropped = snapshot.DroppedItems.FirstOrDefault(d => d.RoomId is null && (d.Position - evaPosition).Length() < TurretInteractionRadius);
            if (nearbyDropped is not null)
                return $"[F]/[ЛКМ] подобрать: {ItemDefinitions.DisplayName(nearbyDropped.Item)}";

            var nearbyDeposit = snapshot.OreDeposits.Any(d =>
                (snapshot.OreDepositStates.FirstOrDefault(s => s.DepositId == d.Id)?.Hp ?? 0f) > 0f &&
                (d.Position - evaPosition).Length() < 3f);
            if (nearbyDeposit)
            {
                // The cutter is aimed and held now, so the hint has to say what's missing: the tool,
                // the tank in it, or nothing at all - just point and hold.
                if (!holdingCutter)
                    return "Нужен резак в руке";
                return me.CutterTank is > 0f
                    ? $"[ЛКМ] резать (баллон: {me.CutterTank:0})"
                    : "В резаке нет кислородного баллона";
            }

            var suitAir = me.SuitTank is { } tank ? $"  Баллон: {tank:0}" : "  БАЛЛОНА НЕТ";
            return me.IsEvaAttached
                ? $"[Space] оттолкнуться (курсором)  Ранец: {me.JetpackFuel:0}{suitAir}"
                : $"В свободном полёте  [WASD] ранец  Ранец: {me.JetpackFuel:0}{suitAir}";
        }

        if (HeldItemTypes(me.Inventory).Contains(ItemType.MedKit) && me.Health < 100f)
            return "[F] использовать аптечку";

        var myPosition = new Vec2(me.X, me.Y);
        var nearTurret = snapshot.Turrets.Any(t => (t.PeriscopePosition - myPosition).Length() < TurretInteractionRadius);
        var nearBallisticTurret = snapshot.Turrets.Any(t =>
            t.WeaponType == TurretWeaponType.Ballistic && (t.PeriscopePosition - myPosition).Length() < TurretInteractionRadius);

        if (me.CarryingAmmoCrate)
            return nearBallisticTurret ? "[F] зарядить орудие" : "Несёте ящик патронов к орудию";

        var nearStorage = snapshot.AmmoStorages.Any(s => (s.Position - myPosition).Length() < TurretInteractionRadius);
        if (nearStorage)
            return "[F] взять ящик патронов";

        // Ship/station floor drops only (World.Storage.cs's drag-to-floor) - EVA's own dropped items
        // are handled above, in the me.IsOutside branch, against the asteroid-field position instead.
        var nearDroppedItem = snapshot.DroppedItems.FirstOrDefault(d => d.RoomId is not null && (d.Position - myPosition).Length() < TurretInteractionRadius);
        if (nearDroppedItem is not null)
            return $"[ЛКМ] подобрать: {ItemDefinitions.DisplayName(nearDroppedItem.Item)}";

        var holding = HeldItemTypes(me.Inventory);

        var nearDamagedTurret = snapshot.Turrets.Any(t =>
            (t.PeriscopePosition - myPosition).Length() < TurretInteractionRadius &&
            (snapshot.TurretStates.FirstOrDefault(s => s.Id == t.Id)?.Damaged ?? false));
        if (nearDamagedTurret)
        {
            return holding.Contains(ItemType.Wrench) || holding.Contains(ItemType.Screwdriver)
                ? "[F] почини турель"
                : "Нужен гаечный ключ или отвёртка в руке";
        }

        if (nearTurret)
            return "[F] сесть за орудие";

        var nearHelm = (snapshot.HelmConsole.Position - myPosition).Length() < TurretInteractionRadius;
        if (nearHelm)
            return "[F] встать за штурвал";

        var nearDamagedSystem = snapshot.SystemDevices.FirstOrDefault(d =>
            (d.Position - myPosition).Length() < TurretInteractionRadius &&
            (snapshot.SystemStates.FirstOrDefault(s => s.DeviceId == d.Id)?.Damaged ?? false));
        if (nearDamagedSystem is not null)
        {
            return holding.Contains(ItemType.Wrench) || holding.Contains(ItemType.Screwdriver)
                ? "[F] почини систему"
                : "Нужен гаечный ключ или отвёртка в руке";
        }

        var nearLocker = snapshot.SuitLockers.Any(l => (l.Position - myPosition).Length() < TurretInteractionRadius);
        if (nearLocker)
            return me.WearingSuit ? "[F] снять скафандр" : "[F] надеть скафандр";

        var myRoom = snapshot.Rooms.FirstOrDefault(r => r.Contains(myPosition));
        var nearBreachedBlock = myRoom is null
            ? null
            : snapshot.WallBlocks.FirstOrDefault(b =>
                b.RoomId == myRoom.Id &&
                (snapshot.WallBlockStates.FirstOrDefault(s => s.Id == b.Id)?.Breached ?? false) &&
                (b.Position - myPosition).Length() < WelderHintReachUnits);
        if (nearBreachedBlock is not null)
        {
            // The welder is aimed and held now, just like the cutter - the hint has to say what's
            // missing: the tool, the tank in it, or nothing at all.
            if (!holding.Contains(ItemType.WeldingTool))
                return "Нужен сварочный аппарат (обе руки)";
            return me.WelderTank is > 0f
                ? $"[ЛКМ] заварить пробоину (баллон: {me.WelderTank:0})"
                : "В сварочном аппарате нет баллона";
        }

        var nearDoor = snapshot.Doors.Any(d => (d.Position - myPosition).Length() < TurretInteractionRadius);
        var nearOuterDoor = snapshot.AirlockOuterDoors.Any(d => (d.Position - myPosition).Length() < TurretInteractionRadius);

        // The commonest way to be stuck aboard: suit on, socket empty. Said at the door, where the
        // player is standing when they find out nothing happens (World.Eva.cs gates on the tank).
        if (nearOuterDoor && me.WearingSuit && me.SuitTank is null)
            return "В скафандре нет баллона — наружу не выпустит";
        // Aboard a boarded hull the same click matters more: those doors start closed, and opening
        // one lets the breach through into the compartment behind it (World.EnemyAtmosphere.cs).
        if (me.OnEnemyShip &&
            snapshot.EnemyShipDoors.Any(d => (d.Position - myPosition).Length() < TurretInteractionRadius))
            return "[ЛКМ] открыть дверь (стравит воздух)";
        if (nearDoor || nearOuterDoor)
            return "[ЛКМ] открыть/закрыть дверь";

        var roomOxygen = myRoom is null ? 100f : snapshot.RoomOxygen.FirstOrDefault(o => o.RoomId == myRoom.Id)?.Oxygen ?? 100f;
        if (roomOxygen < 100f)
            return $"Кислород в отсеке: {roomOxygen:0}";

        return string.Empty;
    }

    private static IReadOnlyCollection<ItemType> HeldItemTypes(InventoryState? inventory) =>
        inventory is null
            ? Array.Empty<ItemType>()
            : inventory.HeldMainSlotIndices.Select(i => inventory.MainSlots[i]).OfType<ItemType>().ToArray();

}
