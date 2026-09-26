using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// Direct user request ("в главном меню около версии... маленькая кнопка (изменения в текущей
// версии)... закрыть менюшку можно нажав на крестик в правом верхнем углу"; later "менюшка была
// размером в половину экрана и была в центре"; later still "сделай экран выше и чтобы текст не
// вылазил за экран, можешь сделать сбоку прямоугольничек и если ты его тянешь вниз то листается
// список сверху вниз как сделано много где") - a self-contained popup listing what changed in the
// CURRENT version, opened from a small button next to Game1.cs's own version footer. The 2-column
// layout this used to have let long lines overlap the next column over (direct user bug report,
// screenshot) - a single scrollable column with a draggable scrollbar thumb on the right edge
// avoids that entirely, and has no ceiling on how many entries can ever fit (direct user standing
// instruction, "во всех новых больших патчах... пиши новые изменения" - Game1.Menu.cs's own
// ChangelogEntries). The list itself lives there - this class only knows how to lay one out, where
// its own close button/scrollbar thumb sit, and how much there is to scroll, the same
// draw/GetButtonRect split PauseMenuPanel already uses so a click can never land somewhere the
// drawing doesn't actually show. Scroll offset/drag state is owned by Game1.cs
// (_changelogScrollOffset) - this class is static and holds nothing itself.
public static class ChangelogPanel
{
    // Taller than half of Game1's own DesignHeight (560) now that scrolling means it never HAS to
    // fit everything - just enough to show a handful of lines at a time without feeling cramped.
    public static readonly Point Size = new(600, 420);
    private const int CloseButtonSize = 22;
    private const int Padding = 16;
    private const float LineScale = 0.42f;
    private const int LineHeight = 17;
    private const int HeaderHeight = 26;
    private const int ScrollbarWidth = 10;
    private const int ScrollbarThumbMinHeight = 24;

    public static Rectangle GetPanelRect(Vector2 origin) => new((int)origin.X, (int)origin.Y, Size.X, Size.Y);

    public static Rectangle GetCloseButtonRect(Vector2 origin) =>
        new((int)origin.X + Size.X - CloseButtonSize - 8, (int)origin.Y + 8, CloseButtonSize, CloseButtonSize);

    // The visible text area - everything below the title, right of the padding, left of the
    // scrollbar's own track.
    public static Rectangle GetListAreaRect(Vector2 origin)
    {
        var rect = GetPanelRect(origin);
        return new Rectangle(rect.X + Padding, rect.Y + Padding + HeaderHeight,
            rect.Width - 2 * Padding - ScrollbarWidth - 8, rect.Height - Padding - HeaderHeight - Padding);
    }

    public static float MaxScroll(int entryCount)
    {
        var overflow = entryCount * LineHeight - GetListAreaRect(Vector2.Zero).Height;
        return MathF.Max(0f, overflow);
    }

    // Null when every entry already fits (nothing to drag) - GetScrollThumbRect's own caller
    // (HandleMainMenuClick, hit-testing) treats that the same as "click missed the thumb".
    public static Rectangle? GetScrollThumbRect(Vector2 origin, int entryCount, float scrollOffset)
    {
        var maxScroll = MaxScroll(entryCount);
        if (maxScroll <= 0f)
            return null;
        var listArea = GetListAreaRect(origin);
        var trackX = listArea.Right + 8;
        var contentHeight = entryCount * LineHeight;
        var thumbHeight = Math.Max(ScrollbarThumbMinHeight, (int)(listArea.Height * (listArea.Height / (float)contentHeight)));
        var travel = listArea.Height - thumbHeight;
        var thumbY = listArea.Y + (int)(travel * (scrollOffset / maxScroll));
        return new Rectangle(trackX, thumbY, ScrollbarWidth, thumbHeight);
    }

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, SpriteFont font, Vector2 origin,
        string title, IReadOnlyList<string> entries, float scrollOffset, Point hoverPoint)
    {
        var rect = GetPanelRect(origin);

        spriteBatch.Draw(pixel, new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4), Color.Black * 0.55f);
        spriteBatch.Draw(pixel, rect, new Color(18, 20, 26) * 0.97f);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), new Color(90, 220, 195) * 0.7f);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), new Color(90, 220, 195) * 0.35f);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), new Color(90, 220, 195) * 0.35f);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), new Color(90, 220, 195) * 0.35f);

        spriteBatch.DrawString(font, title, new Vector2(rect.X + Padding, rect.Y + Padding),
            new Color(90, 220, 195), 0f, Vector2.Zero, 0.52f, SpriteEffects.None, 0f);

        // No real clip rect - a line is either fully drawn or not drawn at all depending on whether
        // its own row falls inside the list area, never partially cut. Simple, and with LineHeight
        // rows there is no half-visible sliver to look wrong either way.
        var listArea = GetListAreaRect(origin);
        for (var i = 0; i < entries.Count; i++)
        {
            var y = listArea.Y + i * LineHeight - (int)scrollOffset;
            if (y + LineHeight < listArea.Y || y > listArea.Bottom)
                continue;
            spriteBatch.DrawString(font, "•", new Vector2(listArea.X, y),
                new Color(150, 156, 166), 0f, Vector2.Zero, LineScale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, entries[i], new Vector2(listArea.X + 10, y),
                Color.White * 0.9f, 0f, Vector2.Zero, LineScale, SpriteEffects.None, 0f);
        }

        // The scrollbar track, drawn even at rest (0 entries scrolled) whenever there's anything to
        // scroll to at all - a completely invisible affordance is what "тянуть его вниз" needs
        // something to actually grab in the first place.
        if (GetScrollThumbRect(origin, entries.Count, scrollOffset) is { } thumbRect)
        {
            var trackRect = new Rectangle(thumbRect.X, listArea.Y, ScrollbarWidth, listArea.Height);
            spriteBatch.Draw(pixel, trackRect, new Color(10, 12, 16) * 0.8f);
            var thumbHovered = thumbRect.Contains(hoverPoint);
            spriteBatch.Draw(pixel, thumbRect, (thumbHovered ? new Color(120, 235, 210) : new Color(90, 220, 195)) * 0.85f);
        }

        var closeRect = GetCloseButtonRect(origin);
        var hovered = closeRect.Contains(hoverPoint);
        spriteBatch.Draw(pixel, closeRect, (hovered ? new Color(200, 70, 60) : new Color(60, 64, 72)) * 0.9f);
        var closeLabelSize = font.MeasureString("X") * 0.5f;
        spriteBatch.DrawString(font, "X",
            new Vector2(closeRect.Center.X - closeLabelSize.X / 2f, closeRect.Center.Y - closeLabelSize.Y / 2f),
            Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }
}
