using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceAdventure.Client.Audio;
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
        var mountOnScreen = (origin + new Vector2((float)mount.Position.X, (float)mount.Position.Y) * ShipRenderer.PixelsPerUnit)
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

    // Reused for aim while manning a turret вЂ” movement is locked server-side at that point.
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
                _sounds?.Play(GameSounds.ItemPickup, nowSeconds, volume: 0.6f);
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
                // Released over nothing - not a slot, not a socket. Over open world that means the
                // item falls to the floor at the character's own feet (the server re-checks
                // reachability itself, same trust level as an ordinary slot-to-slot move). Over any
                // part of the interface it means nothing at all: the item stays where it was, which
                // is what a release that missed a slot by a few pixels almost always meant.
                if (IsOverInterface(_designMouse))
                    return (null, null, true);

                _pendingDropItemFrom = from;
                _sounds?.Play(GameSounds.ItemDrop, nowSeconds, volume: 0.7f);
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

        // The worn suit takes a tank the same way a held cutter does, and now by the same gesture.
        // It needs its own branch because a worn item is not in the row: its icon lives in the equip
        // strip and its socket is keyed to Inventory.WornSuitSlot rather than to a row index.
        //
        // Deliberately not gated on the tank being in hand, even though clicking the socket is
        // (QueueSocketClick). Dragging a tank into a cutter was never gated either, and two gestures
        // that do the same thing should not disagree about when they are allowed.
        if (from.Kind == ItemSlotKind.Main && TankSockets.IsTank(draggedItem) &&
            HoveredWornSocketItem(inventory) is { } wornOwner &&
            TankSockets.AcceptedTank(wornOwner) == draggedItem && inventory.WornSuitTank is null)
            return new DropTarget(new SlotRef(ItemSlotKind.Equip, (int)EquipSlot.Suit), true, (from.Index, -1));

        if (HitTestItemSlot(snapshot) is not { } to || to == from)
            return null;

        var valid = IsClientReachable(me, from) && IsClientReachable(me, to);
        return new DropTarget(to, valid, null);
    }

    private bool IsClientReachable(CharacterState me, SlotRef slot) => slot.Kind switch
    {
        ItemSlotKind.Main => !me.OnEnemyShip && !me.IsOutside,
        ItemSlotKind.Rack => _openBlock.Kind == BlockKind.Rack && !me.OnStation && !me.OnEnemyShip && !me.IsOutside,
        // Suit is excluded here too, mirroring World.Storage.cs's own IsSlotReachable - it's
        // filled only by the suit-locker's timed equip/unequip action, never a plain drag.
        ItemSlotKind.Equip => (EquipSlot)slot.Index != EquipSlot.Suit && !me.OnEnemyShip && !me.IsOutside,
        ItemSlotKind.BeltBag => me.Inventory?.Equipped.GetValueOrDefault(EquipSlot.BeltBag) == ItemType.BeltBag &&
                                !me.OnEnemyShip && !me.IsOutside,
        _ => false,
    };

    // Whether the worn bag's own popup should be visible/interactive this frame - hovering its
    // icon, hovering the popup grid itself once it's open, or already mid-drag with an item that
    // came out of it (so moving the mouse away to find a drop target doesn't hide it first).
    private bool IsBeltBagPopupShown(WorldSnapshot snapshot)
    {
        if (_dragFrom is { Kind: ItemSlotKind.BeltBag })
            return true;

        var inventory = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory;
        if (inventory is null || inventory.Equipped.GetValueOrDefault(EquipSlot.BeltBag) != ItemType.BeltBag)
            return false;

        var bagIndex = Array.FindIndex(InventoryPanel.EquipSlots, s => s.Id == EquipSlot.BeltBag);
        var bagRect = InventoryPanel.GetSlotRect(bagIndex, EquipSlotsOrigin);
        if (bagRect.Contains(_designMouse))
            return true;

        for (var i = 0; i < inventory.BeltBagSlots.Count; i++)
            if (InventoryPanel.GetBeltBagSlotRect(i, bagRect).Contains(_designMouse))
                return true;

        return false;
    }

    // Which row slot's tool socket the mouse is currently close enough to reveal - hovering either
    // the slot itself or the socket band that then appears above it (InventoryPanel.GetSocketRect
    // with above: true). Both count as "hovering this slot" so the socket doesn't wink out the
    // moment the cursor reaches for it.
    // The worn item whose tank socket the cursor is over, if any. Mirrors the equip-strip loop the
    // socket click uses, so a drag and a click agree on where the socket is - and accepts the icon
    // itself as well as the socket band above it, which is what anyone dragging a tank aims at.
    private ItemType? HoveredWornSocketItem(InventoryState inventory)
    {
        for (var i = 0; i < InventoryPanel.EquipSlots.Length; i++)
        {
            var worn = inventory.Equipped.TryGetValue(InventoryPanel.EquipSlots[i].Id, out var e) ? e : null;
            if (worn is not { } wornItem || !TankSockets.HasSocket(wornItem))
                continue;
            var slotRect = InventoryPanel.GetSlotRect(i, EquipSlotsOrigin);
            if (slotRect.Contains(_designMouse) ||
                InventoryPanel.GetSocketRect(slotRect, above: true).Contains(_designMouse))
                return wornItem;
        }
        return null;
    }

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

    // Which row slot (holding anything at all) the cursor sits over right now - the tooltip's own
    // hover test, separate from HoveredToolSlotIndex above since a tooltip applies to every item,
    // not just the ones with a tank socket.
    private int? HoveredMainSlotIndex(InventoryState inventory, Vector2 rowOrigin)
    {
        for (var i = 0; i < inventory.MainSlots.Count; i++)
        {
            if (inventory.MainSlots[i] is null)
                continue;
            if (InventoryPanel.GetMainSlotRect(i, rowOrigin).Contains(_designMouse))
                return i;
        }
        return null;
    }

    // Same tooltip hover test as HoveredMainSlotIndex above, just against a shelf's slots instead
    // of the belt row - i is local to the open shelf (RackPanel.GetSlotRect's own indexing),
    // offset is where that shelf's band starts in the snapshot's flat RackSlots array.
    private int? HoveredRackSlotIndex(WorldSnapshot snapshot, int offset)
    {
        for (var i = 0; i < StorageRack.Capacity; i++)
        {
            var globalIndex = offset + i;
            if (globalIndex >= snapshot.RackSlots.Count || snapshot.RackSlots[globalIndex] is null)
                continue;
            if (RackPanel.GetSlotRect(i, RackPanelOrigin).Contains(_designMouse))
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
            ? (inventory.Equipped.TryGetValue(EquipSlot.Suit, out var worn) ? worn : null)
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

    private bool HoldingAxe() =>
        _client.LatestSnapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory is { } inventory
        && inventory.HeldMainSlotIndices.Any(i => inventory.MainSlots[i] == ItemType.Axe);

    private bool HoldingGoshaScrewdriver() =>
        _client.LatestSnapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory is { } inventory
        && inventory.HeldMainSlotIndices.Any(i => inventory.MainSlots[i] == ItemType.GoshaScrewdriver);

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
    // across one at a time to do it is busywork. Only armed while a rack is open вЂ” "across" has
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
            return null; // this shelf is full вЂ” better to do nothing than to swap with an arbitrary slot
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
        {
            for (var i = 0; i < inventory.MainSlots.Count; i++)
                if (InventoryPanel.GetMainSlotRect(i, InventoryRowOrigin(inventory.MainSlots.Count)).Contains(_designMouse))
                    return new SlotRef(ItemSlotKind.Main, i);

            // Checked before the equip icons themselves - the popup floats above the bag's own
            // slot, so a click there has to land in the bag, not fall through to whatever equip
            // slot happens to sit under it.
            if (IsBeltBagPopupShown(snapshot))
            {
                var bagIndex = Array.FindIndex(InventoryPanel.EquipSlots, s => s.Id == EquipSlot.BeltBag);
                var bagRect = InventoryPanel.GetSlotRect(bagIndex, EquipSlotsOrigin);
                for (var i = 0; i < inventory.BeltBagSlots.Count; i++)
                    if (InventoryPanel.GetBeltBagSlotRect(i, bagRect).Contains(_designMouse))
                        return new SlotRef(ItemSlotKind.BeltBag, i);
            }

            for (var i = 0; i < InventoryPanel.EquipSlots.Length; i++)
                if (InventoryPanel.GetSlotRect(i, EquipSlotsOrigin).Contains(_designMouse))
                    return new SlotRef(ItemSlotKind.Equip, (int)InventoryPanel.EquipSlots[i].Id);
        }

        if (_openBlock.Kind == BlockKind.Rack)
        {
            var offset = CurrentOpenRackOffset(snapshot);
            for (var i = 0; i < StorageRack.Capacity; i++)
                if (RackPanel.GetSlotRect(i, RackPanelOrigin).Contains(_designMouse))
                    return new SlotRef(ItemSlotKind.Rack, offset + i);
        }

        return null;
    }

    private ItemType? ItemInSlot(WorldSnapshot snapshot, SlotRef slot)
    {
        if (slot.Kind == ItemSlotKind.Rack)
            return slot.Index < snapshot.RackSlots.Count ? snapshot.RackSlots[slot.Index] : null;

        var inventory = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId)?.Inventory;
        if (inventory is null)
            return null;

        return slot.Kind switch
        {
            ItemSlotKind.Equip => inventory.Equipped.GetValueOrDefault((EquipSlot)slot.Index),
            ItemSlotKind.BeltBag => slot.Index < inventory.BeltBagSlots.Count ? inventory.BeltBagSlots[slot.Index] : null,
            _ => slot.Index < inventory.MainSlots.Count ? inventory.MainSlots[slot.Index] : null,
        };
    }

    private Rectangle GetSlotScreenRect(SlotRef slot, Vector2 rowOrigin)
    {
        if (slot.Kind == ItemSlotKind.Main)
            return InventoryPanel.GetMainSlotRect(slot.Index, rowOrigin);
        if (slot.Kind == ItemSlotKind.Rack)
            return RackPanel.GetSlotRect(slot.Index - CurrentOpenRackOffset(_client.LatestSnapshot!), RackPanelOrigin);

        var equipIndex = Array.FindIndex(InventoryPanel.EquipSlots, s => (int)s.Id == (slot.Kind == ItemSlotKind.Equip ? slot.Index : (int)EquipSlot.BeltBag));
        var equipRect = InventoryPanel.GetSlotRect(equipIndex, EquipSlotsOrigin);
        return slot.Kind == ItemSlotKind.Equip ? equipRect : InventoryPanel.GetBeltBagSlotRect(slot.Index, equipRect);
    }

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

    // 1-9 then 0 for the tenth slot - the same row of number keys Barotrauma binds its hotbar to.
    // Edge-triggered (a held key doesn't keep re-toggling), and only read where the distribution
    // block isn't open, since 1-5 already mean something else there (ReadPowerSystemSelection).
    private static readonly Keys[] InventoryHotkeys =
        { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0 };

    private int? ReadInventoryHotkeySlot(KeyboardState keyboard)
    {
        for (var i = 0; i < InventoryHotkeys.Length; i++)
        {
            if (keyboard.IsKeyDown(InventoryHotkeys[i]) && _prevGameplayKeyboard.IsKeyUp(InventoryHotkeys[i]))
                return i;
        }
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
    // a wire's line while the wiring panel is open, (5) opening/closing a block by clicking it on
    // the ship view (requires standing close), (6) clicking empty space closes whatever's open.
    // Edge-triggered so a held button doesn't spam. The helm joystick's continuous drag is handled
    // separately (UpdateHelmThrustDrag) since it isn't an edge-triggered click, and stabilize is
    // keyboard-only (S) now - HelmButtonsWidget's own doc comment - so neither rides this tuple.
    private (int ToggleHoldSlotIndex, int ToggleReactorSlotIndex, ItemType? BuyItemType, int SellSlotIndex, bool AcceptCargoQuestPressed, bool TurnInCargoQuestPressed, ShipUpgradeTrack? PurchaseUpgradeTrack, string? DoorToggleId) HandleMouseClick(MouseState mouse)
    {
        var clicked = mouse.LeftButton == ButtonState.Pressed && _prevLeftMouseButton == ButtonState.Released;
        _prevLeftMouseButton = mouse.LeftButton;
        if (!clicked)
            return (-1, -1, null, -1, false, false, null, null);

        // The dev cheat panel (Ё key) sits over everything else too, same reasoning as the pause
        // menu right below - its one button is read via a side-effect field (Game1.cs's Update)
        // rather than this tuple, since it's not part of the game's own action set.
        if (_cheatPanelOpen)
        {
            if (CheatPanel.GetSpawnEnemyButtonRect(CheatPanelOrigin).Contains(_designMouse))
                _debugSpawnEnemyClickedThisFrame = true;
            else if (CheatPanel.GetAddCreditsButtonRect(CheatPanelOrigin).Contains(_designMouse))
                _debugAddCreditsClickedThisFrame = true;
            return (-1, -1, null, -1, false, false, null, null);
        }

        // The pause menu (Game1.Update's Esc handling) sits over literally everything else, so its
        // own 4 buttons are checked before even the turret early-return below. "Главное меню" only
        // sets a flag here rather than tearing the session down on the spot - this method runs in
        // the middle of Update, well before this frame's _client.SendInput, and ReturnToMainMenu
        // nulls _client out; Update itself checks the flag right after this call returns and bails
        // out of the rest of the frame before anything downstream can touch a null _client.
        if (_pauseMenuOpen)
        {
            if (PauseMenuPanel.GetButtonRect(0, PauseMenuPanelOrigin).Contains(_designMouse))
                _pauseMenuOpen = false; // Продолжить
            else if (PauseMenuPanel.GetButtonRect(2, PauseMenuPanelOrigin).Contains(_designMouse))
                Exit(); // Закончить раунд - just flags the game loop to stop, safe to call anytime
            else if (PauseMenuPanel.GetButtonRect(3, PauseMenuPanelOrigin).Contains(_designMouse))
                _pendingReturnToMainMenu = true; // Главное меню
            // Button 1 (Настройки) has no screen behind it yet - a dim placeholder, same convention
            // as the top-bar "Управление" button before it got the ship editor.
            return (-1, -1, null, -1, false, false, null, null);
        }

        var snapshot = _client.LatestSnapshot;
        var me = snapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);

        // Behind a periscope the mouse is the gunsight and nothing else. The scene is drawn from
        // out at the gun and at half scale, so every world-space hit test below would be pointing
        // at whatever used to be under the cursor rather than what's there now.
        if (snapshot is not null && MannedTurret(snapshot) is not null)
            return (-1, -1, null, -1, false, false, null, null);

        // The 3 top-bar buttons and, while it's open, InfoPanel's own 5 tab buttons - pure client
        // state toggles, no server command involved, so they're handled here directly rather than
        // riding through this method's already-full return tuple.
        if (GetTopBarButtonRect(0).Contains(_designMouse))
        {
            _crewPanelOpen = !_crewPanelOpen;
            return (-1, -1, null, -1, false, false, null, null);
        }
        if (GetTopBarButtonRect(1).Contains(_designMouse))
        {
            _shipEditorOpen = !_shipEditorOpen;
            if (_shipEditorOpen)
            {
                _infoPanelOpen = false;
                _openBlock = ClickTarget.None;
            }
            return (-1, -1, null, -1, false, false, null, null);
        }
        if (GetTopBarButtonRect(2).Contains(_designMouse))
        {
            _infoPanelOpen = !_infoPanelOpen;
            if (_infoPanelOpen)
            {
                _openBlock = ClickTarget.None;
                _shipEditorOpen = false;
            }
            return (-1, -1, null, -1, false, false, null, null);
        }
        if (_externalCameraMode && _externalCameraFullscreenIndex is null)
        {
            // Must match Game1.cs's own cameraArea (the full design canvas, M48 follow-up) or a
            // click would hit-test against a rect smaller than what's actually drawn on screen.
            var cameraArea = new Rectangle(0, 0, DesignWidth, DesignHeight);
            if (ExternalCameraPanel.QuadrantHitTest(cameraArea, _designMouse, snapshot?.Cameras.Count ?? 0) is { } quadrant)
                _externalCameraFullscreenIndex = quadrant;
            return (-1, -1, null, -1, false, false, null, null);
        }
        if (_infoPanelOpen)
        {
            for (var i = 0; i < 5; i++)
            {
                if (!InfoPanel.GetTabRect(i, InfoPanelOrigin).Contains(_designMouse))
                    continue;
                _infoPanelTab = (InfoTab)i;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }
        if (_shipEditorOpen && snapshot is not null)
        {
            for (var i = 0; i < snapshot.Wiring.Components.Count; i++)
            {
                if (!ShipEditorPanel.GetRowRect(i, ShipEditorPanelOrigin).Contains(_designMouse))
                    continue;
                _shipEditorSelectedComponentId = snapshot.Wiring.Components[i].Id;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }
        // The crew panel's own picker row (CrewPanel.Draw) - clicking the role you're already on
        // clears it (a second click is the only "unpick" gesture, there's no separate button for
        // it), clicking a different one sets it. Both just arm a pending flag Update() reads once
        // and forwards through SendInput; the server (World.cs ApplyCommand) is the actual source
        // of truth for character.Role, same as every other "set on click" field here.
        // The own-role row on the crew panel used to be a picker. It is a readout now: the role is
        // chosen once, at the start of the campaign, and clicking here does nothing. The clicks are
        // still swallowed rather than falling through to the world underneath, so a stray click on
        // the panel cannot walk the character somewhere.
        if (_crewPanelOpen)
        {
            for (var i = 0; i < CrewPanel.OptionCount; i++)
            {
                if (CrewPanel.GetOwnRoleIconRect(i, CrewPanelOrigin).Contains(_designMouse))
                    return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // Дурак переводной (World.CardGame.cs) - own-hand card clicks and the Взять/Бито buttons.
        // No _openBlock gating: CardGamePanel.Draw already no-ops unless the local player is one
        // of the 2 participants, so the same condition gates the clicks here.
        if (snapshot?.CardGame is { } cardGame && (cardGame.Player1Id == _client.PlayerId || cardGame.Player2Id == _client.PlayerId))
        {
            var myHand = cardGame.Player1Id == _client.PlayerId ? cardGame.Player1Hand : cardGame.Player2Hand;
            for (var i = 0; i < myHand.Count; i++)
            {
                if (!CardGamePanel.GetOwnHandCardRect(i, myHand.Count, CardGamePanelOrigin).Contains(_designMouse))
                    continue;
                _pendingPlayCard = myHand[i];
                return (-1, -1, null, -1, false, false, null, null);
            }
            if (CardGamePanel.GetTakeButtonRect(CardGamePanelOrigin).Contains(_designMouse))
            {
                _pendingCardGameTake = true;
                return (-1, -1, null, -1, false, false, null, null);
            }
            if (CardGamePanel.GetEndRoundButtonRect(CardGamePanelOrigin).Contains(_designMouse))
            {
                _pendingCardGameEndRound = true;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // The CardTable's game-choice step (World.CardTable.cs) - shown only once the local player
        // has clicked the table open (direct user request, "чтобы в стол можно было зайти"), and
        // only to the crew actually seated there before either game has started. Alone, only
        // Фронты (against a bot - direct user request, "можно играть в хойку в одиночку") is on
        // offer; CardTableChoicePanel.GetChoiceKind knows the exact same seated-count-dependent
        // button layout Draw renders, so the two can't disagree about which button is which.
        if (_openBlock.Kind == BlockKind.CardTable &&
            snapshot?.CardTableChoiceSeatedIds is { Count: 1 or 2 } seatedChoice && seatedChoice.Contains(_client.PlayerId))
        {
            for (var i = 0; i < 2; i++)
            {
                if (CardTableChoicePanel.GetChoiceKind(seatedChoice.Count, i) is not { } kind ||
                    !CardTableChoicePanel.GetChoiceButtonRect(i, CardTableChoicePanelOrigin).Contains(_designMouse))
                    continue;
                _pendingCardTableChoice = kind;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // Фронты (World.FrontsGame.cs) - per-front +/- allocation and "Провести бой". Same
        // participant gate as the Дурак block above.
        if (snapshot?.FrontsGame is { } frontsGame && (frontsGame.PlayerAId == _client.PlayerId || frontsGame.PlayerBId == _client.PlayerId))
        {
            var isPlayerA = frontsGame.PlayerAId == _client.PlayerId;
            var myAllocation = isPlayerA ? frontsGame.AllocationA : frontsGame.AllocationB;
            var myUsed = 0;
            foreach (var a in myAllocation)
                myUsed += a;
            var remaining = frontsGame.ArmyPool - myUsed;

            if (!frontsGame.Finished)
            {
                for (var i = 0; i < myAllocation.Count; i++)
                {
                    if (frontsGame.Captured[i])
                        continue;
                    if (myAllocation[i] > 0 && FrontsGamePanel.GetMinusButtonRect(i, FrontsGamePanelOrigin).Contains(_designMouse))
                    {
                        _pendingFrontsAllocationIndex = i;
                        _pendingFrontsAllocationAmount = myAllocation[i] - 1;
                        return (-1, -1, null, -1, false, false, null, null);
                    }
                    if (remaining > 0 && FrontsGamePanel.GetPlusButtonRect(i, FrontsGamePanelOrigin).Contains(_designMouse))
                    {
                        _pendingFrontsAllocationIndex = i;
                        _pendingFrontsAllocationAmount = myAllocation[i] + 1;
                        return (-1, -1, null, -1, false, false, null, null);
                    }
                }
                if (FrontsGamePanel.GetResolveButtonRect(FrontsGamePanelOrigin).Contains(_designMouse))
                {
                    _pendingFrontsResolve = true;
                    return (-1, -1, null, -1, false, false, null, null);
                }
            }
        }

        var slotCount = me?.Inventory?.MainSlots.Count ?? 0;
        for (var i = 0; i < slotCount; i++)
        {
            if (InventoryPanel.GetHoldStripRect(i, InventoryRowOrigin(slotCount)).Contains(_designMouse))
                return (i, -1, null, -1, false, false, null, null);
        }

        if (snapshot is null || me is null)
            return (-1, -1, null, -1, false, false, null, null);

        // Window 2 of the helm redesign (M47 follow-up) - dock/RCS-mode/cameras, at wherever the
        // widget has been dragged to rather than a fixed HelmPanelOrigin. Stabilize is keyboard-
        // only now (S) - it wasn't one of the three buttons the widget was asked to carry.
        if (me.IsAtHelm && (snapshot.CanDock || snapshot.Voyage.DockedPointId is not null) &&
            HelmButtonsWidget.GetDockButtonRect(_helmWidgetPosition).Contains(_designMouse))
        {
            _pendingDock = true;
            return (-1, -1, null, -1, false, false, null, null);
        }

        if (me.IsAtHelm && HelmButtonsWidget.GetControlModeButtonRect(_helmWidgetPosition).Contains(_designMouse))
        {
            _pendingToggleControlMode = true;
            return (-1, -1, null, -1, false, false, null, null);
        }

        // M57 - the 3 tab buttons switch _helmTab (purely client-local, HelmTab.cs's own doc
        // comment) - available on every tab, not gated to Captain, so anyone can switch away.
        if (me.IsAtHelm)
        {
            foreach (var tab in new[] { HelmTab.Captain, HelmTab.Scientist, HelmTab.Engineer })
            {
                if (!HelmTabBar.GetTabRect(tab, HelmTabBarOrigin).Contains(_designMouse))
                    continue;
                _helmTab = tab;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // The captain tab's own ×1/×10/×100/×1000 selector (M57) - same fixed offset above
        // _helmWidgetPosition the Draw call above uses.
        if (me.IsAtHelm && _helmTab == HelmTab.Captain)
        {
            var accelOrigin = _helmWidgetPosition + new Vector2(0, -46);
            for (var levelIndex = 0; levelIndex < 4; levelIndex++)
            {
                if (!TimeAccelerationWidget.GetLevelButtonRect(levelIndex, accelOrigin).Contains(_designMouse))
                    continue;
                _pendingTimeAccelerationLevel = TimeAccelerationWidget.LevelAt(levelIndex);
                return (-1, -1, null, -1, false, false, null, null);
            }

            if (TimeAccelerationWidget.GetFlipButtonRect(accelOrigin).Contains(_designMouse))
            {
                _pendingFlipHeading = true;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // The Engineer tab's own device list (M57) - clicking a row focuses it (starting/resuming
        // its remote repair timer, World.SystemRepair.cs), clicking the already-focused row again
        // clears focus instead of leaving it stuck forever pointed at one device.
        if (snapshot is not null && me.IsAtHelm && _helmTab == HelmTab.Engineer)
        {
            var rows = EngineerDevicePanel.BuildRows(snapshot);
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                if (!EngineerDevicePanel.GetRowRect(rowIndex, EngineerDevicePanelOrigin).Contains(_designMouse))
                    continue;
                var deviceId = rows[rowIndex].DeviceId;
                _engineerFocusDeviceId = _engineerFocusDeviceId == deviceId ? null : deviceId;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        if (me.IsAtHelm && (snapshot.CanLandNow || snapshot.Voyage.LandedBodyId is not null) &&
            HelmButtonsWidget.GetLandingButtonRect(_helmWidgetPosition).Contains(_designMouse))
        {
            _pendingToggleLanding = true;
            return (-1, -1, null, -1, false, false, null, null);
        }

        // External cameras (M46, still on the helm's own button widget) - gated on the Secondary
        // power channel the same way ComputeShipPowerMood already reads it for the ship's own
        // lamps; a click while unpowered is a no-op rather than opening onto a view that would
        // just read as broken.
        if (me.IsAtHelm && snapshot.Cameras.Count > 0 && ComputeShipPowerMood(snapshot).PowerFraction > 0.01f &&
            HelmButtonsWidget.GetCamerasButtonRect(_helmWidgetPosition).Contains(_designMouse))
        {
            _externalCameraMode = !_externalCameraMode;
            _externalCameraFullscreenIndex = null;
            if (_externalCameraMode)
            {
                _openBlock = ClickTarget.None;
                _infoPanelOpen = false;
                _shipEditorOpen = false;
            }
            return (-1, -1, null, -1, false, false, null, null);
        }

        // The scanner console's own toggle switch (M48 follow-up - "радар приводится в действие
        // переключением рычажка") - console-operator only, replaces the old separate "Скан" button:
        // clicking either half both selects that mode (sent every frame as
        // ClientCommand.RequestedScannerMode below) and fires the pulse in it, a no-op server-side
        // while still on cooldown (World.Scanner.cs) same as the old button already was.
        if (_openBlock.Kind == BlockKind.Navigation)
        {
            if (ScannerModeWidget.GetDirectionalRowRect(_scannerWidgetPosition).Contains(_designMouse))
            {
                _requestedScannerMode = ScannerMode.Directional;
                _pendingScannerPing = true;
                return (-1, -1, null, -1, false, false, null, null);
            }
            if (ScannerModeWidget.GetCircularRowRect(_scannerWidgetPosition).Contains(_designMouse))
            {
                _requestedScannerMode = ScannerMode.Circular;
                _pendingScannerPing = true;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        if (_openBlock.Kind == BlockKind.Reactor)
        {
            for (var i = 0; i < snapshot.Reactor.RodCharges.Count; i++)
            {
                if (ReactorPanel.GetSlotRect(i, PowerPanelOrigin).Contains(_designMouse))
                    return (-1, i, null, -1, false, false, null, null);
            }
        }

        if (_openBlock.Kind == BlockKind.Jukebox)
        {
            if (JukeboxPanel.GetCheckboxRect(PowerPanelOrigin).Contains(_designMouse))
            {
                _pendingJukeboxToggle = true;
                return (-1, -1, null, -1, false, false, null, null);
            }
            if (JukeboxPanel.GetTrackPrevRect(PowerPanelOrigin).Contains(_designMouse))
            {
                _pendingJukeboxPrevTrack = true;
                return (-1, -1, null, -1, false, false, null, null);
            }
            if (JukeboxPanel.GetTrackNextRect(PowerPanelOrigin).Contains(_designMouse))
            {
                _pendingJukeboxNextTrack = true;
                return (-1, -1, null, -1, false, false, null, null);
            }
            if (JukeboxPanel.GetVolumeDownRect(PowerPanelOrigin).Contains(_designMouse))
            {
                _pendingJukeboxVolumeDown = true;
                return (-1, -1, null, -1, false, false, null, null);
            }
            if (JukeboxPanel.GetVolumeUpRect(PowerPanelOrigin).Contains(_designMouse))
            {
                _pendingJukeboxVolumeUp = true;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        if (_galacticMapOpen)
        {
            var galacticMapOrigin = GalacticMapPanel.ComputeOrigin(GalaxyMapPanelOrigin, snapshot.StarSystems, _galacticMapZoom, _galacticMapPanOffset);
            foreach (var system in snapshot.StarSystems)
            {
                if (!GalacticMapPanel.GetNodeRect(system, galacticMapOrigin, _galacticMapZoom).Contains(_designMouse))
                    continue;
                _pendingWarpToSystemId = system.Id;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // Content-каталог отсеков - StationBuildPanel's own category tabs + module row. Checked
        // whenever the panel is actually ON SCREEN (Game1.cs's Draw uses this exact same condition),
        // not only while `_openBlock.Kind == BlockKind.Station` right below - a module already
        // picked stays selectable/reselectable even after the player has walked off the station and
        // back aboard their own ship to go point at a spot (the dialogue itself may have closed by
        // then, but the panel - and the choice it represents - hasn't).
        var buildPanelShowing = _placingRoomCatalogId is not null ||
            (snapshot.Station.Npcs.FirstOrDefault(n => n.Id == _talkingToNpcId)?.Kind == NpcKind.Shipwright);
        if (buildPanelShowing)
        {
            for (var i = 0; i < StationBuildPanel.Categories.Length; i++)
            {
                if (!StationBuildPanel.GetCategoryTabRect(i, StationBuildPanelOrigin).Contains(_designMouse))
                    continue;
                _buildPanelCategory = StationBuildPanel.Categories[i].Category;
                return (-1, -1, null, -1, false, false, null, null);
            }

            var buildEntries = StationBuildPanel.EntriesInCategory(_buildPanelCategory);
            for (var i = 0; i < buildEntries.Count; i++)
            {
                if (!StationBuildPanel.GetModuleRect(i, StationBuildPanelOrigin).Contains(_designMouse))
                    continue;
                // Picking a module no longer buys instantly (M60's own one-click purchase) - it
                // ENTERS PLACEMENT MODE, confirmed by a later click on the ship's own interior (the
                // world-click section further down this same method).
                _placingRoomCatalogId = buildEntries[i].Id;
                return (-1, -1, null, -1, false, false, null, null);
            }

            // A click that landed inside the panel's own footprint but missed every button above
            // (padding, gaps between tabs/modules) has to be swallowed here too - otherwise it falls
            // through to the world hit-tests below (this whole block runs BEFORE the world-vs-panel
            // "everything above is a panel's own controls" swallow check) and could confirm a
            // placement or toggle a block right underneath the panel by accident.
            if (new Rectangle((int)StationBuildPanelOrigin.X, (int)StationBuildPanelOrigin.Y, StationBuildPanel.PanelWidth, StationBuildPanel.PanelHeight).Contains(_designMouse))
                return (-1, -1, null, -1, false, false, null, null);
        }

        if (_openBlock.Kind == BlockKind.Station)
        {
            var talkingToKind = snapshot.Station.Npcs.FirstOrDefault(n => n.Id == _talkingToNpcId)?.Kind;

            if (talkingToKind == NpcKind.Trader)
            {
                for (var i = 0; i < TradeCatalog.Goods.Count; i++)
                {
                    if (StationPanel.GetGoodRect(i, StationPanelOrigin).Contains(_designMouse))
                        return (-1, -1, TradeCatalog.Goods[i].Item, -1, false, false, null, null);
                }

                for (var i = 0; i < slotCount; i++)
                {
                    if (StationPanel.GetSellRect(i, StationPanelOrigin).Contains(_designMouse))
                        return (-1, -1, null, i, false, false, null, null);
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
                        return (-1, -1, null, -1, true, false, null, null);
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
                        return (-1, -1, null, -1, false, true, null, null);

                    // Same button, opposite offer, when the job can't be finished here (Station
                    // Panel's own tuple is already at its practical limit, so this rides as a
                    // field like the other Administrator/Recruiter actions above).
                    _pendingAbandonQuest = true;
                    return (-1, -1, null, -1, false, false, null, null);
                }
            }

            if (talkingToKind == NpcKind.Mechanic)
            {
                for (var i = 0; i < ShipUpgradeCatalog.Tracks.Count; i++)
                {
                    if (StationPanel.GetUpgradeRect(i, StationPanelOrigin).Contains(_designMouse))
                        return (-1, -1, null, -1, false, false, ShipUpgradeCatalog.Tracks[i].Track, null);
                }
            }

            if (talkingToKind == NpcKind.Shipwright)
            {
                for (var i = 0; i < StationPanel.PurchasableShipKinds.Length; i++)
                {
                    if (!StationPanel.GetShipRect(i, StationPanelOrigin).Contains(_designMouse))
                        continue;
                    _pendingShipPurchase = StationPanel.PurchasableShipKinds[i];
                    return (-1, -1, null, -1, false, false, null, null);
                }

                // M61 - "Снести <последний построенный>" button.
                if (snapshot is not null && StationPanel.LastBuiltRoomId(snapshot.Rooms) is { } lastRoomId &&
                    StationPanel.GetDemolishLastRoomRect(StationPanelOrigin).Contains(_designMouse))
                {
                    _pendingDemolishRoomId = lastRoomId;
                    return (-1, -1, null, -1, false, false, null, null);
                }
            }

            if (talkingToKind == NpcKind.Recruiter)
            {
                for (var i = 0; i < snapshot.RecruitCandidates.Count; i++)
                {
                    if (!StationPanel.GetCandidateRect(i, StationPanelOrigin).Contains(_designMouse))
                        continue;
                    _pendingHireCandidateId = snapshot.RecruitCandidates[i].Id;
                    return (-1, -1, null, -1, false, false, null, null);
                }
            }
        }

        // Physically standing on the station (game_design.md section 10 - walk up and click an
        // NPC in their own room). Same camera and coordinates as the ship's own interior now, but
        // none of the ship-block clicks below are reachable from over here anyway.
        if (me.OnStation)
        {
            var stationOrigin = ComputeCamera(snapshot, me).Origin;
            foreach (var npc in snapshot.Station.Npcs)
            {
                if (npc.Kind is NpcKind.Security or NpcKind.Scientist)
                    continue; // nothing to discuss with the guard, or with a Research-flavor
                              // Scientist NPC (decorative only - no service like every other kind
                              // has, so falling through to StationPanel's Trader default would be
                              // wrong rather than merely unhelpful)
                if (!StationRenderer.GetNpcRect(npc, stationOrigin).Contains(_designMouse))
                    continue;
                _talkingToNpcId = _talkingToNpcId == npc.Id ? null : npc.Id;
                _openBlock = _talkingToNpcId is null ? ClickTarget.None : ClickTarget.Station;
                return (-1, -1, null, -1, false, false, null, null);
            }

            // Stealing a crate (World.StationCrime.cs, humble-soaring-cat.md) - the same [E] action,
            // now also a click on the crate itself, same size/rect StationRenderer.DrawCrate draws.
            foreach (var crate in snapshot.Station.Crates)
            {
                if (snapshot.Station.CrateStates.FirstOrDefault(s => s.CrateId == crate.Id)?.Looted ?? false)
                    continue;
                if ((crate.Position - new Vec2(me.X, me.Y)).Length() >= TurretInteractionRadius ||
                    !ShipRenderer.GetBlockRect(crate.Position, 20, stationOrigin).Contains(_designMouse))
                    continue;
                _pendingStealCrateId = crate.Id;
                return (-1, -1, null, -1, false, false, null, null);
            }

            // Content-каталог отсеков - while the Shipwright's own whole-ship overview is showing
            // (ShipBuildOverviewActive), a click that missed every NPC above is aimed at the now-
            // visible hull itself (ComputeCamera already re-anchored/zoomed the whole scene to fit
            // it), not "empty station floor, close the dialogue" - fall through to the world-click
            // section further down instead of closing it here.
            if (!ShipBuildOverviewActive(snapshot))
            {
                _openBlock = ClickTarget.None;
                _talkingToNpcId = null;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // Everything above this point is the open panel's own controls - slots, pins, buttons.
        // Everything below is the world underneath it. Since panels open centred they now sit right
        // on top of the ship interior, so a click inside one that hit none of its controls would
        // otherwise fall through and toggle whatever block happens to be beneath it, closing the
        // panel. It has to be swallowed here rather than at the end of the method: by then the world
        // hit tests have already run and returned.
        if (CurrentPanelHousing() is { } openPanelBounds && openPanelBounds.Contains(_designMouse))
            return (-1, -1, null, -1, false, false, null, null);

        var myPosition = new Vec2(me.X, me.Y);
        bool NearEnough(Vec2 blockPosition) => (blockPosition - myPosition).Length() < TurretInteractionRadius;
        var origin = ComputeCamera(snapshot, me).Origin;
        // humble-soaring-cat.md - every RepairDeviceId candidate below needs the same "damaged AND
        // holding the right tool" gate World.Interact.cs's own E-key branches already use.
        bool HoldingRepairTool() =>
            HeldItemTypes(me.Inventory).Contains(ItemType.Wrench) || HeldItemTypes(me.Inventory).Contains(ItemType.Screwdriver);

        // Content-каталог отсеков - a module is selected, so this click (whether the player is
        // physically aboard their own ship, or still standing on the station with the Shipwright's
        // whole-ship overview showing - ShipBuildOverviewActive's own fallthrough above) is aimed at
        // the placement overlay Game1.cs's own Draw is showing right now. Takes priority over every
        // other world click below, the same "modal until confirmed or cancelled" shape the wire-lay/
        // tank-drag flows already use elsewhere in this method.
        if (_placingRoomCatalogId is { } placingCatalogId && RoomCatalog.Find(placingCatalogId) is { } placingEntry)
        {
            var mouseLocal = ScreenToShipLocal(new Vector2(_designMouse.X, _designMouse.Y), origin, SceneZoom(snapshot));
            var candidates = RoomPlacementPreview.FindCandidates(snapshot, placingEntry);
            if (RoomPlacementPreview.NearestTo(candidates, mouseLocal) is { } nearest)
                _pendingBuildRoom = new BuildRoomRequest(placingCatalogId, nearest.X, nearest.Y);
            _placingRoomCatalogId = null;
            return (-1, -1, null, -1, false, false, null, null);
        }

        // Still physically on the station (no module selected right now) - every check below this
        // point assumes myPosition/origin are the character's own SHIP-local ones, which they are
        // not while OnStation (Character.X/Y mean something else there - World.cs's own doc comment
        // on CharacterState). Nothing past here is reachable from the overview's fallthrough on
        // purpose; swallow the click instead of letting it misfire against the wrong coordinate frame.
        if (me.OnStation)
            return (-1, -1, null, -1, false, false, null, null);

        // Screwdriver "open the panel" view (World.Wiring.cs's component graph, ConnectionsPanel) -
        // a second click on the same component closes it again, same as every other block below.
        ClickTarget ToggleConnections(string componentId) =>
            _openBlock.Kind == BlockKind.Connections && _openBlock.TargetComponentId == componentId
                ? ClickTarget.None
                : ClickTarget.ForConnections(componentId);

        // The reactor's 3 physical levers - checked before the reactor's own "open the panel" click
        // below so they don't get shadowed by it (same ordering convention as the fuel-rod slots
        // while the panel is already open, just above).
        if (NearEnough(snapshot.ReactorBlock.Position))
        {
            for (var i = 0; i < 3; i++)
            {
                if (!ShipRenderer.GetReactorLeverRect(i, snapshot.ReactorBlock, origin).Contains(_designMouse))
                    continue;
                switch (i)
                {
                    case 0: _pendingToggleLights = true; break;
                    case 1: _pendingToggleReactorEmergency = true; break;
                    case 2: _pendingToggleDoorsLocked = true; break;
                }
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // Content-каталог отсеков/Ship Editor - a catalog-built reactor's own console can be much
        // bigger than the hand-authored default; ReactorRectIfNear (Game1.Interactables.cs) already
        // accounts for that, same rect hover uses.
        if (ReactorRectIfNear(snapshot.ReactorBlock, myPosition, origin) is { } reactorClickRect && reactorClickRect.Contains(_designMouse))
        {
            // humble-soaring-cat.md - a damaged block repairs on click instead of opening its
            // (otherwise unaffected) panel, same priority order World.Interact.cs's own repair
            // branches already use.
            if (HoldingRepairTool() && (snapshot.BlockStates?.FirstOrDefault(s => s.DeviceId == snapshot.ReactorBlock.Id)?.Damaged ?? false))
            {
                _pendingRepairDeviceId = snapshot.ReactorBlock.Id;
                return (-1, -1, null, -1, false, false, null, null);
            }
            _openBlock = _openBlock.Kind == BlockKind.Reactor ? ClickTarget.None : ClickTarget.Reactor;
            return (-1, -1, null, -1, false, false, null, null);
        }

        if (snapshot.Jukebox is { } jukebox && BlockRectIfNear(jukebox.Block.Position, myPosition, ShipRenderer.MediumBlockSize, origin) is { } jukeboxRect && jukeboxRect.Contains(_designMouse))
        {
            _openBlock = _openBlock.Kind == BlockKind.Jukebox ? ClickTarget.None : ClickTarget.Jukebox;
            return (-1, -1, null, -1, false, false, null, null);
        }

        // The terminal has no panel of its own - one click is the whole "gesture" (direct user
        // request), so this just fires the toggle straight away instead of opening _openBlock.
        if (snapshot.Terminal is { } terminal && BlockRectIfNear(terminal.Block.Position, myPosition, ShipRenderer.MediumBlockSize, origin) is { } terminalRect && terminalRect.Contains(_designMouse))
        {
            _pendingTerminalToggle = true;
            return (-1, -1, null, -1, false, false, null, null);
        }

        if (BlockRectIfNear(snapshot.DistributionBlock.Position, myPosition, ShipRenderer.MediumBlockSize, origin) is { } distributionRect && distributionRect.Contains(_designMouse))
        {
            if (HoldingRepairTool() && (snapshot.BlockStates?.FirstOrDefault(s => s.DeviceId == snapshot.DistributionBlock.Id)?.Damaged ?? false))
            {
                _pendingRepairDeviceId = snapshot.DistributionBlock.Id;
                return (-1, -1, null, -1, false, false, null, null);
            }
            if (HoldingScrewdriver() && snapshot.Wiring.Components.FirstOrDefault(c => c.Kind == ComponentKind.Distribution) is { } distribution)
                _openBlock = ToggleConnections(distribution.Id);
            else
                _openBlock = _openBlock.Kind == BlockKind.Distribution ? ClickTarget.None : ClickTarget.Distribution;
            return (-1, -1, null, -1, false, false, null, null);
        }

        if (BlockRectIfNear(snapshot.BatteryBlock.Position, myPosition, ShipRenderer.MediumBlockSize, origin) is { } batteryRect && batteryRect.Contains(_designMouse))
        {
            if (HoldingRepairTool() && (snapshot.BlockStates?.FirstOrDefault(s => s.DeviceId == snapshot.BatteryBlock.Id)?.Damaged ?? false))
            {
                _pendingRepairDeviceId = snapshot.BatteryBlock.Id;
                return (-1, -1, null, -1, false, false, null, null);
            }
            _openBlock = _openBlock.Kind == BlockKind.Battery ? ClickTarget.None : ClickTarget.Battery;
            return (-1, -1, null, -1, false, false, null, null);
        }

        // Helm/navigation console repair (RepairableBlockKinds.Helm/.Navigation) - neither console
        // has a click-to-open panel of its own (both are entered with [E] instead), so this is the
        // only click behavior either one gets: repair when broken and holding the right tool.
        if (BlockRectIfNear(snapshot.HelmConsole.Position, myPosition, ShipRenderer.MediumBlockSize, origin) is { } helmRect && helmRect.Contains(_designMouse) &&
            HoldingRepairTool() && (snapshot.BlockStates?.FirstOrDefault(s => s.DeviceId == snapshot.HelmConsole.Id)?.Damaged ?? false))
        {
            _pendingRepairDeviceId = snapshot.HelmConsole.Id;
            return (-1, -1, null, -1, false, false, null, null);
        }
        if (BlockRectIfNear(snapshot.NavigationConsole.Position, myPosition, ShipRenderer.MediumBlockSize, origin) is { } navRect && navRect.Contains(_designMouse) &&
            HoldingRepairTool() && (snapshot.BlockStates?.FirstOrDefault(s => s.DeviceId == snapshot.NavigationConsole.Id)?.Damaged ?? false))
        {
            _pendingRepairDeviceId = snapshot.NavigationConsole.Id;
            return (-1, -1, null, -1, false, false, null, null);
        }

        // Turret (World.Interact.cs branches 6+8 - reload/repair/man), one click covers all three
        // the same way [E] already does, resolved server-side by state (World.ClickInteract.cs).
        foreach (var turret in snapshot.Turrets)
        {
            if (BlockRectIfNear(turret.PeriscopePosition, myPosition, ShipRenderer.MediumBlockSize, origin) is not { } rect || !rect.Contains(_designMouse))
                continue;
            _pendingTurretInteractId = turret.Id;
            return (-1, -1, null, -1, false, false, null, null);
        }

        // Ammo storage (World.Interact.cs branch 7 - take a crate).
        foreach (var storage in snapshot.AmmoStorages)
        {
            if (BlockRectIfNear(storage.Position, myPosition, ShipRenderer.NormalBlockSize, origin) is not { } rect || !rect.Contains(_designMouse))
                continue;
            _pendingAmmoStorageInteractId = storage.Id;
            return (-1, -1, null, -1, false, false, null, null);
        }

        // The CardTable (World.CardTable.cs) - direct user request ("сделай чтобы в стол можно
        // было зайти"): clicking it toggles it open/closed exactly like every other console above,
        // rather than the choice panel appearing automatically just from standing near it. A game
        // already in progress (CardGamePanel/FrontsGamePanel) is unaffected - only the pre-game
        // choice step below is gated on this.
        if (BlockRectIfNear(snapshot.CardTable.Position, myPosition, ShipRenderer.MediumBlockSize, origin) is { } cardTableRect && cardTableRect.Contains(_designMouse))
        {
            _openBlock = _openBlock.Kind == BlockKind.CardTable ? ClickTarget.None : ClickTarget.CardTable;
            return (-1, -1, null, -1, false, false, null, null);
        }

        // No mouse interaction with the scanner console any more (M47) - entered with E near it,
        // closed only with Esc (Game1.cs's own interactPressed check, and the escape-handling block
        // in Update()), same asymmetric in/out as HelmConsole's own E-toggle would give it if that
        // toggled both ways, but deliberately doesn't here.

        foreach (var rack in snapshot.StorageRacks)
        {
            if (BlockRectIfNear(rack.Position, myPosition, ShipRenderer.MediumBlockSize, origin) is not { } rect || !rect.Contains(_designMouse))
                continue;
            _openBlock = _openBlock.Kind == BlockKind.Rack && _openBlock.TargetComponentId == rack.Id
                ? ClickTarget.None
                : ClickTarget.ForRack(rack.Id);
            return (-1, -1, null, -1, false, false, null, null);
        }

        // Suit locker (World.Interact.cs branch 16 - equip/unequip) - a click on the locker now
        // performs the actual take/put-back directly (humble-soaring-cat.md's "Полный переход на
        // клик как в Baro"), the same instant action [E] already did; no more read-only view step.
        foreach (var locker in snapshot.SuitLockers)
        {
            if (BlockRectIfNear(locker.Position, myPosition, ShipRenderer.NormalBlockSize, origin) is not { } rect || !rect.Contains(_designMouse))
                continue;
            _pendingSuitLockerInteractId = locker.Id;
            return (-1, -1, null, -1, false, false, null, null);
        }

        foreach (var device in snapshot.SystemDevices)
        {
            if (SystemDeviceRectIfNear(device, myPosition, origin) is { } rect && rect.Contains(_designMouse))
            {
                // Gosha's screwdriver doesn't open anything - a click just breaks the device
                // outright (World.Wiring.cs's HandleSabotageDevice), ahead of the regular
                // screwdriver's read-the-wiring behavior below.
                if (HoldingGoshaScrewdriver())
                {
                    _pendingSabotageDeviceId = device.Id;
                    return (-1, -1, null, -1, false, false, null, null);
                }

                // A damaged device repairs on click instead of opening its panel, same priority as
                // the reactor/distribution/battery blocks above.
                if (HoldingRepairTool() && (snapshot.SystemStates.FirstOrDefault(s => s.DeviceId == device.Id)?.Damaged ?? false))
                {
                    _pendingRepairDeviceId = device.Id;
                    return (-1, -1, null, -1, false, false, null, null);
                }

                _openBlock = HoldingScrewdriver()
                    ? ToggleConnections(device.Id)
                    : _openBlock.Kind == BlockKind.System && _openBlock.System == device.System
                        ? ClickTarget.None
                        : ClickTarget.ForSystem(device.System);
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // Hull cameras (M48) - not a ShipSystemDevice (WireGraphFactory's own comment explains why),
        // but the same click-to-repair as one, resolved by the same RepairDeviceId (World.ClickInteract.cs
        // finds it among Ship.Cameras by id).
        foreach (var camera in snapshot.Cameras)
        {
            if (BlockRectIfNear(camera.InteriorPosition, myPosition, ShipRenderer.NormalBlockSize, origin) is not { } rect || !rect.Contains(_designMouse))
                continue;
            if (HoldingRepairTool() && (snapshot.SystemStates.FirstOrDefault(s => s.DeviceId == camera.Id)?.Damaged ?? false))
            {
                _pendingRepairDeviceId = camera.Id;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // Cosmoteer-style marching engines - the Control tile's own seized-throttle state repairs
        // the same way, one click on the tile itself.
        foreach (var engine in snapshot.EngineStates ?? Array.Empty<EngineState>())
        {
            if (EngineControlRectIfNear(engine, myPosition, origin) is not { } rect || !rect.Contains(_designMouse))
                continue;
            if (HoldingRepairTool() && engine.ControlBroken)
            {
                _pendingRepairDeviceId = engine.Id;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // Junction boxes ("С‰РёС‚РєРё") repair on click the same way every other device above does,
        // regardless of which tool is held - only their screwdriver-only "open the wiring view"
        // behavior below still needs one specifically.
        foreach (var junctionForRepair in snapshot.Wiring.Components.Where(c => c.Kind == ComponentKind.Junction))
        {
            if (BlockRectIfNear(junctionForRepair.Position, myPosition, ShipRenderer.NormalBlockSize, origin) is not { } rect || !rect.Contains(_designMouse))
                continue;
            if (HoldingRepairTool() && (snapshot.JunctionStates.FirstOrDefault(s => s.DeviceId == junctionForRepair.Id)?.Damaged ?? false))
            {
                _pendingRepairDeviceId = junctionForRepair.Id;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // Junction boxes ("С‰РёС‚РєРё") have no function of their own to click for - only screwdriver
        // opens anything here, unlike Distribution/SystemDevice above which fall back to their
        // normal panel otherwise.
        if (HoldingScrewdriver())
        {
            foreach (var junction in snapshot.Wiring.Components.Where(c => c.Kind == ComponentKind.Junction))
            {
                if (BlockRectIfNear(junction.Position, myPosition, ShipRenderer.NormalBlockSize, origin) is not { } rect || !rect.Contains(_designMouse))
                    continue;
                _openBlock = ToggleConnections(junction.Id);
                return (-1, -1, null, -1, false, false, null, null);
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
                return (-1, -1, null, -1, false, false, null, null);
            }

            for (var i = 0; i < InventoryPanel.EquipSlots.Length; i++)
            {
                var worn = tankInventory.Equipped.TryGetValue(InventoryPanel.EquipSlots[i].Id, out var e) ? e : null;
                if (worn is not { } wornItem || !TankSockets.HasSocket(wornItem))
                    continue;
                if (!InventoryPanel.GetSocketRect(InventoryPanel.GetSlotRect(i, EquipSlotsOrigin), above: true).Contains(_designMouse))
                    continue;
                QueueSocketClick(tankInventory, -1); // Inventory.WornSuitSlot
                return (-1, -1, null, -1, false, false, null, null);
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
                var owner = snapshot.Wiring.Components.FirstOrDefault(c => c.Id == pin.ComponentId);
                if (owner is null || !NearEnough(owner.Position))
                    continue;
                _pendingPinInteract = pin;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }
        else
        {
            foreach (var mount in snapshot.Wiring.ComponentMounts)
            {
                if (!NearEnough(mount.Position) || !ComponentRenderer.GetMountBodyRect(mount, origin).Contains(_designMouse))
                    continue;

                // Screwdriver on an occupied mount opens Connections locally instead of sending the
                // interact command - it's a pure client-side view, no server round-trip needed, and
                // it must not also fire the wrench-only uninstall on the server.
                var installedId = snapshot.Wiring.ComponentMountStates.FirstOrDefault(s => s.MountId == mount.Id)?.InstalledComponentId;
                if (HoldingScrewdriver() && installedId is not null)
                    _openBlock = ToggleConnections(installedId);
                else
                    _pendingComponentMountInteractId = mount.Id;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // Dropped items (World.Storage.cs's drag-to-floor, World.Mining.cs's ore chunks): ship and
        // station floors share this scene's ordinary origin, same as doors/mounts above. EVA-space
        // ones need the same world->local fold FieldRenderer's own WorldToScreen closure uses, since
        // they live in the asteroid field, not this ship-local frame.
        foreach (var dropped in snapshot.DroppedItems.Where(d => d.RoomId is not null))
        {
            if (DroppedItemRectIfNear(dropped, myPosition, origin) is not { } rect || !rect.Contains(_designMouse))
                continue;
            _pendingPickupDroppedItemId = dropped.Id;
            return (-1, -1, null, -1, false, false, null, null);
        }

        if (me.IsOutside)
        {
            var hullCenter = ComputeCamera(snapshot, me).HullCenter;
            foreach (var dropped in snapshot.DroppedItems.Where(d => d.RoomId is null))
            {
                var local = ShipLocalFrame.ToLocal(dropped.Position, snapshot.ShipField, hullCenter);
                var screenPos = origin + new Vector2((float)local.X, (float)local.Y) * ShipRenderer.PixelsPerUnit;
                var rect = new Rectangle(
                    (int)screenPos.X - ShipRenderer.DroppedItemHitSize / 2, (int)screenPos.Y - ShipRenderer.DroppedItemHitSize / 2,
                    ShipRenderer.DroppedItemHitSize, ShipRenderer.DroppedItemHitSize);
                if (!rect.Contains(_designMouse))
                    continue;
                _pendingPickupDroppedItemId = dropped.Id;
                return (-1, -1, null, -1, false, false, null, null);
            }
        }

        // Doors toggle directly on click - no panel to open, just an immediate flip
        // (game_design.md Phase 3, M16). Skipped entirely while the axe is in hand - LMB is the
        // axe's swing button then (AxeSwingHeld below), not the door handle, so it never
        // accidentally pops a door open instead of chopping it.
        if (!HoldingAxe())
        {
            // Outside, CharacterState's own X/Y switch to AsteroidField world-space the instant
            // IsOutside flips (World.cs's CreateSnapshot), but a Door/AirlockOuterDoor's own
            // Position never does - it's always the ship's local, unrotated interior frame. Plain
            // NearEnough (which just diffs raw X/Y) compared those two different frames and always
            // came up short, so a suited character standing right next to an open airlock could
            // never actually click it closed. Converting the proximity point into that same local
            // frame - the same conversion World.WallBlocks.cs's FindAimedWallBlock does server-side
            // for a cutter aimed from outside - is what makes the click land where it's drawn.
            var doorClickPosition = me.IsOutside
                ? ShipLocalFrame.ToLocal(myPosition, snapshot.ShipField, ShipLocalFrame.GetHullCenter(snapshot.Rooms))
                : myPosition;

            // Rect comes from TileGridRasterizer.DoorTileRect, not the door's own raw
            // Left/Top/Width/Height - ShipRenderer.Draw's own door loop stopped using the raw rect
            // (bug report: the sprite sat half a tile off from the tile-square wall art flanking
            // it), so the click hit-test has to agree with wherever it's actually drawn now, or a
            // click on the visibly-correct door position would silently miss.
            // A destroyed door (World.Doors.cs) repairs on click instead of toggling (which would be
            // a no-op against a jammed-open door anyway) - same priority every other repairable
            // device above already gives its own click.
            bool DoorDestroyed(string doorId) => snapshot.DoorStates.FirstOrDefault(s => s.DoorId == doorId)?.Destroyed ?? false;

            foreach (var door in snapshot.Doors)
            {
                if (DoorRectIfNear(snapshot.Rooms, door, doorClickPosition, origin) is not { } rect || !rect.Contains(_designMouse))
                    continue;
                if (HoldingRepairTool() && DoorDestroyed(door.Id))
                {
                    _pendingRepairDeviceId = door.Id;
                    return (-1, -1, null, -1, false, false, null, null);
                }
                return (-1, -1, null, -1, false, false, null, door.Id);
            }

            foreach (var outerDoor in snapshot.AirlockOuterDoors)
            {
                if (OuterDoorRectIfNear(snapshot.Rooms, outerDoor, doorClickPosition, origin) is not { } rect || !rect.Contains(_designMouse))
                    continue;
                if (HoldingRepairTool() && DoorDestroyed(outerDoor.Id))
                {
                    _pendingRepairDeviceId = outerDoor.Id;
                    return (-1, -1, null, -1, false, false, null, null);
                }
                return (-1, -1, null, -1, false, false, null, outerDoor.Id);
            }

            // Aboard a boarded hull the doors are the fight: they start closed, and opening one lets
            // the breach through into the next compartment (World.EnemyAtmosphere.cs). Same click, same
            // proximity rule - the character's own coordinates are that structure's while aboard it.
            foreach (var door in snapshot.EnemyShip.Doors)
            {
                if (NearEnough(door.Position) && ShipRenderer.GetDoorRect(door.Left, door.Top, door.Width, door.Height, origin).Contains(_designMouse))
                    return (-1, -1, null, -1, false, false, null, door.Id);
            }
        }

        // A click mid-lay that missed every pin/mount/door/etc. above fixes a bend at that spot
        // instead of doing nothing (World.Wiring.cs's HandleWireBend) - the inverse of
        // ShipRenderer.GetBlockRect's own world->screen transform, so a bend lands exactly under
        // the cursor the same way every hit-test above already lines up with what's drawn there.
        if (me.LayingWireFromPin is not null)
        {
            _pendingWireBendAt = ScreenToShipLocal(new Vector2(_designMouse.X, _designMouse.Y), origin, SceneZoom(snapshot));
            return (-1, -1, null, -1, false, false, null, null);
        }

        // The scanner console isn't a small fixed-size panel like the ones below - it takes over
        // almost the whole screen and every click on it is meaningful (aim the beam, drop a marker,
        // pan the view), so "click landed outside CurrentPanelHousing()" is meaningless here
        // (CurrentPanelSize has no case for it, so it would fall back to the tiny centred Standard
        // box and read nearly every click on the map as "clicked away"). Matches
        // CloseBlockIfWalkedAway's own explicit Navigation exclusion - Esc is the one way out.
        if (_openBlock.Kind == BlockKind.Navigation)
            return (-1, -1, null, -1, false, false, null, null);

        // A click that landed on the open panel but hit none of its controls still belongs to the
        // panel - pressing bare housing metal is not "clicked away". Only a click genuinely outside
        // it closes the thing, which is what everybody expects of a window.
        if (CurrentPanelHousing() is { } openPanel && openPanel.Contains(_designMouse))
            return (-1, -1, null, -1, false, false, null, null);

        _openBlock = ClickTarget.None;
        _talkingToNpcId = null;
        return (-1, -1, null, -1, false, false, null, null);
    }

    // Crew chat input box (direct user request, "как в Баротравме") - only reaches the input while
    // explicitly focused (Enter to open), so W/A/D/S/X/Z keep flying the ship the rest of the time.
    private void OnChatTextInput(object? sender, TextInputEventArgs e)
    {
        if (!_sessionStarted || !_chatFocused)
            return;

        if (e.Character == '\b')
        {
            if (_chatInput.Length > 0)
                _chatInput = _chatInput[..^1];
            return;
        }
        if (e.Character == (char)27) // Escape - handled by UpdateCore's own polled keyboard read
        {
            // (UpdateCore already clears _chatInput/_chatFocused on Escape; nothing to do here.)
            return;
        }
        if (e.Character == '\r')
        {
            // The same physical Enter keystroke that just opened the box - don't also treat it as
            // an immediate submit/close (Game1.cs's UpdateCore sets this guard for exactly one
            // Update call around the frame it opens the box).
            if (_chatJustOpenedThisFrame)
            {
                _chatJustOpenedThisFrame = false;
                return;
            }
            var trimmed = _chatInput.Trim();
            if (trimmed.Length > 0)
                _pendingChatMessage = trimmed;
            _chatInput = "";
            _chatFocused = false;
            return;
        }
        if (!char.IsControl(e.Character) && _chatInput.Length < 120)
            _chatInput += e.Character;
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

    // Walking out of interaction range auto-closes whatever's open вЂ” matches the same radius
    // that gated opening it in the first place, so you can't keep adjusting a slider you've
    // wandered away from.
    private void CloseBlockIfWalkedAway(WorldSnapshot? snapshot)
    {
        if (_openBlock.Kind == BlockKind.None || snapshot is null)
            return;

        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        if (me is null)
            return;

        // The scanner (M44/M47) deliberately doesn't auto-close on walking away - entered with E,
        // it's meant to stay open (map/scanner sweep/manual markers all keep working) while the
        // Scientist walks around doing something else, closing only on a deliberate Esc.
        if (_openBlock.Kind == BlockKind.Navigation)
            return;

        // Station dialogue closes as soon as you're not next to the NPC you were talking to (or
        // not on the station at all any more) - a separate coordinate space from every other
        // block below, so it can't share their myPosition-based distance check.
        if (_openBlock.Kind == BlockKind.Station)
        {
            var talkingTo = snapshot.Station.Npcs.FirstOrDefault(n => n.Id == _talkingToNpcId);
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
            BlockKind.Battery => snapshot.BatteryBlock.Position,
            BlockKind.Navigation => snapshot.NavigationConsole.Position,
            BlockKind.Jukebox => snapshot.Jukebox?.Block.Position ?? myPosition,
            BlockKind.CardTable => snapshot.CardTable.Position,
            BlockKind.Rack => _openBlock.TargetComponentId is { } rackId
                ? snapshot.StorageRacks.FirstOrDefault(r => r.Id == rackId)?.Position ?? myPosition
                : myPosition,
            BlockKind.Connections => _openBlock.TargetComponentId is { } targetId
                ? snapshot.Wiring.Components.FirstOrDefault(c => c.Id == targetId)?.Position ?? myPosition
                : myPosition,
            // Whichever of this system's devices is actually nearest, not always the first one -
            // a system with several identical devices (Engine/Shields) opens the exact same panel
            // from any of them, so staying open must track whichever one the player is actually
            // standing next to, not always device[0]'s position (which could be clear across the
            // ship from the one they opened it at, instantly slamming the panel shut again).
            BlockKind.System => snapshot.SystemDevices
                .Where(d => d.System == _openBlock.System)
                .OrderBy(d => (d.Position - myPosition).Length())
                .First().Position,
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
                var owner = snapshot.Wiring.Components.FirstOrDefault(c => c.Id == pin.ComponentId);
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
            var undoHint = (me.LayingWireBends?.Count ?? 0) > 0 ? "[ПКМ] убрать последний изгиб" : "[ПКМ] отменить";
            return $"Ведём провод от {ComponentRenderer.PinLabel(snapshot, start)} — [ЛКМ] зафиксировать изгиб, навести на контакт — закончить  {undoHint}";
        }

        if (HoldingWireSpool())
        {
            if (HoveredPin() is { } pin)
                return $"[ЛКМ] начать провод: {ComponentRenderer.PinLabel(snapshot, pin)}";
            return null;
        }

        foreach (var mount in snapshot.Wiring.ComponentMounts)
        {
            if (!NearEnough(mount.Position) || !ComponentRenderer.GetMountBodyRect(mount, origin).Contains(_designMouse))
                continue;

            var installedId = snapshot.Wiring.ComponentMountStates.FirstOrDefault(s => s.MountId == mount.Id)?.InstalledComponentId;
            if (installedId is null)
            {
                var heldKind = HeldItemTypes(me.Inventory).Select(ComponentDefinitions.ComponentKindFor).FirstOrDefault(k => k is not null);
                return heldKind is { } kind
                    ? $"[ЛКМ] установить: {ComponentDefinitions.DisplayName(kind)}"
                    : "Нужен компонент в руке";
            }

            var installed = snapshot.Wiring.Components.FirstOrDefault(c => c.Id == installedId);
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
            return $"Наводка мышью ({manned.AimDegrees:0}°)  [Space] огонь  [E] встать";

        if (me.IsAtHelm)
            return "[W] ход  [X] назад  [A/D] поворот  [S] стабилизация  [E] встать";

        if (me.OnEnemyShip)
        {
            var boardingPosition = new Vec2(me.X, me.Y);
            var weapon = HeldItemTypes(me.Inventory).FirstOrDefault(WeaponDefinitions.IsWeapon);
            if (!WeaponDefinitions.IsWeapon(weapon))
                return "Нужно оружие в руках!  [WASD] отступить к пробоине";

            // CharacterState carries no RoomId, so the hint derives the room the same way the
            // interior hint already does for breaches - by which room rect contains the position.
            var boardingRoom = snapshot.EnemyShip.Rooms.FirstOrDefault(r => r.Contains(boardingPosition));
            var inRange = snapshot.EnemyShip.Crew.Any(c => c.Alive && c.RoomId == boardingRoom?.Id &&
                (new Vec2(c.X, c.Y) - boardingPosition).Length() <= WeaponDefinitions.Range(weapon));
            var remaining = snapshot.EnemyShip.Crew.Count(c => c.Alive);
            return inRange
                ? $"[Space] огонь ({ItemDefinitions.DisplayName(weapon)})  Осталось врагов: {remaining}"
                : $"Абордаж. Осталось врагов: {remaining}";
        }

        if (me.OnStation)
        {
            var stationPosition = new Vec2(me.X, me.Y);

            if (snapshot.Station.Guards.Any(g => g.Alive && g.Alerted))
                return "Охрана открыла огонь!  [Space] отстреливаться  [WASD] к шлюзу";

            var nearCrate = snapshot.Station.Crates.FirstOrDefault(c =>
                !(snapshot.Station.CrateStates.FirstOrDefault(s => s.CrateId == c.Id)?.Looted ?? false) &&
                NearEnough(c.Position, stationPosition));
            if (nearCrate is not null)
                return $"[E] украсть: {ItemDefinitions.DisplayName(nearCrate.Item)} (охрана не должна увидеть)";

            var nearNpc = snapshot.Station.Npcs.FirstOrDefault(n =>
                n.Kind is not (NpcKind.Security or NpcKind.Scientist) && NearEnough(n.Position, stationPosition));
            if (nearNpc is not null)
                return $"[ЛКМ] поговорить: {nearNpc.Name}";

            var nearGuard = snapshot.Station.Npcs.Any(n =>
                n.Kind == NpcKind.Security && (n.Position - stationPosition).Length() < 4f);
            return nearGuard ? "Рядом охрана" : "На станции";
        }

        if (me.IsOutside)
        {
            var evaPosition = new Vec2(me.X, me.Y);
            var holdingCutter = HeldItemTypes(me.Inventory).Contains(ItemType.Cutter);

            var nearbyDropped = snapshot.DroppedItems.FirstOrDefault(d => d.RoomId is null && (d.Position - evaPosition).Length() < PickupHintRadius);
            if (nearbyDropped is not null)
                return $"[E]/[ЛКМ] подобрать: {ItemDefinitions.DisplayName(nearbyDropped.Item)}";

            var nearbyDeposit = snapshot.Field.OreDeposits.Any(d =>
                (snapshot.Field.OreDepositStates.FirstOrDefault(s => s.DepositId == d.Id)?.Hp ?? 0f) > 0f &&
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
            return "[E] использовать аптечку";

        var myPosition = new Vec2(me.X, me.Y);
        var nearTurret = snapshot.Turrets.Any(t => NearEnough(t.PeriscopePosition, myPosition));
        var nearAmmoTurret = snapshot.Turrets.Any(t =>
            t.WeaponType != TurretWeaponType.Laser && NearEnough(t.PeriscopePosition, myPosition));

        if (me.CarryingAmmoCrate)
            return nearAmmoTurret ? "[E] зарядить орудие" : "Несёте ящик патронов к орудию";

        var nearStorage = snapshot.AmmoStorages.FirstOrDefault(s => NearEnough(s.Position, myPosition));
        if (nearStorage is not null)
        {
            var stock = snapshot.AmmoStorageStates.FirstOrDefault(s => s.StorageId == nearStorage.Id);
            return stock is { Remaining: 0 }
                ? "Склад патронов пуст — пополняется на станции"
                : $"[E] взять ящик патронов ({stock?.Remaining ?? 0}/{stock?.Capacity ?? 0})";
        }

        // Ship/station floor drops only (World.Storage.cs's drag-to-floor) - EVA's own dropped items
        // are handled above, in the me.IsOutside branch, against the asteroid-field position instead.
        var nearDroppedItem = snapshot.DroppedItems.FirstOrDefault(d => d.RoomId is not null && (d.Position - myPosition).Length() < PickupHintRadius);
        if (nearDroppedItem is not null)
            return $"[ЛКМ] подобрать: {ItemDefinitions.DisplayName(nearDroppedItem.Item)}";

        var holding = HeldItemTypes(me.Inventory);

        var nearDamagedTurret = snapshot.Turrets.Any(t =>
            NearEnough(t.PeriscopePosition, myPosition) &&
            (snapshot.TurretStates.FirstOrDefault(s => s.Id == t.Id)?.Damaged ?? false));
        if (nearDamagedTurret)
        {
            return holding.Contains(ItemType.Wrench) || holding.Contains(ItemType.Screwdriver)
                ? "[E] почини турель"
                : "Нужен гаечный ключ или отвёртка в руке";
        }

        if (nearTurret)
            return "[E] сесть за орудие";

        var nearHelm = NearEnough(snapshot.HelmConsole.Position, myPosition);
        if (nearHelm)
            return "[E] встать за навигационную панель";

        var nearDamagedSystem = snapshot.SystemDevices.FirstOrDefault(d =>
            NearEnough(d.Position, myPosition) &&
            (snapshot.SystemStates.FirstOrDefault(s => s.DeviceId == d.Id)?.Damaged ?? false));
        if (nearDamagedSystem is not null)
        {
            return holding.Contains(ItemType.Wrench) || holding.Contains(ItemType.Screwdriver)
                ? "[E] почини систему"
                : "Нужен гаечный ключ или отвёртка в руке";
        }

        var nearJunction = snapshot.Wiring.Components.FirstOrDefault(c =>
            c.Kind == ComponentKind.Junction && NearEnough(c.Position, myPosition));
        if (nearJunction is not null)
        {
            var junctionDamaged = snapshot.JunctionStates.FirstOrDefault(s => s.DeviceId == nearJunction.Id)?.Damaged ?? false;
            if (junctionDamaged)
                return holding.Contains(ItemType.Wrench) || holding.Contains(ItemType.Screwdriver)
                    ? "[E] почини щиток"
                    : "Нужен гаечный ключ или отвёртка в руке";
        }

        var nearLocker = snapshot.SuitLockers.FirstOrDefault(l => NearEnough(l.Position, myPosition));
        if (nearLocker is not null)
        {
            // Each locker holds exactly one suit now (World.SuitLockers.cs) - the hint reflects
            // whether F will actually do anything here, not just whether a locker is nearby.
            var hasSuit = snapshot.SuitLockerStates.FirstOrDefault(s => s.LockerId == nearLocker.Id)?.HasSuit ?? false;
            if (me.WearingSuit)
                return hasSuit ? "Шкаф занят" : "[E] снять скафандр";
            return hasSuit ? "[E] надеть скафандр" : "Шкаф пуст";
        }

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

        var nearDoor = snapshot.Doors.Any(d => NearEnough(d.Position, myPosition));
        var nearOuterDoor = snapshot.AirlockOuterDoors.Any(d => NearEnough(d.Position, myPosition));

        // A destroyed door (World.Doors.cs) is jammed open and needs the same E-key minigame as a
        // damaged SystemDevice/Junction, not the ordinary click-to-toggle everything below assumes.
        var nearbyDestroyedDoorId =
            snapshot.Doors.FirstOrDefault(d => NearEnough(d.Position, myPosition) &&
                (snapshot.DoorStates.FirstOrDefault(s => s.DoorId == d.Id)?.Destroyed ?? false))?.Id
            ?? snapshot.AirlockOuterDoors.FirstOrDefault(d => NearEnough(d.Position, myPosition) &&
                (snapshot.DoorStates.FirstOrDefault(s => s.DoorId == d.Id)?.Destroyed ?? false))?.Id;
        if (nearbyDestroyedDoorId is not null)
        {
            return holding.Contains(ItemType.Wrench) || holding.Contains(ItemType.Screwdriver)
                ? "[E] почини дверь"
                : "Дверь разрушена — нужен гаечный ключ или отвёртка";
        }

        // The commonest way to be stuck aboard: suit on, socket empty. Said at the door, where the
        // player is standing when they find out nothing happens (World.Eva.cs gates on the tank).
        if (nearOuterDoor && me.WearingSuit && me.SuitTank is null)
            return "В скафандре нет баллона — наружу не выпустит";
        // Axe in hand overrides the ordinary door click everywhere below - HandleMouseClick already
        // skips the toggle click while holding it, so the hint has to match what LMB will actually do.
        if (holding.Contains(ItemType.Axe) && (nearDoor || nearOuterDoor))
            return "[ЛКМ] рубить дверь топором";

        // Aboard a boarded hull the same click matters more: those doors start closed, and opening
        // one lets the breach through into the compartment behind it (World.EnemyAtmosphere.cs).
        if (me.OnEnemyShip &&
            snapshot.EnemyShip.Doors.Any(d => NearEnough(d.Position, myPosition)))
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
