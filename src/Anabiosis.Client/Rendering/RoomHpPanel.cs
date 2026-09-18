using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Direct user request ("хочу добавить чтобы у каждого отсека было скрытое число хп... чтобы хп
// отсека можно было увидеть в терминале управления кораблем") - one row per compartment, same
// list/row shape EngineerDevicePanel already uses for its own devices, just keyed by Room instead.
// A separate, dedicated helm tab (HelmTab.Compartments) rather than folded into EngineerDevicePanel
// itself - per-room and per-device are different lists (a room's own hidden pool isn't any one
// device's own repair state), and the user asked for a standalone panel.
public sealed class RoomHpPanel
{
    public const int Width = 340;
    private const int HeaderHeight = 26;
    private const int RowHeight = 24;
    private const int RowGap = 2;
    private const int BarWidth = 120;

    public static Rectangle GetRowRect(int index, Vector2 origin) =>
        new((int)origin.X, (int)origin.Y + HeaderHeight + index * (RowHeight + RowGap), Width, RowHeight);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public RoomHpPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        var rooms = snapshot.Rooms;
        var hpByRoomId = new Dictionary<string, RoomHpState>();
        if (snapshot.RoomHp is { } states)
            foreach (var state in states)
                hpByRoomId[state.RoomId] = state;

        spriteBatch.DrawString(_font, $"Отсеки ({rooms.Count})", origin, Color.LightGray, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);

        for (var i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            var rect = GetRowRect(i, origin);
            var hasState = hpByRoomId.TryGetValue(room.Id, out var state);
            var hp = hasState ? state!.Hp : 0f;
            var maxHp = hasState ? state!.MaxHp : 1f;
            var fraction = maxHp > 0f ? MathHelper.Clamp(hp / maxHp, 0f, 1f) : 0f;

            spriteBatch.Draw(_pixel, rect, new Color(35, 38, 42) * 0.9f);
            spriteBatch.DrawString(_font, room.Name, new Vector2(rect.X + 6, rect.Y + 5), Color.White, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

            // A plain, low-key bar - this number is a "hidden" test stat (direct user request), not
            // a HUD-critical readout, so it reads as background telemetry rather than a health bar
            // in the ordinary FPS-game sense.
            var barRect = new Rectangle(rect.Right - BarWidth - 6, rect.Y + 6, BarWidth, RowHeight - 12);
            spriteBatch.Draw(_pixel, barRect, new Color(20, 22, 25));
            var fillWidth = (int)(barRect.Width * fraction);
            if (fillWidth > 0)
            {
                var fillColor = fraction > 0.5f ? new Color(90, 160, 90) : fraction > 0.2f ? new Color(200, 160, 60) : new Color(190, 70, 60);
                spriteBatch.Draw(_pixel, new Rectangle(barRect.X, barRect.Y, fillWidth, barRect.Height), fillColor);
            }

            var label = $"{hp:0}/{maxHp:0}";
            var labelSize = _font.MeasureString(label) * 0.36f;
            spriteBatch.DrawString(_font, label,
                new Vector2(barRect.Center.X - labelSize.X / 2f, barRect.Center.Y - labelSize.Y / 2f),
                Color.White, 0f, Vector2.Zero, 0.36f, SpriteEffects.None, 0f);
        }
    }
}
