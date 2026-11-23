using System;
using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Protocol;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// The little film of a hyperspace jump: when the ship's system changes, a small galactic map comes up over the game and a glowing mark leaves the old system,
// runs down the lane and lands on the new one (and the same mark rides on the big galactic map if it is open). It starts and ends itself - the only input is the
// snapshots: a change of CurrentSystemId between two of them is a jump. Nothing is drawn for the very first snapshot, so loading a game is not a jump.
public sealed class JumpAnimation
{
    private const float Duration = 3.4f;     // seconds, all together
    private const float ChargeShare = 0.22f; // the drive winds up at the old system
    private const float TravelShare = 0.58f; // then the run; the rest is the landing and the fade
    private static readonly Rectangle Panel = new(390, 44, 420, 176);

    private string? _lastSystemId;
    private string? _from;
    private string? _to;
    private double _startedAt = double.NegativeInfinity;

    private static double Now => Environment.TickCount64 / 1000.0;

    public bool Active => Now - _startedAt < Duration;

    // Called every frame with the newest snapshot.
    public void Observe(WorldSnapshot? snapshot)
    {
        if (snapshot is null)
            return;
        if (_lastSystemId is not null && _lastSystemId != snapshot.CurrentSystemId)
        {
            _from = _lastSystemId;
            _to = snapshot.CurrentSystemId;
            _startedAt = Now;
        }
        _lastSystemId = snapshot.CurrentSystemId;
    }

    // A new game or a return to the menu: whatever was flying is forgotten.
    public void Reset()
    {
        _lastSystemId = null;
        _from = null;
        _to = null;
        _startedAt = double.NegativeInfinity;
    }

    private float Progress => (float)Math.Clamp((Now - _startedAt) / Duration, 0.0, 1.0);

    // 0 while winding up, 0..1 along the run, 1 after the landing: where the mark is on the lane.
    private float Travel
    {
        get
        {
            var t = Progress;
            if (t <= ChargeShare)
                return 0f;
            var run = Math.Clamp((t - ChargeShare) / TravelShare, 0f, 1f);
            return run * run * (3f - 2f * run); // eases in and out: it leaves slowly, rushes, and settles
        }
    }

    // The overlay: a small map of the two systems and their neighbourhood. Not drawn while the big map is open (it shows the mark itself).
    public void DrawOverlay(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, WorldSnapshot snapshot)
    {
        if (!Active || _from is null || _to is null)
            return;
        var systems = snapshot.StarSystems;
        var from = systems.FirstOrDefault(s => s.Id == _from);
        var to = systems.FirstOrDefault(s => s.Id == _to);
        if (from is null || to is null)
            return;

        var t = Progress;
        var fade = t < 0.08f ? t / 0.08f : t > 0.88f ? Math.Max(0f, (1f - t) / 0.12f) : 1f;
        var grow = Math.Min(1f, t / 0.08f);
        var panel = new Rectangle(Panel.X, Panel.Y - (int)((1f - grow) * 14), Panel.Width, Panel.Height);

        spriteBatch.Draw(pixel, new Rectangle(panel.X - 2, panel.Y - 2, panel.Width + 4, panel.Height + 4), new Color(70, 120, 160) * (0.8f * fade));
        spriteBatch.Draw(pixel, panel, new Color(6, 10, 18) * (0.94f * fade));

        var title = "ГИПЕРПРЫЖОК";
        var titleSize = font.MeasureString(title) * 0.6f;
        spriteBatch.DrawString(font, title, new Vector2(panel.Center.X - titleSize.X / 2f, panel.Y + 8), new Color(160, 210, 245) * fade, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        // The patch of galaxy: the two systems with a margin, fitted into the box with equal scale in both directions.
        var box = new Rectangle(panel.X + 16, panel.Y + 34, panel.Width - 32, panel.Height - 78);
        spriteBatch.Draw(pixel, box, new Color(2, 4, 8) * fade);
        var span = Math.Max(Math.Abs(from.GalaxyX - to.GalaxyX), Math.Abs(from.GalaxyY - to.GalaxyY));
        var margin = Math.Max(45f, span * 0.3f); // the pair fills most of the box, whatever the distance
        var minX = Math.Min(from.GalaxyX, to.GalaxyX) - margin;
        var maxX = Math.Max(from.GalaxyX, to.GalaxyX) + margin;
        var minY = Math.Min(from.GalaxyY, to.GalaxyY) - margin;
        var maxY = Math.Max(from.GalaxyY, to.GalaxyY) + margin;
        var scale = Math.Min(box.Width / (maxX - minX), box.Height / (maxY - minY));
        var offset = new Vector2(box.Center.X - (minX + maxX) / 2f * scale, box.Center.Y - (minY + maxY) / 2f * scale);
        Vector2 At(float x, float y) => offset + new Vector2(x, y) * scale;

        // Neighbouring stars as faint dots.
        foreach (var system in systems)
        {
            if (system.GalaxyX < minX || system.GalaxyX > maxX || system.GalaxyY < minY || system.GalaxyY > maxY)
                continue;
            var p = At(system.GalaxyX, system.GalaxyY);
            spriteBatch.Draw(pixel, new Rectangle((int)p.X - 1, (int)p.Y - 1, 2, 2), new Color(120, 150, 190) * (0.55f * fade));
        }

        var a = At(from.GalaxyX, from.GalaxyY);
        var b = At(to.GalaxyX, to.GalaxyY);
        DrawDashedLane(spriteBatch, pixel, a, b, new Color(90, 150, 210) * (0.6f * fade));

        var travel = Travel;
        var mark = Vector2.Lerp(a, b, travel);
        // Trail: a few fading marks behind it, only while it is running.
        if (travel > 0f && travel < 1f)
            for (var i = 1; i <= 10; i++)
            {
                var back = Math.Max(0f, travel - i * 0.035f);
                DrawGlow(spriteBatch, pixel, Vector2.Lerp(a, b, back), 5f - i * 0.35f, new Color(110, 190, 255) * ((1f - i / 11f) * 0.5f * fade));
            }

        // The drive winding up at the old system, the flash on landing at the new one.
        var charge = t < ChargeShare ? t / ChargeShare : 1f;
        var land = t < ChargeShare + TravelShare ? 0f : Math.Min(1f, (t - ChargeShare - TravelShare) / (1f - ChargeShare - TravelShare));
        DrawSystemMark(spriteBatch, pixel, a, 5f + (travel == 0f ? charge * 6f * (0.6f + 0.4f * MathF.Sin((float)Now * 18f)) : 0f), new Color(255, 200, 110) * fade);
        DrawSystemMark(spriteBatch, pixel, b, 5f + land * 14f * (1f - land), new Color(120, 255, 190) * fade);
        DrawGlow(spriteBatch, pixel, mark, 7f, new Color(170, 225, 255) * fade);
        DrawGlow(spriteBatch, pixel, mark, 3f, Color.White * fade);

        DrawLabel(spriteBatch, font, from.Name, a, box, new Color(255, 215, 150) * fade);
        DrawLabel(spriteBatch, font, to.Name, b, box, new Color(150, 255, 205) * fade);

        // The recharge: the bar fills from empty as the minute runs down (server-side, so every crew member sees the same).
        var cooldown = snapshot.WarpCooldownRemaining;
        var bar = new Rectangle(panel.X + 16, panel.Bottom - 30, panel.Width - 32, 8);
        spriteBatch.Draw(pixel, bar, new Color(20, 28, 40) * fade);
        var filled = 1f - Math.Clamp(cooldown / 60f, 0f, 1f);
        spriteBatch.Draw(pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * filled), bar.Height), new Color(80, 170, 230) * fade);
        var text = cooldown > 0.5f ? $"Перезарядка двигателя: {(int)MathF.Ceiling(cooldown)} с" : "Двигатель готов";
        var textSize = font.MeasureString(text) * 0.45f;
        spriteBatch.DrawString(font, text, new Vector2(panel.Center.X - textSize.X / 2f, panel.Bottom - 20), new Color(170, 200, 225) * fade, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }

    // The mark on the big galactic map, in its own coordinates (`nodeCenter` is the panel's own placement of a system).
    public void DrawOnMap(SpriteBatch spriteBatch, Texture2D pixel, WorldSnapshot snapshot, Func<StarSystemSummary, Vector2> nodeCenter)
    {
        if (!Active || _from is null || _to is null)
            return;
        var from = snapshot.StarSystems.FirstOrDefault(s => s.Id == _from);
        var to = snapshot.StarSystems.FirstOrDefault(s => s.Id == _to);
        if (from is null || to is null)
            return;

        var t = Progress;
        var fade = t > 0.88f ? Math.Max(0f, (1f - t) / 0.12f) : 1f;
        var a = nodeCenter(from);
        var b = nodeCenter(to);
        DrawDashedLane(spriteBatch, pixel, a, b, new Color(150, 210, 255) * (0.7f * fade));
        var travel = Travel;
        if (travel > 0f && travel < 1f)
            for (var i = 1; i <= 12; i++)
                DrawGlow(spriteBatch, pixel, Vector2.Lerp(a, b, Math.Max(0f, travel - i * 0.03f)), 7f - i * 0.4f, new Color(110, 190, 255) * ((1f - i / 13f) * 0.55f * fade));
        var land = t < ChargeShare + TravelShare ? 0f : Math.Min(1f, (t - ChargeShare - TravelShare) / (1f - ChargeShare - TravelShare));
        DrawSystemMark(spriteBatch, pixel, a, 8f + (travel == 0f ? (t / ChargeShare) * 8f : 0f), new Color(255, 200, 110) * fade);
        DrawSystemMark(spriteBatch, pixel, b, 8f + land * 22f * (1f - land), new Color(120, 255, 190) * fade);
        var mark = Vector2.Lerp(a, b, travel);
        DrawGlow(spriteBatch, pixel, mark, 11f, new Color(170, 225, 255) * fade);
        DrawGlow(spriteBatch, pixel, mark, 5f, Color.White * fade);
    }

    // ---- drawing helpers: a soft disc is a stack of squares of shrinking size and rising brightness ----

    private static void DrawGlow(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color)
    {
        radius = Math.Max(1f, radius);
        for (var ring = 0; ring < 4; ring++)
        {
            var r = radius * (1f - ring * 0.22f);
            spriteBatch.Draw(pixel, new Rectangle((int)(center.X - r), (int)(center.Y - r), (int)(r * 2f), (int)(r * 2f)), color * (0.25f + ring * 0.2f));
        }
    }

    // A ring around a system: a thin square outline.
    private static void DrawSystemMark(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color)
    {
        var rect = new Rectangle((int)(center.X - radius), (int)(center.Y - radius), (int)(radius * 2f), (int)(radius * 2f));
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), color);
        spriteBatch.Draw(pixel, new Rectangle((int)center.X - 1, (int)center.Y - 1, 3, 3), color);
    }

    private static void DrawDashedLane(SpriteBatch spriteBatch, Texture2D pixel, Vector2 a, Vector2 b, Color color)
    {
        var length = Vector2.Distance(a, b);
        if (length < 1f)
            return;
        var dashes = Math.Max(1, (int)(length / 8f));
        for (var i = 0; i < dashes; i += 2)
        {
            var p = Vector2.Lerp(a, b, i / (float)dashes);
            spriteBatch.Draw(pixel, new Rectangle((int)p.X, (int)p.Y, 2, 2), color);
            var q = Vector2.Lerp(a, b, (i + 0.5f) / dashes);
            spriteBatch.Draw(pixel, new Rectangle((int)q.X, (int)q.Y, 2, 2), color);
        }
    }

    // The name beside its system, kept inside the box.
    private static void DrawLabel(SpriteBatch spriteBatch, SpriteFont font, string name, Vector2 at, Rectangle box, Color color)
    {
        var size = font.MeasureString(name) * 0.45f;
        var x = Math.Clamp(at.X - size.X / 2f, box.X + 2, box.Right - size.X - 2);
        var y = at.Y + 9 + size.Y > box.Bottom ? at.Y - 10 - size.Y : at.Y + 9;
        spriteBatch.DrawString(font, name, new Vector2(x, y), color, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }
}
