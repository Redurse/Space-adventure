using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The distribution block's readout: reactor + battery, and one bar per system showing how much power
// it's actually getting. Read-only since W2 of wiring-plan.md - there are no sliders any more, the
// ship's grid (World.Power.cs) feeds every wired device by itself, Barotrauma-style; a bar falls
// short only when a wire is cut, a box is broken or the grid is short of power.
public sealed class PowerPanel
{
    private static readonly (PowerSystemId Id, string Label)[] SystemLabels =
    {
        (PowerSystemId.Oxygen, "Кислород"),
        (PowerSystemId.Engine, "Двигатель"),
        (PowerSystemId.Shields, "Щиты"),
        (PowerSystemId.WeaponCharger, "Орудия"),
        (PowerSystemId.Secondary, "Прочее"),
    };

    // What a system draws at full power on the server (World.Power.cs's SystemDemand/EngineDemand) -
    // a full bar.
    private static float FullSystemPower(PowerSystemId system) => system == PowerSystemId.Engine ? 20f : 12f;

    private const float BarWidth = 220f;
    private const float BarHeight = 18f;
    private const float RowSpacing = 26f;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public PowerPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, PowerState power, IReadOnlyList<ShipSystemState> systemStates, Vector2 origin, float totalSeconds)
    {
        var bounds = DevicePanelChrome.StandardBounds(origin);
        var phosphor = new Color(122, 208, 236);
        DevicePanelChrome.Draw(spriteBatch, _font, bounds, "РАСПРЕДЕЛЕНИЕ ПИТАНИЯ", "PW-01", phosphor, totalSeconds);

        DevicePanelChrome.DrawReadout(spriteBatch, _font, origin + new Vector2(0, -6),
            "РЕАКТОР", $"{power.ReactorOutput:0}", $"/ {power.ReactorMaxOutput:0}", phosphor);
        DevicePanelChrome.DrawReadout(spriteBatch, _font, origin + new Vector2(118, -6),
            "ТОПЛИВО", $"{power.ReactorFuel:0}", "", phosphor);
        DevicePanelChrome.DrawReadout(spriteBatch, _font, origin + new Vector2(210, -6),
            "БАТАРЕЯ", $"{power.BatteryCharge:0}", $"/ {power.BatteryCapacity:0}",
            power.BatteryCharge <= power.BatteryCapacity * 0.15f ? new Color(232, 108, 84) : phosphor);

        for (var i = 0; i < SystemLabels.Length; i++)
        {
            var (id, label) = SystemLabels[i];
            var allocated = power.Allocated.TryGetValue(id, out var value) ? value : 0f;
            var damaged = systemStates.FirstOrDefault(s => s.System == id)?.Damaged ?? false;
            var rowOrigin = origin + new Vector2(0, 24 + i * RowSpacing);
            var labelText = damaged ? $"{label} (повреждена)" : label;

            spriteBatch.DrawString(_font, labelText, rowOrigin,
                damaged ? Color.Red : Color.LightGray, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

            var barOrigin = rowOrigin + new Vector2(150, 2);
            spriteBatch.Draw(_pixel, new Rectangle((int)barOrigin.X, (int)barOrigin.Y, (int)BarWidth, (int)BarHeight), Color.DimGray);

            // A full bar is a system getting all it asks for; a short one is being starved (a cut
            // wire, a broken box, or a grid short of power).
            var fillRatio = Math.Clamp(allocated / FullSystemPower(id), 0f, 1f);
            spriteBatch.Draw(_pixel, new Rectangle((int)barOrigin.X, (int)barOrigin.Y, (int)(BarWidth * fillRatio), (int)BarHeight),
                damaged ? Color.DarkRed : fillRatio < 0.99f ? new Color(232, 170, 70) : Color.SteelBlue);
            spriteBatch.DrawString(_font, $"{allocated:0.0}", barOrigin + new Vector2(BarWidth + 6, 0),
                Color.LightGray, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        }
    }
}
