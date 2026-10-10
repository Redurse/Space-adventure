using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// What a click inside the trade window turned out to be.
public enum TradeClick
{
    None,     // outside the window: the caller treats it like any other click on the station floor
    Handled,  // something in the window (a card, a tab, a cart button): nothing more to do
    Close,    // the window's own close button
    Confirm,  // "Подтвердить": the cart's commands are waiting in TradeWindow.TakeCommands()
}

// The Trader's store, laid out like Barotrauma's (direct user request): a big window with the shop on the
// left - category list, Buy/Sell tabs and a grid of item cards - and the shopping cart on the right with
// its totals, a balance and Confirm/Clear. Click a card to put one in the cart, use the cart's - and + to
// change the amount, nothing is paid until you press Confirm (TradeCart has the rules). The mouse wheel
// scrolls the grid. The window owns only its own state (tab, category, scroll, cart); Game1 feeds it
// clicks/wheel and sends the commands it produces.
public sealed class TradeWindow
{
    public enum Tab { Buy, Sell }
    public enum Category { All, Tools, Supplies, Weapons, Electronics, Materials }

    private static readonly (Category Category, string Label)[] Categories =
    {
        (Category.All, "Все"), (Category.Tools, "Инструменты"), (Category.Supplies, "Снабжение"),
        (Category.Weapons, "Оружие"), (Category.Electronics, "Электроника"), (Category.Materials, "Материалы"),
    };

    // ---- layout ----
    // The game's design space is 1200x560 (Game1.DesignWidth/Height, scaled up to the real screen), so the
    // whole window has to fit in that - it is NOT a 1920x1080 canvas.
    private const int WindowX = 16, WindowY = 14, WindowW = 1168, WindowH = 532;
    private const int HeaderH = 40, Pad = 10;
    private const int SidebarW = 136, CategoryH = 34, CategoryGap = 4;
    private const int TabW = 140, TabH = 28;
    private const int Columns = 3, CardW = 232, CardH = 52, CardGap = 6, VisibleRows = 7;
    private const int CartW = 278, CartRowH = 28, MaxBuyRows = 5, MaxSellRows = 3;

    public static Rectangle WindowRect => new(WindowX, WindowY, WindowW, WindowH);
    private static Rectangle CloseRect => new(WindowX + WindowW - 38, WindowY + 6, 28, 28);
    private static int BodyY => WindowY + HeaderH + Pad;
    private static Rectangle CategoryRect(int i) => new(WindowX + Pad, BodyY + i * (CategoryH + CategoryGap), SidebarW, CategoryH);
    private static int ShopX => WindowX + Pad + SidebarW + 12;
    private static Rectangle TabRect(Tab tab) => new(ShopX + (tab == Tab.Buy ? 0 : TabW + 6), BodyY, TabW, TabH);
    private static int GridY => BodyY + TabH + 8;
    private static int GridRight => ShopX + Columns * CardW + (Columns - 1) * CardGap;
    // index is the card's position among the shown items; the grid is scrolled by whole rows.
    private static Rectangle CardRect(int index, int scrollRows) =>
        new(ShopX + (index % Columns) * (CardW + CardGap), GridY + (index / Columns - scrollRows) * (CardH + CardGap), CardW, CardH);
    private static int CartX => GridRight + 16;
    private static Rectangle CartRect => new(CartX, BodyY, CartW, WindowH - HeaderH - Pad * 2);
    private static int CartBuyRowsY => CartRect.Y + 46;
    private static int CartSellRowsY => CartRect.Y + 212;
    private static Rectangle CartRow(int baseY, int row) => new(CartRect.X + 4, baseY + row * CartRowH, CartW - 8, CartRowH - 2);
    private static Rectangle MinusRect(Rectangle row) => new(row.X + 138, row.Y + 3, 20, 20);
    private static Rectangle PlusRect(Rectangle row) => new(row.X + 186, row.Y + 3, 20, 20);
    private static Rectangle ConfirmRect => new(CartRect.X + 8, CartRect.Bottom - 78, CartW - 16, 34);
    private static Rectangle ClearRect => new(CartRect.X + 8, CartRect.Bottom - 38, CartW - 16, 28);

    private static readonly Color Gold = new(214, 178, 112);
    private static readonly Color PanelFill = new(16, 20, 28);
    private static readonly Color CardFill = new(30, 36, 48);
    private static readonly Color CardHover = new(46, 56, 74);

    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;
    private List<(ItemType? Buy, int SellSlot)>? _commands;
    private int _scrollRows;

    public Tab CurrentTab { get; private set; } = Tab.Buy;
    public Category CurrentCategory { get; private set; } = Category.All;
    public TradeCart Cart { get; } = new();

    public TradeWindow(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _font = font;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    // The commands a Confirm click produced, once - then it is empty again.
    public List<(ItemType? Buy, int SellSlot)> TakeCommands()
    {
        var commands = _commands ?? new List<(ItemType?, int)>();
        _commands = null;
        return commands;
    }

    // Mouse wheel over the window scrolls the item grid by whole rows (notches > 0 = down). The limit is applied
    // against the shown list when it is next drawn or clicked.
    public void Scroll(int notches) => _scrollRows = Math.Max(0, _scrollRows + notches);

    // A fresh start whenever you walk up to a trader: nothing left over in the cart from last time.
    public void Reset()
    {
        Cart.Clear();
        CurrentTab = Tab.Buy;
        CurrentCategory = Category.All;
        _scrollRows = 0;
        _commands = null;
    }

    private int ClampScroll(int shownCount)
    {
        var rows = (shownCount + Columns - 1) / Columns;
        _scrollRows = Math.Clamp(_scrollRows, 0, Math.Max(0, rows - VisibleRows));
        return _scrollRows;
    }

    public static Category CategoryOf(ItemType item)
    {
        if (ComponentDefinitions.ComponentKindFor(item) is not null)
            return Category.Electronics;
        return item switch
        {
            ItemType.Wrench or ItemType.Screwdriver or ItemType.WeldingTool or ItemType.Cutter or ItemType.GoshaScrewdriver
                or ItemType.WireSpool => Category.Tools,
            ItemType.WeldingTank or ItemType.OxygenTank or ItemType.Spacesuit or ItemType.MedKit or ItemType.FuelRod
                or ItemType.Hyperium => Category.Supplies,
            ItemType.Rifle or ItemType.LaserRifle or ItemType.Knife or ItemType.Axe or ItemType.AmmoCrate or ItemType.Magazine
                => Category.Weapons,
            _ => Category.Materials,
        };
    }

    // ---- what is on offer ----

    private static Dictionary<ItemType, int> OwnedCounts(CharacterState? me)
    {
        var owned = new Dictionary<ItemType, int>();
        if (me?.Inventory is { } inventory)
            foreach (var item in inventory.MainSlots)
                if (item is { } type && TradeCatalog.Find(type) is not null)
                    owned[type] = owned.GetValueOrDefault(type) + 1;
        return owned;
    }

    private List<ItemType> ShownItems(Dictionary<ItemType, int> owned)
    {
        IEnumerable<ItemType> source = CurrentTab == Tab.Buy
            ? TradeCatalog.Goods.Where(TradeCatalog.IsForSale).Select(g => g.Item)
            : owned.Keys;
        if (CurrentCategory != Category.All)
            source = source.Where(i => CategoryOf(i) == CurrentCategory);
        return source.ToList();
    }

    private static int FreeSlots(CharacterState? me) =>
        me?.Inventory is { } inventory ? inventory.MainSlots.Count(s => s is null) : 0;

    private void SyncPrices(WorldSnapshot snapshot)
    {
        var point = snapshot.GalaxyPoints.FirstOrDefault(p => p.Id == snapshot.Voyage.DockedPointId);
        var standing = point is null ? 0 : snapshot.FactionStandings.FirstOrDefault(f => f.Faction == point.Faction)?.Standing ?? 0;
        Cart.PriceMultiplier = point is null ? 1f : FactionDefinitions.PriceMultiplier(point.Faction, standing);
        Cart.MiningStation = point?.StationKind == StationKind.Mining;
    }

    // ---- input ----

    public TradeClick HandleClick(WorldSnapshot snapshot, int playerId, Point mouse)
    {
        if (!WindowRect.Contains(mouse))
            return TradeClick.None;

        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId);
        var owned = OwnedCounts(me);
        SyncPrices(snapshot);
        Cart.ClampSellTo(owned);

        if (CloseRect.Contains(mouse))
            return TradeClick.Close;

        for (var i = 0; i < Categories.Length; i++)
            if (CategoryRect(i).Contains(mouse))
            {
                CurrentCategory = Categories[i].Category;
                _scrollRows = 0;
                return TradeClick.Handled;
            }

        foreach (var tab in new[] { Tab.Buy, Tab.Sell })
            if (TabRect(tab).Contains(mouse))
            {
                CurrentTab = tab;
                _scrollRows = 0;
                return TradeClick.Handled;
            }

        var shown = ShownItems(owned);
        var scroll = ClampScroll(shown.Count);
        for (var i = scroll * Columns; i < shown.Count && i < (scroll + VisibleRows) * Columns; i++)
            if (CardRect(i, scroll).Contains(mouse))
            {
                if (CurrentTab == Tab.Buy)
                    Cart.AddBuy(shown[i]);
                else
                    Cart.AddSell(shown[i], owned.GetValueOrDefault(shown[i]));
                return TradeClick.Handled;
            }

        var buyLines = Cart.Buy.Keys.Take(MaxBuyRows).ToList();
        for (var row = 0; row < buyLines.Count; row++)
        {
            var rect = CartRow(CartBuyRowsY, row);
            if (MinusRect(rect).Contains(mouse)) { Cart.RemoveBuy(buyLines[row]); return TradeClick.Handled; }
            if (PlusRect(rect).Contains(mouse)) { Cart.AddBuy(buyLines[row]); return TradeClick.Handled; }
        }
        var sellLines = Cart.Sell.Keys.Take(MaxSellRows).ToList();
        for (var row = 0; row < sellLines.Count; row++)
        {
            var rect = CartRow(CartSellRowsY, row);
            if (MinusRect(rect).Contains(mouse)) { Cart.RemoveSell(sellLines[row]); return TradeClick.Handled; }
            if (PlusRect(rect).Contains(mouse)) { Cart.AddSell(sellLines[row], owned.GetValueOrDefault(sellLines[row])); return TradeClick.Handled; }
        }

        if (ClearRect.Contains(mouse))
        {
            Cart.Clear();
            return TradeClick.Handled;
        }
        if (ConfirmRect.Contains(mouse) && Cart.Problem(snapshot.Credits, FreeSlots(me)) is null)
        {
            _commands = Cart.BuildCommands(me?.Inventory?.MainSlots ?? Array.Empty<ItemType?>());
            Cart.Clear();
            return TradeClick.Confirm;
        }
        return TradeClick.Handled; // inside the window but on nothing: swallowed, never falls through to the floor below
    }

    // ---- drawing ----

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int playerId, string traderName, Point mouse)
    {
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId);
        var owned = OwnedCounts(me);
        SyncPrices(snapshot);
        Cart.ClampSellTo(owned);

        Fill(spriteBatch, WindowRect, PanelFill * 0.97f);
        Outline(spriteBatch, WindowRect, Gold, 2);
        Fill(spriteBatch, new Rectangle(WindowX, WindowY, WindowW, HeaderH), new Color(24, 30, 42));
        Text(spriteBatch, $"МАГАЗИН - {traderName}", new Vector2(WindowX + 14, WindowY + 10), Gold, 0.72f);
        var creditsText = $"Кредиты: {snapshot.Credits}";
        Text(spriteBatch, creditsText, new Vector2(WindowX + WindowW - 60 - TextWidth(creditsText, 0.68f), WindowY + 11), Color.LightGreen, 0.68f);
        DrawButton(spriteBatch, CloseRect, "x", true, CloseRect.Contains(mouse), new Color(150, 60, 56));

        for (var i = 0; i < Categories.Length; i++)
        {
            var rect = CategoryRect(i);
            var selected = Categories[i].Category == CurrentCategory;
            Fill(spriteBatch, rect, selected ? new Color(54, 64, 86) : rect.Contains(mouse) ? CardHover : CardFill);
            if (selected)
                Fill(spriteBatch, new Rectangle(rect.X, rect.Y, 3, rect.Height), Gold);
            Text(spriteBatch, Categories[i].Label, new Vector2(rect.X + 12, rect.Y + 8), selected ? Color.White : Color.LightGray, 0.55f);
        }

        foreach (var tab in new[] { Tab.Buy, Tab.Sell })
        {
            var rect = TabRect(tab);
            var selected = tab == CurrentTab;
            Fill(spriteBatch, rect, selected ? new Color(70, 58, 36) : rect.Contains(mouse) ? CardHover : CardFill);
            Outline(spriteBatch, rect, selected ? Gold : new Color(70, 78, 96), selected ? 2 : 1);
            var label = tab == Tab.Buy ? "КУПИТЬ" : "ПРОДАТЬ";
            Text(spriteBatch, label, new Vector2(rect.X + (rect.Width - TextWidth(label, 0.58f)) / 2f, rect.Y + 5), selected ? Gold : Color.LightGray, 0.58f);
        }

        var shown = ShownItems(owned);
        var scroll = ClampScroll(shown.Count);
        if (shown.Count == 0)
            Text(spriteBatch, CurrentTab == Tab.Buy ? "Здесь ничего нет." : "У вас нечего продать в этой категории.",
                new Vector2(ShopX + 6, GridY + 10), Color.Gray, 0.55f);
        for (var i = scroll * Columns; i < shown.Count && i < (scroll + VisibleRows) * Columns; i++)
            DrawCard(spriteBatch, shown[i], CardRect(i, scroll), owned, snapshot.Credits, mouse);
        DrawScrollbar(spriteBatch, shown.Count, scroll);

        DrawCart(spriteBatch, snapshot, me, owned, mouse);
    }

    // A thin bar beside the grid, only when there is more than fits.
    private void DrawScrollbar(SpriteBatch spriteBatch, int shownCount, int scroll)
    {
        var rows = (shownCount + Columns - 1) / Columns;
        if (rows <= VisibleRows)
            return;
        var trackHeight = VisibleRows * (CardH + CardGap) - CardGap;
        var track = new Rectangle(GridRight + 5, GridY, 5, trackHeight);
        Fill(spriteBatch, track, new Color(36, 42, 56));
        var thumbHeight = Math.Max(24, trackHeight * VisibleRows / rows);
        var thumbY = track.Y + (trackHeight - thumbHeight) * scroll / Math.Max(1, rows - VisibleRows);
        Fill(spriteBatch, new Rectangle(track.X, thumbY, track.Width, thumbHeight), Gold * 0.8f);
    }

    private void DrawCard(SpriteBatch spriteBatch, ItemType item, Rectangle rect, Dictionary<ItemType, int> owned, int credits, Point mouse)
    {
        var buying = CurrentTab == Tab.Buy;
        var price = buying ? Cart.BuyPriceOf(item) : Cart.SellPriceOf(item);
        var hover = rect.Contains(mouse);
        var rim = ItemIcons.CategoryColor(item);
        Fill(spriteBatch, rect, hover ? CardHover : CardFill);
        Fill(spriteBatch, new Rectangle(rect.X, rect.Y, 3, rect.Height), rim);
        ItemIcons.DrawIconOrChip(spriteBatch, _pixel, _font, item, new Vector2(rect.X + 28, rect.Y + rect.Height / 2f), 32f);
        Text(spriteBatch, Fit(ItemIcons.WorldLabel(item), 0.55f, rect.Width - 56), new Vector2(rect.X + 50, rect.Y + 6), Color.White, 0.55f);

        var affordable = !buying || credits + Cart.Net >= price;
        Text(spriteBatch, $"{price} кр.", new Vector2(rect.X + 50, rect.Y + 29), buying ? (affordable ? Color.LightGreen : Color.IndianRed) : Color.Khaki, 0.55f);

        var inCart = buying ? Cart.Buy.GetValueOrDefault(item) : Cart.Sell.GetValueOrDefault(item);
        var carrying = owned.GetValueOrDefault(item);
        var note = inCart > 0 ? $"в корзине: {inCart}" : buying ? (carrying > 0 ? $"у вас: {carrying}" : "") : $"у вас: {carrying}";
        if (note.Length > 0)
            Text(spriteBatch, note, new Vector2(rect.Right - 8 - TextWidth(note, 0.45f), rect.Y + 32), inCart > 0 ? Gold : Color.Gray, 0.45f);
        if (inCart > 0)
            Outline(spriteBatch, rect, Gold * 0.9f, 1);
    }

    private void DrawCart(SpriteBatch spriteBatch, WorldSnapshot snapshot, CharacterState? me, Dictionary<ItemType, int> owned, Point mouse)
    {
        var cart = CartRect;
        Fill(spriteBatch, cart, new Color(22, 27, 38));
        Outline(spriteBatch, cart, new Color(70, 78, 96), 1);
        Text(spriteBatch, "КОРЗИНА", new Vector2(cart.X + 10, cart.Y + 6), Gold, 0.65f);

        Text(spriteBatch, "Покупка", new Vector2(cart.X + 10, cart.Y + 28), Color.LightGray, 0.5f);
        DrawCartLines(spriteBatch, Cart.Buy, CartBuyRowsY, MaxBuyRows, buying: true, mouse);
        Text(spriteBatch, "Продажа", new Vector2(cart.X + 10, cart.Y + 194), Color.LightGray, 0.5f);
        DrawCartLines(spriteBatch, Cart.Sell, CartSellRowsY, MaxSellRows, buying: false, mouse);

        var y = cart.Y + 306;
        DrawTotal(spriteBatch, "Покупка", -Cart.BuyTotal, y, Color.IndianRed);
        DrawTotal(spriteBatch, "Продажа", Cart.SellTotal, y + 20, Color.LightGreen);
        var after = snapshot.Credits + Cart.Net;
        DrawTotal(spriteBatch, "Кредиты после", after, y + 46, after < 0 ? Color.IndianRed : Color.White);

        var problem = Cart.Problem(snapshot.Credits, FreeSlots(me));
        if (problem is not null && !Cart.IsEmpty)
            Text(spriteBatch, Fit(problem, 0.5f, CartW - 20), new Vector2(cart.X + 10, ConfirmRect.Y - 18), Color.IndianRed, 0.5f);

        DrawButton(spriteBatch, ConfirmRect, "ПОДТВЕРДИТЬ", problem is null, ConfirmRect.Contains(mouse), new Color(48, 120, 70));
        DrawButton(spriteBatch, ClearRect, "Очистить", !Cart.IsEmpty, ClearRect.Contains(mouse), new Color(70, 78, 96));
    }

    private void DrawCartLines(SpriteBatch spriteBatch, Dictionary<ItemType, int> lines, int baseY, int maxRows, bool buying, Point mouse)
    {
        var row = 0;
        foreach (var (item, count) in lines)
        {
            if (row >= maxRows)
            {
                Text(spriteBatch, $"... и ещё {lines.Count - maxRows}", new Vector2(CartRect.X + 10, baseY + maxRows * CartRowH), Color.Gray, 0.45f);
                break;
            }
            var rect = CartRow(baseY, row);
            Fill(spriteBatch, rect, CardFill);
            ItemIcons.DrawIconOrChip(spriteBatch, _pixel, _font, item, new Vector2(rect.X + 14, rect.Y + rect.Height / 2f), 20f);
            Text(spriteBatch, Fit(ItemIcons.WorldLabel(item), 0.45f, 98), new Vector2(rect.X + 30, rect.Y + 7), Color.White, 0.45f);
            var minus = MinusRect(rect);
            var plus = PlusRect(rect);
            DrawButton(spriteBatch, minus, "-", true, minus.Contains(mouse), new Color(70, 78, 96));
            var countText = count.ToString();
            Text(spriteBatch, countText, new Vector2(minus.Right + (plus.X - minus.Right - TextWidth(countText, 0.5f)) / 2f, rect.Y + 6), Color.White, 0.5f);
            DrawButton(spriteBatch, plus, "+", true, plus.Contains(mouse), new Color(70, 78, 96));
            var lineTotal = count * (buying ? Cart.BuyPriceOf(item) : Cart.SellPriceOf(item));
            var totalText = lineTotal.ToString();
            Text(spriteBatch, totalText, new Vector2(rect.Right - 6 - TextWidth(totalText, 0.45f), rect.Y + 7), buying ? Color.IndianRed : Color.LightGreen, 0.45f);
            row++;
        }
    }

    private void DrawTotal(SpriteBatch spriteBatch, string label, int value, int y, Color color)
    {
        Text(spriteBatch, label, new Vector2(CartRect.X + 10, y), Color.LightGray, 0.52f);
        var text = label == "Кредиты после" ? value.ToString() : value.ToString("+#;-#;0");
        Text(spriteBatch, text, new Vector2(CartRect.Right - 10 - TextWidth(text, 0.58f), y), color, 0.58f);
    }

    // ---- small drawing helpers ----

    private void DrawButton(SpriteBatch spriteBatch, Rectangle rect, string label, bool enabled, bool hover, Color color)
    {
        var fill = !enabled ? new Color(38, 40, 46) : hover ? Color.Lerp(color, Color.White, 0.2f) : color;
        Fill(spriteBatch, rect, fill);
        Outline(spriteBatch, rect, enabled ? Color.White * 0.35f : new Color(60, 62, 70), 1);
        var scale = rect.Height >= 30 ? 0.6f : 0.5f;
        Text(spriteBatch, label, new Vector2(rect.X + (rect.Width - TextWidth(label, scale)) / 2f, rect.Y + (rect.Height - _font.MeasureString("Ag").Y * scale) / 2f),
            enabled ? Color.White : Color.Gray, scale);
    }

    private void Fill(SpriteBatch spriteBatch, Rectangle rect, Color color) => spriteBatch.Draw(_pixel, rect, color);

    private void Outline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness) =>
        ShipRenderer.DrawRectOutline(spriteBatch, _pixel, rect, color, thickness);

    private void Text(SpriteBatch spriteBatch, string text, Vector2 position, Color color, float scale) =>
        spriteBatch.DrawString(_font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

    private float TextWidth(string text, float scale) => _font.MeasureString(text).X * scale;

    // Shortens text with an ellipsis until it fits maxWidth at this scale.
    private string Fit(string text, float scale, float maxWidth)
    {
        if (TextWidth(text, scale) <= maxWidth)
            return text;
        while (text.Length > 1 && TextWidth(text + "…", scale) > maxWidth)
            text = text[..^1];
        return text + "…";
    }
}
