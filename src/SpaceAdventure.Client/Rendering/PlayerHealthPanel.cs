using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client.Rendering;

// The local player's own health, moved into the bottom-right corner as a bar (used to be a text
// line inside the bottom-left combat panel) - always visible regardless of context, the same way
// the inventory row above it always is, since knowing you're hurt shouldn't require opening a
// menu or standing in combat.
public sealed class PlayerHealthPanel
{
    public const int BarWidth = 180;
    public const int BarHeight = 18;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public PlayerHealthPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, CharacterState? me, Vector2 origin)
    {
        if (me is null)
            return;

        spriteBatch.Draw(_pixel, new Rectangle((int)origin.X, (int)origin.Y, BarWidth, BarHeight), Color.DimGray);
        var ratio = MathHelper.Clamp(me.Health / 100f, 0f, 1f);
        var color = me.Health > 40 ? Color.LightGreen : Color.IndianRed;
        spriteBatch.Draw(_pixel, new Rectangle((int)origin.X, (int)origin.Y, (int)(BarWidth * ratio), BarHeight), color);

        var suitStatus = me.WearingSuit ? " [скафандр]" : "";
        var bleedingStatus = me.IsBleeding ? " [кровотечение]" : "";
        spriteBatch.DrawString(_font, $"{me.Health:0}/100{suitStatus}{bleedingStatus}", origin + new Vector2(4, 1),
            Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        // Only means anything with a suit actually on - shown whether on or off (not just when
        // active), since the whole point is telling at a glance which way it's currently set
        // before touching the hull (World.Eva.cs's TryAutoAttach: grabs on when on, bounces when
        // off). Placed above the bar so it never collides with the "НЕДЕЕСПОСОБЕН" line below it.
        if (me.WearingSuit)
        {
            var bootsLabel = me.MagneticBootsOn ? "Магнитные ботинки: ВКЛ" : "Магнитные ботинки: выкл";
            var bootsColor = me.MagneticBootsOn ? Color.LightGreen : Color.Gray;
            spriteBatch.DrawString(_font, bootsLabel, origin + new Vector2(0, -16), bootsColor, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        // At 0 there's no separate "dead" state (World.Injuries.cs) - the character just keeps
        // standing there, fully mobile, with welding/cutting silently refusing to light
        // (World.Welding.cs/World.Cutting.cs both gate on Health > 0). Without this line, that
        // reads as "the tool is broken" rather than "you're down and need a MedKit."
        if (me.Health <= 0)
            spriteBatch.DrawString(_font, "НЕДЕЕСПОСОБЕН - нужна аптечка (сварка/резак не работают)",
                origin + new Vector2(-BarWidth * 1.2f, BarHeight + 4), Color.Red, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }
}
