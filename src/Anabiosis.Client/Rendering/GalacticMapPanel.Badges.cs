using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// What stands in a system, as the small square badge Stellaris hangs over a star: the colour of the realm behind a white
// sign - a planet for a colony (ringed and gold-edged for the capital), a hexagon for a yard (filled for a megayard),
// a small square for a mining outpost, a spiked star for a nest of the virus. Seen from IconZoom on.
public sealed partial class GalacticMapPanel
{
    private enum Mark { Outpost, Colony, Capital, Yard, Megayard, Nest }

    private static readonly Color Gold = new(255, 214, 120);

    private void DrawInstallations(SpriteBatch spriteBatch, int index, WarNodeState node, Vector2 center, float radius, float zoom)
    {
        if (node.Owner == 0)
            return;
        Mark? main = node.Planet == 3 ? Mark.Nest
            : node.Planet == 2 ? Mark.Capital
            : node.Yard == 2 ? Mark.Megayard
            : node.Yard == 1 ? Mark.Yard
            : node.Planet == 1 ? Mark.Colony
            : node.Outpost ? Mark.Outpost
            : null;
        if (main is null)
            return;

        var color = OwnerColor(node.Owner);
        var size = Math.Clamp(8f + zoom * 5f, 9f, 15f);
        var at = center + new Vector2(0f, -(radius + 4f + size / 2f));
        DrawBadge(spriteBatch, main.Value, at, size, color);

        // A yard beside a colony or the capital, and work under way, as small extras on the badge.
        if (node.Yard != 0 && main is Mark.Colony or Mark.Capital or Mark.Nest)
            DrawMark(spriteBatch, node.Yard == 2 ? Mark.Megayard : Mark.Yard, at + new Vector2(size / 2f + 5f, 0f), size * 0.55f, Color.Lerp(color, Color.White, 0.6f));
        if (node.Project != 0)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(_time * 5f + index);
            HudIcons.FillCircle(spriteBatch, _pixel, at + new Vector2(size / 2f, -size / 2f), 2.3f, new Color(255, 176, 60) * (0.55f + 0.45f * pulse));
        }
    }

    // The badge for the legend and the map alike: a square in the dark of the realm colour with a lit edge and the sign inside.
    private void DrawBadge(SpriteBatch spriteBatch, Mark mark, Vector2 center, float size, Color color)
    {
        var half = size / 2f;
        var x = (int)MathF.Round(center.X - half);
        var y = (int)MathF.Round(center.Y - half);
        var side = (int)MathF.Round(size);
        spriteBatch.Draw(_pixel, new Rectangle(x, y, side, side), Color.Lerp(color, new Color(4, 8, 14), 0.55f));
        var edge = mark == Mark.Capital ? Gold : Color.Lerp(color, Color.White, 0.3f);
        spriteBatch.Draw(_pixel, new Rectangle(x, y, side, 1), edge);
        spriteBatch.Draw(_pixel, new Rectangle(x, y + side - 1, side, 1), edge);
        spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, side), edge);
        spriteBatch.Draw(_pixel, new Rectangle(x + side - 1, y, 1, side), edge);
        DrawMark(spriteBatch, mark, center, size, Color.White);
    }

    // The sign itself, scaled to a badge of the given size.
    private void DrawMark(SpriteBatch spriteBatch, Mark mark, Vector2 center, float size, Color color)
    {
        var u = size / 11f; // the signs are drawn for a badge 11 wide
        switch (mark)
        {
            case Mark.Outpost:
                var half = MathF.Max(1.5f, 1.8f * u);
                spriteBatch.Draw(_pixel, new Rectangle((int)MathF.Round(center.X - half), (int)MathF.Round(center.Y - half), (int)MathF.Round(half * 2f), (int)MathF.Round(half * 2f)), color);
                break;
            case Mark.Colony:
                HudIcons.FillCircle(spriteBatch, _pixel, center, 2.7f * u, color);
                break;
            case Mark.Capital:
                HudIcons.FillCircle(spriteBatch, _pixel, center, 2.2f * u, color);
                HudIcons.DrawRingArc(spriteBatch, _pixel, center, 3.9f * u, 0f, 360f, color * 0.9f, 16, 1f);
                break;
            case Mark.Yard:
            case Mark.Megayard:
                var hex = new Vector2[6];
                var r = (mark == Mark.Megayard ? 3.9f : 3.4f) * u;
                for (var k = 0; k < 6; k++)
                    hex[k] = center + new Vector2(MathF.Cos(k * MathF.PI / 3f), MathF.Sin(k * MathF.PI / 3f)) * r;
                if (mark == Mark.Megayard)
                    Primitives.FillPolygon(spriteBatch, _pixel, center, hex, color);
                else
                    Primitives.StrokePolygon(spriteBatch, _pixel, hex, color, 1.2f);
                break;
            case Mark.Nest:
                var spin = _time * 0.5f;
                var spikes = new Vector2[16];
                for (var k = 0; k < 16; k++)
                    spikes[k] = center + new Vector2(MathF.Cos(spin + k * MathF.PI / 8f), MathF.Sin(spin + k * MathF.PI / 8f)) * (k % 2 == 0 ? 4.8f * u : 2.2f * u);
                Primitives.FillPolygon(spriteBatch, _pixel, center, spikes, color);
                break;
        }
    }

    // A small arrowhead pointing along `direction`.
    private void DrawChevron(SpriteBatch spriteBatch, Vector2 position, Vector2 direction, float size, Color color)
    {
        var side = new Vector2(-direction.Y, direction.X);
        var hull = new[] { position + direction * size, position - direction * size * 0.8f + side * size * 0.8f, position - direction * size * 0.3f, position - direction * size * 0.8f - side * size * 0.8f };
        Primitives.FillPolygon(spriteBatch, _pixel, position, hull, color);
        Primitives.StrokePolygon(spriteBatch, _pixel, hull, Color.Black * 0.7f, 1f);
    }
}
