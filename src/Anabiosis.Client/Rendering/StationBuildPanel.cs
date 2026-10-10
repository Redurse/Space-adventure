using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

public enum BuildTool { Compartments, Doors, Demolish }

// The Shipwright's build catalog, shown at the bottom of the screen: a row of tabs - one per compartment category that has
// compartments in the catalog, then "Двери" and "Снос" - above a row of icons for whichever tab is selected. Compartments come from
// the compartment catalog (the same one the Ship Editor places from); picking one enters placement mode (Game1.ShipBuilding.cs).
// "Двери" offers the three door widths; "Снос" is a tool: click a compartment on the ship to take it down for half its price back.
public sealed class StationBuildPanel
{
    public const int PanelWidth = 1120;
    public const int PanelHeight = 128;
    private const int TabSize = 34;
    private const int TabGap = 6;
    private const int ModuleSize = 68;
    private const int ModuleGap = 10;
    private const int TabRowY = 8;
    private const int ModuleRowY = TabRowY + TabSize + 12;

    private static readonly (RoomCategory Category, string Label)[] AllCategories =
    {
        (RoomCategory.Structural, "Корпус"),
        (RoomCategory.Power, "Питание"),
        (RoomCategory.Propulsion, "Двигатели"),
        (RoomCategory.Crew, "Экипаж"),
        (RoomCategory.Weapons, "Оружие"),
        (RoomCategory.Shields, "Щиты"),
        (RoomCategory.Sensors, "Сенсоры"),
    };

    // Only the categories the catalog actually has something in.
    public static readonly (RoomCategory Category, string Label)[] Categories =
        AllCategories.Where(c => CompartmentCatalog.Entries.Any(e => CompartmentPricing.CategoryOf(e.Type) == c.Category)).ToArray();

    // Tab order: the categories, then doors, then demolish.
    public static int TabCount => Categories.Length + 2;
    public static int DoorsTabIndex => Categories.Length;
    public static int DemolishTabIndex => Categories.Length + 1;

    private static Color CategoryColor(RoomCategory category) => category switch
    {
        RoomCategory.Power => new Color(214, 148, 62),
        RoomCategory.Propulsion => new Color(224, 120, 60),
        RoomCategory.Crew => new Color(196, 168, 132),
        RoomCategory.Weapons => new Color(190, 96, 84),
        RoomCategory.Shields => new Color(88, 190, 186),
        RoomCategory.Sensors => new Color(150, 190, 210),
        _ => new Color(126, 138, 156), // Structural
    };

    private static readonly Color DoorColor = new(120, 170, 120);
    private static readonly Color DemolishColor = new(200, 90, 80);

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public StationBuildPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetTabRect(int index, Vector2 panelOrigin) =>
        new((int)panelOrigin.X + 10 + index * (TabSize + TabGap), (int)panelOrigin.Y + TabRowY, TabSize, TabSize);

    public static IReadOnlyList<CompartmentCatalogEntry> EntriesInCategory(RoomCategory category) =>
        CompartmentCatalog.Entries.Where(e => CompartmentPricing.CategoryOf(e.Type) == category).ToList();

    public static Rectangle GetModuleRect(int index, Vector2 panelOrigin) =>
        new((int)panelOrigin.X + 10 + index * (ModuleSize + ModuleGap), (int)panelOrigin.Y + ModuleRowY, ModuleSize, ModuleSize);

    public static Rectangle PanelRect(Vector2 panelOrigin) => new((int)panelOrigin.X, (int)panelOrigin.Y, PanelWidth, PanelHeight);

    // What a compartment's tooltip lists besides its size and price: the machines it carries.
    private static IEnumerable<string> Contents(CompartmentCatalogEntry entry)
    {
        var engines = entry.Engines.Count;
        if (engines > 0)
            yield return engines == 1 ? "Двигатель" : $"Двигатели: {engines}";
        foreach (var group in entry.Devices.GroupBy(d => d.Kind))
            yield return group.Count() == 1 ? CustomDeviceCatalog.Name(group.Key) : $"{CustomDeviceCatalog.Name(group.Key)} ×{group.Count()}";
        if (entry.Airlock is not null)
            yield return "Шлюз";
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, BuildTool tool, RoomCategory selectedCategory,
        string? placingCompartmentId, int doorSpan, Point designMouse)
    {
        PanelFrame.Draw(spriteBatch, _pixel, PanelRect(panelOrigin));

        for (var i = 0; i < TabCount; i++)
        {
            var rect = GetTabRect(i, panelOrigin);
            bool active;
            Color accent;
            string label;
            if (i < Categories.Length)
            {
                active = tool == BuildTool.Compartments && Categories[i].Category == selectedCategory;
                accent = CategoryColor(Categories[i].Category);
                label = Categories[i].Label;
            }
            else if (i == DoorsTabIndex)
            {
                active = tool == BuildTool.Doors;
                accent = DoorColor;
                label = "Двери";
            }
            else
            {
                active = tool == BuildTool.Demolish;
                accent = DemolishColor;
                label = "Снос";
            }
            spriteBatch.Draw(_pixel, rect, active ? Color.Lerp(accent, Color.White, 0.15f) * 0.9f : new Color(32, 40, 35));
            var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
            var glyphColor = active ? Color.White : Color.LightGray;
            if (i < Categories.Length)
                DrawCategoryGlyph(spriteBatch, Categories[i].Category, center, glyphColor);
            else
                DrawCenteredText(spriteBatch, i == DoorsTabIndex ? "|=|" : "×", center, glyphColor, 0.6f);

            if (rect.Contains(designMouse))
                spriteBatch.DrawString(_font, label, new Vector2(rect.X, rect.Bottom + 2), Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }

        switch (tool)
        {
            case BuildTool.Compartments:
                DrawCompartments(spriteBatch, snapshot, panelOrigin, selectedCategory, placingCompartmentId, designMouse);
                break;
            case BuildTool.Doors:
                DrawDoors(spriteBatch, snapshot, panelOrigin, doorSpan, designMouse);
                break;
            default:
                var hint = "Кликните по отсеку на корабле, чтобы снести его (вернётся половина цены)";
                spriteBatch.DrawString(_font, hint, new Vector2(panelOrigin.X + 14, panelOrigin.Y + ModuleRowY + 10), Color.LightGray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, "Реактор, штурвал и последний шлюз снести нельзя; отсек, на котором держится часть корабля - тоже.",
                    new Vector2(panelOrigin.X + 14, panelOrigin.Y + ModuleRowY + 34), Color.Gray, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
                break;
        }
    }

    private void DrawCompartments(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, RoomCategory category,
        string? placingId, Point designMouse)
    {
        var entries = EntriesInCategory(category);
        CompartmentCatalogEntry? hovered = null;
        Rectangle hoveredRect = default;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var rect = GetModuleRect(i, panelOrigin);
            var price = CompartmentPricing.Price(entry);
            var affordable = snapshot.Credits >= price && snapshot.HullPlatingStock >= CompartmentPricing.PlatingCost(entry);
            var placing = entry.Id == placingId;
            var accent = CategoryColor(category);
            var face = placing ? Color.Lerp(accent, Color.White, 0.35f) : affordable ? accent * 0.6f : new Color(40, 40, 40);
            spriteBatch.Draw(_pixel, rect, face);
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, placing ? Color.White : PanelFrame.DefaultBorder, placing ? 2 : 1);
            DrawCategoryGlyph(spriteBatch, category, new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f - 6), affordable ? Color.White : Color.Gray);

            spriteBatch.DrawString(_font, $"{entry.Width}×{entry.Height}", new Vector2(rect.X + 3, rect.Y + 2),
                Color.LightGray, 0f, Vector2.Zero, 0.35f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, $"{price}", new Vector2(rect.X + 3, rect.Bottom - 14),
                affordable ? Color.LightGreen : Color.OrangeRed, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);

            if (rect.Contains(designMouse))
            {
                hovered = entry;
                hoveredRect = rect;
            }
        }

        if (hovered is not null)
        {
            var lines = new List<(string Text, Color Color)>
            {
                ($"{hovered.Width}×{hovered.Height} тайлов", Color.LightGray),
                ($"{CompartmentPricing.Price(hovered)} кр / {CompartmentPricing.PlatingCost(hovered)} об", Color.LightGreen),
            };
            lines.AddRange(Contents(hovered).Select(c => (c, Color.LightSkyBlue)));
            DrawTooltip(spriteBatch, hovered.DisplayName, lines, new Vector2(hoveredRect.X, hoveredRect.Y));
        }
    }

    private void DrawDoors(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, int selectedSpan, Point designMouse)
    {
        string[] names = { "Одинарная дверь", "Двойная дверь", "Тройная дверь" };
        for (var span = 1; span <= 3; span++)
        {
            var rect = GetModuleRect(span - 1, panelOrigin);
            var price = JunctionDoorBuilder.Price(span);
            var affordable = snapshot.Credits >= price;
            var selected = span == selectedSpan;
            var face = selected ? Color.Lerp(DoorColor, Color.White, 0.35f) : affordable ? DoorColor * 0.6f : new Color(40, 40, 40);
            spriteBatch.Draw(_pixel, rect, face);
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, selected ? Color.White : PanelFrame.DefaultBorder, selected ? 2 : 1);
            // The door drawn as span tiles wide.
            for (var i = 0; i < span; i++)
                spriteBatch.Draw(_pixel, new Rectangle(rect.X + 14 + i * 14, rect.Y + 14, 12, 28), affordable ? Color.White : Color.Gray);
            spriteBatch.DrawString(_font, $"{price}", new Vector2(rect.X + 3, rect.Bottom - 14),
                affordable ? Color.LightGreen : Color.OrangeRed, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);

            if (rect.Contains(designMouse))
                DrawTooltip(spriteBatch, names[span - 1], new List<(string, Color)>
                {
                    ($"Ширина: {span} т.", Color.LightGray),
                    ($"{price} кр", Color.LightGreen),
                    ("Ставится в полублочную стену", Color.LightSkyBlue),
                    ("R - повернуть, колёсико - ширина", Color.LightSkyBlue),
                }, new Vector2(rect.X, rect.Y));
        }
    }

    private static void DrawCategoryGlyph(SpriteBatch spriteBatch, Texture2D pixel, RoomCategory category, Vector2 center, Color color)
    {
        switch (category)
        {
            case RoomCategory.Power:
                HudIcons.DrawPowerGlyph(spriteBatch, pixel, center, 1f, color);
                break;
            case RoomCategory.Propulsion:
                HudIcons.DrawThrusterGlyph(spriteBatch, pixel, center, 1f, color);
                break;
            case RoomCategory.Crew:
                HudIcons.DrawCrewGlyph(spriteBatch, pixel, center, 0.9f, color);
                break;
            case RoomCategory.Weapons:
                HudIcons.DrawCrosshairGlyph(spriteBatch, pixel, center, 1f, color);
                break;
            case RoomCategory.Shields:
                HudIcons.DrawShieldGlyph(spriteBatch, pixel, center, 1f, color);
                break;
            case RoomCategory.Sensors:
                HudIcons.DrawSensorGlyph(spriteBatch, pixel, center, 1f, color);
                break;
            default:
                HudIcons.DrawStructuralGlyph(spriteBatch, pixel, center, 1f, color);
                break;
        }
    }

    private void DrawCategoryGlyph(SpriteBatch spriteBatch, RoomCategory category, Vector2 center, Color color) =>
        DrawCategoryGlyph(spriteBatch, _pixel, category, center, color);

    private void DrawCenteredText(SpriteBatch spriteBatch, string text, Vector2 center, Color color, float scale)
    {
        var size = _font.MeasureString(text) * scale;
        spriteBatch.DrawString(_font, text, center - size / 2f, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawTooltip(SpriteBatch spriteBatch, string title, IReadOnlyList<(string Text, Color Color)> lines, Vector2 anchorAboveModule)
    {
        const float titleScale = 0.55f;
        const float bodyScale = 0.48f;
        const float lineGap = 3f;
        var titleSize = _font.MeasureString(title) * titleScale;
        var width = titleSize.X;
        foreach (var (text, _) in lines)
            width = System.Math.Max(width, (_font.MeasureString(text) * bodyScale).X);
        width += 20f;
        var height = titleSize.Y + 10f + (bodyScale * _font.LineSpacing + lineGap) * lines.Count;

        var boxRect = new Rectangle((int)anchorAboveModule.X, (int)(anchorAboveModule.Y - height), (int)width, (int)height);
        PanelFrame.Draw(spriteBatch, _pixel, boxRect, thickness: 1);

        var row = new Vector2(boxRect.X + 10, boxRect.Y + 6);
        spriteBatch.DrawString(_font, title, row, Color.White, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
        row += new Vector2(0, titleSize.Y + 6);
        foreach (var (text, color) in lines)
        {
            spriteBatch.DrawString(_font, text, row, color, 0f, Vector2.Zero, bodyScale, SpriteEffects.None, 0f);
            row += new Vector2(0, bodyScale * _font.LineSpacing + lineGap);
        }
    }
}
