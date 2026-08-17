using System;
using System.Collections.Generic;
using System.Linq;
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

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private readonly GraphicsDevice _graphicsDevice;
    // One baked surface per rock (AsteroidTexture), kept for the life of the client - the same five
    // ids come back every time a field is entered, so this is built once and never again.
    private readonly Dictionary<string, AsteroidTexture.Skin> _asteroidSkins = new();
    private bool _bakedAsteroidThisFrame;

    public FieldRenderer(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _graphicsDevice = graphicsDevice;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
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
            return origin + new Vector2(local.X, local.Y) * ShipRenderer.PixelsPerUnit;
        }

        // At most one rock is baked per frame: five at once is a visible hitch on the frame the
        // field opens, five spread over five frames is nothing, and the ones still waiting are
        // drawn flat in their correct outline meanwhile.
        _bakedAsteroidThisFrame = false;
        foreach (var asteroid in snapshot.Asteroids)
            DrawAsteroid(spriteBatch, asteroid, WorldToScreen(asteroid.Position), WorldToScreen);

        foreach (var deposit in snapshot.OreDeposits)
        {
            var state = snapshot.OreDepositStates.FirstOrDefault(s => s.DepositId == deposit.Id);
            if (state is not null && state.Hp > 0f)
                DrawOreBlock(spriteBatch, deposit, state, WorldToScreen(deposit.Position), totalSeconds);
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
            DrawCuttingFlame(spriteBatch, WorldToScreen(new Vec2(character.X, character.Y)),
                new Vector2(aim.X, aim.Y), totalSeconds);
        }

        foreach (var character in snapshot.Characters.Where(c => c.Welding && c.IsOutside))
        {
            var aim = ShipLocalFrame.ToLocalDirection(
                new Vec2(character.FacingX, character.FacingY), snapshot.ShipField.RotationDegrees);
            DrawWeldingFlame(spriteBatch, _pixel, WorldToScreen(new Vec2(character.X, character.Y)),
                new Vector2(aim.X, aim.Y), totalSeconds);
        }

        DrawEngines(spriteBatch, snapshot, origin, hullCenter, totalSeconds);

        // Only where a station is actually part of the sector - in a hostile sector or an asteroid
        // belt there is none, and the layout the World keeps around for docking is not a thing in
        // the sky (VoyagePhases.HasStationInField).
        if (snapshot.Voyage.Phase.HasStationInField())
        {
            var stationScreen = WorldToScreen(snapshot.StationPosition);
            var portScreen = WorldToScreen(snapshot.StationDockingPortPosition);
            // Once docked the interior is drawn in full by StationRenderer, in these same
            // coordinates - the exterior would land exactly on top of it, so it's skipped rather
            // than double-drawn.
            if (snapshot.Voyage.Phase != VoyagePhase.Station || seenFromOutside)
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
        foreach (var enemy in snapshot.EnemyShips)
        {
            var enemyScreen = WorldToScreen(new Vec2(enemy.X, enemy.Y));
            DrawEnemyShipExterior(spriteBatch, enemyScreen, enemy,
                enemy.IsBoardable ? snapshot.EnemyCrew.Count(c => c.Alive) : -1,
                rotation + (enemy.RotationDegrees * MathF.PI / 180f));
            DrawOffScreenMarker(spriteBatch, enemyScreen, viewportOrigin, viewportSize,
                enemy.IsBoardable ? "Враг" : "Рейдер", Color.OrangeRed);
        }

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
                new Vector2(facing.X, facing.Y));
        }

        if (effects is not null)
            foreach (var effect in effects.Where(e => e.Kind == EffectKind.Cut))
                DrawSparkBurst(spriteBatch, WorldToScreen(effect.Position), effect.Progress);
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
        var underWay = snapshot.Voyage.Phase is VoyagePhase.AsteroidField or VoyagePhase.Battle or VoyagePhase.StationApproach;
        var thrust = underWay
            ? Math.Clamp(new Vector2(snapshot.ShipField.ThrustX, snapshot.ShipField.ThrustY).Length(), 0f, 1f)
            : 0f;
        var halfExtents = ShipLocalFrame.GetHullHalfExtents(snapshot.Rooms);

        foreach (var device in snapshot.SystemDevices)
        {
            if (device.System != PowerSystemId.Engine)
                continue;

            // Which face of the hull this engine is closest to, and therefore which way it fires.
            // Raw axis comparison rather than one normalised by the hull's proportions: on a long
            // thin hull an engine one unit off the centreline still belongs on the end plating.
            var toDevice = device.Position - hullCenter;
            var outward = MathF.Abs(toDevice.X) >= MathF.Abs(toDevice.Y)
                ? new Vector2(MathF.Sign(toDevice.X) is var sx && sx == 0 ? 1f : sx, 0f)
                : new Vector2(0f, MathF.Sign(toDevice.Y));

            var baseLocal = outward.X != 0f
                ? new Vector2(hullCenter.X + outward.X * halfExtents.X, device.Y)
                : new Vector2(device.X, hullCenter.Y + outward.Y * halfExtents.Y);
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

        if (thrust <= 0.05f)
            return;

        // The flame flickers rather than sitting still - a static cone reads as a painted-on decal,
        // and the flicker is what makes "the engines are running" legible at a glance. Each nacelle
        // gets its own phase off its id, so a pair of them doesn't pulse in lockstep.
        var phase = totalSeconds * 22f + deviceId.Length * 1.7f;
        var flicker = 0.75f + 0.25f * MathF.Sin(phase);
        var mouth = baseScreen + outward * (housingLength * 0.75f);
        var flameLength = (14f + thrust * 46f) * flicker * sizeScale;
        var flameWidth = housingWidth * 0.7f;

        spriteBatch.Draw(_pixel, mouth, null, Color.OrangeRed * (0.35f + thrust * 0.35f), rotation, new Vector2(0f, 0.5f),
            new Vector2(flameLength, flameWidth), SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, mouth, null, Color.Orange * (0.6f + thrust * 0.4f), rotation, new Vector2(0f, 0.5f),
            new Vector2(flameLength * 0.6f, flameWidth * 0.6f), SpriteEffects.None, 0f);
        spriteBatch.Draw(_pixel, mouth, null, Color.LightYellow * (0.7f + thrust * 0.3f), rotation, new Vector2(0f, 0.5f),
            new Vector2(flameLength * 0.3f, flameWidth * 0.32f), SpriteEffects.None, 0f);
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
        foreach (var room in snapshot.StationRooms)
        {
            var center = room.Center + snapshot.StationWorldOffset;
            var size = new Vector2(room.Width, room.Height) * ShipRenderer.PixelsPerUnit;
            var screen = worldToScreen(center);
            spriteBatch.Draw(_pixel, screen, null, new Color(52, 60, 74), rotation, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
            DrawRotatedOutline(spriteBatch, screen, size, rotation, new Color(96, 108, 126));
        }

        spriteBatch.DrawString(_font, "Станция", screenCenter + new Vector2(-24, -90), Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
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
    private void DrawEnemyShipExterior(SpriteBatch spriteBatch, Vector2 screenCenter,
        EnemyShipFieldState enemy, int crewAlive, float rotation)
    {
        const float enemyVisualRadius = 3.5f; // matches World.EnemyHullRadius - what a shell has to hit
        var sizePx = enemyVisualRadius * 2 * ShipRenderer.PixelsPerUnit;
        var hullColor = enemy.IsRetreating ? new Color(60, 50, 40) : new Color(80, 40, 45);
        spriteBatch.Draw(_pixel, screenCenter, null, hullColor * 0.95f, rotation, new Vector2(0.5f, 0.5f), new Vector2(sizePx, sizePx * 0.55f), SpriteEffects.None, 0f);
        // Nose block, so which way it's pointing (and therefore shooting) is readable at a glance.
        var nose = screenCenter + new Vector2(MathF.Cos(rotation), MathF.Sin(rotation)) * (sizePx * 0.42f);
        spriteBatch.Draw(_pixel, nose, null, Color.OrangeRed * 0.9f, rotation, new Vector2(0.5f, 0.5f), new Vector2(sizePx * 0.22f, sizePx * 0.3f), SpriteEffects.None, 0f);

        DrawEnemyHealthBar(spriteBatch, screenCenter, sizePx, enemy);

        if (crewAlive >= 0)
        {
            DrawGlowDiamond(spriteBatch, screenCenter - new Vector2(sizePx / 2, 0), 16, Color.LimeGreen * 0.9f); // the breach to climb through
            var label = crewAlive > 0 ? $"Враг (экипаж: {crewAlive})" : "Враг (зачищен)";
            spriteBatch.DrawString(_font, label, screenCenter + new Vector2(-40, -sizePx / 2 - 16), Color.OrangeRed, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        }
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
            if (_bakedAsteroidThisFrame)
            {
                DrawAsteroidFlat(spriteBatch, asteroid, screenCenter, worldToScreen);
                return;
            }
            skin = AsteroidTexture.Bake(_graphicsDevice, asteroid);
            _asteroidSkins[key] = skin;
            _bakedAsteroidThisFrame = true;
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
        var size = (int)(0.7f * ShipRenderer.PixelsPerUnit);
        var rect = new Rectangle((int)screenCenter.X - size / 2, (int)screenCenter.Y - size / 2, size, size);
        spriteBatch.Draw(_pixel, rect, Color.OrangeRed * 0.9f);

        var visorSize = Math.Max(4, size / 2);
        spriteBatch.Draw(_pixel, new Rectangle((int)screenCenter.X - visorSize / 2, (int)screenCenter.Y - visorSize / 2, visorSize, visorSize), Color.CadetBlue);

        if (facing.LengthSquared() > 0.01f)
        {
            facing.Normalize();
            const int notchSize = 4;
            var notchCenter = screenCenter + facing * (size / 2f + 1);
            spriteBatch.Draw(_pixel, new Rectangle((int)notchCenter.X - notchSize / 2, (int)notchCenter.Y - notchSize / 2, notchSize, notchSize), Color.White);
        }
    }
}
