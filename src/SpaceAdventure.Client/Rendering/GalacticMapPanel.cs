using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// The GALACTIC map (game_design.md - two-tier map, "При заходе на клавишу M открывается
// галактическая карта с кучей систем"): every known star system as a node, joined by the limited,
// non-crossing corridor graph (GalaxyMap.Corridors) rather than a full one - so unlike
// GalaxyMapPanel (the current system's own detail, opened at the console) this one is reachable
// from anywhere via the M key, and shows the shape of the wider galaxy instead of one system's
// points of interest. Clicking a system connected to the current one attempts a jump the same way
// GalaxyMapPanel's old inter-system list did - actually landing it still requires being parked at
// the current system's own WarpPoint (World.StarSystems.cs's CanWarpNow), so looking at this map
// is always safe, whether or not a jump could actually happen right now.
public sealed class GalacticMapPanel
{
    // Base scale from StarSystem.GalaxyX/Y (hand-placed layout units) to screen pixels before the
    // player's own zoom is applied - same right-drag-to-pan/scroll-to-zoom camera GalaxyMapPanel
    // already has (Game1.cs's _galacticMapZoom/_galacticMapPanOffset), so a galaxy that outgrows
    // the screen at a glance is still fully reachable rather than permanently cut off.
    private const float PixelsPerUnit = 1.4f;
    private const int NodeRadius = 14;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public GalacticMapPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
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
        spriteBatch.DrawString(_font, "Карта галактики - клик по соседней системе (прыжок, только у границы своей системы), ПКМ тащить (сдвиг), колесо (масштаб), M/Esc - закрыть",
            panelOrigin + new Vector2(0, -24), Color.Yellow, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        var mapOrigin = ComputeOrigin(panelOrigin, snapshot.StarSystems, zoom, panOffset);

        var connectedToCurrent = snapshot.GalaxyCorridors
            .Where(c => c.SystemAId == snapshot.CurrentSystemId || c.SystemBId == snapshot.CurrentSystemId)
            .Select(c => c.SystemAId == snapshot.CurrentSystemId ? c.SystemBId : c.SystemAId)
            .ToHashSet();

        // Corridors first, so every node's own marker draws on top of the lines meeting it.
        foreach (var corridor in snapshot.GalaxyCorridors)
        {
            var a = snapshot.StarSystems.FirstOrDefault(s => s.Id == corridor.SystemAId);
            var b = snapshot.StarSystems.FirstOrDefault(s => s.Id == corridor.SystemBId);
            if (a is null || b is null)
                continue;
            HudIcons.DrawLine(spriteBatch, _pixel, NodeCenter(a, mapOrigin, zoom), NodeCenter(b, mapOrigin, zoom), Color.CadetBlue * 0.6f, 2.5f);
        }

        foreach (var system in snapshot.StarSystems)
        {
            var isCurrent = system.Id == snapshot.CurrentSystemId;
            var isReachable = connectedToCurrent.Contains(system.Id);
            var center = NodeCenter(system, mapOrigin, zoom);
            var color = isCurrent ? Color.LimeGreen : isReachable ? Color.Gold : Color.SteelBlue;
            HudIcons.FillCircle(spriteBatch, _pixel, center, NodeRadius, color);
            if (isCurrent)
                DrawRingOutline(spriteBatch, center, NodeRadius + 4, Color.White);

            var label = isCurrent ? $"{system.Name} (здесь)"
                : isReachable ? (snapshot.CanWarpNow ? $"[Клик] Прыжок: {system.Name}" : $"{system.Name} (долетите до границы своей системы)")
                : system.Name;
            var labelColor = isCurrent ? Color.LimeGreen : isReachable ? (snapshot.CanWarpNow ? Color.Yellow : Color.Gray) : Color.LightGray;
            var textSize = _font.MeasureString(label) * 0.5f;
            spriteBatch.DrawString(_font, label, new Vector2(center.X - textSize.X / 2f, center.Y + NodeRadius + 6),
                labelColor, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }
    }

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
