using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// The end of chapter two (World.CoreHunt.cs) as a banner laid over the whole screen for a while: the core destroyed and the virus
// dead, or the mother ship lost. It does not take the controls - the game goes on under it, since the war does. It shows only
// when the campaign is seen to reach an end during this session, never for a save that was already past it when loaded.
public partial class Game1
{
    private const double EndingBannerSeconds = 16.0;
    private const double EndingFadeInSeconds = 1.6;
    private const double EndingFadeOutSeconds = 3.0;

    private CampaignStage? _endingLastStage;
    private CampaignStage? _endingShown;
    private DateTime _endingStart;

    private static bool IsEnding(CampaignStage stage) => stage is CampaignStage.CoreDestroyed or CampaignStage.ArtifactLost;

    // Looks at the stage every frame; a step into an ending starts the banner.
    private void UpdateEndingBanner(WorldSnapshot? snapshot)
    {
        if (snapshot?.CampaignChapter is not { } stage)
            return;
        if (_endingLastStage is { } last && last != stage && IsEnding(stage))
        {
            _endingShown = stage;
            _endingStart = DateTime.UtcNow;
        }
        _endingLastStage = stage;
    }

    private void DrawEndingBanner()
    {
        UpdateEndingBanner(_client.LatestSnapshot);
        if (_endingShown is not { } stage)
            return;
        var elapsed = (DateTime.UtcNow - _endingStart).TotalSeconds;
        if (elapsed >= EndingBannerSeconds)
        {
            _endingShown = null;
            return;
        }
        var alpha = (float)Math.Min(Math.Min(1.0, elapsed / EndingFadeInSeconds), Math.Min(1.0, (EndingBannerSeconds - elapsed) / EndingFadeOutSeconds));

        var won = stage == CampaignStage.CoreDestroyed;
        var title = won ? "ЯДРО УНИЧТОЖЕНО" : "МАТКА УТРАЧЕНА";
        var lines = won
            ? new[]
            {
                "Артефакт рассыпался, и восстание угасло везде разом:",
                "корабли замерли, захваченные колонии затихли. Без огня и взрывов.",
                "Связь с родной галактикой восстановлена. Союз отстраивается.",
                "Вы успели. Вы - герои.",
            }
            : new[]
            {
                "Колониальная матка разбита, артефакт теперь у восстания.",
                "Остановить его больше нечем. Союз обречён.",
                "Но война ещё не окончена, пока жив хоть один корабль.",
            };
        var accent = won ? new Color(255, 214, 120) : new Color(255, 96, 80);

        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, DesignWidth, DesignHeight), new Color(4, 8, 16) * (0.78f * alpha));
        var titleSize = _font.MeasureString(title) * 1.5f;
        var y = DesignHeight / 2f - 90f;
        _spriteBatch.DrawString(_font, title, new Vector2((DesignWidth - titleSize.X) / 2f + 2f, y + 2f), Color.Black * alpha, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);
        _spriteBatch.DrawString(_font, title, new Vector2((DesignWidth - titleSize.X) / 2f, y), accent * alpha, 0f, Vector2.Zero, 1.5f, SpriteEffects.None, 0f);
        y += titleSize.Y + 18f;
        _spriteBatch.Draw(_pixel, new Rectangle((int)(DesignWidth / 2f - 140f), (int)y, 280, 2), accent * (0.7f * alpha));
        y += 16f;
        foreach (var line in lines)
        {
            var size = _font.MeasureString(line) * 0.7f;
            _spriteBatch.DrawString(_font, line, new Vector2((DesignWidth - size.X) / 2f, y), Color.White * alpha, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
            y += size.Y + 6f;
        }
    }
}
