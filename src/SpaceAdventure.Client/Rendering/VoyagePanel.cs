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
        var text = voyage.Phase switch
        {
            VoyagePhase.Station => $"На станции: {PointName(snapshot, voyage.DockedPointId)}",
            VoyagePhase.Battle => "Бой на месте прибытия",
            VoyagePhase.Traveling when voyage.TravelTargetPointId is not null =>
                $"Курс на {PointName(snapshot, voyage.TravelTargetPointId)}... {DistanceRemaining(snapshot):0} ед.",
            _ => "В открытом космосе - выберите курс на навигационной консоли",
        };

        spriteBatch.DrawString(_font, text, origin, Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, $"Кредиты: {snapshot.Credits}", origin + new Vector2(0, 18),
            Color.LightGreen, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }

    private static string PointName(WorldSnapshot snapshot, string? pointId) =>
        snapshot.GalaxyPoints.FirstOrDefault(p => p.Id == pointId)?.Name ?? "?";

    private static float DistanceRemaining(WorldSnapshot snapshot)
    {
        var target = snapshot.GalaxyPoints.FirstOrDefault(p => p.Id == snapshot.Voyage.TravelTargetPointId);
        return target is null ? 0f : (target.Position - snapshot.Voyage.ShipMapPosition).Length();
    }
}
