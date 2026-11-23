using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Shown while a shuttle bay is "open" (a click on the hangar): what the shuttle is doing and the buttons that order it - launch it
// while it is home, call it back or stop it while it is out. The warning matters: a launch opens the bay doors, and the room behind
// them is open to space until the shuttle is clear. Where it goes once out is ordered from the helm's map (Captain tab).
public sealed class ShuttleBayPanel
{
    private const int RowHeight = 30;
    private const int ButtonWidth = 118;
    private const int ButtonHeight = 28;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public ShuttleBayPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    private static Vector2 RowOrigin(Vector2 origin, int row) => origin + new Vector2(0, 20 + row * RowHeight);

    public static Rectangle GetLaunchRect(Vector2 origin) => ButtonRect(origin, 3, 0);
    public static Rectangle GetRecallRect(Vector2 origin) => ButtonRect(origin, 3, 0);
    public static Rectangle GetStopRect(Vector2 origin) => ButtonRect(origin, 3, 1);

    private static Rectangle ButtonRect(Vector2 origin, int row, int column)
    {
        var at = RowOrigin(origin, row);
        return new Rectangle((int)at.X + column * (ButtonWidth + 10), (int)at.Y, ButtonWidth, ButtonHeight);
    }

    public void Draw(SpriteBatch spriteBatch, ShuttleState? shuttle, Vector2 origin, float totalSeconds)
    {
        var bounds = DevicePanelChrome.StandardBounds(origin);
        var phosphor = new Color(130, 215, 255);
        DevicePanelChrome.Draw(spriteBatch, _font, bounds, "АНГАР ШАТТЛА", "SH-1", phosphor, totalSeconds);

        if (shuttle is null)
        {
            Text(spriteBatch, "Шаттла нет", RowOrigin(origin, 0), Color.White);
            return;
        }

        var status = shuttle.Phase switch
        {
            ShuttlePhase.Docked => "Шаттл в ангаре",
            ShuttlePhase.Opening => $"Ворота открываются: {MathF.Ceiling(shuttle.OpeningSecondsLeft):0} с",
            ShuttlePhase.Returning => "Шаттл возвращается",
            _ => shuttle.HasDestination ? "Шаттл летит к точке" : "Шаттл снаружи, ждёт приказа",
        };
        Text(spriteBatch, status, RowOrigin(origin, 0), phosphor);
        Text(spriteBatch, $"Корпус: {shuttle.Hp}%", RowOrigin(origin, 1), Color.White * 0.9f);

        var warning = shuttle.Phase switch
        {
            ShuttlePhase.Docked => "Запуск открывает ворота в космос: уйдите из ангара или наденьте скафандр.",
            ShuttlePhase.Opening => "ВОРОТА ОТКРЫВАЮТСЯ - покиньте ангар!",
            _ => "Пока шаттл снаружи, корабль не может стыковаться, садиться и прыгать.",
        };
        var warningColor = shuttle.Phase == ShuttlePhase.Opening ? new Color(255, 120, 100) : new Color(200, 190, 140);
        Text(spriteBatch, warning, RowOrigin(origin, 2), warningColor, 0.44f);

        switch (shuttle.Phase)
        {
            case ShuttlePhase.Docked:
                DrawButton(spriteBatch, GetLaunchRect(origin), "ЗАПУСК", new Color(70, 130, 90));
                break;
            case ShuttlePhase.Flying:
            case ShuttlePhase.Returning:
                DrawButton(spriteBatch, GetRecallRect(origin), "ВЕРНУТЬ", new Color(70, 110, 150));
                DrawButton(spriteBatch, GetStopRect(origin), "СТОП", new Color(130, 90, 70));
                Text(spriteBatch, "Курс: штурвал, вкладка «Капитан», клик по шаттлу и по карте.", RowOrigin(origin, 4) + new Vector2(0f, -6f), Color.White * 0.7f, 0.42f);
                break;
        }
    }

    private void Text(SpriteBatch spriteBatch, string text, Vector2 at, Color color, float scale = 0.5f) =>
        spriteBatch.DrawString(_font, text, at, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

    private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string label, Color fill)
    {
        spriteBatch.Draw(_pixel, rect, fill);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), Color.LightGray);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), Color.LightGray);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), Color.LightGray);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), Color.LightGray);
        var size = _font.MeasureString(label) * 0.55f;
        spriteBatch.DrawString(_font, label, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f), Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }
}
