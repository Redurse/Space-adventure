using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// "Фронты" in progress at the ship's CardTable (World.FrontsGame.cs) - a HUD overlay exactly like
// CardGamePanel's own sibling, drawn only for the 2 participants. Direct user request
// ("повтори игру hearts of iron 4"): a simplified wargame keeping just HOI4's core shape - 3
// independent fronts, a shared reinforcement pool per turn, push a front to either end to capture
// it for good - agreed with the user as the right level of abstraction for a card-table minigame.
//
// Both sides' current allocations travel to every client in full (FrontsGameState's own doc
// comment - the same no-hidden-state trust model CardGameState already established for the
// table's other game) - "Провести бой" just locks in whatever is currently showing for both, there
// is nothing secret to protect by hiding the opponent's numbers in the meantime.
public sealed class FrontsGamePanel
{
    public const int PanelWidth = 460;
    public const int PanelHeight = 280;
    private const int RowHeight = 70;
    private const int RowStartY = 56;
    private const int BarWidth = 280;
    private const int BarHeight = 14;

    private static readonly string[] FrontNames = { "Северный фронт", "Центральный фронт", "Южный фронт" };

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public FrontsGamePanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    private static Vector2 RowOrigin(int index, Vector2 panelOrigin) => panelOrigin + new Vector2(16, RowStartY + index * RowHeight);

    public static Rectangle GetMinusButtonRect(int frontIndex, Vector2 panelOrigin)
    {
        var row = RowOrigin(frontIndex, panelOrigin);
        return new Rectangle((int)row.X + BarWidth + 16, (int)row.Y + 18, 28, 28);
    }

    public static Rectangle GetPlusButtonRect(int frontIndex, Vector2 panelOrigin)
    {
        var row = RowOrigin(frontIndex, panelOrigin);
        return new Rectangle((int)row.X + BarWidth + 50, (int)row.Y + 18, 28, 28);
    }

    public static Rectangle GetResolveButtonRect(Vector2 panelOrigin) =>
        new((int)panelOrigin.X + PanelWidth / 2 - 74, (int)panelOrigin.Y + PanelHeight - 38, 148, 32);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int localPlayerId, Vector2 origin)
    {
        if (snapshot.FrontsGame is not { } game || (game.PlayerAId != localPlayerId && game.PlayerBId != localPlayerId))
            return;

        var isPlayerA = game.PlayerAId == localPlayerId;
        var myAllocation = isPlayerA ? game.AllocationA : game.AllocationB;
        var theirAllocation = isPlayerA ? game.AllocationB : game.AllocationA;
        var myUsed = 0;
        foreach (var a in myAllocation)
            myUsed += a;

        var panelRect = new Rectangle((int)origin.X, (int)origin.Y, PanelWidth, PanelHeight);
        spriteBatch.Draw(_pixel, panelRect, new Color(28, 24, 20) * 0.92f);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, panelRect, Color.SaddleBrown, 3);
        var title = game.VsBot ? $"Фронты (против бота) - ход {game.Turn}/{game.TurnCap}" : $"Фронты - ход {game.Turn}/{game.TurnCap}";
        spriteBatch.DrawString(_font, title, origin + new Vector2(14, 8),
            Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, $"Резерв: {game.ArmyPool - myUsed}/{game.ArmyPool}", origin + new Vector2(PanelWidth - 150, 8),
            Color.LightGoldenrodYellow, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

        if (game.Finished)
        {
            var message = game.WinnerId is null ? "Ничья - силы истощены с обеих сторон"
                : game.WinnerId == localPlayerId ? "Вы выиграли кампанию!" : "Кампания проиграна.";
            spriteBatch.DrawString(_font, message, origin + new Vector2(14, 30), Color.Gold, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }

        for (var i = 0; i < FrontNames.Length; i++)
        {
            var row = RowOrigin(i, origin);
            spriteBatch.DrawString(_font, FrontNames[i], row, Color.LightGray, 0f, Vector2.Zero, 0.48f, SpriteEffects.None, 0f);

            var barOrigin = row + new Vector2(0, 18);
            spriteBatch.Draw(_pixel, new Rectangle((int)barOrigin.X, (int)barOrigin.Y, BarWidth, BarHeight), Color.DimGray);

            var progress = game.FrontProgress[i];
            var center = barOrigin.X + BarWidth / 2f;
            var half = BarWidth / 2f;
            var fillColor = progress >= 0 ? Color.SteelBlue : Color.IndianRed;
            if (progress >= 0)
            {
                var width = (int)(half * progress / FrontsGameRange);
                spriteBatch.Draw(_pixel, new Rectangle((int)center, (int)barOrigin.Y, width, BarHeight), fillColor);
            }
            else
            {
                var width = (int)(half * -progress / FrontsGameRange);
                spriteBatch.Draw(_pixel, new Rectangle((int)center - width, (int)barOrigin.Y, width, BarHeight), fillColor);
            }
            spriteBatch.Draw(_pixel, new Rectangle((int)center - 1, (int)barOrigin.Y - 3, 2, BarHeight + 6), Color.White * 0.5f);
            if (game.Captured[i])
                spriteBatch.DrawString(_font, "ЗАХВАЧЕН", barOrigin + new Vector2(BarWidth + 92, -1), Color.Orange, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);

            var interactive = !game.Finished && !game.Captured[i];
            if (interactive)
            {
                var minusRect = GetMinusButtonRect(i, origin);
                var plusRect = GetPlusButtonRect(i, origin);
                ShipRenderer.DrawPanel(spriteBatch, _pixel, minusRect, Color.DarkRed * 0.7f, Color.White, 1);
                spriteBatch.DrawString(_font, "-", minusRect.Location.ToVector2() + new Vector2(10, 3), Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
                ShipRenderer.DrawPanel(spriteBatch, _pixel, plusRect, Color.DarkGreen * 0.7f, Color.White, 1);
                spriteBatch.DrawString(_font, "+", plusRect.Location.ToVector2() + new Vector2(8, 3), Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            }
            spriteBatch.DrawString(_font, $"Я: {myAllocation[i]}   {(game.VsBot ? "Бот" : "Противник")}: {theirAllocation[i]}", row + new Vector2(0, 42),
                Color.LightSteelBlue, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
        }

        if (!game.Finished)
        {
            var resolveRect = GetResolveButtonRect(origin);
            ShipRenderer.DrawPanel(spriteBatch, _pixel, resolveRect, Color.DarkGreen * 0.8f, Color.LimeGreen, 2);
            spriteBatch.DrawString(_font, "Провести бой", resolveRect.Location.ToVector2() + new Vector2(16, 8), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }
    }

    private const int FrontsGameRange = 5;
}
