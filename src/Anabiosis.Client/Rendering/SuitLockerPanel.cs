using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The card a suit locker shows when clicked (BlockKind.SuitLocker, Game1.Input.cs) - title, a
// bordered icon box, and a status bar underneath, matching the reference screenshot's "underwater
// gear locker" popup. Read-only, same as ConnectionsPanel/SystemDevicePanel: the actual take/put
// action is still the E-key interact (World.Interact.cs, now gated on this locker's own stock) -
// this just shows what's in it and what F will do, it doesn't perform the swap itself.
public sealed class SuitLockerPanel
{
    private const int PanelWidth = 220;
    private const int PanelHeight = 260;
    private const int BorderThickness = 2;
    private const int IconBoxSize = 130;
    private const int BarHeight = 12;
    private static readonly Color PanelBackground = new(20, 26, 22);
    private static readonly Color PanelBorder = new(90, 110, 95);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public SuitLockerPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, string lockerId, int playerId, Vector2 origin)
    {
        var hasSuit = snapshot.SuitLockerStates.FirstOrDefault(s => s.LockerId == lockerId)?.HasSuit ?? false;
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId);
        var wearingSuit = me?.WearingSuit ?? false;

        var panelRect = new Rectangle((int)origin.X, (int)origin.Y, PanelWidth, PanelHeight);
        PanelFrame.Draw(spriteBatch, _pixel, panelRect, PanelBackground, PanelBorder, thickness: BorderThickness);

        var titleSize = _font.MeasureString("ШКАФ ДЛЯ") * 0.55f;
        spriteBatch.DrawString(_font, "ШКАФ ДЛЯ", new Vector2(panelRect.Center.X - titleSize.X / 2f, panelRect.Y + 12),
            Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        var titleSize2 = _font.MeasureString("СКАФАНДРА") * 0.55f;
        spriteBatch.DrawString(_font, "СКАФАНДРА", new Vector2(panelRect.Center.X - titleSize2.X / 2f, panelRect.Y + 32),
            Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        var iconBox = new Rectangle(panelRect.Center.X - IconBoxSize / 2, panelRect.Y + 58, IconBoxSize, IconBoxSize);
        spriteBatch.Draw(_pixel, iconBox, new Color(10, 14, 12));
        DrawRectOutline(spriteBatch, iconBox, hasSuit ? Color.CadetBlue : new Color(70, 80, 78), 2);
        if (hasSuit)
            HudIcons.DrawSuitGlyph(spriteBatch, _pixel, new Vector2(iconBox.Center.X, iconBox.Center.Y + 8), 5.5f, Color.CadetBlue);
        else
        {
            var emptyLabelSize = _font.MeasureString("ПУСТО") * 0.5f;
            spriteBatch.DrawString(_font, "ПУСТО", new Vector2(iconBox.Center.X - emptyLabelSize.X / 2f, iconBox.Center.Y - emptyLabelSize.Y / 2f),
                Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        var barRect = new Rectangle(iconBox.X, iconBox.Bottom + 12, IconBoxSize, BarHeight);
        spriteBatch.Draw(_pixel, barRect, Color.Black * 0.6f);
        spriteBatch.Draw(_pixel, hasSuit ? barRect : new Rectangle(barRect.X, barRect.Y, 0, barRect.Height),
            hasSuit ? Color.LimeGreen : Color.DimGray);
        DrawRectOutline(spriteBatch, barRect, PanelBorder, 1);

        var hint = (wearingSuit, hasSuit) switch
        {
            (false, true) => ("[E] надеть скафандр", Color.Gold),
            (false, false) => ("Шкаф пуст", Color.Gray),
            (true, false) => ("[E] снять скафандр сюда", Color.LightGreen),
            (true, true) => ("Скафандр уже надет", Color.Gray),
        };
        var hintSize = _font.MeasureString(hint.Item1) * 0.5f;
        spriteBatch.DrawString(_font, hint.Item1, new Vector2(panelRect.Center.X - hintSize.X / 2f, barRect.Bottom + 14),
            hint.Item2, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
