using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Direct user request ("сделай чтобы при нажатии на деконструктор открывалась менюшка как на
// скрине... получается его содержимое как если бы его скрафтили наоборот") - runs a
// FabricatorRecipe backwards: pick one of the deconstructable items you're carrying, "РАЗОБРАТЬ"
// consumes ONE of it (after the same 2-second timer Fabricator crafting uses,
// FabricatorCatalog.ProductionDurationSeconds) and returns its own recipe's Ingredients straight to
// your inventory. Simplifications from the reference screenshot: "ОЧЕРЕДЬ" is drawn as reserved,
// always-empty UI space (no real multi-item batching exists), there's no separate manual "collect
// materials" step (World.Fabricator.cs's own StepProduction adds them straight to your inventory the
// moment the timer completes), and picking which item to deconstruct is a small icon row rather than
// a literal drag-and-drop slot (this game's Inventory has no drag target for "into a device" the way
// a rack slot works).
public sealed class DeconstructorPanel
{
    public static readonly Point PanelSize = new(560, 360);

    private const int SidebarInsetX = 16;
    private const int QueueTop = 26;
    private const int QueueSlotSize = 46;
    private const int QueueSlotGap = 8;
    private const int InputLabelTop = 26;
    private const int InputSlotsTop = 46;
    private const int InputSlotSize = 46;
    private const int InputSlotGap = 8;
    private const int MaxInputCandidates = 3;
    private const int ResultTop = 150;
    private const int ResultSlotSize = 52;
    private const int ResultSlotGap = 10;
    public const int MaxResultSlots = 5;
    private const int ButtonWidth = 130;
    private const int ButtonHeight = 34;
    private const int ProgressBarHeight = 6;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public DeconstructorPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    // Shared by Draw and the click handler - every distinct ItemType currently held that some
    // FabricatorRecipe actually produces (FabricatorCatalog.FindByOutput), in a stable order.
    public static IReadOnlyList<ItemType> DeconstructableCandidates(InventoryState? inventory) =>
        inventory is null
            ? Array.Empty<ItemType>()
            : inventory.MainSlots.Where(s => s is { } t && FabricatorCatalog.FindByOutput(t) is not null)
                .Select(s => s!.Value).Distinct().Take(MaxInputCandidates).ToList();

    public static Rectangle Housing(Vector2 origin) => new(
        (int)origin.X - DevicePanelChrome.OriginInsetX, (int)origin.Y - DevicePanelChrome.OriginInsetY,
        PanelSize.X, PanelSize.Y);

    public static Rectangle GetInputSlotRect(int index, Vector2 origin) =>
        new((int)origin.X + SidebarInsetX + index * (InputSlotSize + InputSlotGap), (int)origin.Y + InputSlotsTop, InputSlotSize, InputSlotSize);

    public static Rectangle GetCreateButtonRect(Vector2 origin) => new(
        (int)origin.X + SidebarInsetX + MaxInputCandidates * (InputSlotSize + InputSlotGap) + 24,
        (int)origin.Y + InputSlotsTop + InputSlotSize / 2 - ButtonHeight / 2, ButtonWidth, ButtonHeight);

    public static Rectangle GetResultSlotRect(int index, Vector2 origin) =>
        new((int)origin.X + SidebarInsetX + index * (ResultSlotSize + ResultSlotGap), (int)origin.Y + ResultTop + 18, ResultSlotSize, ResultSlotSize);

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int playerId, Vector2 origin, ItemType? selected)
    {
        var housing = Housing(origin);
        spriteBatch.Draw(_pixel, housing, new Color(14, 20, 24));
        DrawRectOutline(spriteBatch, housing, new Color(150, 90, 70), 2);
        DrawRectOutline(spriteBatch, new Rectangle(housing.X + 2, housing.Y + 2, housing.Width - 4, 26), new Color(44, 30, 30), 1);
        DrawCentered(spriteBatch, "ДЕКОНСТРУКТОР", new Vector2(housing.Center.X, housing.Y + 14), Color.White, 0.6f);

        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId);
        var inventory = me?.Inventory;
        var busy = me?.ProductionActionRemaining > 0 && me.ProductionIsDeconstruct;
        var progress = busy ? 1f - Math.Clamp((me!.ProductionActionRemaining) / FabricatorCatalog.ProductionDurationSeconds, 0f, 1f) : 0f;
        var busyItem = busy && me!.ProductionRecipeId is { } busyRecipeId ? FabricatorCatalog.Find(busyRecipeId)?.Output : null;

        // "ОЧЕРЕДЬ" - reserved space only, see the class's own doc comment.
        spriteBatch.DrawString(_font, "ОЧЕРЕДЬ", new Vector2(origin.X + SidebarInsetX, origin.Y + QueueTop - 16), new Color(200, 160, 150), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
        for (var i = 0; i < 2; i++)
        {
            var slot = new Rectangle((int)origin.X + SidebarInsetX + i * (QueueSlotSize + QueueSlotGap), (int)origin.Y + QueueTop, QueueSlotSize, QueueSlotSize);
            spriteBatch.Draw(_pixel, slot, new Color(8, 12, 14));
            DrawRectOutline(spriteBatch, slot, new Color(60, 60, 62), 1);
        }

        spriteBatch.DrawString(_font, "ВВОД", new Vector2(origin.X + SidebarInsetX, origin.Y + InputLabelTop + QueueTop + QueueSlotSize - 8), new Color(200, 160, 150), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);

        var candidates = busy && busyItem is { } lockedItem ? new[] { lockedItem } : DeconstructableCandidates(inventory).ToArray();
        for (var i = 0; i < MaxInputCandidates; i++)
        {
            var slotRect = GetInputSlotRect(i, origin);
            var item = i < candidates.Length ? candidates[i] : (ItemType?)null;
            var isSelected = item is not null && (busy ? item == busyItem : item == selected);
            spriteBatch.Draw(_pixel, slotRect, new Color(8, 12, 14));
            DrawRectOutline(spriteBatch, slotRect, isSelected ? new Color(220, 150, 90) : new Color(60, 60, 62), isSelected ? 2 : 1);
            if (item is { } shownItem)
            {
                var iconRect = new Rectangle(slotRect.X + 3, slotRect.Y + 3, slotRect.Width - 6, slotRect.Height - 6);
                ItemIcons.Draw(spriteBatch, _pixel, shownItem, iconRect);
            }
            if (isSelected && busy)
            {
                var barBack = new Rectangle(slotRect.X, slotRect.Bottom + 4, slotRect.Width, ProgressBarHeight);
                spriteBatch.Draw(_pixel, barBack, new Color(30, 40, 30));
                var fillHeight = (int)(barBack.Height * progress);
                spriteBatch.Draw(_pixel, new Rectangle(barBack.X, barBack.Bottom - fillHeight, barBack.Width, fillHeight), new Color(110, 210, 110));
                DrawRectOutline(spriteBatch, barBack, new Color(80, 100, 80), 1);
            }
        }

        var createRect = GetCreateButtonRect(origin);
        ItemType? effectiveSelected = busy ? busyItem
            : selected is { } sel && DeconstructableCandidates(inventory).Contains(sel) ? sel
            : candidates.Length > 0 ? candidates[0] : null;
        var canStart = !busy && effectiveSelected is not null;
        spriteBatch.Draw(_pixel, createRect, busy ? new Color(130, 70, 60) : canStart ? new Color(130, 90, 60) : new Color(40, 40, 42));
        DrawRectOutline(spriteBatch, createRect, busy ? new Color(230, 140, 120) : canStart ? new Color(230, 180, 120) : new Color(80, 80, 84), 1);
        DrawCentered(spriteBatch, busy ? "ОТМЕНА" : "РАЗОБРАТЬ", new Vector2(createRect.Center.X, createRect.Center.Y), busy || canStart ? Color.White : Color.Gray, 0.44f);

        // "РЕЗУЛЬТАТ" - what deconstructing the selected/in-progress item yields.
        spriteBatch.DrawString(_font, "РЕЗУЛЬТАТ", new Vector2(origin.X + SidebarInsetX, origin.Y + ResultTop), new Color(200, 160, 150), 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        var recipe = effectiveSelected is { } forRecipe ? FabricatorCatalog.FindByOutput(forRecipe) : null;
        for (var i = 0; i < MaxResultSlots; i++)
        {
            var slotRect = GetResultSlotRect(i, origin);
            spriteBatch.Draw(_pixel, slotRect, new Color(8, 12, 14));
            DrawRectOutline(spriteBatch, slotRect, new Color(60, 60, 62), 1);
            var ingredient = recipe is not null && i < recipe.Ingredients.Count ? recipe.Ingredients[i] : null;
            if (ingredient is null)
                continue;
            var iconRect = new Rectangle(slotRect.X + 4, slotRect.Y + 4, slotRect.Width - 8, slotRect.Height - 16);
            ItemIcons.Draw(spriteBatch, _pixel, ingredient.Item, iconRect);
            var qty = $"x{ingredient.Count}";
            var qtySize = _font.MeasureString(qty) * 0.38f;
            spriteBatch.DrawString(_font, qty, new Vector2(slotRect.Center.X - qtySize.X / 2f, slotRect.Bottom - 14), Color.White, 0f, Vector2.Zero, 0.38f, SpriteEffects.None, 0f);
        }

        spriteBatch.DrawString(_font, "МАТЕРИАЛЫ НА ВЫХОДЕ:", new Vector2(origin.X + SidebarInsetX, origin.Y + ResultTop + ResultSlotSize + 34), new Color(200, 190, 150), 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
    }

    private void DrawCentered(SpriteBatch spriteBatch, string text, Vector2 center, Color color, float scale)
    {
        var size = _font.MeasureString(text) * scale;
        spriteBatch.DrawString(_font, text, new Vector2(center.X - size.X / 2f, center.Y - size.Y / 2f), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
