using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Client;

// Space Haven-style tabbed device palette (direct user request) - replaces the old flat, two-column
// CustomDeviceKind list (which stopped being usable once the catalog passed ~30 entries: rows were
// already shrinking below readable height, see the git history on GetEditorDeviceEntryRect). A tab
// groups a handful of related placeable things; picking one still just sets _editorTool/
// _editorSelectedDeviceKind exactly like the old flat list did - nothing about HOW placement itself
// works changes, only how the palette is browsed.
//
// A palette entry isn't always a CustomDeviceKind: Wall/Door/Terminal are their own EditorTool, not
// devices at all, but the user's own tab breakdown lists them alongside real devices ("Стена"/
// "Дверь" under "Стены", "Терминал" под "Мебель", "Шлюз" under "Шлюз" - the same Door tool, just a
// second, differently-labelled entry point for it since an airlock is just a door on the outer hull,
// auto-detected, not its own mechanic). PaletteItem's own Kind is null for these - selecting one only
// switches _editorTool, never touches _editorSelectedDeviceKind.
//
// Deliberately NOT included yet: "Двойная дверь"/"Блок иллюминатора" (Стены tab) - these are
// genuinely new WALL VARIANTS, not devices, and TileWallKind today only knows Solid/Door/None with no
// room for a variant at all. Adding them needs a real model extension (a wall "skin"/variant field on
// TileCell), a separate follow-up task - not just a palette entry.
public partial class Game1
{
    private enum DeviceTab { ShipControl, Airlock, Storage, Production, Power, Furniture, Weapons, Walls, All }

    private static readonly string[] DeviceTabLabels =
    {
        "Управление кораблём", "Шлюз", "Хранение", "Производство", "Электроэнергия", "Мебель", "Оружие", "Стены", "Все",
    };

    private DeviceTab _editorDeviceTab = DeviceTab.ShipControl;

    // Direct user request ("как в space haven, как на скриншоте, и в разделе модификации была
    // возможность создавать зоны") - Space Haven itself splits its build menu into two top-level
    // modes (its own "ОБЪЕКТЫ"/"МОДИФИКАЦИИ" buttons): placing discrete physical things vs.
    // painting/marking the hull itself. Floor and Zone fit that second bucket - neither is a
    // CustomDeviceKind or even had a tab of its own before this - while every real device/wall/door/
    // terminal stays under Objects exactly as already built.
    private enum EditorPanelMode { Objects, Modifications }
    private EditorPanelMode _editorPanelMode = EditorPanelMode.Objects;

    private readonly record struct PaletteItem(string Label, EditorTool Tool, CustomDeviceKind? Kind = null);

    private static PaletteItem DeviceItem(CustomDeviceKind kind) => new(CustomDeviceCatalog.Name(kind), EditorTool.Device, kind);

    private static readonly PaletteItem[] ShipControlItems =
    {
        DeviceItem(CustomDeviceKind.Helm), DeviceItem(CustomDeviceKind.Navigation),
        DeviceItem(CustomDeviceKind.EngineSmall), DeviceItem(CustomDeviceKind.EngineMedium), DeviceItem(CustomDeviceKind.EngineLarge),
        DeviceItem(CustomDeviceKind.WarpEngine), DeviceItem(CustomDeviceKind.Camera),
    };

    // "Шлюз" itself reuses the Door tool (an airlock is just a door on the outer hull, inferred by
    // the tile bridge - see Game1.ShipEditor.TileBridge.cs's own SideIsAirlock) - not a separate
    // mechanic, so no CustomDeviceKind for it either.
    private static readonly PaletteItem[] AirlockItems =
    {
        new("Шлюз", EditorTool.Door),
        DeviceItem(CustomDeviceKind.ShuttleHangar), DeviceItem(CustomDeviceKind.DroneHangar),
    };

    private static readonly PaletteItem[] StorageItems =
    {
        DeviceItem(CustomDeviceKind.SmallStorage), DeviceItem(CustomDeviceKind.LargeStorage),
        DeviceItem(CustomDeviceKind.Morgue), DeviceItem(CustomDeviceKind.FuelRodStorage),
    };

    private static readonly PaletteItem[] ProductionItems =
    {
        DeviceItem(CustomDeviceKind.ConstructionBench), DeviceItem(CustomDeviceKind.Fabricator),
        DeviceItem(CustomDeviceKind.Deconstructor), DeviceItem(CustomDeviceKind.WeaponWorkbench),
    };

    private static readonly PaletteItem[] PowerItems =
    {
        DeviceItem(CustomDeviceKind.Reactor), DeviceItem(CustomDeviceKind.Distribution),
        DeviceItem(CustomDeviceKind.Battery), DeviceItem(CustomDeviceKind.Junction), DeviceItem(CustomDeviceKind.PowerConduit),
    };

    // Terminal reuses its own existing tool (mounts to a wall's side, doesn't occupy a floor slot the
    // way every real device does) - included here per direct user request, not a CustomDeviceKind.
    private static readonly PaletteItem[] FurnitureItems =
    {
        DeviceItem(CustomDeviceKind.Table), DeviceItem(CustomDeviceKind.Chair), DeviceItem(CustomDeviceKind.Sofa),
        DeviceItem(CustomDeviceKind.Bed), DeviceItem(CustomDeviceKind.Nightstand), DeviceItem(CustomDeviceKind.WallLamp),
        DeviceItem(CustomDeviceKind.Spotlight), DeviceItem(CustomDeviceKind.Lamp), DeviceItem(CustomDeviceKind.DecorativePlant),
        new("Терминал", EditorTool.Terminal),
    };

    // "Лазерное орудие"/"Автопушка"/"Рельсотрон" map onto the existing TurretLaser/TurretMachineGun/
    // TurretBallistic (just newly categorized here, not renamed - the user's own list gave no rename
    // callout for these the way it did for Helm/Navigation/Junction).
    private static readonly PaletteItem[] WeaponItems =
    {
        DeviceItem(CustomDeviceKind.TurretBallistic), DeviceItem(CustomDeviceKind.TurretLaser), DeviceItem(CustomDeviceKind.TurretMachineGun),
        DeviceItem(CustomDeviceKind.DefensiveTurret), DeviceItem(CustomDeviceKind.ShieldGeneratorSmall), DeviceItem(CustomDeviceKind.ShieldGeneratorLarge),
        DeviceItem(CustomDeviceKind.WeaponPanel),
    };

    // "Двойная дверь"/"Блок иллюминатора" deliberately absent - see this file's own top comment.
    private static readonly PaletteItem[] WallItems =
    {
        new("Стена", EditorTool.Wall), new("Дверь", EditorTool.Door),
    };

    // Every device kind the enum actually has, in declaration order, plus the 3 tool-only entries -
    // the same full set the old flat palette showed, just reachable from one tab instead of being
    // the only view. Existing device kinds that no per-tab list above mentions yet (Shields/Oxygen/
    // AmmoStorage/SuitLocker/StorageRack/CardTable/Jukebox/ComponentMount/Secondary/WeaponCharger)
    // still show up here - nothing is ever unreachable, even before every kind has its own curated tab.
    private static readonly PaletteItem[] AllItems =
        new PaletteItem[] { new("Стена", EditorTool.Wall), new("Дверь", EditorTool.Door), new("Терминал", EditorTool.Terminal) }
            .Concat(EditorDeviceKinds.Select(DeviceItem))
            .ToArray();

    // The "МОДИФИКАЦИИ" mode's own (untabbed - only 2 items, no categories needed) item row.
    private static readonly PaletteItem[] ModificationItems =
    {
        new("Пол", EditorTool.Floor), new("Зона", EditorTool.Zone),
    };

    private static PaletteItem[] DeviceTabItems(DeviceTab tab) => tab switch
    {
        DeviceTab.ShipControl => ShipControlItems,
        DeviceTab.Airlock => AirlockItems,
        DeviceTab.Storage => StorageItems,
        DeviceTab.Production => ProductionItems,
        DeviceTab.Power => PowerItems,
        DeviceTab.Furniture => FurnitureItems,
        DeviceTab.Weapons => WeaponItems,
        DeviceTab.Walls => WallItems,
        DeviceTab.All => AllItems,
        _ => Array.Empty<PaletteItem>(),
    };

    // Restyled to match the Space Haven/RimWorld reference screenshot (direct user request): dark
    // navy panel, gold/amber accents, one label-above-icon cell per item in a SINGLE horizontal row
    // (not a wrapped multi-row grid) - scrollable left/right once a tab has more items than fit (only
    // "Все" actually needs this; every curated tab has <=10 items, comfortably under one row's own
    // visible capacity). No real per-item art exists (direct user answer - no image-generation tool
    // available here); DrawItemIcon draws a simple flat swatch tinted per item (CustomDeviceCatalog.
    // Tint) as a placeholder, not bespoke art.
    private static readonly Color DeviceGold = new(255, 176, 40);
    private static readonly Color DevicePanelBg = new(9, 13, 24);

    private const int DevicePanelLeft = 10;
    // The tab row/item strip's own right edge - narrower than the panel's true outer edge
    // (DevicePanelOuterRight) to leave room for the "МОДИФИКАЦИИ"/"ОБЪЕКТЫ" mode column.
    private const int DevicePanelRight = 1080;
    private const int DevicePanelOuterRight = 1190;
    // Flush against the true bottom of the design canvas (DesignHeight=560) - direct user request
    // ("менюшка со всеми блоками в самом низу") after Название/Нос/статус/action buttons moved up
    // near the title (ShipEditorCanvas's own doc comment) freed up the space down here.
    private const int DevicePanelBottom = 556;
    private const int DeviceTabRowHeight = 28;
    // Direct user request ("чтобы не было пустот, максимально компактной") - the item row's own
    // viewport height is exactly label+icon+a small margin (see DeviceItemLabelHeight/IconSize
    // below), not a big leftover band - the old DeviceItemsTop=362 left ~80px of dead space below
    // every icon since the viewport height was sized to the WHOLE gap up to the tab row rather than
    // to the content actually drawn in it.
    private const int DeviceItemsTop = 440;
    private const int DeviceItemCellWidth = 132;
    private const int DeviceItemCellGap = 6;
    private const int DeviceItemIconSize = 56;
    private const int DeviceItemLabelHeight = 22;
    private const int DeviceScrollArrowWidth = 20;
    private const float DeviceTabFontScale = 0.55f;
    private const float DeviceItemFontScale = 0.55f;

    // Reset whenever the active tab changes (HandleDeviceTabClick) - a scroll position from a
    // 48-item tab makes no sense once the player switches to one with 4.
    private int _editorDeviceTabScroll;

    // Tab labels flow left-to-right and wrap onto a second row only if they'd overflow the panel's
    // own right edge - true for the tab row itself (9 labels, some long, comfortably fit one row at
    // DesignWidth's own 1200 units) but never actually needed by the item row below, which scrolls
    // horizontally instead of wrapping.
    private List<Rectangle> FlowLayout(IReadOnlyList<string> labels, float fontScale, int rowHeight, int top, int left, int right)
    {
        var rects = new List<Rectangle>(labels.Count);
        var x = left;
        var row = 0;
        foreach (var label in labels)
        {
            var width = (int)(_font.MeasureString(label).X * fontScale) + 10;
            if (x + width > right && x > left)
            {
                row++;
                x = left;
            }
            rects.Add(new Rectangle(x, top + row * rowHeight, width, rowHeight - 2));
            x += width + 4;
        }
        return rects;
    }

    private List<Rectangle> ComputeDeviceTabRects() =>
        FlowLayout(DeviceTabLabels, DeviceTabFontScale, DeviceTabRowHeight, DevicePanelBottom - DeviceTabRowHeight, DevicePanelLeft, DevicePanelRight);

    private int DeviceItemViewportHeight => (DevicePanelBottom - DeviceTabRowHeight - 4) - DeviceItemsTop;

    // Single row, fixed-width cells (unlike the tab row, an icon slot's own size shouldn't vary with
    // its label's length) - item i's un-scrolled X is purely index * step, then _editorDeviceTabScroll
    // slides the whole row left.
    private Rectangle GetDeviceItemViewport() => new(
        DevicePanelLeft + DeviceScrollArrowWidth + 4, DeviceItemsTop,
        DevicePanelRight - DevicePanelLeft - DeviceScrollArrowWidth * 2 - 8, DeviceItemViewportHeight);

    private int DeviceItemStep => DeviceItemCellWidth + DeviceItemCellGap;

    private Rectangle GetDeviceItemRect(int index)
    {
        var viewport = GetDeviceItemViewport();
        var x = viewport.X + index * DeviceItemStep - _editorDeviceTabScroll;
        return new Rectangle(x, viewport.Y, DeviceItemCellWidth, viewport.Height);
    }

    private int MaxDeviceItemScroll(int itemCount)
    {
        var contentWidth = itemCount * DeviceItemStep - DeviceItemCellGap;
        return Math.Max(0, contentWidth - GetDeviceItemViewport().Width);
    }

    private Rectangle GetDeviceScrollArrowRect(bool right)
    {
        var y = DeviceItemsTop + (DeviceItemViewportHeight - 40) / 2;
        var x = right ? DevicePanelRight - DeviceScrollArrowWidth : DevicePanelLeft;
        return new Rectangle(x, y, DeviceScrollArrowWidth, 40);
    }

    // The two mode buttons stack in the reserved right column, together spanning the panel's full
    // height (item row + tab row combined) - matches the reference screenshot's own tall two-part
    // toggle, not just a tab-row-height button.
    private Rectangle GetModeButtonRect(bool modifications)
    {
        var top = DeviceItemsTop - 6;
        var bottom = DevicePanelBottom + 2;
        var half = (bottom - top) / 2;
        var y = modifications ? top : top + half;
        return new Rectangle(DevicePanelRight + 6, y, DevicePanelOuterRight - DevicePanelRight - 12, half - 2);
    }

    // What the item row actually shows right now - Objects mode drills down through the 9 tabs,
    // Modifications mode is just its own flat 2-item list (Пол/Зона), no tabs needed.
    private PaletteItem[] CurrentItems => _editorPanelMode == EditorPanelMode.Objects
        ? DeviceTabItems(_editorDeviceTab)
        : ModificationItems;

    // Same design-space-to-backbuffer-pixels transform DrawEditorCanvas's own EditorCanvasDeviceRect
    // already uses for its scissor clip - ScissorRectangle wants real device pixels, not design units.
    private Rectangle DesignRectToDeviceRect(Rectangle designRect)
    {
        var topLeft = Vector2.Transform(new Vector2(designRect.X, designRect.Y), _renderScale);
        var bottomRight = Vector2.Transform(new Vector2(designRect.Right, designRect.Bottom), _renderScale);
        var viewport = GraphicsDevice.Viewport;
        var x = Math.Clamp((int)MathF.Round(topLeft.X), 0, viewport.Width);
        var y = Math.Clamp((int)MathF.Round(topLeft.Y), 0, viewport.Height);
        var right = Math.Clamp((int)MathF.Round(bottomRight.X), 0, viewport.Width);
        var bottom = Math.Clamp((int)MathF.Round(bottomRight.Y), 0, viewport.Height);
        return new Rectangle(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }

    private bool HandleDeviceTabClick(Point point)
    {
        if (GetModeButtonRect(modifications: true).Contains(point))
        {
            _editorPanelMode = EditorPanelMode.Modifications;
            _editorDeviceTabScroll = 0;
            return true;
        }
        if (GetModeButtonRect(modifications: false).Contains(point))
        {
            _editorPanelMode = EditorPanelMode.Objects;
            _editorDeviceTabScroll = 0;
            return true;
        }

        if (_editorPanelMode == EditorPanelMode.Objects)
        {
            var tabRects = ComputeDeviceTabRects();
            for (var i = 0; i < tabRects.Count; i++)
            {
                if (!tabRects[i].Contains(point))
                    continue;
                _editorDeviceTab = (DeviceTab)i;
                _editorDeviceTabScroll = 0;
                return true;
            }
        }

        var items = CurrentItems;
        var maxScroll = MaxDeviceItemScroll(items.Length);
        if (maxScroll > 0)
        {
            var page = 3 * DeviceItemStep;
            if (GetDeviceScrollArrowRect(right: false).Contains(point))
            {
                _editorDeviceTabScroll = Math.Max(0, _editorDeviceTabScroll - page);
                return true;
            }
            if (GetDeviceScrollArrowRect(right: true).Contains(point))
            {
                _editorDeviceTabScroll = Math.Min(maxScroll, _editorDeviceTabScroll + page);
                return true;
            }
        }

        var viewport = GetDeviceItemViewport();
        if (!viewport.Contains(point))
            return false;
        for (var i = 0; i < items.Length; i++)
        {
            if (!GetDeviceItemRect(i).Contains(point))
                continue;
            _editorTool = items[i].Tool;
            if (items[i].Kind is { } kind)
                _editorSelectedDeviceKind = kind;
            return true;
        }
        return false;
    }

    // Flat placeholder swatch, not bespoke art - the last-resort fallback for the handful of
    // entries DrawItemArt can't give real art to (Door/Terminal - no baked face or texture exists
    // for either today). A dark base plate with a smaller tinted inset and a thin gold frame, so
    // even these at least read as a distinct colour at a glance instead of pure text.
    private void DrawItemIcon(Rectangle rect, Color tint)
    {
        _spriteBatch.Draw(_pixel, rect, new Color(18, 22, 32));
        var inset = new Rectangle(rect.X + 5, rect.Y + 5, rect.Width - 10, rect.Height - 10);
        _spriteBatch.Draw(_pixel, inset, tint * 0.9f);
        DrawRectOutline(rect, DeviceGold * 0.8f, 1f);
    }

    private static Color ItemTint(PaletteItem item) => item.Kind is { } kind ? CustomDeviceCatalog.Tint(kind) : item.Tool switch
    {
        EditorTool.Wall => new Color(150, 150, 160),
        EditorTool.Door => new Color(160, 130, 90),
        EditorTool.Terminal => new Color(100, 180, 190),
        _ => Color.Gray,
    };

    // Direct user request ("картинки предметов были не просто разноцветные квадратики а были
    // настоящие похожие текстурки с видом сверху") - reuses REAL art the game already has instead
    // of inventing new: the Reactor's own real texture, a real wall panel texture for "Стена", and
    // DeviceSkin's baked device faces (Rendering/DeviceSkin.cs) - genuinely detailed painted-steel
    // machine art, not placeholder colour, already used for these exact kinds in real gameplay
    // (ShipRenderer.cs's own DrawDeviceFace calls). New kinds that share a real device's job but
    // have no fixture of their own yet (EngineSmall/Medium/Large/WarpEngine, ShieldGenerator Small/
    // Large, the combat tab's turrets/WeaponPanel, every storage variant) reuse that same category's
    // face rather than a bespoke one - still real detailed art, just not unique per size/tier.
    // Only Door and Terminal (no baked face or texture exists for either) fall back to the flat
    // tinted swatch below.
    private DeviceSkin? _deviceIconSkin;
    private DeviceSkin DeviceIconSkin => _deviceIconSkin ??= new DeviceSkin(GraphicsDevice);

    private static DeviceSkin.Face FaceForKind(CustomDeviceKind kind) => kind switch
    {
        CustomDeviceKind.Distribution => DeviceSkin.Face.Distribution,
        CustomDeviceKind.Battery => DeviceSkin.Face.Battery,
        CustomDeviceKind.Oxygen => DeviceSkin.Face.Oxygen,
        CustomDeviceKind.Engine or CustomDeviceKind.EngineSmall or CustomDeviceKind.EngineMedium
            or CustomDeviceKind.EngineLarge or CustomDeviceKind.WarpEngine => DeviceSkin.Face.Engine,
        CustomDeviceKind.Shields or CustomDeviceKind.ShieldGeneratorSmall or CustomDeviceKind.ShieldGeneratorLarge => DeviceSkin.Face.Shields,
        CustomDeviceKind.WeaponCharger or CustomDeviceKind.WeaponPanel or CustomDeviceKind.TurretBallistic
            or CustomDeviceKind.TurretLaser or CustomDeviceKind.TurretMachineGun or CustomDeviceKind.DefensiveTurret => DeviceSkin.Face.Weapons,
        CustomDeviceKind.Secondary => DeviceSkin.Face.Auxiliary,
        CustomDeviceKind.StorageRack or CustomDeviceKind.SmallStorage or CustomDeviceKind.LargeStorage
            or CustomDeviceKind.FuelRodStorage or CustomDeviceKind.Morgue or CustomDeviceKind.AmmoStorage => DeviceSkin.Face.Rack,
        CustomDeviceKind.Navigation => DeviceSkin.Face.Navigation,
        CustomDeviceKind.Helm => DeviceSkin.Face.Helm,
        CustomDeviceKind.SuitLocker => DeviceSkin.Face.Locker,
        CustomDeviceKind.Jukebox => DeviceSkin.Face.Jukebox,
        _ => DeviceSkin.Face.Generic,
    };

    private void DrawItemArt(Rectangle rect, PaletteItem item)
    {
        if (item.Kind == CustomDeviceKind.Reactor && _editorReactorTexture is { } reactorTex)
            _spriteBatch.Draw(reactorTex, rect, Color.White);
        else if (item.Tool == EditorTool.Wall && _editorWallVerticalTexture is { } wallTex)
            _spriteBatch.Draw(wallTex, rect, Color.White);
        else if (item.Kind is { } kind)
            _spriteBatch.Draw(DeviceIconSkin.Get(FaceForKind(kind), rect.Width, lit: true), rect, Color.White);
        else
        {
            DrawItemIcon(rect, ItemTint(item));
            return;
        }
        DrawRectOutline(rect, DeviceGold * 0.8f, 1f);
    }

    private void DrawDeviceTabs()
    {
        var panelTop = DeviceItemsTop - 6;
        _spriteBatch.Draw(_pixel, new Rectangle(DevicePanelLeft - 6, panelTop, DevicePanelOuterRight - DevicePanelLeft + 12, DevicePanelBottom + 2 - panelTop),
            DevicePanelBg * 0.95f);

        DrawModeButton(GetModeButtonRect(modifications: true), "МОДИФИКАЦИИ", _editorPanelMode == EditorPanelMode.Modifications);
        DrawModeButton(GetModeButtonRect(modifications: false), "ОБЪЕКТЫ", _editorPanelMode == EditorPanelMode.Objects);

        if (_editorPanelMode == EditorPanelMode.Objects)
        {
            var tabRects = ComputeDeviceTabRects();
            for (var i = 0; i < tabRects.Count; i++)
            {
                var rect = tabRects[i];
                var selected = (int)_editorDeviceTab == i;
                _spriteBatch.Draw(_pixel, rect, selected ? DeviceGold : DevicePanelBg);
                DrawRectOutline(rect, DeviceGold, 1f);
                _spriteBatch.DrawString(_font, DeviceTabLabels[i], new Vector2(rect.X + 5, rect.Y + 3),
                    selected ? new Color(24, 18, 6) : DeviceGold, 0f, Vector2.Zero, DeviceTabFontScale, SpriteEffects.None, 0f);
            }
        }

        var items = CurrentItems;
        var maxScroll = MaxDeviceItemScroll(items.Length);
        _editorDeviceTabScroll = Math.Clamp(_editorDeviceTabScroll, 0, maxScroll);
        if (maxScroll > 0)
        {
            DrawScrollArrow(GetDeviceScrollArrowRect(right: false), "<", _editorDeviceTabScroll > 0);
            DrawScrollArrow(GetDeviceScrollArrowRect(right: true), ">", _editorDeviceTabScroll < maxScroll);
        }

        // Clipped to the viewport so a partially-scrolled cell at either edge cuts off cleanly
        // instead of drawing over the scroll arrows or past the panel's own border.
        var viewport = GetDeviceItemViewport();
        var previousScissor = GraphicsDevice.ScissorRectangle;
        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = DesignRectToDeviceRect(viewport);
        _spriteBatch.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true }, transformMatrix: _renderScale);

        for (var i = 0; i < items.Length; i++)
        {
            var rect = GetDeviceItemRect(i);
            if (rect.Right < viewport.X || rect.X > viewport.Right)
                continue; // fully outside the visible strip - skip drawing it at all

            var item = items[i];
            var iconRect = new Rectangle(rect.X + (rect.Width - DeviceItemIconSize) / 2, rect.Y + DeviceItemLabelHeight, DeviceItemIconSize, DeviceItemIconSize);
            // A tool-only entry (Wall/Door/Terminal) reads as selected whenever its tool is active,
            // regardless of _editorSelectedDeviceKind - the same "which button matches the current
            // state" convention the old flat device row already used for real devices.
            var selected = item.Kind is { } kind
                ? _editorTool == EditorTool.Device && kind == _editorSelectedDeviceKind
                : _editorTool == item.Tool;

            DrawItemArt(iconRect, item);
            if (selected)
                DrawRectOutline(new Rectangle(iconRect.X - 3, iconRect.Y - 3, iconRect.Width + 6, iconRect.Height + 6), Color.White, 2f);

            var textSize = _font.MeasureString(item.Label) * DeviceItemFontScale;
            _spriteBatch.DrawString(_font, item.Label, new Vector2(rect.X + (rect.Width - textSize.X) / 2, rect.Y),
                DeviceGold, 0f, Vector2.Zero, DeviceItemFontScale, SpriteEffects.None, 0f);
        }

        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = previousScissor;
        _spriteBatch.Begin(transformMatrix: _renderScale);
    }

    private void DrawModeButton(Rectangle rect, string label, bool selected)
    {
        _spriteBatch.Draw(_pixel, rect, selected ? DeviceGold : DevicePanelBg);
        DrawRectOutline(rect, DeviceGold, 2f);
        var textSize = _font.MeasureString(label) * DeviceTabFontScale;
        _spriteBatch.DrawString(_font, label, new Vector2(rect.X + (rect.Width - textSize.X) / 2, rect.Y + (rect.Height - textSize.Y) / 2),
            selected ? new Color(24, 18, 6) : DeviceGold, 0f, Vector2.Zero, DeviceTabFontScale, SpriteEffects.None, 0f);
    }

    private void DrawScrollArrow(Rectangle rect, string glyph, bool enabled)
    {
        _spriteBatch.Draw(_pixel, rect, DevicePanelBg);
        DrawRectOutline(rect, enabled ? DeviceGold : DeviceGold * 0.4f, 1f);
        var textSize = _font.MeasureString(glyph) * 0.6f;
        _spriteBatch.DrawString(_font, glyph, new Vector2(rect.X + (rect.Width - textSize.X) / 2, rect.Y + (rect.Height - textSize.Y) / 2),
            enabled ? DeviceGold : DeviceGold * 0.4f, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }
}
