using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The Barotrauma-style "open the junction box" screen: one component's pins, inputs down the left
// edge and outputs down the right, each with its connector dot and every wire on it listed as its
// own line ("-> щиток.out-0").
//
// W5 of wiring-plan.md made it the place wiring is actually done (it used to be read-only):
//  - with a wire spool in hand, clicking a pin starts a wire there, or finishes the one being laid
//    (World.Wiring.cs's HandlePinInteract - the same command a click on the pin in the world sends);
//  - with a screwdriver in hand, clicking one of a pin's wires takes that wire off and gives the
//    spool back (HandleRemoveWire).
// Layout() is shared by drawing and by Game1's click handling, so a click lands on exactly what it
// looks like it should.
public sealed class ConnectionsPanel
{
    public const int Width = 460;

    private const int HeaderHeight = 22;
    private const int RowHeight = 18;
    private const int PinGap = 4;
    private const int DotSize = 8;
    private const float TextScale = 0.42f;
    private const int HintHeight = 18;

    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;

    public ConnectionsPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _font = font;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    // One clickable line of the panel: a pin's own line (WireId null) or one wire on it.
    // W7: a settings row (below the pins) - IsSetting items are the label of a setting, or one of its buttons when Setting is not None.
    public readonly record struct Item(Rectangle Rect, PinRef Pin, PinKind Kind, bool IsInput, string? WireId, string Text,
        bool IsSetting = false, ComponentSettingAction Setting = ComponentSettingAction.None);

    private const int SettingRowHeight = 22;
    private const int SettingButtonWidth = 26;

    public (Rectangle Panel, IReadOnlyList<Item> Items) Layout(WorldSnapshot snapshot, string componentId, Rectangle bounds)
    {
        var items = new List<Item>();
        var component = snapshot.Wiring.Components.FirstOrDefault(c => c.Id == componentId);
        if (component is null)
            return (Rectangle.Empty, items);

        var pins = ComponentRenderer.PinsFor(component, snapshot).ToList();
        var columnWidth = bounds.Width / 2 - 12;
        var top = bounds.Y + HeaderHeight + 18;
        var bottom = top;
        foreach (var isInput in new[] { true, false })
        {
            var y = top;
            var x = isInput ? bounds.X + 8 : bounds.X + bounds.Width / 2 + 4;
            foreach (var (pinId, kind) in pins.Where(p => (p.Kind is PinKind.PowerIn or PinKind.SignalIn) == isInput))
            {
                var pinRef = new PinRef(componentId, pinId);
                var wires = snapshot.Wiring.Wires.Where(w => w.FromPin == pinRef || w.ToPin == pinRef).ToList();
                // W6: a signal output shows the value it's putting out right now.
                var value = snapshot.Wiring.PinSignals?.FirstOrDefault(s => s.Pin == pinRef)?.Value;
                items.Add(new Item(new Rectangle(x, y, columnWidth, RowHeight), pinRef, kind, isInput, null,
                    $"{pinId}  ({wires.Count}/{WireRules.MaxWiresPerPin})" + (value is null ? "" : $"  = {value}")));
                y += RowHeight;
                foreach (var wire in wires)
                {
                    var other = wire.FromPin == pinRef ? wire.ToPin : wire.FromPin;
                    items.Add(new Item(new Rectangle(x + 14, y, columnWidth - 14, RowHeight), pinRef, kind, isInput, wire.Id,
                        $"-> {ComponentRenderer.PinLabel(snapshot, other)}"));
                    y += RowHeight;
                }
                y += PinGap;
            }
            bottom = Math.Max(bottom, y);
        }

        bottom = AddSettingRows(items, component, bounds, bottom);

        var height = Math.Max(bounds.Height, bottom - bounds.Y + HintHeight + 8);
        return (new Rectangle(bounds.X, bounds.Y, bounds.Width, height), items);
    }

    // The part's settings (ComponentSettings): time, a number, an operation - each a labelled row with its buttons. Returns the new bottom.
    // While a part's text is being typed, the text so far (set by Game1 every frame); null otherwise.
    public string? EditingComponentId { get; set; }
    public string EditingText { get; set; } = "";

    private int AddSettingRows(List<Item> items, Component component, Rectangle bounds, int bottom)
    {
        if (!ComponentSettings.HasSettings(component.Kind))
            return bottom;

        var y = bottom + 6;
        var right = bounds.Right - 8;
        var plus = new Rectangle(right - SettingButtonWidth, 0, SettingButtonWidth, SettingRowHeight - 4);
        var minus = new Rectangle(plus.X - SettingButtonWidth - 4, 0, SettingButtonWidth, SettingRowHeight - 4);

        void Row(string text, ComponentSettingAction down, ComponentSettingAction up)
        {
            var label = new Rectangle(bounds.X + 8, y, minus.X - bounds.X - 12, SettingRowHeight - 4);
            items.Add(new Item(label, default, PinKind.SignalIn, true, null, text, IsSetting: true));
            items.Add(new Item(minus with { Y = y }, default, PinKind.SignalIn, true, null, "-", IsSetting: true, Setting: down));
            items.Add(new Item(plus with { Y = y }, default, PinKind.SignalIn, true, null, "+", IsSetting: true, Setting: up));
            y += SettingRowHeight;
        }

        if (ComponentSettings.HasTime(component.Kind))
            Row($"{ComponentSettings.TimeLabel(component.Kind)}: {component.TimerSeconds:0.0} с", ComponentSettingAction.TimeDown, ComponentSettingAction.TimeUp);
        if (ComponentSettings.HasValue(component.Kind))
            Row($"{ComponentSettings.ValueLabel(component.Kind)}: {component.Value:0.##}", ComponentSettingAction.ValueDown, ComponentSettingAction.ValueUp);
        if (ComponentSettings.HasText(component.Kind))
        {
            var editing = EditingComponentId == component.Id;
            var shown = editing ? EditingText + "_" : string.IsNullOrEmpty(component.Text) ? "(пусто)" : component.Text;
            var hint = editing ? "  (Enter - принять)" : component.Kind == ComponentKind.Keyboard ? "  (клик - ввести и отправить)" : "  (клик - ввести)";
            var label = new Rectangle(bounds.X + 8, y, bounds.Width - 16, SettingRowHeight - 4);
            items.Add(new Item(label, default, PinKind.SignalIn, true, null, $"{ComponentSettings.TextLabel(component.Kind)}: {shown}{hint}",
                IsSetting: true, Setting: ComponentSettingAction.EditText));
            y += SettingRowHeight;
        }
        if (ComponentSettings.Modes(component.Kind) is not null)
        {
            // One wide button: every click moves on to the next operation.
            var label = new Rectangle(bounds.X + 8, y, bounds.Width - 16, SettingRowHeight - 4);
            items.Add(new Item(label, default, PinKind.SignalIn, true, null, $"Режим: {ComponentSettings.ModeLabel(component)}  (клик - сменить)",
                IsSetting: true, Setting: ComponentSettingAction.ModeNext));
            y += SettingRowHeight;
        }
        return y;
    }

    public Item? HitTest(WorldSnapshot snapshot, string componentId, Rectangle bounds, Point mouse) =>
        Layout(snapshot, componentId, bounds).Items.Cast<Item?>().FirstOrDefault(i => i!.Value.Rect.Contains(mouse));

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, string componentId, Rectangle bounds, float totalSeconds,
        PinRef? layingFrom = null, bool holdingSpool = false, bool holdingScrewdriver = false, Point? mouse = null)
    {
        var (rect, items) = Layout(snapshot, componentId, bounds);
        if (rect == Rectangle.Empty)
            return;

        // The housing is drawn after the layout: this panel sizes itself from its own rows, and the
        // caller passes height 0 meaning "you work it out".
        DevicePanelChrome.Draw(spriteBatch, _font, rect,
            ComponentRenderer.ComponentLabel(snapshot, componentId).ToUpperInvariant(), "CN-07",
            new Color(148, 196, 236), totalSeconds);

        if (items.Count == 0)
        {
            spriteBatch.DrawString(_font, "Нет проводных контактов.", new Vector2(rect.X + 8, rect.Y + HeaderHeight + 6),
                Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return;
        }

        spriteBatch.DrawString(_font, "ВХОДЫ", new Vector2(rect.X + 8, rect.Y + HeaderHeight + 2), Color.Gray, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, "ВЫХОДЫ", new Vector2(rect.X + rect.Width / 2 + 4, rect.Y + HeaderHeight + 2),
            Color.Gray, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);

        foreach (var item in items)
        {
            if (item.IsSetting)
            {
                DrawSettingItem(spriteBatch, item, mouse);
                continue;
            }
            var hovered = mouse is { } m && item.Rect.Contains(m)
                && (item.WireId is null ? holdingSpool : holdingScrewdriver);
            if (hovered)
                spriteBatch.Draw(_pixel, item.Rect, Color.White * 0.08f);

            if (item.WireId is null)
                DrawPinLine(spriteBatch, snapshot, item, layingFrom == item.Pin, totalSeconds);
            else
                DrawWireLine(spriteBatch, snapshot, item, hovered);
        }

        var hint = holdingSpool
            ? layingFrom is null ? "Катушка: клик по контакту - начать провод" : "Катушка: клик по контакту - закончить провод (ПКМ - отмена)"
            : holdingScrewdriver ? "Отвёртка: клик по проводу - снять его (катушка вернётся)" : "Возьмите катушку провода, чтобы подключать";
        spriteBatch.DrawString(_font, hint, new Vector2(rect.X + 8, rect.Bottom - HintHeight - 2),
            new Color(170, 170, 120), 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
    }

    private void DrawSettingItem(SpriteBatch spriteBatch, Item item, Point? mouse)
    {
        if (item.Setting == ComponentSettingAction.None)
        {
            // A label: the setting's name and its current value.
            spriteBatch.DrawString(_font, item.Text, new Vector2(item.Rect.X, item.Rect.Y + 3), new Color(190, 215, 235), 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
            return;
        }

        var hovered = mouse is { } m && item.Rect.Contains(m);
        spriteBatch.Draw(_pixel, item.Rect, hovered ? new Color(70, 90, 110) : new Color(40, 52, 64));
        var size = _font.MeasureString(item.Text) * TextScale;
        spriteBatch.DrawString(_font, item.Text, new Vector2(item.Rect.Center.X - size.X / 2f, item.Rect.Center.Y - size.Y / 2f),
            Color.White, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
    }

    private void DrawPinLine(SpriteBatch spriteBatch, WorldSnapshot snapshot, Item item, bool anchored, float totalSeconds)
    {
        var wires = snapshot.Wiring.Wires.Where(w => w.FromPin == item.Pin || w.ToPin == item.Pin).ToList();
        var damaged = wires.Any(w => snapshot.Wiring.WireStates.FirstOrDefault(s => s.WireId == w.Id)?.Damaged == true);
        var power = item.Kind is PinKind.PowerIn or PinKind.PowerOut;
        var dotColor = anchored ? Color.CadetBlue * (0.6f + 0.4f * MathF.Sin(totalSeconds * 8f))
            : wires.Count == 0 ? Color.DimGray : damaged ? Color.OrangeRed : power ? Color.Gold : Color.LimeGreen;
        spriteBatch.Draw(_pixel, new Rectangle(item.Rect.X, item.Rect.Y + 4, DotSize, DotSize), dotColor);

        var color = anchored ? Color.CadetBlue : wires.Count >= WireRules.MaxWiresPerPin ? Color.Orange : Color.LightGray;
        spriteBatch.DrawString(_font, item.Text, new Vector2(item.Rect.X + 12, item.Rect.Y + 2), color, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
    }

    private void DrawWireLine(SpriteBatch spriteBatch, WorldSnapshot snapshot, Item item, bool hovered)
    {
        var damaged = snapshot.Wiring.WireStates.FirstOrDefault(s => s.WireId == item.WireId)?.Damaged == true;
        var color = hovered ? Color.OrangeRed : damaged ? Color.OrangeRed * 0.8f : Color.LightGreen;
        var text = damaged ? item.Text + "  (перебит)" : item.Text;
        spriteBatch.DrawString(_font, text, new Vector2(item.Rect.X, item.Rect.Y + 2), color, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
    }
}
