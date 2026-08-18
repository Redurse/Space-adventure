using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Screwdriver-only readout of one component's pins and what's wired to each - the Barotrauma-style
// "open the junction box" screen. The physical scene (ComponentRenderer) already IS the schematic,
// so this doesn't let you rewire anything, it just answers "what's plugged into THIS one" without
// tracing green lines across the whole ship by eye. Laid out as a bordered box with every input pin
// down the left edge and every output pin down the right edge (ComponentRenderer.PinsFor - the full
// catalog for the component's kind, not just the ones currently wired), each with a connector dot
// and, if wired, the other end's name inline - matching the reference wiring popup instead of a
// plain scrolling text list.
public sealed class ConnectionsPanel
{
    public const int Width = 460;

    private const int HeaderHeight = 22;
    private const int RowHeight = 20;
    private const int DotSize = 8;
    private const int BorderThickness = 2;
    private static readonly Color PanelBackground = new(20, 26, 22);
    private static readonly Color PanelBorder = new(90, 110, 95);

    private readonly SpriteFont _font;
    private readonly Texture2D _pixel;

    public ConnectionsPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _font = font;
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, string componentId, Rectangle bounds)
    {
        var component = snapshot.Components.FirstOrDefault(c => c.Id == componentId);
        if (component is null)
            return;

        var pins = ComponentRenderer.PinsFor(component, snapshot).ToList();
        var inputs = pins.Where(p => p.Kind is PinKind.PowerIn or PinKind.SignalIn).ToList();
        var outputs = pins.Where(p => p.Kind is PinKind.PowerOut or PinKind.SignalOut).ToList();
        var rows = Math.Max(inputs.Count, outputs.Count);
        var height = Math.Max(bounds.Height, HeaderHeight + 16 + rows * RowHeight + 10);
        var rect = new Rectangle(bounds.X, bounds.Y, bounds.Width, height);

        spriteBatch.Draw(_pixel, rect, PanelBackground * 0.95f);
        DrawRectOutline(spriteBatch, rect, PanelBorder, BorderThickness);

        var headerRect = new Rectangle(rect.X, rect.Y, rect.Width, HeaderHeight);
        spriteBatch.Draw(_pixel, headerRect, new Color(30, 38, 33));
        spriteBatch.Draw(_pixel, new Rectangle(headerRect.X, headerRect.Bottom - 1, headerRect.Width, 1), PanelBorder);
        spriteBatch.DrawString(_font, $"Подключения: {ComponentRenderer.ComponentLabel(snapshot, componentId)}",
            new Vector2(rect.X + 8, rect.Y + 4), Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        if (rows == 0)
        {
            spriteBatch.DrawString(_font, "Нет проводных пинов.", new Vector2(rect.X + 8, rect.Y + HeaderHeight + 6),
                Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
            return;
        }

        spriteBatch.DrawString(_font, "ВХОДЫ", new Vector2(rect.X + 8, rect.Y + HeaderHeight + 2), Color.Gray, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);
        var outputsLabelSize = _font.MeasureString("ВЫХОДЫ") * 0.45f;
        spriteBatch.DrawString(_font, "ВЫХОДЫ", new Vector2(rect.Right - 8 - outputsLabelSize.X, rect.Y + HeaderHeight + 2),
            Color.Gray, 0f, Vector2.Zero, 0.45f, SpriteEffects.None, 0f);

        var top = rect.Y + HeaderHeight + 18;
        for (var i = 0; i < inputs.Count; i++)
            DrawPinRow(spriteBatch, snapshot, componentId, inputs[i], new Vector2(rect.X + 8, top + i * RowHeight), isInput: true);
        for (var i = 0; i < outputs.Count; i++)
            DrawPinRow(spriteBatch, snapshot, componentId, outputs[i], new Vector2(rect.Right - 8, top + i * RowHeight), isInput: false);
    }

    private void DrawPinRow(SpriteBatch spriteBatch, WorldSnapshot snapshot, string componentId,
        (string PinId, PinKind Kind) pin, Vector2 dotPosition, bool isInput)
    {
        var pinRef = new PinRef(componentId, pin.PinId);
        var wires = snapshot.Wires.Where(w => w.FromPin == pinRef || w.ToPin == pinRef).ToList();
        var damaged = wires.Any(w => snapshot.WireStates.FirstOrDefault(s => s.WireId == w.Id)?.Damaged == true);
        var dotColor = wires.Count == 0 ? Color.DimGray : damaged ? Color.OrangeRed : Color.LimeGreen;

        spriteBatch.Draw(_pixel, new Rectangle((int)dotPosition.X - (isInput ? 0 : DotSize), (int)dotPosition.Y, DotSize, DotSize), dotColor);

        var text = wires.Count == 0
            ? pin.PinId
            : $"{pin.PinId} -> {string.Join(", ", wires.Select(w => ComponentRenderer.PinLabel(snapshot, w.FromPin == pinRef ? w.ToPin : w.FromPin)))}";
        var color = wires.Count == 0 ? Color.LightGray : damaged ? Color.OrangeRed : Color.LightGreen;

        var textPos = new Vector2(isInput ? dotPosition.X + 12 : dotPosition.X - 12, dotPosition.Y - 4);
        if (!isInput)
        {
            var size = _font.MeasureString(text) * 0.42f;
            textPos.X -= size.X;
        }
        spriteBatch.DrawString(_font, text, textPos, color, 0f, Vector2.Zero, 0.42f, SpriteEffects.None, 0f);
    }

    private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
