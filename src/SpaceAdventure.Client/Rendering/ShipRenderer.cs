using System;
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

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public ShipRenderer(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    // Shared by Draw() and by Game1's mouse hit-testing so click regions always match what's
    // actually rendered.
    public static Rectangle GetBlockRect(Vec2 worldPosition, int size, Vector2 origin)
    {
        var center = origin + new Vector2(worldPosition.X, worldPosition.Y) * PixelsPerUnit;
        return new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, ClickTarget openBlock)
    {
        foreach (var room in snapshot.Rooms)
        {
            var oxygen = snapshot.RoomOxygen.FirstOrDefault(o => o.RoomId == room.Id)?.Oxygen ?? 100f;
            DrawRoom(spriteBatch, room, oxygen, origin);
        }

        // Drawn after room outlines so the opening visibly cuts through the shared wall.
        foreach (var door in snapshot.Doors)
            DrawDoor(spriteBatch, door, origin);

        // Only breached blocks get drawn — an intact one is just an ordinary bit of the hull
        // the room outline already implies.
        foreach (var state in snapshot.WallBlockStates)
        {
            if (!state.Breached)
                continue;
            var block = snapshot.WallBlocks.FirstOrDefault(b => b.Id == state.Id);
            if (block is not null)
                DrawBreachedWallBlock(spriteBatch, block, origin);
        }

        foreach (var storage in snapshot.AmmoStorages)
            DrawAmmoStorage(spriteBatch, storage, origin);

        foreach (var locker in snapshot.SuitLockers)
            DrawSuitLocker(spriteBatch, locker, origin);

        foreach (var station in snapshot.ToolStations)
            DrawToolStation(spriteBatch, station, origin);

        foreach (var device in snapshot.SystemDevices)
        {
            var damaged = snapshot.SystemStates.FirstOrDefault(s => s.System == device.System)?.Damaged ?? false;
            var isOpen = openBlock.Kind == BlockKind.System && openBlock.System == device.System;
            var size = device.System == PowerSystemId.Engine ? BigBlockSize : NormalBlockSize;
            DrawSystemDevice(spriteBatch, device, damaged, isOpen, size, origin);
        }

        DrawReactorBlock(spriteBatch, snapshot.ReactorBlock, snapshot.Reactor, openBlock.Kind == BlockKind.Reactor, origin);
        DrawDistributionBlock(spriteBatch, snapshot.DistributionBlock, openBlock.Kind == BlockKind.Distribution, origin);
        DrawNavigationConsole(spriteBatch, snapshot.NavigationConsole, openBlock.Kind == BlockKind.Navigation, origin);
        DrawAirlockConsole(spriteBatch, snapshot.AirlockConsole, snapshot.Voyage.Phase == VoyagePhase.Station, openBlock.Kind == BlockKind.Station, origin);

        foreach (var turret in snapshot.Turrets)
        {
            var state = snapshot.TurretStates.FirstOrDefault(s => s.Id == turret.Id);
            DrawTurret(spriteBatch, turret, state, origin);
        }

        foreach (var character in snapshot.Characters)
            DrawCharacter(spriteBatch, character, origin);
    }

    private void DrawAmmoStorage(SpriteBatch spriteBatch, AmmoStorage storage, Vector2 origin)
    {
        const int size = 14;
        var center = origin + new Vector2(storage.X, storage.Y) * PixelsPerUnit;
        spriteBatch.Draw(_pixel, new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size), Color.SaddleBrown);
    }

    private void DrawSuitLocker(SpriteBatch spriteBatch, SuitLocker locker, Vector2 origin)
    {
        const int size = 14;
        var center = origin + new Vector2(locker.X, locker.Y) * PixelsPerUnit;
        spriteBatch.Draw(_pixel, new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size), Color.CadetBlue);
    }

    private void DrawToolStation(SpriteBatch spriteBatch, ToolStation station, Vector2 origin)
    {
        const int size = 14;
        var center = origin + new Vector2(station.X, station.Y) * PixelsPerUnit;
        var isWeapon = station.Item is ItemType.Knife or ItemType.Rifle or ItemType.LaserRifle;
        var color = isWeapon ? Color.DarkRed : Color.DarkKhaki;
        spriteBatch.Draw(_pixel, new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size), color);
        spriteBatch.DrawString(_font, ItemDefinitions.ShortLabel(station.Item), center + new Vector2(9, -8), color, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    // Physical, damageable system block (game_design.md section 1) — click it to see its
    // readout. Bigger than the item/tool markers so it reads as ship equipment; Engine gets the
    // "big" tier like the reactor (see Draw()).
    private void DrawSystemDevice(SpriteBatch spriteBatch, ShipSystemDevice device, bool damaged, bool isOpen, int size, Vector2 origin)
    {
        var rect = GetBlockRect(device.Position, size, origin);
        var center = new Vector2(rect.Center.X, rect.Center.Y);

        spriteBatch.Draw(_pixel, rect, damaged ? Color.Red * 0.6f : Color.SlateGray * 0.8f);
        DrawRectOutline(spriteBatch, rect, damaged ? Color.Red : isOpen ? Color.Gold : Color.LightSteelBlue, isOpen ? 3 : 2);
        spriteBatch.DrawString(_font, SystemShortLabel(device.System), new Vector2(rect.X + 2, rect.Y + 3), Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        if (damaged)
            spriteBatch.DrawString(_font, "!", center + new Vector2(size / 2f - 2, -size), Color.Red, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
    }

    // Big, clickable — walking up and clicking it "enters" the reactor and shows its 4 fuel-rod
    // slots (see ReactorPanel). Glows warmer the more rods are loaded.
    private void DrawReactorBlock(SpriteBatch spriteBatch, ReactorBlock block, ReactorState reactor, bool isOpen, Vector2 origin)
    {
        var rect = GetBlockRect(block.Position, BigBlockSize, origin);
        var running = reactor.CurrentOutput > 0;
        spriteBatch.Draw(_pixel, rect, running ? Color.DarkOrange * 0.55f : Color.DimGray * 0.6f);
        DrawRectOutline(spriteBatch, rect, isOpen ? Color.Gold : running ? Color.Orange : Color.Gray, isOpen ? 3 : 2);
        spriteBatch.DrawString(_font, "Реактор", new Vector2(rect.X + 2, rect.Y + 4), Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    // Medium — bigger than a plain system block, smaller than the reactor/engine (as requested).
    private void DrawDistributionBlock(SpriteBatch spriteBatch, PowerDistributionBlock block, bool isOpen, Vector2 origin)
    {
        var rect = GetBlockRect(block.Position, MediumBlockSize, origin);
        spriteBatch.Draw(_pixel, rect, Color.MediumPurple * 0.6f);
        DrawRectOutline(spriteBatch, rect, isOpen ? Color.Gold : Color.Plum, isOpen ? 3 : 2);
        spriteBatch.DrawString(_font, "Э", new Vector2(rect.X + 6, rect.Y + 6), Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    // Bridge console (game_design.md section 5) — click it to bring up the galaxy map.
    private void DrawNavigationConsole(SpriteBatch spriteBatch, NavigationConsole console, bool isOpen, Vector2 origin)
    {
        var rect = GetBlockRect(console.Position, MediumBlockSize, origin);
        spriteBatch.Draw(_pixel, rect, Color.Teal * 0.6f);
        DrawRectOutline(spriteBatch, rect, isOpen ? Color.Gold : Color.LightSeaGreen, isOpen ? 3 : 2);
        spriteBatch.DrawString(_font, "Карта", new Vector2(rect.X + 1, rect.Y + 7), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    // Airlock in the corridor (game_design.md section 10) — only actually usable while docked;
    // dims when the ship isn't at a station so it doesn't look clickable when it can't be.
    private void DrawAirlockConsole(SpriteBatch spriteBatch, AirlockConsole console, bool usable, bool isOpen, Vector2 origin)
    {
        var rect = GetBlockRect(console.Position, MediumBlockSize, origin);
        spriteBatch.Draw(_pixel, rect, (usable ? Color.SeaGreen : Color.DimGray) * 0.6f);
        DrawRectOutline(spriteBatch, rect, isOpen ? Color.Gold : usable ? Color.LightGreen : Color.Gray, isOpen ? 3 : 2);
        spriteBatch.DrawString(_font, "Шлюз", new Vector2(rect.X + 1, rect.Y + 7), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
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

    private void DrawTurret(SpriteBatch spriteBatch, Turret turret, TurretState? state, Vector2 origin)
    {
        var center = origin + new Vector2(turret.PeriscopeX, turret.PeriscopeY) * PixelsPerUnit;
        var manned = state?.MannedByPlayerId is not null;
        var damaged = state?.Damaged ?? false;

        const int markerSize = 10;
        var markerColor = damaged ? Color.Red : manned ? Color.Gold : Color.Silver;
        spriteBatch.Draw(_pixel,
            new Rectangle((int)center.X - markerSize / 2, (int)center.Y - markerSize / 2, markerSize, markerSize),
            markerColor);

        if (damaged)
            spriteBatch.DrawString(_font, "!", center + new Vector2(8, -18), Color.Red, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);

        if (state is null)
            return;

        // 0 degrees points toward the bow (-X); positive degrees rotate toward -Y.
        var angleRad = state.AimDegrees * MathF.PI / 180f;
        var direction = new Vector2(-MathF.Cos(angleRad), MathF.Sin(angleRad));
        var rotation = MathF.Atan2(direction.Y, direction.X);

        const float lineLengthPx = 70f;
        spriteBatch.Draw(_pixel, center, null, Color.Gold, rotation, Vector2.Zero, new Vector2(lineLengthPx, 3f), SpriteEffects.None, 0f);
    }

    private void DrawDoor(SpriteBatch spriteBatch, Door door, Vector2 origin)
    {
        var rect = new Rectangle(
            (int)(origin.X + door.Left * PixelsPerUnit),
            (int)(origin.Y + door.Top * PixelsPerUnit),
            (int)(door.Width * PixelsPerUnit),
            (int)(door.Height * PixelsPerUnit));

        spriteBatch.Draw(_pixel, rect, Color.SeaGreen);
    }

    // Tint scales with how low the room's oxygen actually is (game_design.md section 1 —
    // Barotrauma-style atmosphere) rather than a flat breached/not-breached flag: a single
    // holding-steady breach barely shows, a room actually suffocating goes visibly red.
    private void DrawRoom(SpriteBatch spriteBatch, Room room, float oxygen, Vector2 origin)
    {
        var rect = new Rectangle(
            (int)(origin.X + room.X * PixelsPerUnit),
            (int)(origin.Y + room.Y * PixelsPerUnit),
            (int)(room.Width * PixelsPerUnit),
            (int)(room.Height * PixelsPerUnit));

        var deficit = Math.Clamp((100f - oxygen) / 100f, 0f, 1f);
        if (deficit > 0f)
            spriteBatch.Draw(_pixel, rect, Color.Red * (deficit * 0.5f));

        DrawRectOutline(spriteBatch, rect, deficit > 0.3f ? Color.Red : Color.SteelBlue, 2);
        spriteBatch.DrawString(_font, room.Name, new Vector2(rect.X + 6, rect.Y + 6), Color.LightSteelBlue, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        var oxygenColor = oxygen >= 50f ? Color.LightSteelBlue : oxygen >= 20f ? Color.Orange : Color.OrangeRed;
        spriteBatch.DrawString(_font, $"O2: {oxygen:0}", new Vector2(rect.X + 6, rect.Y + 26), oxygenColor, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
    }

    private void DrawBreachedWallBlock(SpriteBatch spriteBatch, WallBlock block, Vector2 origin)
    {
        const int size = 12;
        var center = origin + new Vector2(block.X, block.Y) * PixelsPerUnit;
        var rect = new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);
        spriteBatch.Draw(_pixel, rect, Color.Red);
        spriteBatch.DrawString(_font, "!", center + new Vector2(-3, -18), Color.Red, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
    }

    private void DrawCharacter(SpriteBatch spriteBatch, CharacterState character, Vector2 origin)
    {
        var size = (int)(CharacterDiameter * PixelsPerUnit);
        var rect = new Rectangle(
            (int)(origin.X + character.X * PixelsPerUnit) - size / 2,
            (int)(origin.Y + character.Y * PixelsPerUnit) - size / 2,
            size, size);

        if (character.WearingSuit)
        {
            const int ringMargin = 3;
            DrawRectOutline(spriteBatch, new Rectangle(rect.X - ringMargin, rect.Y - ringMargin, rect.Width + ringMargin * 2, rect.Height + ringMargin * 2), Color.CadetBlue, 2);
        }

        spriteBatch.Draw(_pixel, rect, Color.OrangeRed);

        if (character.CarryingAmmoCrate)
        {
            const int crateSize = 8;
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - crateSize / 2, rect.Top - crateSize / 2, crateSize, crateSize), Color.SaddleBrown);
        }

        if (character.SuitActionRemaining > 0)
            spriteBatch.DrawString(_font, "...", new Vector2(rect.X, rect.Bottom + 2), Color.CadetBlue, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
