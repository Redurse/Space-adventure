using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

public enum InfoTab
{
    Team,
    Missions,
    Reputation,
    Ship,
    Character,
}

// The top bar's "Информация" button takes over the viewport the same way the galaxy map does
// (Game1.cs) - a full-screen read on the crew, its jobs, the standing with every faction, the
// ship's own fit, and (later) the character sheet. Five tabs down the right edge, one content area
// on the left that swaps per tab - nothing here changes game state, it's all read from the
// snapshot the rest of the HUD already has.
public sealed class InfoPanel
{
    private const int PanelWidth = 1080;
    private const int PanelHeight = 460;
    private const int HeaderHeight = 40;
    private const int TabButtonSize = 44;
    private const int TabColumnWidth = 64;
    private const int ContentX = TabColumnWidth + 20;
    private const int RowHeight = 24;
    private const int BorderThickness = 2;

    private static readonly (InfoTab Tab, string Label)[] Tabs =
    {
        (InfoTab.Team, "Команда"),
        (InfoTab.Missions, "Миссии"),
        (InfoTab.Reputation, "Репутация"),
        (InfoTab.Ship, "Корабль"),
        (InfoTab.Character, "Персонаж"),
    };

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public InfoPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    // Down the LEFT edge, under the header - matches the reference layout (tabs beside the
    // content they switch, not off on the far side of a table that can run the full panel width).
    public static Rectangle GetTabRect(int index, Vector2 panelOrigin) =>
        new((int)panelOrigin.X + 10, (int)panelOrigin.Y + HeaderHeight + 10 + index * (TabButtonSize + 10), TabButtonSize, TabButtonSize);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int playerId, InfoTab activeTab, Vector2 panelOrigin)
    {
        var panelRect = new Rectangle((int)panelOrigin.X, (int)panelOrigin.Y, PanelWidth, PanelHeight);
        spriteBatch.Draw(_pixel, panelRect, new Color(20, 26, 22) * 0.95f);
        DrawRectOutline(spriteBatch, panelRect, new Color(90, 110, 95), BorderThickness);

        // Header bar, set off from the body by its own fill and a rule under it.
        var headerRect = new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, HeaderHeight);
        spriteBatch.Draw(_pixel, headerRect, new Color(30, 38, 33));
        spriteBatch.Draw(_pixel, new Rectangle(headerRect.X, headerRect.Bottom - BorderThickness, headerRect.Width, BorderThickness), new Color(90, 110, 95));
        spriteBatch.DrawString(_font, "Информация", panelOrigin + new Vector2(16, 10), Color.White, 0f, Vector2.Zero, 0.85f, SpriteEffects.None, 0f);

        // Tab column separated from the content area by its own rule, same idea as the header's.
        spriteBatch.Draw(_pixel, new Rectangle(panelRect.X + TabColumnWidth, panelRect.Y + HeaderHeight, BorderThickness, panelRect.Height - HeaderHeight), new Color(90, 110, 95));

        for (var i = 0; i < Tabs.Length; i++)
        {
            var (tab, label) = Tabs[i];
            var rect = GetTabRect(i, panelOrigin);
            var active = tab == activeTab;
            spriteBatch.Draw(_pixel, rect, active ? new Color(70, 100, 85) : new Color(32, 40, 35));
            DrawTabGlyph(spriteBatch, _pixel, tab, new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f - 4f), active ? Color.White : Color.LightGray);
        }

        var content = panelOrigin + new Vector2(ContentX, HeaderHeight + 20);
        switch (activeTab)
        {
            case InfoTab.Team:
                DrawTeamTab(spriteBatch, snapshot, content);
                break;
            case InfoTab.Missions:
                DrawMissionsTab(spriteBatch, snapshot, content);
                break;
            case InfoTab.Reputation:
                DrawReputationTab(spriteBatch, snapshot, content);
                break;
            case InfoTab.Ship:
                DrawShipTab(spriteBatch, snapshot, content);
                break;
            case InfoTab.Character:
                spriteBatch.DrawString(_font, "Скоро.", content, Color.Gray, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
                break;
        }
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }

    private static void DrawTabGlyph(SpriteBatch spriteBatch, Texture2D pixel, InfoTab tab, Vector2 center, Color color)
    {
        switch (tab)
        {
            case InfoTab.Team:
                HudIcons.DrawCrewGlyph(spriteBatch, pixel, center, 0.9f, color);
                break;
            case InfoTab.Missions:
                HudIcons.DrawFlagGlyph(spriteBatch, pixel, center, 1f, color);
                break;
            case InfoTab.Reputation:
                HudIcons.DrawMedalGlyph(spriteBatch, pixel, center, 1f, color);
                break;
            case InfoTab.Ship:
                HudIcons.DrawShipGlyph(spriteBatch, pixel, center, 1f, color);
                break;
            case InfoTab.Character:
                HudIcons.DrawFingerprintGlyph(spriteBatch, pixel, center, 1f, color);
                break;
        }
    }

    private void DrawTeamTab(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        var columns = new[] { 0f, 60f, 340f, 420f, 520f };
        spriteBatch.DrawString(_font, "Роль", origin + new Vector2(columns[0], 0), Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, "Никнейм", origin + new Vector2(columns[1], 0), Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, "Пинг", origin + new Vector2(columns[2], 0), Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, "Кошелёк", origin + new Vector2(columns[3], 0), Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

        for (var i = 0; i < snapshot.Characters.Count; i++)
        {
            var character = snapshot.Characters[i];
            var row = origin + new Vector2(0, 26 + i * RowHeight);
            var iconCenter = row + new Vector2(columns[0] + 10, 6);
            HudIcons.DrawRoleGlyph(spriteBatch, _pixel, iconCenter, 0.6f, character.IsBot ? Color.LightSkyBlue : Color.White, character.Role);

            var roleLabel = character.IsBot && character.Role is { } role ? CrewRoles.Name(role) : "Игрок";
            spriteBatch.DrawString(_font, roleLabel, row + new Vector2(columns[0] + 24, 0), Color.LightGray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

            var nickname = character.IsBot ? character.BotName ?? "?" : character.Nickname ?? $"Игрок {character.PlayerId}";
            var nameColor = character.Health > 0 ? Color.White : Color.IndianRed;
            spriteBatch.DrawString(_font, nickname, row + new Vector2(columns[1], 0), nameColor, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

            // Shared ship wallet, not a per-player balance (WorldSnapshot.Credits' own doc comment)
            // - every row shows the same figure on purpose, not one column per player's own money.
            var ping = character.IsBot ? "-" : $"{character.PingMs:0} мс";
            spriteBatch.DrawString(_font, ping, row + new Vector2(columns[2], 0), Color.LightGray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, $"{snapshot.Credits} кред.", row + new Vector2(columns[3], 0), Color.LightGreen, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }
    }

    private void DrawMissionsTab(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        if (snapshot.ActiveQuest is not { } quest)
        {
            spriteBatch.DrawString(_font, "Нет активного задания.", origin, Color.Gray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
            return;
        }

        spriteBatch.DrawString(_font, $"Задание: {quest.Describe()}", origin, Color.White, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, $"Награда: {quest.RewardCredits} кред.", origin + new Vector2(0, 26), Color.LightGreen, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        if (quest.Kind == QuestKind.Bounty)
            spriteBatch.DrawString(_font, quest.ObjectiveComplete ? "Цель уничтожена - можно сдавать." : "Цель ещё не уничтожена.",
                origin + new Vector2(0, 50), quest.ObjectiveComplete ? Color.LightGreen : Color.Gray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private void DrawReputationTab(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        for (var i = 0; i < snapshot.FactionStandings.Count; i++)
        {
            var standing = snapshot.FactionStandings[i];
            var label = FactionDefinitions.StandingLabel(standing.Standing);
            var color = standing.Standing >= FactionDefinitions.FriendlyThreshold ? Color.LimeGreen
                : standing.Standing <= FactionDefinitions.HostileThreshold ? Color.OrangeRed
                : Color.LightGray;
            var row = origin + new Vector2(0, i * RowHeight);
            spriteBatch.Draw(_pixel, new Rectangle((int)row.X, (int)row.Y + 2, 10, 10), GalaxyMapPanel.FactionColor(standing.Faction));
            spriteBatch.DrawString(_font, $"{standing.Name}: {label} ({standing.Standing})", row + new Vector2(16, 0),
                color, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        }
    }

    private void DrawShipTab(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        spriteBatch.DrawString(_font, ShipCatalog.Name(snapshot.CurrentShipKind), origin, Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        HudIcons.DrawShipGlyph(spriteBatch, _pixel, origin + new Vector2(120, 60), 3f, Color.LightSteelBlue);

        var upgradesOrigin = origin + new Vector2(0, 120);
        spriteBatch.DrawString(_font, "Модернизации", upgradesOrigin, Color.Gray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        for (var i = 0; i < ShipUpgradeCatalog.Tracks.Count; i++)
        {
            var track = ShipUpgradeCatalog.Tracks[i];
            var level = snapshot.ShipUpgradeLevels.TryGetValue(track.Track, out var lvl) ? lvl : 0;
            var row = upgradesOrigin + new Vector2(0, 24 + i * RowHeight);
            var color = level >= track.MaxLevel ? Color.LightGreen : Color.White;
            spriteBatch.DrawString(_font, $"{track.Name}: {level}/{track.MaxLevel}", row, color, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        }
    }
}
