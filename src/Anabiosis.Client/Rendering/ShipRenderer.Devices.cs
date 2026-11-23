using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Every fixed ship device's own visual (reactor, distribution, batteries, storage, consoles, terminals, wall lamp, turret, engines) - split out of ShipRenderer.cs to keep that file to its own topic.
public sealed partial class ShipRenderer
{
    // Direct user request ("чтобы приборы в игре имели размеры как в редакторе") - the same
    // CustomDeviceFootprint.Size(kind) the Ship Editor already uses for placement, converted to
    // real screen pixels (Reactor already does exactly this via its own ReactorBlockSize constant -
    // this is that same idea generalized to every other kind instead of one hand-picked literal per
    // device). Deliberately NOT wired into movement collision (RoomLayout.RoomObstacle) - only
    // rendering and the click/hover hit-rect (GetBlockRect) read this; see this session's own plan
    // notes on why a blanket footprint-sized physical obstacle would risk trapping characters or
    // clipping through walls on hulls that place devices without a footprint-sized clearance
    // guarantee (every hand-authored hull today, unlike a CompartmentCatalog-built one).
    internal static (int Width, int Height) FootprintPixelSize(CustomDeviceKind kind)
    {
        var (tileW, tileH) = CustomDeviceFootprint.Size(kind);
        return ((int)(tileW * PixelsPerUnit), (int)(tileH * PixelsPerUnit));
    }

    // Direct user bug report ("устройства не повернуты как в редакторе") - Helm/Navigation are the
    // only two kinds with a real Ship-side object (HelmConsole/NavigationConsole) that can actually
    // be Rotated (every other rotatable kind - workbenches, Bed, ShuttleHangar - stays cosmetic-only
    // in real gameplay, see CustomDeviceDef.Rotated's own doc comment), so this overload is the only
    // one that ever needs the swap; the plain one above is untouched for every other caller.
    internal static (int Width, int Height) FootprintPixelSize(CustomDeviceKind kind, bool rotated)
    {
        var (width, height) = FootprintPixelSize(kind);
        return rotated ? (height, width) : (width, height);
    }
    // Depleted (World.Ammo.cs's finite stock, restocked at a station) reads as an empty crate -
    // dimmed out with a red outline - rather than looking identical to a full one.
    private void DrawAmmoStorage(SpriteBatch spriteBatch, AmmoStorage storage, int remaining, Vector2 origin)
    {
        var (width, height) = FootprintPixelSize(CustomDeviceKind.AmmoStorage);
        var center = origin + new Vector2(storage.X, storage.Y) * PixelsPerUnit;
        var rect = new Rectangle((int)center.X - width / 2, (int)center.Y - height / 2, width, height);
        var empty = remaining <= 0;
        // Crates with brass in them (DeviceSkin.Furniture.cs's AmmoCrates) - dull and outlined red
        // once the stock runs out.
        spriteBatch.Draw(_deviceSkin.Get(DeviceSkin.Face.AmmoCrates, rect.Width, rect.Height, !empty), rect, Color.White);
        CastShadow(rect);
        if (empty)
            DrawRectOutline(spriteBatch, rect, Color.OrangeRed * 0.8f, 1);
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
        var (lockerWidth, lockerHeight) = FootprintPixelSize(CustomDeviceKind.SuitLocker);
        var rect = GetBlockRect(locker.Position, lockerWidth, lockerHeight, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Locker, hasSuit, DeviceRim, 1);

        const int lightSize = 4;
        var lightColor = hasSuit ? Color.CadetBlue : new Color(60, 70, 68);
        spriteBatch.Draw(_pixel, new Rectangle((int)center.X - lightSize / 2, rect.Y + 3, lightSize, lightSize), lightColor);
    }

    public const int DroppedItemHitSize = 48; // the pad ItemIcons.DrawInWorld draws, so a click anywhere on it picks the item up

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
            ItemIcons.DrawInWorld(spriteBatch, _pixel, _font, dropped.Item, center, totalSeconds);
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
            damaged ? Color.Red : isOpen ? Color.Gold : DeviceRim, isOpen ? 3 : damaged ? 2 : 1);
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
        var (camWidth, camHeight) = FootprintPixelSize(CustomDeviceKind.Camera);
        var rect = GetBlockRect(camera.InteriorPosition, camWidth, camHeight, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Auxiliary, powered && !damaged,
            damaged ? Color.Red : DeviceRim, damaged ? 2 : 1);
        if (damaged)
        {
            DrawScorch(spriteBatch, rect);
            DrawHazardStripes(spriteBatch, new Rectangle(rect.X, rect.Bottom - 3, rect.Width, 3), horizontal: true);
        }
        DrawDeviceLabel(spriteBatch, rect, "Кам.");
        if (damaged)
            spriteBatch.DrawString(_font, "!", new Vector2(rect.Center.X + camWidth / 2f - 2, rect.Center.Y - camHeight),
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
    // Rotating a texture with an explicit origin (EngineArtRotation below, same convention
    // ShipRenderer.Rooms.cs's wall corner/T-junction/end-cap rotations already use) needs a
    // CENTER-anchored destination rect: this SpriteBatch.Draw overload maps `origin` (given in
    // SOURCE-texture pixels) onto the destination rect's own X/Y, not its top-left corner - a plain
    // top-left rect (GetBlockRect's own convention, correct for hitboxes/outlines/scorch/labels)
    // puts the texture's own center where its corner belongs, drawing the art shifted up-left by
    // half its own size. Direct user bug report ("текстура двигателя съезжает на пол клетки") -
    // this recenters just the destination handed to a rotated Draw call; the original rect (used
    // for everything else - outline, scorch, hitbox) is untouched.
    private static Rectangle CenterAnchored(Rectangle rect) => new(rect.Center.X, rect.Center.Y, rect.Width, rect.Height);

    // `drawControl` skips the shared Control box entirely - direct user request ("двойной
    // двигатель... 2 наложенных друг на друга двигателя с общим началом") - a double engine is TWO
    // EngineStates sharing one (X,Y), and drawing the Control box (and its "!"/label) once per
    // EngineState there would paint it twice at the identical rect, showing conflicting broken/intact
    // tints if only one twin were damaged. The caller (the loop below) draws the Control box once per
    // shared position using an "either broken" tint, then calls this per-engine only for its own
    // Bulkhead (which differs by Facing and is already fine per-engine).
    private void DrawShipEngine(SpriteBatch spriteBatch, EngineState engine, Vector2 origin, float totalSeconds, bool drawControl = true)
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
            spriteBatch.Draw(bulkheadTex, CenterAnchored(bulkheadRect), null, engine.BulkheadBroken ? new Color(255, 130, 130) : Color.White,
                EngineArtRotation(engine.Facing), new Vector2(bulkheadTex.Width / 2f, bulkheadTex.Height / 2f), SpriteEffects.None, 0f);
            DrawRectOutline(spriteBatch, bulkheadRect, engine.BulkheadBroken ? Color.Red : DeviceRim, engine.BulkheadBroken ? 3 : 1);
        }
        else
        {
            DrawDeviceFace(spriteBatch, bulkheadRect, DeviceSkin.Face.Engine, lit: false,
                engine.BulkheadBroken ? Color.Red : DeviceRim, engine.BulkheadBroken ? 3 : 1);
        }
        if (engine.BulkheadBroken)
        {
            DrawScorch(spriteBatch, bulkheadRect);
            DrawHazardStripes(spriteBatch, new Rectangle(bulkheadRect.X, bulkheadRect.Bottom - 4, bulkheadRect.Width, 4), horizontal: true);
        }

        if (!drawControl)
            return;

        // Control - same size/style as any other system-device box (DrawSystemDevice).
        var controlRect = GetBlockRect(controlPos, BigBlockSize, origin);
        if (_engineControlTexture is { } controlTex)
        {
            spriteBatch.Draw(controlTex, CenterAnchored(controlRect), null, engine.ControlBroken ? new Color(255, 130, 130) : Color.White,
                EngineArtRotation(engine.Facing), new Vector2(controlTex.Width / 2f, controlTex.Height / 2f), SpriteEffects.None, 0f);
            DrawRectOutline(spriteBatch, controlRect, engine.ControlBroken ? Color.Red : DeviceRim, engine.ControlBroken ? 3 : 1);
        }
        else
        {
            DrawDeviceFace(spriteBatch, controlRect, DeviceSkin.Face.Helm, !engine.ControlBroken,
                engine.ControlBroken ? Color.Red : DeviceRim, engine.ControlBroken ? 3 : 1);
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

    // Draws ONLY the shared Control box for a double engine's pair - same visuals as the single-
    // engine branch above, just tinted "broken" if EITHER twin's own Control half is broken (from the
    // player's perspective there is exactly one physical control panel there, however many independent
    // HP pools the server tracks behind it - World.Engines.cs's own doc comment on repairing "one at a
    // time from the same spot").
    private void DrawSharedEngineControl(SpriteBatch spriteBatch, EngineState anyTwin, bool eitherControlBroken, Vector2 origin)
    {
        var controlPos = new Vec2(anyTwin.X, anyTwin.Y);
        var controlRect = GetBlockRect(controlPos, BigBlockSize, origin);
        if (_engineControlTexture is { } controlTex)
        {
            spriteBatch.Draw(controlTex, CenterAnchored(controlRect), null, eitherControlBroken ? new Color(255, 130, 130) : Color.White,
                EngineArtRotation(anyTwin.Facing), new Vector2(controlTex.Width / 2f, controlTex.Height / 2f), SpriteEffects.None, 0f);
            DrawRectOutline(spriteBatch, controlRect, eitherControlBroken ? Color.Red : DeviceRim, eitherControlBroken ? 3 : 1);
        }
        else
        {
            DrawDeviceFace(spriteBatch, controlRect, DeviceSkin.Face.Helm, !eitherControlBroken,
                eitherControlBroken ? Color.Red : DeviceRim, eitherControlBroken ? 3 : 1);
        }
        if (eitherControlBroken)
        {
            DrawScorch(spriteBatch, controlRect);
            DrawHazardStripes(spriteBatch, new Rectangle(controlRect.X, controlRect.Bottom - 3, controlRect.Width, 3), horizontal: true);
            spriteBatch.DrawString(_font, "!", new Vector2(controlRect.Center.X + BigBlockSize / 2f - 2, controlRect.Center.Y - BigBlockSize),
                Color.Red, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
        }
        DrawDeviceLabel(spriteBatch, controlRect, "Двойной двигатель");
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
                spriteBatch.Draw(nozzleTex, CenterAnchored(nozzleRect), null, tint, EngineArtRotation(engine.Facing),
                    new Vector2(nozzleTex.Width / 2f, nozzleTex.Height / 2f), SpriteEffects.None, 0f);
                DrawRectOutline(spriteBatch, nozzleRect, engine.NozzleBroken ? Color.Red : DeviceRim, engine.NozzleBroken ? 3 : 1);
            }
            else
            {
                DrawDeviceFace(spriteBatch, nozzleRect, DeviceSkin.Face.Engine, engine.IsThrusting,
                    engine.NozzleBroken ? Color.Red : DeviceRim, engine.NozzleBroken ? 3 : 1);
            }
            if (engine.NozzleBroken)
            {
                DrawScorch(spriteBatch, nozzleRect);
                DrawHazardStripes(spriteBatch, new Rectangle(nozzleRect.X, nozzleRect.Bottom - 3, nozzleRect.Width, 3), horizontal: true);
            }
        }
        spriteBatch.End();

        // The flame itself (EngineExhaustFx: Cosmoteer-style particle stream + plume + glow + sparks) in its own
        // additive pass, same convention DrawRcsPlume's puffs use. Every engine is fed every frame, thrusting or not,
        // so a burn spools up and its particles finish fading after the engine stops.
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, transformMatrix: sceneTransform);
        foreach (var engine in engines)
        {
            var step = EngineFacingStep(engine.Facing);
            var nozzlePos = new Vec2(engine.X + step.X * 2, engine.Y + step.Y * 2);
            var nozzleRect = GetBlockRect(nozzlePos, BigBlockSize, origin);
            _engineExhaust.Draw(spriteBatch, engine.Id, engine.IsThrusting,
                new Vector2(nozzleRect.Center.X, nozzleRect.Center.Y), step, BigBlockSize, totalSeconds);
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
    private void DrawReactorBlock(SpriteBatch spriteBatch, ReactorBlock block, ReactorState reactor, ReactorLeverState levers, bool isOpen, Vector2 origin, float totalSeconds, string? roomName, PowerState power, float integrity, bool drawLevers = true)
    {
        // A catalog/editor room with its own reference art already draws the whole machine baked
        // into the room's own texture (RoomDecor's own "texture doubles as the device" rule, same
        // as an engine/turret/camera room) - no separate icon/housing drawn on top of it, just a
        // plain outline around the exact same rect the click-to-open check (Game1.Input.cs) and the
        // walking obstacle (Ship.DeviceObstacles) already use, so the interactive area the art
        // doubles as is visible at a glance instead of an invisible zone floating over the room.
        //
        // Direct user bug report ("не могу нажать на рычаги") - this branch used to skip the 3
        // levers entirely too, on the same "the art already shows it" reasoning, but nothing in the
        // reactor's reference art actually depicts them - unlike the housing, HandleMouseClick's own
        // lever click-test (GetReactorLeverRect) still fires here exactly as normal, so the levers
        // were live, clickable, and completely invisible on any catalog-textured reactor room. Drawn
        // the same as the procedural-housing branch below now - the one thing this branch was
        // legitimately skipping was the housing itself, not these.
        if (RoomDecor.HasCatalogTexture(roomName))
        {
            var artRect = GetBlockRect(block.Position, ReactorSize(block), origin);
            DrawComplexReactorOutline(spriteBatch, artRect, isOpen ? Color.Gold : Color.White * 0.85f, isOpen ? 3f : 2f);
            if (drawLevers)
            {
                DrawReactorLever(0, spriteBatch, GetReactorLeverRect(0, block, origin), levers.LightsOn, Color.Gold, totalSeconds);
                DrawReactorLever(1, spriteBatch, GetReactorLeverRect(1, block, origin), !levers.EmergencyShutdown,
                    reactor.CurrentOutput > 0 ? new Color(63, 184, 232) : new Color(40, 50, 55), totalSeconds);
                DrawReactorLever(2, spriteBatch, GetReactorLeverRect(2, block, origin), !levers.DoorsLocked, Color.OrangeRed, totalSeconds);
            }
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
        if (_reactorArt is { } art)
        {
            // Worn stage from the room's integrity; the light is a separate additive mask on top
            // (teal while running, sliding to a pulsing red as the plasma runs hot).
            var stage = ReactorTexture.StageFor(integrity);
            spriteBatch.Draw(art.Stages[stage], rect, running ? Color.White : new Color(170, 170, 170));
            CastShadow(rect);
            if (running)
            {
                var margin = (int)MathF.Round(rect.Width * ReactorTexture.GlowMargin / 64f);
                var glowRect = new Rectangle(rect.X - margin, rect.Y - margin, rect.Width + 2 * margin, rect.Height + 2 * margin);
                var load = MathHelper.Clamp(reactor.CurrentOutput / Math.Max(1f, reactor.MaxOutput), 0f, 1f);
                var heat = MathHelper.Clamp((power.PlasmaTemperature - 125f) / 25f, 0f, 1f);
                var flicker = 0.95f + 0.05f * MathF.Sin(totalSeconds * 13f) * MathF.Sin(totalSeconds * 3.1f);
                var pulse = 1f - heat * 0.25f * (0.5f + 0.5f * MathF.Sin(totalSeconds * 5f));
                var strength = (0.5f + 0.5f * load) * flicker * pulse * (stage == 2 ? 0.8f : 1f);
                var tone = Vector3.Lerp(new Vector3(0.30f, 0.86f, 0.80f), new Vector3(1f, 0.22f, 0.15f), heat) * strength;
                spriteBatch.Draw(art.GlowFor(stage), glowRect, new Color(tone.X, tone.Y, tone.Z, 0f));
            }
            if (isOpen) DrawRectOutline(spriteBatch, rect, Color.Gold, 3);
        }
        else if (_reactorTexture is { } reactorTex)
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
        // Direct user report ("сделай чтобы они при нажатии меняли текстуру и рычаг двигался") -
        // the handle now visibly SWINGS between the two poses over a fraction of a second (DrawLever
        // eases toward whichever pose is current) rather than snapping instantly, on top of the
        // brighter/dimmer tip colour it already had.
        if (!drawLevers)
            return; // an extra reactor shares the first one's levers, which are on the first
        DrawReactorLever(0, spriteBatch, GetReactorLeverRect(0, block, origin), levers.LightsOn, Color.Gold, totalSeconds);
        DrawReactorLever(1, spriteBatch, GetReactorLeverRect(1, block, origin), !levers.EmergencyShutdown, glowColor, totalSeconds);
        DrawReactorLever(2, spriteBatch, GetReactorLeverRect(2, block, origin), !levers.DoorsLocked, Color.OrangeRed, totalSeconds);
    }

    // Current visually-eased angle per lever (index 0-2), swung toward whichever pose is actually
    // current a fixed number of degrees per real second rather than snapping there instantly.
    // _reactorLeverLastTotalSeconds tracks the game clock between calls so a per-frame delta can be
    // derived without Draw's own caller threading one all the way down here; NaN means "never drawn
    // yet" - the very first call snaps straight to the real pose instead of swinging in from
    // whatever the fields happen to default to.
    private readonly float[] _reactorLeverAngleDegrees = new float[3];
    private float _reactorLeverLastTotalSeconds = float.NaN;

    private void DrawReactorLever(int index, SpriteBatch spriteBatch, Rectangle rect, bool on, Color indicatorColor, float totalSeconds)
    {
        const float degreesPerSecond = 480f; // a quick, visible swing - 90 degrees in ~0.19s
        var targetAngle = on ? -135f : -45f;
        if (float.IsNaN(_reactorLeverLastTotalSeconds))
        {
            _reactorLeverAngleDegrees[index] = targetAngle;
        }
        else
        {
            var deltaSeconds = MathHelper.Clamp(totalSeconds - _reactorLeverLastTotalSeconds, 0f, 0.1f);
            var diff = targetAngle - _reactorLeverAngleDegrees[index];
            var maxStep = degreesPerSecond * deltaSeconds;
            _reactorLeverAngleDegrees[index] += MathHelper.Clamp(diff, -maxStep, maxStep);
        }
        // Only the LAST lever drawn each frame should advance the shared clock - all 3 calls this
        // frame carry the same totalSeconds, so updating it on every call would zero deltaSeconds
        // for levers 1 and 2 every single frame.
        if (index == 2)
            _reactorLeverLastTotalSeconds = totalSeconds;

        DrawPanel(spriteBatch, rect, Color.SlateGray * 0.75f, Color.Black, 1);
        var pivot = new Vector2(rect.Right - 3, rect.Bottom - 3);
        var angle = _reactorLeverAngleDegrees[index] * (MathF.PI / 180f);
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
        var (distWidth, distHeight) = FootprintPixelSize(CustomDeviceKind.Distribution);
        var rect = GetBlockRect(block.Position, distWidth, distHeight, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Distribution, powered, isOpen ? Color.Gold : DeviceRim, isOpen ? 3 : 1);
        DrawDeviceLabel(spriteBatch, rect, "Щиток");
    }

    // Medium — sits next to the distribution block, charge level shown as a bottom-up fill so a
    // drained battery reads differently from a full one at a glance (same idea as the reactor's rods).
    private void DrawBatteryBlock(SpriteBatch spriteBatch, BatteryBlock block, PowerState power, bool isOpen, Vector2 origin, bool powered)
    {
        var (batteryWidth, batteryHeight) = FootprintPixelSize(CustomDeviceKind.Battery);
        var rect = GetBlockRect(block.Position, batteryWidth, batteryHeight, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Battery, powered, isOpen ? Color.Gold : DeviceRim, isOpen ? 3 : 1);

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
        var (rackWidth, rackHeight) = FootprintPixelSize(CustomDeviceKind.StorageRack);
        var rect = GetBlockRect(rack.Position, rackWidth, rackHeight, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Rack, true, isOpen ? Color.Gold : DeviceRim, isOpen ? 3 : 1);
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

    // Every device's resting outline - a dark steel edge rather than a saturated ring in each device's
    // own colour (direct user requests, DeviceSkin.BridgeConsoles.cs, then every other device): the colour
    // now lives on the machine itself; gold still marks an open panel and red a damaged one.
    private static readonly Color DeviceRim = new(26, 29, 35);

    // Bridge console (game_design.md section 5) — click it to bring up the galaxy map.
    private void DrawNavigationConsole(SpriteBatch spriteBatch, NavigationConsole console, bool isOpen, Vector2 origin, bool powered)
    {
        DrawHelmOrNavigationConsole(spriteBatch, console.Position, console.HalfSide, CustomDeviceKind.Navigation,
            DeviceSkin.Face.Navigation, powered, isOpen ? Color.Gold : DeviceRim, isOpen ? 3 : 1, "Сканер", origin);
    }

    // Pilot's console (game_design.md Phase 3, M15) — click it to man it and bring up the helm's
    // joystick panel instead of the ship view.
    private void DrawHelmConsole(SpriteBatch spriteBatch, HelmConsole console, bool isOpen, Vector2 origin, bool powered)
    {
        DrawHelmOrNavigationConsole(spriteBatch, console.Position, console.HalfSide, CustomDeviceKind.Helm,
            DeviceSkin.Face.Helm, powered, isOpen ? Color.Gold : DeviceRim, isOpen ? 3 : 1, "Навигационная панель", origin);
    }

    // Direct user request ("монитор состояния корабля... консоль связи... свои уникальные
    // текстуры") - same physical console footprint as Navigation/Helm above
    // (DrawHelmOrNavigationConsole is generic despite its name), each with its own bespoke
    // DeviceSkin face now (DeviceSkin.cs's own ShipStatusMonitor/CommsConsole).
    private void DrawShipStatusMonitor(SpriteBatch spriteBatch, ShipStatusMonitor console, bool isOpen, Vector2 origin, bool powered)
    {
        DrawHelmOrNavigationConsole(spriteBatch, console.Position, console.HalfSide, CustomDeviceKind.ShipStatusMonitor,
            DeviceSkin.Face.ShipStatusMonitor, powered, isOpen ? Color.Gold : DeviceRim, isOpen ? 3 : 1, "Монитор состояния", origin);
    }

    private void DrawCommsConsole(SpriteBatch spriteBatch, CommsConsole console, bool isOpen, Vector2 origin, bool powered)
    {
        DrawHelmOrNavigationConsole(spriteBatch, console.Position, console.HalfSide, CustomDeviceKind.CommsConsole,
            DeviceSkin.Face.CommsConsole, powered, isOpen ? Color.Gold : DeviceRim, isOpen ? 3 : 1, "Связь", origin);
    }

    // Direct user bug report ("они должны быть вплотную это раз, а во вторых само устройство должно
    // быть таких размеров а не состоять из двух элементов") - a prior version of this drew the "full"
    // tile with the real device face art and the "half" tile as a SEPARATE flat half-block swatch,
    // with a visible seam (and, before the matching fix in HalfWidthDeviceRect below, a walkable gap)
    // between them. Now ONE seamless rect, spanning continuously from the full tile's own far edge to
    // the half tile's own midpoint (1.5 units along the halved axis, 2 along the other - exactly
    // TileGrid.IsWalkable's real combined blocked area, not a cosmetic approximation of it) - the
    // console's face art, hood and label are all drawn once into that single rect, same as any other
    // device kind.
    private void DrawHelmOrNavigationConsole(SpriteBatch spriteBatch, Vec2 position, TileSide halfSide, CustomDeviceKind kind,
        DeviceSkin.Face face, bool powered, Color glow, float glowThickness, string label, Vector2 origin)
    {
        var rect = HalfWidthDeviceRect(position, halfSide, otherAxisTiles: 2, origin);

        DrawDeviceFace(spriteBatch, rect, face, powered, glow, glowThickness);
        DrawHood(spriteBatch, rect);
        DrawDeviceLabel(spriteBatch, rect, label);
    }

    // Shared by DrawHelmOrNavigationConsole and DrawDecorativeDevice (Fabricator/Deconstructor,
    // direct user request "по аналогии... полтора на 3") - any CustomDeviceFootprint.IsHalfWidthKind
    // device's real combined rect: 1.5 units wide/tall along the halved axis (whichever axis
    // `halfSide` names), `otherAxisTiles` units along the other - 2 for Helm/Navigation, 3 for
    // Fabricator/Deconstructor. `position` is the CENTER of the old full anchor box
    // (TileShipBuilder/Ship.Custom.cs's own anchor+footprintSize/2f export, unchanged regardless of
    // halfSide - both mirror states of a pair share the same box, just flipped, see
    // CustomDeviceDef.HalfWidthSide's own doc comment) - the 0.25-unit shift away from `position`
    // flips sign between the "near" (West/North, half tile AT the anchor) and "far" (East/South, half
    // tile PAST the anchor) mirror of the halved axis; the un-halved axis needs no shift at all.
    private Rectangle HalfWidthDeviceRect(Vec2 position, TileSide halfSide, int otherAxisTiles, Vector2 origin)
    {
        var unit = (int)PixelsPerUnit;
        var half = (int)(1.5f * unit);
        var otherAxisPixels = otherAxisTiles * unit;
        return halfSide switch
        {
            TileSide.East => GetBlockRect(new Vec2(position.X - 0.25, position.Y), half, otherAxisPixels, origin),
            TileSide.West => GetBlockRect(new Vec2(position.X + 0.25, position.Y), half, otherAxisPixels, origin),
            TileSide.South => GetBlockRect(new Vec2(position.X, position.Y - 0.25), otherAxisPixels, half, origin),
            _ => GetBlockRect(new Vec2(position.X, position.Y + 0.25), otherAxisPixels, half, origin), // North
        };
    }

    // A quiet card table - not clickable, just a felt surface bolted to the deck; two crew
    // standing beside it together is what actually starts a hand (World.CardGame.cs's
    // StepCardGame). Lit up gold whenever a hand happens to be running there, the same "isOpen"
    // glow every other console gets, so it's obvious at a glance the table isn't just furniture.
    private void DrawCardTable(SpriteBatch spriteBatch, CardTable table, bool inUse, Vector2 origin)
    {
        var (tableWidth, tableHeight) = FootprintPixelSize(CustomDeviceKind.CardTable);
        var rect = GetBlockRect(table.Position, tableWidth, tableHeight, origin);
        spriteBatch.Draw(_deviceSkin.Get(DeviceSkin.Face.CardTable, rect.Width, rect.Height, inUse), rect, Color.White);
        CastShadow(rect);
        if (inUse)
            DrawRectOutline(spriteBatch, rect, Color.Gold, 2);
        var cardTableLabelPos = new Vector2(rect.X + 1, rect.Y + 7);
        DrawLabelBacking(spriteBatch, "Карты", cardTableLabelPos, 0.42f);
        spriteBatch.DrawString(_font, "Карты", cardTableLabelPos, Color.White, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
    }

    // The jukebox (Ship Editor only for now, Ship.Jukebox is nullable) - lit warm amber while
    // playing, dim otherwise, the same "glow says it's doing something" language DrawCardTable
    // uses for an active hand.
    private void DrawJukebox(SpriteBatch spriteBatch, JukeboxState jukebox, bool isOpen, Vector2 origin)
    {
        var (jukeboxWidth, jukeboxHeight) = FootprintPixelSize(CustomDeviceKind.Jukebox);
        var rect = GetBlockRect(jukebox.Block.Position, jukeboxWidth, jukeboxHeight, origin);
        // Lit means playing: the arch, the window and one pressed key all come up together, so the
        // machine says whether it is running without anybody reading a label.
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Jukebox, jukebox.On, isOpen ? Color.Gold : DeviceRim, isOpen ? 3 : 1);
        DrawDeviceLabel(spriteBatch, rect, "Музыка");
    }

    // A wall terminal - no panel of its own (one click on the block is the whole toggle gesture),
    // so nothing here ever draws an "isOpen" highlight the way the reactor/jukebox do. Off dims the
    // whole baked face to gray, the same convention DrawReactorBlock uses for its own texture.
    // Always a half-block flush against Terminal.FacingSide (direct user request, "занимал
    // половину блока и визуально выглядел в соответствии с полублоком") - recessed-in-a-half-thick-
    // wall or protruding-from-an-ordinary-wall alike, FacingSide already carries which side to draw
    // on directly (Ship.cs's own TileShipBuilder export sets it), no per-frame tile lookup needed.
    // Direct user request ("сделай щитку свою собственную текстуру и сделай чтобы он занимал размер
    // полтора на 1 блок, как делались все новые блоки") - JunctionBox used to be purely decorative,
    // drawn as an 18px flat tinted swatch with a centered glyph (the same "no dedicated Face yet"
    // fallback look the Ship Editor's own canvas used too - Game1.ShipEditor.Draw.cs's own 1x1-device
    // fallback). Now a real half-width console footprint (CustomDeviceFootprint.IsHalfWidthKind, same
    // HalfWidthDeviceRect mechanic Helm/Navigation/ShipStatusMonitor/CommsConsole already use, just 1
    // tile tall instead of 2) with its own bespoke DeviceSkin face (DeviceSkin.cs's own Junction).
    // `isOpen` glows gold the same way ShipStatusMonitor/CommsConsole's shared full-screen overlay
    // does - one client-only screen shared by every instance, not per-fixture data.
    private void DrawJunctionBox(SpriteBatch spriteBatch, JunctionBox box, bool isOpen, Vector2 origin, bool powered)
    {
        var otherAxisTiles = CustomDeviceFootprint.Size(CustomDeviceKind.Junction).Height;
        var rect = HalfWidthDeviceRect(box.Position, box.HalfSide, otherAxisTiles, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Junction, powered, isOpen ? Color.Gold : DeviceRim, isOpen ? 3 : 1);
    }

    // Direct user bug report ("некоторые устройства в игре не отображаются а в редакторе они
    // видны") - DecorativeDevice.Kinds, drawn generically at the same footprint the Ship Editor
    // already places them at (CustomDeviceFootprint.Size via FootprintPixelSize). A handful of these
    // kinds already had real DeviceSkin art baked for them (ConstructionBench/Fabricator/
    // Deconstructor/WeaponWorkbench/Bed/ShuttleHangar/TripleDoor - built for exactly this device, but
    // never actually reachable before now since no Ship-side object ever carried a position for
    // them) - used here when it matches; everything else falls back to DrawJunctionBox's own
    // established "no dedicated Face yet" look, so the game and the editor's own canvas agree.
    private static readonly Dictionary<CustomDeviceKind, DeviceSkin.Face> DecorativeDeviceFaces = new()
    {
        [CustomDeviceKind.ConstructionBench] = DeviceSkin.Face.ConstructionBench,
        [CustomDeviceKind.Fabricator] = DeviceSkin.Face.Fabricator,
        [CustomDeviceKind.Deconstructor] = DeviceSkin.Face.Deconstructor,
        [CustomDeviceKind.WeaponWorkbench] = DeviceSkin.Face.WeaponWorkbench,
        [CustomDeviceKind.Bed] = DeviceSkin.Face.Bed,
        [CustomDeviceKind.ShuttleHangar] = DeviceSkin.Face.ShuttleHangar,
        [CustomDeviceKind.TripleDoor] = DeviceSkin.Face.TripleDoor,
        // Direct user request ("обнови текстуры для всех устройств") - every kind that was still a
        // tinted square with a letter gets real art: machinery borrows the closest existing machine
        // (an engine is an engine, a shield generator the shield emitter), furniture and floor kit
        // get their own free-standing faces (DeviceSkin.Furniture.cs).
        [CustomDeviceKind.EngineSmall] = DeviceSkin.Face.Engine,
        [CustomDeviceKind.EngineMedium] = DeviceSkin.Face.Engine,
        [CustomDeviceKind.EngineLarge] = DeviceSkin.Face.Engine,
        [CustomDeviceKind.WarpEngine] = DeviceSkin.Face.Engine,
        [CustomDeviceKind.DroneHangar] = DeviceSkin.Face.ShuttleHangar,
        [CustomDeviceKind.SmallStorage] = DeviceSkin.Face.Rack,
        [CustomDeviceKind.LargeStorage] = DeviceSkin.Face.Rack,
        [CustomDeviceKind.Morgue] = DeviceSkin.Face.Locker,
        [CustomDeviceKind.FuelRodStorage] = DeviceSkin.Face.Weapons,
        [CustomDeviceKind.PowerConduit] = DeviceSkin.Face.Junction,
        [CustomDeviceKind.ShieldGeneratorSmall] = DeviceSkin.Face.Shields,
        [CustomDeviceKind.ShieldGeneratorLarge] = DeviceSkin.Face.Shields,
        [CustomDeviceKind.WeaponPanel] = DeviceSkin.Face.Weapons,
        [CustomDeviceKind.Table] = DeviceSkin.Face.Table,
        [CustomDeviceKind.Chair] = DeviceSkin.Face.Chair,
        [CustomDeviceKind.Sofa] = DeviceSkin.Face.Sofa,
        [CustomDeviceKind.Nightstand] = DeviceSkin.Face.Nightstand,
        [CustomDeviceKind.DecorativePlant] = DeviceSkin.Face.Plant,
        [CustomDeviceKind.Lamp] = DeviceSkin.Face.FloorLamp,
        [CustomDeviceKind.Spotlight] = DeviceSkin.Face.Spotlight,
    };

    // Free-standing on the deck rather than a machine on a plate: drawn without the device outline
    // and drop shadow (their own baked art carries a soft shadow on a transparent canvas).
    private static readonly HashSet<DeviceSkin.Face> FreeStandingFaces = new()
    {
        DeviceSkin.Face.Table, DeviceSkin.Face.Chair, DeviceSkin.Face.Sofa, DeviceSkin.Face.Nightstand,
        DeviceSkin.Face.Plant, DeviceSkin.Face.FloorLamp, DeviceSkin.Face.Spotlight,
        DeviceSkin.Face.CardTable, DeviceSkin.Face.AmmoCrates, DeviceSkin.Face.ShuttleHangar,
    };

    private void DrawDecorativeDevice(SpriteBatch spriteBatch, DecorativeDevice device, Vector2 origin)
    {
        // Direct user request ("сделай по аналогии фабрикатор и деконструктор, только чтобы они
        // занимали полтора на 3 клетки") - same half-width mechanic DrawHelmOrNavigationConsole
        // already uses, just with a 3-tall other axis instead of 2 (CustomDeviceFootprint.Size's own
        // Height component names it per kind) - everything else (StorageRack, Bed, etc.) keeps the
        // ordinary full-footprint rect, unchanged.
        Rectangle rect;
        if (CustomDeviceFootprint.IsHalfWidthKind(device.Kind))
            rect = HalfWidthDeviceRect(device.Position, device.HalfSide, CustomDeviceFootprint.Size(device.Kind).Height, origin);
        else
        {
            var (width, height) = FootprintPixelSize(device.Kind, device.Rotated);
            rect = GetBlockRect(device.Position, width, height, origin);
        }
        // A defensive turret's mount reads as the same Space Haven gun base the periscope station
        // uses (DeviceSkin.Periscope.cs), square in the middle of its wider footprint.
        if (device.Kind == CustomDeviceKind.DefensiveTurret)
        {
            var side = Math.Min(rect.Width, rect.Height);
            var square = new Rectangle(rect.Center.X - side / 2, rect.Center.Y - side / 2, side, side);
            spriteBatch.Draw(_deviceSkin.Get(DeviceSkin.Face.Periscope, side, side, true), square, Color.White);
            CastShadow(square);
            return;
        }
        if (TryDrawWorkshopMachine(spriteBatch, device.Kind, rect,
                MachineArt.StageFor(_frameRoomIntegrity.TryGetValue(device.RoomId, out var integrity) ? integrity : 1f), _framePowered))
        {
            CastShadow(rect);
            return;
        }
        if (DecorativeDeviceFaces.TryGetValue(device.Kind, out var face))
        {
            if (FreeStandingFaces.Contains(face))
            {
                spriteBatch.Draw(_deviceSkin.Get(face, rect.Width, rect.Height, true), rect, Color.White);
                CastShadow(rect);
            }
            else
                DrawDeviceFace(spriteBatch, rect, face, lit: true, DeviceRim, 1);
            return;
        }
        spriteBatch.Draw(_pixel, rect, CustomDeviceCatalog.Tint(device.Kind));
        DrawRectOutline(spriteBatch, rect, Color.Black, 1);
        var glyph = CustomDeviceCatalog.ShortGlyph(device.Kind);
        var glyphSize = _font.MeasureString(glyph) * 0.5f;
        spriteBatch.DrawString(_font, glyph, new Vector2(rect.Center.X - glyphSize.X / 2f, rect.Center.Y - glyphSize.Y / 2f),
            Color.Black, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private void DrawTerminal(SpriteBatch spriteBatch, TerminalState terminal, Vector2 origin)
    {
        var tint = terminal.On ? Color.White : new Color(90, 90, 90);
        var center = origin + new Vector2((float)terminal.Block.Position.X * PixelsPerUnit, (float)terminal.Block.Position.Y * PixelsPerUnit);
        spriteBatch.Draw(_terminalTexture, HalfRect(center, (int)PixelsPerUnit, terminal.Block.FacingSide), tint);
    }

    // Direct user request ("настенную лампу... дай ей другую текстуру") - genuinely no bitmap art
    // for this yet, so drawn procedurally instead of reusing the terminal's own textured panel: a
    // dark housing plate (reads as a fixture bolted to the wall, not a screen) with a warm glowing
    // burst at its centre (HudIcons.DrawPowerGlyph - a filled core with radiating rays, already the
    // universal "light/energy" icon shape elsewhere in this file) - a different SILHOUETTE, not
    // just a different tint on the same shape. Always lit at full brightness - WallLamp has no
    // on/off of its own, Game1.Lighting.cs is what actually dims the SCENE when the ship's own
    // lamps are off, not this.
    // Then (direct user request, "сделай нормальную текстуру настенной лампы, чтобы она была похожа
    // как в баротравме или space haven") the dark slab with a power glyph on it gave way to an
    // actual light fitting seen from above: a bolted steel mounting strip on the wall face, a
    // top-lit housing standing off it, a bright tube behind a protective cage, and a soft pool of
    // light spilling onto the deck in front - with the floor of the free half left visible round it
    // instead of covered by a plate.
    private static readonly Color[] WallLampHousingBands = { new(150, 158, 170), new(124, 132, 146), new(100, 108, 122), new(82, 88, 100) };
    private static readonly Color WallLampMount = new(62, 68, 78);
    private static readonly Color WallLampTube = new(255, 236, 188);
    private static readonly Color WallLampCore = new(255, 251, 236);
    private static readonly Color WallLampCage = new(44, 48, 56);
    private static readonly Color WallLampGlow = new(255, 220, 150);

    // Then ("сделай чтобы было видно что она излучает свет как в баротравме") it also has to read
    // as a light SOURCE, not just a fitting: a broad warm falloff thrown across the deck in front
    // of it and a hot core over the tube that the bloom pass picks up - on top of the real shadowed
    // PointLight Game1.Lighting.cs's BuildWallLampLights casts from the same spot. `lit` follows
    // that light: with the ship's light lever off or no power, the tube goes dark and so does the
    // glow.
    private static readonly Color WallLampTubeOff = new(92, 90, 84);
    private Texture2D? _lampGlowTexture;

    // A radial falloff (bright centre easing to nothing at the rim, squared so it holds near the
    // centre and dies softly) - HudIcons' soft circle is solid out to 85% and reads as a disc.
    private Texture2D LampGlowTexture()
    {
        if (_lampGlowTexture is not null)
            return _lampGlowTexture;
        const int size = 64;
        var data = new Color[size * size];
        var c = (size - 1) / 2f;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = MathF.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / (size / 2f);
                var a = MathHelper.Clamp(1f - d, 0f, 1f);
                data[y * size + x] = Color.White * (a * a);
            }
        _lampGlowTexture = new Texture2D(_pixel.GraphicsDevice, size, size);
        _lampGlowTexture.SetData(data);
        return _lampGlowTexture;
    }

    private void DrawLampGlow(SpriteBatch spriteBatch, Vector2 center, Vector2 size, Color color)
    {
        var glow = LampGlowTexture();
        spriteBatch.Draw(glow, center, null, color, 0f, new Vector2(glow.Width / 2f, glow.Height / 2f),
            new Vector2(size.X / glow.Width, size.Y / glow.Height), SpriteEffects.None, 0f);
    }

    private void DrawWallLamp(SpriteBatch spriteBatch, WallLamp lamp, Vector2 origin, bool lit)
    {
        var center = origin + new Vector2((float)lamp.Position.X * PixelsPerUnit, (float)lamp.Position.Y * PixelsPerUnit);
        var rect = HalfRect(center, (int)PixelsPerUnit, lamp.FacingSide);
        var facing = lamp.FacingSide;
        var horizontal = facing is TileSide.North or TileSide.South; // the fitting runs along X
        var length = horizontal ? rect.Width : rect.Height;
        var outward = facing switch
        {
            TileSide.North => new Vector2(0, -1),
            TileSide.South => new Vector2(0, 1),
            TileSide.East => new Vector2(1, 0),
            _ => new Vector2(-1, 0),
        };
        Vector2 Sized(float along, float across) => horizontal ? new Vector2(along, across) : new Vector2(across, along);

        // Laid out once in fitting-local terms: `along` the wall, and `out` from the wall face (the
        // free half's far edge, where the solid half ends) toward the room.
        Rectangle Part(int along, int alongLength, int outFrom, int outLength) => facing switch
        {
            TileSide.South => new Rectangle(rect.X + along, rect.Y + outFrom, alongLength, outLength),
            TileSide.North => new Rectangle(rect.X + along, rect.Bottom - outFrom - outLength, alongLength, outLength),
            TileSide.East => new Rectangle(rect.X + outFrom, rect.Y + along, outLength, alongLength),
            _ => new Rectangle(rect.Right - outFrom - outLength, rect.Y + along, outLength, alongLength),
        };

        // The light it throws on the deck first, so the fitting sits on top of it: a wide soft pool
        // reaching well out into the room, and a tighter, brighter one right under the fitting.
        var lensCenter = new Vector2(Part(11, length - 22, 6, 6).Center.X, Part(11, length - 22, 6, 6).Center.Y);
        if (lit)
        {
            DrawLampGlow(spriteBatch, lensCenter + outward * length * 0.8f, Sized(length * 3.4f, length * 2.8f), WallLampGlow * 0.42f);
            DrawLampGlow(spriteBatch, lensCenter + outward * length * 0.3f, Sized(length * 1.8f, length * 1.2f), WallLampGlow * 0.55f);
        }

        // Mounting strip along the wall face, a bolt head at each end.
        var mount = Part(3, length - 6, 0, 5);
        spriteBatch.Draw(_pixel, mount, WallLampMount);
        DrawRectOutline(spriteBatch, mount, Color.Black * 0.55f, 1);
        foreach (var bolt in new[] { Part(5, 2, 2, 2), Part(length - 7, 2, 2, 2) })
        {
            spriteBatch.Draw(_pixel, bolt, Color.Black * 0.5f);
            spriteBatch.Draw(_pixel, new Rectangle(bolt.X, bolt.Y, 1, 1), Color.White * 0.45f);
        }

        // The housing standing off the strip, top-lit like every other steel surface on the deck,
        // with a cast shadow on the room side.
        var housing = Part(8, length - 16, 3, 10);
        spriteBatch.Draw(_pixel, Part(9, length - 16, 4, 10), Color.Black * 0.35f);
        DrawTopLitBands(spriteBatch, housing, horizontal, WallLampHousingBands);
        DrawRectOutline(spriteBatch, housing, Color.Black * 0.6f, 1);

        // The tube behind its cage: warm lens, a hotter core line, cage bars across it.
        var lens = Part(11, length - 22, 6, 6);
        spriteBatch.Draw(_pixel, lens, lit ? WallLampTube : WallLampTubeOff);
        if (lit)
            spriteBatch.Draw(_pixel, Part(11, length - 22, 8, 2), WallLampCore);
        for (var along = 14; along < length - 12; along += 5)
            spriteBatch.Draw(_pixel, Part(along, 1, 6, 6), WallLampCage * 0.85f);
        DrawRectOutline(spriteBatch, lens, WallLampCage, 1);

        // The hot core bleeding out over the cage - bright enough for the bloom pass to catch.
        if (lit)
        {
            DrawLampGlow(spriteBatch, lensCenter, Sized(length * 1.0f, length * 0.6f), WallLampCore * 0.7f);
            DrawLampGlow(spriteBatch, lensCenter, Sized(length * 0.55f, length * 0.25f), Color.White * 0.9f);
        }
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

    // Two separate things: the periscope station inside the room, which is what the gunner walks up
    // to and mans (drawn here, always), and the gun itself (DrawTurretGun), which sits ABOVE the ship
    // on top of that device and is only ever drawn when the ship is seen from outside.
    private void DrawTurret(SpriteBatch spriteBatch, Turret turret, TurretState? state, Vector2 origin, float totalSeconds)
    {
        var center = origin + new Vector2(turret.PeriscopeX, turret.PeriscopeY) * PixelsPerUnit;
        DrawPeriscopeStation(spriteBatch, center, state?.MannedByPlayerId is not null, state?.Damaged ?? false, totalSeconds);
    }

    // The gun on top of the ship, over its turret device (direct user request): a full 360 degree
    // traverse, drawn only while the ship is seen from outside (manning a gun, or out on a
    // spacewalk), never from inside a compartment. Game1 calls this as its own pass right after Draw.
    public void DrawTurretGuns(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        foreach (var turret in snapshot.Turrets)
            if (snapshot.TurretStates.FirstOrDefault(s => s.Id == turret.Id) is { } state)
                DrawTurretGun(spriteBatch, turret, state, snapshot.Rooms, snapshot.Turrets, origin);
    }

    private void DrawTurretGun(SpriteBatch spriteBatch, Turret turret, TurretState state,
        IReadOnlyList<Room> rooms, IReadOnlyList<Turret> allTurrets, Vector2 origin)
    {
        var manned = state.MannedByPlayerId is not null;
        var damaged = state.Damaged;

        var mount = TurretMount.For(rooms, allTurrets, turret);
        var mountPx = origin + new Vector2((float)mount.Position.X, (float)mount.Position.Y) * PixelsPerUnit;
        var rotation = mount.FireDegrees(state.AimDegrees) * (MathF.PI / 180f);

        // Two sprites, and only the second one turns: a barbette bolted through the plating, and the
        // rotating mass sitting in it. Both are drawn VisualScale times their baked size so the gun
        // reads as a gun on a 4x3-tile device.
        //
        // Which one is manned is carried by the trim paint and a lit sight rather than by making the
        // whole thing bigger and gold; the sight line below is drawn only for the gun you are behind.
        var look = damaged ? TurretSkin.Look.Damaged
            : manned ? TurretSkin.Look.Manned
            : TurretSkin.Look.Idle;
        spriteBatch.Draw(_turretSkin.Base(look), mountPx, null, Color.White, 0f,
            TurretSkin.BaseOrigin, TurretMount.VisualScale, SpriteEffects.None, 0f);
        spriteBatch.Draw(_turretSkin.Gun(look), mountPx, null, Color.White, rotation,
            TurretSkin.GunOrigin, TurretMount.VisualScale, SpriteEffects.None, 0f);

        if (!manned)
            return;

        // The gunner's aiming aid: a sight line running out of the muzzle so it's obvious where a
        // shell would go. There is no firing arc to draw any more - the traverse is a full 360 degrees.

        var muzzleLocal = mount.Muzzle(state.AimDegrees);
        var muzzle = origin + new Vector2((float)muzzleLocal.X, (float)muzzleLocal.Y) * PixelsPerUnit;
        spriteBatch.Draw(_pixel, muzzle, null, Color.Gold * 0.45f, rotation, new Vector2(0f, 0.5f), new Vector2(900f, 2f), SpriteEffects.None, 0f);

        var readout = $"{state.AimDegrees:0}°";
        spriteBatch.DrawString(_font, readout, mountPx + new Vector2(-10, -52), Color.Gold, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        // Rounds left, as pips rather than a count. Mid-engagement what the gunner needs off the gun
        // itself is "nearly out" or "fine"; the exact number is already on the gunnery panel, and
        // reading a digit means looking away from what you are tracking.
        if (state.MagazineCapacity > 0)
        {
            const int pips = 8;
            var loaded = (int)MathF.Ceiling(pips * MathHelper.Clamp(
                state.AmmoRemaining / (float)state.MagazineCapacity, 0f, 1f));
            for (var i = 0; i < pips; i++)
                spriteBatch.Draw(_pixel, new Rectangle((int)mountPx.X - pips * 3 + i * 6, (int)mountPx.Y + 46, 4, 3),
                    i < loaded ? new Color(232, 196, 96) : new Color(52, 56, 62));
        }
    }

    // Direct user request ("терминал для управления пушкой отображается не так как в редакторе, не
    // 3 на 3 тайла") - every turret kind already carries a 3x3 footprint in CustomDeviceFootprint
    // (this session's own earlier "все турели 3 на 3" fix), but that only ever reached the Ship
    // Editor's own placement/palette art; the LIVE game's periscope console below was still hand-
    // drawn at its original small-marker size, unrelated to any footprint. Rather than redesign the
    // console from scratch, PeriscopeStationScale blows up every one of its offsets/radii/rects
    // uniformly - tuned so the whole console (octagon + handles + terminal) sits comfortably inside
    // a real 3-tile (144px) box with a visible margin, the same "same art, correct size" fix already
    // used for Helm/Navigation's DrawHood. The hit/hover rect (BlockRectIfNear in Game1.Interactables.
    // cs/Game1.Input.cs) is updated to the same 3x3 footprint alongside this.
    private const float PeriscopeStationScale = 4f;

    // The station is a baked model now (DeviceSkin.Periscope.cs - a Space Haven-style gun base with
    // the periscope head on its hub, direct user request); only what changes is drawn live over it:
    // the eyepiece's glow (dim cyan idle, a brighter pulse while someone looks through it, a red
    // pulse when damaged), the hub's status lamp, a gold rim round the hub while manned, the faint
    // beam out of the lens, and soot plus a warning mark when damaged.
    private void DrawPeriscopeStation(SpriteBatch spriteBatch, Vector2 center, bool manned, bool damaged, float totalSeconds)
    {
        const float s = PeriscopeStationScale;
        var boxSize = (int)(PixelsPerUnit * 2.85f);
        var rect = new Rectangle((int)(center.X - boxSize / 2f), (int)(center.Y - boxSize / 2f), boxSize, boxSize);
        spriteBatch.Draw(_deviceSkin.Get(DeviceSkin.Face.Periscope, boxSize, boxSize, !damaged), rect, Color.White);
        CastShadow(rect);

        var lens = new Vector2(rect.X + rect.Width * DeviceSkin.PeriscopeLensX, rect.Y + rect.Height * DeviceSkin.PeriscopeLensY);
        var lensRadius = rect.Width * DeviceSkin.PeriscopeLensRadius;
        var lamp = new Vector2(rect.Center.X, rect.Y + rect.Height * (0.5f + 0.27f * 0.62f));

        Color outerGlow, midGlow, coreGlow, lampColor;
        if (damaged)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(totalSeconds * 6f);
            outerGlow = new Color(150, 46, 34) * (0.25f + 0.2f * pulse);
            midGlow = new Color(200, 64, 46) * (0.4f + 0.3f * pulse);
            coreGlow = new Color(255, 138, 106) * (0.5f + 0.4f * pulse);
            lampColor = new Color(240, 70, 50) * (0.5f + 0.5f * pulse);
            DrawScorch(spriteBatch, rect);
            spriteBatch.DrawString(_font, "!", new Vector2(rect.Right - 14f, rect.Y + 4f), Color.Red, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
        }
        else if (manned)
        {
            var pulse = 0.7f + 0.3f * MathF.Sin(totalSeconds * 3f);
            outerGlow = new Color(40, 150, 180) * 0.45f;
            midGlow = new Color(70, 200, 230) * 0.65f;
            coreGlow = new Color(170, 244, 255) * pulse;
            lampColor = new Color(255, 206, 90);
            // A faint beam projecting "up" out of the lens - only shows while someone's actually
            // looking through it.
            Primitives.FillTriangle(spriteBatch, _pixel, lens, lens + new Vector2(-6f * s, -14f * s), lens + new Vector2(6f * s, -14f * s), coreGlow * 0.14f);
            HudIcons.DrawRingArc(spriteBatch, _pixel, new Vector2(rect.Center.X, rect.Center.Y), rect.Width * 0.275f, 0f, 360f, Color.Gold * 0.55f, 48, 2f);
        }
        else
        {
            outerGlow = new Color(30, 110, 130) * 0.3f;
            midGlow = new Color(50, 170, 200) * 0.45f;
            coreGlow = new Color(127, 224, 255) * 0.75f;
            lampColor = new Color(100, 230, 120);
        }

        DrawLampGlow(spriteBatch, lens, new Vector2(lensRadius * 6.5f), outerGlow);
        DrawLampGlow(spriteBatch, lens, new Vector2(lensRadius * 3.4f), midGlow);
        DrawLampGlow(spriteBatch, lens, new Vector2(lensRadius * 1.8f), coreGlow);
        DrawLampGlow(spriteBatch, lamp, new Vector2(rect.Width * 0.09f), lampColor * 0.45f);
        HudIcons.FillCircle(spriteBatch, _pixel, lamp, Math.Max(1.5f, rect.Width * 0.016f), lampColor);
    }
}
