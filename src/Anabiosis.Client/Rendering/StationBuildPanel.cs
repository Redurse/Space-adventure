using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Content-каталог отсеков - the Shipwright's own bottom-of-screen build catalog (replaces the old
// flat two-column text list in StationPanel.DrawShipyard): a row of category tabs (InfoPanel's own
// icon-tab shape, just horizontal instead of vertical) above a row of module icons for whichever
// category is selected, each carrying small corner badges (tile size, price) and a hover tooltip
// (InventoryPanel.DrawTooltip's own "box sized to content, anchored so it never runs off-screen"
// pattern). Picking a module here doesn't buy it outright any more - it ENTERS PLACEMENT MODE
// (Game1.cs's own _placingRoomCatalogId), handled by Game1.Input.cs/ShipPlacementOverlay once the
// player is back aboard their own ship to actually point at a spot.
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

    // Display order - roughly "what you'd reach for first setting up a hull": basic structure,
    // then power, then getting it moving, then crew comfort, then combat/defense/sensing fitted on
    // top of an already-flyable hull. Public so Game1.Input.cs's own click handling walks the
    // SAME order GetCategoryTabRect's indices assume, rather than keeping a second copy in sync.
    public static readonly (RoomCategory Category, string Label)[] Categories =
    {
        (RoomCategory.Structural, "Корпус"),
        (RoomCategory.Power, "Питание"),
        (RoomCategory.Propulsion, "Двигатели"),
        (RoomCategory.Crew, "Экипаж"),
        (RoomCategory.Weapons, "Оружие"),
        (RoomCategory.Shields, "Щиты"),
        (RoomCategory.Sensors, "Сенсоры"),
    };

    // One accent colour per category - reused for both the tab highlight and the module tiles in
    // that category, so a module's own tile colour always matches whichever tab is currently
    // selected (a quick "yes, you're still looking at Propulsion" cue with no extra label needed).
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

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public StationBuildPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public static Rectangle GetCategoryTabRect(int index, Vector2 panelOrigin) =>
        new((int)panelOrigin.X + 10 + index * (TabSize + TabGap), (int)panelOrigin.Y + TabRowY, TabSize, TabSize);

    public static IReadOnlyList<RoomCatalogEntry> EntriesInCategory(RoomCategory category) =>
        RoomCatalog.Entries.Where(e => e.Category == category).ToList();

    public static Rectangle GetModuleRect(int index, Vector2 panelOrigin) =>
        new((int)panelOrigin.X + 10 + index * (ModuleSize + ModuleGap), (int)panelOrigin.Y + ModuleRowY, ModuleSize, ModuleSize);

    // Plain-language effect line for the hover tooltip - the same facts RecomputeDeviceBonuses
    // actually applies (World.ShipBuilding.cs), just spelled out for a player instead of left as
    // raw device kinds.
    private static string? EffectDescription(RoomCatalogEntry entry)
    {
        if (entry.ThrustBonus > 0f)
            return $"Тяга: +{entry.ThrustBonus:0}";
        if (entry.TurnBonus > 0f)
            return $"Разворот: +{entry.TurnBonus:0}";
        if (entry.Devices.Contains(CustomDeviceKind.Reactor))
            return $"Мощность реактора: +{RoomCatalog.ReactorRoomBonusOutput:0}";
        if (entry.Devices.Contains(CustomDeviceKind.Shields))
            return $"Прочность щита: +{RoomCatalog.ShieldRoomCapacityBonus:0}";
        if (entry.Devices.Contains(CustomDeviceKind.Helm) || entry.Devices.Contains(CustomDeviceKind.Navigation))
            return "Ещё одно место пилота и штурмана";
        if (entry.Devices.Contains(CustomDeviceKind.TurretLaser))
            return "Лазерная турель";
        if (entry.Devices.Contains(CustomDeviceKind.TurretBallistic))
            return "Пушечная турель";
        if (entry.Devices.Contains(CustomDeviceKind.Camera))
            return "Внешняя камера обзора";
        return null;
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, RoomCategory selectedCategory,
        string? placingCatalogId, Point designMouse)
    {
        var panelRect = new Rectangle((int)panelOrigin.X, (int)panelOrigin.Y, PanelWidth, PanelHeight);
        PanelFrame.Draw(spriteBatch, _pixel, panelRect);

        for (var i = 0; i < Categories.Length; i++)
        {
            var (category, label) = Categories[i];
            var rect = GetCategoryTabRect(i, panelOrigin);
            var active = category == selectedCategory;
            var accent = CategoryColor(category);
            spriteBatch.Draw(_pixel, rect, active ? Color.Lerp(accent, Color.White, 0.15f) * 0.9f : new Color(32, 40, 35));
            DrawCategoryGlyph(spriteBatch, _pixel, category, new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f), active ? Color.White : Color.LightGray);

            if (rect.Contains(designMouse))
                spriteBatch.DrawString(_font, label, new Vector2(rect.X, rect.Bottom + 2), Color.White, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        }

        var entries = EntriesInCategory(selectedCategory);
        RoomCatalogEntry? hovered = null;
        Rectangle hoveredRect = default;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var rect = GetModuleRect(i, panelOrigin);
            var affordable = snapshot.Credits >= entry.Price && snapshot.HullPlatingStock >= entry.PlatingCost;
            var placing = entry.Id == placingCatalogId;
            var accent = CategoryColor(entry.Category);
            var face = placing ? Color.Lerp(accent, Color.White, 0.35f) : affordable ? accent * 0.6f : new Color(40, 40, 40);
            spriteBatch.Draw(_pixel, rect, face);
            ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, placing ? Color.White : PanelFrame.DefaultBorder, placing ? 2 : 1);
            DrawCategoryGlyph(spriteBatch, _pixel, entry.Category, new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f - 6), affordable ? Color.White : Color.Gray);

            // Corner badges: tile size (top-left) and price (bottom-right) - same "small DrawString
            // at a rect corner offset" idiom InventoryPanel.DrawTooltip already uses for a tank's %.
            spriteBatch.DrawString(_font, $"{entry.Width / 3f:0.#}×{entry.Height / 3f:0.#}", new Vector2(rect.X + 3, rect.Y + 2),
                Color.LightGray, 0f, Vector2.Zero, 0.35f, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_font, $"{entry.Price}", new Vector2(rect.X + 3, rect.Bottom - 14),
                affordable ? Color.LightGreen : Color.OrangeRed, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);

            if (rect.Contains(designMouse))
            {
                hovered = entry;
                hoveredRect = rect;
            }
        }

        if (hovered is not null)
            DrawTooltip(spriteBatch, hovered, new Vector2(hoveredRect.X, hoveredRect.Y));
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

    private void DrawTooltip(SpriteBatch spriteBatch, RoomCatalogEntry entry, Vector2 anchorAboveModule)
    {
        var name = entry.Name;
        var sizeText = $"{entry.Width / 3f:0.#}×{entry.Height / 3f:0.#} тайла";
        var priceText = $"{entry.Price} кр / {entry.PlatingCost} об";
        var effect = EffectDescription(entry);

        const float titleScale = 0.55f;
        const float bodyScale = 0.48f;
        var nameSize = _font.MeasureString(name) * titleScale;
        var sizeSize = _font.MeasureString(sizeText) * bodyScale;
        var priceSize = _font.MeasureString(priceText) * bodyScale;
        var effectSize = effect is not null ? _font.MeasureString(effect) * bodyScale : Vector2.Zero;

        const float lineGap = 3f;
        var width = System.Math.Max(nameSize.X, System.Math.Max(sizeSize.X, System.Math.Max(priceSize.X, effectSize.X))) + 20f;
        var lines = 2 + (effect is not null ? 1 : 0);
        var height = nameSize.Y + 10f + (bodyScale * _font.LineSpacing + lineGap) * lines;

        var boxRect = new Rectangle((int)anchorAboveModule.X, (int)(anchorAboveModule.Y - height), (int)width, (int)height);
        PanelFrame.Draw(spriteBatch, _pixel, boxRect, thickness: 1);

        var textOrigin = new Vector2(boxRect.X + 10, boxRect.Y + 6);
        spriteBatch.DrawString(_font, name, textOrigin, Color.White, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
        var row = textOrigin + new Vector2(0, nameSize.Y + 6);
        spriteBatch.DrawString(_font, sizeText, row, Color.LightGray, 0f, Vector2.Zero, bodyScale, SpriteEffects.None, 0f);
        row += new Vector2(0, bodyScale * _font.LineSpacing + lineGap);
        spriteBatch.DrawString(_font, priceText, row, Color.LightGreen, 0f, Vector2.Zero, bodyScale, SpriteEffects.None, 0f);
        if (effect is not null)
        {
            row += new Vector2(0, bodyScale * _font.LineSpacing + lineGap);
            spriteBatch.DrawString(_font, effect, row, Color.LightSkyBlue, 0f, Vector2.Zero, bodyScale, SpriteEffects.None, 0f);
        }
    }
}
