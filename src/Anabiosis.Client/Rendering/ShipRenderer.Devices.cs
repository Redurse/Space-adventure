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
        var (lockerWidth, lockerHeight) = FootprintPixelSize(CustomDeviceKind.SuitLocker);
        var rect = GetBlockRect(locker.Position, lockerWidth, lockerHeight, origin);
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
        var (camWidth, camHeight) = FootprintPixelSize(CustomDeviceKind.Camera);
        var rect = GetBlockRect(camera.InteriorPosition, camWidth, camHeight, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Auxiliary, powered && !damaged,
            damaged ? Color.Red : Color.LightSteelBlue, 2);
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
            DrawReactorLever(0, spriteBatch, GetReactorLeverRect(0, block, origin), levers.LightsOn, Color.Gold, totalSeconds);
            DrawReactorLever(1, spriteBatch, GetReactorLeverRect(1, block, origin), !levers.EmergencyShutdown,
                reactor.CurrentOutput > 0 ? new Color(63, 184, 232) : new Color(40, 50, 55), totalSeconds);
            DrawReactorLever(2, spriteBatch, GetReactorLeverRect(2, block, origin), !levers.DoorsLocked, Color.OrangeRed, totalSeconds);
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
        // Direct user report ("сделай чтобы они при нажатии меняли текстуру и рычаг двигался") -
        // the handle now visibly SWINGS between the two poses over a fraction of a second (DrawLever
        // eases toward whichever pose is current) rather than snapping instantly, on top of the
        // brighter/dimmer tip colour it already had.
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
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Distribution, powered, isOpen ? Color.Gold : Color.Plum, isOpen ? 3 : 2);
        DrawDeviceLabel(spriteBatch, rect, "Щиток");
    }

    // Medium — sits next to the distribution block, charge level shown as a bottom-up fill so a
    // drained battery reads differently from a full one at a glance (same idea as the reactor's rods).
    private void DrawBatteryBlock(SpriteBatch spriteBatch, BatteryBlock block, PowerState power, bool isOpen, Vector2 origin, bool powered)
    {
        var (batteryWidth, batteryHeight) = FootprintPixelSize(CustomDeviceKind.Battery);
        var rect = GetBlockRect(block.Position, batteryWidth, batteryHeight, origin);
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
        var (rackWidth, rackHeight) = FootprintPixelSize(CustomDeviceKind.StorageRack);
        var rect = GetBlockRect(rack.Position, rackWidth, rackHeight, origin);
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
        var (navWidth, navHeight) = FootprintPixelSize(CustomDeviceKind.Navigation, console.Rotated);
        var rect = GetBlockRect(console.Position, navWidth, navHeight, origin);
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Navigation, powered, isOpen ? Color.Gold : Color.LightSeaGreen, isOpen ? 3 : 2);
        DrawHood(spriteBatch, rect);
        DrawDeviceLabel(spriteBatch, rect, "Сканер");
    }


    // Pilot's console (game_design.md Phase 3, M15) — click it to man it and bring up the helm's
    // joystick panel instead of the ship view.
    private void DrawHelmConsole(SpriteBatch spriteBatch, HelmConsole console, bool isOpen, Vector2 origin, bool powered)
    {
        var (helmWidth, helmHeight) = FootprintPixelSize(CustomDeviceKind.Helm, console.Rotated);
        var rect = GetBlockRect(console.Position, helmWidth, helmHeight, origin);
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
        var (tableWidth, tableHeight) = FootprintPixelSize(CustomDeviceKind.CardTable);
        var rect = GetBlockRect(table.Position, tableWidth, tableHeight, origin);
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
        var (jukeboxWidth, jukeboxHeight) = FootprintPixelSize(CustomDeviceKind.Jukebox);
        var rect = GetBlockRect(jukebox.Block.Position, jukeboxWidth, jukeboxHeight, origin);
        var accent = isOpen ? Color.Gold : jukebox.On ? new Color(224, 196, 120) : new Color(140, 120, 90);
        // Lit means playing: the arch, the window and one pressed key all come up together, so the
        // machine says whether it is running without anybody reading a label.
        DrawDeviceFace(spriteBatch, rect, DeviceSkin.Face.Jukebox, jukebox.On, accent, isOpen ? 3 : 2);
        DrawDeviceLabel(spriteBatch, rect, "Музыка");
    }

    // A wall terminal - no panel of its own (one click on the block is the whole toggle gesture),
    // so nothing here ever draws an "isOpen" highlight the way the reactor/jukebox do. Off dims the
    // whole baked face to gray, the same convention DrawReactorBlock uses for its own texture.
    // Always a half-block flush against Terminal.FacingSide (direct user request, "занимал
    // половину блока и визуально выглядел в соответствии с полублоком") - recessed-in-a-half-thick-
    // wall or protruding-from-an-ordinary-wall alike, FacingSide already carries which side to draw
    // on directly (Ship.cs's own TileShipBuilder export sets it), no per-frame tile lookup needed.
    // Direct user bug report ("щитки отображались в игре а не была просто пустота") - JunctionBox
    // is purely decorative (no on/off, no interaction), so this reuses the EXACT same "no dedicated
    // Face yet" fallback look the Ship Editor's own palette/canvas already draws it as (Game1.
    // ShipEditor.Draw.cs's own 1x1-device fallback: an 18px tinted box, black outline, centered
    // glyph) - the real game and the editor preview now show the identical fixture, not a
    // placeholder in one and empty floor in the other.
    private const int JunctionBoxSize = 18;

    private void DrawJunctionBox(SpriteBatch spriteBatch, JunctionBox box, Vector2 origin)
    {
        var rect = GetBlockRect(box.Position, JunctionBoxSize, origin);
        spriteBatch.Draw(_pixel, rect, CustomDeviceCatalog.Tint(CustomDeviceKind.Junction));
        DrawRectOutline(spriteBatch, rect, Color.Black, 1);
        var glyph = CustomDeviceCatalog.ShortGlyph(CustomDeviceKind.Junction);
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
    private static readonly Color WallLampHousing = new(40, 36, 30);
    private static readonly Color WallLampGlow = new(255, 220, 140);
    private void DrawWallLamp(SpriteBatch spriteBatch, WallLamp lamp, Vector2 origin)
    {
        var center = origin + new Vector2((float)lamp.Position.X * PixelsPerUnit, (float)lamp.Position.Y * PixelsPerUnit);
        var rect = HalfRect(center, (int)PixelsPerUnit, lamp.FacingSide);
        spriteBatch.Draw(_pixel, rect, WallLampHousing);
        var glyphCenter = new Vector2(rect.Center.X, rect.Center.Y);
        HudIcons.FillCircle(spriteBatch, _pixel, glyphCenter, 7f, WallLampGlow * 0.35f);
        HudIcons.DrawPowerGlyph(spriteBatch, _pixel, glyphCenter, 1f, WallLampGlow);
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

    // A real periscope station instead of a flat marker square: a bolted octagonal floor block,
    // two handle grips a gunner would actually hold, cable stubs feeding into the deck, and a wide
    // flat terminal on top whose "screen" is a glowing blue-cyan projector lens - the thing the
    // gunner actually looks into - rather than a flat readout. Colour/glow carries the same idle/
    // manned/damaged read the old marker did, just on a model that looks like real hardware.
    private void DrawPeriscopeStation(SpriteBatch spriteBatch, Vector2 center, bool manned, bool damaged, float totalSeconds)
    {
        const float s = PeriscopeStationScale;
        var baseColor = new Color(58, 64, 70);
        var baseEdge = new Color(23, 26, 29);
        var octagon = Octagon(center, 9f * s);
        Primitives.FillPolygon(spriteBatch, _pixel, center, octagon, baseColor);
        Primitives.StrokePolygon(spriteBatch, _pixel, octagon, baseEdge, 1.5f * s);
        foreach (var vertex in octagon)
            spriteBatch.Draw(_pixel, new Rectangle((int)vertex.X - (int)s, (int)vertex.Y - (int)s, (int)(2 * s), (int)(2 * s)), baseEdge);

        // Handle grips, one on each side - lit gold while manned, same as the barrel it steers.
        foreach (var side in new[] { -1f, 1f })
        {
            var handleCenter = center + new Vector2(side * 13f * s, 0f);
            var handleRect = new Rectangle((int)(handleCenter.X - 4f * s), (int)(handleCenter.Y - 4f * s), (int)(8f * s), (int)(8f * s));
            spriteBatch.Draw(_pixel, handleRect, new Color(51, 56, 61));
            DrawRectOutline(spriteBatch, handleRect, baseEdge, (int)s);
            if (manned)
                HudIcons.DrawRingArc(spriteBatch, _pixel, handleCenter, 5f * s, 0f, 360f, Color.Gold * 0.7f, 8, s);
        }

        // Cable stubs trailing off toward the deck it's bolted into.
        HudIcons.DrawLine(spriteBatch, _pixel, center + new Vector2(-6f * s, 6f * s), center + new Vector2(-4f * s, 12f * s), baseEdge, 1.5f * s);
        HudIcons.DrawLine(spriteBatch, _pixel, center + new Vector2(6f * s, 6f * s), center + new Vector2(4f * s, 12f * s), baseEdge, 1.5f * s);

        // The wide flat terminal, offset slightly toward the "front".
        var terminalCenter = center + new Vector2(0f, -1f * s);
        var terminalRect = new Rectangle((int)(terminalCenter.X - 12f * s), (int)(terminalCenter.Y - 9f * s), (int)(24f * s), (int)(16f * s));
        spriteBatch.Draw(_pixel, terminalRect, new Color(49, 54, 60));
        DrawRectOutline(spriteBatch, terminalRect, baseEdge, (int)s);
        DrawRivets(spriteBatch, terminalRect);
        spriteBatch.Draw(_pixel, new Rectangle((int)(terminalRect.X + 3 * s), (int)(terminalRect.Bottom - 4 * s), (int)(6 * s), (int)(3 * s)), new Color(32, 36, 40));
        spriteBatch.Draw(_pixel, new Rectangle((int)(terminalRect.Right - 9 * s), (int)(terminalRect.Bottom - 4 * s), (int)(6 * s), (int)(3 * s)), new Color(32, 36, 40));

        var wellRect = new Rectangle((int)(terminalRect.X + 3 * s), (int)(terminalRect.Y + 2 * s), terminalRect.Width - (int)(6 * s), terminalRect.Height - (int)(7 * s));
        spriteBatch.Draw(_pixel, wellRect, new Color(10, 13, 16));
        var glowCenter = new Vector2(wellRect.Center.X, wellRect.Center.Y);

        Color outerGlow, midGlow, coreGlow;
        if (damaged)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(totalSeconds * 6f);
            outerGlow = new Color(90, 38, 30) * (0.3f + 0.2f * pulse);
            midGlow = new Color(161, 56, 42) * (0.5f + 0.3f * pulse);
            coreGlow = new Color(255, 138, 106) * (0.6f + 0.4f * pulse);
            HudIcons.DrawLine(spriteBatch, _pixel, glowCenter + new Vector2(-4f * s, -3f * s), glowCenter + new Vector2(4f * s, 3f * s), baseEdge, s);
            HudIcons.DrawLine(spriteBatch, _pixel, glowCenter + new Vector2(3f * s, -3f * s), glowCenter + new Vector2(-3f * s, 2f * s), baseEdge, s);
            spriteBatch.DrawString(_font, "!", center + new Vector2(12f * s, -20f * s), Color.Red, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);
        }
        else if (manned)
        {
            var pulse = 0.7f + 0.3f * MathF.Sin(totalSeconds * 3f);
            outerGlow = new Color(30, 122, 144) * 0.5f;
            midGlow = new Color(51, 182, 214) * 0.7f;
            coreGlow = new Color(143, 236, 255) * pulse;
            // A faint beam projecting "up" out of the lens - only shows while someone's actually
            // looking through it.
            Primitives.FillTriangle(spriteBatch, _pixel, glowCenter, glowCenter + new Vector2(-6f * s, -14f * s), glowCenter + new Vector2(6f * s, -14f * s), coreGlow * 0.18f);
        }
        else
        {
            outerGlow = new Color(30, 90, 102) * 0.35f;
            midGlow = new Color(47, 164, 194) * 0.55f;
            coreGlow = new Color(127, 224, 255) * 0.9f;
        }

        HudIcons.FillCircle(spriteBatch, _pixel, glowCenter, 4.5f * s, outerGlow);
        HudIcons.FillCircle(spriteBatch, _pixel, glowCenter, 3f * s, midGlow);
        HudIcons.FillCircle(spriteBatch, _pixel, glowCenter, 1.6f * s, coreGlow);
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
}
