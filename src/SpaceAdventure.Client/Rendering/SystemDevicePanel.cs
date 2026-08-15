using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Read-only readout shown while a system block (oxygen/engine/shields/weapon charger/secondary)
// is open — power draw and damage state; Shields also gets its charge/consumption shown here
// per the request ("увидеть сколько электричества он потребляет").
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

    private readonly SpriteFont _font;

    public SystemDevicePanel(SpriteFont font) => _font = font;

    public void Draw(SpriteBatch spriteBatch, PowerSystemId system, PowerState power, ShieldState shield, IReadOnlyList<ShipSystemState> systemStates, Vector2 origin)
    {
        var label = Labels.First(l => l.Id == system).Label;
        var allocated = power.Allocated.TryGetValue(system, out var value) ? value : 0f;
        var damaged = systemStates.Any(s => s.System == system && s.Damaged);

        spriteBatch.DrawString(_font, $"Система: {label}", origin, Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, $"Потребление: {allocated:0.0}", origin + new Vector2(0, 22), Color.LightGray, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(_font, damaged ? "Состояние: повреждена" : "Состояние: исправна",
            origin + new Vector2(0, 42), damaged ? Color.Red : Color.LightGreen, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

        if (system == PowerSystemId.Shields)
        {
            spriteBatch.DrawString(_font, $"Заряд щитов: {shield.Points:0}/{shield.MaxPoints:0}",
                origin + new Vector2(0, 62), Color.SkyBlue, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        }
    }
}
