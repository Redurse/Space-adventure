using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// A hand of Дурак переводной in progress at the ship's CardTable (World.CardGame.cs) - a HUD
// overlay on top of the physical scene, exactly like StationPanel's dialogue box: drawn whenever
// the local player happens to be one of the 2 participants, no-opping internally otherwise. There
// is no click to open it and nothing to gate - the server starts the hand the moment 2 crew stand
// at the table, and this panel simply follows that state.
//
// Both hands travel to every client in full (CardGameState's own doc comment - the same
// no-hidden-state trust model this whole project already uses elsewhere); this panel is just
// courteous enough to only ever draw the LOCAL player's own hand face-up, rendering the opponent's
// as card backs sized to their count.
public sealed class CardGamePanel
{
    public const int CardWidth = 34;
    public const int CardHeight = 48;
    private const int CardGap = 6;
    public const int PanelWidth = 640;
    public const int PanelHeight = 340;

    private static readonly Color Felt = new(18, 70, 40);
    private static readonly Color CardFace = new(238, 232, 214);
    private static readonly Color CardBack = new(52, 58, 74);
    private static readonly Color RedSuit = new(178, 40, 40);
    private static readonly Color BlackSuit = new(35, 35, 40);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public CardGamePanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    // index runs left-to-right starting at 0 - CardGamePanel.Draw always lays the local player's
    // own hand out in that same order, so a click here always lands on the card it looks like it
    // should.
    public static Rectangle GetOwnHandCardRect(int index, int handCount, Vector2 panelOrigin)
    {
        var totalWidth = handCount * CardWidth + (handCount - 1) * CardGap;
        var x = (int)panelOrigin.X + (PanelWidth - totalWidth) / 2 + index * (CardWidth + CardGap);
        var y = (int)panelOrigin.Y + PanelHeight - CardHeight - 14;
        return new Rectangle(x, y, CardWidth, CardHeight);
    }

    public static Rectangle GetTakeButtonRect(Vector2 panelOrigin) =>
        new((int)panelOrigin.X + PanelWidth - 100, (int)panelOrigin.Y + PanelHeight / 2 - 16, 88, 32);

    public static Rectangle GetEndRoundButtonRect(Vector2 panelOrigin) =>
        new((int)panelOrigin.X + PanelWidth - 100, (int)panelOrigin.Y + PanelHeight / 2 + 20, 88, 32);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int localPlayerId, Vector2 origin)
    {
        if (snapshot.CardGame is not { } game || (game.Player1Id != localPlayerId && game.Player2Id != localPlayerId))
            return;

        var myHand = game.Player1Id == localPlayerId ? game.Player1Hand : game.Player2Hand;
        var opponentHandCount = (game.Player1Id == localPlayerId ? game.Player2Hand : game.Player1Hand).Count;

        var panelRect = new Rectangle((int)origin.X, (int)origin.Y, PanelWidth, PanelHeight);
        spriteBatch.Draw(_pixel, panelRect, Felt);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, panelRect, Color.SaddleBrown, 3);

        spriteBatch.DrawString(_font, "Дурак переводной", origin + new Vector2(14, 8), Color.White, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        if (game.Finished)
        {
            var message = game.WinnerId is null ? "Ничья - колода закончилась одновременно"
                : game.WinnerId == localPlayerId ? "Вы победили!" : "Вы - дурак!";
            spriteBatch.DrawString(_font, message, origin + new Vector2(14, 34), Color.Gold, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            return;
        }

        var isAttacker = game.AttackerId == localPlayerId;
        var status = isAttacker
            ? (game.PendingAttacks.Count == 0 ? "Ваш ход - атакуйте или нажмите «Бито»" : "Ждём защиты соперника")
            : (game.PendingAttacks.Count == 0 ? "Ход соперника" : "Ваш ход - защищайтесь, переведите или возьмите");
        spriteBatch.DrawString(_font, status, origin + new Vector2(14, 34), Color.LightGoldenrodYellow, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

        // Trump + deck, top-right - the one card that's always shown face-up regardless of hands.
        var trumpRect = new Rectangle((int)origin.X + PanelWidth - CardWidth - 14, (int)origin.Y + 8, CardWidth, CardHeight);
        DrawCard(spriteBatch, trumpRect, game.TrumpCard);
        spriteBatch.DrawString(_font, $"Козырь. В колоде: {game.DeckCount}", trumpRect.Location.ToVector2() + new Vector2(-140, CardHeight + 2),
            Color.LightGray, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

        // Opponent's hand, face down - only their count is real information here.
        var opponentY = (int)origin.Y + 60;
        var opponentTotalWidth = opponentHandCount * (CardWidth / 2 + 2);
        var opponentStartX = (int)origin.X + (PanelWidth - opponentTotalWidth) / 2;
        for (var i = 0; i < opponentHandCount; i++)
            spriteBatch.Draw(_pixel, new Rectangle(opponentStartX + i * (CardWidth / 2 + 2), opponentY, CardWidth / 2, CardHeight / 2), CardBack);

        // The table: already-beaten pairs first, then whatever's still pending on the defender.
        var tableY = (int)origin.Y + PanelHeight / 2 - CardHeight / 2 - 20;
        var tableX = (int)origin.X + 40;
        foreach (var pair in game.ResolvedPairs)
        {
            DrawCard(spriteBatch, new Rectangle(tableX, tableY, CardWidth, CardHeight), pair.Attack);
            DrawCard(spriteBatch, new Rectangle(tableX + 10, tableY + 12, CardWidth, CardHeight), pair.Defense);
            tableX += CardWidth + 22;
        }
        foreach (var pending in game.PendingAttacks)
        {
            DrawCard(spriteBatch, new Rectangle(tableX, tableY, CardWidth, CardHeight), pending);
            tableX += CardWidth + 14;
        }

        if (!isAttacker && game.PendingAttacks.Count > 0)
        {
            var takeRect = GetTakeButtonRect(origin);
            ShipRenderer.DrawPanel(spriteBatch, _pixel, takeRect, Color.DarkRed * 0.8f, Color.OrangeRed, 2);
            spriteBatch.DrawString(_font, "Взять", takeRect.Location.ToVector2() + new Vector2(18, 8), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }
        if (isAttacker && game.PendingAttacks.Count == 0 && game.ResolvedPairs.Count > 0)
        {
            var endRect = GetEndRoundButtonRect(origin);
            ShipRenderer.DrawPanel(spriteBatch, _pixel, endRect, Color.DarkGreen * 0.8f, Color.LimeGreen, 2);
            spriteBatch.DrawString(_font, "Бито", endRect.Location.ToVector2() + new Vector2(22, 8), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }

        // Own hand, face up and clickable (Game1.Input.cs's own hit-test uses the exact same rects).
        for (var i = 0; i < myHand.Count; i++)
            DrawCard(spriteBatch, GetOwnHandCardRect(i, myHand.Count, origin), myHand[i]);
    }

    private void DrawCard(SpriteBatch spriteBatch, Rectangle rect, PlayingCard card)
    {
        spriteBatch.Draw(_pixel, rect, CardFace);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, Color.Black * 0.6f, 1);
        var suitColor = card.Suit is CardSuit.Hearts or CardSuit.Diamonds ? RedSuit : BlackSuit;
        spriteBatch.DrawString(_font, RankLabel(card.Rank), rect.Location.ToVector2() + new Vector2(4, 2), suitColor, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        DrawSuitGlyph(spriteBatch, card.Suit, rect.Location.ToVector2() + new Vector2(rect.Width / 2f, rect.Height - 14), suitColor);
    }

    private static string RankLabel(int rank) => rank switch
    {
        11 => "В",
        12 => "Д",
        13 => "К",
        14 => "А",
        _ => rank.ToString(),
    };

    // Four small, unmistakably distinct vector shapes rather than reaching for a ♠♥♦♣ glyph the
    // project's one baked SpriteFont almost certainly never rasterized - same "everything is drawn
    // from the one white pixel" rule as the rest of this client.
    private void DrawSuitGlyph(SpriteBatch spriteBatch, CardSuit suit, Vector2 center, Color color)
    {
        switch (suit)
        {
            case CardSuit.Hearts:
                HudIcons.FillCircle(spriteBatch, _pixel, center, 5f, color);
                break;
            case CardSuit.Diamonds:
                Primitives.FillPolygon(spriteBatch, _pixel, center, new[]
                {
                    center + new Vector2(0, -6), center + new Vector2(5, 0), center + new Vector2(0, 6), center + new Vector2(-5, 0),
                }, color);
                break;
            case CardSuit.Clubs:
                Primitives.FillTriangle(spriteBatch, _pixel, center + new Vector2(0, -6), center + new Vector2(6, 6), center + new Vector2(-6, 6), color);
                break;
            default: // Spades
                Primitives.FillTriangle(spriteBatch, _pixel, center + new Vector2(0, 6), center + new Vector2(6, -6), center + new Vector2(-6, -6), color);
                break;
        }
    }
}
