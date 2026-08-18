using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Draws the station's own physical rooms/doors/NPCs (game_design.md section 10 - "тоже
// модульные, как корабль, можно ходить по ним пешком после стыковки") through the exact same
// visual language as the ship's interior, reusing ShipRenderer's room/door/character drawing
// (made internal there for this purpose) instead of duplicating it. Station rooms have no
// atmosphere simulation (Station.cs's doc comment) - oxygen is always drawn full, doors are
// always open except the one connector back to the ship, which follows the ship's own outer
// airlock door state.
public sealed partial class StationRenderer
{
    public const int NpcMarkerSize = 22;

    // A warm rose-gold rather than any of RoomDecor's own ship accents (cockpit blue, armory red,
    // reactor orange, shields teal, life-support green, cargo tan) - crossing the connector should
    // read as "somewhere else" the instant the deck markings and wall lamps change colour, not
    // just because the room happens to have different furniture in it.
    private static readonly Color StationAccent = new(214, 150, 130);

    private readonly ShipRenderer _shipRenderer;
    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public StationRenderer(ShipRenderer shipRenderer, GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _shipRenderer = shipRenderer;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetNpcRect(StationNpc npc, Vector2 origin) =>
        ShipRenderer.GetBlockRect(npc.Position, NpcMarkerSize, origin);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, string? talkingToNpcId, float totalSeconds = 0f)
    {
        foreach (var room in snapshot.StationRooms)
            _shipRenderer.DrawRoomFloor(spriteBatch, room, oxygen: 100f, origin, StationAccent);
        foreach (var room in snapshot.StationRooms)
            _shipRenderer.DrawRoomWalls(spriteBatch, room, oxygen: 100f, origin, StationAccent);

        foreach (var door in snapshot.StationDoors)
            _shipRenderer.DrawDoor(spriteBatch, door.Left, door.Top, door.Width, door.Height, isOpen: true, origin);

        // Same physical door as the ship's own outer airlock - its open/closed state is whatever
        // that door's DoorState already says (World.StationDocking.cs gates both directions on it).
        var connector = snapshot.StationShipConnector;
        var shipDoorOpen = snapshot.DoorStates.FirstOrDefault(s => s.DoorId == snapshot.AirlockOuterDoors.First().Id)?.IsOpen ?? false;
        _shipRenderer.DrawDoor(spriteBatch, connector.Left, connector.Top, connector.Width, connector.Height, shipDoorOpen, origin, leadsToVacuum: true);

        // Unlooted crates only - a taken one leaves nothing behind (World.StationCrime.cs).
        foreach (var crate in snapshot.StationCrates)
        {
            if (snapshot.StationCrateStates.FirstOrDefault(s => s.CrateId == crate.Id)?.Looted ?? false)
                continue;
            DrawCrate(spriteBatch, crate, origin);
        }

        foreach (var npc in snapshot.StationNpcs)
        {
            // A guard shot dead stops being drawn - same convention as cleared enemy crew.
            if (npc.Kind == NpcKind.Security &&
                !(snapshot.StationGuards.FirstOrDefault(g => g.NpcId == npc.Id)?.Alive ?? true))
                continue;
            DrawNpc(spriteBatch, npc, origin, npc.Id == talkingToNpcId);
        }

        _shipRenderer.DrawDroppedItems(spriteBatch, snapshot.DroppedItems, snapshot.StationRooms.Select(r => r.Id), origin, totalSeconds);

        foreach (var character in snapshot.Characters.Where(c => c.OnStation))
            _shipRenderer.DrawCharacter(spriteBatch, character, origin);

        // Shooting it out with station security uses the same travelling rounds as boarding does.
        foreach (var shot in snapshot.PersonalShots.Where(s => s.Scene == ShotScene.Station))
            BoardingRenderer.DrawShot(spriteBatch, _pixel, shot, origin);

        foreach (var character in snapshot.Characters.Where(c => c.Cutting && c.OnStation))
            FieldRenderer.DrawCuttingFlame(spriteBatch, _pixel,
                origin + new Vector2(character.X, character.Y) * ShipRenderer.PixelsPerUnit,
                new Vector2(character.FacingX, character.FacingY), 0f);

        foreach (var character in snapshot.Characters.Where(c => c.Welding && c.OnStation))
            FieldRenderer.DrawWeldingFlame(spriteBatch, _pixel,
                origin + new Vector2(character.X, character.Y) * ShipRenderer.PixelsPerUnit,
                new Vector2(character.FacingX, character.FacingY), 0f);
    }

    private static Color NpcColor(NpcKind kind) => kind switch
    {
        NpcKind.Administrator => Color.SteelBlue,
        NpcKind.Trader => Color.Goldenrod,
        NpcKind.Mechanic => Color.DarkOliveGreen,
        NpcKind.Shipwright => Color.MediumPurple,
        NpcKind.Security => Color.Firebrick,
        _ => Color.Gray,
    };

    // Station property standing out in the open (game_design.md section 10) - a proper latched
    // container now (ShipRenderer's own beveled-panel-plus-rivets look, the same one every power
    // block uses) with the actual item's own icon sitting on the lid, rather than a flat brown
    // square and a text abbreviation.
    private void DrawCrate(SpriteBatch spriteBatch, StationCrate crate, Vector2 origin)
    {
        const int size = 20;
        var rect = ShipRenderer.GetBlockRect(crate.Position, size, origin);
        ShipRenderer.DrawPanel(spriteBatch, _pixel, rect, new Color(96, 68, 46), new Color(150, 110, 74), 2);
        var iconRect = new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, rect.Height - 6);
        if (ItemIcons.HasIcon(crate.Item))
            ItemIcons.Draw(spriteBatch, _pixel, crate.Item, iconRect);
        else
            spriteBatch.DrawString(_font, ItemDefinitions.ShortLabel(crate.Item), new Vector2(rect.Right + 3, rect.Y),
                Color.Khaki, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    private void DrawNpc(SpriteBatch spriteBatch, StationNpc npc, Vector2 origin, bool talkingTo)
    {
        var rect = GetNpcRect(npc, origin);
        var color = NpcColor(npc.Kind);
        HudIcons.FillCircle(spriteBatch, _pixel, RectCenter(rect), rect.Width / 2f, color * 0.35f);
        DrawNpcGlyph(spriteBatch, npc.Kind, rect, color);
        if (talkingTo)
        {
            const int margin = 3;
            spriteBatch.Draw(_pixel, new Rectangle(rect.X - margin, rect.Y - margin, rect.Width + margin * 2, 2), Color.White);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X - margin, rect.Bottom + margin - 2, rect.Width + margin * 2, 2), Color.White);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X - margin, rect.Y - margin, 2, rect.Height + margin * 2), Color.White);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right + margin - 2, rect.Y - margin, 2, rect.Height + margin * 2), Color.White);
        }
        spriteBatch.DrawString(_font, npc.Name, new Vector2(rect.X - 10, rect.Bottom + 4), Color.LightGray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    private static Vector2 RectCenter(Rectangle rect) => new(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
}
