using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// The one "cover" every informational UI panel wears - dark desaturated teal face, pale border,
// corner rivets, optional header strip - so InfoPanel, SuitLockerPanel, PauseMenuPanel,
// ConnectionsPanel and ShipEditorPanel all read as the same physical material instead of each
// screen hand-rolling its own near-identical fill+outline. In-world device housings
// (ShipRenderer.DrawPanel/DrawChamferedHousing) are a separate, unrelated family - this is only
// for full-screen/overlay UI.
internal static class PanelFrame
{
    public static readonly Color DefaultFace = new(20, 26, 22);
    public static readonly Color DefaultBorder = new(90, 110, 95);
    private static readonly Color HeaderFace = new(30, 38, 33);

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect,
        Color? face = null, Color? border = null, float alpha = 0.95f, int thickness = 2)
    {
        spriteBatch.Draw(pixel, rect, (face ?? DefaultFace) * alpha);
        ShipRenderer.DrawRectOutline(spriteBatch, pixel, rect, border ?? DefaultBorder, thickness);
        ShipRenderer.DrawRivets(spriteBatch, pixel, rect);
    }

    // Same base panel plus a lighter header strip and rule under it, for panels that show a title.
    public static Rectangle DrawWithHeader(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, int headerHeight,
        Color? face = null, Color? border = null, float alpha = 0.95f, int thickness = 2)
    {
        Draw(spriteBatch, pixel, rect, face, border, alpha, thickness);
        var borderColor = border ?? DefaultBorder;
        var headerRect = new Rectangle(rect.X, rect.Y, rect.Width, headerHeight);
        spriteBatch.Draw(pixel, headerRect, HeaderFace);
        spriteBatch.Draw(pixel, new Rectangle(headerRect.X, headerRect.Bottom - thickness, headerRect.Width, thickness), borderColor);
        return headerRect;
    }
}
