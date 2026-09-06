using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The GALACTIC map (game_design.md - two-tier map, "При заходе на клавишу M открывается
// галактическая карта с кучей систем"): every known star system as a node, with a circle of
// GalaxyMap.WarpJumpRadius drawn around the current one - any system inside it is a valid jump
// target, no hand-authored edge list - so unlike GalaxyMapPanel (the current system's own detail,
// opened at the console) this one is reachable from anywhere via the M key, and shows the shape of
// the wider galaxy instead of one system's points of interest. Clicking a system inside the circle
// attempts a jump the same way GalaxyMapPanel's old inter-system list did - actually landing it
// still requires being out past the current system's own warp zone (World.StarSystems.cs's
// CanWarpNow, drawn as a ring on GalaxyMapPanel), so looking at this map is always safe, whether or
// not a jump could actually happen right now.
public sealed class GalacticMapPanel
{
    // Base scale from StarSystem.GalaxyX/Y (hand-placed layout units) to screen pixels before the
    // player's own zoom is applied - same right-drag-to-pan/scroll-to-zoom camera GalaxyMapPanel
    // already has (Game1.cs's _galacticMapZoom/_galacticMapPanOffset), so a galaxy that outgrows
    // the screen at a glance is still fully reachable rather than permanently cut off.
    private const float PixelsPerUnit = 1.4f;
    private const int NodeRadius = 14;
    // At 200 systems, drawing every single label unconditionally (the old behavior, fine for a
    // handful of nodes) turns the map into unreadable overlapping text. The current system and
    // whatever's reachable from it are always worth naming - those are the only nodes with any
    // clickable meaning right now - everything else only gets a label once zoomed in enough to
    // actually read it without the label crowding its neighbours off the map.
    private const float LabelZoomThreshold = 1.5f;

    // Purely decorative backdrop (PULSAR: Lost Colony's own galaxy screen sits its sector nodes
    // over a dense scattered starfield, not a bare black background) - has nothing to do with any
    // real StarSystem, generated once with a fixed seed so it never shimmers or changes between
    // opening the map. Panned/zoomed by the exact same camera as the real nodes, so it reads as
    // part of the same space rather than a flat sticker behind it.
    private const int BackgroundStarCount = 500;
    private const float BackgroundFieldExtent = 4200f;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private readonly Vector2[] _backgroundStars;
    private readonly float[] _backgroundStarBrightness;

    public GalacticMapPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;

        var random = new Random(424242);
        _backgroundStars = new Vector2[BackgroundStarCount];
        _backgroundStarBrightness = new float[BackgroundStarCount];
        for (var i = 0; i < BackgroundStarCount; i++)
        {
            _backgroundStars[i] = new Vector2(
                (float)(random.NextDouble() * 2 - 1) * BackgroundFieldExtent,
                (float)(random.NextDouble() * 2 - 1) * BackgroundFieldExtent);
            _backgroundStarBrightness[i] = 0.12f + (float)random.NextDouble() * 0.38f;
        }
    }

    // Auto-fits every system's own bounding box to start right at panelOrigin (before the player's
    // zoom/pan camera is applied on top) - the same "everything visible by default" convention
    // GalaxyMapPanel.ComputeMapOrigin already uses, so the whole graph starts on-screen instead of
    // partly cut off, however far the layout eventually grows.
    public static Vector2 ComputeOrigin(Vector2 panelOrigin, IReadOnlyList<StarSystemSummary> systems, float zoom, Vector2 panOffset)
    {
        if (systems.Count == 0)
            return panelOrigin + panOffset;

        var minX = systems.Min(s => s.GalaxyX);
        var minY = systems.Min(s => s.GalaxyY);
        return panelOrigin + panOffset - new Vector2(minX, minY) * PixelsPerUnit * zoom;
    }

    public static Vector2 NodeCenter(StarSystemSummary system, Vector2 mapOrigin, float zoom) =>
        mapOrigin + new Vector2(system.GalaxyX, system.GalaxyY) * PixelsPerUnit * zoom;

    public static Rectangle GetNodeRect(StarSystemSummary system, Vector2 mapOrigin, float zoom)
    {
        var center = NodeCenter(system, mapOrigin, zoom);
        return new Rectangle((int)center.X - NodeRadius, (int)center.Y - NodeRadius, NodeRadius * 2, NodeRadius * 2);
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, float zoom, Vector2 panOffset)
    {
        spriteBatch.DrawString(_font, "Карта галактики - клик по системе в круге прыжка (только у границы своей системы), ПКМ тащить (сдвиг), колесо (масштаб), M/Esc - закрыть",
            panelOrigin + new Vector2(0, -24), Color.Yellow, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        var mapOrigin = ComputeOrigin(panelOrigin, snapshot.StarSystems, zoom, panOffset);

        for (var i = 0; i < _backgroundStars.Length; i++)
        {
            var screenPos = mapOrigin + _backgroundStars[i] * PixelsPerUnit * zoom;
            spriteBatch.Draw(_pixel, new Rectangle((int)screenPos.X, (int)screenPos.Y, 1, 1), Color.White * _backgroundStarBrightness[i]);
        }

        var current = snapshot.StarSystems.FirstOrDefault(s => s.Id == snapshot.CurrentSystemId);
        var reachableFromCurrent = current is null
            ? new HashSet<string>()
            : snapshot.StarSystems
                .Where(s => s.Id != current.Id && Distance(s, current) <= GalaxyMap.WarpJumpRadius)
                .Select(s => s.Id)
                .ToHashSet();

        // The warp circle itself, drawn first so every node's own marker draws on top of it.
        if (current is not null)
            DrawRingOutline(spriteBatch, NodeCenter(current, mapOrigin, zoom), GalaxyMap.WarpJumpRadius * PixelsPerUnit * zoom, Color.CadetBlue * 0.6f);

        // Reused across every label drawn below so two nodes sitting close together on screen (a
        // real thing now that the current system can have several reachable neighbours at once)
        // stack their names instead of printing on top of each other into unreadable mush.
        var drawnLabelRects = new List<Rectangle>();

        foreach (var system in snapshot.StarSystems)
        {
            var isCurrent = system.Id == snapshot.CurrentSystemId;
            var isReachable = reachableFromCurrent.Contains(system.Id);
            var center = NodeCenter(system, mapOrigin, zoom);
            var color = isCurrent ? Color.LimeGreen : isReachable ? Color.Gold : Color.SteelBlue;
            HudIcons.FillCircle(spriteBatch, _pixel, center, NodeRadius, color);
            if (isCurrent)
                DrawRingOutline(spriteBatch, center, NodeRadius + 4, Color.White);

            if (!isCurrent && !isReachable && zoom < LabelZoomThreshold)
                continue; // a distant system's name only earns screen space once zoomed in on it

            var label = isCurrent ? $"{system.Name} (здесь)"
                : isReachable ? (snapshot.CanWarpNow ? $"[Клик] Прыжок: {system.Name}" : $"{system.Name} (долетите до границы своей системы)")
                : system.Name;
            var labelColor = isCurrent ? Color.LimeGreen : isReachable ? (snapshot.CanWarpNow ? Color.Yellow : Color.Gray) : Color.LightGray;
            var textSize = _font.MeasureString(label) * 0.5f;
            var labelPos = new Vector2(center.X - textSize.X / 2f, center.Y + NodeRadius + 6);
            var labelRect = new Rectangle((int)labelPos.X, (int)labelPos.Y, (int)textSize.X, (int)textSize.Y);

            // Push straight down, one line at a time, until this label clears every one already
            // placed this frame - a handful of tries is plenty since only nodes crowded within a
            // couple of NodeRadius of each other ever collide in the first place.
            for (var attempt = 0; attempt < 6 && drawnLabelRects.Any(r => r.Intersects(labelRect)); attempt++)
            {
                labelRect.Y += labelRect.Height + 2;
                labelPos.Y += textSize.Y + 2;
            }
            drawnLabelRects.Add(labelRect);

            spriteBatch.DrawString(_font, label, labelPos, labelColor, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }
    }

    private static float Distance(StarSystemSummary a, StarSystemSummary b) =>
        MathF.Sqrt(MathF.Pow(a.GalaxyX - b.GalaxyX, 2) + MathF.Pow(a.GalaxyY - b.GalaxyY, 2));

    private void DrawRingOutline(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
    {
        const int segments = 24;
        for (var i = 0; i < segments; i++)
        {
            var a0 = i * 2f * MathHelper.Pi / segments;
            var a1 = (i + 1) * 2f * MathHelper.Pi / segments;
            var p0 = center + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * radius;
            var p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            HudIcons.DrawLine(spriteBatch, _pixel, p0, p1, color, 1.5f);
        }
    }
}
