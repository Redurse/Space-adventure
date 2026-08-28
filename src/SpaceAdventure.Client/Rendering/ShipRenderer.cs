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
    // Doubled on request, from 1.0.
    //
    // Worth knowing what this crosses: the collision clearance is RoomLayout.CharacterRadius, 0.7
    // units or 33.6 pixels, and that has not moved. At a drawn diameter of 2.0 the figure's own
    // half-width is 48 pixels against those 33.6, so a crewman standing against a bulkhead now
    // overlaps it by about fourteen pixels instead of clearing it. Purely a drawing matter - where
    // anyone can walk is unchanged - but if the overlap reads badly the fix is the collision
    // radius, not this number.
    internal const float CharacterDiameter = 2.0f; // world units - the footprint labels sit against

    // How tall the drawn figure is, separate from the footprint above because the sprite is a person
    // standing up rather than a disc seen from overhead.
    //
    // Halved from 2.5 on request: sixty pixels tall.
    //
    // Worth knowing, because it costs something: the figure is forty art rows, so sixty pixels is
    // one and a half screen pixels per row - not a whole number, so the single-pixel details (the
    // eyes, the seams between the limbs) land between pixels and soften. The nearest sizes that
    // stay perfectly crisp are 80px, which is two rows to the pixel, and 40px, which is one. This
    // is the size that was asked for; 80 is the one notch up that stays sharp.
    internal const float CharacterHeight = 1.25f;

    // Size tiers requested for the power grid blocks: reactor/engine read as the biggest,
    // fixed installations; the distribution block is noticeably bigger than a plain system
    // block but still smaller than those two.
    public const int NormalBlockSize = 24;
    public const int MediumBlockSize = 32;
    public const int BigBlockSize = 40;

    // Bulkhead slab, in screen pixels, centred on the room boundary. Deliberately narrower than a
    // door's 1-unit (48px) span so a doorway still cuts cleanly through it, and narrower than twice
    // RoomLayout.CharacterRadius (33.6px) so a character stopped at the collision clearance never
    // still pokes out past the wall's outer face.
    internal const int WallThickness = 28;
    private const int RibSpacing = 26;

    private readonly Texture2D _pixel;
    private readonly Texture2D _wallPlate;
    private readonly Texture2D[] _hullPlates;
    private readonly Texture2D _devicePlate;
    private readonly Texture2D _floorNormals;
    private readonly Texture2D _hullNormals;
    private readonly Texture2D _faceShade;
    private readonly SpriteFont _font;
    private readonly Starfield _starfield;

    // worldViewport: the same rect Game1's WorldViewportOrigin/WorldViewportSize describe - passed
    // in rather than duplicated here so the starfield always fills exactly the area the ship is
    // actually drawn into, not a guess at it.
    private readonly DeviceSkin _deviceSkin;
    private readonly TurretSkin _turretSkin;

    // One set of plates per deck kind, baked at load. Three kinds times six variants is eighteen
    // 48px textures, which is nothing, and it saves the whole floor being generated per frame.
    private readonly Dictionary<DeckPlates.Deck, Texture2D[]> _deckPlates = new();
    private readonly Texture2D _deckGrime;
    private readonly CrewSkin _crewSkin;

    public ShipRenderer(GraphicsDevice graphicsDevice, SpriteFont font, Rectangle worldViewport)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _wallPlate = TileTextures.CreateWallPlate(graphicsDevice);
        _hullPlates = TileTextures.CreateHullPlates(graphicsDevice);
        _devicePlate = TileTextures.CreateDevicePlate(graphicsDevice);
        _floorNormals = TileTextures.CreateFloorNormals(graphicsDevice);
        _hullNormals = TileTextures.CreateHullNormals(graphicsDevice);
        _faceShade = TileTextures.CreateFaceShade(graphicsDevice);
        _deviceSkin = new DeviceSkin(graphicsDevice);
        _turretSkin = new TurretSkin(graphicsDevice);
        foreach (var deck in Enum.GetValues<DeckPlates.Deck>())
            _deckPlates[deck] = DeckPlates.Create(graphicsDevice, deck);
        _deckGrime = DeckPlates.CreateGrime(graphicsDevice);
        _crewSkin = new CrewSkin(graphicsDevice);
        _font = font;
        _starfield = new Starfield(_pixel, worldViewport);
    }

    // Shared by Draw() and by Game1's mouse hit-testing so click regions always match what's
    // actually rendered.
    public static Rectangle GetBlockRect(Vec2 worldPosition, int size, Vector2 origin)
    {
        // Truncated the same way GetRoomRect is, and for the same reason: rounding the sum lets a
        // device drift a pixel back and forth against the deck it is bolted to as the camera moves.
        var centerX = (int)origin.X + (int)(worldPosition.X * PixelsPerUnit);
        var centerY = (int)origin.Y + (int)(worldPosition.Y * PixelsPerUnit);
        return new Rectangle(centerX - size / 2, centerY - size / 2, size, size);
    }

    // The reactor's 3 physical levers (light / reactor power / door lock — ReactorLeverState),
    // stacked down its left flank just outside the main housing rect, same "shared by drawing and
    // hit-testing" convention as GetBlockRect above.
    public static Rectangle GetReactorLeverRect(int index, ReactorBlock block, Vector2 origin)
    {
        var size = (int)(BigBlockSize * block.SizeScale);
        var rect = GetBlockRect(block.Position, size, origin);
        var leverWidth = Math.Max(10, size / 4);
        var leverHeight = Math.Max(8, size / 5);
        var gap = Math.Max(2, size / 14);
        var totalHeight = leverHeight * 3 + gap * 2;
        var startY = rect.Center.Y - totalHeight / 2;
        var x = rect.X - leverWidth - 2;
        var y = startY + index * (leverHeight + gap);
        return new Rectangle(x, y, leverWidth, leverHeight);
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
        var forwardDegrees = snapshot.ShipForwardDegrees;

        // Space itself, under absolutely everything - fixed to the screen (not translated by
        // origin) so it reads as an infinitely distant backdrop rather than a world object the
        // camera pans across. The drift fed into it is the ship's own real travelled distance
        // (ShipField.X/Y, server-authoritative), rotated into the ship's local/screen frame the
        // same way EVA aiming already is (ShipLocalFrame) - so background stars actually slide by
        // in step with real flight instead of a fake ambient scroll, and hold still the instant the
        // ship does.
        var travelled = ShipLocalFrame.ToLocalDirection(new Vec2(snapshot.ShipField.X, snapshot.ShipField.Y), snapshot.ShipField.RotationDegrees);
        var starDrift = new Vector2((float)travelled.X, (float)travelled.Y) * PixelsPerUnit;
        _starfield.Draw(spriteBatch, totalSeconds, starDrift);

        // The armour the compartments sit inside, under everything else - what shows of it is the
        // plated border around the decks and the bow sticking out ahead of them.
        HullSkin.Draw(spriteBatch, _pixel, _hullPlates, snapshot.Rooms, snapshot.AirlockOuterDoors, snapshot.SystemDevices,
            origin, forwardDegrees, snapshot.CurrentShipKind, totalSeconds, snapshot.SystemStates);

        // Floors first, walls second: the bulkheads are thick and straddle the boundary between
        // two rooms, so a room drawn later would otherwise paint its floor over its neighbour's
        // wall slab.
        foreach (var room in snapshot.Rooms)
            DrawRoomFloor(spriteBatch, room, RoomOxygen(snapshot, room.Id), origin);
        foreach (var room in snapshot.Rooms)
            DrawRoomWalls(spriteBatch, room, RoomOxygen(snapshot, room.Id), origin);

        // A frame over the metal plus a plain unpainted pane, only for the crew station that
        // actually faces open space - deliberately left blank rather than filled with any painted
        // backdrop, same trick DrawBreachedWallBlock already uses for a hull breach: FieldRenderer
        // draws every asteroid/ship/EVA character at its own real position after this (Game1's own
        // draw order), so whatever is actually out there - or nothing, just black - shows through
        // exactly as it would through a real pane of glass, with no separate starfield of its own
        // to keep in sync.
        foreach (var pane in CockpitWindows.Panes(snapshot.Rooms))
            DrawWindowPane(spriteBatch, pane, origin);

        // Drawn after room outlines so the opening visibly cuts through the shared wall.
        foreach (var door in snapshot.Doors)
        {
            var state = snapshot.DoorStates.FirstOrDefault(s => s.DoorId == door.Id);
            DrawDoor(spriteBatch, door.Left, door.Top, door.Width, door.Height, state?.IsOpen ?? true, origin,
                destroyed: state?.Destroyed ?? false, totalSeconds: totalSeconds);
        }

        foreach (var outerDoor in snapshot.AirlockOuterDoors)
        {
            var state = snapshot.DoorStates.FirstOrDefault(s => s.DoorId == outerDoor.Id);
            DrawDoor(spriteBatch, outerDoor.Left, outerDoor.Top, outerDoor.Width, outerDoor.Height, state?.IsOpen ?? false, origin,
                leadsToVacuum: true, destroyed: state?.Destroyed ?? false, totalSeconds: totalSeconds);
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

        // M62 - a room under construction (World.ShipBuilding.cs's StepRoomBuilds) isn't part of
        // Rooms above yet, so it needs its own draw pass - a translucent outline over whatever's
        // already there (open space through a window, or genuinely nothing) plus a progress
        // readout, the plan's own "не проходима, не герметична, не запитана" made visible.
        if (snapshot.PendingRoomBuilds is { Count: > 0 } pendingBuilds)
            foreach (var pending in pendingBuilds)
                DrawPendingRoomBuild(spriteBatch, pending, origin);

        foreach (var storage in snapshot.AmmoStorages)
        {
            var stock = snapshot.AmmoStorageStates.FirstOrDefault(s => s.StorageId == storage.Id);
            DrawAmmoStorage(spriteBatch, storage, stock?.Remaining ?? 0, origin);
        }

        foreach (var locker in snapshot.SuitLockers)
        {
            var hasSuit = snapshot.SuitLockerStates.FirstOrDefault(s => s.LockerId == locker.Id)?.HasSuit ?? false;
            DrawSuitLocker(spriteBatch, locker, origin, hasSuit);
        }

        DrawDroppedItems(spriteBatch, snapshot.DroppedItems, snapshot.Rooms.Select(r => r.Id), origin, totalSeconds);

        // A console is dark when the ship cannot power it: reactor down and batteries flat. Room
        // lighting dims the whole compartment separately (RoomLighting) - this is the device's own
        // screen going out, which is what actually reads as the ship being dead.
        var shipPowered = snapshot.Power.ReactorOutput > 0.01f || snapshot.Power.BatteryCharge > 0.01f;
        foreach (var device in snapshot.SystemDevices)
        {
            // Match by DeviceId, not System — Shields has two separate physical generators
            // (M14) that can be damaged independently of each other.
            var damaged = snapshot.SystemStates.FirstOrDefault(s => s.DeviceId == device.Id)?.Damaged ?? false;
            var isOpen = openBlock.Kind == BlockKind.System && openBlock.System == device.System;
            var size = (int)((device.System == PowerSystemId.Engine ? BigBlockSize : NormalBlockSize) * device.SizeScale);
            DrawSystemDevice(spriteBatch, device, damaged, isOpen, size, origin, shipPowered);
        }

        // Hull cameras (M48) aren't ShipSystemDevices (WireGraphFactory's own comment explains why -
        // TestRunner.Mining.cs's ExpectedSystemDeviceIds asserts an exact 7-id set per hull), so they
        // get their own small drawing pass instead of joining the loop above - same visual language,
        // no click-to-open System panel behind it since there's nothing to open.
        foreach (var camera in snapshot.Cameras)
        {
            var camDamaged = snapshot.SystemStates.FirstOrDefault(s => s.DeviceId == camera.Id)?.Damaged ?? false;
            DrawCameraJunctionBox(spriteBatch, camera, camDamaged, origin, shipPowered);
        }

        DrawReactorBlock(spriteBatch, snapshot.ReactorBlock, snapshot.Reactor, snapshot.ReactorLevers, openBlock.Kind == BlockKind.Reactor, origin, totalSeconds);
        DrawDistributionBlock(spriteBatch, snapshot.DistributionBlock, openBlock.Kind == BlockKind.Distribution, origin, shipPowered);
        DrawReactorTrunkWires(spriteBatch,
            GetBlockRect(snapshot.ReactorBlock.Position, (int)(BigBlockSize * snapshot.ReactorBlock.SizeScale), origin),
            GetBlockRect(snapshot.DistributionBlock.Position, MediumBlockSize, origin),
            snapshot.Reactor.CurrentOutput > 0);
        DrawBatteryBlock(spriteBatch, snapshot.BatteryBlock, snapshot.Power, openBlock.Kind == BlockKind.Battery, origin, shipPowered);
        DrawNavigationConsole(spriteBatch, snapshot.NavigationConsole, openBlock.Kind == BlockKind.Navigation, origin, shipPowered);
        for (var rackIndex = 0; rackIndex < snapshot.StorageRacks.Count; rackIndex++)
        {
            var rack = snapshot.StorageRacks[rackIndex];
            var isOpen = openBlock.Kind == BlockKind.Rack && openBlock.TargetComponentId == rack.Id;
            DrawStorageRack(spriteBatch, rack, rackIndex * StorageRack.Capacity, snapshot, isOpen, origin);
        }
        ComponentRenderer.Draw(spriteBatch, _pixel, _font, snapshot, origin, totalSeconds);
        var anyoneAtHelm = snapshot.Characters.Any(c => c.IsAtHelm);
        DrawHelmConsole(spriteBatch, snapshot.HelmConsole, anyoneAtHelm, origin, shipPowered);
        DrawCardTable(spriteBatch, snapshot.CardTable, snapshot.CardGame is not null, origin);
        if (snapshot.Jukebox is { } jukebox)
            DrawJukebox(spriteBatch, jukebox, openBlock.Kind == BlockKind.Jukebox, origin);

        foreach (var turret in snapshot.Turrets)
        {
            var state = snapshot.TurretStates.FirstOrDefault(s => s.Id == turret.Id);
            DrawTurret(spriteBatch, turret, state, snapshot.Rooms, snapshot.Turrets, origin, totalSeconds);
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
            var center = origin + new Vector2((float)character.X, (float)character.Y) * PixelsPerUnit;
            var muzzle = GetHeldToolMuzzle(ItemType.Cutter, character.Inventory, center, facing) ?? center + HeldToolOffset(facing);
            FieldRenderer.DrawCuttingFlame(spriteBatch, _pixel, muzzle, facing, totalSeconds);
        }

        foreach (var character in snapshot.Characters.Where(c => c.Welding && !c.IsOutside && !c.OnStation && !c.OnEnemyShip))
        {
            var facing = new Vector2(character.FacingX, character.FacingY);
            var center = origin + new Vector2((float)character.X, (float)character.Y) * PixelsPerUnit;
            var muzzle = GetHeldToolMuzzle(ItemType.WeldingTool, character.Inventory, center, facing) ?? center + HeldToolOffset(facing);
            FieldRenderer.DrawWeldingFlame(spriteBatch, _pixel, muzzle, facing, totalSeconds);
        }

        if (effects is not null)
            // Cut and Explosion are both in AsteroidField world space, not this ship-local frame -
            // FieldRenderer draws those instead.
            foreach (var effect in effects.Where(e => e.Kind is not EffectKind.Cut and not EffectKind.Explosion))
                DrawSparkBurst(spriteBatch, origin + new Vector2((float)effect.Position.X, (float)effect.Position.Y) * PixelsPerUnit, effect.Progress, effect.Kind == EffectKind.Weld ? Color.White : Color.PaleGreen);

        if (atmosphere is not null)
            foreach (var particle in atmosphere)
                DrawAtmosphereParticle(spriteBatch, particle, origin);
    }

    // A breach's steam, a damaged system's sparks, a starved reactor's embers - continuous rather
    // than a one-shot burst, so each is just a soft dot that drifts and fades over its own lifetime
    // (AtmosphereParticle.Progress) instead of DrawSparkBurst's radiating rays.
    private void DrawAtmosphereParticle(SpriteBatch spriteBatch, AtmosphereParticle particle, Vector2 origin)
    {
        var center = origin + new Vector2((float)particle.Position.X, (float)particle.Position.Y) * PixelsPerUnit;
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
        DrawPanel(spriteBatch, _pixel, rect, faceColor, borderColor, borderThickness, _devicePlate, _faceShade);

    // internal + static, with the pixel texture passed explicitly, so ComponentRenderer.cs can draw
    // the exact same beveled-panel-plus-rivets look for installed components instead of a new art
    // style from scratch.
    //
    // `material` and `shade` are optional so the HUD callers (CardGamePanel) keep the flat look
    // they were drawn against - a machine face belongs on a machine, not on a playing card.
    internal static void DrawPanel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color faceColor, Color borderColor, int borderThickness,
        Texture2D? material = null, Texture2D? shade = null)
    {
        if (material is not null)
            TileTextures.DrawTiled(spriteBatch, material, TileTextures.DeviceTileSize, rect, faceColor);
        else
            spriteBatch.Draw(pixel, rect, faceColor);

        // Stretched over the whole face in one draw rather than tiled, so the top-to-bottom
        // shading spans the panel instead of repeating inside it.
        if (shade is not null)
            spriteBatch.Draw(shade, rect, Color.White);
        // Bevel: a lighter sliver along the top/left, a darker one along bottom/right.
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), Color.White * 0.18f);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), Color.White * 0.18f);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), Color.Black * 0.35f);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), Color.Black * 0.35f);
        DrawRectOutline(spriteBatch, pixel, rect, borderColor, borderThickness);
        DrawRivets(spriteBatch, pixel, rect);
        // A shadow just outside the bottom and right edges. Small, and the single biggest thing
        // that stops a panel reading as printed onto the deck rather than bolted onto it.
        spriteBatch.Draw(pixel, new Rectangle(rect.X + 2, rect.Bottom, rect.Width, 2), Color.Black * 0.28f);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right, rect.Y + 2, 2, rect.Height), Color.Black * 0.28f);
    }


    // The chamfered ("stepped octagon") housing every ship device now shares - the angular,
    // riveted-corner silhouette the Reactor's Hullwright's Bench redesign introduced, approximated
    // with 3 axis-aligned bands since this renderer has no arbitrary polygon fill. Reuses the same
    // material/shade textures DrawPanel already applies to a plain rect, so a chamfered device
    // still reads as the same family, not a different art style.
    private void DrawChamferedHousing(SpriteBatch spriteBatch, Rectangle rect, Color faceColor, Color borderColor, float borderThickness)
    {
        var chamfer = Math.Max(2, Math.Min(rect.Width, rect.Height) / 6);
        var topBand = new Rectangle(rect.X + chamfer, rect.Y, rect.Width - chamfer * 2, chamfer);
        var midBand = new Rectangle(rect.X, rect.Y + chamfer, rect.Width, rect.Height - chamfer * 2);
        var botBand = new Rectangle(rect.X + chamfer, rect.Bottom - chamfer, rect.Width - chamfer * 2, chamfer);

        TileTextures.DrawTiled(spriteBatch, _devicePlate, TileTextures.DeviceTileSize, topBand, faceColor);
        TileTextures.DrawTiled(spriteBatch, _devicePlate, TileTextures.DeviceTileSize, midBand, faceColor);
        TileTextures.DrawTiled(spriteBatch, _devicePlate, TileTextures.DeviceTileSize, botBand, faceColor);
        spriteBatch.Draw(_faceShade, rect, Color.White);

        Span<Vector2> vertices = stackalloc Vector2[]
        {
            new(rect.X + chamfer, rect.Y), new(rect.Right - chamfer, rect.Y),
            new(rect.Right, rect.Y + chamfer), new(rect.Right, rect.Bottom - chamfer),
            new(rect.Right - chamfer, rect.Bottom), new(rect.X + chamfer, rect.Bottom),
            new(rect.X, rect.Bottom - chamfer), new(rect.X, rect.Y + chamfer),
        };
        for (var i = 0; i < vertices.Length; i++)
        {
            HudIcons.DrawLine(spriteBatch, _pixel, vertices[i], vertices[(i + 1) % vertices.Length], borderColor, borderThickness);
            HudIcons.FillCircle(spriteBatch, _pixel, vertices[i], 1.2f, Color.Black * 0.5f);
        }

        // Same "bolted on, not painted on" drop shadow DrawPanel's plain rect gets, just clipped
        // short of the two chamfered corners it would otherwise poke past.
        spriteBatch.Draw(_pixel, new Rectangle(rect.X + chamfer, rect.Bottom, rect.Width - chamfer * 2, 2), Color.Black * 0.28f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right, rect.Y + chamfer, 2, rect.Height - chamfer * 2), Color.Black * 0.28f);
    }

    // A small dark backing sized to the text, drawn just under it - keeps a label legible over
    // whatever glow/screen/texture happens to sit behind it, rather than tuning every glow's own
    // brightness down to the point of looking dead just to keep text readable on top of it.
    // A device's baked face, plus the two things that have no business being baked into it: the
    // outline, which says whether this machine's panel is currently open, and the drop shadow that
    // sits it on the deck rather than on top of the picture.
    private void DrawDeviceFace(SpriteBatch spriteBatch, Rectangle rect, DeviceSkin.Face face, bool lit,
        Color borderColor, float borderThickness)
    {
        spriteBatch.Draw(_deviceSkin.Get(face, rect.Width, lit), rect, Color.White);

        var chamfer = Math.Max(2, Math.Min(rect.Width, rect.Height) / 6);
        Span<Vector2> vertices = stackalloc Vector2[]
        {
            new(rect.X + chamfer, rect.Y), new(rect.Right - chamfer, rect.Y),
            new(rect.Right, rect.Y + chamfer), new(rect.Right, rect.Bottom - chamfer),
            new(rect.Right - chamfer, rect.Bottom), new(rect.X + chamfer, rect.Bottom),
            new(rect.X, rect.Bottom - chamfer), new(rect.X, rect.Y + chamfer),
        };
        for (var i = 0; i < vertices.Length; i++)
            HudIcons.DrawLine(spriteBatch, _pixel, vertices[i], vertices[(i + 1) % vertices.Length], borderColor, borderThickness);

        spriteBatch.Draw(_pixel, new Rectangle(rect.X + chamfer, rect.Bottom, rect.Width - chamfer * 2, 2), Color.Black * 0.28f);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right, rect.Y + chamfer, 2, rect.Height - chamfer * 2), Color.Black * 0.28f);
    }

    // A device's name, above the machine rather than painted across it.
    //
    // On the face it fought with the hardware: the painted band, the dials and the text all wanted
    // the same few pixels, and on a 24px device the dark backing plate alone covered a third of the
    // face. Moving it off also frees the name from having to fit - which is why these went from
    // "O2", "Э", "Б" to the actual words. A single letter is an abbreviation the player has to
    // learn; a word is just the name of the thing.
    private void DrawDeviceLabel(SpriteBatch spriteBatch, Rectangle rect, string text, float scale = 0.5f)
    {
        var size = _font.MeasureString(text) * scale;
        // Six pixels of clearance, which also steps over a console's hood - that sits three pixels
        // proud of the plate and is four deep, so anything tighter would have the name resting on it.
        var position = new Vector2(rect.Center.X - size.X / 2f, rect.Y - size.Y - 6f);
        DrawLabelBacking(spriteBatch, text, position, scale);
        spriteBatch.DrawString(_font, text, position, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawLabelBacking(SpriteBatch spriteBatch, string text, Vector2 position, float scale)
    {
        var size = _font.MeasureString(text) * scale;
        spriteBatch.Draw(_pixel, new Rectangle((int)position.X - 2, (int)position.Y - 1, (int)size.X + 4, (int)size.Y + 2), Color.Black * 0.55f);
    }

    // A lit display inset into the upper half of a device face. Deliberately drawn *brighter* than
    // anything around it: ScenePost credits a lit pixel with extra brightness before testing it
    // against the bloom threshold, so a screen that clears that threshold is what actually reads as
    // "this machine is powered" rather than "this rectangle is painted a lighter colour".
    //
    // `powered` is passed true by every caller today. Wiring it to real per-device power state is
    // the next step and belongs with the callers, not here.
    private void DrawScreen(SpriteBatch spriteBatch, Rectangle panel, Color glow, bool powered)
    {
        // Too small to read as a screen at all - better nothing than a two-pixel smear.
        if (panel.Width < 16 || panel.Height < 14)
            return;

        var screen = new Rectangle(panel.X + 4, panel.Y + 4, panel.Width - 8, (int)(panel.Height * 0.5f) - 2);
        spriteBatch.Draw(_pixel, new Rectangle(screen.X - 1, screen.Y - 1, screen.Width + 2, screen.Height + 2), Color.Black * 0.8f);

        // Only a little white mixed in: the screen has to clear the bloom threshold, but pushing it
        // further just washes the colour out and every console ends up the same white blob.
        var face = powered ? Color.Lerp(glow, Color.White, 0.15f) : glow * 0.10f;
        spriteBatch.Draw(_pixel, screen, face);

        if (!powered)
            return;

        // Scanlines: period 3 so they survive at small sizes, and they hand the relief pass a
        // gradient to work with on what would otherwise be another flat fill.
        for (var y = screen.Y + 1; y < screen.Bottom; y += 3)
            spriteBatch.Draw(_pixel, new Rectangle(screen.X, y, screen.Width, 1), Color.Black * 0.22f);

        // The brightest point on the device, top-left, where the glass would catch the room light.
        spriteBatch.Draw(_pixel, new Rectangle(screen.X + 1, screen.Y + 1, Math.Max(2, screen.Width / 4), 1), Color.White * 0.40f);
    }


    // Deterministic per-position noise for the scorch below: a burnt machine has to look the same
    // from frame to frame, not crawl.
    private static float Splat(int seed)
    {
        var n = seed * 374761393;
        n = (n ^ (n >> 13)) * 1274126177;
        return ((n ^ (n >> 16)) & 0xFFFF) / 65535f;
    }

    // Soot over a damaged device. Damage used to be painting the face red, which reads as a red box;
    // what reads as a machine that has been on fire is the machine still being itself, with burns
    // on it.
    private void DrawScorch(SpriteBatch spriteBatch, Rectangle rect)
    {
        var key = (rect.X * 73856093) ^ (rect.Y * 19349663);
        for (var i = 0; i < 8; i++)
        {
            var w = 2 + (int)(Splat(key + i * 3) * (rect.Width / 3f));
            var h = 2 + (int)(Splat(key + i * 3 + 1) * (rect.Height / 4f));
            var x = rect.X + (int)(Splat(key + i * 3 + 2) * Math.Max(1, rect.Width - w));
            var y = rect.Y + (int)(Splat(key + i * 7) * Math.Max(1, rect.Height - h));
            spriteBatch.Draw(_pixel, new Rectangle(x, y, w, h), Color.Black * (0.16f + Splat(key + i * 11) * 0.20f));
        }
    }

    // Cooling slots across the lower half of a face - each a dark slot with a lit lip beneath it,
    // so the relief pass sees a real edge instead of a painted line.
    private void DrawVents(SpriteBatch spriteBatch, Rectangle rect, int count)
    {
        var w = Math.Max(4, rect.Width - 10);
        var x = rect.X + (rect.Width - w) / 2;
        for (var i = 0; i < count; i++)
        {
            var y = rect.Bottom - 5 - i * 4;
            if (y <= rect.Y + rect.Height / 2)
                return;
            spriteBatch.Draw(_pixel, new Rectangle(x, y, w, 2), Color.Black * 0.55f);
            spriteBatch.Draw(_pixel, new Rectangle(x, y + 2, w, 1), Color.White * 0.20f);
        }
    }

    // A hood over the top edge of a console: what makes it read as something you stand at rather
    // than a plate bolted to the wall. Every device in this game was the same rectangle before
    // these, and silhouette is what the eye sorts objects by first.
    private void DrawHood(SpriteBatch spriteBatch, Rectangle rect)
    {
        var hood = new Rectangle(rect.X - 2, rect.Y - 3, rect.Width + 4, 4);
        spriteBatch.Draw(_pixel, hood, new Color(38, 42, 52));
        spriteBatch.Draw(_pixel, new Rectangle(hood.X, hood.Y, hood.Width, 1), Color.White * 0.22f);
        spriteBatch.Draw(_pixel, new Rectangle(hood.X, hood.Bottom - 1, hood.Width, 1), Color.Black * 0.5f);
    }

    // A grab handle down one side of a locker.
    private void DrawHandle(SpriteBatch spriteBatch, Rectangle rect)
    {
        var x = rect.Right - 5;
        var y = rect.Y + rect.Height / 3;
        var h = Math.Max(4, rect.Height / 3);
        spriteBatch.Draw(_pixel, new Rectangle(x, y, 2, h), new Color(150, 155, 165));
        spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, h), Color.White * 0.30f);
        spriteBatch.Draw(_pixel, new Rectangle(x - 1, y + h, 4, 1), Color.Black * 0.5f);
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
        {
            spriteBatch.Draw(pixel, new Rectangle(x, y, size, size), color);
            // One lit pixel on the head: a flat dark square is a hole, a dark square with a
            // highlight on it is a rivet - and the relief pass needs both flanks to see a bump.
            spriteBatch.Draw(pixel, new Rectangle(x, y, 1, 1), Color.White * 0.38f);
        }
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

    // Depleted (World.Ammo.cs's finite stock, restocked at a station) reads as an empty crate -
    // dimmed out with a red outline - rather than looking identical to a full one.
    private void DrawAmmoStorage(SpriteBatch spriteBatch, AmmoStorage storage, int remaining, Vector2 origin)
    {
        const int size = 16;
        var center = origin + new Vector2(storage.X, storage.Y) * PixelsPerUnit;
        var rect = new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);
        var empty = remaining <= 0;
        DrawChamferedHousing(spriteBatch, rect, Color.SaddleBrown * (empty ? 0.35f : 0.85f), empty ? Color.OrangeRed : Color.SaddleBrown, 1);
        var ammoLabelPos = new Vector2(rect.X + 3, rect.Bottom + 1);
        var ammoLabel = remaining.ToString();
        DrawLabelBacking(spriteBatch, ammoLabel, ammoLabelPos, 0.5f);
        spriteBatch.DrawString(_font, ammoLabel, ammoLabelPos, empty ? Color.OrangeRed : Color.BurlyWood, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    // An upright cabinet, not a flat floor tile - a vertical seam down the middle like a locker
    // door, plus a small status light (lit CadetBlue with a suit inside, dim when it's been taken
    // and not yet put back - SuitLockerPanel shows the same state in more detail on click).
    private void DrawSuitLocker(SpriteBatch spriteBatch, SuitLocker locker, Vector2 origin, bool hasSuit)
    {
        var center = origin + new Vector2(locker.X, locker.Y) * PixelsPerUnit;
        var rect = GetBlockRect(locker.Position, NormalBlockSize, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Locker, hasSuit, Color.LightSteelBlue, 1);

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

            var center = origin + new Vector2((float)dropped.X, (float)dropped.Y) * PixelsPerUnit;
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
    private void DrawSystemDevice(SpriteBatch spriteBatch, ShipSystemDevice device, bool damaged, bool isOpen, int size, Vector2 origin, bool powered)
    {
        var rect = GetBlockRect(device.Position, size, origin);
        var center = new Vector2(rect.Center.X, rect.Center.Y);

        // Each system carries its own hardware rather than the same box in a different colour:
        // dials and pipework for life support, an intake for the engines, an emitter for the
        // shields, a capacitor bank for the weapon charger. The machinery is what tells them apart;
        // the painted band only confirms it.
        var face = device.System switch
        {
            PowerSystemId.Oxygen => DeviceSkin.Face.Oxygen,
            PowerSystemId.Engine => DeviceSkin.Face.Engine,
            PowerSystemId.Shields => DeviceSkin.Face.Shields,
            PowerSystemId.WeaponCharger => DeviceSkin.Face.Weapons,
            _ => DeviceSkin.Face.Auxiliary,
        };
        DrawDeviceFace(spriteBatch, rect, face, powered && !damaged,
            damaged ? Color.Red : isOpen ? Color.Gold : Color.LightSteelBlue, isOpen ? 3 : 2);
        if (damaged)
            DrawScorch(spriteBatch, rect);
        if (damaged)
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), horizontal: true);
        DrawDeviceLabel(spriteBatch, rect, SystemLabel(device.System));

        if (damaged)
            spriteBatch.DrawString(_font, "!", center + new Vector2(size / 2f - 2, -size), Color.Red, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
    }

    // Big, clickable — walking up and clicking it "enters" the reactor and shows its 4 fuel-rod
    // slots (see ReactorPanel). Glows warmer the more rods are loaded. Also carries the 3 physical
    // levers from the Hullwright's Bench concept pass (light / reactor power / door lock) and a
    // small cooling turbine that visibly spins while running — reactor.CurrentOutput already folds
    // in the emergency-shutdown lever (Reactor.cs), so every glow/spin here just follows it.
    // A plain junction box for a hull camera's own wiring/repair point - the Auxiliary face and
    // "Кам." label are the only things distinguishing it from the ship's own lighting box next
    // door, both riding the same Secondary channel (WireGraphFactory).
    private void DrawCameraJunctionBox(SpriteBatch spriteBatch, HullCamera camera, bool damaged, Vector2 origin, bool powered)
    {
        var rect = GetBlockRect(camera.InteriorPosition, NormalBlockSize, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Auxiliary, powered && !damaged,
            damaged ? Color.Red : Color.LightSteelBlue, 2);
        if (damaged)
        {
            DrawScorch(spriteBatch, rect);
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), horizontal: true);
        }
        DrawDeviceLabel(spriteBatch, rect, "Кам.");
        if (damaged)
            spriteBatch.DrawString(_font, "!", new Vector2(rect.Center.X + NormalBlockSize / 2f - 2, rect.Center.Y - NormalBlockSize),
                Color.Red, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
    }

    private void DrawReactorBlock(SpriteBatch spriteBatch, ReactorBlock block, ReactorState reactor, ReactorLeverState levers, bool isOpen, Vector2 origin, float totalSeconds)
    {
        var rect = GetBlockRect(block.Position, (int)(BigBlockSize * block.SizeScale), origin);
        var running = reactor.CurrentOutput > 0;
        var glowColor = running ? new Color(63, 184, 232) : new Color(40, 50, 55);
        var faceColor = running ? Color.DarkOrange * 0.55f : Color.DimGray * 0.6f;
        var borderColor = isOpen ? Color.Gold : running ? Color.Orange : Color.Gray;

        // Chamfered octagon housing — the Hullwright's Bench concept's "elongated octagon, long
        // straight flanks" silhouette, shared with every other device now (DrawChamferedHousing).
        DrawChamferedHousing(spriteBatch, rect, faceColor, borderColor, isOpen ? 2.4f : 1.6f);
        var chamfer = Math.Max(2, Math.Min(rect.Width, rect.Height) / 6);

        // 3 flat control terminals braced against the top edge, same spacing/idea as the concept's
        // lecterns — small, so just a stepped block plus a coloured screen-glow line each. Dimmed
        // from the concept's own brightness so they read as lit without competing with the
        // "Реактор" label drawn over the housing just below them.
        var terminalWidth = (rect.Width - chamfer * 2) / 3f;
        var terminalColors = new[] { Color.Gold, glowColor, Color.Gold };
        for (var i = 0; i < 3; i++)
        {
            var tx = rect.X + chamfer + terminalWidth * i;
            var baseRect = new Rectangle((int)tx + 1, rect.Y - 2, (int)terminalWidth - 2, 3);
            spriteBatch.Draw(_pixel, baseRect, Color.SlateGray * 0.8f);
            HudIcons.DrawLine(spriteBatch, _pixel,
                new Vector2(baseRect.X + 1, baseRect.Y + 1), new Vector2(baseRect.Right - 1, baseRect.Y + 1),
                terminalColors[i] * (running ? 0.6f : 0.3f), 1f);
        }

        // Twin glow tubes flanking the core (Hullwright's Bench concept) — the same electric-blue,
        // dimmed to a cold dead color once the emergency lever cuts output, and dimmed overall from
        // the concept's own brightness so it stays a background detail, not a wash of light.
        var tubeWidth = Math.Max(2, rect.Width / 10);
        var tubeInset = rect.Width / 6;
        var tubeY = rect.Y + rect.Height / 5;
        var tubeHeight = rect.Height * 3 / 5;
        spriteBatch.Draw(_pixel, new Rectangle(rect.X + tubeInset, tubeY, tubeWidth, tubeHeight), glowColor * (running ? 0.55f : 0.35f));
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - tubeInset - tubeWidth, tubeY, tubeWidth, tubeHeight), glowColor * (running ? 0.55f : 0.35f));

        // Core glow: a small inset square that reads as the fuel core, brighter while running.
        var coreSize = rect.Width / 3;
        var coreRect = new Rectangle(rect.Center.X - coreSize / 2, rect.Center.Y - coreSize / 2, coreSize, coreSize);
        spriteBatch.Draw(_pixel, coreRect, (running ? Color.Yellow : Color.DarkSlateGray) * (running ? 0.55f : 0.4f));
        DrawRectOutline(spriteBatch, coreRect, glowColor * 0.6f, 1);
        var reactorLabelPos = new Vector2(rect.X + 4, rect.Y + 4);
        DrawLabelBacking(spriteBatch, "Реактор", reactorLabelPos, 0.6f);
        spriteBatch.DrawString(_font, "Реактор", reactorLabelPos, Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        // Mini transformer sub-panels along the right flank (concept's 5, trimmed to 3 to stay
        // legible at ship-icon scale) — a distinct department colour each, purely decorative.
        var miniColors = new[] { new Color(46, 158, 134), new Color(176, 61, 130), new Color(138, 154, 58) };
        var miniSize = Math.Max(3, rect.Width / 8);
        var miniGap = (rect.Height - chamfer * 2) / 4f;
        for (var i = 0; i < 3; i++)
        {
            var my = rect.Y + chamfer + miniGap * (i + 0.6f);
            var miniRect = new Rectangle(rect.Right - miniSize / 2, (int)my, miniSize, miniSize);
            spriteBatch.Draw(_pixel, miniRect, Color.Black * 0.6f);
            HudIcons.FillCircle(spriteBatch, _pixel, new Vector2(miniRect.Center.X, miniRect.Center.Y), miniSize * 0.3f, miniColors[i] * (running ? 0.7f : 0.35f));
        }

        // Cooling turbine hanging off the bottom edge — blades actually spin while running, freeze
        // instantly the moment the reactor lever cuts output (the concept sketch's own
        // animation-play-state:paused, done here by simply not advancing the angle).
        var turbineCenter = new Vector2(rect.Center.X, rect.Bottom + rect.Height * 0.16f);
        var turbineRadius = rect.Width * 0.16f;
        HudIcons.FillCircle(spriteBatch, _pixel, turbineCenter, turbineRadius * 1.6f, glowColor * (running ? 0.22f : 0.1f));
        HudIcons.FillCircle(spriteBatch, _pixel, turbineCenter, turbineRadius, Color.Black * 0.75f);
        if (running)
        {
            var spin = totalSeconds * 3f;
            for (var i = 0; i < 4; i++)
            {
                var angle = spin + i * MathF.PI / 2f;
                var tip = turbineCenter + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * turbineRadius * 0.85f;
                HudIcons.DrawLine(spriteBatch, _pixel, turbineCenter, tip, glowColor, 2f);
            }
        }
        HudIcons.FillCircle(spriteBatch, _pixel, turbineCenter, turbineRadius * 0.3f, Color.Black * 0.9f);

        // The 3 levers themselves (ShipRenderer.GetReactorLeverRect) — each a little handle that
        // physically leans one way when on, the other when off, tipped with its own state color.
        DrawReactorLever(spriteBatch, GetReactorLeverRect(0, block, origin), levers.LightsOn, Color.Gold);
        DrawReactorLever(spriteBatch, GetReactorLeverRect(1, block, origin), !levers.EmergencyShutdown, glowColor);
        DrawReactorLever(spriteBatch, GetReactorLeverRect(2, block, origin), !levers.DoorsLocked, Color.OrangeRed);
    }

    private void DrawReactorLever(SpriteBatch spriteBatch, Rectangle rect, bool on, Color indicatorColor)
    {
        DrawPanel(spriteBatch, rect, Color.SlateGray * 0.75f, Color.Black, 1);
        var pivot = new Vector2(rect.Right - 3, rect.Bottom - 3);
        var angle = (on ? -135f : -45f) * (MathF.PI / 180f);
        var tip = pivot + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (rect.Height * 0.7f);
        HudIcons.DrawLine(spriteBatch, _pixel, pivot, tip, Color.Black, 2f);
        HudIcons.FillCircle(spriteBatch, _pixel, tip, 2f, on ? indicatorColor : indicatorColor * 0.3f);
        HudIcons.FillCircle(spriteBatch, _pixel, pivot, 1.5f, Color.Black);
    }

    // 3 physical outputs off the reactor's own housing, routed with a single tidy corner (never a
    // diagonal cut across the compartment) to 3 matching inputs on the distribution block - purely
    // visual (Reactor -> Distribution isn't part of the player-editable Component/Wire graph;
    // PowerGrid already treats the reactor's output as unconditionally feeding Distribution), but
    // it's the one link in the power backbone that had no wire drawn for it at all before this.
    // Direction-agnostic: picks whichever pair of facing edges points from one block toward the
    // other, so this reads right whether Distribution sits below the reactor (most hulls) or above
    // it (the Corvette) or off to a side (a Ship Editor layout).
    private void DrawReactorTrunkWires(SpriteBatch spriteBatch, Rectangle reactorRect, Rectangle distributionRect, bool running)
    {
        var reactorCenter = new Vector2(reactorRect.Center.X, reactorRect.Center.Y);
        var distributionCenter = new Vector2(distributionRect.Center.X, distributionRect.Center.Y);
        var delta = distributionCenter - reactorCenter;
        var vertical = MathF.Abs(delta.Y) >= MathF.Abs(delta.X);
        var color = (running ? new Color(63, 184, 232) : new Color(90, 96, 100)) * 0.8f;

        for (var i = 0; i < 3; i++)
        {
            var spread = (i - 1) * 5f;
            Vector2 start, end, bend;
            if (vertical)
            {
                var reactorEdgeY = delta.Y >= 0 ? reactorRect.Bottom : reactorRect.Y;
                var distributionEdgeY = delta.Y >= 0 ? distributionRect.Y : distributionRect.Bottom;
                start = new Vector2(reactorCenter.X + spread, reactorEdgeY);
                end = new Vector2(distributionCenter.X + spread, distributionEdgeY);
                bend = new Vector2(start.X, end.Y);
            }
            else
            {
                var reactorEdgeX = delta.X >= 0 ? reactorRect.Right : reactorRect.X;
                var distributionEdgeX = delta.X >= 0 ? distributionRect.X : distributionRect.Right;
                start = new Vector2(reactorEdgeX, reactorCenter.Y + spread);
                end = new Vector2(distributionEdgeX, distributionCenter.Y + spread);
                bend = new Vector2(end.X, start.Y);
            }

            HudIcons.DrawLine(spriteBatch, _pixel, start, bend, color, 2f);
            HudIcons.DrawLine(spriteBatch, _pixel, bend, end, color, 2f);
            HudIcons.FillCircle(spriteBatch, _pixel, start, 2f, color);
            HudIcons.FillCircle(spriteBatch, _pixel, end, 2f, color);
        }
    }

    // Medium — bigger than a plain system block, smaller than the reactor/engine (as requested).
    private void DrawDistributionBlock(SpriteBatch spriteBatch, PowerDistributionBlock block, bool isOpen, Vector2 origin, bool powered)
    {
        var rect = GetBlockRect(block.Position, MediumBlockSize, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Distribution, powered, isOpen ? Color.Gold : Color.Plum, isOpen ? 3 : 2);
        DrawDeviceLabel(spriteBatch, rect, "Щиток");
    }

    // Medium — sits next to the distribution block, charge level shown as a bottom-up fill so a
    // drained battery reads differently from a full one at a glance (same idea as the reactor's rods).
    private void DrawBatteryBlock(SpriteBatch spriteBatch, BatteryBlock block, PowerState power, bool isOpen, Vector2 origin, bool powered)
    {
        var rect = GetBlockRect(block.Position, MediumBlockSize, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Battery, powered, isOpen ? Color.Gold : Color.LightGreen, isOpen ? 3 : 2);

        // The charge column, live, in the recess the baked face leaves open for it. Segments rather
        // than one continuous bar: a length has to be measured against something, while six lit
        // cells out of six can simply be counted.
        var u = rect.Width / 40f;
        var column = new Rectangle(rect.X + (int)(28 * u), rect.Y + (int)(17 * u),
            Math.Max(3, (int)(5 * u)), Math.Max(6, (int)(18 * u)));
        var fraction = power.BatteryCapacity > 0 ? MathHelper.Clamp(power.BatteryCharge / power.BatteryCapacity, 0f, 1f) : 0f;
        const int segments = 6;
        var litCells = (int)MathF.Round(fraction * segments);
        var segmentHeight = Math.Max(1, column.Height / segments - 1);
        for (var i = 0; i < segments; i++)
        {
            var colour = i >= litCells ? new Color(48, 52, 58)
                : fraction > 0.5f ? new Color(120, 228, 140)
                : fraction > 0.2f ? new Color(232, 186, 80)
                : new Color(226, 96, 70);
            spriteBatch.Draw(_pixel, new Rectangle(column.X + 1,
                column.Bottom - 1 - (i + 1) * (segmentHeight + 1), column.Width - 2, segmentHeight), colour);
        }

        DrawDeviceLabel(spriteBatch, rect, "Батарея");
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
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Rack, true, isOpen ? Color.Gold : Color.Peru, isOpen ? 3 : 2);
        DrawHandle(spriteBatch, rect);

        var used = 0;
        for (var i = 0; i < StorageRack.Capacity; i++)
            if (offset + i < snapshot.RackSlots.Count && snapshot.RackSlots[offset + i] is not null)
                used++;
        // How full it is, without a second readout bolted on. The baked face already has crates on
        // every shelf, so an empty one is that shelf with the light taken off it - a half-loaded
        // rack stays the same piece of furniture instead of becoming a different drawing.
        const int shelves = 3;
        var shelfU = rect.Width / 40f;
        for (var i = 0; i < shelves; i++)
        {
            if (used > i * StorageRack.Capacity / shelves)
                continue;
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + (int)(7 * shelfU), rect.Y + (int)((12 + i * 8) * shelfU),
                (int)(26 * shelfU), Math.Max(2, (int)(7 * shelfU))), Color.Black * 0.62f);
        }
    }

    // Bridge console (game_design.md section 5) — click it to bring up the galaxy map.
    private void DrawNavigationConsole(SpriteBatch spriteBatch, NavigationConsole console, bool isOpen, Vector2 origin, bool powered)
    {
        var rect = GetBlockRect(console.Position, MediumBlockSize, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Navigation, powered, isOpen ? Color.Gold : Color.LightSeaGreen, isOpen ? 3 : 2);
        DrawHood(spriteBatch, rect);
        DrawDeviceLabel(spriteBatch, rect, "Сканер");
    }


    // Pilot's console (game_design.md Phase 3, M15) — click it to man it and bring up the helm's
    // joystick panel instead of the ship view.
    private void DrawHelmConsole(SpriteBatch spriteBatch, HelmConsole console, bool isOpen, Vector2 origin, bool powered)
    {
        var rect = GetBlockRect(console.Position, MediumBlockSize, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Helm, powered, isOpen ? Color.Gold : Color.Goldenrod, isOpen ? 3 : 2);
        DrawHood(spriteBatch, rect);
        DrawDeviceLabel(spriteBatch, rect, "Штурвал");
    }

    // A quiet card table - not clickable, just a felt surface bolted to the deck; two crew
    // standing beside it together is what actually starts a hand (World.CardGame.cs's
    // StepCardGame). Lit up gold whenever a hand happens to be running there, the same "isOpen"
    // glow every other console gets, so it's obvious at a glance the table isn't just furniture.
    private void DrawCardTable(SpriteBatch spriteBatch, CardTable table, bool inUse, Vector2 origin)
    {
        var rect = GetBlockRect(table.Position, MediumBlockSize, origin);
        DrawChamferedHousing(spriteBatch, rect, new Color(24, 90, 52) * 0.75f, inUse ? Color.Gold : new Color(90, 140, 100), inUse ? 3 : 2);
        var cardTableLabelPos = new Vector2(rect.X + 1, rect.Y + 7);
        DrawLabelBacking(spriteBatch, "Карты", cardTableLabelPos, 0.42f);
        spriteBatch.DrawString(_font, "Карты", cardTableLabelPos, Color.White, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
    }

    // The jukebox (Ship Editor only for now, Ship.Jukebox is nullable) - lit warm amber while
    // playing, dim otherwise, the same "glow says it's doing something" language DrawCardTable
    // uses for an active hand.
    private void DrawJukebox(SpriteBatch spriteBatch, JukeboxState jukebox, bool isOpen, Vector2 origin)
    {
        var rect = GetBlockRect(jukebox.Block.Position, MediumBlockSize, origin);
        var accent = isOpen ? Color.Gold : jukebox.On ? new Color(224, 196, 120) : new Color(140, 120, 90);
        // Lit means playing: the arch, the window and one pressed key all come up together, so the
        // machine says whether it is running without anybody reading a label.
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Jukebox, jukebox.On, accent, isOpen ? 3 : 2);
        DrawDeviceLabel(spriteBatch, rect, "Музыка");
    }

    private static string SystemLabel(PowerSystemId system) => system switch
    {
        PowerSystemId.Oxygen => "Кислород",
        PowerSystemId.Engine => "Двигатель",
        PowerSystemId.Shields => "Щиты",
        PowerSystemId.WeaponCharger => "Орудия",
        PowerSystemId.Secondary => "Прочее",
        _ => "?",
    };

    // Two separate things in two separate places: the periscope inside the room, which is what the
    // gunner walks up to and mans, and the gun itself out on the hull plating (TurretMount), whose
    // barrel is what the shell actually leaves through. Drawing the aim line from the console used
    // to imply the ship shot out of its own furniture.
    private void DrawTurret(SpriteBatch spriteBatch, Turret turret, TurretState? state,
        IReadOnlyList<Room> rooms, IReadOnlyList<Turret> allTurrets, Vector2 origin, float totalSeconds, bool showPeriscope = true)
    {
        var center = origin + new Vector2(turret.PeriscopeX, turret.PeriscopeY) * PixelsPerUnit;
        var manned = state?.MannedByPlayerId is not null;
        var damaged = state?.Damaged ?? false;

        // The crew station is inside the ship, so it goes with the rest of the interior when the
        // hull is drawn closed up.
        if (showPeriscope)
            DrawPeriscopeStation(spriteBatch, center, manned, damaged, totalSeconds);

        if (state is null)
            return;

        var mount = TurretMount.For(rooms, allTurrets, turret);
        var mountPx = origin + new Vector2((float)mount.Position.X, (float)mount.Position.Y) * PixelsPerUnit;
        var rotation = mount.FireDegrees(state.AimDegrees) * (MathF.PI / 180f);

        // Two sprites, and only the second one turns: a barbette bolted through the plating, and the
        // rotating mass sitting in it. Drawing those as one square with a stick out of it is most of
        // why this used to read as a diagram of a gun rather than as a gun.
        //
        // Which one is manned is carried by the trim paint and a lit sight rather than by making the
        // whole thing bigger and gold. It still has to be findable against the plating while you are
        // steering it, and the aim arc and sight line below are what do that - they are drawn only
        // for the gun you are actually behind.
        var look = damaged ? TurretSkin.Look.Damaged
            : manned ? TurretSkin.Look.Manned
            : TurretSkin.Look.Idle;
        spriteBatch.Draw(_turretSkin.Base(look), mountPx, null, Color.White, 0f,
            TurretSkin.BaseOrigin, 1f, SpriteEffects.None, 0f);
        spriteBatch.Draw(_turretSkin.Gun(look), mountPx, null, Color.White, rotation,
            TurretSkin.GunOrigin, 1f, SpriteEffects.None, 0f);

        if (!manned)
            return;

        // The gunner's aiming aids: the arc the barrel can actually cover, and a sight line running
        // out of the muzzle so it's obvious where a shell would go.
        DrawAimArcEdge(spriteBatch, mountPx, mount.FireDegrees(turret.MinAimDegrees));
        DrawAimArcEdge(spriteBatch, mountPx, mount.FireDegrees(turret.MaxAimDegrees));

        var muzzleLocal = mount.Muzzle(state.AimDegrees);
        var muzzle = origin + new Vector2((float)muzzleLocal.X, (float)muzzleLocal.Y) * PixelsPerUnit;
        spriteBatch.Draw(_pixel, muzzle, null, Color.Gold * 0.45f, rotation, new Vector2(0f, 0.5f), new Vector2(900f, 2f), SpriteEffects.None, 0f);

        var readout = $"{state.AimDegrees:0}°";
        spriteBatch.DrawString(_font, readout, mountPx + new Vector2(-10, -30), Color.Gold, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        // Rounds left, as pips rather than a count. Mid-engagement what the gunner needs off the gun
        // itself is "nearly out" or "fine"; the exact number is already on the gunnery panel, and
        // reading a digit means looking away from what you are tracking.
        if (state.MagazineCapacity > 0)
        {
            const int pips = 8;
            var loaded = (int)MathF.Ceiling(pips * MathHelper.Clamp(
                state.AmmoRemaining / (float)state.MagazineCapacity, 0f, 1f));
            for (var i = 0; i < pips; i++)
                spriteBatch.Draw(_pixel, new Rectangle((int)mountPx.X - pips * 3 + i * 6, (int)mountPx.Y + 28, 4, 3),
                    i < loaded ? new Color(232, 196, 96) : new Color(52, 56, 62));
        }
    }

    // A real periscope station instead of a flat marker square: a bolted octagonal floor block,
    // two handle grips a gunner would actually hold, cable stubs feeding into the deck, and a wide
    // flat terminal on top whose "screen" is a glowing blue-cyan projector lens - the thing the
    // gunner actually looks into - rather than a flat readout. Colour/glow carries the same idle/
    // manned/damaged read the old marker did, just on a model that looks like real hardware.
    private void DrawPeriscopeStation(SpriteBatch spriteBatch, Vector2 center, bool manned, bool damaged, float totalSeconds)
    {
        var baseColor = new Color(58, 64, 70);
        var baseEdge = new Color(23, 26, 29);
        var octagon = Octagon(center, 9f);
        Primitives.FillPolygon(spriteBatch, _pixel, center, octagon, baseColor);
        Primitives.StrokePolygon(spriteBatch, _pixel, octagon, baseEdge, 1.5f);
        foreach (var vertex in octagon)
            spriteBatch.Draw(_pixel, new Rectangle((int)vertex.X - 1, (int)vertex.Y - 1, 2, 2), baseEdge);

        // Handle grips, one on each side - lit gold while manned, same as the barrel it steers.
        foreach (var side in new[] { -1f, 1f })
        {
            var handleCenter = center + new Vector2(side * 13f, 0f);
            var handleRect = new Rectangle((int)handleCenter.X - 4, (int)handleCenter.Y - 4, 8, 8);
            spriteBatch.Draw(_pixel, handleRect, new Color(51, 56, 61));
            DrawRectOutline(spriteBatch, handleRect, baseEdge, 1);
            if (manned)
                HudIcons.DrawRingArc(spriteBatch, _pixel, handleCenter, 5f, 0f, 360f, Color.Gold * 0.7f, 8, 1f);
        }

        // Cable stubs trailing off toward the deck it's bolted into.
        HudIcons.DrawLine(spriteBatch, _pixel, center + new Vector2(-6f, 6f), center + new Vector2(-4f, 12f), baseEdge, 1.5f);
        HudIcons.DrawLine(spriteBatch, _pixel, center + new Vector2(6f, 6f), center + new Vector2(4f, 12f), baseEdge, 1.5f);

        // The wide flat terminal, offset slightly toward the "front".
        var terminalCenter = center + new Vector2(0f, -1f);
        var terminalRect = new Rectangle((int)terminalCenter.X - 12, (int)terminalCenter.Y - 9, 24, 16);
        spriteBatch.Draw(_pixel, terminalRect, new Color(49, 54, 60));
        DrawRectOutline(spriteBatch, terminalRect, baseEdge, 1);
        DrawRivets(spriteBatch, terminalRect);
        spriteBatch.Draw(_pixel, new Rectangle(terminalRect.X + 3, terminalRect.Bottom - 4, 6, 3), new Color(32, 36, 40));
        spriteBatch.Draw(_pixel, new Rectangle(terminalRect.Right - 9, terminalRect.Bottom - 4, 6, 3), new Color(32, 36, 40));

        var wellRect = new Rectangle(terminalRect.X + 3, terminalRect.Y + 2, terminalRect.Width - 6, terminalRect.Height - 7);
        spriteBatch.Draw(_pixel, wellRect, new Color(10, 13, 16));
        var glowCenter = new Vector2(wellRect.Center.X, wellRect.Center.Y);

        Color outerGlow, midGlow, coreGlow;
        if (damaged)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(totalSeconds * 6f);
            outerGlow = new Color(90, 38, 30) * (0.3f + 0.2f * pulse);
            midGlow = new Color(161, 56, 42) * (0.5f + 0.3f * pulse);
            coreGlow = new Color(255, 138, 106) * (0.6f + 0.4f * pulse);
            HudIcons.DrawLine(spriteBatch, _pixel, glowCenter + new Vector2(-4, -3), glowCenter + new Vector2(4, 3), baseEdge, 1f);
            HudIcons.DrawLine(spriteBatch, _pixel, glowCenter + new Vector2(3, -3), glowCenter + new Vector2(-3, 2), baseEdge, 1f);
            spriteBatch.DrawString(_font, "!", center + new Vector2(12, -20), Color.Red, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
        }
        else if (manned)
        {
            var pulse = 0.7f + 0.3f * MathF.Sin(totalSeconds * 3f);
            outerGlow = new Color(30, 122, 144) * 0.5f;
            midGlow = new Color(51, 182, 214) * 0.7f;
            coreGlow = new Color(143, 236, 255) * pulse;
            // A faint beam projecting "up" out of the lens - only shows while someone's actually
            // looking through it.
            Primitives.FillTriangle(spriteBatch, _pixel, glowCenter, glowCenter + new Vector2(-6, -14), glowCenter + new Vector2(6, -14), coreGlow * 0.18f);
        }
        else
        {
            outerGlow = new Color(30, 90, 102) * 0.35f;
            midGlow = new Color(47, 164, 194) * 0.55f;
            coreGlow = new Color(127, 224, 255) * 0.9f;
        }

        HudIcons.FillCircle(spriteBatch, _pixel, glowCenter, 4.5f, outerGlow);
        HudIcons.FillCircle(spriteBatch, _pixel, glowCenter, 3f, midGlow);
        HudIcons.FillCircle(spriteBatch, _pixel, glowCenter, 1.6f, coreGlow);
    }

    private static Vector2[] Octagon(Vector2 center, float radius)
    {
        var points = new Vector2[8];
        for (var i = 0; i < 8; i++)
        {
            var angle = i * MathF.PI / 4f;
            points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
        return points;
    }

    private void DrawAimArcEdge(SpriteBatch spriteBatch, Vector2 mountPx, float degrees)
    {
        var rotation = degrees * (MathF.PI / 180f);
        spriteBatch.Draw(_pixel, mountPx, null, Color.Gold * 0.18f, rotation, new Vector2(0f, 0.5f), new Vector2(420f, 2f), SpriteEffects.None, 0f);
    }

    // Green: open and passable. Dark red: closed and airtight (game_design.md Phase 3, M16).
    // leadsToVacuum only changes the OPEN color (purple instead of green) and widens the hazard
    // tape - a closed door reads the same "sealed" red no matter what's behind it; only an open
    // one to vacuum is the state that can actually kill you. destroyed overrides everything else
    // about its color - a door with its own hit points hit zero (World.Doors.cs, cut open or
    // chopped through) is jammed open regardless of leadsToVacuum, and a pulsing warning color plus
    // scorch marks read as "broken" at a glance instead of looking like an ordinary open door.
    // A framed bezel with corner rivets (matching every other device housing's own treatment),
    // hazard tape on a closed leaf, and a small flat access terminal mounted on the wall on each
    // flank - the "press to open" control the crew would actually reach for - round this out from
    // a flat colored rectangle into a proper airlock. internal: reused by StationRenderer, which
    // draws the station's own Rooms/Doors/Characters through the exact same visual language
    // instead of duplicating it.
    internal void DrawDoor(SpriteBatch spriteBatch, float left, float top, float width, float height, bool isOpen, Vector2 origin,
        bool leadsToVacuum = false, bool destroyed = false, float totalSeconds = 0f)
    {
        var rect = GetDoorRect(left, top, width, height, origin);
        var horizontal = rect.Width >= rect.Height;

        var indicator = destroyed
            ? Color.OrangeRed * (0.6f + 0.4f * MathF.Sin(totalSeconds * 6f))
            : isOpen
                ? (leadsToVacuum ? new Color(190, 140, 255) : new Color(90, 230, 120))
                : new Color(255, 90, 90);

        DrawDoorFrame(spriteBatch, rect, destroyed);

        if (destroyed)
            DrawDestroyedDoorLeaf(spriteBatch, rect, horizontal);
        else if (isOpen)
            DrawOpenDoorLeaf(spriteBatch, rect, horizontal, leadsToVacuum);
        else
            DrawClosedDoorLeaf(spriteBatch, rect, horizontal, leadsToVacuum);

        DrawDoorIndicator(spriteBatch, rect, indicator);
        DrawDoorTerminals(spriteBatch, rect, horizontal, indicator);
    }

    private void DrawDoorFrame(SpriteBatch spriteBatch, Rectangle rect, bool destroyed)
    {
        const int margin = 5;
        var bezel = new Rectangle(rect.X - margin, rect.Y - margin, rect.Width + margin * 2, rect.Height + margin * 2);
        spriteBatch.Draw(_pixel, bezel, destroyed ? new Color(92, 80, 72) : new Color(92, 104, 116));
        DrawRivets(spriteBatch, bezel);
    }

    private void DrawClosedDoorLeaf(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, bool leadsToVacuum)
    {
        spriteBatch.Draw(_pixel, rect, Color.DarkRed);
        if (horizontal)
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Center.Y - 1, rect.Width, 2), new Color(58, 10, 10));
        else
            spriteBatch.Draw(_pixel, new Rectangle(rect.Center.X - 1, rect.Y, 2, rect.Height), new Color(58, 10, 10));

        DrawDoorEdgeStripes(spriteBatch, rect, horizontal, leadsToVacuum ? 8 : 6);
    }

    private void DrawOpenDoorLeaf(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, bool leadsToVacuum)
    {
        var mid = leadsToVacuum ? new Color(70, 62, 96) : new Color(58, 64, 70);
        var cap = leadsToVacuum ? new Color(96, 80, 150) : new Color(63, 90, 68);
        spriteBatch.Draw(_pixel, rect, mid);
        DrawDoorLeafCaps(spriteBatch, rect, horizontal, cap);
    }

    // Destroyed is always jammed open (World.Doors.cs), so it gets the same retracted-leaf caps as
    // an ordinary open door, just worn-looking, plus scorch marks and a crack across the middle.
    private void DrawDestroyedDoorLeaf(SpriteBatch spriteBatch, Rectangle rect, bool horizontal)
    {
        spriteBatch.Draw(_pixel, rect, new Color(58, 52, 48));
        DrawDoorLeafCaps(spriteBatch, rect, horizontal, new Color(63, 90, 68) * 0.7f);

        var scorch = new Color(36, 31, 28) * 0.85f;
        var scorchSize = Math.Max(4, Math.Min(rect.Width, rect.Height) / 3);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, scorchSize, scorchSize), scorch);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - scorchSize, rect.Bottom - scorchSize, scorchSize, scorchSize), scorch);

        var crack = new Color(26, 21, 18);
        HudIcons.DrawLine(spriteBatch, _pixel, new Vector2(rect.X + rect.Width * 0.25f, rect.Y), new Vector2(rect.Center.X, rect.Center.Y), crack, 2f);
        HudIcons.DrawLine(spriteBatch, _pixel, new Vector2(rect.Center.X, rect.Center.Y), new Vector2(rect.X + rect.Width * 0.3f, rect.Bottom), crack, 2f);
    }

    // The leaf's own two halves shown slid back into the frame - what actually tells "open" apart
    // from "closed" now, instead of just a different flat color.
    private void DrawDoorLeafCaps(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, Color capColor)
    {
        var capThickness = horizontal ? Math.Max(6, rect.Width / 4) : Math.Max(6, rect.Height / 4);
        if (horizontal)
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, capThickness, rect.Height), capColor);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - capThickness, rect.Y, capThickness, rect.Height), capColor);
        }
        else
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, capThickness), capColor);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - capThickness, rect.Width, capThickness), capColor);
        }
    }

    private void DrawDoorEdgeStripes(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, int thickness)
    {
        if (horizontal)
        {
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Y, rect.Width, thickness), horizontal: true);
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), horizontal: true);
        }
        else
        {
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Y, thickness, rect.Height), horizontal: false);
            DrawHazardStripes(spriteBatch, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), horizontal: false);
        }
    }

    // A small backed light set into the leaf's own middle - the same state color the two side
    // terminals show, just read from the door itself once you're already close to it.
    private void DrawDoorIndicator(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        const int size = 8;
        var center = new Point(rect.Center.X, rect.Center.Y);
        spriteBatch.Draw(_pixel, new Rectangle(center.X - size / 2 - 1, center.Y - size / 2 - 1, size + 2, size + 2), Color.Black * 0.6f);
        spriteBatch.Draw(_pixel, new Rectangle(center.X - size / 2, center.Y - size / 2, size, size), color);
    }

    // The "press to open" control the crew would actually reach for, mounted on the wall on both
    // flanks of the doorway rather than on the door itself (which slides or jams) - one on each
    // side (never both on the same flank), a little below the door's own near end so it reads as
    // reachable rather than floating in the wall. Its own light mirrors the leaf's indicator, so
    // which way a door currently reads is obvious before getting close enough to see the leaf.
    private void DrawDoorTerminals(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, Color indicatorColor)
    {
        const int thickness = 8;
        const int length = 26;
        const int frameMargin = 5;
        const int gap = 2;
        const int alongOffset = 4;

        if (!horizontal)
        {
            var y = rect.Y + alongOffset;
            DrawDoorTerminal(spriteBatch, new Rectangle(rect.X - frameMargin - gap - thickness, y, thickness, length), indicatorColor, vertical: true);
            DrawDoorTerminal(spriteBatch, new Rectangle(rect.Right + frameMargin + gap, y, thickness, length), indicatorColor, vertical: true);
        }
        else
        {
            var x = rect.X + alongOffset;
            DrawDoorTerminal(spriteBatch, new Rectangle(x, rect.Y - frameMargin - gap - thickness, length, thickness), indicatorColor, vertical: false);
            DrawDoorTerminal(spriteBatch, new Rectangle(x, rect.Bottom + frameMargin + gap, length, thickness), indicatorColor, vertical: false);
        }
    }

    private void DrawDoorTerminal(SpriteBatch spriteBatch, Rectangle rect, Color indicatorColor, bool vertical)
    {
        spriteBatch.Draw(_pixel, rect, new Color(49, 54, 60));
        DrawRectOutline(spriteBatch, rect, Color.Black * 0.5f, 1);
        if (vertical)
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + 1, rect.Y + 2, rect.Width - 2, (int)(rect.Height * 0.4f)), new Color(16, 19, 24));
            var lightHeight = Math.Max(2, (int)(rect.Height * 0.25f));
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + 1, rect.Y + rect.Height / 2, rect.Width - 2, lightHeight), indicatorColor);
        }
        else
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + 2, rect.Y + 1, (int)(rect.Width * 0.4f), rect.Height - 2), new Color(16, 19, 24));
            var lightWidth = Math.Max(2, (int)(rect.Width * 0.25f));
            spriteBatch.Draw(_pixel, new Rectangle(rect.X + rect.Width / 2, rect.Y + 1, lightWidth, rect.Height - 2), indicatorColor);
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
    // The floor normal map, drawn into ScenePost's normals target with exactly the geometry and
    // tile size DrawRoomFloor uses for the visible plate below, so the two line up texel for
    // texel. Floors only: they are most of the lit surface on screen, and unlike the walls their
    // geometry is one rectangle per room that GetRoomRect already hands out - the walls would
    // mean duplicating the band layout and then keeping the duplicate in step with it.
    internal void DrawFloorNormals(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        foreach (var room in snapshot.Rooms)
            TileTextures.DrawTiled(spriteBatch, _floorNormals, TileTextures.FloorTileSize, GetRoomRect(room, origin), Color.White);
    }

    // The hull's own true normals, stamped into the same target right alongside the floor's -
    // same room rect HullSkin.Draw already tiles the visible plate texture across (its own
    // RoomRect, identical formula to GetRoomRect here), so the normal map lines up texel for
    // texel with what's actually on screen. The cut-corner plate polygon HullSkin fills is not
    // replicated here on purpose: a normal map is read-only input to the lighting pass, not
    // something a player can see the silhouette of directly, so the handful of stray normal
    // texels just past the true corner cut (covered by the plate edge stroke/interior geometry
    // anyway) cost nothing to leave in, the same tradeoff HullSkin's own albedo tiling already
    // makes at line 62 of HullSkin.cs.
    internal void DrawHullNormals(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        foreach (var room in snapshot.Rooms)
            TileTextures.DrawTiled(spriteBatch, _hullNormals, TileTextures.HullTileSize, GetRoomRect(room, origin), Color.White);
    }

    internal void DrawRoomFloor(SpriteBatch spriteBatch, Room room, float oxygen, Vector2 origin, Color? accentOverride = null)
    {
        var rect = GetRoomRect(room, origin);
        var accent = accentOverride ?? RoomDecor.Accent(room.Id, room.Name);

        // Plates rather than one repeated stamp, and the seams are cut into them rather than drawn
        // over the top - which is why DrawFloorGrating is gone: its hairline grid had no depth, so
        // it read as printed on. The gunnery and reactor compartments get their own field inside the
        // same frame, so they are recognisably different rooms on recognisably the same ship.
        //
        // Indexed from the ship's own origin, so which plate lands where belongs to the ship and the
        // pattern does not crawl across the deck when the camera moves.
        DeckPlates.DrawTiled(spriteBatch, _deckPlates[DeckPlates.For(room.Id)], rect, Color.White,
            new Point((int)origin.X, (int)origin.Y));
        // Dirt across the plates, which is the one thing that hides the tile grid - nothing painted
        // inside a tile can, because it repeats with the tile.
        DeckPlates.DrawGrime(spriteBatch, _deckGrime, rect, room.Id);
        RoomDecor.DrawLightPool(spriteBatch, _pixel, rect, accent);
        RoomDecor.DrawFurniture(spriteBatch, _pixel, rect, room.Id, accent);
        // Content-каталог отсеков - a built room's own id is always a plain "room-N" (never matches
        // DrawFurniture's id-substring switch above), so this is never a double-draw: it only ever
        // fires for the 13 catalog room types DrawFurniture already silently skips.
        RoomDecor.DrawCatalogDecor(spriteBatch, _pixel, rect, room.Name, accent);

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
    //
    // Every band carries the same hull-plate tile HullSkin paints on the true exterior of the
    // ship, on every side of every room - not just the sides that happen to face open space. Only
    // texturing the genuinely exterior sides once read as inconsistent (interior corridors kept
    // the plain, flatter wall pattern right next to compartments that got the heavier plate look),
    // so the one texture that actually reads well wins everywhere a wall is drawn.
    internal void DrawRoomWalls(SpriteBatch spriteBatch, Room room, float oxygen, Vector2 origin, Color? accentOverride = null)
    {
        var rect = GetRoomRect(room, origin);
        var alarmed = oxygen < 70f;
        var accent = accentOverride ?? RoomDecor.Accent(room.Id, room.Name);
        const int half = WallThickness / 2;

        RoomDecor.DrawWallLamps(spriteBatch, _pixel, rect, accent, alarmed);

        DrawWallBand(spriteBatch, new Rectangle(rect.X - half, rect.Y - half, rect.Width + WallThickness, WallThickness), true, alarmed, origin);
        DrawWallBand(spriteBatch, new Rectangle(rect.X - half, rect.Bottom - half, rect.Width + WallThickness, WallThickness), true, alarmed, origin);
        DrawWallBand(spriteBatch, new Rectangle(rect.X - half, rect.Y - half, WallThickness, rect.Height + WallThickness), false, alarmed, origin);
        DrawWallBand(spriteBatch, new Rectangle(rect.Right - half, rect.Y - half, WallThickness, rect.Height + WallThickness), false, alarmed, origin);

        DrawCornerPlate(spriteBatch, rect.X, rect.Y);
        DrawCornerPlate(spriteBatch, rect.Right, rect.Y);
        DrawCornerPlate(spriteBatch, rect.X, rect.Bottom);
        DrawCornerPlate(spriteBatch, rect.Right, rect.Bottom);
    }

    private void DrawWallBand(SpriteBatch spriteBatch, Rectangle band, bool horizontal, bool alarmed, Vector2 origin)
    {
        // Untinted, same as HullSkin's own use of this texture - it already bakes its real
        // gunmetal colour in, so multiplying it by an alarmed/normal wall tint would just darken
        // it towards black instead of recolouring it. The alarmed conduit/rib overlay drawn below
        // still carries the alarm state on this band.
        var cellOrigin = new Point((int)origin.X, (int)origin.Y);
        TileTextures.DrawSquares(spriteBatch, _hullPlates, TileTextures.HullTileSize, WallThickness, band, Color.White, cellOrigin);
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

    private void DrawCornerPlate(SpriteBatch spriteBatch, int x, int y)
    {
        const int size = WallThickness + 6;
        var rect = new Rectangle(x - size / 2, y - size / 2, size, size);
        TileTextures.DrawSquares(spriteBatch, _hullPlates, TileTextures.HullTileSize, size, rect, Color.White, new Point(x, y));
        DrawRectOutline(spriteBatch, rect, Color.Black * 0.45f, 1);
        DrawRivets(spriteBatch, rect);
    }

    // A metal frame around a deliberately blank pane - see the comment at this method's call site
    // in Draw() for why the inside is left plain rather than painted with any backdrop of its own.
    private void DrawWindowPane(SpriteBatch spriteBatch, CockpitWindows.Pane worldPane, Vector2 origin)
    {
        var pane = new Rectangle(
            (int)(origin.X + worldPane.Left * PixelsPerUnit), (int)(origin.Y + worldPane.Top * PixelsPerUnit),
            (int)((worldPane.Right - worldPane.Left) * PixelsPerUnit), (int)((worldPane.Bottom - worldPane.Top) * PixelsPerUnit));
        if (pane.Width <= 0 || pane.Height <= 0)
            return;

        var bezel = new Rectangle(pane.X - 2, pane.Y - 2, pane.Width + 4, pane.Height + 4);
        TileTextures.DrawTiled(spriteBatch, _wallPlate, TileTextures.WallTileSize, bezel, new Color(96, 104, 116));
        DrawRectOutline(spriteBatch, bezel, Color.Black * 0.45f, 1);
        DrawRivets(spriteBatch, bezel);

        // Plain black, matching GraphicsDevice.Clear - not a fake starfield of its own. Whatever
        // FieldRenderer paints there next (a real asteroid/ship/EVA character, or nothing) is what
        // actually shows.
        spriteBatch.Draw(_pixel, pane, Color.Black);
        // A faint blue glass tint - what tells "window" apart from "hole" when there's nothing out
        // there to see; a real object drawn over it afterward reads normally, same as breach holes.
        spriteBatch.Draw(_pixel, pane, new Color(80, 160, 210) * 0.08f);

        var mullion = Color.Black * 0.55f;
        const int panes = 4;
        for (var i = 1; i < panes; i++)
        {
            if (worldPane.HorizontalBand)
            {
                var x = pane.X + pane.Width * i / panes;
                spriteBatch.Draw(_pixel, new Rectangle(x, pane.Y, 2, pane.Height), mullion);
            }
            else
            {
                var y = pane.Y + pane.Height * i / panes;
                spriteBatch.Draw(_pixel, new Rectangle(pane.X, y, pane.Width, 2), mullion);
            }
        }
    }

    // The camera's own fraction is dropped *before* the room's offset is added, not after.
    //
    // (int)(origin + offset) - (int)origin is not a constant as the camera glides: it flips by a
    // pixel with origin's fractional part. Every tiled surface picks its plate variant from exactly
    // that difference divided by the tile size, so for any room sitting at a whole world coordinate
    // - which is most of them - the two possible values land either side of a multiple of 48 and the
    // entire compartment reindexes. That is the deck and the wall plating visibly reshuffling while
    // the camera pans from one compartment to the next.
    //
    // Splitting the truncation makes the difference exactly (int)(room.X * PixelsPerUnit), which
    // does not depend on where the camera is at all. Costs at most a pixel of placement.
    private static Rectangle GetRoomRect(Room room, Vector2 origin) => new(
        (int)origin.X + (int)(room.X * PixelsPerUnit),
        (int)origin.Y + (int)(room.Y * PixelsPerUnit),
        (int)(room.Width * PixelsPerUnit),
        (int)(room.Height * PixelsPerUnit));

    // M62 - the "ghost" for a room still under construction: a translucent cyan fill (deliberately
    // not the hazard-red DrawBreachedWallBlock's pulse uses, since an in-progress build isn't a
    // problem to fix) plus a dashed-looking border (drawn as short segments rather than one solid
    // line, cheap enough with just _pixel and reads as a blueprint/holographic outline) and a
    // percentage readout centered in the footprint.
    private void DrawPendingRoomBuild(SpriteBatch spriteBatch, PendingRoomBuildState pending, Vector2 origin)
    {
        var rect = new Rectangle(
            (int)origin.X + (int)(pending.X * PixelsPerUnit),
            (int)origin.Y + (int)(pending.Y * PixelsPerUnit),
            (int)(pending.Width * PixelsPerUnit),
            (int)(pending.Height * PixelsPerUnit));

        spriteBatch.Draw(_pixel, rect, Color.CornflowerBlue * 0.28f);

        const int dash = 10, gap = 6, thickness = 2;
        for (var x = rect.Left; x < rect.Right; x += dash + gap)
        {
            var w = Math.Min(dash, rect.Right - x);
            spriteBatch.Draw(_pixel, new Rectangle(x, rect.Top, w, thickness), Color.CornflowerBlue);
            spriteBatch.Draw(_pixel, new Rectangle(x, rect.Bottom - thickness, w, thickness), Color.CornflowerBlue);
        }
        for (var y = rect.Top; y < rect.Bottom; y += dash + gap)
        {
            var h = Math.Min(dash, rect.Bottom - y);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Left, y, thickness, h), Color.CornflowerBlue);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, y, thickness, h), Color.CornflowerBlue);
        }

        var text = $"{pending.Name}\n{(int)(pending.ProgressFraction * 100)}%";
        var size = _font.MeasureString(text) * 0.6f;
        var center = new Vector2(rect.Center.X, rect.Center.Y) - size * 0.5f;
        spriteBatch.DrawString(_font, text, center, Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    // Content-каталог отсеков - click-to-place UI's own overlay while a module is selected
    // (Game1.cs's own _placingRoomCatalogId): a light 1-tile (3-unit) grid across the hull's own
    // footprint, every currently valid attach spot (RoomPlacementPreview.FindCandidates) outlined
    // faintly, and whichever one is closest to the cursor right now filled solid green - the one a
    // click would actually confirm.
    public void DrawPlacementOverlay(SpriteBatch spriteBatch, WorldSnapshot snapshot,
        IReadOnlyList<RoomPlacementPreview.Candidate> candidates, RoomPlacementPreview.Candidate? nearest, Vector2 origin)
    {
        const float tileUnits = 3f;
        var minX = snapshot.Rooms.Min(r => r.X) - tileUnits;
        var maxX = snapshot.Rooms.Max(r => r.X + r.Width) + tileUnits;
        var minY = snapshot.Rooms.Min(r => r.Y) - tileUnits;
        var maxY = snapshot.Rooms.Max(r => r.Y + r.Height) + tileUnits;

        Color gridLine = new(120, 160, 190, 60);
        for (var x = MathF.Floor(minX / tileUnits) * tileUnits; x <= maxX; x += tileUnits)
        {
            var screenX = (int)origin.X + (int)(x * PixelsPerUnit);
            spriteBatch.Draw(_pixel, new Rectangle(screenX, (int)origin.Y + (int)(minY * PixelsPerUnit), 1, (int)((maxY - minY) * PixelsPerUnit)), gridLine);
        }
        for (var y = MathF.Floor(minY / tileUnits) * tileUnits; y <= maxY; y += tileUnits)
        {
            var screenY = (int)origin.Y + (int)(y * PixelsPerUnit);
            spriteBatch.Draw(_pixel, new Rectangle((int)origin.X + (int)(minX * PixelsPerUnit), screenY, (int)((maxX - minX) * PixelsPerUnit), 1), gridLine);
        }

        foreach (var candidate in candidates)
        {
            var rect = new Rectangle(
                (int)origin.X + (int)(candidate.X * PixelsPerUnit), (int)origin.Y + (int)(candidate.Y * PixelsPerUnit),
                (int)(candidate.Width * PixelsPerUnit), (int)(candidate.Height * PixelsPerUnit));
            var isNearest = nearest is { } n && n.X == candidate.X && n.Y == candidate.Y;
            spriteBatch.Draw(_pixel, rect, Color.LightGreen * (isNearest ? 0.35f : 0.1f));
            DrawRectOutline(spriteBatch, _pixel, rect, Color.LightGreen * (isNearest ? 1f : 0.4f), isNearest ? 2 : 1);
        }
    }

    private static float RoomOxygen(WorldSnapshot snapshot, string roomId) =>
        snapshot.RoomOxygen.FirstOrDefault(o => o.RoomId == roomId)?.Oxygen ?? 100f;

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
    // internal: also called by BoardingRenderer for the boarded enemy hull's own breached interior
    // wall blocks, the same visual language as the player's own ship's.
    internal void DrawBreachedWallBlock(SpriteBatch spriteBatch, WallBlock block, Room room, Vector2 origin, float totalSeconds)
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
    internal void DrawWallToolTargetBar(SpriteBatch spriteBatch, WallBlock block, WallBlockState state, Vector2 origin) =>
        DrawToolTargetBar(spriteBatch, new Vector2(block.X, block.Y), state.Fraction, origin);

    // Same bar, over a door being cut open (World.Cutting.cs's CutIndoorAlongFlame) instead of a
    // hull block - worldPosition is whichever of Door/AirlockOuterDoor matched
    // (character.DoorToolTargetId), since both share the same X/Y shape but not a common base type.
    internal void DrawDoorToolTargetBar(SpriteBatch spriteBatch, Vector2 worldPosition, DoorState state, Vector2 origin) =>
        DrawToolTargetBar(spriteBatch, worldPosition, state.Fraction, origin);

    // internal: Game1's HUD batch also calls this directly for an enemy hull's own airlock target
    // (a WallBlockState like a wall block's, but on an AirlockOuterDoor, which isn't a WallBlock) -
    // the two typed wrappers above only cover the player's own ship's WallBlock/Door shapes.
    internal void DrawToolTargetBar(SpriteBatch spriteBatch, Vector2 worldPosition, float fraction, Vector2 origin)
    {
        const int width = 32;
        const int height = 6;
        var center = origin + worldPosition * PixelsPerUnit;
        var bar = new Rectangle((int)center.X - width / 2, (int)center.Y - 22, width, height);
        var fill = fraction > 0.6f ? Color.LimeGreen : fraction > 0.25f ? Color.Orange : Color.OrangeRed;
        spriteBatch.Draw(_pixel, bar, Color.Black * 0.7f);
        spriteBatch.Draw(_pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * fraction), bar.Height), fill);
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
    internal void DrawCharacter(SpriteBatch spriteBatch, CharacterState character, Vector2 origin)
    {
        var size = (int)(CharacterDiameter * PixelsPerUnit);
        var center = new Vector2(origin.X + (float)character.X * PixelsPerUnit, origin.Y + (float)character.Y * PixelsPerUnit);
        var rect = new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);

        var facing = new Vector2(character.FacingX, character.FacingY);
        if (facing.LengthSquared() > 0.01f)
            facing.Normalize();
        else
            facing = new Vector2(1f, 0f); // idle characters still need a direction to hold a tool toward

        // Hired crew (World.Recruiting.cs) reads as a body of a different colour, not another
        // anonymous crewmate - the point of hiring one is knowing it's there and doing its job.
        var bodyColor = character.IsBot ? new Color(70, 110, 150) : new Color(196, 78, 44);
        // The accent is the shoulder patch on a uniform - the one place a crewman carries a colour
        // that is not the cloth itself.
        var accent = character.IsBot ? new Color(150, 200, 235) : new Color(226, 186, 70);
        // Standing, anchored at the feet, so the body goes up the screen from where the crewman
        // actually is. The world stays top-down and the person does not - the same mix SS13 and
        // Rimworld use, and the reason the figure finally has arms and legs you can see.
        _crewSkin.Draw(spriteBatch, new Vector2(center.X, center.Y + size * 0.30f),
            CharacterHeight * PixelsPerUnit, bodyColor, accent, character.WearingSuit, facing);

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
