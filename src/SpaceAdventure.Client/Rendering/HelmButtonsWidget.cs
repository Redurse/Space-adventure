using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Window 2 of the helm redesign (M47 follow-up - "окно 2 - кнопки. стыковка, рсу, камеру"): a
// small floating widget the player can drag anywhere out of the way of window 1's own near-
// fullscreen schematic, carrying exactly the three buttons asked for. Replaces the button stack
// HelmPanel used to draw fixed in its own corner - Stabilize stays a keyboard-only control (S),
// since it wasn't one of the three named here.
public sealed class HelmButtonsWidget
{
    public static readonly Point Size = new(170, 132);
    public const int TitleBarHeight = 16;

    private static readonly Rectangle DockButtonRectLocal = new(10, 24, 150, 30);
    private static readonly Rectangle ControlModeButtonRectLocal = new(10, 60, 150, 30);
    private static readonly Rectangle CamerasButtonRectLocal = new(10, 96, 150, 30);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public HelmButtonsWidget(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    // The drag handle (Game1's own UpdateHelmWidgetDrag) - only this strip picks the widget up, so
    // a press meant for one of the buttons just underneath it never gets stolen into a drag.
    public static Rectangle GetTitleBarRect(Vector2 origin) => new((int)origin.X, (int)origin.Y, Size.X, TitleBarHeight);

    public static Rectangle GetDockButtonRect(Vector2 origin) => Offset(DockButtonRectLocal, origin);
    public static Rectangle GetControlModeButtonRect(Vector2 origin) => Offset(ControlModeButtonRectLocal, origin);
    public static Rectangle GetCamerasButtonRect(Vector2 origin) => Offset(CamerasButtonRectLocal, origin);

    private static Rectangle Offset(Rectangle rect, Vector2 origin) =>
        new((int)origin.X + rect.X, (int)origin.Y + rect.Y, rect.Width, rect.Height);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, bool camerasPowered, bool camerasActive)
    {
        var housing = new Rectangle((int)origin.X, (int)origin.Y, Size.X, Size.Y);
        spriteBatch.Draw(_pixel, housing, new Color(20, 24, 30) * 0.92f);
        spriteBatch.Draw(_pixel, GetTitleBarRect(origin), new Color(45, 52, 60));
        spriteBatch.DrawString(_font, "Управление", origin + new Vector2(6, 1), Color.LightGray, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);

        DrawDockButton(spriteBatch, snapshot, origin);
        DrawControlModeButton(spriteBatch, snapshot.ShipField, origin);
        DrawCamerasButton(spriteBatch, origin, camerasPowered, camerasActive);
    }

    // Same slot either direction, same reasoning HelmPanel's own version had (World.StationDocking.cs's
    // HandleDockButtonPressed) - dock while approaching, cast off once already docked.
    private void DrawDockButton(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        var rect = GetDockButtonRect(origin);

        if (snapshot.Voyage.DockedPointId is not null)
        {
            spriteBatch.Draw(_pixel, rect, Color.SeaGreen);
            spriteBatch.DrawString(_font, "[Клик] ОТСТЫКОВАТЬСЯ", new Vector2(rect.X + 6, rect.Y + 9), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return;
        }

        if (!snapshot.Voyage.HasNearbyStation)
        {
            spriteBatch.Draw(_pixel, rect, new Color(40, 40, 40));
            spriteBatch.DrawString(_font, "Стыковка", new Vector2(rect.X + 6, rect.Y + 9), Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return;
        }

        if (snapshot.CanDock)
        {
            spriteBatch.Draw(_pixel, rect, Color.SeaGreen);
            spriteBatch.DrawString(_font, "[Клик] СТЫКОВКА", new Vector2(rect.X + 6, rect.Y + 9), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return;
        }

        var toBerth = new Vector2(snapshot.DockBerthPosition.X - snapshot.ShipField.X, snapshot.DockBerthPosition.Y - snapshot.ShipField.Y);
        spriteBatch.Draw(_pixel, rect, new Color(50, 50, 50));
        spriteBatch.DrawString(_font, $"До причала: {toBerth.Length():0}", new Vector2(rect.X + 6, rect.Y + 9), Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    // Click toggles the same Arc/Rcs mode the Z key does (World.ShipField.cs, M41) - a mouse-only
    // way to reach the button the widget was specifically asked to carry.
    private void DrawControlModeButton(SpriteBatch spriteBatch, ShipFieldState shipField, Vector2 origin)
    {
        var rect = GetControlModeButtonRect(origin);
        var arc = shipField.ControlMode == ShipControlMode.Arc;
        spriteBatch.Draw(_pixel, rect, arc ? new Color(50, 90, 120) : new Color(120, 80, 30));
        spriteBatch.DrawString(_font, arc ? "РСУ: ВИРАЖ" : "РСУ: СВОБОДНОЕ", new Vector2(rect.X + 6, rect.Y + 9), Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private void DrawCamerasButton(SpriteBatch spriteBatch, Vector2 origin, bool powered, bool active)
    {
        var rect = GetCamerasButtonRect(origin);
        var color = !powered ? new Color(40, 40, 40) : active ? Color.SteelBlue : new Color(60, 60, 60);
        spriteBatch.Draw(_pixel, rect, color);
        spriteBatch.DrawString(_font, "Камеры", new Vector2(rect.X + 6, rect.Y + 9), powered ? Color.White : Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }
}
