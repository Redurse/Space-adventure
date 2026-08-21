using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Open-galaxy voyage readout (game_design.md section 5): where the ship is right now, in plain
// language rather than the raw map coordinates.
public sealed class VoyagePanel
{
    private readonly SpriteFont _font;

    public VoyagePanel(SpriteFont font) => _font = font;

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        var voyage = snapshot.Voyage;
        var text = voyage switch
        {
            { DockedPointId: { } dockedId } => $"На станции: {PointName(snapshot, dockedId)}",
            { IsInBattle: true } => "Бой на месте прибытия",
            _ => "В открытом космосе - штурвал на ручном управлении",
        };

        spriteBatch.DrawString(_font, text, origin, Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, $"Кредиты: {snapshot.Credits}", origin + new Vector2(0, 18),
            Color.LightGreen, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    private static string PointName(WorldSnapshot snapshot, string? pointId) =>
        snapshot.GalaxyPoints.FirstOrDefault(p => p.Id == pointId)?.Name ?? "?";
}
