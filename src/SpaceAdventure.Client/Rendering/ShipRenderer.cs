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
    private readonly SpriteFont _font;

    public ShipRenderer(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _floorPlate = TileTextures.CreateFloorPlate(graphicsDevice);
        _wallPlate = TileTextures.CreateWallPlate(graphicsDevice);
        _font = font;
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

    // hullPlating: draw the ship closed up, seen from outside. That's the view from a turret
    // periscope - the gunner is looking along the plating at the field, and the decks, crew and
    // furniture behind that plating are not things they can see. Without it the ship reads as an
    // open floor plan floating in space while you're supposedly outside it.
    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, ClickTarget openBlock,
        float totalSeconds = 0f, IEnumerable<TransientEffect>? effects = null, bool hullPlating = false,
        IEnumerable<AtmosphereParticle>? atmosphere = null)
    {
        var forwardDegrees = ShipCatalog.ForwardDegrees(snapshot.CurrentShipKind);

        if (hullPlating)
        {
            HullSkin.Draw(spriteBatch, _pixel, snapshot.Rooms, snapshot.AirlockOuterDoors, snapshot.SystemDevices,
                origin, forwardDegrees, closedUp: true);
            foreach (var turret in snapshot.Turrets)
                DrawTurret(spriteBatch, turret, snapshot.TurretStates.FirstOrDefault(s => s.Id == turret.Id),
                    snapshot.Rooms, snapshot.Turrets, origin, showPeriscope: false);
            return;
        }

        // The armour the compartments sit inside, under everything else - what shows of it is the
        // plated border around the decks and the bow sticking out ahead of them.
        HullSkin.Draw(spriteBatch, _pixel, snapshot.Rooms, snapshot.AirlockOuterDoors, snapshot.SystemDevices,
            origin, forwardDegrees, closedUp: false);

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
            if (block is not null)
                DrawBreachedWallBlock(spriteBatch, block, origin, totalSeconds);
        }

        foreach (var storage in snapshot.AmmoStorages)
            DrawAmmoStorage(spriteBatch, storage, origin);

        foreach (var locker in snapshot.SuitLockers)
            DrawSuitLocker(spriteBatch, locker, origin);

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
        DrawAirlockConsole(spriteBatch, snapshot.AirlockConsole, snapshot.Voyage.Phase == VoyagePhase.Station, openBlock.Kind == BlockKind.Station, origin);
        for (var rackIndex = 0; rackIndex < snapshot.StorageRacks.Count; rackIndex++)
        {
            var rack = snapshot.StorageRacks[rackIndex];
            var isOpen = openBlock.Kind == BlockKind.Rack && openBlock.TargetComponentId == rack.Id;
            DrawStorageRack(spriteBatch, rack, rackIndex * StorageRack.Capacity, snapshot, isOpen, origin);
        }
        ComponentRenderer.Draw(spriteBatch, _pixel, _font, snapshot, origin, totalSeconds);
        var anyoneAtHelm = snapshot.Characters.Any(c => c.IsAtHelm);
        DrawHelmConsole(spriteBatch, snapshot.HelmConsole, anyoneAtHelm, origin);

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
            FieldRenderer.DrawCuttingFlame(spriteBatch, _pixel,
                origin + new Vector2(character.X, character.Y) * PixelsPerUnit + HeldToolOffset(facing),
                facing, totalSeconds);
        }

        foreach (var character in snapshot.Characters.Where(c => c.Welding && !c.IsOutside && !c.OnStation && !c.OnEnemyShip))
        {
            var facing = new Vector2(character.FacingX, character.FacingY);
            FieldRenderer.DrawWeldingFlame(spriteBatch, _pixel,
                origin + new Vector2(character.X, character.Y) * PixelsPerUnit + HeldToolOffset(facing),
                facing, totalSeconds);
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

    private void DrawSuitLocker(SpriteBatch spriteBatch, SuitLocker locker, Vector2 origin)
    {
        const int size = 16;
        var center = origin + new Vector2(locker.X, locker.Y) * PixelsPerUnit;
        var rect = new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);
        DrawPanel(spriteBatch, rect, Color.CadetBlue * 0.7f, Color.CadetBlue, 1);
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

    // Airlock in the corridor (game_design.md section 10) — only actually usable while docked;
    // dims when the ship isn't at a station so it doesn't look clickable when it can't be.
    private void DrawAirlockConsole(SpriteBatch spriteBatch, AirlockConsole console, bool usable, bool isOpen, Vector2 origin)
    {
        var rect = GetBlockRect(console.Position, MediumBlockSize, origin);
        DrawPanel(spriteBatch, rect, (usable ? Color.SeaGreen : Color.DimGray) * 0.6f, isOpen ? Color.Gold : usable ? Color.LightGreen : Color.Gray, isOpen ? 3 : 2);
        spriteBatch.DrawString(_font, "Шлюз", new Vector2(rect.X + 1, rect.Y + 7), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    // Pilot's console (game_design.md Phase 3, M15) — click it to man it and bring up the helm's
    // joystick panel instead of the ship view.
    private void DrawHelmConsole(SpriteBatch spriteBatch, HelmConsole console, bool isOpen, Vector2 origin)
    {
        var rect = GetBlockRect(console.Position, MediumBlockSize, origin);
        DrawPanel(spriteBatch, rect, Color.DarkGoldenrod * 0.6f, isOpen ? Color.Gold : Color.Goldenrod, isOpen ? 3 : 2);
        spriteBatch.DrawString(_font, "Штурв", new Vector2(rect.X + 1, rect.Y + 7), Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
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
    internal void DrawRoomFloor(SpriteBatch spriteBatch, Room room, float oxygen, Vector2 origin)
    {
        var rect = GetRoomRect(room, origin);
        var accent = RoomDecor.Accent(room.Id);

        TileTextures.DrawTiled(spriteBatch, _floorPlate, TileTextures.FloorTileSize, rect, new Color(35, 40, 47));
        DrawFloorGrating(spriteBatch, rect);
        RoomDecor.DrawDeckMarkings(spriteBatch, _pixel, rect, accent);
        RoomDecor.DrawLightPool(spriteBatch, _pixel, rect, accent);

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
    internal void DrawRoomWalls(SpriteBatch spriteBatch, Room room, float oxygen, Vector2 origin)
    {
        var rect = GetRoomRect(room, origin);
        var alarmed = oxygen < 70f;
        var accent = RoomDecor.Accent(room.Id);
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
    private void DrawBreachedWallBlock(SpriteBatch spriteBatch, WallBlock block, Vector2 origin, float totalSeconds)
    {
        const int size = 14;
        var center = origin + new Vector2(block.X, block.Y) * PixelsPerUnit;
        var rect = new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);
        var flicker = 0.55f + 0.45f * MathF.Sin(totalSeconds * 6f);
        DrawHazardStripes(spriteBatch, rect, horizontal: true);
        spriteBatch.Draw(_pixel, rect, Color.Red * (flicker * 0.6f));
        spriteBatch.DrawString(_font, "!", center + new Vector2(-3, -18), Color.Red, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
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
    internal static void DrawHeldItems(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font,
        IReadOnlyList<ItemType> held, Vector2 center, Vector2 facing)
    {
        if (held.Count == 0)
            return;

        var offset = HeldToolOffset(facing);
        var side = new Vector2(-offset.Y, offset.X);
        if (side.LengthSquared() > 0.01f)
            side.Normalize();

        const int iconSize = 26;
        for (var i = 0; i < held.Count; i++)
        {
            var lateral = held.Count == 1 ? 0f : (i == 0 ? -1f : 1f) * (iconSize * 0.65f);
            DrawHeldItemIcon(spriteBatch, pixel, font, held[i], center + offset + side * lateral, iconSize);
        }
    }

    private static void DrawHeldItemIcon(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, ItemType item, Vector2 pos, int size)
    {
        var rect = new Rectangle((int)pos.X - size / 2, (int)pos.Y - size / 2, size, size);
        spriteBatch.Draw(pixel, rect, Color.Black * 0.35f);
        DrawRectOutline(spriteBatch, pixel, rect, Color.Black * 0.6f, 1);

        // Gripping it, not just floating beside it - drawn under the item itself so the fingers read
        // as wrapped around it rather than stamped on top (InventoryPanel.DrawHands - the same hands
        // the hotbar shows once an item is actually in hand, always lit here since this chip only
        // ever draws for something that is).
        InventoryPanel.DrawHands(spriteBatch, pixel, rect, ItemDefinitions.HandsRequired(item), held: true);

        const int margin = 2;
        if (ItemIcons.HasIcon(item))
        {
            ItemIcons.Draw(spriteBatch, pixel, item, new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2));
            return;
        }

        spriteBatch.Draw(pixel, new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2), InventoryPanel.ItemColor(item));
        var label = ItemDefinitions.ShortLabel(item);
        if (label.Length == 0)
            return;
        var textSize = font.MeasureString(label) * 0.4f;
        spriteBatch.DrawString(font, label, pos - textSize / 2f, Color.White, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
    }

    // Simple humanoid read (helmet + torso) rather than a flat square — no sprite sheet, but a
    // second smaller square as a "helmet" plus a facing notch is enough to read as a person from
    // this camera height. FacingX/Y drives the notch so idle characters still show which way
    // they're looking.
    internal void DrawCharacter(SpriteBatch spriteBatch, CharacterState character, Vector2 origin)
    {
        var size = (int)(CharacterDiameter * PixelsPerUnit);
        var center = new Vector2(origin.X + character.X * PixelsPerUnit, origin.Y + character.Y * PixelsPerUnit);
        var rect = new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);

        if (character.WearingSuit)
        {
            const int ringMargin = 3;
            DrawRectOutline(spriteBatch, new Rectangle(rect.X - ringMargin, rect.Y - ringMargin, rect.Width + ringMargin * 2, rect.Height + ringMargin * 2), Color.CadetBlue, 2);
        }

        // Hired crew (World.Recruiting.cs) reads as a body of a different colour, not another
        // anonymous crewmate - the point of hiring one is knowing it's there and doing its job.
        spriteBatch.Draw(_pixel, rect, character.IsBot ? Color.SteelBlue * 0.9f : Color.OrangeRed * 0.9f);
        // "Helmet": a smaller, lighter square centered on the body reads as a head/visor.
        var helmetSize = Math.Max(4, size / 2);
        var visorColor = character.WearingSuit ? Color.CadetBlue : new Color(255, 220, 190);
        spriteBatch.Draw(_pixel, new Rectangle((int)center.X - helmetSize / 2, (int)center.Y - helmetSize / 2, helmetSize, helmetSize), visorColor);

        if (character.IsBot && character.Role is { } role)
            spriteBatch.DrawString(_font, $"{character.BotName} ({CrewRoles.Name(role)})", new Vector2(rect.X - 10, rect.Y - 14),
                Color.LightSkyBlue, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        // A human crewmate reads the same way a hired bot does - name floating over the head,
        // always on, not just when hovered - so telling a crew of several players apart doesn't
        // depend on remembering whose colour is whose.
        else if (!character.IsBot && character.Nickname is { Length: > 0 } nickname)
            spriteBatch.DrawString(_font, nickname, new Vector2(rect.X - 10, rect.Y - 14),
                Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);

        // Facing notch: a tiny bright square nudged toward FacingX/Y, off the body's edge.
        var facing = new Vector2(character.FacingX, character.FacingY);
        if (facing.LengthSquared() > 0.01f)
        {
            facing.Normalize();
            const int notchSize = 3;
            var notchCenter = center + facing * (size / 2f + 1);
            spriteBatch.Draw(_pixel, new Rectangle((int)notchCenter.X - notchSize / 2, (int)notchCenter.Y - notchSize / 2, notchSize, notchSize), Color.White);
        }
        else
        {
            facing = new Vector2(1f, 0f); // idle characters still need a direction to hold a tool toward
        }

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
