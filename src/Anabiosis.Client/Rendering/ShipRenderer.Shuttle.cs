using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// What stands on the pad of a shuttle hangar bay: the shuttle itself while it is home (nose toward the long axis of the pad), and
// while the doors are going up or are open, hazard lights flashing round the pad's edge.
public sealed partial class ShipRenderer
{
    private void DrawBayShuttle(SpriteBatch spriteBatch, DecorativeDevice bay, ShuttleState? shuttle, Vector2 origin, float totalSeconds)
    {
        var (width, height) = FootprintPixelSize(bay.Kind, bay.Rotated);
        var rect = GetBlockRect(bay.Position, width, height, origin);
        var center = new Vector2(rect.Center.X, rect.Center.Y);

        if (shuttle is { Phase: ShuttlePhase.Docked })
        {
            // The nose points along the pad's longer side, away from the middle of the ship is not known here, so just that way.
            var rotation = rect.Width >= rect.Height ? 0f : -MathF.PI / 2f;
            ShuttleArt.Draw(spriteBatch, _pixel, center, rotation, MathF.Min(rect.Width, rect.Height) / 4.6f, thrust: false, alpha: 1f, totalSeconds);
        }

        if (shuttle is { BayDoorOpen: true } or { Phase: ShuttlePhase.Opening })
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(totalSeconds * 9f);
            var hazard = new Color(255, 70, 50) * (0.35f + 0.5f * pulse);
            const int thickness = 3;
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), hazard);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), hazard);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), hazard);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), hazard);
        }
    }
}
