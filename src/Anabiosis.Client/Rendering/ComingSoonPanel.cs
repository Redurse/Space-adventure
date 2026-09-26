using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// Direct user request (Junction/"Щиток" - "сделай чтобы при заходе в него открывался серый экран
// с надписью скоро") - a placeholder full-screen takeover for a fixture that's visually/physically
// real (its own bespoke DeviceSkin face, real half-width footprint) but has no actual feature behind
// it yet, same "placeable now, wired later" shape CommsConsole's own stub button used before this
// session's follow-up gave it a real panel. Deliberately generic (no per-instance state, no per-kind
// name baked in) so a future not-yet-built feature can reuse the same screen instead of each one
// inventing its own "coming soon" look.
public sealed class ComingSoonPanel
{
    private static readonly Color BackgroundColor = new(46, 48, 54);
    private static readonly Color TextColor = new(150, 156, 166);

    public void Draw(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, Rectangle area, string label = "СКОРО")
    {
        spriteBatch.Draw(pixel, area, BackgroundColor);
        const float scale = 1.4f;
        var size = font.MeasureString(label) * scale;
        var position = new Vector2(area.Center.X - size.X / 2f, area.Center.Y - size.Y / 2f);
        spriteBatch.DrawString(font, label, position, TextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }
}
