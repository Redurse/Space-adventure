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

    // Direct user request ("реактор это устройство 4 на 4 тайла") - a real, fixed 4x4-game-unit
    // footprint everywhere in the game, not just a bigger icon; independent of BigBlockSize (still
    // used unchanged for Engine system devices, which this request never touched).
    public const int ReactorBlockSize = (int)(4 * PixelsPerUnit);

    // A single wall tile (1 game unit), chosen to exactly match TerminalTexture's own native 48px
    // resolution (1 * PixelsPerUnit == 48) - the baked texture draws 1:1 with no up/downscaling.
    public const int TerminalBlockSize = (int)(1 * PixelsPerUnit);

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
    // Hand-made wall panel art (Content/Textures/Walls) - optional, set post-construction via
    // SetWallTextures once Game1.LoadContent has loaded them, same "load real art, fall back to the
    // procedural plate if missing" convention RoomDecor.SetCatalogTexture already uses. Null (the
    // default until LoadContent runs, or if the PNGs are ever removed) means DrawWallBand/
    // DrawCornerPlate keep drawing the old procedural _hullPlates exactly as before.
    private Texture2D? _wallVerticalTexture;
    private Texture2D? _wallHorizontalTexture;
    private Texture2D? _wallCornerTexture;
    private Texture2D? _wallEndCapTexture;
    private Texture2D? _wallTJunctionTexture;
    private Texture2D? _reactorTexture;
    // Same "load real art, fall back to procedural if missing" convention - see SetEngineTextures.
    private Texture2D? _engineControlTexture;
    private Texture2D? _engineBulkheadTexture;
    private Texture2D? _engineNozzleTexture;
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
    private readonly Texture2D _terminalTexture;

    public ShipRenderer(GraphicsDevice graphicsDevice, SpriteFont font, Rectangle worldViewport)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _terminalTexture = TerminalTexture.Create(graphicsDevice);
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

    // Called once from Game1.LoadContent after the real PNGs (or lack of them) are known - see
    // this class's own doc comment on _wallVerticalTexture for why this is a post-construction
    // setter rather than a ContentManager passed into the constructor (ShipRenderer builds every
    // other texture procedurally and never touches Content itself).
    internal void SetWallTextures(Texture2D? vertical, Texture2D? horizontal, Texture2D? corner, Texture2D? endCap,
        Texture2D? tJunction = null)
    {
        _wallVerticalTexture = vertical;
        _wallHorizontalTexture = horizontal;
        _wallCornerTexture = corner;
        _wallEndCapTexture = endCap;
        _wallTJunctionTexture = tJunction;
    }

    // Same "load real art, fall back to procedural if missing" convention as SetWallTextures.
    internal void SetReactorTexture(Texture2D? reactor) => _reactorTexture = reactor;

    // Direct user request - real hand-picked art for all three parts of a Cosmoteer-style marching
    // engine, replacing the DeviceSkin-face placeholder each part drew before. Same "load real art,
    // fall back to procedural if missing" convention as SetWallTextures/SetReactorTexture.
    internal void SetEngineTextures(Texture2D? control, Texture2D? bulkhead, Texture2D? nozzle)
    {
        _engineControlTexture = control;
        _engineBulkheadTexture = bulkhead;
        _engineNozzleTexture = nozzle;
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

    // Reactor housing size in real screen pixels - a fixed 4x4-game-unit footprint (ReactorBlockSize)
    // times whatever per-hull-class SizeScale flavor a ship's own ReactorBlock still carries.
    public static int ReactorSize(ReactorBlock block) => (int)(ReactorBlockSize * block.SizeScale);

    // The reactor's 3 physical levers (light / reactor power / door lock — ReactorLeverState),
    // stacked down its left flank just outside the main housing rect, same "shared by drawing and
    // hit-testing" convention as GetBlockRect above.
    public static Rectangle GetReactorLeverRect(int index, ReactorBlock block, Vector2 origin)
    {
        var size = ReactorSize(block);
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
        // M75 - the actual wall art is now a real per-tile pass (DrawShipWalls), not one band per
        // room edge; DrawRoomWallLamps keeps only the per-room lamp decor that used to ride along
        // with DrawRoomWalls (still called in full by BoardingRenderer/StationRenderer, unchanged).
        foreach (var room in snapshot.Rooms)
            DrawRoomWallLamps(spriteBatch, room, RoomOxygen(snapshot, room.Id), origin);
        DrawShipWalls(spriteBatch, snapshot, origin);

        // A frame over the metal plus a plain unpainted pane, only for the crew station that
        // actually faces open space - deliberately left blank rather than filled with any painted
        // backdrop, same trick DrawBreachedWallBlock already uses for a hull breach: FieldRenderer
        // draws every asteroid/ship/EVA character at its own real position after this (Game1's own
        // draw order), so whatever is actually out there - or nothing, just black - shows through
        // exactly as it would through a real pane of glass, with no separate starfield of its own
        // to keep in sync.
        foreach (var pane in CockpitWindows.Panes(snapshot.Rooms))
            DrawWindowPane(spriteBatch, pane, origin);

        // Drawn after room outlines so the opening visibly cuts through the shared wall. Rect comes
        // from TileGridRasterizer.DoorTileRect, not the door's own raw Left/Top/Width/Height - see
        // that method's own doc comment (bug report: the door sprite sat half a tile off from
        // DrawShipWalls' own tile-square wall art on either side of it).
        foreach (var door in snapshot.Doors)
        {
            var state = snapshot.DoorStates.FirstOrDefault(s => s.DoorId == door.Id);
            var (left, top, width, height) = TileGridRasterizer.DoorTileRect(snapshot.Rooms, door.X, door.Y, door.Width, door.Height);
            DrawDoor(spriteBatch, left, top, width, height, state?.IsOpen ?? true, origin,
                destroyed: state?.Destroyed ?? false, totalSeconds: totalSeconds);
        }

        foreach (var outerDoor in snapshot.AirlockOuterDoors)
        {
            var state = snapshot.DoorStates.FirstOrDefault(s => s.DoorId == outerDoor.Id);
            // Just the airlock's own room, not the full ship - same scoping FromRooms/DoorTileCoords
            // themselves require (their own doc comments), since an AirlockOuterDoor sits on a
            // room's outer hull edge, not a shared boundary between two rooms in the list.
            var ownRoom = new[] { snapshot.Rooms.First(r => r.Id == outerDoor.RoomId) };
            var (left, top, width, height) = TileGridRasterizer.DoorTileRect(ownRoom, outerDoor.X, outerDoor.Y, outerDoor.Width, outerDoor.Height);
            DrawDoor(spriteBatch, left, top, width, height, state?.IsOpen ?? false, origin,
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

        // Cosmoteer-style marching engines (direct user request) - EngineState already carries its
        // own X/Y/Facing (no separate static list to cross-reference, unlike WallBlocks/WallBlockStates).
        foreach (var engine in snapshot.EngineStates ?? Array.Empty<EngineState>())
            DrawShipEngine(spriteBatch, engine, origin, totalSeconds);

        DrawReactorBlock(spriteBatch, snapshot.ReactorBlock, snapshot.Reactor, snapshot.ReactorLevers, openBlock.Kind == BlockKind.Reactor, origin, totalSeconds,
            snapshot.Rooms.FirstOrDefault(r => r.Id == snapshot.ReactorBlock.RoomId)?.Name);
        DrawDistributionBlock(spriteBatch, snapshot.DistributionBlock, openBlock.Kind == BlockKind.Distribution, origin, shipPowered);
        DrawReactorTrunkWires(spriteBatch,
            GetBlockRect(snapshot.ReactorBlock.Position, ReactorSize(snapshot.ReactorBlock), origin),
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
        DrawCardTable(spriteBatch, snapshot.CardTable, snapshot.CardGame is not null || snapshot.FrontsGame is not null, origin);
        if (snapshot.Jukebox is { } jukebox)
            DrawJukebox(spriteBatch, jukebox, openBlock.Kind == BlockKind.Jukebox, origin);
        if (snapshot.Terminal is { } terminal)
            DrawTerminal(spriteBatch, terminal, origin);

        foreach (var turret in snapshot.Turrets)
        {
            var state = snapshot.TurretStates.FirstOrDefault(s => s.Id == turret.Id);
            DrawTurret(spriteBatch, turret, state, snapshot.Rooms, snapshot.Turrets, origin, totalSeconds);
        }

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

    // Split out of Draw above (docked-station wall-clipping bug report) - the caller draws this as
    // its own, later pass so ship characters (and their floating nameplates) always end up on top
    // of a docked station's own geometry too, not just the ship's own walls. Draw() itself already
    // sequences floors-then-walls-then-devices correctly so a character never sank behind its own
    // ship's walls; the station is a second, entirely separate renderer invoked afterward
    // (Game1.cs), so a crewmate standing near the shared airlock boundary had their nameplate
    // partly painted over by the station's own hull art - moving the character pass to run after
    // both renderers fixes that regardless of which side the character is actually closer to.
    public void DrawCharacters(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, ChatBubbleTracker? chatBubbles = null)
    {
        foreach (var character in snapshot.Characters)
            DrawCharacter(spriteBatch, character, origin, chatBubbles?.BubbleFor(character.PlayerId));
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

    private static Vector2 EngineFacingStep(TileSide side) => side switch
    {
        TileSide.North => new Vector2(0, -1),
        TileSide.South => new Vector2(0, 1),
        TileSide.East => new Vector2(1, 0),
        TileSide.West => new Vector2(-1, 0),
        _ => Vector2.Zero,
    };

    // The user's real art (Content/Textures/Devices/Engine*.png) is drawn with its own business end
    // (the open port every part connects through) facing DOWN in the source image - South needs no
    // rotation, everything else derived from the standard rotation matrix against that baseline.
    private static float EngineArtRotation(TileSide side) => side switch
    {
        TileSide.South => 0f,
        TileSide.West => MathHelper.PiOver2,
        TileSide.North => MathHelper.Pi,
        TileSide.East => -MathHelper.PiOver2,
        _ => 0f,
    };

    // Cosmoteer-style marching engine (direct user request) - three tiles drawn as three distinct
    // pieces of hardware, not one icon repeated, so a hit on any single one reads immediately at a
    // glance: Control (the crew's own throttle lever - DeviceSkin.Face.Helm, a plain console with no
    // reason to look like the fiery business end), Bulkhead (the engine's own housing standing in
    // for the hull plate there - Face.Engine unlit, painted the same steel as everything else on the
    // deck until it's actually damaged). Nozzle is deliberately NOT drawn here - see DrawEngineNozzles
    // below, and its own doc comment, for why.
    private void DrawShipEngine(SpriteBatch spriteBatch, EngineState engine, Vector2 origin, float totalSeconds)
    {
        var step = EngineFacingStep(engine.Facing);
        var controlPos = new Vec2(engine.X, engine.Y);
        var bulkheadPos = new Vec2(engine.X + step.X, engine.Y + step.Y);

        // Bulkhead - full tile size, repainting whatever plain wall art ClientTileGrid already drew
        // at that same spot (it has no idea an engine lives there - Ship.cs's own constructor is
        // what actually drops the redundant WallBlock server-side; this is a client-side-only
        // repaint of the same tile, same "draw over it" approach DrawBreachedWallBlock already uses).
        var bulkheadRect = GetBlockRect(bulkheadPos, (int)PixelsPerUnit, origin);
        if (_engineBulkheadTexture is { } bulkheadTex)
        {
            spriteBatch.Draw(bulkheadTex, bulkheadRect, null, engine.BulkheadBroken ? new Color(255, 130, 130) : Color.White,
                EngineArtRotation(engine.Facing), new Vector2(bulkheadTex.Width / 2f, bulkheadTex.Height / 2f), SpriteEffects.None, 0f);
            DrawRectOutline(spriteBatch, bulkheadRect, engine.BulkheadBroken ? Color.Red : new Color(150, 155, 165), engine.BulkheadBroken ? 3 : 2);
        }
        else
        {
            DrawDeviceFace(spriteBatch, bulkheadRect, DeviceSkin.Face.Engine, lit: false,
                engine.BulkheadBroken ? Color.Red : new Color(150, 155, 165), engine.BulkheadBroken ? 3 : 2);
        }
        if (engine.BulkheadBroken)
        {
            DrawScorch(spriteBatch, bulkheadRect);
            DrawHazardStripes(spriteBatch, new Rectangle(bulkheadRect.X, bulkheadRect.Bottom - 4, bulkheadRect.Width, 4), horizontal: true);
        }

        // Control - same size/style as any other system-device box (DrawSystemDevice).
        var controlRect = GetBlockRect(controlPos, BigBlockSize, origin);
        if (_engineControlTexture is { } controlTex)
        {
            spriteBatch.Draw(controlTex, controlRect, null, engine.ControlBroken ? new Color(255, 130, 130) : Color.White,
                EngineArtRotation(engine.Facing), new Vector2(controlTex.Width / 2f, controlTex.Height / 2f), SpriteEffects.None, 0f);
            DrawRectOutline(spriteBatch, controlRect, engine.ControlBroken ? Color.Red : Color.LightSteelBlue, engine.ControlBroken ? 3 : 2);
        }
        else
        {
            DrawDeviceFace(spriteBatch, controlRect, DeviceSkin.Face.Helm, !engine.ControlBroken,
                engine.ControlBroken ? Color.Red : Color.LightSteelBlue, engine.ControlBroken ? 3 : 2);
        }
        if (engine.ControlBroken)
        {
            DrawScorch(spriteBatch, controlRect);
            DrawHazardStripes(spriteBatch, new Rectangle(controlRect.X, controlRect.Bottom - 3, controlRect.Width, 3), horizontal: true);
            spriteBatch.DrawString(_font, "!", new Vector2(controlRect.Center.X + BigBlockSize / 2f - 2, controlRect.Center.Y - BigBlockSize),
                Color.Red, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
        }
        DrawDeviceLabel(spriteBatch, controlRect, "Двигатель");
    }

    // A sustained rocket exhaust, not a torch - widens AWAY from the nozzle rather than narrowing to
    // a point the way DrawToolFlame's cutting/welding beam does, and flickers slowly enough to read
    // as a steady burn rather than a shower of sparks. `seed` staggers multiple engines' flicker
    // phase apart so a hull with several of them doesn't pulse in obvious unison.
    private void DrawEngineExhaust(SpriteBatch spriteBatch, Vector2 nozzleCenter, Vector2 direction, float totalSeconds, float seed)
    {
        if (direction.LengthSquared() < 0.001f)
            return;
        var rotation = MathF.Atan2(direction.Y, direction.X);
        var flicker = 0.85f + 0.15f * MathF.Sin(totalSeconds * 14f + seed);
        var length = PixelsPerUnit * (1.05f + 0.35f * flicker);

        void Layer(Color color, float lengthFraction, float width) =>
            spriteBatch.Draw(_pixel, nozzleCenter, null, color, rotation, new Vector2(0f, 0.5f),
                new Vector2(length * lengthFraction, width * flicker), SpriteEffects.None, 0f);

        Layer(new Color(255, 120, 20) * 0.30f, 1f, 26f);
        Layer(new Color(255, 170, 60) * 0.55f, 0.75f, 15f);
        Layer(new Color(255, 225, 140) * 0.85f, 0.42f, 7f);
        Layer(Color.White * 0.9f, 0.18f, 3f);
    }

    // Every marching engine's Nozzle, drawn as its OWN pass after the scene composite - the same
    // "goes on after the composite rather than into the scene" fix Game1.Vacuum.cs's own
    // DrawRcsPlume already needed for a suit's manoeuvring thrusters, for the identical underlying
    // reason: the sight-cone/room-lighting mask (Game1.Lighting.cs's BuildVisibilityMask) multiplies
    // the WHOLE captured scene, and Nozzle sits one tile past the hull in genuine open space no Room
    // ever covers - to that mask it reads exactly like the far side of an ordinary wall and gets
    // blacked out, even though it's the ship's own hardware and the player built it on purpose.
    // Confirmed live (a magenta test rect drawn from inside the masked pass never appeared on
    // screen, pixel-sampled directly) before writing this - not a coordinate bug, not GPU clipping.
    // Called from Game1.cs right where DrawRcsPlume already is, with its own Begin/End (nothing is
    // guaranteed open at that point in the frame) and the same sceneTransform so it tracks the
    // camera's own rotation/zoom exactly like everything drawn inside the masked pass does.
    public void DrawEngineNozzles(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, Matrix sceneTransform, float totalSeconds)
    {
        var engines = snapshot.EngineStates;
        if (engines is not { Count: > 0 })
            return;

        spriteBatch.Begin(transformMatrix: sceneTransform);
        foreach (var engine in engines)
        {
            var step = EngineFacingStep(engine.Facing);
            var nozzlePos = new Vec2(engine.X + step.X * 2, engine.Y + step.Y * 2);
            var nozzleRect = GetBlockRect(nozzlePos, BigBlockSize, origin);
            if (_engineNozzleTexture is { } nozzleTex)
            {
                var tint = engine.NozzleBroken ? new Color(255, 130, 130) : engine.IsThrusting ? Color.White : new Color(160, 160, 160);
                spriteBatch.Draw(nozzleTex, nozzleRect, null, tint, EngineArtRotation(engine.Facing),
                    new Vector2(nozzleTex.Width / 2f, nozzleTex.Height / 2f), SpriteEffects.None, 0f);
                DrawRectOutline(spriteBatch, nozzleRect, engine.NozzleBroken ? Color.Red : new Color(230, 140, 70), engine.NozzleBroken ? 3 : 2);
            }
            else
            {
                DrawDeviceFace(spriteBatch, nozzleRect, DeviceSkin.Face.Engine, engine.IsThrusting,
                    engine.NozzleBroken ? Color.Red : new Color(230, 140, 70), engine.NozzleBroken ? 3 : 2);
            }
            if (engine.NozzleBroken)
            {
                DrawScorch(spriteBatch, nozzleRect);
                DrawHazardStripes(spriteBatch, new Rectangle(nozzleRect.X, nozzleRect.Bottom - 3, nozzleRect.Width, 3), horizontal: true);
            }
        }
        spriteBatch.End();

        // The flame itself in its own additive-blended pass, same convention DrawRcsPlume's own
        // exhaust puffs use - a glow has no business being darkened by alpha-blended overdraw, and
        // additive over the housing just drawn above reads as the housing catching its own light.
        var anyThrusting = false;
        foreach (var engine in engines)
            if (engine.IsThrusting) { anyThrusting = true; break; }
        if (!anyThrusting)
            return;

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, transformMatrix: sceneTransform);
        foreach (var engine in engines)
        {
            if (!engine.IsThrusting)
                continue;
            var step = EngineFacingStep(engine.Facing);
            var nozzlePos = new Vec2(engine.X + step.X * 2, engine.Y + step.Y * 2);
            var nozzleRect = GetBlockRect(nozzlePos, BigBlockSize, origin);
            DrawEngineExhaust(spriteBatch, new Vector2(nozzleRect.Center.X, nozzleRect.Center.Y), step, totalSeconds, engine.X + engine.Y * 7f);
        }
        spriteBatch.End();
    }

    // Redesigned as one big spinning reactor core (the previous "Hullwright's Bench" housing -
    // chamfered box, terminal strip, twin tubes, mini transformer panels - was hand-tuned for the
    // old fixed ~40px icon and stopped reading as one machine once SizeScale started tracking a
    // catalog/editor room's own size (Ship.Custom.cs) instead: at several times that scale the
    // pieces just floated apart, worst of all the old cooling turbine drawn hanging off the
    // housing's OWN bottom edge, which at a big SizeScale lands well outside the housing entirely
    // and reads as a second, disconnected object in the middle of the room. This version is a
    // single circle centred on block.Position (matching Ship.DeviceObstacles' own centre exactly),
    // sized to fill its own square rect - simple enough to actually scale.
    private void DrawReactorBlock(SpriteBatch spriteBatch, ReactorBlock block, ReactorState reactor, ReactorLeverState levers, bool isOpen, Vector2 origin, float totalSeconds, string? roomName)
    {
        // A catalog/editor room with its own reference art already draws the whole machine baked
        // into the room's own texture (RoomDecor's own "texture doubles as the device" rule, same
        // as an engine/turret/camera room) - no separate icon, housing or levers drawn on top of it,
        // just a plain outline around the exact same rect the click-to-open check (Game1.Input.cs)
        // and the walking obstacle (Ship.DeviceObstacles) already use, so the interactive area the
        // art doubles as is visible at a glance instead of an invisible zone floating over the room.
        if (RoomDecor.HasCatalogTexture(roomName))
        {
            var artRect = GetBlockRect(block.Position, ReactorSize(block), origin);
            DrawComplexReactorOutline(spriteBatch, artRect, isOpen ? Color.Gold : Color.White * 0.85f, isOpen ? 3f : 2f);
            return;
        }

        var rect = GetBlockRect(block.Position, ReactorSize(block), origin);
        var running = reactor.CurrentOutput > 0;
        var glowColor = running ? new Color(63, 184, 232) : new Color(40, 50, 55);
        var borderColor = isOpen ? Color.Gold : running ? Color.Orange : Color.Gray;

        var center = new Vector2(rect.Center.X, rect.Center.Y);
        var radius = Math.Min(rect.Width, rect.Height) / 2f;

        // Direct user request - a real reactor texture (Content/Textures/Devices/Reactor.png, filling
        // the whole 4x4-unit block) instead of the old procedural rings/turbine, everywhere in the
        // game. Its own glowing cells already read as "running"; when the reactor is off, tinting it
        // down (rather than trying to fake a separate lit/unlit texture) reads as "the same machine,
        // powered down" - same idea the old procedural glowColor swap already used.
        if (_reactorTexture is { } reactorTex)
        {
            var tint = running ? Color.White : new Color(90, 90, 90);
            spriteBatch.Draw(reactorTex, rect, tint);
            DrawRectOutline(spriteBatch, rect, borderColor, isOpen ? 3 : 2);
        }
        else
        {
            // Outer ring (border colour showing through) behind a slightly smaller face - reads as a
            // rimmed housing without a separate outline draw call.
            HudIcons.FillCircle(spriteBatch, _pixel, center, radius, borderColor);
            HudIcons.FillCircle(spriteBatch, _pixel, center, radius - Math.Max(2f, radius * 0.05f),
                running ? Color.DarkOrange * 0.55f : Color.DimGray * 0.6f);

            // Cooling turbine, unchanged in spirit from the old design (blades spin while running,
            // freeze the instant the reactor lever cuts output) - now the reactor's own core instead of
            // a separate part hanging off its housing.
            HudIcons.FillCircle(spriteBatch, _pixel, center, radius * 0.62f, glowColor * (running ? 0.22f : 0.1f));
            HudIcons.FillCircle(spriteBatch, _pixel, center, radius * 0.42f, Color.Black * 0.75f);
            if (running)
            {
                var spin = totalSeconds * 3f;
                for (var i = 0; i < 4; i++)
                {
                    var angle = spin + i * MathF.PI / 2f;
                    var tip = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 0.38f;
                    HudIcons.DrawLine(spriteBatch, _pixel, center, tip, glowColor, Math.Max(2f, radius * 0.06f));
                }
            }
            HudIcons.FillCircle(spriteBatch, _pixel, center, radius * 0.14f,
                (running ? Color.Yellow : Color.DarkSlateGray) * (running ? 0.7f : 0.4f));
        }

        var reactorLabelPos = new Vector2(rect.X + 4, rect.Y + 4);
        DrawLabelBacking(spriteBatch, "Реактор", reactorLabelPos, 0.6f);
        spriteBatch.DrawString(_font, "Реактор", reactorLabelPos, Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

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
        DrawDeviceLabel(spriteBatch, rect, "Навигационная панель");
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

    // A wall terminal - no panel of its own (one click on the block is the whole toggle gesture),
    // so nothing here ever draws an "isOpen" highlight the way the reactor/jukebox do. Off dims the
    // whole baked face to gray, the same convention DrawReactorBlock uses for its own texture.
    private void DrawTerminal(SpriteBatch spriteBatch, TerminalState terminal, Vector2 origin)
    {
        var rect = GetBlockRect(terminal.Block.Position, TerminalBlockSize, origin);
        var tint = terminal.On ? Color.White : new Color(90, 90, 90);
        spriteBatch.Draw(_terminalTexture, rect, tint);
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
        bool leadsToVacuum = false, bool destroyed = false, float totalSeconds = 0f) =>
        DrawDoor(spriteBatch, GetDoorRect(left, top, width, height, origin), isOpen, leadsToVacuum, destroyed, totalSeconds);

    // Rect-based overload (Game1.ShipEditor.Draw.cs's own DrawEditorDoorTile, direct user request -
    // "дверь своей моделькой, а не голым квадратом") - the editor has no world-unit origin/PixelsPerUnit
    // mapping of its own to feed the position-based overload above, but it already computes exactly
    // the merged (for a 2-tile wide door) or single screen rect its own placeholder used to fill flat,
    // so this skips straight to drawing the real door art at that rect instead of re-deriving one.
    internal void DrawDoor(SpriteBatch spriteBatch, Rectangle rect, bool isOpen,
        bool leadsToVacuum = false, bool destroyed = false, float totalSeconds = 0f)
    {
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

    // Bronze/brass housing (direct user request, matching a reference screenshot) - a plain flat
    // fill read as too flat next to the leaf's own top-lit bands below, so the bezel gets one too:
    // a lighter strip along its top edge standing in for the same single overhead light source.
    private static readonly Color DoorFrameLit = new(140, 107, 74);
    private static readonly Color DoorFrame = new(90, 68, 46);
    private static readonly Color DoorFrameDestroyed = new(92, 80, 72);

    private void DrawDoorFrame(SpriteBatch spriteBatch, Rectangle rect, bool destroyed)
    {
        const int margin = 5;
        var bezel = new Rectangle(rect.X - margin, rect.Y - margin, rect.Width + margin * 2, rect.Height + margin * 2);
        if (destroyed)
        {
            spriteBatch.Draw(_pixel, bezel, DoorFrameDestroyed);
        }
        else
        {
            spriteBatch.Draw(_pixel, bezel, DoorFrame);
            var litHeight = Math.Max(2, bezel.Height / 4);
            spriteBatch.Draw(_pixel, new Rectangle(bezel.X, bezel.Y, bezel.Width, litHeight), DoorFrameLit);
        }
        DrawRivets(spriteBatch, bezel);
    }

    // Warm painted-metal panel (direct user request, matching a reference screenshot) - a top-lit
    // gradient (approximated as flat bands, same "no image assets" convention as everywhere else in
    // this file) plus a stepped diagonal brace on each leaf half. Every offset is a fraction of
    // rect/halfWidth rather than a fixed pixel count, so the exact same drawing already scales
    // correctly whether this door spans one tile or two (Door.Width/Height, ShipRenderer.GetDoorRect) -
    // no separate "single" vs "double" art or code path needed.
    private static readonly Color[] DoorPanelBands = { new(224, 128, 80), new(200, 98, 60), new(168, 78, 48), new(136, 60, 36) };
    private static readonly Color DoorSeam = new(110, 50, 32);
    private static readonly Color DoorBrace = new(92, 44, 24);

    private void DrawClosedDoorLeaf(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, bool leadsToVacuum)
    {
        DrawTopLitBands(spriteBatch, rect, horizontal, DoorPanelBands);

        // The center seam - between the two leaf halves, exactly where DrawDoorLeafCaps' own split
        // already reads as "open" for the same door, so closed and open agree on where the leaf
        // actually divides.
        const int seamThickness = 4;
        if (horizontal)
            spriteBatch.Draw(_pixel, new Rectangle(rect.Center.X - seamThickness / 2, rect.Y, seamThickness, rect.Height), DoorSeam);
        else
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Center.Y - seamThickness / 2, rect.Width, seamThickness), DoorSeam);

        DrawDoorBraces(spriteBatch, rect, horizontal, leadsToVacuum);
    }

    // Fills rect with `bands.Length` equal strips along the SHORT axis (top-to-bottom for a
    // horizontal door, left-to-right for a vertical one - always across the leaf's own thickness,
    // never along its length), lightest band first - the same "one overhead light source" language
    // DrawDoorFrame's own lit strip above uses.
    private void DrawTopLitBands(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, Color[] bands)
    {
        var span = horizontal ? rect.Height : rect.Width;
        for (var i = 0; i < bands.Length; i++)
        {
            var from = i * span / bands.Length;
            var to = (i + 1) * span / bands.Length;
            var band = horizontal
                ? new Rectangle(rect.X, rect.Y + from, rect.Width, to - from)
                : new Rectangle(rect.X + from, rect.Y, to - from, rect.Height);
            spriteBatch.Draw(_pixel, band, bands[i]);
        }
    }

    // One stepped (not smooth) diagonal per leaf half, mirrored around the center seam - reads as a
    // welded cross-brace at a glance, blocky enough to sit comfortably next to the banded fill
    // above rather than looking like a stray anti-aliased line.
    private void DrawDoorBraces(SpriteBatch spriteBatch, Rectangle rect, bool horizontal, bool leadsToVacuum)
    {
        const int steps = 6;
        var halfLength = (horizontal ? rect.Width : rect.Height) / 2f;
        var acrossExtent = horizontal ? rect.Height : rect.Width;
        // Divides the leaf's own thickness exactly (never overshoots it), independent of how far
        // along the diagonal actually runs - the two axes only need to agree on step COUNT, not size.
        var acrossStep = Math.Max(1, acrossExtent / steps);
        var thickness = Math.Max(2, acrossStep);
        var alongStep = Math.Max(2, (int)(halfLength * 0.7f / steps));

        void Brace(float lengthStart, int direction)
        {
            for (var i = 0; i < steps; i++)
            {
                var alongLeaf = lengthStart + direction * i * alongStep;
                var acrossLeaf = i * acrossStep;
                var pos = horizontal
                    ? new Rectangle(rect.X + (int)alongLeaf, rect.Y + acrossLeaf, alongStep, thickness)
                    : new Rectangle(rect.X + acrossLeaf, rect.Y + (int)alongLeaf, thickness, alongStep);
                spriteBatch.Draw(_pixel, pos, DoorBrace);
            }
        }

        // First half: brace runs from its near end inward; second half mirrors it outward from the
        // seam - together they form the same "V per half" shape regardless of how wide either half
        // actually is (one tile or two), since every offset above is relative to halfLength.
        Brace(halfLength * 0.15f, 1);
        Brace(halfLength * 0.85f, -1);
        Brace(halfLength * 1.15f, 1);
        Brace(halfLength * 1.85f, -1);

        // leadsToVacuum keeps a thin hazard edge (the one place stripes still earn their keep - the
        // functional "this one opens onto vacuum" signal, not decoration) rather than the old
        // all-doors-get-stripes treatment.
        if (leadsToVacuum)
            DrawDoorEdgeStripes(spriteBatch, rect, horizontal, 5);
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

        // Content-каталог отсеков - a catalog room with real reference art draws that instead of
        // the procedural stack below: the whole floor/walls/equipment are already baked into the
        // one image. Falls through to the ordinary procedural room for every hand-authored hull
        // (room.Name never matches a catalog entry there) and for the 2 plain "empty" catalog
        // shells, which never got reference art.
        if (!RoomDecor.TryDrawCatalogTexture(spriteBatch, rect, room.Name))
        {
            // Plates rather than one repeated stamp, and the seams are cut into them rather than
            // drawn over the top - which is why DrawFloorGrating is gone: its hairline grid had no
            // depth, so it read as printed on. The gunnery and reactor compartments get their own
            // field inside the same frame, so they are recognisably different rooms on recognisably
            // the same ship.
            //
            // Indexed from the ship's own origin, so which plate lands where belongs to the ship and
            // the pattern does not crawl across the deck when the camera moves.
            DeckPlates.DrawTiled(spriteBatch, _deckPlates[DeckPlates.For(room.Id)], rect, Color.White,
                new Point((int)origin.X, (int)origin.Y));
            // Dirt across the plates, which is the one thing that hides the tile grid - nothing
            // painted inside a tile can, because it repeats with the tile.
            DeckPlates.DrawGrime(spriteBatch, _deckGrime, rect, room.Id);
            RoomDecor.DrawLightPool(spriteBatch, _pixel, rect, accent);
            RoomDecor.DrawFurniture(spriteBatch, _pixel, rect, room.Id, accent);
        }

        var deficit = Math.Clamp((100f - oxygen) / 100f, 0f, 1f);
        if (deficit > 0f)
            spriteBatch.Draw(_pixel, rect, Color.Red * (deficit * 0.5f));

        // Compartment name on a painted plate in the department's own colour, the way a bulkhead is
        // actually stencilled - a bare label floating on the deck reads as a debug overlay.
        //
        // Sized from the font's own real measurement, not a per-character guess (the old
        // `34 + room.Name.Length * 9` under- or over-shot depending on the actual glyph widths) - a
        // narrow room with a long name used to spill text straight past its own plate into whatever
        // drew next (a wall, the neighbouring room), leaving only a fragment of the name legible.
        // If it still doesn't fit even at the smallest useful size, the name shrinks to match instead
        // of overflowing - a slightly smaller label beats a truncated-looking one.
        var nameScale = 0.7f;
        var textSize = _font.MeasureString(room.Name) * nameScale;
        var maxTextWidth = rect.Width - 24f;
        if (textSize.X > maxTextWidth && textSize.X > 0f)
        {
            nameScale *= MathHelper.Clamp(maxTextWidth / textSize.X, 0.5f, 1f);
            textSize = _font.MeasureString(room.Name) * nameScale;
        }
        var plate = new Rectangle(rect.X + 8, rect.Y + 8, (int)Math.Min(rect.Width - 16, textSize.X + 20), 20);
        spriteBatch.Draw(_pixel, plate, accent * 0.22f);
        spriteBatch.Draw(_pixel, new Rectangle(plate.X, plate.Y, 3, plate.Height), accent * 0.8f);
        spriteBatch.DrawString(_font, room.Name, new Vector2(rect.X + 14, rect.Y + 10), Color.LightSteelBlue, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);

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
    // Kept fully intact for BoardingRenderer/StationRenderer (an enemy hull's/station's own rooms
    // aren't part of the client's per-tile grid - ClientTileGrid.Build only ever rasterizes the
    // player's OWN Ship.Rooms/Doors/AirlockOuterDoors from WorldSnapshot). The player's own ship no
    // longer calls this for its walls (M75, humble-soaring-cat.md) - see DrawRoomWallLamps/
    // DrawShipWalls below and this method's own call site in Draw().
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

    // M75 - the player's own ship's wall LAMPS only (still per-room decor, unaffected by the tile
    // rework); the actual wall art is now DrawShipWalls below, driven by a real per-tile grid instead
    // of one band per room edge.
    internal void DrawRoomWallLamps(SpriteBatch spriteBatch, Room room, float oxygen, Vector2 origin, Color? accentOverride = null)
    {
        var rect = GetRoomRect(room, origin);
        var alarmed = oxygen < 70f;
        var accent = accentOverride ?? RoomDecor.Accent(room.Id, room.Name);
        RoomDecor.DrawWallLamps(spriteBatch, _pixel, rect, accent, alarmed);
    }

    // M75 (humble-soaring-cat.md) - real per-tile wall rendering: rebuilds the exact same tile shape
    // Ship.Tiles has (ClientTileGrid.Build, a pure function of Rooms/Doors/AirlockOuterDoors - no new
    // protocol field needed) and draws one tile-sized square per Solid wall cell, oriented by which
    // of its 4 neighbors are also wall-kind (a door counts as "wall" for orientation - same material
    // either side of it). Door tiles themselves are skipped entirely - DrawDoor already draws them,
    // unchanged. Breached walls are also skipped nothing special here either - DrawBreachedWallBlock
    // already punches its own hole/hazard-stripe visual on top of whatever's drawn underneath, at the
    // WallBlock's own position, so it reads correctly over the new art with no changes on its side.
    // Reinforced/Window (direct user request, humble-soaring-cat.md M76 follow-up "варианты стен")
    // reuse the same wall textures, just tinted - no bespoke art exists for either variant yet, same
    // convention the Ship Editor's own canvas/palette already use for it.
    private static Color WallMaterialTint(WallMaterial material) => material switch
    {
        WallMaterial.Reinforced => new Color(150, 155, 165),
        WallMaterial.Window => new Color(150, 215, 235) * 0.75f,
        _ => Color.White,
    };

    internal void DrawShipWalls(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        var tiles = ClientTileGrid.Build(snapshot);
        // Material lives on the WallBlock itself (WallBlock.cs), not on the tile grid (a pure
        // projection of Rooms/Doors/AirlockOuterDoors, no WallBlock input - see ClientTileGrid's own
        // doc comment) - matched back to a tile coordinate via the same WallBlockTileCoord mapping
        // World.TileSync.cs already uses server-side, so this can never disagree with which block
        // actually owns that position.
        var materialByTile = snapshot.WallBlocks
            .Where(b => b.Material != WallMaterial.Standard)
            .Select(b => (Coord: TileGridRasterizer.WallBlockTileCoord(b, snapshot.Rooms, snapshot.Rooms.First(r => r.Id == b.RoomId)), b.Material))
            .ToDictionary(x => x.Coord, x => x.Material);
        foreach (var (coord, cell) in tiles.Cells)
        {
            if (cell.Wall != TileWallKind.Solid)
                continue; // None = no wall; Door is drawn separately by the existing DrawDoor calls
            DrawWallTile(spriteBatch, tiles, coord, origin, materialByTile.GetValueOrDefault(coord, WallMaterial.Standard));
        }
    }

    private void DrawWallTile(SpriteBatch spriteBatch, TileGrid tiles, TileCoord coord, Vector2 origin, WallMaterial material = WallMaterial.Standard)
    {
        var tint = WallMaterialTint(material);
        bool HasWall(TileSide side) => tiles.CellAt(side.Offset(coord)) is { Wall: TileWallKind.Solid or TileWallKind.Door };

        var north = HasWall(TileSide.North);
        var south = HasWall(TileSide.South);
        var east = HasWall(TileSide.East);
        var west = HasWall(TileSide.West);

        var unit = (int)PixelsPerUnit;
        var center = origin + new Vector2((coord.X + 0.5f) * PixelsPerUnit, (coord.Y + 0.5f) * PixelsPerUnit);

        // A T-junction (exactly 3 wall-kind neighbors - the free-form tile editor can produce these
        // even though no rectangular hand-authored hull ever did) has to be checked BEFORE the plain
        // straight-run tests below, since 3 neighbors always include one opposite pair and would
        // otherwise silently read as a plain straight tile, ignoring the third branch entirely.
        var neighborCount = (north ? 1 : 0) + (south ? 1 : 0) + (east ? 1 : 0) + (west ? 1 : 0);
        if (neighborCount == 3 && _wallTJunctionTexture is { } tTex)
        {
            // Base art has the missing/open side facing North (a horizontal run continuing East+West
            // with a spur branching South) - rotate 90° per step clockwise to whichever side is
            // actually the open one here, same convention as the corner/end-cap rotations above.
            var tRotation = !north ? 0f : !east ? MathHelper.PiOver2 : !south ? MathHelper.Pi : -MathHelper.PiOver2;
            var tOrigin = new Vector2(tTex.Width / 2f, tTex.Height / 2f);
            spriteBatch.Draw(tTex, new Rectangle((int)center.X, (int)center.Y, unit, unit), null, tint,
                tRotation, tOrigin, SpriteEffects.None, 0f);
            return;
        }
        if (north && south && _wallVerticalTexture is { } vTex)
        {
            spriteBatch.Draw(vTex, new Rectangle((int)center.X - unit / 2, (int)center.Y - unit / 2, unit, unit), tint);
            return;
        }
        if (east && west && _wallHorizontalTexture is { } hTex)
        {
            spriteBatch.Draw(hTex, new Rectangle((int)center.X - unit / 2, (int)center.Y - unit / 2, unit, unit), tint);
            return;
        }
        // A dead end (exactly one wall-kind neighbor) reads wrong with the corner texture (a "turn"
        // where the wall actually just stops) - direct user report. Base end-cap art connects South,
        // caps at North; rotate the same 90°-per-step clockwise convention the corner uses.
        if (neighborCount == 1 && _wallEndCapTexture is { } capTex)
        {
            var capRotation = south ? 0f : west ? MathHelper.PiOver2 : north ? MathHelper.Pi : -MathHelper.PiOver2;
            var capOrigin = new Vector2(capTex.Width / 2f, capTex.Height / 2f);
            spriteBatch.Draw(capTex, new Rectangle((int)center.X, (int)center.Y, unit, unit), null, tint,
                capRotation, capOrigin, SpriteEffects.None, 0f);
            return;
        }
        if (_wallCornerTexture is { } cTex)
        {
            // Base art turns South-then-East (a room's own top-left corner, per the corner tile's
            // own construction - vertical texture bottom-left, horizontal top-right). Rotate 90° per
            // corner clockwise from there; a fully isolated tile (zero neighbors - vanishingly rare)
            // has no better single answer yet, so it falls back to the same base orientation.
            var rotation = (south, east, west, north) switch
            {
                (true, true, _, _) => 0f,
                (true, _, true, _) => MathHelper.PiOver2,
                (_, _, true, true) => MathHelper.Pi,
                (_, true, _, true) => -MathHelper.PiOver2,
                _ => 0f,
            };
            var texOrigin = new Vector2(cTex.Width / 2f, cTex.Height / 2f);
            spriteBatch.Draw(cTex, new Rectangle((int)center.X, (int)center.Y, unit, unit), null, tint,
                rotation, texOrigin, SpriteEffects.None, 0f);
            return;
        }

        // No new art loaded at all (Content missing) - a single procedural tile-sized square, same
        // material HullSkin/the old wall band used, just without the per-room alarm/conduit overlay
        // that band drawing had (this path is only ever reached if the .mgcb build is broken, so it's
        // not worth threading room/alarm context through a tile loop for it).
        var fallbackRect = new Rectangle((int)center.X - unit / 2, (int)center.Y - unit / 2, unit, unit);
        TileTextures.DrawSquares(spriteBatch, _hullPlates, TileTextures.HullTileSize, unit, fallbackRect, tint, new Point((int)origin.X, (int)origin.Y));
    }

    // Debug aid (M74 follow-up, humble-soaring-cat.md) - draws a bold outline around every 1-unit
    // tile cell within each room, held up by the Ъ key (Game1.cs). Computed straight from Room
    // rectangles (1 tile = 1 world unit, by design) rather than reading Ship.Tiles itself, which the
    // client has no access to at all - WorldSnapshot never sends it, nothing outside tests reads it
    // yet (Ship.cs's own doc comment on Tiles) - so this stays a pure visualization, not a read of
    // the real tile grid's actual wall/floor/device content.
    internal void DrawTileGridOverlay(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        const int thickness = 3;
        var unit = (int)PixelsPerUnit;
        foreach (var room in snapshot.Rooms)
        {
            var rect = GetRoomRect(room, origin);
            for (var x = rect.X; x <= rect.Right; x += unit)
                spriteBatch.Draw(_pixel, new Rectangle(x - thickness / 2, rect.Y, thickness, rect.Height), Color.Black);
            for (var y = rect.Y; y <= rect.Bottom; y += unit)
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, y - thickness / 2, rect.Width, thickness), Color.Black);
        }
    }

    // M75 (humble-soaring-cat.md) - one full panel per REAL 1-unit tile along the band's length,
    // not a cosmetic repeat period of its own (the old TileTextures.DrawTiled(..., WallThickness, ...)
    // call repeated every 28px, which doesn't correspond to anything - visually the panel motif never
    // lined up with an actual game tile). A room's own edges always start on an exact tile boundary
    // (Room.X/Y are whole or half units, scaled by PixelsPerUnit), so starting the repeat at the
    // band's own origin - no extra phase correction - already lines up with the real grid. Each cell
    // samples the texture's FULL source square stretched to fill the destination, same "whole design
    // in miniature" idea TileTextures.DrawSquares already uses for the procedural plate.
    private void DrawWallPanels(SpriteBatch spriteBatch, Texture2D texture, Rectangle band, bool horizontal)
    {
        var unit = (int)PixelsPerUnit;
        var source = new Rectangle(0, 0, texture.Width, texture.Height);
        if (horizontal)
        {
            for (var x = band.X; x < band.Right; x += unit)
            {
                var w = Math.Min(unit, band.Right - x);
                spriteBatch.Draw(texture, new Rectangle(x, band.Y, w, band.Height), source, Color.White);
            }
        }
        else
        {
            for (var y = band.Y; y < band.Bottom; y += unit)
            {
                var h = Math.Min(unit, band.Bottom - y);
                spriteBatch.Draw(texture, new Rectangle(band.X, y, band.Width, h), source, Color.White);
            }
        }
    }

    private void DrawWallBand(SpriteBatch spriteBatch, Rectangle band, bool horizontal, bool alarmed, Vector2 origin)
    {
        // Hand-made panel art, once loaded (SetWallTextures) - drawn as its own complete design, no
        // alarm/conduit/rib overlay (that dressing was built for the flat procedural plate below, and
        // would just clutter artwork that already carries its own detail).
        var wallTexture = horizontal ? _wallHorizontalTexture : _wallVerticalTexture;
        if (wallTexture is not null)
        {
            DrawWallPanels(spriteBatch, wallTexture, band, horizontal);
            return;
        }

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
        if (_wallCornerTexture is not null)
        {
            // M75 - sized to one full real tile (matching DrawWallPanels's straight-run pitch), not
            // the old procedural corner's own smaller WallThickness+6 footprint, so the corner reads
            // as the same size tile as the straight runs either side of it. A single stamp, not
            // tiled - a corner only ever appears once per corner.
            var unit = (int)PixelsPerUnit;
            var texRect = new Rectangle(x - unit / 2, y - unit / 2, unit, unit);
            spriteBatch.Draw(_wallCornerTexture, texRect, Color.White);
            return;
        }

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
    internal void DrawCharacter(SpriteBatch spriteBatch, CharacterState character, Vector2 origin,
        (string Text, float Alpha)? chatBubble = null)
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

        DrawHeldItems(spriteBatch, _pixel, _font, HeldItemTypes(character.Inventory), center, facing);

        if (character.CarryingAmmoCrate)
        {
            const int crateSize = 8;
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - crateSize / 2, rect.Top - crateSize / 2, crateSize, crateSize), Color.SaddleBrown);
        }

        if (character.SuitActionRemaining > 0)
            spriteBatch.DrawString(_font, "...", new Vector2(rect.X, rect.Bottom + 2), Color.CadetBlue, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        if (chatBubble is { } bubble)
            DrawChatBubble(spriteBatch, bubble.Text, bubble.Alpha, new Vector2(center.X, rect.Y - 44));
    }

    // Every crew nameplate, in one later pass over the whole snapshot - deliberately NOT part of
    // DrawCharacter/DrawCharacters above. Those draw inside the lit scene batch (captured for
    // ScenePost, multiplied by the room-lighting/sight mask); a nameplate that shares that batch
    // goes dark right along with whatever real shadow the character happens to be standing in - a
    // once-subtle seam that the darker post-Barotrauma-pass PoweredFloor made an actual bug report
    // ("Eisenhorn (Капитан)" fading into a wall's own cast shadow). Direct user request: a
    // nameplate's whole job is identifying who this is, so it must read the same whether they are
    // standing in a lit room or a dark one - the caller draws this call site AFTER ScenePost.Present
    // (Game1.cs), once the lighting multiply has already been applied to everything else.
    public void DrawCharacterLabels(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, Matrix sceneTransform)
    {
        spriteBatch.Begin(transformMatrix: sceneTransform);
        foreach (var character in snapshot.Characters)
            DrawCharacterLabel(spriteBatch, character, origin);
        spriteBatch.End();
    }

    private void DrawCharacterLabel(SpriteBatch spriteBatch, CharacterState character, Vector2 origin)
    {
        var size = (int)(CharacterDiameter * PixelsPerUnit);
        var center = new Vector2(origin.X + (float)character.X * PixelsPerUnit, origin.Y + (float)character.Y * PixelsPerUnit);
        var rect = new Rectangle((int)center.X - size / 2, (int)center.Y - size / 2, size, size);

        // The crew panel's role picker (CrewPanel.GetOwnRoleIconRect) is the only way a live
        // player's Role ever gets set - drawing the same glyph HudIcons.DrawRoleGlyph already
        // gives CrewPanel/InfoPanel rows here is what makes that choice visible in the ship view
        // itself, for a bot's fixed Role too since both read from the same field.
        //
        // Drawn before the nameplate below (not after, as this used to be) - a long name can extend
        // far enough to pass under this glyph's own position, and the plate the nameplate now has
        // needs to be the last thing painted there so it always wins, not whichever happened to be
        // drawn most recently.
        if (character.Role is { } headRole)
            HudIcons.DrawRoleGlyph(spriteBatch, _pixel, new Vector2(center.X, rect.Y - 26), 0.5f,
                character.IsBot ? Color.LightSkyBlue : Color.White, headRole);

        // A human crewmate reads the same way a hired bot does - name floating over the head,
        // always on, not just when hovered - so telling a crew of several players apart doesn't
        // depend on remembering whose colour is whose. Once they've picked their own Role from
        // CrewPanel, it's shown in the name label too, the same way a bot's already is above.
        //
        // Backed by the same opaque plate every other floating label in this file uses
        // (DrawLabelBacking) - previously bare text, so standing near a device (whose own label
        // already has a backing) or another crewmate produced two sets of glyphs painted straight
        // over each other, unreadable regardless of which one was technically drawn last.
        if (character.IsBot && character.Role is { } role)
        {
            var text = $"{character.BotName} ({CrewRoles.Name(role)})";
            var position = new Vector2(rect.X - 10, rect.Y - 14);
            DrawLabelBacking(spriteBatch, text, position, 0.45f);
            spriteBatch.DrawString(_font, text, position, Color.LightSkyBlue, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }
        else if (!character.IsBot && character.Nickname is { Length: > 0 } nickname)
        {
            var text = character.Role is { } playerRole ? $"{nickname} ({CrewRoles.Name(playerRole)})" : nickname;
            var position = new Vector2(rect.X - 10, rect.Y - 14);
            DrawLabelBacking(spriteBatch, text, position, 0.45f);
            spriteBatch.DrawString(_font, text, position, Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }
    }

    // A speech bubble above the sender (direct user request, "как в Баротравме", ChatBubbleTracker) -
    // positioned further above the head than the permanent nameplate/role glyph so the two never
    // overlap. A bubble is a brief announcement, not the full chat log, so long text is truncated.
    private void DrawChatBubble(SpriteBatch spriteBatch, string text, float alpha, Vector2 anchorBottomCenter)
    {
        const int maxChars = 40;
        if (text.Length > maxChars)
            text = text[..maxChars] + "…";

        const float scale = 0.45f;
        var size = _font.MeasureString(text) * scale;
        var padding = new Vector2(6, 4);
        var boxSize = size + padding * 2;
        var boxOrigin = new Vector2(anchorBottomCenter.X - boxSize.X / 2f, anchorBottomCenter.Y - boxSize.Y);

        spriteBatch.Draw(_pixel, new Rectangle((int)boxOrigin.X, (int)boxOrigin.Y, (int)boxSize.X, (int)boxSize.Y), Color.Black * (0.6f * alpha));
        spriteBatch.DrawString(_font, text, boxOrigin + padding, Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness) =>
        DrawRectOutline(spriteBatch, _pixel, rect, color, thickness);

    // Traces the reference art's own actual panel silhouette (visible up close in reactor.png) -
    // chamfered corners PLUS a stepped notch cut into the middle of each straight edge, not a plain
    // rectangle/octagon/rounded-rect. For a device whose face is already fully drawn by something
    // else (a room's own reference art) and only needs its own interactive footprint traced, not a
    // second housing drawn on top of the picture.
    private void DrawComplexReactorOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, float thickness)
    {
        var chamfer = Math.Max(3, Math.Min(rect.Width, rect.Height) / 6);
        var notchDepth = Math.Max(2, chamfer / 2);
        var notchWidth = Math.Max(4, chamfer);
        var cx = rect.Center.X;
        var cy = rect.Center.Y;

        Span<Vector2> vertices = stackalloc Vector2[]
        {
            // Top edge: left chamfer -> centred notch (dips down into the panel) -> right chamfer.
            new(rect.X + chamfer, rect.Y),
            new(cx - notchWidth / 2f, rect.Y), new(cx - notchWidth / 2f, rect.Y + notchDepth),
            new(cx + notchWidth / 2f, rect.Y + notchDepth), new(cx + notchWidth / 2f, rect.Y),
            new(rect.Right - chamfer, rect.Y),
            // Top-right chamfer, then right edge with its own centred notch (dips left).
            new(rect.Right, rect.Y + chamfer),
            new(rect.Right, cy - notchWidth / 2f), new(rect.Right - notchDepth, cy - notchWidth / 2f),
            new(rect.Right - notchDepth, cy + notchWidth / 2f), new(rect.Right, cy + notchWidth / 2f),
            new(rect.Right, rect.Bottom - chamfer),
            // Bottom-right chamfer, then bottom edge with its own centred notch (dips up).
            new(rect.Right - chamfer, rect.Bottom),
            new(cx + notchWidth / 2f, rect.Bottom), new(cx + notchWidth / 2f, rect.Bottom - notchDepth),
            new(cx - notchWidth / 2f, rect.Bottom - notchDepth), new(cx - notchWidth / 2f, rect.Bottom),
            new(rect.X + chamfer, rect.Bottom),
            // Bottom-left chamfer, then left edge with its own centred notch (dips right).
            new(rect.X, rect.Bottom - chamfer),
            new(rect.X, cy + notchWidth / 2f), new(rect.X + notchDepth, cy + notchWidth / 2f),
            new(rect.X + notchDepth, cy - notchWidth / 2f), new(rect.X, cy - notchWidth / 2f),
            new(rect.X, rect.Y + chamfer),
        };
        for (var i = 0; i < vertices.Length; i++)
            HudIcons.DrawLine(spriteBatch, _pixel, vertices[i], vertices[(i + 1) % vertices.Length], color, thickness);
    }

    internal static void DrawRectOutline(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
