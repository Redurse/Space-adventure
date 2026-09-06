using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Contextual hints for the turret (approach prompt / manned controls) plus the manned turret's own
// ammo/charge. Enemy HP/shield used to live here too - moved off (enemy HP dropped entirely, the
// ship's own shield moved to the helm's window 3, ShipSchematicPanel) so this corner only ever shows what's
// actually relevant to whoever's reading it right now.
public sealed class CombatPanel
{
    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public CombatPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int playerId, string hint, Vector2 origin)
    {
        var ammoOrigin = origin;
        // Only the turret THIS player is actually sitting in - ammo/charge is meaningless (and just
        // clutter) for every turret on the ship at once when nobody's aiming most of them.
        foreach (var turret in snapshot.TurretStates.Where(t => t.MannedByPlayerId == playerId))
        {
            var isLaser = snapshot.Turrets.FirstOrDefault(t => t.Id == turret.Id)?.WeaponType == TurretWeaponType.Laser;
            string label;
            Color color;
            if (isLaser)
            {
                label = $"Заряд орудия: {turret.Charge:0}/{turret.MaxCharge:0}";
                color = turret.Charge > 0 ? Color.LightSkyBlue : Color.IndianRed;
            }
            else
            {
                label = $"Боезапас: {turret.AmmoRemaining}/{turret.MagazineCapacity}";
                color = turret.AmmoRemaining > 0 ? Color.LightGray : Color.IndianRed;
            }

            if (turret.Damaged)
            {
                label += " (повреждена)";
                color = Color.Red;
            }

            spriteBatch.DrawString(_font, label, ammoOrigin, color, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
            ammoOrigin += new Vector2(0, 20);
        }

        if (hint.Length > 0)
            spriteBatch.DrawString(_font, hint, ammoOrigin, Color.Yellow, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
    }
}
