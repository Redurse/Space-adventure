using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// The top bar's "Экипаж" button slides this out from the left edge - every character on the ship
// (live players and hired bots alike), one small row each: role glyph, then nickname. A quicker,
// glance-sized alternative to InfoPanel's fuller Team tab (role/nickname/ping/wallet columns) -
// this one is for "who's aboard right now", not a sit-down roster read.
//
// A fixed picker row under the title lets the local player pick/clear their own CrewRole - purely a
// self-identification label (World.Recruiting.cs's bots are the only Role a job is ever driven by,
// game_design.md section 4), shown the same way a bot's own Role already is: the glyph by their row
// and "(RoleName)" after their nickname.
public sealed class CrewPanel
{
    private const int RowHeight = 26;
    private const int RowWidth = 200;
    private const int IconColumnWidth = 28;
    private const int RoleIconSize = 20;
    private const int RoleIconGap = 2;
    private const int RolePickerY = 26;
    private const int RolePickerIconsX = 40;
    private const int ListTop = RolePickerY + RoleIconSize + 8;

    // "No role" first, then the 5 CrewRoles in their own enum order - a fixed set of options, not
    // tied to any particular crew row, so clicking one doesn't depend on where in the list the
    // player's own row happens to land.
    private static readonly CrewRole?[] RoleOptions = { null, CrewRole.Captain, CrewRole.Engineer, CrewRole.Mechanic, CrewRole.Security, CrewRole.Medic };

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public CrewPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, int localPlayerId)
    {
        var crew = snapshot.Characters;
        var panelHeight = ListTop + crew.Count * RowHeight + 4;
        spriteBatch.Draw(_pixel, new Rectangle((int)origin.X, (int)origin.Y, RowWidth, panelHeight), Color.Black * 0.75f);
        spriteBatch.DrawString(_font, "Экипаж", origin + new Vector2(8, 6), Color.Yellow, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);

        var myRole = crew.FirstOrDefault(c => c.PlayerId == localPlayerId)?.Role;
        spriteBatch.DrawString(_font, "Роль:", origin + new Vector2(6, RolePickerY + 3), Color.LightGray, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        for (var i = 0; i < RoleOptions.Length; i++)
        {
            var rect = GetOwnRoleIconRect(i, origin);
            var selected = myRole == RoleOptions[i];
            spriteBatch.Draw(_pixel, rect, selected ? new Color(120, 92, 30) : Color.DimGray * 0.5f);
            HudIcons.DrawRoleGlyph(spriteBatch, _pixel, RectCenter(rect), 0.4f, selected ? Color.White : Color.LightGray, RoleOptions[i]);
        }

        for (var i = 0; i < crew.Count; i++)
        {
            var character = crew[i];
            var rowOrigin = origin + new Vector2(0, ListTop + i * RowHeight);
            var iconCenter = rowOrigin + new Vector2(IconColumnWidth / 2f, RowHeight / 2f);
            HudIcons.DrawRoleGlyph(spriteBatch, _pixel, iconCenter, 0.7f, character.IsBot ? Color.LightSkyBlue : Color.White, character.Role);

            var name = character.IsBot ? character.BotName ?? "?" : character.Nickname ?? $"Игрок {character.PlayerId}";
            var label = character.Role is { } role ? $"{name} ({CrewRoles.Name(role)})" : name;
            spriteBatch.DrawString(_font, label, rowOrigin + new Vector2(IconColumnWidth + 4, RowHeight / 2f - 7f),
                character.Health > 0 ? Color.White : Color.IndianRed, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }
    }

    private static Vector2 RectCenter(Rectangle rect) => new(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);

    // Shared with Game1.Input.cs's click handler so a click always lands on exactly the icon it
    // looks like it should. index into RoleOptions above - index 0 is the "no role" option.
    public static Rectangle GetOwnRoleIconRect(int index, Vector2 panelOrigin) =>
        new((int)panelOrigin.X + RolePickerIconsX + index * (RoleIconSize + RoleIconGap), (int)panelOrigin.Y + RolePickerY, RoleIconSize, RoleIconSize);

    // Game1.Input.cs looks up which option a click landed on and whether that's "clear" (index 0)
    // or a specific role, without duplicating RoleOptions' own ordering.
    public static CrewRole? RoleAtOption(int index) => RoleOptions[index];
    public const int OptionCount = 6;
}
