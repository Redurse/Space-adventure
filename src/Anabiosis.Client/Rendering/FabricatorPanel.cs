using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Direct user request ("сделай меню фабрикатора как в баротравме, где можно выбрать в списке
// предмет, снизу 5 ячеек для вставки ресурсов и где показывает какие ресурсы нужны и разделы с
// боку... сделай интерфейс буквально почти точь в точь как на скрине") - a category sidebar, a
// filterable/sortable recipe list with a "only what I can afford" checkbox, a detail pane for the
// selected recipe, and a bottom "ВВОД" row of ingredient slots plus a Create button. Two
// simplifications from the reference screenshot, both because the underlying systems don't exist in
// this game at all: no "Рекомендуемые навыки"/craft-time section (no skill system, crafting is
// instant - World.Fabricator.cs's own TryCraftAtFabricator), and no batch-quantity slider (always
// crafts exactly 1 per click). The category sidebar only has one real, clickable entry
// (FabricatorCatalog's own single "Материалы" category) - the other icons are drawn disabled, purely
// so the sidebar reads the same as the reference for when a second category's worth of recipes
// exists to put behind them.
public sealed class FabricatorPanel
{
    public static readonly Point PanelSize = new(620, 420);

    private const int SidebarWidth = 34;
    private const int SidebarIconSize = 28;
    private const int ListWidth = 232;
    private const int ContentTop = 4;
    private const int FilterBoxHeight = 18;
    private const int SortRowY = 26;
    private const int CheckboxRowY = 46;
    private const int CheckboxSize = 13;
    private const int ListTop = 68;
    private const int RowHeight = 19;
    private const int DetailLeft = SidebarWidth + ListWidth + 14;
    private const int InputRowTop = 330;
    private const int InputSlotSize = 46;
    private const int InputSlotGap = 8;
    public const int MaxInputSlots = 5;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public FabricatorPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    // Shared by Draw and the click handler (Game1.Input.cs) so the two can never disagree about
    // which recipe a given row/slot actually is - same "one shared layer" idiom
    // Game1.Interactables.cs's own doc comment already uses for device hit-rects.
    public static bool CanAfford(FabricatorRecipe recipe, InventoryState? inventory) =>
        inventory is not null && recipe.Ingredients.All(ing => inventory.MainSlots.Count(s => s == ing.Item) >= ing.Count);

    public static IReadOnlyList<FabricatorRecipe> VisibleRecipes(bool onlyAvailable, InventoryState? inventory) =>
        onlyAvailable ? FabricatorCatalog.Recipes.Where(r => CanAfford(r, inventory)).ToList() : FabricatorCatalog.Recipes.ToList();

    public static Rectangle Housing(Vector2 origin) => new(
        (int)origin.X - DevicePanelChrome.OriginInsetX, (int)origin.Y - DevicePanelChrome.OriginInsetY,
        PanelSize.X, PanelSize.Y);

    public static Rectangle GetOnlyAvailableCheckboxRect(Vector2 origin) =>
        new((int)origin.X + SidebarWidth, (int)origin.Y + CheckboxRowY, CheckboxSize, CheckboxSize);

    public static Rectangle GetSortToggleRect(Vector2 origin) =>
        new((int)origin.X + SidebarWidth, (int)origin.Y + SortRowY, ListWidth, FilterBoxHeight);

    public static Rectangle GetRecipeRowRect(int visibleIndex, Vector2 origin) =>
        new((int)origin.X + SidebarWidth, (int)origin.Y + ListTop + visibleIndex * RowHeight, ListWidth, RowHeight - 2);

    public static Rectangle GetInputSlotRect(int index, Vector2 origin) =>
        new((int)origin.X + SidebarWidth + index * (InputSlotSize + InputSlotGap), (int)origin.Y + InputRowTop + 14, InputSlotSize, InputSlotSize);

    public static Rectangle GetCreateButtonRect(Vector2 origin) => new(
        (int)origin.X + SidebarWidth + MaxInputSlots * (InputSlotSize + InputSlotGap) + 16,
        (int)origin.Y + InputRowTop + 14 + InputSlotSize / 2 - 12, 120, 26);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int playerId, Vector2 origin, bool onlyAvailable, bool sortByName, string? selectedRecipeId)
    {
        var housing = Housing(origin);
        spriteBatch.Draw(_pixel, housing, new Color(14, 20, 24));
        DrawRectOutline(spriteBatch, housing, new Color(70, 150, 140), 2);
        DrawRectOutline(spriteBatch, new Rectangle(housing.X + 2, housing.Y + 2, housing.Width - 4, 26), new Color(30, 44, 46), 1);
        DrawCentered(spriteBatch, "ФАБРИКАТОР", new Vector2(housing.Center.X, housing.Y + 14), Color.White, 0.6f);

        var inventory = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId)?.Inventory;
        var visible = VisibleRecipes(onlyAvailable, inventory);
        if (sortByName)
            visible = visible.OrderBy(r => ItemDefinitions.DisplayName(r.Output)).ToList();

        DrawSidebar(spriteBatch, origin);
        DrawListColumn(spriteBatch, origin, visible, inventory, onlyAvailable, sortByName, selectedRecipeId);

        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId);
        var busy = me?.ProductionActionRemaining > 0 && !me.ProductionIsDeconstruct;
        var busyRecipe = busy && me!.ProductionRecipeId is { } busyId ? FabricatorCatalog.Find(busyId) : null;
        var selected = busyRecipe ?? visible.FirstOrDefault(r => r.Id == selectedRecipeId) ?? visible.FirstOrDefault();
        DrawDetailPane(spriteBatch, origin, selected);
        var progress = busy ? 1f - Math.Clamp(me!.ProductionActionRemaining / FabricatorCatalog.ProductionDurationSeconds, 0f, 1f) : (float?)null;
        DrawInputRow(spriteBatch, origin, selected, inventory, progress);
    }

    // Only "Материалы" is real (FabricatorCatalog.MaterialsCategory) - the rest are drawn dimmed,
    // reserved for whenever a second recipe category exists (weapons/medical/... per the reference
    // screenshot), not wired to anything yet.
    private void DrawSidebar(SpriteBatch spriteBatch, Vector2 origin)
    {
        var x = (int)origin.X;
        var y = (int)origin.Y + ContentTop + 30;
        DrawSidebarIcon(spriteBatch, new Rectangle(x, y, SidebarIconSize, SidebarIconSize), "М", active: true);
        for (var i = 1; i < 5; i++)
            DrawSidebarIcon(spriteBatch, new Rectangle(x, y + i * (SidebarIconSize + 6), SidebarIconSize, SidebarIconSize), "-", active: false);
    }

    private void DrawSidebarIcon(SpriteBatch spriteBatch, Rectangle rect, string glyph, bool active)
    {
        spriteBatch.Draw(_pixel, rect, active ? new Color(40, 70, 68) : new Color(24, 30, 32));
        DrawRectOutline(spriteBatch, rect, active ? new Color(120, 210, 190) : new Color(60, 68, 70), 1);
        DrawCentered(spriteBatch, glyph, new Vector2(rect.Center.X, rect.Center.Y), active ? Color.White : Color.Gray * 0.6f, 0.5f);
    }

    private void DrawListColumn(SpriteBatch spriteBatch, Vector2 origin, IReadOnlyList<FabricatorRecipe> visible,
        InventoryState? inventory, bool onlyAvailable, bool sortByName, string? selectedRecipeId)
    {
        var listX = origin.X + SidebarWidth;

        // Filter box - visual only (FabricatorPanel.cs's own doc comment: only 5 recipes exist
        // today, not enough to make free-text search worth the keyboard-capture plumbing yet).
        var filterRect = new Rectangle((int)listX, (int)origin.Y + ContentTop, ListWidth, FilterBoxHeight);
        spriteBatch.Draw(_pixel, filterRect, new Color(8, 12, 14));
        DrawRectOutline(spriteBatch, filterRect, new Color(60, 90, 88), 1);
        spriteBatch.DrawString(_font, "Фильтр...", new Vector2(filterRect.X + 4, filterRect.Y + 2), Color.Gray * 0.8f, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

        var sortRect = GetSortToggleRect(origin);
        spriteBatch.Draw(_pixel, sortRect, new Color(20, 30, 32));
        DrawRectOutline(spriteBatch, sortRect, new Color(60, 90, 88), 1);
        spriteBatch.DrawString(_font, sortByName ? "Сортировка: имя" : "Сортировка: категория", new Vector2(sortRect.X + 4, sortRect.Y + 2), Color.White, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

        var checkboxRect = GetOnlyAvailableCheckboxRect(origin);
        spriteBatch.Draw(_pixel, checkboxRect, new Color(20, 30, 32));
        DrawRectOutline(spriteBatch, checkboxRect, Color.LightGray, 1);
        if (onlyAvailable)
            spriteBatch.Draw(_pixel, new Rectangle(checkboxRect.X + 2, checkboxRect.Y + 2, checkboxRect.Width - 4, checkboxRect.Height - 4), new Color(120, 220, 140));
        spriteBatch.DrawString(_font, "Только доступные", new Vector2(checkboxRect.Right + 6, checkboxRect.Y - 1), Color.White, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

        var categoryLabelPos = new Vector2(listX, origin.Y + ListTop - 16);
        spriteBatch.DrawString(_font, FabricatorCatalog.Recipes.FirstOrDefault()?.Category ?? "Материалы", categoryLabelPos, new Color(150, 200, 190), 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);

        for (var i = 0; i < visible.Count; i++)
        {
            var recipe = visible[i];
            var rowRect = GetRecipeRowRect(i, origin);
            var afford = CanAfford(recipe, inventory);
            var isSelected = recipe.Id == selectedRecipeId;
            spriteBatch.Draw(_pixel, rowRect, isSelected ? new Color(70, 110, 60) : new Color(18, 26, 28));
            if (isSelected)
                DrawRectOutline(spriteBatch, rowRect, new Color(180, 220, 100), 1);
            var textColor = afford ? Color.White : Color.Gray * 0.7f;
            spriteBatch.DrawString(_font, ItemDefinitions.DisplayName(recipe.Output), new Vector2(rowRect.X + 4, rowRect.Y + 2), textColor, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
        }
    }

    private void DrawDetailPane(SpriteBatch spriteBatch, Vector2 origin, FabricatorRecipe? recipe)
    {
        var left = origin.X + DetailLeft;
        var iconRect = new Rectangle((int)left, (int)origin.Y + ContentTop + 4, 64, 64);
        spriteBatch.Draw(_pixel, iconRect, new Color(8, 12, 14));
        DrawRectOutline(spriteBatch, iconRect, new Color(60, 90, 88), 1);
        if (recipe is not null)
            ItemIcons.Draw(spriteBatch, _pixel, recipe.Output, iconRect);

        if (recipe is null)
        {
            spriteBatch.DrawString(_font, "Ничего не выбрано", new Vector2(left, origin.Y + ContentTop + 76), Color.Gray, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
            return;
        }

        var namePos = new Vector2(iconRect.Right + 10, origin.Y + ContentTop + 14);
        spriteBatch.DrawString(_font, ItemDefinitions.DisplayName(recipe.Output).ToUpperInvariant(), namePos, new Color(150, 220, 200), 0f, Vector2.Zero, 0.48f, SpriteEffects.None, 0f);

        var description = ItemDescriptions.Describe(recipe.Output) ?? "";
        DrawWrapped(spriteBatch, description, new Vector2(left, iconRect.Bottom + 12), (int)(PanelSize.X - DetailLeft - 16), Color.White, 0.42f);
    }

    // `progress` is null while idle, 0..1 while a craft is actually running (direct user request,
    // "процесс сборки... занимал 2 секунды... заполнялась снизу вверх зелёная зарисовка").
    private void DrawInputRow(SpriteBatch spriteBatch, Vector2 origin, FabricatorRecipe? recipe, InventoryState? inventory, float? progress)
    {
        var labelPos = new Vector2(origin.X + SidebarWidth, origin.Y + InputRowTop - 4);
        spriteBatch.DrawString(_font, "ВВОД", labelPos, new Color(150, 200, 190), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
        DrawRectOutline(spriteBatch, new Rectangle((int)origin.X + 2, (int)origin.Y + InputRowTop - 10, PanelSize.X - 4, PanelSize.Y - InputRowTop - 2), new Color(40, 60, 60), 1);

        for (var i = 0; i < MaxInputSlots; i++)
        {
            var slotRect = GetInputSlotRect(i, origin);
            var ingredient = recipe is not null && i < recipe.Ingredients.Count ? recipe.Ingredients[i] : null;
            var have = ingredient is null ? 0 : (inventory?.MainSlots.Count(s => s == ingredient.Item) ?? 0);
            var afford = ingredient is null || have >= ingredient.Count;

            spriteBatch.Draw(_pixel, slotRect, new Color(8, 12, 14));
            DrawRectOutline(spriteBatch, slotRect, ingredient is null ? new Color(40, 50, 52) : afford ? new Color(110, 200, 120) : new Color(200, 90, 80), 2);
            if (ingredient is not null)
            {
                var iconRect = new Rectangle(slotRect.X + 4, slotRect.Y + 4, slotRect.Width - 8, slotRect.Height - 16);
                ItemIcons.Draw(spriteBatch, _pixel, ingredient.Item, iconRect);
                var qty = $"{have}/{ingredient.Count}";
                var qtySize = _font.MeasureString(qty) * 0.36f;
                spriteBatch.DrawString(_font, qty, new Vector2(slotRect.Center.X - qtySize.X / 2f, slotRect.Bottom - 12), afford ? Color.White : new Color(255, 160, 140), 0f, Vector2.Zero, 0.36f, SpriteEffects.None, 0f);
                if (progress is { } p && i == 0)
                {
                    var barBack = new Rectangle(slotRect.X, slotRect.Bottom + 4, slotRect.Width, 6);
                    spriteBatch.Draw(_pixel, barBack, new Color(30, 40, 30));
                    var fillHeight = (int)(barBack.Height * p);
                    spriteBatch.Draw(_pixel, new Rectangle(barBack.X, barBack.Bottom - fillHeight, barBack.Width, fillHeight), new Color(110, 210, 110));
                    DrawRectOutline(spriteBatch, barBack, new Color(80, 100, 80), 1);
                }
            }
        }

        var createRect = GetCreateButtonRect(origin);
        var busy = progress is not null;
        var canCraft = !busy && recipe is not null && CanAfford(recipe, inventory);
        spriteBatch.Draw(_pixel, createRect, busy ? new Color(130, 70, 60) : canCraft ? new Color(70, 130, 60) : new Color(40, 40, 42));
        DrawRectOutline(spriteBatch, createRect, busy ? new Color(230, 140, 120) : canCraft ? new Color(160, 230, 120) : new Color(80, 80, 84), 1);
        DrawCentered(spriteBatch, busy ? "ОТМЕНА" : "СОЗДАТЬ", new Vector2(createRect.Center.X, createRect.Center.Y), busy || canCraft ? Color.White : Color.Gray, 0.44f);
    }

    private void DrawCentered(SpriteBatch spriteBatch, string text, Vector2 center, Color color, float scale)
    {
        var size = _font.MeasureString(text) * scale;
        spriteBatch.DrawString(_font, text, new Vector2(center.X - size.X / 2f, center.Y - size.Y / 2f), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    // A crude word-wrap - splits on spaces and breaks a line once it would exceed maxWidth. Good
    // enough for a short flavor sentence, not meant for anything longer.
    private void DrawWrapped(SpriteBatch spriteBatch, string text, Vector2 origin, int maxWidth, Color color, float scale)
    {
        var words = text.Split(' ');
        var line = "";
        var y = origin.Y;
        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (_font.MeasureString(candidate).X * scale > maxWidth && line.Length > 0)
            {
                spriteBatch.DrawString(_font, line, new Vector2(origin.X, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                line = word;
                y += 14;
            }
            else
            {
                line = candidate;
            }
        }
        if (line.Length > 0)
            spriteBatch.DrawString(_font, line, new Vector2(origin.X, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
