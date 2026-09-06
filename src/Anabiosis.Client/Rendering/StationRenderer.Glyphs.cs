using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// One small silhouette per NpcKind, replacing what used to be a flat coloured square for every
// role - the same "build it from the one white pixel" primitives (FillCircle/FillPolygon/
// RingArc) every other procedural glyph in this client already uses.
public sealed partial class StationRenderer
{
    private void DrawNpcGlyph(SpriteBatch spriteBatch, NpcKind kind, Rectangle rect, Color color)
    {
        switch (kind)
        {
            case NpcKind.Trader:
                DrawTraderGlyph(spriteBatch, rect, color);
                break;
            case NpcKind.Mechanic:
                DrawMechanicGlyph(spriteBatch, rect, color);
                break;
            case NpcKind.Shipwright:
                DrawShipwrightGlyph(spriteBatch, rect, color);
                break;
            case NpcKind.Security:
                DrawSecurityGlyph(spriteBatch, rect, color);
                break;
            default: // Administrator
                DrawAdministratorGlyph(spriteBatch, rect, color);
                break;
        }
    }

    // A stack of coins, slightly offset - reads as "money" without needing a currency symbol the
    // font may not even carry.
    private void DrawTraderGlyph(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        var r = rect.Width * 0.26f;
        foreach (var offset in new[] { new Vector2(-r * 0.5f, r * 0.5f), new Vector2(r * 0.5f, r * 0.5f), Vector2.Zero })
        {
            var coin = center + offset;
            HudIcons.FillCircle(spriteBatch, _pixel, coin, r, Color.Lerp(color, Color.White, 0.15f));
            HudIcons.DrawRingArc(spriteBatch, _pixel, coin, r * 0.7f, 0f, 360f, Color.Black * 0.3f, 10, 1f);
        }
    }

    // A wrench: a bar to an open jaw at one end - the same silhouette idea ItemIcons' own wrench
    // uses, simplified down to marker size.
    private void DrawMechanicGlyph(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        var half = rect.Width * 0.32f;
        var angle = -MathF.PI / 4f;
        var along = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var handleStart = center - along * half;
        var jawCenter = center + along * half;

        spriteBatch.Draw(_pixel, handleStart, null, color, angle, new Vector2(0f, 0.5f),
            new Vector2(half * 1.7f, half * 0.4f), SpriteEffects.None, 0f);
        HudIcons.FillCircle(spriteBatch, _pixel, jawCenter, half * 0.55f, color);
        HudIcons.FillCircle(spriteBatch, _pixel, jawCenter, half * 0.3f, Color.Lerp(color, Color.Black, 0.5f));
    }

    // A clipboard: a plate with a couple of ruled lines on it - the quest board made small.
    private void DrawAdministratorGlyph(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        var board = new Rectangle(rect.X + rect.Width / 5, rect.Y + rect.Height / 6, rect.Width * 3 / 5, rect.Height * 2 / 3);
        spriteBatch.Draw(_pixel, board, Color.Lerp(color, Color.White, 0.2f));
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, board, Color.Black * 0.4f, 1);
        for (var i = 0; i < 3; i++)
            spriteBatch.Draw(_pixel, new Rectangle(board.X + 2, board.Y + 4 + i * (board.Height - 8) / 3, board.Width - 4, 2), Color.Black * 0.35f);
        spriteBatch.Draw(_pixel, new Rectangle(board.X + board.Width / 2 - 3, board.Y - 2, 6, 4), color);
    }

    // A little hull-and-bow silhouette rather than a tool - the shipwright sells ships, not fixes.
    private void DrawShipwrightGlyph(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        var r = rect.Width * 0.36f;
        var hull = new[]
        {
            center + new Vector2(0, -r), center + new Vector2(r * 0.7f, r * 0.15f),
            center + new Vector2(r * 0.4f, r * 0.8f), center + new Vector2(-r * 0.4f, r * 0.8f),
            center + new Vector2(-r * 0.7f, r * 0.15f),
        };
        Primitives.FillPolygon(spriteBatch, _pixel, center, hull, color);
        Primitives.StrokePolygon(spriteBatch, _pixel, hull, Color.White * 0.6f, 1.2f);
    }

    // A shield outline - security's own badge.
    private void DrawSecurityGlyph(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        var r = rect.Width * 0.38f;
        var shield = new[]
        {
            center + new Vector2(0, -r), center + new Vector2(r * 0.85f, -r * 0.45f),
            center + new Vector2(r * 0.7f, r * 0.5f), center + new Vector2(0, r),
            center + new Vector2(-r * 0.7f, r * 0.5f), center + new Vector2(-r * 0.85f, -r * 0.45f),
        };
        Primitives.FillPolygon(spriteBatch, _pixel, center, shield, color);
        Primitives.StrokePolygon(spriteBatch, _pixel, shield, Color.White * 0.7f, 1.4f);
    }
}
