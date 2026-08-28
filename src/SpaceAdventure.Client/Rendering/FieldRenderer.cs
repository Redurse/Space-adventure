using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Draws everything that lives in AsteroidField world space (asteroids, ore, dropped items, EVA
// characters) layered on top of the ship interior that ShipRenderer just drew - both use the same
// ship-local camera (origin/PixelsPerUnit), so there's no separate "outside" camera or scale
// anymore (game_design.md: "one continuous space, no hidden transition"). Every world-space point
// is converted into the ship's local frame via ShipLocalFrame.ToLocal before being placed on
// screen, exactly like ShipRenderer already places Room/Door/etc. - the ship's own rotation shows
// up as these objects swinging around the (always upright) ship, not as the interior spinning.
public sealed class FieldRenderer
{
    private const float EngineGlowMarginUnits = 0.3f;
    // Beyond EnemyWeaponRangeUnits(31)/NpcAggroRadius(60) with real headroom, so a raider's hull
    // has visibly taken shape well before it's close enough to shoot - not just a dot snapping into
    // a sprite at the last moment. Everything past this is rendered as a plain colored point instead
    // of a baked hull/triangle (DrawDistantShipDot) - the common case now that fields span real
    // system distances (M50), most ambient traffic and not-yet-engaged raiders are far past it.
    private const float ShipDetailRenderDistance = 220f;

    private readonly Texture2D _pixel;
    // A real celestial body's own disc, or its gravity well (M50), can be tens of thousands of
    // pixels across at this view's real field scale - HudIcons.FillCircle's per-pixel-row loop
    // would cost millions of Draw calls at that size ("нереально лагает"), so bodies use this
    // pre-baked circle texture (HudIcons.DrawScaledCircle, O(1) regardless of radius) instead.
    private readonly Texture2D _softCircle;
    private readonly SpriteFont _font;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly CrewSkin _crewSkin;
    private readonly EnemyHullSkin _enemyHulls;
    // One baked surface per rock (AsteroidTexture), kept for the life of the client - the same five
    // ids come back every time a field is entered, so this is built once and never again.
    private readonly Dictionary<string, AsteroidTexture.Skin> _asteroidSkins = new();
    // The actual pixel math (AsteroidTexture.BakePixels - the noise/crater/lighting loop over up
    // to 640x640 texels) measured at ~250-400ms for one rock at its real baked resolution - that
    // used to run synchronously inside DrawAsteroid, on the render thread, which is exactly what
    // showed up as "Field: ~300+ms" the moment any never-before-seen rock needed baking (a fresh
    // belt on first entry, or - the M55 landing feature - a planet surface's own dozen-plus
    // obstacles, all brand new the instant the ship touches down). Throttling to "one bake per
    // frame" (the old _bakedAsteroidThisFrame) never fixed that: each individual bake was still a
    // multi-hundred-millisecond stall, just spread over consecutive frames instead of stacked into
    // one. Moved onto a background task instead - BakePixels touches no GraphicsDevice state, only
    // local arrays/Random instances, so it's safe off the render thread; only the cheap
    // texture.SetData upload (a handful of ms) still happens here once a task completes.
    private readonly Dictionary<string, Task<(Color[] Pixels, int Side, float HalfExtentUnits)>> _pendingAsteroidBakes = new();
    // Same pattern, same reason, for a celestial body's own surface (PlanetTexture, M55 follow-up
    // - "почему на месте планет пустота") - a body only ever needs baking once per session (its
    // radius never changes), so keyed by id alone rather than "id:radius" the way a belt rock is.
    private readonly Dictionary<string, PlanetTexture.Skin> _planetSkins = new();
    private readonly Dictionary<string, Task<(Color[] Pixels, int Side, float HalfExtentUnits)>> _pendingPlanetBakes = new();
    // The engine glow's own displayed intensity, separate from the ship's actual instantaneous
    // thrust - snaps up the moment real thrust appears, but decays on its own once it drops back
    // to zero instead of cutting out the same instant the throttle is released, so easing off the
    // stick reads as the engines spooling down rather than a light switching off. Derived from
    // consecutive totalSeconds values (this method's only time input) rather than a real delta -
    // harmless if Draw is called more than once for the same frame (ExternalCameraPanel's own
    // quadrants share this same instance), since a repeated totalSeconds simply yields zero decay.
    private float _displayedEngineThrust;
    private float _lastEngineTotalSeconds = -1f;
    private const float EngineThrustFadePerSecond = 0.6f; // ~1.7s to fade fully from a dead stop

    public FieldRenderer(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _graphicsDevice = graphicsDevice;
        _crewSkin = new CrewSkin(graphicsDevice);
        _enemyHulls = new EnemyHullSkin(graphicsDevice);
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _softCircle = HudIcons.CreateSoftCircleTexture(graphicsDevice);
        _font = font;
    }

    // seenFromOutside: the caller has drawn the ship (and the station it's docked to) closed up
    // rather than as interiors - a turret periscope. The station then needs its exterior drawn even
    // while docked, or the gunner is looking at a black gap where the station they're moored to is.
    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, Vec2 hullCenter,
        Vector2 viewportOrigin, Vector2 viewportSize, float totalSeconds = 0f, IEnumerable<TransientEffect>? effects = null,
        bool seenFromOutside = false)
    {
        Vector2 WorldToScreen(Vec2 world)
        {
            var local = ShipLocalFrame.ToLocal(world, snapshot.ShipField, hullCenter);
            return origin + new Vector2((float)local.X, (float)local.Y) * ShipRenderer.PixelsPerUnit;
        }

        // M55 - landed on a planet's own surface: none of the system-field's own bodies/asteroids/
        // ore exist in this small, unrelated-scale local space (PlanetSurface's own coordinates
        // just happen to reuse the same "exterior world position" convention everything below
        // already draws through). Ground drawn first, same reason DrawCelestialBodies goes first
        // in the other branch - everything else should read as sitting on top of it.
        var landedBodyId = snapshot.Voyage.LandedBodyId;
        if (landedBodyId is not null)
        {
            DrawPlanetSurfaceGround(spriteBatch, landedBodyId, snapshot, WorldToScreen);
            foreach (var rock in PlanetSurface.Generate(landedBodyId))
                DrawAsteroid(spriteBatch, rock, WorldToScreen(rock.Position), WorldToScreen);
        }
        else
        {
            // The star, its planets, and any moons (M50) - real, huge, gravity-having bodies, drawn
            // first so everything else (asteroids, ships, characters) reads as being in front of
            // them rather than the other way round.
            DrawCelestialBodies(spriteBatch, snapshot, WorldToScreen);

            // At most one rock is baked per frame: five at once is a visible hitch on the frame the
            // field opens, five spread over five frames is nothing, and the ones still waiting are
            // drawn flat in their correct outline meanwhile.
            foreach (var asteroid in snapshot.Field.Asteroids)
                DrawAsteroid(spriteBatch, asteroid, WorldToScreen(asteroid.Position), WorldToScreen);

            foreach (var deposit in snapshot.Field.OreDeposits)
            {
                var state = snapshot.Field.OreDepositStates.FirstOrDefault(s => s.DepositId == deposit.Id);
                if (state is not null && state.Hp > 0f)
                    DrawOreBlock(spriteBatch, deposit, state, WorldToScreen(deposit.Position), totalSeconds);
            }
        }

        // RoomId is null only for the original EVA-space drops - anything dropped on a ship/station
        // floor (World.Storage.cs) belongs to ShipRenderer/StationRenderer instead, whose room-space
        // coordinates aren't this scene's ship-local exterior frame.
        foreach (var dropped in snapshot.DroppedItems.Where(d => d.RoomId is null))
            DrawDroppedItem(spriteBatch, dropped, WorldToScreen(dropped.Position), totalSeconds);

        // The cutting flame, out of the character toward whatever they're aiming at - the tool's
        // whole feedback, since the ore's progress bar only moves while this is on it.
        foreach (var character in snapshot.Characters.Where(c => c.Cutting && c.IsOutside))
        {
            var aim = ShipLocalFrame.ToLocalDirection(
                new Vec2(character.FacingX, character.FacingY), snapshot.ShipField.RotationDegrees);
            var direction = new Vector2((float)aim.X, (float)aim.Y);
            var center = WorldToScreen(new Vec2(character.X, character.Y));
            var muzzle = ShipRenderer.GetHeldToolMuzzle(ItemType.Cutter, character.Inventory, center, direction) ?? center + ShipRenderer.HeldToolOffset(direction);
            DrawCuttingFlame(spriteBatch, muzzle, direction, totalSeconds);
        }

        foreach (var character in snapshot.Characters.Where(c => c.Welding && c.IsOutside))
        {
            var aim = ShipLocalFrame.ToLocalDirection(
                new Vec2(character.FacingX, character.FacingY), snapshot.ShipField.RotationDegrees);
            var direction = new Vector2((float)aim.X, (float)aim.Y);
            var center = WorldToScreen(new Vec2(character.X, character.Y));
            var muzzle = ShipRenderer.GetHeldToolMuzzle(ItemType.WeldingTool, character.Inventory, center, direction) ?? center + ShipRenderer.HeldToolOffset(direction);
            DrawWeldingFlame(spriteBatch, _pixel, muzzle,
                direction, totalSeconds);
        }

        DrawEngines(spriteBatch, snapshot, origin, hullCenter, totalSeconds);

        // Only where a station actually exists in this system - many procedural systems have none
        // at all (GalaxyMap.cs), and the layout the World keeps around for docking is not a thing
        // in the sky when there's nothing nearby to anchor it to (snapshot.Voyage.HasNearbyStation).
        // Also skipped while landed (M55) - HasNearbyStation/Station.Position are frozen/stale by
        // then (UpdateNearestStation stops running the moment the ship lands), and numerically
        // meaningless against this small local surface field regardless.
        if (landedBodyId is null && snapshot.Voyage.HasNearbyStation)
        {
            var stationScreen = WorldToScreen(snapshot.Station.Position);
            var portScreen = WorldToScreen(snapshot.Station.DockingPortPosition);
            // Once docked the interior is drawn in full by StationRenderer, in these same
            // coordinates - the exterior would land exactly on top of it, so it's skipped rather
            // than double-drawn.
            if (snapshot.Voyage.DockedPointId is null || seenFromOutside)
                DrawStationExterior(spriteBatch, snapshot, WorldToScreen, stationScreen);
            DrawDockingPort(spriteBatch, portScreen, snapshot.CanDock, totalSeconds);
            // Off-screen bearings, so neither the station nor the berth can be lost track of during
            // a manual approach. The port gets its own marker because that's the thing to actually
            // aim at - the station's centre is solid and can't be docked with.
            DrawOffScreenMarker(spriteBatch, stationScreen, viewportOrigin, viewportSize, "Станция", Color.SteelBlue);
            DrawOffScreenMarker(spriteBatch, portScreen, viewportOrigin, viewportSize, "Шлюз", Color.LimeGreen);
        }

        // The whole squadron, each hull where it actually is. The one carrying the crew you'd board
        // is called out by name; the rest are marked as raiders, so it's obvious which one to fly
        // at with a suit on.
        var rotation = -snapshot.ShipField.RotationDegrees * (MathF.PI / 180f);
        var shipPosition = new Vec2(snapshot.ShipField.X, snapshot.ShipField.Y);
        foreach (var enemy in snapshot.EnemyShip.Ships)
        {
            var enemyWorld = new Vec2(enemy.X, enemy.Y);
            var enemyScreen = WorldToScreen(enemyWorld);

            // M50's real field sizes mean "far away" is now the common case - the baked hull
            // sprite, scorch marks, engine glow and health bar only earn their draw calls once
            // something might actually come to blows. A raider still crossing the field from far
            // out is rendered as the same plain dot ambient traffic gets below, and only "loads"
            // its full silhouette once it closes to ShipDetailRenderDistance.
            if ((enemyWorld - shipPosition).Length() <= ShipDetailRenderDistance)
            {
                DrawEnemyShipExterior(spriteBatch, enemyScreen, enemy,
                    enemy.IsBoardable ? snapshot.EnemyShip.Crew.Count(c => c.Alive) : -1,
                    rotation + (enemy.RotationDegrees * MathF.PI / 180f), totalSeconds);

                // Any wall the player has already cut through on the currently boardable hull - the
                // enemy-ship counterpart of ShipRenderer.DrawBreachedWallBlock, just placed by
                // rotating the block's local position out to world space (World.Cutting.cs uses the
                // identical maths server-side) instead of drawing straight into a room-local origin.
                if (enemy.IsBoardable && snapshot.EnemyShip.WallBlockStates.Count > 0)
                {
                    var enemyLocalCenter = ShipLocalFrame.GetHullCenter(snapshot.EnemyShip.Rooms);
                    foreach (var state in snapshot.EnemyShip.WallBlockStates)
                    {
                        if (!state.Breached)
                            continue;
                        var block = snapshot.EnemyShip.WallBlocks.FirstOrDefault(b => b.Id == state.Id);
                        if (block is null)
                            continue;
                        var blockWorld = enemyWorld +
                            ShipLocalFrame.ToWorldDirection(new Vec2(block.X, block.Y) - enemyLocalCenter, enemy.RotationDegrees);
                        DrawEnemyHullBreach(spriteBatch, WorldToScreen(blockWorld), totalSeconds);
                    }
                }
            }
            else
            {
                DrawDistantShipDot(spriteBatch, enemyScreen, Color.OrangeRed);
            }

            DrawOffScreenMarker(spriteBatch, enemyScreen, viewportOrigin, viewportSize,
                enemy.IsBoardable ? "Враг" : "Рейдер", Color.OrangeRed);
        }

        // Ambient traffic (World.NpcShips.cs, M43) - present and flying whether or not the player
        // has ever come near, unlike the squadron above which only exists during a fight. Almost
        // always far away at this scale (M50's real field sizes), so this is the common case the
        // dot fallback below is really for. Skipped while landed (M55) - every NPC position is
        // still a system-scale number, meaningless (and never actually near) this small local
        // surface field's own coordinates.
        foreach (var npc in landedBodyId is null ? snapshot.NpcShips : Array.Empty<NpcShipFieldState>())
        {
            var npcWorld = new Vec2(npc.X, npc.Y);
            var npcScreen = WorldToScreen(npcWorld);
            if ((npcWorld - shipPosition).Length() <= ShipDetailRenderDistance)
            {
                DrawNpcShipExterior(spriteBatch, npcScreen, npc, snapshot.FactionStandings,
                    rotation + (npc.RotationDegrees * MathF.PI / 180f));
            }
            else
            {
                DrawDistantShipDot(spriteBatch, npcScreen, NpcShipMarkerColor(npc, snapshot.FactionStandings));
            }

            DrawOffScreenMarker(spriteBatch, npcScreen, viewportOrigin, viewportSize,
                NpcShipLabel(npc), NpcShipMarkerColor(npc, snapshot.FactionStandings));
        }

        // M63 - drifting wreckage (World.ShipDebris.cs) - each fragment has its own independent
        // world position/rotation (unlike Station, which never rotates on its own), so it needs the
        // same "rotate this room's own offset out to world space" transform an enemy hull's wall
        // blocks already use, not the simpler station-exterior one.
        if (snapshot.ShipDebris is { Count: > 0 } debrisFragments)
            foreach (var fragment in debrisFragments)
                DrawShipDebris(spriteBatch, fragment, WorldToScreen, rotation);

        foreach (var shot in snapshot.Projectiles)
            DrawProjectile(spriteBatch, shot, WorldToScreen(new Vec2(shot.X, shot.Y)), rotation);

        // Outside, a character's facing is stored in field coordinates, but this whole scene is
        // drawn in the ship's frame - which swings as the ship turns. Drawn raw, the marker for
        // "which way am I looking" pointed somewhere else entirely whenever the hull wasn't level.
        foreach (var character in snapshot.Characters.Where(c => c.IsOutside))
        {
            var facing = ShipLocalFrame.ToLocalDirection(
                new Vec2(character.FacingX, character.FacingY), snapshot.ShipField.RotationDegrees);
            DrawCharacter(spriteBatch, character, WorldToScreen(new Vec2(character.X, character.Y)),
                new Vector2((float)facing.X, (float)facing.Y));
        }

        if (effects is not null)
        {
            foreach (var effect in effects.Where(e => e.Kind == EffectKind.Cut))
                DrawSparkBurst(spriteBatch, WorldToScreen(effect.Position), effect.Progress);
            foreach (var effect in effects.Where(e => e.Kind == EffectKind.Explosion))
                DrawExplosion(spriteBatch, WorldToScreen(effect.Position), effect.Progress);
        }
    }

    // The star, planets, and moons of whatever system the ship is currently in (M50) - fixed
    // positions now (M59), a pure function of the system id alone: CelestialBodyGenerator.Generate/
    // PositionAt take no time argument any more, so client and server can never show a body
    // anywhere other than where it actually sits.
    // Generate/ToDictionary/the StarSystems lookup below are all real allocations (a fresh Random,
    // several Lists, a Dictionary, LINQ enumerators) - fine once, ruinous run fresh every single
    // frame at 60fps, which is exactly what calling this with no caching did: a steady stream of
    // garbage that showed up as real, visible stutter (M50 follow-up - "нереально лагает"). The
    // system id and its body layout are deterministic and never change mid-flight, so this only
    // ever needs to be recomputed the one time the ship actually warps somewhere else.
    private string? _cachedBodiesSystemId;
    private IReadOnlyList<CelestialBody> _cachedBodies = Array.Empty<CelestialBody>();
    private Dictionary<string, CelestialBody> _cachedBodiesById = new();
    private Vec2 _cachedFieldCenter;

    private void EnsureBodiesCached(WorldSnapshot snapshot)
    {
        if (_cachedBodiesSystemId == snapshot.CurrentSystemId)
            return;
        _cachedBodiesSystemId = snapshot.CurrentSystemId;
        _cachedBodies = CelestialBodyGenerator.Generate(snapshot.CurrentSystemId);
        _cachedBodiesById = _cachedBodies.ToDictionary(b => b.Id);
        var currentSystem = snapshot.StarSystems.First(s => s.Id == snapshot.CurrentSystemId);
        _cachedFieldCenter = new Vec2(currentSystem.Width / 2f, currentSystem.Height / 2f);
    }

    // M55 - "камера поварачивалась вертикально относительно положения планеты чтобы было проще
    // садиться": how far the ship still is above the nearest LANDABLE body's own surface, and the
    // bearing away from that body's centre (the direction that should read as screen-up once
    // close) - Game1.Camera.cs's own scene-rotation blend uses this to ease the view from "flying
    // past a huge circle" into "ground below, sky above" purely as a landing approach aid, no
    // physics involved. Piggybacks on the same cached body list DrawCelestialBodies/
    // DrawPlanetSurfaceGround already keep warm, rather than re-generating the system's bodies in
    // a method called every single frame regardless of turret/landing state.
    public (float SurfaceDistance, Vec2 AwayFromBody, float BodyRadius)? NearestLandableBodyApproach(WorldSnapshot snapshot, Vec2 shipFieldPosition)
    {
        EnsureBodiesCached(snapshot);
        CelestialBody? nearestBody = null;
        var nearestSurfaceDistance = float.MaxValue;
        var nearestAway = Vec2.Zero;

        foreach (var body in _cachedBodies)
        {
            if (!CelestialBodyGenerator.IsLandable(body))
                continue;
            var bodyPosition = CelestialBodyGenerator.PositionAt(body, _cachedBodiesById) + _cachedFieldCenter;
            var offset = shipFieldPosition - bodyPosition;
            var distance = offset.Length();
            var surfaceDistance = distance - body.Radius;
            if (surfaceDistance >= nearestSurfaceDistance)
                continue;
            nearestSurfaceDistance = (float)surfaceDistance;
            nearestBody = body;
            nearestAway = distance > 0.0001f ? offset * (1f / distance) : new Vec2(0f, -1f);
        }

        return nearestBody is null ? null : (nearestSurfaceDistance, nearestAway, nearestBody.Radius);
    }

    // M55 - a flat, MassTier-tinted ground fill under a landed ship, drawn as a single rotated
    // quad rather than per-pixel: WorldToScreen's own rotation (ShipLocalFrame.ToLocal) is a pure
    // rigid rotation, so an axis-aligned rectangle in PlanetSurface's own local coordinates stays
    // a rectangle on screen, just turned - exactly what stretching/rotating a single pixel already
    // draws for free, the same trick DrawEngines/DrawProjectile etc. already use below for their
    // own rotated rectangles.
    private void DrawPlanetSurfaceGround(SpriteBatch spriteBatch, string bodyId, WorldSnapshot snapshot, Func<Vec2, Vector2> worldToScreen)
    {
        EnsureBodiesCached(snapshot);
        var color = _cachedBodiesById.TryGetValue(bodyId, out var body) ? CelestialBodyColor(body.MassTier) : Color.Gray;
        var center = worldToScreen(PlanetSurface.Center);
        var rotation = -snapshot.ShipField.RotationDegrees * (MathF.PI / 180f);
        var size = new Vector2(PlanetSurface.Width, PlanetSurface.Height) * ShipRenderer.PixelsPerUnit;
        spriteBatch.Draw(_pixel, center, null, color, rotation, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
    }

    private void DrawCelestialBodies(SpriteBatch spriteBatch, WorldSnapshot snapshot, Func<Vec2, Vector2> worldToScreen)
    {
        EnsureBodiesCached(snapshot);
        var bodies = _cachedBodies;
        var byId = _cachedBodiesById;
        var fieldCenter = _cachedFieldCenter;

        foreach (var body in bodies)
        {
            // PositionAt is relative to the star's own local origin - the field's own centre is
            // where that origin actually sits in absolute field coordinates, so worldToScreen needs
            // it added back in too.
            var screen = worldToScreen(CelestialBodyGenerator.PositionAt(body, byId) + fieldCenter);
            var radiusPx = body.Radius * ShipRenderer.PixelsPerUnit;
            var color = CelestialBodyColor(body.MassTier);

            // The star keeps the plain flat glow (M55 follow-up) - it isn't a solid surface to put
            // relief or cloud bands on, and the existing bright disc + limb shading already reads
            // fine for something that's meant to look like a light source, not a walkable body.
            if (body.MassTier == BodyMassTier.Star)
            {
                HudIcons.DrawScaledCircle(spriteBatch, _softCircle, screen, radiusPx, color);
                HudIcons.DrawScaledCircle(spriteBatch, _softCircle, screen + new Vector2(radiusPx * 0.18f, radiusPx * 0.18f), radiusPx * 0.92f, color * 0.8f);
                HudIcons.DrawRingArc(spriteBatch, _pixel, screen, radiusPx, 0f, 360f, Color.Black * 0.35f, 96, 2f);
                continue;
            }

            // Rotated by the same amount the field itself is rotated on screen (DrawAsteroid's own
            // convention) - sunlight/craters/cloud bands stay fixed in the world while everything
            // swings around the always-upright ship, instead of the texture just sitting frozen
            // while its position correctly swings around it.
            var axis = worldToScreen(CelestialBodyGenerator.PositionAt(body, byId) + fieldCenter + new Vec2(1f, 0f)) - screen;
            var rotation = MathF.Atan2(axis.Y, axis.X);
            DrawPlanetSkin(spriteBatch, body, screen, radiusPx, color, rotation);
        }
    }

    // M55 follow-up - "почему на месте планет пустота": a real baked surface (PlanetTexture)
    // instead of the flat shaded circle, baked once per body id on a background task exactly like
    // DrawAsteroid's own rock bakes (PlanetTexture.BakePixels costs the same multi-hundred-ms as
    // AsteroidTexture's own at this resolution - doing that synchronously here would reintroduce
    // the exact frame-lag bug DrawAsteroid was just fixed for). Falls back to the old flat disc
    // for however many frames the bake takes, same "waiting its turn" shape as an unbaked rock.
    private void DrawPlanetSkin(SpriteBatch spriteBatch, CelestialBody body, Vector2 screen, float radiusPx, Color color, float rotation)
    {
        if (!_planetSkins.TryGetValue(body.Id, out var skin))
        {
            if (!_pendingPlanetBakes.TryGetValue(body.Id, out var pending))
            {
                pending = Task.Run(() => PlanetTexture.BakePixels(body));
                _pendingPlanetBakes[body.Id] = pending;
            }

            if (!pending.IsCompletedSuccessfully)
            {
                HudIcons.DrawScaledCircle(spriteBatch, _softCircle, screen, radiusPx, color);
                HudIcons.DrawScaledCircle(spriteBatch, _softCircle, screen + new Vector2(radiusPx * 0.18f, radiusPx * 0.18f), radiusPx * 0.92f, color * 0.8f);
                HudIcons.DrawRingArc(spriteBatch, _pixel, screen, radiusPx, 0f, 360f, Color.Black * 0.35f, 96, 2f);
                return;
            }

            var (pixels, side, halfExtentUnits) = pending.Result;
            var texture = new Texture2D(_graphicsDevice, side, side);
            texture.SetData(pixels);
            skin = new PlanetTexture.Skin(texture, halfExtentUnits);
            _planetSkins[body.Id] = skin;
            _pendingPlanetBakes.Remove(body.Id);
        }

        var scale = skin.HalfExtentUnits * 2f * ShipRenderer.PixelsPerUnit / skin.Texture.Width;
        spriteBatch.Draw(skin.Texture, screen, null, Color.White, rotation,
            new Vector2(skin.Texture.Width / 2f, skin.Texture.Height / 2f), scale, SpriteEffects.None, 0f);
        HudIcons.DrawRingArc(spriteBatch, _pixel, screen, radiusPx, 0f, 360f, Color.Black * 0.35f, 96, 2f);
    }

    private static Color CelestialBodyColor(BodyMassTier tier) => tier switch
    {
        BodyMassTier.Star => new Color(255, 230, 140),
        BodyMassTier.Rocky => new Color(150, 120, 95),
        BodyMassTier.IceGiant => new Color(170, 195, 220),
        BodyMassTier.GasGiant => new Color(200, 160, 110),
        _ => new Color(180, 180, 180), // Moon
    };

    // A bigger, longer-lived burst than DrawSparkBurst's tool feedback - an expanding shockwave
    // ring plus a scatter of hull-chunk debris flung outward, for the one moment a whole ship
    // actually comes apart rather than just taking a hit.
    private void DrawExplosion(SpriteBatch spriteBatch, Vector2 center, float progress)
    {
        var alpha = 1f - progress;
        var ringRadius = 8f + progress * 60f;
        HudIcons.DrawRingArc(spriteBatch, _pixel, center, ringRadius, 0f, 360f, new Color(255, 200, 120) * (alpha * 0.7f), 24, 3f);

        var coreRadius = MathF.Max(0f, 22f * (1f - progress * 2f));
        if (coreRadius > 0f)
        {
            HudIcons.FillCircle(spriteBatch, _pixel, center, coreRadius, Color.White * (alpha * 0.9f));
            HudIcons.FillCircle(spriteBatch, _pixel, center, coreRadius * 0.6f, new Color(255, 200, 90) * alpha);
        }

        const int debrisCount = 8;
        var random = new Random(center.GetHashCode());
        for (var i = 0; i < debrisCount; i++)
        {
            var angle = i * MathF.PI * 2f / debrisCount + (float)random.NextDouble() * 0.5f;
            var speed = 40f + (float)random.NextDouble() * 70f;
            var chunk = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed * progress;
            var size = 4f * (1f - progress);
            if (size > 0.3f)
                spriteBatch.Draw(_pixel, chunk, null, new Color(70, 60, 55) * alpha, angle, new Vector2(0.5f, 0.5f),
                    new Vector2(size * 1.6f, size), SpriteEffects.None, 0f);
        }
    }

    // One nacelle per engine block the ship actually carries, hung on the outside of the plating
    // nearest that block, with its exhaust burning outward. Reading the position off the engine
    // devices rather than from a fixed point means a hull with two engines shows two of them, in
    // the compartments they're really installed in - which is the whole point of a class built
    // around a pair of them. Drawn in the ship-local frame (no rotation needed, see the
    // class-level note), so they stay glued to the hull exactly like everything ShipRenderer draws.
    private void DrawEngines(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, Vec2 hullCenter, float totalSeconds)
    {
        // Only burning where the ship can actually fly: a joystick pushed at a docked ship commands
        // nothing, and lit engines against a station berth would be a lie the display tells.
        var underWay = snapshot.Voyage.DockedPointId is null;
        var rawThrust = underWay
            ? Math.Clamp(new Vector2(snapshot.ShipField.ThrustX, snapshot.ShipField.ThrustY).Length(), 0f, 1f)
            : 0f;

        // Snaps up to the real value the instant thrust appears, but only ever decays toward it
        // over time rather than cutting to it - easing off the throttle reads as the engines
        // spooling down, not a light switching off.
        var deltaSeconds = _lastEngineTotalSeconds < 0f ? 0f : Math.Max(0f, totalSeconds - _lastEngineTotalSeconds);
        _lastEngineTotalSeconds = totalSeconds;
        _displayedEngineThrust = MathF.Max(rawThrust, _displayedEngineThrust - EngineThrustFadePerSecond * deltaSeconds);
        var thrust = _displayedEngineThrust;
        var halfExtents = ShipLocalFrame.GetHullHalfExtents(snapshot.Rooms);

        foreach (var device in snapshot.SystemDevices)
        {
            if (device.System != PowerSystemId.Engine)
                continue;

            // Which face of the hull this engine is closest to, and therefore which way it fires.
            // Raw axis comparison rather than one normalised by the hull's proportions: on a long
            // thin hull an engine one unit off the centreline still belongs on the end plating.
            var toDevice = device.Position - hullCenter;
            var outward = MathF.Abs((float)toDevice.X) >= MathF.Abs((float)toDevice.Y)
                ? new Vector2(MathF.Sign((float)toDevice.X) is var sx && sx == 0 ? 1f : sx, 0f)
                : new Vector2(0f, MathF.Sign((float)toDevice.Y));

            var baseLocal = outward.X != 0f
                ? new Vector2((float)(hullCenter.X + outward.X * halfExtents.X), device.Y)
                : new Vector2(device.X, (float)(hullCenter.Y + outward.Y * halfExtents.Y));
            var baseScreen = origin + baseLocal * ShipRenderer.PixelsPerUnit;

            DrawNacelle(spriteBatch, baseScreen, outward, device.SizeScale, thrust, totalSeconds, device.Id);
        }
    }

    private void DrawNacelle(SpriteBatch spriteBatch, Vector2 baseScreen, Vector2 outward, float sizeScale, float thrust, float totalSeconds, string deviceId)
    {
        var rotation = MathF.Atan2(outward.Y, outward.X);
        var housingLength = 22f * sizeScale;
        var housingWidth = 26f * sizeScale;

        // Housing straddles the plating so it reads as bolted on rather than floating alongside.
        var housingCenter = baseScreen + outward * (housingLength * 0.3f);
        spriteBatch.Draw(_pixel, housingCenter, null, new Color(70, 78, 90), rotation, new Vector2(0.5f, 0.5f),
            new Vector2(housingLength, housingWidth), SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, housingCenter + outward * (housingLength * 0.45f), null, new Color(40, 44, 52), rotation,
            new Vector2(0.5f, 0.5f), new Vector2(housingLength * 0.25f, housingWidth * 0.85f), SpriteEffects.None, 0f);

        // FTL's ships never go dark at the tail - a small blue idle glow burns at every nacelle
        // whether or not anyone's on the stick, and only grows into a full flame under real thrust.
        // A chemical-rocket orange cone at rest would read as "on fire", not "idling".
        var phase = totalSeconds * 22f + deviceId.Length * 1.7f;
        var flicker = 0.75f + 0.25f * MathF.Sin(phase);
        var idlePulse = 0.85f + 0.15f * MathF.Sin(totalSeconds * 2.4f + deviceId.Length * 0.9f);
        var mouth = baseScreen + outward * (housingLength * 0.75f);
        var idleLength = 9f * idlePulse * sizeScale;
        // Long enough at full burn to read as a real drive flame, not a hot spark at the tail -
        // the pilot flying by this exact glow (now also drawn while at the helm, not just when
        // walking the ship's own corridors) is the one who most needs it to be unmistakable.
        var burnLength = thrust > 0.05f ? (18f + thrust * 90f) * flicker * sizeScale : 0f;
        var flameLength = MathF.Max(idleLength, burnLength);
        var flameWidth = housingWidth * (thrust > 0.05f ? 0.7f : 0.5f);
        var burn = Math.Clamp(thrust * 3f, 0f, 1f);

        // Deep blue outer glow, then a brighter cyan core, then a near-white hot centre - blended
        // toward the same palette whether it's a bare idle flicker or a full burn, so the ramp-up
        // never has a colour seam.
        spriteBatch.Draw(_pixel, mouth, null, new Color(40, 90, 220) * (0.30f + burn * 0.35f), rotation, new Vector2(0f, 0.5f),
            new Vector2(flameLength, flameWidth), SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, mouth, null, new Color(90, 180, 255) * (0.55f + burn * 0.35f), rotation, new Vector2(0f, 0.5f),
            new Vector2(flameLength * 0.6f, flameWidth * 0.6f), SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, mouth, null, new Color(220, 240, 255) * (0.6f + burn * 0.4f), rotation, new Vector2(0f, 0.5f),
            new Vector2(flameLength * 0.3f, flameWidth * 0.32f), SpriteEffects.None, 0f);

        // A drifting trail behind the flame proper, only once there's real thrust to leave one -
        // stateless like the flicker above (no actual particles to track), just dots recomputed
        // every frame from how far totalSeconds has carried each one along a repeating cycle.
        if (burn > 0.15f)
        {
            const int trailCount = 6;
            const float trailRange = 90f;
            for (var i = 0; i < trailCount; i++)
            {
                var cycle = (totalSeconds * 46f + i * (trailRange / trailCount) + deviceId.Length * 3f) % trailRange / trailRange;
                var dist = flameLength + cycle * trailRange * sizeScale;
                var dotAlpha = (1f - cycle) * burn * 0.5f;
                var dotSize = (1f - cycle) * flameWidth * 0.4f + 1.5f;
                var dotPosition = mouth + outward * dist;
                spriteBatch.Draw(_pixel, dotPosition, null, new Color(120, 190, 255) * dotAlpha, 0f, new Vector2(0.5f, 0.5f),
                    new Vector2(dotSize, dotSize), SpriteEffects.None, 0f);
            }
        }
    }

    // The station seen from open space: its actual compartments, one block per room, at the exact
    // positions you'll walk through after docking - not a stand-in silhouette. Snapshot rooms are
    // in the docked frame, so +StationWorldOffset puts them in field coordinates; from there the
    // usual ship-local fold applies, which means a rotated ship sees the station rotated the other
    // way, hence the rotated draws rather than plain rectangles.
    private void DrawStationExterior(SpriteBatch spriteBatch, WorldSnapshot snapshot,
        Func<Vec2, Vector2> worldToScreen, Vector2 screenCenter)
    {
        var rotation = -snapshot.ShipField.RotationDegrees * (MathF.PI / 180f);
        foreach (var room in snapshot.Station.Rooms)
        {
            var center = room.Center + snapshot.Station.WorldOffset;
            var size = new Vector2(room.Width, room.Height) * ShipRenderer.PixelsPerUnit;
            var screen = worldToScreen(center);
            spriteBatch.Draw(_pixel, screen, null, new Color(52, 60, 74), rotation, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
            DrawRotatedOutline(spriteBatch, screen, size, rotation, new Color(96, 108, 126));
        }

        spriteBatch.DrawString(_font, "Станция", screenCenter + new Vector2(-24, -90), Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    // M63 - a fragment's own Rooms are stored relative to ITS pivot (World.ShipDebris.cs's own doc
    // comment), so each room's world position is the fragment's world position plus that offset
    // rotated by the fragment's own (independent, frozen-at-detachment) rotation - the identical
    // transform DrawEnemyShipExterior's own wall-block-breach loop above already uses for an enemy
    // hull's blocks, just for whole rooms instead of single blocks.
    private void DrawShipDebris(SpriteBatch spriteBatch, ShipDebrisState fragment, Func<Vec2, Vector2> worldToScreen, float cameraRotation)
    {
        var fragmentWorld = new Vec2(fragment.X, fragment.Y);
        var drawRotation = cameraRotation + fragment.RotationDegrees * MathF.PI / 180f;
        foreach (var room in fragment.Rooms)
        {
            var roomWorld = fragmentWorld + ShipLocalFrame.ToWorldDirection(room.Center, fragment.RotationDegrees);
            var screen = worldToScreen(roomWorld);
            var size = new Vector2(room.Width, room.Height) * ShipRenderer.PixelsPerUnit;
            spriteBatch.Draw(_pixel, screen, null, new Color(70, 62, 58), drawRotation, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
            DrawRotatedOutline(spriteBatch, screen, size, drawRotation, new Color(120, 108, 100));
        }
    }

    // Four thin rotated bars around the block's edges - SpriteBatch has no rotated-rect outline, so
    // each side is drawn as its own rotated sliver placed by rotating its own offset from the centre.
    private void DrawRotatedOutline(SpriteBatch spriteBatch, Vector2 center, Vector2 size, float rotation, Color color)
    {
        const float thickness = 3f;
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        Vector2 Rotate(Vector2 v) => new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);

        foreach (var (offset, bar) in new[]
                 {
                     (new Vector2(0, -size.Y / 2), new Vector2(size.X, thickness)),
                     (new Vector2(0, size.Y / 2), new Vector2(size.X, thickness)),
                     (new Vector2(-size.X / 2, 0), new Vector2(thickness, size.Y)),
                     (new Vector2(size.X / 2, 0), new Vector2(thickness, size.Y)),
                 })
            spriteBatch.Draw(_pixel, center + Rotate(offset), null, color, rotation, new Vector2(0.5f, 0.5f), bar, SpriteEffects.None, 0f);
    }

    // The berth itself, drawn apart from the station's bulk so it's clear *where* to bring the
    // ship (World.StationDocking.cs). Pulses green once the ship is close and slow enough for the
    // helm's "Стыковка" button to arm, so the two readouts agree.
    private void DrawDockingPort(SpriteBatch spriteBatch, Vector2 screenCenter, bool armed, float totalSeconds)
    {
        var pulse = armed ? 0.65f + 0.35f * MathF.Sin(totalSeconds * 5f) : 0.7f;
        var color = (armed ? Color.LimeGreen : Color.SteelBlue) * pulse;
        DrawGlowDiamond(spriteBatch, screenCenter, 20, color);
        spriteBatch.DrawString(_font, armed ? "ШЛЮЗ — готов" : "Шлюз станции",
            screenCenter + new Vector2(-30, 16), armed ? Color.LimeGreen : Color.LightSteelBlue,
            0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    // Keeps a target findable when it's off screen: a marker pinned to the edge of the viewport in
    // its direction, labelled with the distance still to cover. Without this the station simply
    // vanishes the moment you drift past it, and the manual approach becomes guesswork.
    private void DrawOffScreenMarker(SpriteBatch spriteBatch, Vector2 target, Vector2 viewportOrigin, Vector2 viewportSize,
        string label, Color color)
    {
        var bounds = new Rectangle((int)viewportOrigin.X, (int)viewportOrigin.Y, (int)viewportSize.X, (int)viewportSize.Y);
        if (bounds.Contains((int)target.X, (int)target.Y))
            return;

        const float margin = 18f;
        var center = viewportOrigin + viewportSize / 2f;
        var direction = target - center;
        if (direction.LengthSquared() < 0.001f)
            return;
        direction.Normalize();

        // Walk out from the centre until the edge - simpler and more robust than solving the
        // rectangle intersection, and precise enough for a HUD arrow.
        var halfWidth = viewportSize.X / 2f - margin;
        var halfHeight = viewportSize.Y / 2f - margin;
        var scale = Math.Min(
            Math.Abs(direction.X) > 0.0001f ? halfWidth / Math.Abs(direction.X) : float.MaxValue,
            Math.Abs(direction.Y) > 0.0001f ? halfHeight / Math.Abs(direction.Y) : float.MaxValue);
        var edge = center + direction * scale;

        var rotation = MathF.Atan2(direction.Y, direction.X);
        spriteBatch.Draw(_pixel, edge, null, color, rotation, new Vector2(0.5f, 0.5f), new Vector2(20f, 5f), SpriteEffects.None, 0f);

        var distanceUnits = (target - center).Length() / ShipRenderer.PixelsPerUnit;
        spriteBatch.DrawString(_font, $"{label} {distanceUnits:0}", edge + new Vector2(-24, 8), color,
            0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    // The enemy ship as something you can physically fly to and board (game_design.md Phase 3) -
    // only drawn during a battle, since that's the only time it exists in field space at all.
    // The green marker is the hull breach: drift into it suited and you're aboard.
    // crewAlive < 0 means this isn't the hull the boarding party would enter, so it gets no breach
    // marker and no crew count - the breach is a promise that you can get in, and it should only be
    // made about the ship that actually has an interior behind it.
    // An asymmetric hostile hull rather than a plain rectangle - a jagged raider silhouette,
    // weathered rust patches, a glowing engine at the tail, and scorch marks that accumulate as
    // its own health drops, so a fight against it visibly wears the ship down the way the
    // player's own hull already shows damage (HullSkin's DrawHullDamage).
    private void DrawEnemyShipExterior(SpriteBatch spriteBatch, Vector2 screenCenter,
        EnemyShipFieldState enemy, int crewAlive, float rotation, float totalSeconds)
    {
        // The hull's own real footprint (EnemyShipLayout.Classes.cs), not a uniform stand-in
        // diameter - a Frigate (deliberately Corvette-sized) now actually reads as bigger on
        // screen than a Raider. World.EnemyHullRadius (the shell hit-test circle) stays a fixed
        // 3.5 for every class regardless - a bigger hull just means a shot can land visibly on the
        // plating well outside that circle without it having missed a smaller one.
        var (_, halfExtents) = EnemyShipLayout.Of(enemy.Kind).GetLocalBounds();
        var sizePx = (float)(halfExtents.Length() * 2f * ShipRenderer.PixelsPerUnit);

        // A baked hull per class, at its own true scale - the same armour HullSkin draws for the
        // player's own ship, run once offscreen against this class's real Rooms (EnemyHullSkin).
        var (hull, hullOrigin) = _enemyHulls.Get(enemy.Kind);
        // Muted rather than a second bake for the retreating state - approximates the old bake's
        // own colour mix (Mix(baseColour, darker, 0.5f)) as a straight tint on the already-drawn
        // armour instead.
        var tint = enemy.IsRetreating ? new Color(150, 138, 128) : Color.White;
        spriteBatch.Draw(hull, screenCenter, null, tint, rotation, hullOrigin, 1f, SpriteEffects.None, 0f);

        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        Vector2 Local(float x, float y)
        {
            var s = new Vector2(x, y) * sizePx;
            return screenCenter + new Vector2(s.X * cos - s.Y * sin, s.X * sin + s.Y * cos);
        }

        // The engine flare pulses, so it stays live rather than being baked into the hull: a
        // derelict that can still manoeuvre is one that can still fight.
        var enginePulse = 0.7f + 0.3f * MathF.Sin(totalSeconds * 3f + enemy.Id.GetHashCode());
        var engineColor = enemy.IsRetreating ? new Color(200, 120, 40) : new Color(255, 90, 40);
        HudIcons.FillCircle(spriteBatch, _pixel, Local(-0.46f, 0.02f), sizePx * 0.06f * enginePulse,
            engineColor * 0.75f);

        // Scorch marks that accumulate as the hull loses health - none at full health, several
        // near death, so the fight's progress is visible on the ship itself, not just its bar.
        var damageFraction = enemy.MaxHp > 0 ? 1f - Math.Clamp(enemy.Hp / enemy.MaxHp, 0f, 1f) : 0f;
        var scorchCount = (int)(damageFraction * 5f);
        for (var i = 0; i < scorchCount; i++)
        {
            var scorch = new Random(enemy.Id.GetHashCode() + i * 31);
            var position = Local(-0.35f + (float)scorch.NextDouble() * 0.7f, -0.3f + (float)scorch.NextDouble() * 0.6f);
            HudIcons.FillCircle(spriteBatch, _pixel, position, sizePx * (0.04f + (float)scorch.NextDouble() * 0.05f), Color.Black * 0.5f);
            HudIcons.FillCircle(spriteBatch, _pixel, position, sizePx * 0.025f, new Color(255, 100, 40) * 0.3f);
        }

        DrawEnemyHealthBar(spriteBatch, screenCenter, sizePx, enemy);

        if (crewAlive >= 0)
        {
            DrawGlowDiamond(spriteBatch, screenCenter - new Vector2(sizePx / 2, 0), 16, Color.LimeGreen * 0.9f); // the breach to climb through
            var label = crewAlive > 0 ? $"Враг (экипаж: {crewAlive})" : "Враг (зачищен)";
            spriteBatch.DrawString(_font, label, screenCenter + new Vector2(-40, -sizePx / 2 - 16), Color.OrangeRed, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        }
    }

    // A player-cut hole in the hull, distinct from the always-open green-diamond breach above
    // (DrawEnemyShipExterior) - this one only appears once World.Cutting.cs actually reports the
    // block as breached, at the exact world position the cutting/boarding logic itself uses, same
    // "black square + flickering hazard outline" language ShipRenderer.DrawBreachedWallBlock uses
    // for the player's own hull.
    private void DrawEnemyHullBreach(SpriteBatch spriteBatch, Vector2 screenCenter, float totalSeconds)
    {
        const int size = 22;
        var rect = new Rectangle((int)screenCenter.X - size / 2, (int)screenCenter.Y - size / 2, size, size);
        spriteBatch.Draw(_pixel, rect, Color.Black);
        var flicker = 0.5f + 0.5f * MathF.Sin(totalSeconds * 6f);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, Color.OrangeRed * flicker, 2);
    }

    private void DrawEnemyHealthBar(SpriteBatch spriteBatch, Vector2 screenCenter, float sizePx, EnemyShipFieldState enemy)
    {
        const int barWidth = 54;
        const int barHeight = 5;
        var barOrigin = new Vector2(screenCenter.X - barWidth / 2f, screenCenter.Y - sizePx * 0.35f - 10);
        var rect = new Rectangle((int)barOrigin.X, (int)barOrigin.Y, barWidth, barHeight);
        spriteBatch.Draw(_pixel, rect, Color.Black * 0.7f);
        var fraction = enemy.MaxHp > 0 ? Math.Clamp(enemy.Hp / enemy.MaxHp, 0f, 1f) : 0f;
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, (int)(barWidth * fraction), barHeight),
            enemy.IsRetreating ? Color.Goldenrod : Color.OrangeRed);
    }

    // Whether this NPC's own faction currently hates the player enough to fight - the same
    // threshold World.NpcShips.cs's TryEngageHostileNpc checks server-side, just read off the
    // standings the snapshot already carries rather than duplicating the number.
    private static bool IsNpcHostile(NpcShipFieldState npc, IReadOnlyList<FactionStandingState> standings) =>
        npc.Kind == NpcShipKind.Military &&
        (standings.FirstOrDefault(s => s.Faction == npc.FactionId)?.Standing ?? 0) <= FactionDefinitions.HostileThreshold;

    private static string NpcShipLabel(NpcShipFieldState npc) => npc.Kind switch
    {
        NpcShipKind.Cargo => "Транспорт",
        NpcShipKind.Scout => "Разведчик",
        _ => "Патруль",
    };

    private static Color NpcShipMarkerColor(NpcShipFieldState npc, IReadOnlyList<FactionStandingState> standings) =>
        IsNpcHostile(npc, standings) ? Color.OrangeRed : npc.Kind switch
        {
            NpcShipKind.Cargo => Color.SteelBlue,
            NpcShipKind.Scout => Color.LightGray,
            _ => Color.CornflowerBlue,
        };

    // The far-away stand-in for both DrawEnemyShipExterior and DrawNpcShipExterior (ShipDetailRenderDistance) -
    // a hull that's still just closing the distance doesn't need its silhouette, scorch marks or
    // engine glow evaluated every frame, only something readable enough to track its heading by eye
    // until it's close enough to matter.
    private void DrawDistantShipDot(SpriteBatch spriteBatch, Vector2 screenCenter, Color color)
    {
        const float radius = 4f;
        HudIcons.FillCircle(spriteBatch, _pixel, screenCenter, radius, color);
    }

    // A plain triangular hull, unlike the raider's scavenged silhouette (DrawEnemyShipExterior) -
    // ambient traffic isn't a fight to read the shape of, just a coloured blip with a heading.
    private void DrawNpcShipExterior(SpriteBatch spriteBatch, Vector2 screenCenter,
        NpcShipFieldState npc, IReadOnlyList<FactionStandingState> standings, float rotation)
    {
        const float npcVisualRadius = 3f;
        var sizePx = npcVisualRadius * 2 * ShipRenderer.PixelsPerUnit;
        var color = NpcShipMarkerColor(npc, standings);
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        Vector2 Local(float x, float y)
        {
            var s = new Vector2(x, y) * sizePx;
            return screenCenter + new Vector2(s.X * cos - s.Y * sin, s.X * sin + s.Y * cos);
        }

        var hull = new[] { Local(0.5f, 0f), Local(-0.35f, -0.28f), Local(-0.35f, 0.28f) };
        Primitives.FillPolygon(spriteBatch, _pixel, screenCenter, hull, color * 0.85f);
        Primitives.StrokePolygon(spriteBatch, _pixel, hull, Color.Black * 0.5f, 2f);
    }

    // A shot in flight, drawn as a short streak along its heading rather than a dot: at these
    // speeds a dot reads as a flicker, while a streak reads as something travelling - which is the
    // whole point of it being a projectile you can watch miss.
    private void DrawProjectile(SpriteBatch spriteBatch, ProjectileState shot, Vector2 screen, float frameRotation)
    {
        var heading = frameRotation + shot.DirectionDegrees * (MathF.PI / 180f);
        var (length, thickness) = shot.IsLaser ? (26f, 3f) : (14f, 4f);
        var color = shot.FromEnemy ? Color.OrangeRed : shot.IsLaser ? Color.Aqua : Color.Gold;

        spriteBatch.Draw(_pixel, screen, null, color * 0.35f, heading, new Vector2(1f, 0.5f), new Vector2(length * 1.6f, thickness), SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, screen, null, color, heading, new Vector2(1f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    // Rock chips flying off a cut ore deposit (game_design.md Phase 3, M18) - same "rays radiating
    // out, fading over TransientEffect.Progress" language as ShipRenderer's weld/repair sparks,
    // just gold/orange instead of white/green to read as stone rather than metal.
    private void DrawSparkBurst(SpriteBatch spriteBatch, Vector2 center, float progress)
    {
        var alpha = 1f - progress;
        var length = 4f + progress * 10f;
        const int rayCount = 5;
        for (var i = 0; i < rayCount; i++)
        {
            var angle = i * MathF.PI * 2f / rayCount - progress * 1.5f;
            spriteBatch.Draw(_pixel, center, null, Color.OrangeRed * alpha, angle, new Vector2(0f, 0.5f), new Vector2(length, 2f), SpriteEffects.None, 0f);
        }
    }

    // Game_design.md Phase 3, M18 - a lit-up vein you can still cut vs. one worked out (filtered
    // out above, since an exhausted deposit is indistinguishable from bare rock). Drawn as a
    // slowly pulsing glowing crystal (diamond) rather than a flat square, so it reads as an ore
    // vein embedded in rock instead of a UI marker.
    // A short blue torch flame: a bright core, a wider cooler envelope and a flicker, drawn along
    // the aim direction. Its length is the cutter's real reach (World.Cutting.cs), so what you see
    // is exactly what will bite.
    private void DrawCuttingFlame(SpriteBatch spriteBatch, Vector2 origin, Vector2 aim, float totalSeconds) =>
        DrawCuttingFlame(spriteBatch, _pixel, origin, aim, totalSeconds);

    // internal + static so the interior renderers light the same torch: a cutter works anywhere,
    // there just isn't any ore inside a ship to bite on.
    internal static void DrawCuttingFlame(SpriteBatch spriteBatch, Texture2D pixel, Vector2 origin, Vector2 aim, float totalSeconds) =>
        DrawToolFlame(spriteBatch, pixel, origin, aim, totalSeconds, new Color(70, 130, 255), new Color(120, 200, 255));

    // The welder's flame: same shape and reach as the cutter's (World.Welding.cs uses the same
    // WelderReachUnits), but yellow-orange rather than blue - a torch welding metal, not cutting
    // rock, and a color a player can tell apart from across the room.
    internal static void DrawWeldingFlame(SpriteBatch spriteBatch, Texture2D pixel, Vector2 origin, Vector2 aim, float totalSeconds) =>
        DrawToolFlame(spriteBatch, pixel, origin, aim, totalSeconds, new Color(255, 120, 0), new Color(255, 200, 60));

    private static void DrawToolFlame(SpriteBatch spriteBatch, Texture2D pixel, Vector2 origin, Vector2 aim, float totalSeconds, Color outer, Color mid)
    {
        if (aim.LengthSquared() < 0.001f)
            return;
        aim.Normalize();

        var rotation = MathF.Atan2(aim.Y, aim.X);
        var flicker = 0.85f + 0.15f * MathF.Sin(totalSeconds * 40f);
        var length = 1.7f * ShipRenderer.PixelsPerUnit * flicker; // World.CutterReachUnits / World.WelderReachUnits

        spriteBatch.Draw(pixel, origin, null, outer * 0.35f, rotation, new Vector2(0f, 0.5f),
            new Vector2(length, 14f), SpriteEffects.None, 0f);
        spriteBatch.Draw(pixel, origin, null, mid * 0.75f, rotation, new Vector2(0f, 0.5f),
            new Vector2(length * 0.92f, 7f), SpriteEffects.None, 0f);
        spriteBatch.Draw(pixel, origin, null, Color.White * 0.9f, rotation, new Vector2(0f, 0.5f),
            new Vector2(length * 0.55f, 3f), SpriteEffects.None, 0f);

        // The contact point, at the far end of the beam - a bright flare with sparks kicked off
        // sideways, the way an actual torch throws slag, rather than the beam just ending in mid-air.
        DrawWeldSparkFlare(spriteBatch, pixel, origin + aim * (length * 0.55f), aim, totalSeconds);
    }

    // A small rotating starburst at the weld point plus a scatter of individual streaks flying
    // outward and away from the beam. Continuous rather than TransientEffect's one-shot
    // DrawSparkBurst, so it needs no stored particle state: each streak's own angle/reach/phase is a
    // pure function of totalSeconds and its own index, looping forever for as long as the tool is
    // held.
    private static void DrawWeldSparkFlare(SpriteBatch spriteBatch, Texture2D pixel, Vector2 tip, Vector2 aim, float totalSeconds)
    {
        var pulse = 0.8f + 0.2f * MathF.Sin(totalSeconds * 55f);

        // A hot white core plus a slowly-turning 4-point cross flare - the classic "muzzle flash
        // star" the reference screenshot's flash reads as, brighter and longer than the spark trails
        // below so it stays the single brightest thing on screen.
        spriteBatch.Draw(pixel, tip, null, Color.White * pulse, 0f, new Vector2(0.5f, 0.5f),
            new Vector2(7f, 7f) * pulse, SpriteEffects.None, 0f);
        var starLength = 16f * pulse;
        for (var arm = 0; arm < 4; arm++)
        {
            var angle = arm * MathF.PI / 4f + totalSeconds * 1.5f;
            spriteBatch.Draw(pixel, tip, null, Color.White * (0.7f * pulse), angle, new Vector2(0.5f, 0.5f),
                new Vector2(starLength, 2f), SpriteEffects.None, 0f);
        }

        // A wider scatter of individual spark trails - each a short streak with a fading tail flying
        // out on its own straight path, not a dot, closer to the reference's sparks shooting off in
        // several directions from the flash. Biased away from the beam (mostly sideways/backwards,
        // the way real slag actually kicks off a weld) but spread across a full half-circle rather
        // than just directly sideways. Continuous rather than TransientEffect's one-shot
        // DrawSparkBurst: each streak's own angle/reach/phase is a pure function of totalSeconds and
        // its own index, so no particle state needs to be stored.
        var side = new Vector2(-aim.Y, aim.X);
        const int sparkCount = 9;
        for (var i = 0; i < sparkCount; i++)
        {
            var spread = HashNoise(i) * 2f - 1f; // -1..1 across the whole arc behind the beam
            var direction = -aim * (0.3f + HashNoise(i + 41) * 0.5f) + side * spread;
            if (direction.LengthSquared() > 0.0001f)
                direction.Normalize();

            var cycle = (totalSeconds * (1.4f + i * 0.31f) + HashNoise(i + 17)) % 1f;
            var reach = 5f + cycle * 30f;
            var trailLength = 5f + cycle * 6f;
            var pos = tip + direction * reach;
            var alpha = 1f - cycle;
            var angle = MathF.Atan2(direction.Y, direction.X);
            var color = Color.Lerp(Color.White, Color.OrangeRed, cycle) * alpha;
            // Origin at the far (x=1) edge, not the near one: the streak trails behind `pos` along
            // where the spark came from, rather than ahead of it.
            spriteBatch.Draw(pixel, pos, null, color, angle, new Vector2(1f, 0.5f),
                new Vector2(trailLength, 1.6f), SpriteEffects.None, 0f);
        }
    }

    // Deterministic 0..1 noise, same trick TileTextures.Hash uses - no seed state, so spark #i always
    // gets the same lateral offset and phase across frames instead of jittering randomly.
    private static float HashNoise(int i)
    {
        var value = MathF.Sin(i * 12.9898f + 78.233f) * 43758.5453f;
        return value - MathF.Floor(value);
    }

    // A block of ore: a real body sitting on the rock, with the bar showing how much of it is left
    // to cut. It used to be a pulsing diamond with a number of "cuts" beside it - a marker, not a
    // thing - and there was nothing to aim a torch at.
    private void DrawOreBlock(SpriteBatch spriteBatch, OreDeposit deposit, OreDepositState state, Vector2 screenCenter, float totalSeconds)
    {
        var half = deposit.Radius * ShipRenderer.PixelsPerUnit;
        var body = new Rectangle((int)(screenCenter.X - half), (int)(screenCenter.Y - half), (int)(half * 2), (int)(half * 2));

        spriteBatch.Draw(_pixel, body, new Color(96, 78, 44));
        spriteBatch.Draw(_pixel, new Rectangle(body.X, body.Y, body.Width, 3), new Color(150, 126, 74));
        spriteBatch.Draw(_pixel, new Rectangle(body.X, body.Bottom - 3, body.Width, 3), Color.Black * 0.4f);

        // Veins of the actual mineral, glinting - what makes it read as ore rather than as a crate.
        var glint = 0.65f + 0.35f * MathF.Sin(totalSeconds * 2f + screenCenter.X * 0.05f);
        var inset = body.Width / 4;
        spriteBatch.Draw(_pixel, new Rectangle(body.X + inset, body.Y + inset, body.Width - inset * 2, body.Height / 6), Color.Goldenrod * glint);
        spriteBatch.Draw(_pixel, new Rectangle(body.Center.X - 2, body.Y + inset, 4, body.Height - inset * 2), Color.Goldenrod * (glint * 0.7f));

        // Progress bar: only once it has actually been worked, so an untouched field isn't a wall
        // of bars.
        if (state.Fraction >= 0.999f)
            return;

        var barWidth = body.Width + 10;
        var bar = new Rectangle(body.Center.X - barWidth / 2, body.Y - 12, barWidth, 5);
        spriteBatch.Draw(_pixel, bar, Color.Black * 0.7f);
        spriteBatch.Draw(_pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * state.Fraction), bar.Height), Color.Goldenrod);
    }

    // Freshly cut loose ore lying in the open (game_design.md Phase 3, M18's "lying in the world"
    // item) - a smaller, steadier glow than a live deposit so the two don't read as the same thing.
    private void DrawDroppedItem(SpriteBatch spriteBatch, DroppedItem dropped, Vector2 screenCenter, float totalSeconds)
    {
        var pulse = 0.8f + 0.2f * MathF.Sin(totalSeconds * 4f + screenCenter.X);
        DrawGlowDiamond(spriteBatch, screenCenter, 7, Color.LightGoldenrodYellow * pulse);
        spriteBatch.DrawString(_font, ItemDefinitions.ShortLabel(dropped.Item), screenCenter + new Vector2(6, -6), Color.LightGoldenrodYellow, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }

    // A 45-degree-rotated square reads as a crystal/gem facet - built from the same 1x1 pixel
    // texture as everything else (no image assets in this project), just rotated via SpriteBatch.
    private void DrawGlowDiamond(SpriteBatch spriteBatch, Vector2 center, float size, Color color)
    {
        spriteBatch.Draw(_pixel, center, null, color, MathF.PI / 4f, new Vector2(0.5f, 0.5f), new Vector2(size, size), SpriteEffects.None, 0f);
    }

    // The rock itself is one baked image (AsteroidTexture) rather than a pile of shaded quads: a
    // lit height field with craters, fissures, staining and a weathered crust, masked by exactly
    // the outline the physics uses. Quads could only ever manage "lighter here, darker there".
    //
    // The image is drawn rotated by the same amount the field is rotated on screen, so the sun
    // stays fixed in the world while everything swings around the (always upright) ship, and the
    // scale is read off the transform itself instead of assuming a zoom level.
    private void DrawAsteroid(SpriteBatch spriteBatch, Asteroid asteroid, Vector2 screenCenter, Func<Vec2, Vector2> worldToScreen)
    {
        var axis = worldToScreen(asteroid.Position + new Vec2(1f, 0f)) - screenCenter;
        var pixelsPerUnit = axis.Length();
        var rotation = MathF.Atan2(axis.Y, axis.X);

        // Radius keys the cache alongside the id: a field could hand the same id a different size,
        // and a rock wearing another rock's face would be worse than one with no face at all.
        var key = $"{asteroid.Id}:{asteroid.Radius}";
        if (!_asteroidSkins.TryGetValue(key, out var skin))
        {
            // The pixel math runs on a background task (see _pendingAsteroidBakes' own comment) -
            // started once per key and polled here every frame afterward. Still drawn flat
            // (DrawAsteroidFlat) for however many frames the bake takes, exactly like the old
            // one-per-frame throttle already did while "waiting its turn" - the only change is
            // that the wait no longer blocks the render thread while it happens.
            if (!_pendingAsteroidBakes.TryGetValue(key, out var pending))
            {
                pending = Task.Run(() => AsteroidTexture.BakePixels(asteroid));
                _pendingAsteroidBakes[key] = pending;
            }

            if (!pending.IsCompletedSuccessfully)
            {
                DrawAsteroidFlat(spriteBatch, asteroid, screenCenter, worldToScreen);
                return;
            }

            var (pixels, side, halfExtentUnits) = pending.Result;
            var texture = new Texture2D(_graphicsDevice, side, side);
            texture.SetData(pixels);
            skin = new AsteroidTexture.Skin(texture, halfExtentUnits);
            _asteroidSkins[key] = skin;
            _pendingAsteroidBakes.Remove(key);
        }

        var scale = skin.HalfExtentUnits * 2f * pixelsPerUnit / skin.Texture.Width;
        spriteBatch.Draw(skin.Texture, screenCenter, null, Color.White, rotation,
            new Vector2(skin.Texture.Width / 2f, skin.Texture.Height / 2f), scale, SpriteEffects.None, 0f);
    }

    // Stand-in for the one frame or two a rock spends waiting its turn to be baked: the right
    // outline in flat stone, so nothing pops into existence out of empty space.
    private void DrawAsteroidFlat(SpriteBatch spriteBatch, Asteroid asteroid, Vector2 screenCenter, Func<Vec2, Vector2> worldToScreen)
    {
        var outline = AsteroidShape.Outline(asteroid);
        for (var i = 0; i < outline.Length; i++)
        {
            var a = worldToScreen(outline[i]);
            var b = worldToScreen(outline[(i + 1) % outline.Length]);
            Primitives.FillTriangle(spriteBatch, _pixel, screenCenter, a, b, new Color(74, 68, 62));
        }
    }


    // A suited EVA character always shows the CadetBlue visor ring (ShipRenderer only shows it
    // when WearingSuit - out here everyone's necessarily suited, game_design.md Phase 3 M17).
    // facing arrives already folded into the ship's frame by the caller - see the call site.
    private void DrawCharacter(SpriteBatch spriteBatch, CharacterState character, Vector2 screenCenter, Vector2 facing)
    {
        // The same body as indoors (ShipRenderer.CharacterDiameter). It used to be a 10px dot, so a
        // crewman shrank to a third of their size the moment they stepped through the airlock -
        // inside and outside are one continuous space at one scale, and the person has to be too.
        // Read from ShipRenderer rather than repeated here, so inside and outside cannot drift.
        var size = (int)(ShipRenderer.CharacterDiameter * ShipRenderer.PixelsPerUnit);

        if (facing.LengthSquared() > 0.01f)
            facing.Normalize();
        else
            facing = new Vector2(1f, 0f);
        _crewSkin.Draw(spriteBatch, new Vector2(screenCenter.X, screenCenter.Y + size * 0.30f),
            ShipRenderer.CharacterHeight * ShipRenderer.PixelsPerUnit,
            new Color(196, 78, 44), new Color(226, 186, 70), true, facing);

        // Same held-item chip as indoors (ShipRenderer.DrawHeldItems) - a suited EVA crewmate holding
        // a cutter still reads as holding something, not just glowing from an invisible tool.
        ShipRenderer.DrawHeldItems(spriteBatch, _pixel, _font, ShipRenderer.HeldItemTypes(character.Inventory), screenCenter, facing);

        // Same always-on nameplate as indoors (ShipRenderer.DrawCharacter) - a suited EVA crewmate
        // is still someone specific, not just an anonymous orange square drifting past.
        if (!character.IsBot && character.Nickname is { Length: > 0 } nickname)
            spriteBatch.DrawString(_font, nickname, new Vector2(screenCenter.X - 10, screenCenter.Y - size / 2f - 14),
                Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }
}
