using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// The ship's one CardTable, free and waiting for a choice (World.CardTable.cs) - a small HUD
// overlay shown only to the crew actually seated there, letting one pick which game to start.
// Direct user request ("чтобы на карточном столе можно было выбирать игры"). Same "no-op unless
// I'm a participant" shape CardGamePanel/FrontsGamePanel already use for their own in-progress
// state.
//
// 2 seated: both Дурак and Фронты on offer (Дурак needs the other player to also pick it -
// World.CardTable.cs's own doc comment; Фронты starts on the first click). Alone: only Фронты,
// against a bot opponent (direct user request, "можно играть в хойку в одиночку") - Дурак
// genuinely needs a second hand, so it isn't offered solo at all.
public sealed class CardTableChoicePanel
{
    public const int PanelWidth = 300;
    public const int PanelHeight = 150;
    private const int ButtonHeight = 44;
    private const int ButtonGap = 10;

    private static readonly (CardTableGameKind Kind, string Label, Color Accent)[] TwoSeatedChoices =
    {
        (CardTableGameKind.Durak, "Дурак переводной", Color.DarkGreen),
        (CardTableGameKind.Fronts, "Фронты", Color.DarkRed),
    };

    private static readonly (CardTableGameKind Kind, string Label, Color Accent)[] SoloChoices =
    {
        (CardTableGameKind.Fronts, "Фронты (против бота)", Color.DarkRed),
    };

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public CardTableChoicePanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    private static (CardTableGameKind Kind, string Label, Color Accent)[] ChoicesFor(int seatedCount) =>
        seatedCount == 1 ? SoloChoices : TwoSeatedChoices;

    // Game1.Input.cs's own hit-test uses this to know which kind a click on button `index` actually
    // picks - the same seated-count-dependent layout Draw below renders, kept in one place so the
    // two can never quietly disagree about which button is which.
    public static CardTableGameKind? GetChoiceKind(int seatedCount, int index)
    {
        var choices = ChoicesFor(seatedCount);
        return index >= 0 && index < choices.Length ? choices[index].Kind : null;
    }

    public static Rectangle GetChoiceButtonRect(int index, Vector2 panelOrigin) =>
        new((int)panelOrigin.X + 20, (int)panelOrigin.Y + 48 + index * (ButtonHeight + ButtonGap), PanelWidth - 40, ButtonHeight);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int localPlayerId, Vector2 origin)
    {
        if (snapshot.CardTableChoiceSeatedIds is not { Count: 1 or 2 } seated || !seated.Contains(localPlayerId))
            return;

        var choices = ChoicesFor(seated.Count);
        var panelRect = new Rectangle((int)origin.X, (int)origin.Y, PanelWidth, PanelHeight + 20);
        spriteBatch.Draw(_pixel, panelRect, new Color(24, 36, 30) * 0.92f);
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, panelRect, Color.SaddleBrown, 3);
        spriteBatch.DrawString(_font, seated.Count == 1 ? "За столом (в одиночку) - выберите игру" : "За столом - выберите игру",
            origin + new Vector2(14, 10), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);

        var iVotedDurak = snapshot.CardTableDurakVotes?.Contains(localPlayerId) ?? false;
        for (var i = 0; i < choices.Length; i++)
        {
            var (kind, label, accent) = choices[i];
            var rect = GetChoiceButtonRect(i, origin);
            var votedForThis = kind == CardTableGameKind.Durak && iVotedDurak;
            spriteBatch.Draw(_pixel, rect, accent * (votedForThis ? 0.9f : 0.65f));
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, votedForThis ? Color.Gold : Color.White * 0.7f, votedForThis ? 3 : 2);
            var textSize = _font.MeasureString(label) * 0.55f;
            spriteBatch.DrawString(_font, label, rect.Location.ToVector2() + new Vector2((rect.Width - textSize.X) / 2f, (rect.Height - textSize.Y) / 2f),
                Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }

        // Дурак needs both seated players to pick it (direct user request - mutual consent, see
        // World.CardTable.cs's own doc comment); Фронты still starts on the first click, so there's
        // never anything to wait on there.
        if (iVotedDurak)
            spriteBatch.DrawString(_font, "Вы выбрали Дурака - ждём второго игрока...",
                origin + new Vector2(14, PanelHeight + 2), Color.LightGoldenrodYellow, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
    }
}
