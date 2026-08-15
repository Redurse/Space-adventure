using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Approximates the suit-helmet flashlight cone (game_design.md section 2: "пока скафандр
// надет, обзор всегда ограничен освещённым передним конусом... всё остальное — чёрное").
// Only active while the local player is wearing a suit. Built from a handful of overlapping
// rotated rectangles fanning from the character — a plain-SpriteBatch approximation, not a
// real stencil-masked cone.
public sealed class VisionOverlay
{
    private const float PixelsPerUnit = 48f;
    private const float ConeRadiusUnits = 5f;
    private const float ConeHalfAngleDegrees = 32f;
    private const int ConeSlices = 14;

    private readonly Texture2D _pixel;

    public VisionOverlay(GraphicsDevice graphicsDevice)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, int playerId, Vector2 shipOrigin, Rectangle darknessArea)
    {
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == playerId);
        if (me is null || !me.WearingSuit)
            return; // full vision while unsuited — nothing to draw

        spriteBatch.Draw(_pixel, darknessArea, Color.Black * 0.85f);

        var characterScreenPos = shipOrigin + new Vector2(me.X, me.Y) * PixelsPerUnit;
        var baseAngle = MathF.Atan2(me.FacingY, me.FacingX);
        var halfAngleRad = ConeHalfAngleDegrees * MathF.PI / 180f;
        var radiusPx = ConeRadiusUnits * PixelsPerUnit;
        var sliceThickness = 2 * radiusPx * MathF.Sin(halfAngleRad / (ConeSlices - 1));

        for (var i = 0; i < ConeSlices; i++)
        {
            var t = (float)i / (ConeSlices - 1);
            var angle = baseAngle - halfAngleRad + t * 2 * halfAngleRad;
            spriteBatch.Draw(_pixel, characterScreenPos, null, Color.White * 0.10f, angle, Vector2.Zero,
                new Vector2(radiusPx, sliceThickness), SpriteEffects.None, 0f);
        }
    }
}
