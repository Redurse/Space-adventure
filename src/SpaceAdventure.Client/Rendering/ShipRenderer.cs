using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Draws the ship's rooms and characters with a fixed top-down camera (no follow/zoom yet — M2 scope).
public sealed class ShipRenderer
{
    public const float PixelsPerUnit = 48f;
    private const float CharacterDiameter = 0.7f; // world units

    // Size tiers requested for the power grid blocks: reactor/engine read as the biggest,
    // fixed installations; the distribution block is noticeably bigger than a plain system
    // block but still smaller than those two.
    public const int NormalBlockSize = 20;
    public const int MediumBlockSize = 28;
    public const int BigBlockSize = 36;

    // Bulkhead slab, in screen pixels, centred on the room boundary. Deliberately narrower than a
    // door's 1-unit (48px) span so a doorway still cuts cleanly through it, and narrower than twice
    // RoomLayout.CharacterRadius (33.6px) so a character stopped at the collision clearance never
    // still pokes out past the wall's outer face.
    private const int WallThickness = 28;
    private const int RibSpacing = 26;

    private readonly Texture2D _pixel;
    private readonly Texture2D _floorPlate;
    private readonly Texture2D _wallPlate;
    private readonly Texture2D _hullPlate;
    private readonly SpriteFont _font;
    private readonly Starfield _starfield;

    // worldViewport: the same rect Game1's WorldViewportOrigin/WorldViewportSize describe - passed
    // in rather than duplicated here so the starfield always fills exactly the area the ship is
    // actually drawn into, not a guess at it.
    public ShipRenderer(GraphicsDevice graphicsDevice, SpriteFont font, Rectangle worldViewport)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _floorPlate = TileTextures.CreateFloorPlate(graphicsDevice);
        _wallPlate = TileTextures.CreateWallPlate(graphicsDevice);
        _hullPlate = TileTextures.CreateHullPlate(graphicsDevice);
        _font = font;
        _starfield = new Starfield(_pixel, worldViewport);
    }

    // Shared by Draw() and by Game1's mouse hit-testing so click regions always match what's
    // actually rendered.
    public static Rectangle GetBlockRect(Vec2 worldPosition, int size, Vector2 origin)
    {
        var center = origin + new Vector2(worldPosition.X, worldPosition.Y) * PixelsPerUnit;
        return new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);
    }

    // Shared with Game1's door click-toggle hit-testing (game_design.md Phase 3, M16) so the
    // clickable area always matches what DrawDoor actually renders.
    public static Rectangle GetDoorRect(float left, float top, float width, float height, Vector2 origin) =>
        new(
            (int)(origin.X + left * PixelsPerUnit),
            (int)(origin.Y + top * PixelsPerUnit),
            (int)(width * PixelsPerUnit),
            (int)(height * PixelsPerUnit));

    // One continuous space, drawn the same way no matter where the camera is currently looking
    // from (a crew station, a manned turret's periscope, drifting outside in a suit) - there used
    // to be a second "closed up" mode substituted in for the turret view specifically, so a
    // breach couldn't be seen through from behind the gun; that's exactly backwards from how a
    // real hull breach should read, so the ship is never drawn any other way now.
    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, ClickTarget openBlock,
        float totalSeconds = 0f, IEnumerable<TransientEffect>? effects = null,
        IEnumerable<AtmosphereParticle>? atmosphere = null)
    {
        var forwardDegrees = ShipCatalog.ForwardDegrees(snapshot.CurrentShipKind);

        // Space itself, under absolutely everything - fixed to the screen (not translated by
        // origin) so it reads as an infinitely distant backdrop rather than a world object the
        // camera pans across.
        _starfield.Draw(spriteBatch, totalSeconds);

        // The armour the compartments sit inside, under everything else - what shows of it is the
        // plated border around the decks and the bow sticking out ahead of them.
        HullSkin.Draw(spriteBatch, _pixel, _hullPlate, snapshot.Rooms, snapshot.AirlockOuterDoors, snapshot.SystemDevices,
            origin, forwardDegrees, snapshot.CurrentShipKind, totalSeconds, snapshot.SystemStates);

        // Floors first, walls second: the bulkheads are thick and straddle the boundary between
        // two rooms, so a room drawn later would otherwise paint its floor over its neighbour's
        // wall slab.
        foreach (var room in snapshot.Rooms)
            DrawRoomFloor(spriteBatch, room, RoomOxygen(snapshot, room.Id), origin);
        foreach (var room in snapshot.Rooms)
            DrawRoomWalls(spriteBatch, room, RoomOxygen(snapshot, room.Id), origin);

        // Drawn after room outlines so the opening visibly cuts through the shared wall.
        foreach (var door in snapshot.Doors)
        {
            var isOpen = snapshot.DoorStates.FirstOrDefault(s => s.DoorId == door.Id)?.IsOpen ?? true;
            DrawDoor(spriteBatch, door.Left, door.Top, door.Width, door.Height, isOpen, origin);
        }

        foreach (var outerDoor in snapshot.AirlockOuterDoors)
        {
            var isOpen = snapshot.DoorStates.FirstOrDefault(s => s.DoorId == outerDoor.Id)?.IsOpen ?? false;
            DrawDoor(spriteBatch, outerDoor.Left, outerDoor.Top, outerDoor.Width, outerDoor.Height, isOpen, origin, leadsToVacuum: true);
        }

        // Only breached blocks get drawn — an intact one is just an ordinary bit of the hull
        // the room outline already implies.
        foreach (var state in snapshot.WallBlockStates)
        {
            if (!state.Breached)
                continue;
            var block = snapshot.WallBlocks.FirstOrDefault(b => b.Id == state.Id);
            var room = block is null ? null : snapshot.Rooms.FirstOrDefault(r => r.Id == block.RoomId);
            if (block is not null && room is not null)
                DrawBreachedWallBlock(spriteBatch, block, room, origin, totalSeconds);
        }

        foreach (var storage in snapshot.AmmoStorages)
            DrawAmmoStorage(spriteBatch, storage, origin);

        foreach (var locker in snapshot.SuitLockers)
        {
            var hasSuit = snapshot.SuitLockerStates.FirstOrDefault(s => s.LockerId == locker.Id)?.HasSuit ?? false;
            DrawSuitLocker(spriteBatch, locker, origin, hasSuit);
        }

        DrawDroppedItems(spriteBatch, snapshot.DroppedItems, snapshot.Rooms.Select(r => r.Id), origin, totalSeconds);

        foreach (var device in snapshot.SystemDevices)
        {
            // Match by DeviceId, not System — Shields has two separate physical generators
            // (M14) that can be damaged independently of each other.
            var damaged = snapshot.SystemStates.FirstOrDefault(s => s.DeviceId == device.Id)?.Damaged ?? false;
            var isOpen = openBlock.Kind == BlockKind.System && openBlock.System == device.System;
            var size = (int)((device.System == PowerSystemId.Engine ? BigBlockSize : NormalBlockSize) * device.SizeScale);
            DrawSystemDevice(spriteBatch, device, damaged, isOpen, size, origin);
        }

        DrawReactorBlock(spriteBatch, snapshot.ReactorBlock, snapshot.Reactor, openBlock.Kind == BlockKind.Reactor, origin);
        DrawDistributionBlock(spriteBatch, snapshot.DistributionBlock, openBlock.Kind == BlockKind.Distribution, origin);
        DrawNavigationConsole(spriteBatch, snapshot.NavigationConsole, openBlock.Kind == BlockKind.Navigation, origin);
        for (var rackIndex = 0; rackIndex < snapshot.StorageRacks.Count; rackIndex++)
        {
            var rack = snapshot.StorageRacks[rackIndex];
            var isOpen = openBlock.Kind == BlockKind.Rack && openBlock.TargetComponentId == rack.Id;
            DrawStorageRack(spriteBatch, rack, rackIndex * StorageRack.Capacity, snapshot, isOpen, origin);
        }
        ComponentRenderer.Draw(spriteBatch, _pixel, _font, snapshot, origin, totalSeconds);
        var anyoneAtHelm = snapshot.Characters.Any(c => c.IsAtHelm);
        DrawHelmConsole(spriteBatch, snapshot.HelmConsole, anyoneAtHelm, origin);
        DrawCardTable(spriteBatch, snapshot.CardTable, snapshot.CardGame is not null, origin);

        foreach (var turret in snapshot.Turrets)
        {
            var state = snapshot.TurretStates.FirstOrDefault(s => s.Id == turret.Id);
            DrawTurret(spriteBatch, turret, state, snapshot.Rooms, snapshot.Turrets, origin);
        }

        foreach (var character in snapshot.Characters)
            DrawCharacter(spriteBatch, character, origin);

        // A cutter works anywhere - there's just nothing to cut in here. The flame still lights, and
        // it still burns the tank, so "why is my bottle empty" has a visible cause.
        foreach (var shot in snapshot.PersonalShots.Where(s => s.Scene == ShotScene.Ship))
            BoardingRenderer.DrawShot(spriteBatch, _pixel, shot, origin);

        foreach (var character in snapshot.Characters.Where(c => c.Cutting && !c.IsOutside && !c.OnStation && !c.OnEnemyShip))
        {
            var facing = new Vector2(character.FacingX, character.FacingY);
            var center = origin + new Vector2(character.X, character.Y) * PixelsPerUnit;
            var muzzle = GetHeldToolMuzzle(ItemType.Cutter, character.Inventory, center, facing) ?? center + HeldToolOffset(facing);
            FieldRenderer.DrawCuttingFlame(spriteBatch, _pixel, muzzle, facing, totalSeconds);
        }

        foreach (var character in snapshot.Characters.Where(c => c.Welding && !c.IsOutside && !c.OnStation && !c.OnEnemyShip))
        {
            var facing = new Vector2(character.FacingX, character.FacingY);
            var center = origin + new Vector2(character.X, character.Y) * PixelsPerUnit;
            var muzzle = GetHeldToolMuzzle(ItemType.WeldingTool, character.Inventory, center, facing) ?? center + HeldToolOffset(facing);
            FieldRenderer.DrawWeldingFlame(spriteBatch, _pixel, muzzle, facing, totalSeconds);
        }

        if (effects is not null)
            foreach (var effect in effects.Where(e => e.Kind != EffectKind.Cut)) // Cut is exterior-only, drawn by FieldRenderer
                DrawSparkBurst(spriteBatch, origin + new Vector2(effect.Position.X, effect.Position.Y) * PixelsPerUnit, effect.Progress, effect.Kind == EffectKind.Weld ? Color.White : Color.PaleGreen);

        if (atmosphere is not null)
            foreach (var particle in atmosphere)
                DrawAtmosphereParticle(spriteBatch, particle, origin);
    }

    // A breach's steam, a damaged system's sparks, a starved reactor's embers - continuous rather
    // than a one-shot burst, so each is just a soft dot that drifts and fades over its own lifetime
    // (AtmosphereParticle.Progress) instead of DrawSparkBurst's radiating rays.
    private void DrawAtmosphereParticle(SpriteBatch spriteBatch, AtmosphereParticle particle, Vector2 origin)
    {
        var center = origin + new Vector2(particle.Position.X, particle.Position.Y) * PixelsPerUnit;
        var alpha = 1f - particle.Progress;
        var color = particle.Kind switch
        {
            AtmosphereKind.Steam => Color.WhiteSmoke * (alpha * 0.32f),
            AtmosphereKind.Spark => Color.Lerp(Color.Yellow, Color.OrangeRed, particle.Progress) * alpha,
            _ => Color.Lerp(Color.Orange, new Color(90, 20, 10), particle.Progress) * alpha,
        };
        // Steam swells as it disperses; sparks and embers shrink towards nothing.
        var scale = particle.Kind == AtmosphereKind.Steam ? 1f + particle.Progress * 1.6f : 1f - particle.Progress * 0.6f;
        var size = particle.Size * PixelsPerUnit * scale;
        spriteBatch.Draw(_pixel, center, null, color, 0f, new Vector2(0.5f, 0.5f), new Vector2(size, size), SpriteEffects.None, 0f);
    }

    // Barotrauma-style brief spark burst for a tool action that just landed (welding a breach,
    // repairing a system) - a handful of short rays radiating from the point, expanding and
    // fading over the effect's lifetime (TransientEffect.Progress goes 0 -> 1).
    private void DrawSparkBurst(SpriteBatch spriteBatch, Vector2 center, float progress, Color color)
    {
        var alpha = 1f - progress;
        var length = 5f + progress * 16f;
        const int rayCount = 6;
        for (var i = 0; i < rayCount; i++)
        {
            var angle = i * MathF.PI * 2f / rayCount + progress * 2f;
            spriteBatch.Draw(_pixel, center, null, color * alpha, angle, new Vector2(0f, 0.5f), new Vector2(length, 2f), SpriteEffects.None, 0f);
        }
    }

    // Shared industrial "panel" look for equipment blocks (game_design.md Phase 3 visual pass) —
    // a beveled face plus four corner rivets, built entirely from the single white pixel texture
    // (this project has no image assets/content pipeline for real sprites).
    private void DrawPanel(SpriteBatch spriteBatch, Rectangle rect, Color faceColor, Color borderColor, int borderThickness) =>
        DrawPanel(spriteBatch, _pixel, rect, faceColor, borderColor, borderThickness);

    // internal + static, with the pixel texture passed explicitly, so ComponentRenderer.cs can draw
    // the exact same beveled-panel-plus-rivets look for installed components instead of a new art
    // style from scratch.
    internal static void DrawPanel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color faceColor, Color borderColor, int borderThickness)
    {
        spriteBatch.Draw(pixel, rect, faceColor);
        // Bevel: a lighter sliver along the top/left, a darker one along bottom/right.
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), Color.White * 0.18f);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), Color.White * 0.18f);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), Color.Black * 0.35f);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), Color.Black * 0.35f);
        DrawRectOutline(spriteBatch, pixel, rect, borderColor, borderThickness);
        DrawRivets(spriteBatch, pixel, rect);
    }

    private void DrawRivets(SpriteBatch spriteBatch, Rectangle rect) => DrawRivets(spriteBatch, _pixel, rect);

    internal static void DrawRivets(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect)
    {
        const int inset = 3;
        const int size = 2;
        var color = Color.Black * 0.5f;
        foreach (var (x, y) in new[]
                 {
                     (rect.X + inset, rect.Y + inset), (rect.Right - inset - size, rect.Y + inset),
                     (rect.X + inset, rect.Bottom - inset - size), (rect.Right - inset - size, rect.Bottom - inset - size),
                 })
            spriteBatch.Draw(pixel, new Rectangle(x, y, size, size), color);
    }

    // Alternating yellow/black hazard tape (SS13/Barotrauma convention for anything dangerous:
    // airlocks, breached hull) - plain vertical stripes rather than true diagonals since there's
    // no clipping/scissor rect available to cut a rotated stripe to the target shape.
    private void DrawHazardStripes(SpriteBatch spriteBatch, Rectangle rect, bool horizontal)
    {
        const int stripeSize = 5;
        if (horizontal)
        {
            for (var x = rect.X; x < rect.Right; x += stripeSize)
            {
                var w = Math.Min(stripeSize, rect.Right - x);
                var stripe = ((x - rect.X) / stripeSize) % 2 == 0 ? Color.Gold : Color.Black;
                spriteBatch.Draw(_pixel, new Rectangle(x, rect.Y, w, rect.Height), stripe * 0.9f);
            }
        }
        else
        {
            for (var y = rect.Y; y < rect.Bottom; y += stripeSize)
            {
                var h = Math.Min(stripeSize, rect.Bottom - y);
                var stripe = ((y - rect.Y) / stripeSize) % 2 == 0 ? Color.Gold : Color.Black;
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, y, rect.Width, h), stripe * 0.9f);
            }
        }
    }

    private void DrawAmmoStorage(SpriteBatch spriteBatch, AmmoStorage storage, Vector2 origin)
    {
        const int size = 16;
        var center = origin + new Vector2(storage.X, storage.Y) * PixelsPerUnit;
        var rect = new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);
        DrawPanel(spriteBatch, rect, Color.SaddleBrown * 0.85f, Color.SaddleBrown, 1);
    }

    // An upright cabinet, not a flat floor tile - a vertical seam down the middle like a locker
    // door, plus a small status light (lit CadetBlue with a suit inside, dim when it's been taken
    // and not yet put back - SuitLockerPanel shows the same state in more detail on click).
    private void DrawSuitLocker(SpriteBatch spriteBatch, SuitLocker locker, Vector2 origin, bool hasSuit)
    {
        var center = origin + new Vector2(locker.X, locker.Y) * PixelsPerUnit;
        var rect = GetBlockRect(locker.Position, NormalBlockSize, origin);
        DrawPanel(spriteBatch, rect, Color.SlateGray * 0.7f, Color.LightSteelBlue, 1);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Center.X - 1, rect.Y + 2, 2, rect.Height - 4), Color.LightSteelBlue * 0.6f);

        const int lightSize = 4;
        var lightColor = hasSuit ? Color.CadetBlue : new Color(60, 70, 68);
        spriteBatch.Draw(_pixel, new Rectangle((int)center.X - lightSize / 2, rect.Y + 3, lightSize, lightSize), lightColor);
    }

    public const int DroppedItemHitSize = 20;

    // Shared by Draw() and Game1's click-to-pick-up hit-testing, same "one function serves both"
    // convention GetBlockRect already establishes.
    public static Rectangle GetDroppedItemRect(DroppedItem dropped, Vector2 origin) =>
        GetBlockRect(dropped.Position, DroppedItemHitSize, origin);

    // Reused by StationRenderer (constructed with this instance) so a station floor's own drops get
    // the same look through the same method rather than a second copy of it - FieldRenderer's
    // DrawDroppedItem is the EVA-space twin of this, same pulsing-diamond idea, different Draw() and
    // a different coordinate frame entirely, so it isn't shared code, just a shared look.
    internal void DrawDroppedItems(SpriteBatch spriteBatch, IReadOnlyList<DroppedItem> droppedItems,
        IEnumerable<string> validRoomIds, Vector2 origin, float totalSeconds)
    {
        var rooms = validRoomIds as ICollection<string> ?? validRoomIds.ToList();
        foreach (var dropped in droppedItems)
        {
            if (dropped.RoomId is not { } roomId || !rooms.Contains(roomId))
                continue;

            var center = origin + new Vector2(dropped.X, dropped.Y) * PixelsPerUnit;
            var pulse = 0.8f + 0.2f * MathF.Sin(totalSeconds * 4f + center.X);
            const int size = 14;
            var rect = new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);
            DrawPanel(spriteBatch, rect, Color.LightGoldenrodYellow * (0.55f * pulse), Color.LightGoldenrodYellow, 1);
            spriteBatch.DrawString(_font, ItemDefinitions.ShortLabel(dropped.Item), center + new Vector2(9, -7),
                Color.LightGoldenrodYellow, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }
    }

    // Physical, damageable system block (game_design.md section 1) — click it to see its
    // readout. Bigger than the item/tool markers so it reads as ship equipment; Engine gets the
    // "big" tier like the reactor (see Draw()).
    private void DrawSystemDevice(SpriteBatch spriteBatch, ShipSystemDevice device, bool damaged, bool isOpen, int size, Vector2 origin)
    {
        var rect = GetBlockRect(device.Position, size, origin);
        var center = new Vector2(rect.Center.X, rect.Center.Y);

        DrawPanel(spriteBatch, rect, damaged ? Color.Red * 0.6f : Color.SlateGray * 0.8f, damaged ? Color.Red : isOpen ? Color.Gold : Color.LightSteelBlue, isOpen ? 3 : 2);
        if (damaged)
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), horizontal: true);
        spriteBatch.DrawString(_font, SystemShortLabel(device.System), new Vector2(rect.X + 2, rect.Y + 3), Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        if (damaged)
            spriteBatch.DrawString(_font, "!", center + new Vector2(size / 2f - 2, -size), Color.Red, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
    }

    // Big, clickable — walking up and clicking it "enters" the reactor and shows its 4 fuel-rod
    // slots (see ReactorPanel). Glows warmer the more rods are loaded.
    private void DrawReactorBlock(SpriteBatch spriteBatch, ReactorBlock block, ReactorState reactor, bool isOpen, Vector2 origin)
    {
        var rect = GetBlockRect(block.Position, (int)(BigBlockSize * block.SizeScale), origin);
        var running = reactor.CurrentOutput > 0;
        DrawPanel(spriteBatch, rect, running ? Color.DarkOrange * 0.55f : Color.DimGray * 0.6f, isOpen ? Color.Gold : running ? Color.Orange : Color.Gray, isOpen ? 3 : 2);
        // Core glow: a small inset square that reads as the fuel core, brighter while running.
        var coreSize = rect.Width / 3;
        var coreRect = new Rectangle(rect.Center.X - coreSize / 2, rect.Center.Y - coreSize / 2, coreSize, coreSize);
        spriteBatch.Draw(_pixel, coreRect, (running ? Color.Yellow : Color.DarkSlateGray) * (running ? 0.8f : 0.5f));
        spriteBatch.DrawString(_font, "Реактор", new Vector2(rect.X + 2, rect.Y + 4), Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    // Medium — bigger than a plain system block, smaller than the reactor/engine (as requested).
    private void DrawDistributionBlock(SpriteBatch spriteBatch, PowerDistributionBlock block, bool isOpen, Vector2 origin)
    {
        var rect = GetBlockRect(block.Position, MediumBlockSize, origin);
        DrawPanel(spriteBatch, rect, Color.MediumPurple * 0.6f, isOpen ? Color.Gold : Color.Plum, isOpen ? 3 : 2);
        spriteBatch.DrawString(_font, "Э", new Vector2(rect.X + 6, rect.Y + 6), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    // Cargo shelving (game_design.md section 13) — click it to open its 30 slots. Shows how full it
    // is at a glance as a row of little filled bars, so you can tell a loaded rack from an empty one
    // without walking over and opening it.
    // offset: where this particular shelf's own 30-slot band starts in the snapshot's flat
    // RackSlots array (World.Storage.cs's RackFor) - a hull carries two shelves now, so the "how
    // full" readout has to count only this one's band, not every shelf's items combined.
    private void DrawStorageRack(SpriteBatch spriteBatch, StorageRack rack, int offset, WorldSnapshot snapshot, bool isOpen, Vector2 origin)
    {
        var rect = GetBlockRect(rack.Position, MediumBlockSize, origin);
        DrawPanel(spriteBatch, rect, Color.Sienna * 0.6f, isOpen ? Color.Gold : Color.Peru, isOpen ? 3 : 2);

        var used = 0;
        for (var i = 0; i < StorageRack.Capacity; i++)
            if (offset + i < snapshot.RackSlots.Count && snapshot.RackSlots[offset + i] is not null)
                used++;
        const int shelves = 3;
        for (var i = 0; i < shelves; i++)
        {
            var filled = used > i * StorageRack.Capacity / shelves;
            var y = rect.Y + 6 + i * 7;
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + 5, y, rect.Width - 10, 3), filled ? Color.Khaki : Color.Black * 0.5f);
        }
    }

    // Bridge console (game_design.md section 5) — click it to bring up the galaxy map.
    private void DrawNavigationConsole(SpriteBatch spriteBatch, NavigationConsole console, bool isOpen, Vector2 origin)
    {
        var rect = GetBlockRect(console.Position, MediumBlockSize, origin);
        DrawPanel(spriteBatch, rect, Color.Teal * 0.6f, isOpen ? Color.Gold : Color.LightSeaGreen, isOpen ? 3 : 2);
        spriteBatch.DrawString(_font, "Карта", new Vector2(rect.X + 1, rect.Y + 7), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }


    // Pilot's console (game_design.md Phase 3, M15) — click it to man it and bring up the helm's
    // joystick panel instead of the ship view.
    private void DrawHelmConsole(SpriteBatch spriteBatch, HelmConsole console, bool isOpen, Vector2 origin)
    {
        var rect = GetBlockRect(console.Position, MediumBlockSize, origin);
        DrawPanel(spriteBatch, rect, Color.DarkGoldenrod * 0.6f, isOpen ? Color.Gold : Color.Goldenrod, isOpen ? 3 : 2);
        spriteBatch.DrawString(_font, "Штурв", new Vector2(rect.X + 1, rect.Y + 7), Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }

    // A quiet card table - not clickable, just a felt surface bolted to the deck; two crew
    // standing beside it together is what actually starts a hand (World.CardGame.cs's
    // StepCardGame). Lit up gold whenever a hand happens to be running there, the same "isOpen"
    // glow every other console gets, so it's obvious at a glance the table isn't just furniture.
    private void DrawCardTable(SpriteBatch spriteBatch, CardTable table, bool inUse, Vector2 origin)
    {
        var rect = GetBlockRect(table.Position, MediumBlockSize, origin);
        DrawPanel(spriteBatch, rect, new Color(24, 90, 52) * 0.75f, inUse ? Color.Gold : new Color(90, 140, 100), inUse ? 3 : 2);
        spriteBatch.DrawString(_font, "Карты", new Vector2(rect.X + 1, rect.Y + 7), Color.White, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
    }

    private static string SystemShortLabel(PowerSystemId system) => system switch
    {
        PowerSystemId.Oxygen => "O2",
        PowerSystemId.Engine => "Дв",
        PowerSystemId.Shields => "Щт",
        PowerSystemId.WeaponCharger => "Ор",
        PowerSystemId.Secondary => "Пр",
        _ => "?",
    };

    // Two separate things in two separate places: the periscope inside the room, which is what the
    // gunner walks up to and mans, and the gun itself out on the hull plating (TurretMount), whose
    // barrel is what the shell actually leaves through. Drawing the aim line from the console used
    // to imply the ship shot out of its own furniture.
    private void DrawTurret(SpriteBatch spriteBatch, Turret turret, TurretState? state,
        IReadOnlyList<Room> rooms, IReadOnlyList<Turret> allTurrets, Vector2 origin, bool showPeriscope = true)
    {
        var center = origin + new Vector2(turret.PeriscopeX, turret.PeriscopeY) * PixelsPerUnit;
        var manned = state?.MannedByPlayerId is not null;
        var damaged = state?.Damaged ?? false;

        // The crew station is inside the ship, so it goes with the rest of the interior when the
        // hull is drawn closed up.
        if (showPeriscope)
        {
            const int markerSize = 10;
            var markerColor = damaged ? Color.Red : manned ? Color.Gold : Color.Silver;
            spriteBatch.Draw(_pixel,
                new Rectangle((int)center.X - markerSize / 2, (int)center.Y - markerSize / 2, markerSize, markerSize),
                markerColor);

            if (damaged)
                spriteBatch.DrawString(_font, "!", center + new Vector2(8, -18), Color.Red, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
        }

        if (state is null)
            return;

        var mount = TurretMount.For(rooms, allTurrets, turret);
        var mountPx = origin + new Vector2(mount.Position.X, mount.Position.Y) * PixelsPerUnit;
        var rotation = mount.FireDegrees(state.AimDegrees) * (MathF.PI / 180f);
        var barrelColor = damaged ? Color.DarkRed : manned ? Color.Gold : Color.Silver;

        // Mount ring, then the barrel sticking out of it along the current aim. A manned gun is
        // drawn heavier: while you're behind the periscope this is the thing you're steering, and
        // it has to be findable at a glance against the ship's own plating.
        var ringSize = manned ? 22f : 16f;
        var barrelThickness = manned ? 10f : 7f;
        spriteBatch.Draw(_pixel, mountPx, null, barrelColor * 0.85f, 0f, new Vector2(0.5f, 0.5f), new Vector2(ringSize, ringSize), SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, mountPx, null, barrelColor, rotation, new Vector2(0f, 0.5f),
            new Vector2(TurretMount.BarrelLength * PixelsPerUnit, barrelThickness), SpriteEffects.None, 0f);

        if (!manned)
            return;

        // The gunner's aiming aids: the arc the barrel can actually cover, and a sight line running
        // out of the muzzle so it's obvious where a shell would go.
        DrawAimArcEdge(spriteBatch, mountPx, mount.FireDegrees(turret.MinAimDegrees));
        DrawAimArcEdge(spriteBatch, mountPx, mount.FireDegrees(turret.MaxAimDegrees));

        var muzzleLocal = mount.Muzzle(state.AimDegrees);
        var muzzle = origin + new Vector2(muzzleLocal.X, muzzleLocal.Y) * PixelsPerUnit;
        spriteBatch.Draw(_pixel, muzzle, null, Color.Gold * 0.45f, rotation, new Vector2(0f, 0.5f), new Vector2(900f, 2f), SpriteEffects.None, 0f);

        var readout = $"{state.AimDegrees:0}°";
        spriteBatch.DrawString(_font, readout, mountPx + new Vector2(-10, -30), Color.Gold, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    private void DrawAimArcEdge(SpriteBatch spriteBatch, Vector2 mountPx, float degrees)
    {
        var rotation = degrees * (MathF.PI / 180f);
        spriteBatch.Draw(_pixel, mountPx, null, Color.Gold * 0.18f, rotation, new Vector2(0f, 0.5f), new Vector2(420f, 2f), SpriteEffects.None, 0f);
    }

    // Green: open and passable. Dark red: closed and airtight (game_design.md Phase 3, M16).
    // leadsToVacuum darkens it further - an AirlockOuterDoor open to space rather than a room.
    // Hazard tape frames anything airtight-relevant (SS13/Barotrauma convention), thicker on the
    // ones leading to open vacuum since those are the ones that can actually kill you.
    // internal: reused by StationRenderer, which draws the station's own Rooms/Doors/Characters
    // through the exact same visual language instead of duplicating it.
    internal void DrawDoor(SpriteBatch spriteBatch, float left, float top, float width, float height, bool isOpen, Vector2 origin, bool leadsToVacuum = false)
    {
        var rect = GetDoorRect(left, top, width, height, origin);
        var color = isOpen ? (leadsToVacuum ? Color.MediumPurple : Color.SeaGreen) : Color.DarkRed;
        spriteBatch.Draw(_pixel, rect, color);

        var stripeThickness = leadsToVacuum ? 4 : 3;
        var horizontal = rect.Width >= rect.Height;
        if (horizontal)
        {
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Y, rect.Width, stripeThickness), horizontal: true);
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Bottom - stripeThickness, rect.Width, stripeThickness), horizontal: true);
        }
        else
        {
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Y, stripeThickness, rect.Height), horizontal: false);
            DrawHazardStripes(spriteBatch, new Rectangle(rect.Right - stripeThickness, rect.Y, stripeThickness, rect.Height), horizontal: false);
        }
    }

    // Tint scales with how low the room's oxygen actually is (game_design.md section 1 —
    // Barotrauma-style atmosphere) rather than a flat breached/not-breached flag: a single
    // holding-steady breach barely shows, a room actually suffocating goes visibly red. The floor
    // gets a paneled-grating pattern instead of a flat fill (this project has no image assets, so
    // the "texture" is drawn as a grid of seams rather than an actual sprite).
    // accentOverride: station rooms pass their own warm "commercial" accent here instead of the
    // per-room-id lookup below (StationRenderer.Draw) - everything else about the compartment
    // (grating, oxygen tint, name plate) stays exactly as it is on the ship, only the accent-tinted
    // decor (deck markings, light pool, wall lamps, corner fillets) shifts, which is enough for the
    // station to read as a different kind of place without forking this whole method.
    internal void DrawRoomFloor(SpriteBatch spriteBatch, Room room, float oxygen, Vector2 origin, Color? accentOverride = null)
    {
        var rect = GetRoomRect(room, origin);
        var accent = accentOverride ?? RoomDecor.Accent(room.Id);

        TileTextures.DrawTiled(spriteBatch, _floorPlate, TileTextures.FloorTileSize, rect, new Color(35, 40, 47));
        DrawFloorGrating(spriteBatch, rect);
        RoomDecor.DrawDeckMarkings(spriteBatch, _pixel, rect, accent);
        RoomDecor.DrawLightPool(spriteBatch, _pixel, rect, accent);
        RoomDecor.DrawFurniture(spriteBatch, _pixel, rect, room.Id, accent);

        var deficit = Math.Clamp((100f - oxygen) / 100f, 0f, 1f);
        if (deficit > 0f)
            spriteBatch.Draw(_pixel, rect, Color.Red * (deficit * 0.5f));

        // Compartment name on a painted plate in the department's own colour, the way a bulkhead is
        // actually stencilled - a bare label floating on the deck reads as a debug overlay.
        var plate = new Rectangle(rect.X + 8, rect.Y + 8, Math.Min(rect.Width - 16, 34 + room.Name.Length * 9), 20);
        spriteBatch.Draw(_pixel, plate, accent * 0.22f);
        spriteBatch.Draw(_pixel, new Rectangle(plate.X, plate.Y, 3, plate.Height), accent * 0.8f);
        spriteBatch.DrawString(_font, room.Name, new Vector2(rect.X + 14, rect.Y + 10), Color.LightSteelBlue, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        var oxygenColor = oxygen >= 50f ? Color.LightSteelBlue : oxygen >= 20f ? Color.Orange : Color.OrangeRed;
        spriteBatch.DrawString(_font, $"O2: {oxygen:0}", new Vector2(rect.X + 10, rect.Y + 30), oxygenColor, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
    }

    // Thick, plated bulkheads rather than a 3px outline: a slab centred on the room's boundary
    // (so two neighbouring rooms share one wall instead of stacking two), with a lit inner edge, a
    // shadowed outer one, ribs every RibSpacing pixels, a service conduit running down the middle
    // and bolted plates over the corners. VisibilityMask raycasts against that same boundary line,
    // so what blocks sight is exactly what's drawn here.
    internal void DrawRoomWalls(SpriteBatch spriteBatch, Room room, float oxygen, Vector2 origin, Color? accentOverride = null)
    {
        var rect = GetRoomRect(room, origin);
        var alarmed = oxygen < 70f;
        var accent = accentOverride ?? RoomDecor.Accent(room.Id);
        const int half = WallThickness / 2;

        // Rounded inside corners before the bulkheads themselves, so the wall slabs cover the seam
        // where the arc meets them.
        RoomDecor.DrawCornerFillets(spriteBatch, _pixel, rect, alarmed ? new Color(92, 60, 62) : new Color(70, 78, 90), 30f);
        RoomDecor.DrawWallLamps(spriteBatch, _pixel, rect, accent, alarmed);

        DrawWallBand(spriteBatch, new Rectangle(rect.X - half, rect.Y - half, rect.Width + WallThickness, WallThickness), true, alarmed);
        DrawWallBand(spriteBatch, new Rectangle(rect.X - half, rect.Bottom - half, rect.Width + WallThickness, WallThickness), true, alarmed);
        DrawWallBand(spriteBatch, new Rectangle(rect.X - half, rect.Y - half, WallThickness, rect.Height + WallThickness), false, alarmed);
        DrawWallBand(spriteBatch, new Rectangle(rect.Right - half, rect.Y - half, WallThickness, rect.Height + WallThickness), false, alarmed);

        DrawCornerPlate(spriteBatch, rect.X, rect.Y, alarmed);
        DrawCornerPlate(spriteBatch, rect.Right, rect.Y, alarmed);
        DrawCornerPlate(spriteBatch, rect.X, rect.Bottom, alarmed);
        DrawCornerPlate(spriteBatch, rect.Right, rect.Bottom, alarmed);
    }

    private void DrawWallBand(SpriteBatch spriteBatch, Rectangle band, bool horizontal, bool alarmed)
    {
        TileTextures.DrawTiled(spriteBatch, _wallPlate, TileTextures.WallTileSize, band,
            alarmed ? new Color(92, 60, 62) : new Color(70, 78, 90));
        var conduit = (alarmed ? Color.OrangeRed : Color.SteelBlue) * 0.45f;

        if (horizontal)
        {
            spriteBatch.Draw(_pixel, new Rectangle(band.X, band.Y, band.Width, 2), Color.White * 0.16f);
            spriteBatch.Draw(_pixel, new Rectangle(band.X, band.Bottom - 2, band.Width, 2), Color.Black * 0.5f);
            spriteBatch.Draw(_pixel, new Rectangle(band.X, band.Center.Y - 1, band.Width, 2), conduit);
            for (var x = band.X + RibSpacing / 2; x < band.Right; x += RibSpacing)
            {
                spriteBatch.Draw(_pixel, new Rectangle(x, band.Y, 2, band.Height), Color.Black * 0.45f);
                spriteBatch.Draw(_pixel, new Rectangle(x + 2, band.Y, 1, band.Height), Color.White * 0.12f);
            }
        }
        else
        {
            spriteBatch.Draw(_pixel, new Rectangle(band.X, band.Y, 2, band.Height), Color.White * 0.16f);
            spriteBatch.Draw(_pixel, new Rectangle(band.Right - 2, band.Y, 2, band.Height), Color.Black * 0.5f);
            spriteBatch.Draw(_pixel, new Rectangle(band.Center.X - 1, band.Y, 2, band.Height), conduit);
            for (var y = band.Y + RibSpacing / 2; y < band.Bottom; y += RibSpacing)
            {
                spriteBatch.Draw(_pixel, new Rectangle(band.X, y, band.Width, 2), Color.Black * 0.45f);
                spriteBatch.Draw(_pixel, new Rectangle(band.X, y + 2, band.Width, 1), Color.White * 0.12f);
            }
        }
    }

    private void DrawCornerPlate(SpriteBatch spriteBatch, int x, int y, bool alarmed)
    {
        const int size = WallThickness + 6;
        var rect = new Rectangle(x - size / 2, y - size / 2, size, size);
        TileTextures.DrawTiled(spriteBatch, _wallPlate, TileTextures.WallTileSize, rect,
            alarmed ? new Color(110, 70, 72) : new Color(88, 96, 110));
        DrawRectOutline(spriteBatch, rect, Color.Black * 0.45f, 1);
        DrawRivets(spriteBatch, rect);
    }

    private static Rectangle GetRoomRect(Room room, Vector2 origin) => new(
        (int)(origin.X + room.X * PixelsPerUnit),
        (int)(origin.Y + room.Y * PixelsPerUnit),
        (int)(room.Width * PixelsPerUnit),
        (int)(room.Height * PixelsPerUnit));

    private static float RoomOxygen(WorldSnapshot snapshot, string roomId) =>
        snapshot.RoomOxygen.FirstOrDefault(o => o.RoomId == roomId)?.Oxygen ?? 100f;

    // Panel-seam grid — the floor "texture": a grid of faint darker lines every 24px reads as
    // welded deck plating without needing an actual tileable sprite.
    private void DrawFloorGrating(SpriteBatch spriteBatch, Rectangle rect)
    {
        const int cell = 24;
        var seam = Color.Black * 0.25f;
        for (var x = rect.X + cell; x < rect.Right; x += cell)
            spriteBatch.Draw(_pixel, new Rectangle(x, rect.Y, 1, rect.Height), seam);
        for (var y = rect.Y + cell; y < rect.Bottom; y += cell)
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, y, rect.Width, 1), seam);
    }

    // Pulses via totalSeconds instead of a flat red square — reads as an active hazard light
    // rather than a static marker (SS13's breach warning strobe).
    // A real hole, not just a warning marker - sized to fully punch through the wall band's own
    // thickness (DrawRoomWalls' WallThickness) rather than sitting half-buried in it, filled with
    // dark space and a few fixed stars (seeded off the block's own position so they don't shimmer
    // frame to frame) so the plating genuinely reads as breached, with a thin pulsing hazard frame
    // around the opening for the "this is damage, not empty space" read at a glance.
    // Punches an actual hole rather than painting a fake starfield over it: this game has no
    // background starfield layer at all, and FieldRenderer draws every asteroid/ship/EVA character
    // unconditionally at its real position with no regard for the hull's own opacity (it only ever
    // reads as "outside the ship" because those things are normally positioned away from the hull's
    // footprint). FieldRenderer.Draw runs after this method in Game1's own draw order, so as long as
    // this leaves the block's own screen rect plain black (matching GraphicsDevice.Clear), whatever
    // FieldRenderer paints there next - a nearby asteroid, an enemy ship, empty void if nothing's
    // close - shows through exactly as it would look through a real gap in the plating, without any
    // extra portal/render-target machinery. Barotrauma's own breaches work the same way: they don't
    // fake the ocean, they just stop drawing the hull over it.
    private void DrawBreachedWallBlock(SpriteBatch spriteBatch, WallBlock block, Room room, Vector2 origin, float totalSeconds)
    {
        var center = origin + new Vector2(block.X, block.Y) * PixelsPerUnit;
        var onTopOrBottom = MathF.Abs(block.Y - room.Top) < 0.01f || MathF.Abs(block.Y - room.Bottom) < 0.01f;
        const int along = 40; // short of the full 48px block pitch, so adjacent holes still read as separate bites
        var width = onTopOrBottom ? along : WallThickness;
        var height = onTopOrBottom ? WallThickness : along;
        var rect = new Rectangle((int)center.X - width / 2, (int)center.Y - height / 2, width, height);

        spriteBatch.Draw(_pixel, rect, Color.Black);

        const int frame = 3;
        DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Y, rect.Width, frame), horizontal: onTopOrBottom);
        DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Bottom - frame, rect.Width, frame), horizontal: onTopOrBottom);
        DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Y, frame, rect.Height), horizontal: onTopOrBottom);
        DrawHazardStripes(spriteBatch, new Rectangle(rect.Right - frame, rect.Y, frame, rect.Height), horizontal: onTopOrBottom);

        var flicker = 0.5f + 0.5f * MathF.Sin(totalSeconds * 6f);
        DrawRectOutline(spriteBatch, rect, Color.OrangeRed * flicker, 1);
    }

    // Same black-backing + colour-scaled-by-fraction bar every other Hp readout in this project
    // uses (InventoryPanel.DrawChargeBar, FieldRenderer's ore deposit bar) - a wall's own Hp is
    // otherwise invisible, this is what a lit welder/cutter aimed at one reveals.
    // internal, called from Game1's HUD batch rather than from this class's own Draw() - the scene
    // batch it used to live in gets multiplied by the sight-cone/room-lighting mask
    // (BuildVisibilityMask), which hid the bar the instant the block itself fell into a blind spot;
    // the HUD batch is drawn after that composite, same exemption InfoPanel/CrewPanel already get.
    internal void DrawWallToolTargetBar(SpriteBatch spriteBatch, WallBlock block, WallBlockState state, Vector2 origin)
    {
        const int width = 32;
        const int height = 6;
        var center = origin + new Vector2(block.X, block.Y) * PixelsPerUnit;
        var bar = new Rectangle((int)center.X - width / 2, (int)center.Y - 22, width, height);
        var fill = state.Fraction > 0.6f ? Color.LimeGreen : state.Fraction > 0.25f ? Color.Orange : Color.OrangeRed;
        spriteBatch.Draw(_pixel, bar, Color.Black * 0.7f);
        spriteBatch.Draw(_pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * state.Fraction), bar.Height), fill);
        DrawRectOutline(spriteBatch, bar, Color.LightGray * 0.7f, 1);
    }

    // Out in front of the body along the way the character is facing - far enough out that a
    // held-item icon doesn't overlap it, the same distance a tool's flame is now drawn from
    // (DrawWeldingFlame/DrawCuttingFlame in ShipRenderer.Draw and FieldRenderer.Draw), so the beam
    // reads as coming out of the tool in hand rather than out of the character's chest.
    internal static Vector2 HeldToolOffset(Vector2 facing)
    {
        if (facing.LengthSquared() < 0.01f)
            facing = new Vector2(1f, 0f);
        else
            facing = Vector2.Normalize(facing);
        return facing * (CharacterDiameter * PixelsPerUnit / 2f + 10f);
    }

    internal static IReadOnlyList<ItemType> HeldItemTypes(InventoryState? inventory) =>
        inventory is null
            ? Array.Empty<ItemType>()
            : inventory.HeldMainSlotIndices.Select(i => inventory.MainSlots[i]).OfType<ItemType>().ToArray();

    // A held tool/item reads as a small coloured chip out in front of the body - the same colour and
    // 1-2 letter label InventoryPanel already shows it by in a slot, just without the slot frame
    // (this project has no item sprites to actually put in someone's hand). Two held items (a
    // two-handed tool's "both hands" or two one-handed ones) sit side by side rather than stacked.
    // internal + static, pixel/font passed explicitly, so FieldRenderer's own (simpler) EVA
    // DrawCharacter draws the exact same icon for a suited crewmate holding a cutter outside.
    private const int HeldIconSize = 30;

    internal static void DrawHeldItems(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font,
        IReadOnlyList<ItemType> held, Vector2 center, Vector2 facing)
    {
        var facingAngle = MathF.Atan2(facing.Y, facing.X);
        for (var i = 0; i < held.Count; i++)
            DrawHeldItemIcon(spriteBatch, pixel, font, held[i], HeldItemIconRect(held, i, center, facing), facingAngle);
    }

    // The chip rect a given held item's icon actually gets drawn into - broken out so the flame's
    // own origin (GetHeldToolMuzzle below) can be computed from this exact rect/rotation instead of
    // a separately-tuned guess that only approximately lines up with the texture.
    private static Rectangle HeldItemIconRect(IReadOnlyList<ItemType> held, int index, Vector2 center, Vector2 facing)
    {
        var offset = HeldToolOffset(facing);
        var side = new Vector2(-offset.Y, offset.X);
        if (side.LengthSquared() > 0.01f)
            side.Normalize();
        var lateral = held.Count <= 1 ? 0f : (index == 0 ? -1f : 1f) * (HeldIconSize * 0.65f);
        var pos = center + offset + side * lateral;
        return new Rectangle((int)pos.X - HeldIconSize / 2, (int)pos.Y - HeldIconSize / 2, HeldIconSize, HeldIconSize);
    }

    private static bool IsTopDownGunTool(ItemType item) => item is ItemType.WeldingTool or ItemType.Cutter;

    // Where a currently-held Cutter/WeldingTool's own drawn muzzle sits, so FieldRenderer's flame can
    // start exactly there instead of a generic offset off the character's centre - null if the
    // character isn't holding one (or is holding one but it's not the item this frame's Cutting/
    // Welding flag is actually about, which can't happen: only one of each is ever equipped at once).
    internal static Vector2? GetHeldToolMuzzle(ItemType tool, InventoryState? inventory, Vector2 center, Vector2 facing)
    {
        var held = HeldItemTypes(inventory);
        var index = -1;
        for (var i = 0; i < held.Count; i++)
            if (held[i] == tool) { index = i; break; }
        if (index < 0)
            return null;

        var rect = HeldItemIconRect(held, index, center, facing);
        var facingAngle = MathF.Atan2(facing.Y, facing.X);
        var rectCenter = new Vector2(rect.Center.X, rect.Center.Y);
        return ItemIcons.GetTopDownMuzzleWorldPosition(rectCenter, facingAngle, HeldIconSize);
    }

    private static void DrawHeldItemIcon(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, ItemType item, Rectangle rect, float rotation)
    {
        // Every held item reads as the thing itself, in the character's hand, with nothing drawn
        // around it - no backdrop chip, no hand glyphs (those still make sense in the hotbar, an
        // abstract "this slot is equipped" square, but not once the item has its own recognizable
        // silhouette to look at directly).
        if (IsTopDownGunTool(item))
        {
            // The cutter/welder get their own top-down silhouette (the same angle the character
            // itself is seen from) rather than the side-view hotbar icon.
            var rectCenter = new Vector2(rect.Center.X, rect.Center.Y);
            ItemIcons.DrawGunToolTopDown(spriteBatch, pixel, rectCenter, rotation, HeldIconSize, item);
            return;
        }

        const int margin = 2;
        if (ItemIcons.HasIcon(item))
        {
            // Turns to point where the character is actually facing, rather than always reading the
            // same way on screen - the same angle GetHeldToolMuzzle uses, so a gun tool's texture and
            // its flame never drift out of alignment with each other.
            ItemIcons.Draw(spriteBatch, pixel, item, new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2), rotation);
            return;
        }

        var pos = new Vector2(rect.Center.X, rect.Center.Y);
        spriteBatch.Draw(pixel, new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2), InventoryPanel.ItemColor(item));
        var label = ItemDefinitions.ShortLabel(item);
        if (label.Length == 0)
            return;
        var textSize = font.MeasureString(label) * 0.4f;
        spriteBatch.DrawString(font, label, pos - textSize / 2f, Color.White, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
    }

    // A rounded top-down read (shoulders around a head, not a flat square stack) rather than a bare
    // helmet-on-a-box - no sprite sheet, but a hip capsule, a shoulder capsule and a head circle
    // built from the same rect+HudIcons.FillCircle rounding every tool icon already uses reads as an
    // actual person from this camera height. FacingX/Y offsets the shoulders/head forward and picks
    // which side the (unlit) hip capsule trails on, plus a small bright nose on the head, so a
    // standing-still character still visibly has a front and a back.
    internal static void DrawHumanBody(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, int size, Color bodyColor, Color visorColor, Vector2 facing)
    {
        var perp = new Vector2(-facing.Y, facing.X);

        var hipColor = bodyColor * 0.72f;
        var hipCenter = center - facing * (size * 0.12f);

        // Feet: two small dark ovals peeking out behind the hips - the one part of a standing
        // figure that still shows past the torso when looking straight down at them, and drawn
        // first so the hip capsule covers where they join the legs.
        var footColor = new Color(28, 28, 32);
        var footBack = hipCenter - facing * (size * 0.15f);
        HudIcons.FillCircle(spriteBatch, pixel, footBack - perp * (size * 0.13f), size * 0.085f, footColor);
        HudIcons.FillCircle(spriteBatch, pixel, footBack + perp * (size * 0.13f), size * 0.085f, footColor);

        DrawCapsule(spriteBatch, pixel, hipCenter, perp, size * 0.62f, size * 0.40f, hipColor);

        // Arms: small capsules off both sides of the shoulders, drawn before the shoulder capsule
        // so it covers their inner ends and reads as a socket rather than two separate blobs.
        var shoulderCenter = center + facing * (size * 0.06f);
        var armColor = bodyColor * 0.85f;
        DrawCapsule(spriteBatch, pixel, shoulderCenter - perp * (size * 0.36f), facing, size * 0.22f, size * 0.15f, armColor);
        DrawCapsule(spriteBatch, pixel, shoulderCenter + perp * (size * 0.36f), facing, size * 0.22f, size * 0.15f, armColor);

        DrawCapsule(spriteBatch, pixel, shoulderCenter, perp, size * 0.92f, size * 0.48f, bodyColor);
        // A lit edge on the leading side, a shadowed one trailing - one flat tone reads as a cutout,
        // the same tone with a light on it reads as a body with some actual shape to it
        // (HullSkin.DrawPlateShading uses the same trick on the hull's own armour plates).
        var shoulderHighlight = shoulderCenter + facing * (size * 0.02f) - perp * (size * 0.14f);
        var shoulderShadow = shoulderCenter - facing * (size * 0.10f) + perp * (size * 0.18f);
        HudIcons.FillCircle(spriteBatch, pixel, shoulderHighlight, size * 0.14f, Color.White * 0.16f);
        HudIcons.FillCircle(spriteBatch, pixel, shoulderShadow, size * 0.14f, Color.Black * 0.16f);

        // Neck: a short, slightly darker band closing the gap between shoulders and head, so the
        // two don't read as one shape stacked on another with nothing between them.
        var neckCenter = center + facing * (size * 0.14f);
        HudIcons.FillCircle(spriteBatch, pixel, neckCenter, size * 0.15f, bodyColor * 0.8f);

        var headCenter = center + facing * (size * 0.22f);
        HudIcons.FillCircle(spriteBatch, pixel, headCenter, size * 0.30f, visorColor);
        // Helmet rim - a ring round the visor's own edge, so the head reads as a helmet with a
        // faceplate set into it rather than a flat coloured disc.
        HudIcons.DrawRingArc(spriteBatch, pixel, headCenter, size * 0.30f, 0f, 360f, bodyColor * 0.7f, 14, MathF.Max(1.4f, size * 0.05f));

        var noseCenter = headCenter + facing * (size * 0.20f);
        HudIcons.FillCircle(spriteBatch, pixel, noseCenter, MathF.Max(1.5f, size * 0.045f), Color.White);
    }

    // A rect with its two short ends rounded off - a rivet-free version of the same "bar plus end
    // circles" capsule ItemIcons builds tanks and handles from. `across` is the (already normalised)
    // direction the capsule's long axis runs; the rect is rotated to match it exactly (rather than
    // staying screen-aligned while only the end caps move), so a corner can never poke out past the
    // rounding at a diagonal facing.
    private static void DrawCapsule(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, Vector2 across, float width, float height, Color color)
    {
        var angle = MathF.Atan2(across.Y, across.X);
        spriteBatch.Draw(pixel, center, null, color, angle, new Vector2(0.5f, 0.5f), new Vector2(width, height), SpriteEffects.None, 0f);
        var capOffset = across * (width / 2f - height / 2f);
        HudIcons.FillCircle(spriteBatch, pixel, center - capOffset, height / 2f, color);
        HudIcons.FillCircle(spriteBatch, pixel, center + capOffset, height / 2f, color);
    }

    internal void DrawCharacter(SpriteBatch spriteBatch, CharacterState character, Vector2 origin)
    {
        var size = (int)(CharacterDiameter * PixelsPerUnit);
        var center = new Vector2(origin.X + character.X * PixelsPerUnit, origin.Y + character.Y * PixelsPerUnit);
        var rect = new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);

        var facing = new Vector2(character.FacingX, character.FacingY);
        if (facing.LengthSquared() > 0.01f)
            facing.Normalize();
        else
            facing = new Vector2(1f, 0f); // idle characters still need a direction to hold a tool toward

        // Hired crew (World.Recruiting.cs) reads as a body of a different colour, not another
        // anonymous crewmate - the point of hiring one is knowing it's there and doing its job.
        var bodyColor = character.IsBot ? Color.SteelBlue * 0.9f : Color.OrangeRed * 0.9f;
        var visorColor = character.WearingSuit ? Color.CadetBlue : new Color(255, 220, 190);
        DrawHumanBody(spriteBatch, _pixel, center, size, bodyColor, visorColor, facing);

        if (character.IsBot && character.Role is { } role)
            spriteBatch.DrawString(_font, $"{character.BotName} ({CrewRoles.Name(role)})", new Vector2(rect.X - 10, rect.Y - 14),
                Color.LightSkyBlue, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        // A human crewmate reads the same way a hired bot does - name floating over the head,
        // always on, not just when hovered - so telling a crew of several players apart doesn't
        // depend on remembering whose colour is whose. Once they've picked their own Role from
        // CrewPanel, it's shown in the name label too, the same way a bot's already is above.
        else if (!character.IsBot && character.Nickname is { Length: > 0 } nickname)
            spriteBatch.DrawString(_font, character.Role is { } playerRole ? $"{nickname} ({CrewRoles.Name(playerRole)})" : nickname,
                new Vector2(rect.X - 10, rect.Y - 14), Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);

        // The crew panel's role picker (CrewPanel.GetOwnRoleIconRect) is the only way a live
        // player's Role ever gets set - drawing the same glyph HudIcons.DrawRoleGlyph already
        // gives CrewPanel/InfoPanel rows here is what makes that choice visible in the ship view
        // itself, for a bot's fixed Role too since both read from the same field.
        if (character.Role is { } headRole)
            HudIcons.DrawRoleGlyph(spriteBatch, _pixel, new Vector2(center.X, rect.Y - 26), 0.5f,
                character.IsBot ? Color.LightSkyBlue : Color.White, headRole);

        DrawHeldItems(spriteBatch, _pixel, _font, HeldItemTypes(character.Inventory), center, facing);

        if (character.CarryingAmmoCrate)
        {
            const int crateSize = 8;
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - crateSize / 2, rect.Top - crateSize / 2, crateSize, crateSize), Color.SaddleBrown);
        }

        if (character.SuitActionRemaining > 0)
            spriteBatch.DrawString(_font, "...", new Vector2(rect.X, rect.Bottom + 2), Color.CadetBlue, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness) =>
        DrawRectOutline(spriteBatch, _pixel, rect, color, thickness);

    internal static void DrawRectOutline(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
