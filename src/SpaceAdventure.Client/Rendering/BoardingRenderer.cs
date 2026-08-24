using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// The enemy ship's interior while boarding it (game_design.md Phase 3 - "бой отсек за отсеком").
// Same approach as StationRenderer: reuse ShipRenderer's room/door/character drawing rather than
// duplicating the visual language, and add only what's specific here - living defenders with
// health bars.
public sealed class BoardingRenderer
{
    private const int CrewMarkerSize = 22;

    private readonly ShipRenderer _shipRenderer;
    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public BoardingRenderer(ShipRenderer shipRenderer, GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _shipRenderer = shipRenderer;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, float totalSeconds = 0f)
    {
        // Real air, drawn through the same red tint the player's own compartments use: which rooms
        // are already vented is the boarding party's main tactical readout (World.EnemyAtmosphere.cs).
        float Oxygen(string roomId) =>
            snapshot.EnemyShip.RoomOxygen.FirstOrDefault(o => o.RoomId == roomId)?.Oxygen ?? 100f;

        foreach (var room in snapshot.EnemyShip.Rooms)
            _shipRenderer.DrawRoomFloor(spriteBatch, room, Oxygen(room.Id), origin);
        foreach (var room in snapshot.EnemyShip.Rooms)
            _shipRenderer.DrawRoomWalls(spriteBatch, room, Oxygen(room.Id), origin);

        // Doors start closed aboard a hull that has just been boarded, and opening one is how the
        // vacuum gets to the next compartment - so their real state has to show. Destroyed, not just
        // open, gets the player's own ship's flashing-orange treatment (ShipRenderer.DrawDoor) -
        // without it a door chopped to 0 Hp (World.Doors.cs's ChopDoor) silently read as an
        // ordinary open door instead of a wrecked one.
        foreach (var door in snapshot.EnemyShip.Doors)
        {
            var state = snapshot.DoorStates.FirstOrDefault(s => s.DoorId == door.Id);
            _shipRenderer.DrawDoor(spriteBatch, door.Left, door.Top, door.Width, door.Height,
                state?.IsOpen ?? false, origin, destroyed: state?.Destroyed ?? false, totalSeconds: totalSeconds);
        }

        // A cut-through interior wall panel, same black-hole-plus-hazard-stripes treatment the
        // player's own ship's DrawBreachedWallBlock gives one - previously this hull's own
        // WallBlocks/WallBlockStates were never even read here, so a fully-cut interior panel
        // produced no visual at all despite the server-side breach (World.Cutting.cs's
        // CutIndoorAlongFlameOnEnemyShip) being real.
        foreach (var state in snapshot.EnemyShip.WallBlockStates)
        {
            if (!state.Breached)
                continue;
            var block = snapshot.EnemyShip.WallBlocks.FirstOrDefault(b => b.Id == state.Id);
            var room = block is null ? null : snapshot.EnemyShip.Rooms.FirstOrDefault(r => r.Id == block.RoomId);
            if (block is not null && room is not null)
                _shipRenderer.DrawBreachedWallBlock(spriteBatch, block, room, origin, totalSeconds);
        }

        // Locked hatches, not standing-open holes: each only reads as passable once actually cut
        // through (EnemyShipRuntime's own per-hull Hp, World.Cutting.cs), same "destroyed reads as
        // open" convention the player's own airlocks use.
        foreach (var airlock in snapshot.EnemyShip.AirlockOuterDoors)
        {
            var breached = snapshot.EnemyShip.AirlockStates.FirstOrDefault(s => s.Id == airlock.Id)?.Breached ?? false;
            _shipRenderer.DrawDoor(spriteBatch, airlock.Left, airlock.Top, airlock.Width, airlock.Height,
                isOpen: breached, origin, leadsToVacuum: true, destroyed: breached);
        }

        // Which hull this is: the classes differ in how many defenders hold them and whether those
        // defenders can be suffocated, so naming it is naming the plan of attack.
        var firstRoom = snapshot.EnemyShip.Rooms.FirstOrDefault();
        if (firstRoom is not null)
            spriteBatch.DrawString(_font, snapshot.EnemyShip.ClassName,
                origin + new Vector2(firstRoom.X * ShipRenderer.PixelsPerUnit, firstRoom.Y * ShipRenderer.PixelsPerUnit - 34),
                Color.OrangeRed, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);

        foreach (var crew in snapshot.EnemyShip.Crew.Where(c => c.Alive))
            DrawCrew(spriteBatch, crew, origin);

        foreach (var character in snapshot.Characters.Where(c => c.OnEnemyShip))
            _shipRenderer.DrawCharacter(spriteBatch, character, origin);

        foreach (var shot in snapshot.PersonalShots.Where(s => s.Scene == ShotScene.EnemyShip))
            DrawShot(spriteBatch, _pixel, shot, origin);

        // Same muzzle-out-in-front-of-the-body placement and real animation clock the player's own
        // ship gets (ShipRenderer.Draw) - this used to draw straight from the character's own
        // centre with the flame frozen at totalSeconds=0, which read as static/off compared to
        // every other tool flame in the game.
        foreach (var character in snapshot.Characters.Where(c => c.Cutting && c.OnEnemyShip))
        {
            var facing = new Vector2(character.FacingX, character.FacingY);
            var center = origin + new Vector2(character.X, character.Y) * ShipRenderer.PixelsPerUnit;
            var muzzle = ShipRenderer.GetHeldToolMuzzle(ItemType.Cutter, character.Inventory, center, facing)
                ?? center + ShipRenderer.HeldToolOffset(facing);
            FieldRenderer.DrawCuttingFlame(spriteBatch, _pixel, muzzle, facing, totalSeconds);
        }

        foreach (var character in snapshot.Characters.Where(c => c.Welding && c.OnEnemyShip))
        {
            var facing = new Vector2(character.FacingX, character.FacingY);
            var center = origin + new Vector2(character.X, character.Y) * ShipRenderer.PixelsPerUnit;
            var muzzle = ShipRenderer.GetHeldToolMuzzle(ItemType.WeldingTool, character.Inventory, center, facing)
                ?? center + ShipRenderer.HeldToolOffset(facing);
            FieldRenderer.DrawWeldingFlame(spriteBatch, _pixel, muzzle, facing, totalSeconds);
        }
    }

    // A round in flight: a bright head with a short streak behind it, coloured by who fired and
    // what from (World.PersonalShots.cs). internal so the station's renderer draws its firefights
    // exactly the same way.
    internal static void DrawShot(SpriteBatch spriteBatch, Texture2D pixel, PersonalShotState shot, Vector2 origin)
    {
        var center = origin + new Vector2(shot.X, shot.Y) * ShipRenderer.PixelsPerUnit;
        var color = shot.Weapon == ItemType.LaserRifle
            ? (shot.FromEnemy ? Color.MediumPurple : Color.Cyan)
            : (shot.FromEnemy ? Color.OrangeRed : Color.Gold);

        spriteBatch.Draw(pixel, center, null, color * 0.35f, 0f, new Vector2(0.5f, 0.5f), new Vector2(10f, 10f), SpriteEffects.None, 0f);
        spriteBatch.Draw(pixel, center, null, color, 0f, new Vector2(0.5f, 0.5f), new Vector2(5f, 5f), SpriteEffects.None, 0f);
    }

    private void DrawCrew(SpriteBatch spriteBatch, EnemyCrewState crew, Vector2 origin)
    {
        var rect = ShipRenderer.GetBlockRect(new Vec2(crew.X, crew.Y), CrewMarkerSize, origin);
        spriteBatch.Draw(_pixel, rect, Color.DarkRed * 0.9f);

        // Visor-ish inner square, mirroring how ShipRenderer draws a person - reads as a hostile
        // crew member rather than another piece of equipment.
        const int visorSize = 9;
        spriteBatch.Draw(_pixel, new Rectangle(rect.Center.X - visorSize / 2, rect.Center.Y - visorSize / 2, visorSize, visorSize), Color.OrangeRed);

        // Health bar above the head - the only readout that matters while clearing a room.
        const int barWidth = 30;
        const int barHeight = 4;
        var barX = rect.Center.X - barWidth / 2;
        var barY = rect.Y - 10;
        spriteBatch.Draw(_pixel, new Rectangle(barX, barY, barWidth, barHeight), Color.Black * 0.7f);
        var fraction = MathHelper.Clamp(crew.Health / 60f, 0f, 1f); // EnemyCrewRuntime.MaxHealth
        spriteBatch.Draw(_pixel, new Rectangle(barX, barY, (int)(barWidth * fraction), barHeight), Color.Red);

        spriteBatch.DrawString(_font, crew.Name, new Vector2(rect.X - 8, rect.Bottom + 3), Color.LightGray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }
}
