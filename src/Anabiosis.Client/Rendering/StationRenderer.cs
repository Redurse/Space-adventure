using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

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

    public static Rectangle GetResidentRect(StationResidentState resident, Vector2 origin) =>
        ShipRenderer.GetBlockRect(new Vec2(resident.X, resident.Y), NpcMarkerSize, origin);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, string? talkingToNpcId, float totalSeconds = 0f,
        IReadOnlyDictionary<string, (string Text, float Alpha)>? residentBubbles = null)
    {
        foreach (var room in snapshot.Station.Rooms)
            _shipRenderer.DrawRoomFloor(spriteBatch, room, oxygen: 100f, origin, StationAccent);
        foreach (var room in snapshot.Station.Rooms)
            _shipRenderer.DrawRoomWalls(spriteBatch, room, oxygen: 100f, origin, StationAccent);

        foreach (var door in snapshot.Station.Doors)
            _shipRenderer.DrawDoor(spriteBatch, door.Left, door.Top, door.Width, door.Height, door.IsVertical, isOpen: true, origin);

        // Same physical door as the ship's own outer airlock - its open/closed state is whatever
        // that door's DoorState already says (World.StationDocking.cs gates both directions on it).
        var connector = snapshot.Station.ShipConnector;
        // Mirrors World.StationDocking.cs's own ResolveShipAirlock - either a real vacuum-facing
        // Door (Door.LeadsToVacuum) or, failing that, a vacuum-facing door edge (direct user report -
        // "но у меня на корабле 2 шлюза": a hull built entirely from Door-tool-onto-open-space
        // airlocks). A ship with neither kind at all (relaxed CustomShipValidator rule) has no
        // matching door state to read - the connector just reads permanently closed rather than crashing.
        var shipAirlockId = snapshot.Doors.FirstOrDefault(d => d.LeadsToVacuum)?.Id
            ?? snapshot.DoorEdges?.FirstOrDefault(e => e.RoomAId is null || e.RoomBId is null)?.Id;
        var shipDoorOpen = shipAirlockId is not null &&
            ((snapshot.DoorStates.FirstOrDefault(s => s.DoorId == shipAirlockId)?.IsOpen)
                ?? snapshot.DoorEdgeStates?.FirstOrDefault(s => s.Id == shipAirlockId)?.IsOpen
                ?? false);
        _shipRenderer.DrawDoor(spriteBatch, connector.Left, connector.Top, connector.Width, connector.Height, connector.IsVertical, shipDoorOpen, origin, leadsToVacuum: true);

        // Unlooted crates only - a taken one leaves nothing behind (World.StationCrime.cs).
        foreach (var crate in snapshot.Station.Crates)
        {
            if (snapshot.Station.CrateStates.FirstOrDefault(s => s.CrateId == crate.Id)?.Looted ?? false)
                continue;
            DrawCrate(spriteBatch, crate, origin);
        }

        foreach (var npc in snapshot.Station.Npcs)
        {
            // A guard shot dead stops being drawn - same convention as cleared enemy crew.
            if (npc.Kind == NpcKind.Security &&
                !(snapshot.Station.Guards.FirstOrDefault(g => g.NpcId == npc.Id)?.Alive ?? true))
                continue;
            DrawNpc(spriteBatch, npc, origin, npc.Id == talkingToNpcId, NearestStationVisitor(snapshot, npc));
        }

        // The people walking about the station (World.StationResidents.cs). A name shows once one of your
        // crew is close enough to be introduced; a line they have just said floats above them.
        foreach (var resident in snapshot.Station.Residents ?? Array.Empty<StationResidentState>())
        {
            var at = new Vec2(resident.X, resident.Y);
            var nearest = NearestStationVisitor(snapshot, at);
            var center = RectCenter(GetResidentRect(resident, origin));
            _shipRenderer.DrawResident(spriteBatch, resident, center, nearest);
            if (nearest is { } visitor && Vector2.Distance(visitor, new Vector2(resident.X, resident.Y)) < 4f)
                spriteBatch.DrawString(_font, resident.Name, new Vector2(center.X - 4 * resident.Name.Length, center.Y + 14), Color.LightGray * 0.9f, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            if (residentBubbles is not null && residentBubbles.TryGetValue(resident.Id, out var bubble))
                _shipRenderer.DrawChatBubble(spriteBatch, bubble.Text, bubble.Alpha, new Vector2(center.X, center.Y - 24));
        }

        _shipRenderer.DrawDroppedItems(spriteBatch, snapshot.DroppedItems, snapshot.Station.Rooms.Select(r => r.Id), origin, totalSeconds);

        foreach (var character in snapshot.Characters.Where(c => c.OnStation))
            _shipRenderer.DrawCharacter(spriteBatch, character, origin);

        // Shooting it out with station security uses the same travelling rounds as boarding does.
        foreach (var beam in snapshot.LaserBeams?.Where(b => b.Scene == ShotScene.Station) ?? Enumerable.Empty<LaserBeamState>())
            BoardingRenderer.DrawLaserBeam(spriteBatch, _pixel, beam, origin);

        foreach (var shot in snapshot.PersonalShots.Where(s => s.Scene == ShotScene.Station))
            BoardingRenderer.DrawShot(spriteBatch, _pixel, shot, origin);

        foreach (var character in snapshot.Characters.Where(c => c.Cutting && c.OnStation))
            FieldRenderer.DrawCuttingFlame(spriteBatch, _pixel,
                origin + new Vector2((float)character.X, (float)character.Y) * ShipRenderer.PixelsPerUnit,
                new Vector2(character.FacingX, character.FacingY), totalSeconds);

        foreach (var character in snapshot.Characters.Where(c => c.Welding && c.OnStation))
            FieldRenderer.DrawWeldingFlame(spriteBatch, _pixel,
                origin + new Vector2((float)character.X, (float)character.Y) * ShipRenderer.PixelsPerUnit,
                new Vector2(character.FacingX, character.FacingY), totalSeconds);
    }

    private static Color NpcColor(NpcKind kind) => kind switch
    {
        NpcKind.Administrator => Color.SteelBlue,
        NpcKind.Trader => Color.Goldenrod,
        NpcKind.Mechanic => Color.DarkOliveGreen,
        NpcKind.Shipwright => Color.MediumPurple,
        NpcKind.Security => Color.Firebrick,
        NpcKind.Scientist => Color.LightSeaGreen,
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
        // What is inside, by name, so a crate on the deck is readable without walking up to it.
        ItemIcons.DrawNameplate(spriteBatch, _pixel, _font, ItemIcons.WorldLabel(crate.Item),
            new Vector2(rect.Center.X, rect.Bottom + 3), ItemIcons.CategoryColor(crate.Item));
    }

    // Whichever of your crew is closest to a resident - who they turn to look at while standing still.
    private static Vector2? NearestStationVisitor(WorldSnapshot snapshot, StationNpc npc) =>
        NearestStationVisitor(snapshot, npc.Position);

    private static Vector2? NearestStationVisitor(WorldSnapshot snapshot, Vec2 from)
    {
        Vector2? best = null;
        var bestDistance = float.MaxValue;
        foreach (var character in snapshot.Characters.Where(c => c.OnStation && c.Health > 0f))
        {
            var at = new Vector2((float)character.X, (float)character.Y);
            var distance = Vector2.DistanceSquared(at, new Vector2((float)from.X, (float)from.Y));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = at;
            }
        }
        return best;
    }

    private void DrawNpc(SpriteBatch spriteBatch, StationNpc npc, Vector2 origin, bool talkingTo, Vector2? lookToward)
    {
        var rect = GetNpcRect(npc, origin);
        var color = NpcColor(npc.Kind);
        var center = RectCenter(rect);
        _shipRenderer.DrawStationResident(spriteBatch, npc, center, lookToward);
        // The role icon that used to BE the marker now floats over the head, so a trader is still a trader
        // at a glance.
        var badge = new Rectangle((int)center.X - 9, (int)center.Y - 38, 18, 18);
        HudIcons.FillCircle(spriteBatch, _pixel, RectCenter(badge), 11f, Color.Black * 0.45f);
        DrawNpcGlyph(spriteBatch, npc.Kind, badge, color);
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
