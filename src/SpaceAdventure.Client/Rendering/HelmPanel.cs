using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Shown while the pilot is manning the helm (game_design.md Phase 3, M15): a Barotrauma-style
// joystick — drag the handle away from the center to set the ship's thrust vector, direction and
// distance both matter (distance = how hard the engines push). The handle stays wherever it was
// last dragged to even after releasing the mouse (Game1 owns that persisted vector, not this
// panel) — matches the ship still flying on the same heading after standing up. A radar to the
// right shows nearby asteroids relative to the ship's current position and heading.
public sealed class HelmPanel
{
    public static readonly Vector2 StickCenterOffset = new(90, 110);
    public const float StickRadius = 70f;
    private static readonly Vector2 RadarCenterOffset = new(370, 180);
    private const float RadarRadiusPixels = 150f;
    private const float RadarRangeUnits = 50f; // world units from the ship's center to the radar's edge
    private static readonly Rectangle StabilizeButtonRectLocal = new(20, 210, 140, 34);
    // Only drawn/clickable while the ship is actually alongside the station's berth
    // (WorldSnapshot.CanDock) - it's the deliberate press that docks, not proximity alone.
    private static readonly Rectangle DockButtonRectLocal = new(20, 252, 140, 34);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public HelmPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetStabilizeButtonRect(Vector2 panelOrigin) =>
        new((int)panelOrigin.X + StabilizeButtonRectLocal.X, (int)panelOrigin.Y + StabilizeButtonRectLocal.Y,
            StabilizeButtonRectLocal.Width, StabilizeButtonRectLocal.Height);

    public static Rectangle GetDockButtonRect(Vector2 panelOrigin) =>
        new((int)panelOrigin.X + DockButtonRectLocal.X, (int)panelOrigin.Y + DockButtonRectLocal.Y,
            DockButtonRectLocal.Width, DockButtonRectLocal.Height);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin)
    {
        spriteBatch.DrawString(_font, "[W] ход  [X] назад  [A/D] поворот  [S] стабилизация  [Z] режим руления",
            panelOrigin + new Vector2(0, -24), Color.Yellow, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        // Which of Arc/Rcs is currently flying the ship (World.ShipField.cs, M41) - drawn right
        // next to the dial its own turning behavior actually depends on.
        var modeLabel = snapshot.ShipField.ControlMode == ShipControlMode.Arc ? "ВИРАЖ" : "РСУ";
        var modeColor = snapshot.ShipField.ControlMode == ShipControlMode.Arc ? Color.SkyBlue : Color.Orange;
        spriteBatch.DrawString(_font, modeLabel, panelOrigin + StickCenterOffset + new Vector2(-18, StickRadius + 10),
            modeColor, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        DrawAttitudeDial(spriteBatch, snapshot.ShipField, panelOrigin);
        DrawStabilizeButton(spriteBatch, snapshot.ShipField, panelOrigin);
        DrawDockButton(spriteBatch, snapshot, panelOrigin);
        DrawRadar(spriteBatch, snapshot, panelOrigin);
    }

    // The same slot, both directions (World.StationDocking.cs's HandleDockButtonPressed - one
    // button either way): while approaching it either arms once alongside the berth or shows the
    // remaining distance, and once actually docked it offers to cast off instead - no separate
    // control to hunt for just to leave.
    private void DrawDockButton(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin)
    {
        var rect = GetDockButtonRect(panelOrigin);

        if (snapshot.Voyage.DockedPointId is not null)
        {
            spriteBatch.Draw(_pixel, rect, Color.SeaGreen);
            spriteBatch.DrawString(_font, "[Клик] ОТСТЫКОВАТЬСЯ", new Vector2(rect.X + 6, rect.Y + 9), Color.White,
                0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return;
        }

        if (!snapshot.Voyage.HasNearbyStation)
            return;

        if (snapshot.CanDock)
        {
            spriteBatch.Draw(_pixel, rect, Color.SeaGreen);
            spriteBatch.DrawString(_font, "[Клик] СТЫКОВКА", new Vector2(rect.X + 6, rect.Y + 9), Color.White,
                0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
            return;
        }

        // Distance to the berth - the spot the hull itself has to sit on - not to the airlock
        // rectangle, which the hull is a good half-length short of when the two mate.
        var toBerth = new Vector2(
            snapshot.DockBerthPosition.X - snapshot.ShipField.X,
            snapshot.DockBerthPosition.Y - snapshot.ShipField.Y);
        spriteBatch.Draw(_pixel, rect, new Color(50, 50, 50));
        spriteBatch.DrawString(_font, $"До причала: {toBerth.Length():0}", new Vector2(rect.X + 6, rect.Y + 9),
            Color.Gray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    // Where the bow is pointing and how hard the engines are pushing. It replaces the draggable
    // joystick: with W/A/D/X the pilot's hands are on the keys, so this dial's job is to report,
    // not to be grabbed. Heading is drawn in world terms (up the dial is world -Y), because that's
    // the frame the radar beside it plots contacts in.
    private void DrawAttitudeDial(SpriteBatch spriteBatch, ShipFieldState shipField, Vector2 panelOrigin)
    {
        var center = panelOrigin + StickCenterOffset;
        DrawCircleOutline(spriteBatch, center, StickRadius, Color.SlateGray);

        var thrust = new Vector2(shipField.ThrustX, shipField.ThrustY);
        var heading = thrust.LengthSquared() > 0.0001f
            ? Vector2.Normalize(thrust)
            : new Vector2(MathF.Cos(shipField.RotationDegrees * MathF.PI / 180f), MathF.Sin(shipField.RotationDegrees * MathF.PI / 180f));

        // Bow line out to the rim, plus a nose block, so heading reads at a glance.
        var rotation = MathF.Atan2(heading.Y, heading.X);
        spriteBatch.Draw(_pixel, center, null, Color.SlateGray * 0.8f, rotation, new Vector2(0f, 0.5f),
            new Vector2(StickRadius, 2f), SpriteEffects.None, 0f);
        var nose = center + heading * StickRadius;
        spriteBatch.Draw(_pixel, new Rectangle((int)nose.X - 5, (int)nose.Y - 5, 10, 10), Color.White);

        // Throttle: how far along the bow line the engines are actually pushing, astern shown
        // behind the hub in a colder colour.
        var push = thrust.Length();
        if (push > 0.01f)
        {
            var ahead = Vector2.Dot(thrust, heading) >= 0f;
            spriteBatch.Draw(_pixel, center, null, ahead ? Color.Gold : Color.MediumTurquoise,
                MathF.Atan2(thrust.Y, thrust.X), new Vector2(0f, 0.5f),
                new Vector2(StickRadius * Math.Min(1f, push), 8f), SpriteEffects.None, 0f);
        }

        spriteBatch.Draw(_pixel, new Rectangle((int)center.X - 5, (int)center.Y - 5, 10, 10),
            shipField.AutoStabilize ? Color.SteelBlue : Color.Gray);
    }

    private void DrawStabilizeButton(SpriteBatch spriteBatch, ShipFieldState shipField, Vector2 panelOrigin)
    {
        var rect = GetStabilizeButtonRect(panelOrigin);
        spriteBatch.Draw(_pixel, rect, shipField.AutoStabilize ? Color.SteelBlue : new Color(60, 60, 60));
        spriteBatch.DrawString(_font, "Стабилизация", new Vector2(rect.X + 6, rect.Y + 9), Color.White,
            0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    // Radar-style local view: ship always drawn at the center pointing along its current heading,
    // asteroids plotted relative to it and scaled down - matches how a pilot would actually read
    // "what's near me and which way is it", rather than an absolute-position minimap.
    private void DrawRadar(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin)
    {
        var center = panelOrigin + RadarCenterOffset;
        DrawCircleOutline(spriteBatch, center, RadarRadiusPixels, Color.SlateGray);

        var shipField = snapshot.ShipField;
        var shipWorldPos = new Vector2(shipField.X, shipField.Y);
        var scale = RadarRadiusPixels / RadarRangeUnits;

        // Rocks are plotted as their real outline (AsteroidShape), the same polygon the hull will
        // collide with - a square blip says "something is there", an outline says whether the gap
        // the pilot is aiming for is actually a gap.
        foreach (var asteroid in snapshot.Field.Asteroids)
        {
            var offset = new Vector2(asteroid.X, asteroid.Y) - shipWorldPos;
            if (offset.Length() * scale > RadarRadiusPixels)
                continue;

            var outline = AsteroidShape.Outline(asteroid);
            var points = new Vector2[outline.Length];
            for (var i = 0; i < outline.Length; i++)
                points[i] = center + ToRadar(outline[i], shipWorldPos, scale);

            var dotCenter = center + offset * scale;
            Primitives.FillPolygon(spriteBatch, _pixel, dotCenter, points, new Color(96, 74, 56));
            Primitives.StrokePolygon(spriteBatch, _pixel, points, new Color(150, 120, 92));
        }

        // The station and its berth, plus the enemy during a fight. Unlike the asteroids these are
        // clamped to the radar's rim rather than hidden when out of range, so the pilot always has
        // a bearing to steer by - losing track of where the station is would make the whole manual
        // approach guesswork.
        if (snapshot.Voyage.HasNearbyStation)
        {
            // The station is plotted as its real compartments, so what the radar shows is the same
            // shape the pilot will be walking around a minute later - and the berth's position on
            // it is obvious rather than something to be taken on trust from a lone dot.
            foreach (var room in snapshot.Station.Rooms)
            {
                var roomCenter = center + ToRadar(room.Center + snapshot.Station.WorldOffset, shipWorldPos, scale);
                var size = new Vector2(room.Width, room.Height) * scale;
                if ((roomCenter - center).Length() > RadarRadiusPixels + size.Length())
                    continue;
                spriteBatch.Draw(_pixel, roomCenter, null, Color.SteelBlue * 0.75f, 0f, new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
            }
            DrawTrackedBlip(spriteBatch, center, ToRadar(snapshot.Station.Position, shipWorldPos, scale), 6, Color.SteelBlue);
            DrawTrackedBlip(spriteBatch, center, ToRadar(snapshot.DockBerthPosition, shipWorldPos, scale), 7, Color.LimeGreen);
        }
        // Every hostile hull in the sector, not just the one being boarded - the captain flies the
        // ship by this display, and a raider that isn't on it is a raider they can't avoid. Clamped
        // to the rim like the station, so a contact off the edge of the plot still gives a bearing.
        foreach (var enemy in snapshot.EnemyShip.Ships)
            DrawTrackedBlip(spriteBatch, center, ToRadar(new Vec2(enemy.X, enemy.Y), shipWorldPos, scale), 8,
                enemy.IsRetreating ? Color.Goldenrod : Color.OrangeRed);

        // Shells in flight, so incoming fire is visible on the plot before it lands.
        foreach (var shot in snapshot.Projectiles)
        {
            var blip = center + ToRadar(new Vec2(shot.X, shot.Y), shipWorldPos, scale);
            if ((blip - center).Length() > RadarRadiusPixels)
                continue; // unlike the ships, a shell off the plot isn't worth a rim marker
            spriteBatch.Draw(_pixel, new Rectangle((int)blip.X - 1, (int)blip.Y - 1, 3, 3),
                shot.FromEnemy ? Color.Red : Color.Gold);
        }

        DrawOwnShipSchematic(spriteBatch, snapshot, center, scale);
    }

    // The player's own hull, compartment by compartment, sitting at the middle of the plot and
    // turned to its real heading. An arrow would say where the nose points and nothing else; a
    // schematic says which end of the ship a raider is closing on, which is the thing the pilot is
    // actually steering by. Same treatment the station already gets on this display.
    private void DrawOwnShipSchematic(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 center, float scale)
    {
        var radians = snapshot.ShipField.RotationDegrees * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);

        foreach (var room in snapshot.Rooms)
        {
            var local = room.Center - hullCenter;
            var rotated = new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
            var size = new Vector2(room.Width, room.Height) * scale;
            var breached = snapshot.WallBlockStates.Any(s =>
                s.Breached && snapshot.WallBlocks.FirstOrDefault(b => b.Id == s.Id)?.RoomId == room.Id);

            spriteBatch.Draw(_pixel, center + rotated * scale, null,
                (breached ? Color.IndianRed : Color.LightSteelBlue) * 0.85f, radians,
                new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
        }

        // Nose marker, so which way it's facing survives even when the hull is a symmetric blob.
        var nose = center + new Vector2(cos, sin) * (RadarRadiusPixels * 0.18f);
        spriteBatch.Draw(_pixel, new Rectangle((int)nose.X - 3, (int)nose.Y - 3, 6, 6), Color.White);
    }

    private static Vector2 ToRadar(Vec2 worldPosition, Vector2 shipWorldPos, float scale) =>
        (new Vector2(worldPosition.X, worldPosition.Y) - shipWorldPos) * scale;

    // A contact that's always shown: inside the radar's range it sits where it really is, beyond
    // it gets pinned to the rim as a bearing (with a small tick mark, so "on the edge" reads as
    // "further than this" rather than "exactly here").
    private void DrawTrackedBlip(SpriteBatch spriteBatch, Vector2 radarCenter, Vector2 offset, float size, Color color)
    {
        var distance = offset.Length();
        var beyondRange = distance > RadarRadiusPixels;
        var clamped = beyondRange && distance > 0.001f ? offset / distance * RadarRadiusPixels : offset;
        var position = radarCenter + clamped;

        spriteBatch.Draw(_pixel,
            new Rectangle((int)(position.X - size / 2), (int)(position.Y - size / 2), (int)size, (int)size),
            color * (beyondRange ? 0.65f : 1f));

        if (beyondRange)
        {
            // Outward tick: reads as an arrow pointing off-radar.
            var rotation = MathF.Atan2(clamped.Y, clamped.X);
            spriteBatch.Draw(_pixel, position, null, color * 0.8f, rotation, new Vector2(0f, 0.5f),
                new Vector2(7f, 2f), SpriteEffects.None, 0f);
        }
    }

    // Approximates a circle with short line segments — there's no primitive circle draw in the
    // SpriteBatch pipeline this project uses elsewhere (see WiringPanel's line-based rendering).
    private void DrawCircleOutline(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
    {
        const int segments = 24;
        var previous = center + new Vector2(radius, 0);
        for (var i = 1; i <= segments; i++)
        {
            var angle = i * (2 * MathF.PI / segments);
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            var delta = point - previous;
            var length = delta.Length();
            if (length > 0.01f)
            {
                var rotation = MathF.Atan2(delta.Y, delta.X);
                spriteBatch.Draw(_pixel, previous, null, color, rotation, Vector2.Zero, new Vector2(length, 1.5f), SpriteEffects.None, 0f);
            }
            previous = point;
        }
    }
}
