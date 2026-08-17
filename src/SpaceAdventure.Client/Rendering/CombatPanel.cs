using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Enemy HP readout + contextual hints for the turret (approach prompt / manned controls).
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
        var enemyStatus = snapshot.Enemy.Hp <= 0 ? " (уничтожен)" : snapshot.Enemy.IsRetreating ? " (отступает)" : "";
        // A defended sector sends its ships in one at a time (game_design.md section 12) - showing
        // how many are left is the difference between "nearly done" and "this is the first of three".
        var squadron = snapshot.Enemy.RemainingShips > 1 ? $"  [ещё кораблей: {snapshot.Enemy.RemainingShips}]" : "";
        var enemyText = $"Враг: {snapshot.Enemy.Hp:0}/{snapshot.Enemy.MaxHp:0} HP{enemyStatus}{squadron}";
        spriteBatch.DrawString(_font, enemyText, origin, snapshot.Enemy.Hp > 0 ? Color.OrangeRed : Color.Gray, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);

        var barOrigin = origin + new Vector2(0, 22);
        const float barWidth = 220f, barHeight = 14f;
        spriteBatch.Draw(_pixel, new Rectangle((int)barOrigin.X, (int)barOrigin.Y, (int)barWidth, (int)barHeight), Color.DimGray);
        var ratio = snapshot.Enemy.MaxHp > 0 ? Math.Clamp(snapshot.Enemy.Hp / snapshot.Enemy.MaxHp, 0f, 1f) : 0f;
        spriteBatch.Draw(_pixel, new Rectangle((int)barOrigin.X, (int)barOrigin.Y, (int)(barWidth * ratio), (int)barHeight), Color.OrangeRed);

        // Ship-wide shield bar (game_design.md section 1) — absorbs enemy attacks before they
        // land on compartments; only drains/regrows from power routed to the Shields system.
        var shieldOrigin = barOrigin + new Vector2(0, barHeight + 4);
        spriteBatch.Draw(_pixel, new Rectangle((int)shieldOrigin.X, (int)shieldOrigin.Y, (int)barWidth, (int)barHeight), Color.DimGray);
        var shieldRatio = snapshot.Shield.MaxPoints > 0 ? Math.Clamp(snapshot.Shield.Points / snapshot.Shield.MaxPoints, 0f, 1f) : 0f;
        spriteBatch.Draw(_pixel, new Rectangle((int)shieldOrigin.X, (int)shieldOrigin.Y, (int)(barWidth * shieldRatio), (int)barHeight), Color.SkyBlue);
        spriteBatch.DrawString(_font, $"Щиты: {snapshot.Shield.Points:0}/{snapshot.Shield.MaxPoints:0}", shieldOrigin + new Vector2(4, -1), Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId);
        var healthOrigin = shieldOrigin + new Vector2(0, barHeight + 6);
        if (me is not null)
        {
            var healthColor = me.Health > 40 ? Color.LightGreen : Color.IndianRed;
            var suitStatus = me.WearingSuit ? " [скафандр]" : "";
            var bleedingStatus = me.IsBleeding ? " [кровотечение]" : "";
            spriteBatch.DrawString(_font, $"Здоровье: {me.Health:0}/100{suitStatus}{bleedingStatus}", healthOrigin, healthColor, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);

            // At 0 there's no separate "dead" state (World.Injuries.cs) - the character just keeps
            // standing there, fully mobile, with welding/cutting silently refusing to light
            // (World.Welding.cs/World.Cutting.cs both gate on Health > 0). Without this line, that
            // reads as "the tool is broken" rather than "you're down and need a MedKit."
            if (me.Health <= 0)
                spriteBatch.DrawString(_font, "НЕДЕЕСПОСОБЕН - нужна аптечка (сварка/резак не работают)",
                    healthOrigin + new Vector2(0, 16), Color.Red, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
        }

        var ammoOrigin = healthOrigin + new Vector2(0, me?.Health <= 0 ? 36 : 20);
        foreach (var turret in snapshot.TurretStates)
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
