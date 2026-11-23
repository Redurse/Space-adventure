using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The docked station's power plant (World.StationPower.cs): opened by clicking the reactor, power box, battery or a junction box the author drew
// into the station. A full-screen takeover like the other consoles. Shows the station's reactor, the ship's own reactor and battery side by side
// and one button - plug the ship into the station's reactor ("береговое питание") or pull the plug.
public sealed class StationPowerPanel
{
    private static readonly Color Background = new(12, 16, 22);
    private static readonly Color Accent = new(140, 200, 240);
    private static readonly Color Dim = new(140, 150, 165);
    private static readonly Color Good = new(120, 220, 150);
    private static readonly Color Warn = new(240, 190, 90);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public StationPowerPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    // The one button, centred under the readouts - the same rectangle the click test uses.
    public static Rectangle ButtonRect(Rectangle area) => new(area.Center.X - 220, area.Y + 400, 440, 54);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Rectangle area, Point hoverPoint)
    {
        spriteBatch.Draw(_pixel, area, Background);
        Text(spriteBatch, "ЭЛЕКТРОСЕТЬ СТАНЦИИ", new Vector2(area.Center.X, area.Y + 28), 0.9f, Accent, centered: true);

        var station = snapshot.Station.Power;
        if (station is null)
        {
            Text(spriteBatch, "На этой станции нет собственного реактора", new Vector2(area.Center.X, area.Y + 120), 0.6f, Dim, centered: true);
            return;
        }

        var left = area.Center.X - 330;
        var y = area.Y + 90;
        Row(spriteBatch, left, ref y, "Реактор станции",
            station.Available ? "работает" : "недоступен", station.Available ? Good : Warn);
        Row(spriteBatch, left, ref y, "Может отдать", $"{station.Capacity:0} ед.", Dim);
        Row(spriteBatch, left, ref y, "Береговое питание",
            station.Connected ? "ПОДКЛЮЧЕНО" : "отключено", station.Connected ? Good : Dim);
        Row(spriteBatch, left, ref y, "Станция отдаёт кораблю", $"{station.Delivered:0.0} ед.", station.Delivered > 0.05f ? Good : Dim);
        y += 24;

        var power = snapshot.Power;
        Row(spriteBatch, left, ref y, "Реактор корабля",
            station.Connected ? $"{power.ReactorOutput:0.0} / {power.ReactorMaxOutput:0} (простаивает)" : $"{power.ReactorOutput:0.0} / {power.ReactorMaxOutput:0}",
            station.Connected && power.ReactorOutput < power.ReactorMaxOutput * 0.3f ? Good : Dim);
        Row(spriteBatch, left, ref y, "Топливо реактора", $"{power.ReactorFuel:0} / {power.ReactorMaxFuel:0}", Dim);
        Row(spriteBatch, left, ref y, "Батарея корабля", $"{power.BatteryCharge:0} / {power.BatteryCapacity:0}", Dim);
        Bar(spriteBatch, new Rectangle(left, y, 660, 10), power.BatteryCapacity > 0 ? power.BatteryCharge / power.BatteryCapacity : 0f, Accent);

        var button = ButtonRect(area);
        var enabled = station.Available;
        var hover = enabled && button.Contains(hoverPoint);
        spriteBatch.Draw(_pixel, button, !enabled ? new Color(40, 44, 50) : hover ? new Color(70, 110, 150) : new Color(46, 78, 108));
        Outline(spriteBatch, button, enabled ? Accent : Dim);
        Text(spriteBatch, station.Connected ? "ОТКЛЮЧИТЬ БЕРЕГОВОЕ ПИТАНИЕ" : "ПОДКЛЮЧИТЬ БЕРЕГОВОЕ ПИТАНИЕ",
            new Vector2(button.Center.X, button.Center.Y - 9), 0.6f, enabled ? Color.White : Dim, centered: true);
        Text(spriteBatch, "Esc — закрыть", new Vector2(area.Center.X, area.Bottom - 40), 0.5f, Dim, centered: true);
    }

    private void Row(SpriteBatch spriteBatch, int left, ref int y, string label, string value, Color valueColor)
    {
        Text(spriteBatch, label, new Vector2(left, y), 0.6f, Dim, centered: false);
        Text(spriteBatch, value, new Vector2(left + 330, y), 0.6f, valueColor, centered: false);
        y += 34;
    }

    private void Bar(SpriteBatch spriteBatch, Rectangle rect, float fraction, Color color)
    {
        spriteBatch.Draw(_pixel, rect, new Color(30, 36, 44));
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, (int)(rect.Width * MathHelper.Clamp(fraction, 0f, 1f)), rect.Height), color);
    }

    private void Outline(SpriteBatch spriteBatch, Rectangle rect, Color color)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 2), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 2, rect.Width, 2), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 2, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 2, rect.Y, 2, rect.Height), color);
    }

    private void Text(SpriteBatch spriteBatch, string text, Vector2 position, float scale, Color color, bool centered)
    {
        var size = _font.MeasureString(text) * scale;
        spriteBatch.DrawString(_font, text, centered ? new Vector2(position.X - size.X / 2f, position.Y) : position, color, 0f, Vector2.Zero, scale,
            SpriteEffects.None, 0f);
    }
}
