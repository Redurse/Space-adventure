using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

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

    public void Draw(SpriteBatch spriteBatch, ReactorState reactor, Vector2 origin, float totalSeconds)
    {
        // The housing is drawn around the content rather than the content being re-laid-out
        // inside it: the slot rectangles are already published by GetSlotRect and the click
        // handler uses the same numbers, so moving them would mean moving input too.
        var bounds = DevicePanelChrome.StandardBounds(origin);
        var phosphor = new Color(236, 176, 92);
        DevicePanelChrome.Draw(spriteBatch, _font, bounds, "РЕАКТОР", "RX-04", phosphor, totalSeconds);

        DevicePanelChrome.DrawReadout(spriteBatch, _font, origin + new Vector2(0, -6),
            "ВЫХОД", $"{reactor.CurrentOutput:0}", $"/ {reactor.MaxOutput:0}", phosphor);
        DevicePanelChrome.DrawReadout(spriteBatch, _font, origin + new Vector2(120, -6),
            "ТОПЛИВО", $"{reactor.Fuel:0}", $"/ {reactor.MaxFuel:0}",
            reactor.Fuel <= reactor.MaxFuel * 0.2f ? new Color(232, 108, 84) : phosphor);

        for (var i = 0; i < reactor.RodCharges.Count; i++)
        {
            var rect = GetSlotRect(i, origin);

            spriteBatch.Draw(_pixel, rect, Color.DimGray * 0.5f);
            DrawRectOutline(spriteBatch, rect, Color.LightGray, 1);

            if (reactor.RodCharges[i] is not { } charge)
                continue;

            // The rod body is always drawn, and only the charge left in it is lit up from the
            // bottom — a spent rod still occupying a slot has to look different from an empty
            // slot, otherwise "swap the dead one out" isn't a readable instruction.
            const int margin = 4;
            var body = new Rectangle(rect.X + margin, rect.Y + margin, rect.Width - margin * 2, rect.Height - margin * 2);
            spriteBatch.Draw(_pixel, body, Color.DarkSlateGray);

            var litHeight = (int)(body.Height * MathHelper.Clamp(charge, 0f, 1f));
            if (litHeight > 0)
            {
                var fill = charge > 0.5f ? Color.YellowGreen : charge > 0.2f ? Color.Orange : Color.OrangeRed;
                spriteBatch.Draw(_pixel, new Rectangle(body.X, body.Bottom - litHeight, body.Width, litHeight), fill);
            }

            var label = litHeight > 0 ? "Яд" : "0%";
            spriteBatch.DrawString(_font, label, new Vector2(rect.X + 4, rect.Y + 8), litHeight > 0 ? Color.Black : Color.OrangeRed, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
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
