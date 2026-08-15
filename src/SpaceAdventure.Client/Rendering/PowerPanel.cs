using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Debug/MVP view of the distribution block: reactor + battery readout, one bar per system.
// Player picks a system with number keys (1-5) and adjusts it with Q/E — see Game1.Update.
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

    public void Draw(SpriteBatch spriteBatch, PowerState power, IReadOnlyList<ShipSystemState> systemStates, int selectedIndex, Vector2 origin)
    {
        var header = $"Реактор: {power.ReactorOutput:0}/{power.ReactorMaxOutput:0}  " +
                      $"Топливо: {power.ReactorFuel:0}/{power.ReactorMaxFuel:0}  " +
                      $"Батарея: {power.BatteryCharge:0}/{power.BatteryCapacity:0}";
        spriteBatch.DrawString(_font, header, origin, Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);

        for (var i = 0; i < SystemLabels.Length; i++)
        {
            var (id, label) = SystemLabels[i];
            var allocated = power.Allocated.TryGetValue(id, out var value) ? value : 0f;
            var damaged = systemStates.FirstOrDefault(s => s.System == id)?.Damaged ?? false;
            var rowOrigin = origin + new Vector2(0, 24 + i * RowSpacing);
            var isSelected = i == selectedIndex;
            var labelText = damaged ? $"[{i + 1}] {label} (повреждена)" : $"[{i + 1}] {label}";

            spriteBatch.DrawString(_font, labelText, rowOrigin,
                damaged ? Color.Red : isSelected ? Color.Yellow : Color.LightGray, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

            var barOrigin = rowOrigin + new Vector2(150, 2);
            spriteBatch.Draw(_pixel, new Rectangle((int)barOrigin.X, (int)barOrigin.Y, (int)BarWidth, (int)BarHeight), Color.DimGray);

            // While damaged, effective output is 0 (see PowerGrid.GetAllocation) even though the
            // slider itself still shows where the player left it — draw a slashed red bar instead
            // of the normal fill so that's visible without hiding the player's chosen setting.
            var fillRatio = power.ReactorMaxOutput > 0 ? Math.Clamp(allocated / power.ReactorMaxOutput, 0f, 1f) : 0f;
            spriteBatch.Draw(_pixel, new Rectangle((int)barOrigin.X, (int)barOrigin.Y, (int)(BarWidth * fillRatio), (int)BarHeight),
                damaged ? Color.DarkRed : isSelected ? Color.Gold : Color.SteelBlue);
        }
    }
}
