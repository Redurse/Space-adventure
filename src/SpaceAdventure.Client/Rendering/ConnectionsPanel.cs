using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Screwdriver-only readout of one component's pins and what's wired to each - the Barotrauma-style
// "open the junction box" screen. The physical scene (ComponentRenderer) already IS the schematic,
// so this doesn't let you rewire anything, it just answers "what's plugged into THIS one" without
// tracing green lines across the whole ship by eye.
public sealed class ConnectionsPanel
{
    private const float LineHeight = 18f;

    private readonly SpriteFont _font;

    public ConnectionsPanel(SpriteFont font) => _font = font;

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, string componentId, Vector2 origin)
    {
        var component = snapshot.Components.FirstOrDefault(c => c.Id == componentId);
        if (component is null)
            return;

        spriteBatch.DrawString(_font, $"Подключения: {ComponentRenderer.ComponentLabel(snapshot, componentId)}",
            origin, Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        var y = LineHeight + 6f;
        foreach (var (pinId, kind) in ComponentRenderer.PinsFor(component, snapshot))
        {
            var direction = kind is PinKind.PowerIn or PinKind.SignalIn ? "вход" : "выход";
            var category = kind is PinKind.PowerIn or PinKind.PowerOut ? "питание" : "сигнал";
            spriteBatch.DrawString(_font, $"{pinId} ({direction}, {category}):", origin + new Vector2(0, y),
                Color.LightGray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            y += LineHeight;

            var pinRef = new PinRef(componentId, pinId);
            var wires = snapshot.Wires.Where(w => w.FromPin == pinRef || w.ToPin == pinRef).ToList();
            if (wires.Count == 0)
            {
                spriteBatch.DrawString(_font, "  не подключено", origin + new Vector2(0, y),
                    Color.DimGray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
                y += LineHeight;
                continue;
            }

            foreach (var wire in wires)
            {
                var otherEnd = wire.FromPin == pinRef ? wire.ToPin : wire.FromPin;
                var damaged = snapshot.WireStates.FirstOrDefault(s => s.WireId == wire.Id)?.Damaged ?? false;
                var text = $"  -> {ComponentRenderer.PinLabel(snapshot, otherEnd)} ({(damaged ? "ПОВРЕЖДЁН" : "цел")})";
                spriteBatch.DrawString(_font, text, origin + new Vector2(0, y),
                    damaged ? Color.OrangeRed : Color.LightGreen, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
                y += LineHeight;
            }
        }
    }
}
