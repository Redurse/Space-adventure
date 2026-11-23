using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// A shuttle that has left its bay, out in the field beside the ship, drawn where it is (and with a stern glow while it is moving).
public sealed partial class FieldRenderer
{
    private void DrawShuttles(SpriteBatch spriteBatch, WorldSnapshot snapshot, Func<Vec2, Vector2> worldToScreen, float cameraRotation, float totalSeconds)
    {
        if (snapshot.Shuttles is not { Count: > 0 } shuttles)
            return;
        foreach (var shuttle in shuttles.Where(s => s.Phase is ShuttlePhase.Flying or ShuttlePhase.Returning))
        {
            var screen = worldToScreen(new Vec2(shuttle.X, shuttle.Y));
            var moving = shuttle.VelocityX * shuttle.VelocityX + shuttle.VelocityY * shuttle.VelocityY > 4f;
            var rotation = cameraRotation + shuttle.RotationDegrees * MathF.PI / 180f;
            ShuttleArt.Draw(spriteBatch, _pixel, screen, rotation, ShipRenderer.PixelsPerUnit * 0.9f, moving, 1f, totalSeconds);
        }
    }
}
