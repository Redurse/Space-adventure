using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Which instrument overlay is currently drawn over the hull silhouette - one tab at a time, same
// "worst thing wins" data ShipStatusPanel already read, just as a picture instead of a row of text.
public enum ShipSchematicCategory
{
    Hull,
    Power,
    Oxygen,
    Crew,
    Weapons,
}

// One hit from the search box (game_design.md/M47 follow-up - "поиск предметов на корабле"), a
// name plus wherever it actually is right now - a shelf, the floor, a locker, or someone's own
// hands. Position is null only for a piece of shipboard equipment (an ammo crate's own supply, a
// suit locker) that isn't a single point-pickable item to walk to.
public readonly record struct ItemSearchResult(string Name, string Location, Vec2? Position);

// Window 3 of the helm redesign (M47 follow-up): a Barotrauma-style captain's status board - the
// hull's own silhouette with a few instrument overlays a click apart, plus a way to find a
// specific item anywhere aboard without walking the whole ship looking for it. Deliberately reads
// snapshot data only (WallBlockStates, RoomOxygen, Power, Characters, Turrets, storage) rather
// than touching ShipRenderer/TileTextures/HullSkin - this is its own flat top-down diagram, not
// the textured room rendering those files own.
public sealed class ShipSchematicPanel
{
    public const int Width = 380;
    public const int Height = 460;
    private const int SilhouetteTop = 30;
    private const int SilhouetteHeight = 210;
    private const int IconSize = 38;
    private const int IconGap = 8;
    private const int IconsTop = SilhouetteTop + SilhouetteHeight + 12;
    private const int SearchBoxTop = IconsTop + IconSize + 12;
    private const int SearchBoxHeight = 22;
    private const int ResultsTop = SearchBoxTop + SearchBoxHeight + 8;

    private static readonly (ShipSchematicCategory Category, string Label, string Icon)[] Categories =
    {
        (ShipSchematicCategory.Hull, "Корпус", "К"),
        (ShipSchematicCategory.Power, "Питание", "П"),
        (ShipSchematicCategory.Oxygen, "Кислород", "O2"),
        (ShipSchematicCategory.Crew, "Экипаж", "Э"),
        (ShipSchematicCategory.Weapons, "Оружие", "В"),
    };

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public ShipSchematicPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetCategoryIconRect(int index, Vector2 origin) =>
        new((int)origin.X + 14 + index * (IconSize + IconGap), (int)origin.Y + IconsTop, IconSize, IconSize);

    public static Rectangle GetSearchBoxRect(Vector2 origin) =>
        new((int)origin.X + 14, (int)origin.Y + SearchBoxTop, Width - 28, SearchBoxHeight);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, ShipSchematicCategory category,
        string searchQuery, bool searchFocused)
    {
        var housing = new Rectangle((int)origin.X, (int)origin.Y, Width, Height);
        spriteBatch.Draw(_pixel, housing, new Color(14, 18, 24) * 0.95f);
        DrawRectOutline(spriteBatch, housing, new Color(70, 90, 100));
        spriteBatch.DrawString(_font, "Системы корабля", origin + new Vector2(12, 6), Color.LightGray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        var silhouetteArea = new Rectangle((int)origin.X + 8, (int)origin.Y + SilhouetteTop, Width - 16, SilhouetteHeight);
        DrawSilhouette(spriteBatch, snapshot, silhouetteArea, category);

        DrawCategoryIcons(spriteBatch, origin, category);
        DrawSearchBox(spriteBatch, origin, searchQuery, searchFocused);

        var results = string.IsNullOrWhiteSpace(searchQuery) ? null : Search(snapshot, searchQuery);
        if (results is not null)
            DrawSearchResults(spriteBatch, origin, results);
        else
            DrawCategorySummary(spriteBatch, snapshot, origin, category);
    }

    private void DrawCategoryIcons(SpriteBatch spriteBatch, Vector2 origin, ShipSchematicCategory selected)
    {
        for (var i = 0; i < Categories.Length; i++)
        {
            var (cat, label, icon) = Categories[i];
            var rect = GetCategoryIconRect(i, origin);
            spriteBatch.Draw(_pixel, rect, cat == selected ? new Color(70, 110, 130) : new Color(35, 40, 48));
            DrawRectOutline(spriteBatch, rect, cat == selected ? Color.SkyBlue : new Color(60, 66, 74));
            var iconSize = _font.MeasureString(icon) * 0.6f;
            spriteBatch.DrawString(_font, icon, new Vector2(rect.Center.X - iconSize.X / 2, rect.Y + 4), Color.White, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            var labelSize = _font.MeasureString(label) * 0.4f;
            spriteBatch.DrawString(_font, label, new Vector2(rect.Center.X - labelSize.X / 2, rect.Bottom - 15), Color.LightGray, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
        }
    }

    private void DrawSearchBox(SpriteBatch spriteBatch, Vector2 origin, string query, bool focused)
    {
        var rect = GetSearchBoxRect(origin);
        spriteBatch.Draw(_pixel, rect, focused ? new Color(40, 50, 58) : new Color(28, 32, 38));
        DrawRectOutline(spriteBatch, rect, focused ? Color.SkyBlue : new Color(60, 66, 74));
        var shown = string.IsNullOrEmpty(query) ? "Поиск предмета на корабле..." : query;
        var color = string.IsNullOrEmpty(query) ? Color.Gray : Color.White;
        spriteBatch.DrawString(_font, shown, new Vector2(rect.X + 6, rect.Y + 4), color, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        if (focused)
        {
            var caretX = rect.X + 6 + _font.MeasureString(query).X * 0.5f;
            spriteBatch.Draw(_pixel, new Rectangle((int)caretX + 2, rect.Y + 3, 1, rect.Height - 6), Color.White);
        }
    }

    private void DrawSearchResults(SpriteBatch spriteBatch, Vector2 origin, IReadOnlyList<ItemSearchResult> results)
    {
        if (results.Count == 0)
        {
            spriteBatch.DrawString(_font, "Ничего не найдено", origin + new Vector2(14, ResultsTop), Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return;
        }

        var row = 0;
        foreach (var result in results.Take(8))
        {
            var rowOrigin = origin + new Vector2(14, ResultsTop + row * 18);
            spriteBatch.DrawString(_font, result.Name, rowOrigin, Color.White, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, result.Location, rowOrigin + new Vector2(200, 0), Color.LightSteelBlue, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            row++;
        }
    }

    private void DrawCategorySummary(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, ShipSchematicCategory category)
    {
        var text = category switch
        {
            ShipSchematicCategory.Hull => $"Пробоин: {snapshot.WallBlockStates.Count(s => s.Breached)}",
            ShipSchematicCategory.Power => $"Реактор: {snapshot.Power.ReactorOutput:0}/{snapshot.Power.ReactorMaxOutput:0}  Батарея: {snapshot.Power.BatteryCharge:0}/{snapshot.Power.BatteryCapacity:0}  Щиты: {snapshot.Shield.Points:0}/{snapshot.Shield.MaxPoints:0}",
            ShipSchematicCategory.Oxygen => $"Мин. O2 в отсеке: {(snapshot.RoomOxygen.Count > 0 ? snapshot.RoomOxygen.Min(o => o.Oxygen) : 0):0}",
            ShipSchematicCategory.Crew => $"Экипаж: {snapshot.Characters.Count}",
            _ => $"Орудий: {snapshot.Turrets.Count}",
        };
        spriteBatch.DrawString(_font, text, origin + new Vector2(14, ResultsTop), Color.LightGray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
    }

    // Flat, unrotated top-down diagram - Barotrauma's own captain's board never turns with the sub
    // either, it's a fixed reference plan, not a live exterior view (M47 follow-up - window 1 is
    // the one that behaves like a moving camera, this one deliberately doesn't).
    private void DrawSilhouette(SpriteBatch spriteBatch, WorldSnapshot snapshot, Rectangle area, ShipSchematicCategory category)
    {
        spriteBatch.Draw(_pixel, area, new Color(8, 10, 14));

        if (snapshot.Rooms.Count == 0)
            return;

        var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
        var halfExtents = ShipLocalFrame.GetHullHalfExtents(snapshot.Rooms);
        var scale = halfExtents.X > 0f && halfExtents.Y > 0f
            ? (float)(Math.Min(area.Width / (2f * halfExtents.X), area.Height / (2f * halfExtents.Y)) * 0.92f)
            : 1f;
        var areaCenter = new Vector2(area.Center.X, area.Center.Y);

        Vector2 ToPanel(Vec2 local) => areaCenter + new Vector2((float)(local.X - hullCenter.X), (float)(local.Y - hullCenter.Y)) * scale;

        foreach (var room in snapshot.Rooms)
        {
            var topLeft = ToPanel(new Vec2(room.Left, room.Top));
            var size = new Vector2(room.Width, room.Height) * scale;
            var rect = new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)size.X, (int)size.Y);
            spriteBatch.Draw(_pixel, rect, RoomFillColor(snapshot, room, category));
            DrawRectOutline(spriteBatch, rect, new Color(90, 100, 110));
        }

        switch (category)
        {
            case ShipSchematicCategory.Crew:
                DrawCrewOverlay(spriteBatch, snapshot, ToPanel);
                break;
            case ShipSchematicCategory.Weapons:
                DrawWeaponsOverlay(spriteBatch, snapshot, ToPanel, scale);
                break;
        }
    }

    private Color RoomFillColor(WorldSnapshot snapshot, Room room, ShipSchematicCategory category)
    {
        switch (category)
        {
            case ShipSchematicCategory.Hull:
            {
                var blocksHere = snapshot.WallBlocks.Where(b => b.RoomId == room.Id).ToList();
                if (blocksHere.Count == 0)
                    return new Color(40, 50, 60);
                var breached = blocksHere.Count(b => snapshot.WallBlockStates.FirstOrDefault(s => s.Id == b.Id)?.Breached ?? false);
                var fraction = (float)breached / blocksHere.Count;
                return Color.Lerp(new Color(40, 70, 45), new Color(150, 40, 30), fraction);
            }
            case ShipSchematicCategory.Power:
            {
                var devicesHere = snapshot.SystemDevices.Where(d => d.RoomId == room.Id).ToList();
                if (devicesHere.Count == 0)
                    return new Color(40, 50, 60);
                var anyDamaged = devicesHere.Any(d => snapshot.SystemStates.FirstOrDefault(s => s.DeviceId == d.Id)?.Damaged ?? false);
                return anyDamaged ? new Color(120, 90, 30) : new Color(35, 70, 90);
            }
            case ShipSchematicCategory.Oxygen:
            {
                var oxygen = snapshot.RoomOxygen.FirstOrDefault(o => o.RoomId == room.Id)?.Oxygen;
                if (oxygen is null)
                    return new Color(40, 50, 60);
                var fraction = MathHelper.Clamp(oxygen.Value / 100f, 0f, 1f);
                return Color.Lerp(new Color(140, 50, 30), new Color(35, 80, 70), fraction);
            }
            default:
                return new Color(40, 50, 60);
        }
    }

    private void DrawCrewOverlay(SpriteBatch spriteBatch, WorldSnapshot snapshot, Func<Vec2, Vector2> toPanel)
    {
        foreach (var character in snapshot.Characters)
        {
            if (character.OnStation || character.OnEnemyShip || character.IsOutside)
                continue; // only aboard the ship's own hull reads on this diagram

            var screen = toPanel(new Vec2(character.X, character.Y));
            var healthFraction = MathHelper.Clamp(character.Health / 100f, 0f, 1f);
            var color = healthFraction > 0.66f ? Color.LimeGreen : healthFraction > 0.33f ? Color.Orange : Color.OrangeRed;
            HudIcons.FillCircle(spriteBatch, _pixel, screen, 6f, color * 0.9f);
            if (character.IsBleeding)
                HudIcons.DrawRingArc(spriteBatch, _pixel, screen, 9f, 0f, 360f, Color.Red, 12, 1.5f);

            var initial = character.Role is { } role ? CrewRoles.Name(role)[..1] : "И";
            var size = _font.MeasureString(initial) * 0.4f;
            spriteBatch.DrawString(_font, initial, screen - size / 2f, Color.Black, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
        }
    }

    private void DrawWeaponsOverlay(SpriteBatch spriteBatch, WorldSnapshot snapshot, Func<Vec2, Vector2> toPanel, float scale)
    {
        foreach (var turret in snapshot.Turrets)
        {
            var screen = toPanel(new Vec2(turret.PeriscopeX, turret.PeriscopeY));
            var state = snapshot.TurretStates.FirstOrDefault(s => s.Id == turret.Id);
            var ready = state is not null && !state.Damaged;
            HudIcons.FillCircle(spriteBatch, _pixel, screen, 7f, (ready ? Color.SteelBlue : Color.DimGray) * 0.9f);
            HudIcons.DrawRingArc(spriteBatch, _pixel, screen, 10f, 0f, 360f, ready ? Color.LightSkyBlue : Color.Gray, 12, 1.5f);
        }

        foreach (var storage in snapshot.AmmoStorages)
        {
            var state = snapshot.AmmoStorageStates.FirstOrDefault(s => s.StorageId == storage.Id);
            var screen = toPanel(new Vec2(storage.X, storage.Y));
            var rect = new Rectangle((int)screen.X - 4, (int)screen.Y - 4, 8, 8);
            spriteBatch.Draw(_pixel, rect, state is { Remaining: > 0 } ? Color.Goldenrod : new Color(60, 60, 60));
        }
    }

    // Scans everywhere a physical item can currently be (game_design.md/M47 follow-up), the same
    // set of places DroppedItem/StorageRack/SuitLocker/AmmoStorage/Inventory already cover
    // elsewhere in the client - nothing new is tracked just for this search.
    public static IReadOnlyList<ItemSearchResult> Search(WorldSnapshot snapshot, string query)
    {
        var results = new List<ItemSearchResult>();
        var q = query.Trim();
        if (q.Length == 0)
            return results;

        bool Matches(string name) => name.Contains(q, StringComparison.OrdinalIgnoreCase);
        string RoomName(string roomId) => snapshot.Rooms.FirstOrDefault(r => r.Id == roomId)?.Name ?? roomId;

        for (var i = 0; i < snapshot.RackSlots.Count; i++)
        {
            if (snapshot.RackSlots[i] is not { } item)
                continue;
            var name = ItemDefinitions.DisplayName(item);
            if (!Matches(name))
                continue;
            var rackIndex = i / StorageRack.Capacity;
            if (rackIndex >= snapshot.StorageRacks.Count)
                continue;
            var rack = snapshot.StorageRacks[rackIndex];
            results.Add(new ItemSearchResult(name, RoomName(rack.RoomId), new Vec2(rack.X, rack.Y)));
        }

        foreach (var dropped in snapshot.DroppedItems)
        {
            var name = ItemDefinitions.DisplayName(dropped.Item);
            if (!Matches(name))
                continue;
            results.Add(new ItemSearchResult(name, dropped.RoomId is { } roomId ? RoomName(roomId) : "снаружи", dropped.Position));
        }

        if (Matches(ItemDefinitions.DisplayName(ItemType.Spacesuit)))
        {
            foreach (var locker in snapshot.SuitLockers)
            {
                var state = snapshot.SuitLockerStates.FirstOrDefault(s => s.LockerId == locker.Id);
                if (state?.HasSuit != true)
                    continue;
                results.Add(new ItemSearchResult(ItemDefinitions.DisplayName(ItemType.Spacesuit), RoomName(locker.RoomId), new Vec2(locker.X, locker.Y)));
            }
        }

        if (Matches(ItemDefinitions.DisplayName(ItemType.AmmoCrate)))
        {
            foreach (var storage in snapshot.AmmoStorages)
            {
                var state = snapshot.AmmoStorageStates.FirstOrDefault(s => s.StorageId == storage.Id);
                if (state is null || state.Remaining <= 0)
                    continue;
                results.Add(new ItemSearchResult($"{ItemDefinitions.DisplayName(ItemType.AmmoCrate)} ({state.Remaining})", RoomName(storage.RoomId), new Vec2(storage.X, storage.Y)));
            }
        }

        foreach (var character in snapshot.Characters)
        {
            if (character.Inventory is not { } inventory)
                continue;
            var who = character.BotName ?? character.Nickname ?? $"игрок {character.PlayerId}";

            foreach (var slot in inventory.MainSlots)
            {
                if (slot is not { } item)
                    continue;
                var name = ItemDefinitions.DisplayName(item);
                if (Matches(name))
                    results.Add(new ItemSearchResult(name, $"у {who}", new Vec2(character.X, character.Y)));
            }

            foreach (var equipped in inventory.Equipped.Values)
            {
                if (equipped is not { } item)
                    continue;
                var name = ItemDefinitions.DisplayName(item);
                if (Matches(name))
                    results.Add(new ItemSearchResult(name, $"надето, у {who}", new Vec2(character.X, character.Y)));
            }
        }

        return results;
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness = 1)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
