using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Shown while the airlock console is open (game_design.md section 10): the station's NPCs as
// clickable icons. Real transactions/quests land in later milestones (M10 economy, M11 cargo
// quests) — for now clicking an NPC just shows what they'd offer.
public sealed class StationPanel
{
    public const float PixelsPerUnit = 6f;
    public const int NpcMarkerSize = 24;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public StationPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetNpcRect(StationNpc npc, Vector2 panelOrigin)
    {
        var center = panelOrigin + new Vector2(npc.X, npc.Y) * PixelsPerUnit;
        return new Rectangle((int)center.X - NpcMarkerSize / 2, (int)center.Y - NpcMarkerSize / 2, NpcMarkerSize, NpcMarkerSize);
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, string? talkingToNpcId)
    {
        spriteBatch.DrawString(_font, "Станция - клик по человеку, чтобы поговорить", panelOrigin + new Vector2(0, -24),
            Color.Yellow, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        foreach (var npc in snapshot.StationNpcs)
        {
            var rect = GetNpcRect(npc, panelOrigin);
            var isTalking = npc.Id == talkingToNpcId;
            var color = npc.Kind == NpcKind.Administrator ? Color.SteelBlue : Color.Goldenrod;

            spriteBatch.Draw(_pixel, rect, color);
            if (isTalking)
                DrawRectOutline(spriteBatch, new Rectangle(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6), Color.White, 2);

            spriteBatch.DrawString(_font, npc.Name, new Vector2(rect.X - 20, rect.Bottom + 4),
                Color.LightGray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }

        if (talkingToNpcId is null)
            return;

        var talkingTo = snapshot.StationNpcs.FirstOrDefault(n => n.Id == talkingToNpcId);
        if (talkingTo is null)
            return;

        var dialogueOrigin = panelOrigin + new Vector2(0, 140);
        var line = talkingTo.Kind switch
        {
            NpcKind.Administrator => "\"Заданий для тебя пока нет - загляни попозже.\"",
            NpcKind.Trader => "\"Торговать пока нечем - склад пуст.\"",
            _ => "...",
        };
        spriteBatch.DrawString(_font, $"{talkingTo.Name}:", dialogueOrigin, Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, line, dialogueOrigin + new Vector2(0, 20), Color.LightGray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
