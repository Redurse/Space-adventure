using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// The captain's status monitor, shown beside the helm: one row per compartment with its air, its
// breaches and the state of whatever systems live in it. Everything on it is already visible
// somewhere else - by walking there and looking - which is exactly the point: the pilot is stuck at
// the helm mid-manoeuvre and needs to know which end of the ship is losing air without leaving the
// console.
public sealed class ShipStatusPanel
{
    private const int RowHeight = 22;
    private const int Width = 430;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public ShipStatusPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin)
    {
        DrawShieldBar(spriteBatch, snapshot.Shield, origin);

        var compartmentsOrigin = origin + new Vector2(0, ShieldBarHeight + 10);
        spriteBatch.DrawString(_font, "Отсеки корабля", compartmentsOrigin, Color.Yellow, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        var top = (int)compartmentsOrigin.Y + 24;
        spriteBatch.Draw(_pixel, new Rectangle((int)origin.X, top, Width, snapshot.Rooms.Count * RowHeight + 6), new Color(18, 22, 28) * 0.9f);

        for (var i = 0; i < snapshot.Rooms.Count; i++)
            DrawRow(spriteBatch, snapshot, snapshot.Rooms[i], new Vector2(origin.X + 6, top + 4 + i * RowHeight));
    }

    // Ship-wide shield bar (game_design.md section 1) — absorbs enemy attacks before they land on
    // compartments; only drains/regrows from power routed to the Shields system. Moved here from
    // the old bottom-left combat corner - the pilot at the helm is exactly who needs to see it
    // continuously (it's the one number that tells them whether to keep dodging or can afford to
    // hold a line), and nobody else needs it cluttering their own screen.
    private const float ShieldBarWidth = 220f;
    private const float ShieldBarHeight = 14f;

    private void DrawShieldBar(SpriteBatch spriteBatch, ShieldState shield, Vector2 origin)
    {
        spriteBatch.Draw(_pixel, new Rectangle((int)origin.X, (int)origin.Y, (int)ShieldBarWidth, (int)ShieldBarHeight), Color.DimGray);
        var ratio = shield.MaxPoints > 0 ? MathHelper.Clamp(shield.Points / shield.MaxPoints, 0f, 1f) : 0f;
        spriteBatch.Draw(_pixel, new Rectangle((int)origin.X, (int)origin.Y, (int)(ShieldBarWidth * ratio), (int)ShieldBarHeight), Color.SkyBlue);
        spriteBatch.DrawString(_font, $"Щиты: {shield.Points:0}/{shield.MaxPoints:0}", origin + new Vector2(4, -1), Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private void DrawRow(SpriteBatch spriteBatch, WorldSnapshot snapshot, Room room, Vector2 rowOrigin)
    {
        var oxygen = snapshot.RoomOxygen.FirstOrDefault(o => o.RoomId == room.Id)?.Oxygen ?? 0f;
        var breaches = snapshot.WallBlockStates.Count(s =>
            s.Breached && snapshot.WallBlocks.FirstOrDefault(b => b.Id == s.Id)?.RoomId == room.Id);
        var brokenSystems = snapshot.SystemDevices
            .Where(d => d.RoomId == room.Id)
            .Count(d => snapshot.SystemStates.FirstOrDefault(s => s.DeviceId == d.Id)?.Damaged ?? false);

        // Worst thing wins the colour: a hole in the hull outranks thin air, which outranks a dead
        // box, because that's the order the crew has to deal with them in.
        var (status, color) = breaches > 0 ? ($"ПРОБОИН: {breaches}", Color.OrangeRed)
            : oxygen < 50f ? ("НИЗКИЙ O2", Color.Orange)
            : brokenSystems > 0 ? ($"ОТКАЗ: {brokenSystems}", Color.Gold)
            : ("норма", Color.LimeGreen);

        spriteBatch.DrawString(_font, room.Name, rowOrigin, Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        // Air as a bar as well as a number - a row of bars reads as "which compartment is emptying"
        // at a glance, which a column of percentages does not.
        var barRect = new Rectangle((int)rowOrigin.X + 210, (int)rowOrigin.Y + 3, 90, 9);
        spriteBatch.Draw(_pixel, barRect, new Color(45, 50, 58));
        spriteBatch.Draw(_pixel, new Rectangle(barRect.X, barRect.Y, (int)(barRect.Width * MathHelper.Clamp(oxygen / 100f, 0f, 1f)), barRect.Height),
            oxygen < 50f ? Color.Orange : Color.MediumSeaGreen);
        spriteBatch.DrawString(_font, $"{oxygen:0}", new Vector2(barRect.Right + 6, rowOrigin.Y), Color.LightGray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        spriteBatch.DrawString(_font, status, new Vector2(rowOrigin.X + 340, rowOrigin.Y), color, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }
}
