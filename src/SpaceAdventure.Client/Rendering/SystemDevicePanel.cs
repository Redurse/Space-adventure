using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Read-only readout shown while a system block ("щиток" - oxygen/engine/shields/weapon charger/
// secondary) is opened WITHOUT a screwdriver in hand (Game1.Input.cs - holding one opens the
// wiring ConnectionsPanel instead, which is the tool-gated view; this is the no-tool default).
// Three status lights, plus two breaker-style numeric readouts (this device's own draw, and the
// whole grid's current total) - the layout a player asked to see reproduced here.
public sealed class SystemDevicePanel
{
    private static readonly (PowerSystemId Id, string Label)[] Labels =
    {
        (PowerSystemId.Oxygen, "Кислород"),
        (PowerSystemId.Engine, "Двигатель"),
        (PowerSystemId.Shields, "Щиты"),
        (PowerSystemId.WeaponCharger, "Орудия"),
        (PowerSystemId.Secondary, "Прочее"),
    };

    // Cosmetic only - stretches the raw allocation units (typically single/low-double digits,
    // PowerGrid.cs's reactor tops out at 60) into a "kW"-scale reading closer to what a breaker
    // panel's LCD would show. Doesn't touch the simulation, only how the number is printed.
    private const float DisplayScale = 10f;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public SystemDevicePanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, PowerSystemId system, PowerState power, ShieldState shield, IReadOnlyList<ShipSystemState> systemStates, Vector2 origin, float totalSeconds)
    {
        var label = Labels.First(l => l.Id == system).Label;
        var allocated = power.Allocated.TryGetValue(system, out var value) ? value : 0f;
        var damaged = systemStates.Any(s => s.System == system && s.Damaged);
        var totalAllocated = power.Allocated.Values.Sum();

        // Was a flat dark rectangle; now the same housing every other terminal wears, and it
        // carries the fault state - a damaged system turns its own panel red rather than only
        // saying so in a line of text.
        var phosphor = damaged ? new Color(236, 108, 92) : new Color(126, 214, 168);
        DevicePanelChrome.Draw(spriteBatch, _font,
            DevicePanelChrome.StandardBounds(origin),
            label.ToUpperInvariant(), "SY-" + ((int)system + 10), phosphor, totalSeconds);

        DrawStatusLights(spriteBatch, origin + new Vector2(0, 30), allocated, damaged, totalAllocated);

        var readoutOrigin = origin + new Vector2(180, 24);
        DrawReadout(spriteBatch, "Энергия", allocated, readoutOrigin);
        DrawReadout(spriteBatch, "Загрузка", totalAllocated, readoutOrigin + new Vector2(0, 46));

        if (system == PowerSystemId.Shields)
            spriteBatch.DrawString(_font, $"Заряд щитов: {shield.Points:0}/{shield.MaxPoints:0}",
                origin + new Vector2(0, 120), Color.SkyBlue, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    // Exactly one light at a time, read off data the wiring already tracks rather than a new
    // fault-simulation mechanic: a severed wire (Damaged) or nothing routed here at all reads as
    // under-powered; one system alone eating most of the reactor's current output reads as
    // overloaded; anything else in between is a clean, healthy supply.
    private void DrawStatusLights(SpriteBatch spriteBatch, Vector2 origin, float allocated, bool damaged, float totalAllocated)
    {
        string lit;
        if (damaged || allocated <= 0.1f)
            lit = "НИЗК. НАПРЯЖЕНИЕ";
        else if (totalAllocated > 0.1f && allocated / totalAllocated > 0.6f)
            lit = "ПЕРЕНАПРЯЖЕНИЕ";
        else
            lit = "ЕСТЬ ПИТАНИЕ";

        var rows = new[]
        {
            ("ЕСТЬ ПИТАНИЕ", Color.LimeGreen),
            ("ПЕРЕНАПРЯЖЕНИЕ", Color.Gold),
            ("НИЗК. НАПРЯЖЕНИЕ", Color.OrangeRed),
        };
        for (var i = 0; i < rows.Length; i++)
        {
            var (rowLabel, onColor) = rows[i];
            var on = rowLabel == lit;
            var row = origin + new Vector2(0, i * 26);
            spriteBatch.Draw(_pixel, new Rectangle((int)row.X, (int)row.Y + 3, 13, 13), on ? onColor : new Color(45, 50, 45));
            spriteBatch.DrawString(_font, rowLabel, row + new Vector2(20, 0), on ? Color.White : Color.Gray, 0f, Vector2.Zero, 0.5f, SpriteEffects.None, 0f);
        }
    }

    private void DrawReadout(SpriteBatch spriteBatch, string label, float rawValue, Vector2 origin)
    {
        spriteBatch.DrawString(_font, label, origin, Color.LightGray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        var boxRect = new Rectangle((int)origin.X, (int)origin.Y + 16, 110, 26);
        spriteBatch.Draw(_pixel, boxRect, new Color(28, 38, 30));
        spriteBatch.DrawString(_font, $"{rawValue * DisplayScale:0} кВт", new Vector2(boxRect.X + 6, boxRect.Y + 5), Color.PaleGreen, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }
}
