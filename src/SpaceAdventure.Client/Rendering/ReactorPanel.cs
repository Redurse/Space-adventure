using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Shown only while the reactor block is "open" (game_design.md section 1). 4 fuel-rod slots —
// click one to insert a rod held in hand, or click a loaded slot to take its rod back — plus a
// small output/fuel readout.
public sealed class ReactorPanel
{
    public const int SlotSize = 30;
    public const int SlotSpacing = 6;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public ReactorPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetSlotRect(int index, Vector2 origin)
    {
        var slotOrigin = origin + new Vector2(0, 24) + new Vector2(index * (SlotSize + SlotSpacing), 0);
        return new Rectangle((int)slotOrigin.X, (int)slotOrigin.Y, SlotSize, SlotSize);
    }

    public void Draw(SpriteBatch spriteBatch, ReactorState reactor, Vector2 origin)
    {
        var header = $"Реактор: {reactor.CurrentOutput:0}/{reactor.MaxOutput:0}  Топливо: {reactor.Fuel:0}/{reactor.MaxFuel:0}";
        spriteBatch.DrawString(_font, header, origin, Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);

        for (var i = 0; i < reactor.RodSlots.Count; i++)
        {
            var rect = GetSlotRect(i, origin);
            var loaded = reactor.RodSlots[i];

            spriteBatch.Draw(_pixel, rect, Color.DimGray * 0.5f);
            DrawRectOutline(spriteBatch, rect, Color.LightGray, 1);

            if (loaded)
            {
                const int margin = 4;
                spriteBatch.Draw(_pixel, new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2), Color.YellowGreen);
                spriteBatch.DrawString(_font, "Яд", new Vector2(rect.X + 4, rect.Y + 8), Color.Black, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            }
        }

        var hintOrigin = origin + new Vector2(0, 24 + SlotSize + 10);
        spriteBatch.DrawString(_font, "Клик по слоту: вставить/забрать стержень из руки", hintOrigin, Color.Yellow, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
