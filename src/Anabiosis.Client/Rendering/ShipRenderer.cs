using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Draws the ship's rooms and characters with a fixed top-down camera (no follow/zoom yet — M2 scope).
public sealed partial class ShipRenderer
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
    public static Rectangle GetBlockRect(Vec2 worldPosition, int size, Vector2 origin) =>
        GetBlockRect(worldPosition, size, size, origin);

    // Direct user request ("чтобы приборы в игре имели размеры как в редакторе") - a non-square
    // overload, since a device's own real footprint (CustomDeviceFootprint.Size) isn't always
    // square (Helm/Navigation 3x2) - the square overload above just forwards into this one with
    // width==height, so every existing caller keeps compiling unchanged.
    public static Rectangle GetBlockRect(Vec2 worldPosition, int width, int height, Vector2 origin)
    {
        // Truncated the same way GetRoomRect is, and for the same reason: rounding the sum lets a
        // device drift a pixel back and forth against the deck it is bolted to as the camera moves.
        var centerX = (int)origin.X + (int)(worldPosition.X * PixelsPerUnit);
        var centerY = (int)origin.Y + (int)(worldPosition.Y * PixelsPerUnit);
        return new Rectangle(centerX - width / 2, centerY - height / 2, width, height);
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
        DrawWreckPatches(spriteBatch, snapshot, origin);

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
            DrawDoor(spriteBatch, left, top, width, height, door.IsVertical, state?.IsOpen ?? true, origin,
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
            // AirlockOuterDoor has no Vertical field of its own (unlike Door) - same Width<=Height
            // fallback Door.IsVertical itself uses, always unambiguous here since an airlock is
            // always StandardSpanUnits-wide on its span axis.
            DrawDoor(spriteBatch, left, top, width, height, outerDoor.Width <= outerDoor.Height, state?.IsOpen ?? false, origin,
                leadsToVacuum: true, destroyed: state?.Destroyed ?? false, totalSeconds: totalSeconds);
        }

        // M-doors-as-edges - the new narrow-door-as-a-barrier-between-2-tiles primitive (empty for
        // every hand-authored hull; only a Ship Editor-built one ever carries these).
        if (snapshot.DoorEdges is { Count: > 0 } doorEdges)
            foreach (var edge in doorEdges)
            {
                var state = snapshot.DoorEdgeStates?.FirstOrDefault(s => s.Id == edge.Id);
                DrawDoorEdge(spriteBatch, edge.Coord, edge.Side, state?.IsOpen ?? true,
                    state?.Destroyed ?? false, origin, totalSeconds);
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
        foreach (var terminal in snapshot.Terminals ?? Array.Empty<TerminalState>())
            DrawTerminal(spriteBatch, terminal, origin);
        foreach (var wallLamp in snapshot.WallLamps ?? Array.Empty<WallLamp>())
            DrawWallLamp(spriteBatch, wallLamp, origin);
        foreach (var junctionBox in snapshot.JunctionBoxes ?? Array.Empty<JunctionBox>())
            DrawJunctionBox(spriteBatch, junctionBox, origin);

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
}
