using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Anabiosis.Client.Achievements;
using Anabiosis.Client.Rendering;

namespace Anabiosis.Client;

// Direct user request ("в новом разделе настроек... достижения можно было листать как в Стиме") -
// a new Settings tab, same tab-column/content-area shell every other tab already uses
// (Game1.Settings.cs), just a scrollable list instead of sliders/checkboxes since there's nothing
// here to actually configure - only browse. AchievementCatalog.All is the one shared source both
// this tab and AchievementTracker (the thing that actually unlocks them) read from.
public partial class Game1
{
    private const int AchievementRowHeight = 60;
    private const int AchievementListWidth = 1040;
    private const int AchievementScrollbarWidth = 10;

    // Scroll state (mirrors ChangelogPanel's own drag-a-thumb convention, Game1.Menu.cs) - reset
    // whenever the tab is entered fresh, same as every other staged Settings field.
    private float _achievementsScrollOffset;
    private int? _achievementsThumbDragLastMouseY;

    private Rectangle GetAchievementsListAreaRect(Vector2 content) =>
        new((int)content.X, (int)content.Y, AchievementListWidth, SettingsPanelHeight - SettingsContentY - 76);

    private float AchievementsMaxScroll(Vector2 content)
    {
        var overflow = AchievementCatalog.All.Count * AchievementRowHeight - GetAchievementsListAreaRect(content).Height;
        return MathF.Max(0f, overflow);
    }

    // Null when everything already fits (nothing to drag) - same "click missed the thumb" contract
    // ChangelogPanel.GetScrollThumbRect already uses.
    private Rectangle? GetAchievementsThumbRect(Vector2 content)
    {
        var maxScroll = AchievementsMaxScroll(content);
        if (maxScroll <= 0f)
            return null;
        var listArea = GetAchievementsListAreaRect(content);
        var trackX = listArea.Right + 8;
        var contentHeight = AchievementCatalog.All.Count * AchievementRowHeight;
        var thumbHeight = Math.Max(24, (int)(listArea.Height * (listArea.Height / (float)contentHeight)));
        var travel = listArea.Height - thumbHeight;
        var thumbY = listArea.Y + (int)(travel * (_achievementsScrollOffset / maxScroll));
        return new Rectangle(trackX, thumbY, AchievementScrollbarWidth, thumbHeight);
    }

    private void HandleAchievementsTabInput(Vector2 content, Point point, bool held, bool clicked)
    {
        if (!held)
        {
            _achievementsThumbDragLastMouseY = null;
            return;
        }
        if (_achievementsThumbDragLastMouseY is { } lastY)
        {
            var maxScroll = AchievementsMaxScroll(content);
            var listArea = GetAchievementsListAreaRect(content);
            var thumbHeight = GetAchievementsThumbRect(content)?.Height ?? 24;
            var travel = Math.Max(1, listArea.Height - thumbHeight);
            _achievementsScrollOffset = MathHelper.Clamp(_achievementsScrollOffset + (point.Y - lastY) * (maxScroll / travel), 0f, maxScroll);
            _achievementsThumbDragLastMouseY = point.Y;
            return;
        }
        if (clicked && GetAchievementsThumbRect(content) is { } thumbRect && thumbRect.Contains(point))
            _achievementsThumbDragLastMouseY = point.Y;
    }

    private void DrawAchievementsTab(Vector2 content)
    {
        var listArea = GetAchievementsListAreaRect(content);
        var unlockedCount = AchievementCatalog.All.Count(a => _achievementTracker.IsUnlocked(a.Id));
        _spriteBatch.DrawString(_font, $"ПОЛУЧЕНО: {unlockedCount} / {AchievementCatalog.All.Count}",
            content - new Vector2(0, 22), SettingsTextDim, 0f, Vector2.Zero, 0.48f, SpriteEffects.None, 0f);

        for (var i = 0; i < AchievementCatalog.All.Count; i++)
        {
            var y = listArea.Y + i * AchievementRowHeight - (int)_achievementsScrollOffset;
            if (y + AchievementRowHeight < listArea.Y || y > listArea.Bottom)
                continue;
            DrawAchievementRow(new Rectangle(listArea.X, y, listArea.Width, AchievementRowHeight - 6), AchievementCatalog.All[i]);
        }

        if (GetAchievementsThumbRect(content) is { } thumb)
        {
            var track = new Rectangle(thumb.X, listArea.Y, AchievementScrollbarWidth, listArea.Height);
            _spriteBatch.Draw(_pixel, track, new Color(10, 12, 16) * 0.8f);
            var hovered = thumb.Contains(_designMouse);
            _spriteBatch.Draw(_pixel, thumb, (hovered ? Color.Lerp(SettingsAccentGold, Color.White, 0.3f) : SettingsAccentGold) * 0.85f);
        }
    }

    private void DrawAchievementRow(Rectangle rect, AchievementDefinition achievement)
    {
        var unlockedAt = _achievementTracker.UnlockedAt(achievement.Id);
        var unlocked = unlockedAt is not null;

        _spriteBatch.Draw(_pixel, rect, new Color(24, 28, 34) * (unlocked ? 0.85f : 0.55f));
        DrawBevel(rect, raised: false, strength: unlocked ? 0.5f : 0.3f);

        var badge = new Rectangle(rect.X + 8, rect.Y + (rect.Height - 40) / 2, 40, 40);
        _spriteBatch.Draw(_pixel, badge, (unlocked ? SettingsAccentGold : new Color(60, 64, 72)) * (unlocked ? 0.9f : 0.6f));
        var glyphSize = _font.MeasureString(achievement.Glyph) * 0.6f;
        _spriteBatch.DrawString(_font, achievement.Glyph, new Vector2(badge.Center.X - glyphSize.X / 2f, badge.Center.Y - glyphSize.Y / 2f),
            unlocked ? new Color(30, 24, 10) : new Color(150, 154, 160), 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        var textX = badge.Right + 12;
        _spriteBatch.DrawString(_font, achievement.Name, new Vector2(textX, rect.Y + 8),
            unlocked ? SettingsTextPrimary : SettingsTextDim, 0f, Vector2.Zero, 0.52f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, achievement.Description, new Vector2(textX, rect.Y + 28),
            unlocked ? SettingsTextDim : SettingsTextDim * 0.6f, 0f, Vector2.Zero, 0.44f, SpriteEffects.None, 0f);

        if (unlockedAt is { } at)
        {
            var dateLabel = $"Получено {at.ToLocalTime():dd.MM.yyyy}";
            var dateSize = _font.MeasureString(dateLabel) * 0.4f;
            _spriteBatch.DrawString(_font, dateLabel, new Vector2(rect.Right - dateSize.X - 10, rect.Y + rect.Height - 20),
                SettingsAccentTeal, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
        }
    }
}
