using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Shown only while the battery block is "open" (game_design.md section 1 — emergency power
// storage next to the reactor/distribution block). Purely a readout: charge/capacity and a bar,
// same idea as ReactorPanel's header but without any slots to click, since nothing is inserted
// into a battery by hand.
public sealed class BatteryPanel
{
    public const int BarWidth = 220;
    public const int BarHeight = 20;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public BatteryPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, PowerState power, Vector2 origin)
    {
        var header = $"Батарея: {power.BatteryCharge:0}/{power.BatteryCapacity:0}";
        spriteBatch.DrawString(_font, header, origin, Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);

        var barOrigin = origin + new Vector2(0, 24);
        var barRect = new Rectangle((int)barOrigin.X, (int)barOrigin.Y, BarWidth, BarHeight);
        spriteBatch.Draw(_pixel, barRect, Color.DimGray * 0.5f);

        var fraction = power.BatteryCapacity > 0 ? MathHelper.Clamp(power.BatteryCharge / power.BatteryCapacity, 0f, 1f) : 0f;
        var filledWidth = (int)(BarWidth * fraction);
        if (filledWidth > 0)
        {
            var fill = fraction > 0.5f ? Color.YellowGreen : fraction > 0.2f ? Color.Orange : Color.OrangeRed;
            spriteBatch.Draw(_pixel, new Rectangle(barRect.X, barRect.Y, filledWidth, barRect.Height), fill);
        }

        var hintOrigin = barOrigin + new Vector2(0, BarHeight + 10);
        spriteBatch.DrawString(_font, "Заряжается от избытка мощности реактора, отдаёт при её нехватке", hintOrigin, Color.Yellow, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }
}
