using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Direct user request ("то, как корабль должен повернуться и прийти к концу пути... как в
// cosmoteer" / "при зажатии пкм я не вижу силуэта" / "призрак начинает поворачиваться, а так ведь
// быть не должно") - the pilot's own primary flight view is THIS map (GalaxyMapPanel, pilotView),
// not the exterior FieldRenderer scene, so the destination ghost belongs here, not (or not only)
// there. Same rotated-room-schematic approach ShipAndStations.cs's own DrawShipHullSchematic
// already uses for the LIVE ship, just at an arbitrary destination point and a predicted heading
// instead of the ship's own live position/rotation. The predicted heading itself comes straight
// from World.Autopilot.cs's own AutopilotState.PredictedFacingDegrees (its own doc comment has the
// full reasoning) rather than a client-side bearing-to-destination guess, which used to visibly
// swing as the ship's own overshoot carried it past the point.
public sealed partial class GalaxyMapPanel
{
    private void DrawAutopilotGhostHull(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 destScreen, Vec2 destination, float zoom, float? predictedFacingDegrees)
    {
        if (snapshot.Rooms.Count == 0)
            return;

        var shipPosition = new Vec2(snapshot.ShipField.X, snapshot.ShipField.Y);
        var toDestination = destination - shipPosition;
        if (toDestination.Length() < 0.5)
            return; // already basically there - nothing meaningful to preview

        var predictedNoseBearing = predictedFacingDegrees
            ?? MathF.Atan2((float)toDestination.Y, (float)toDestination.X) * (180f / MathF.PI);
        var ghostRotationDegrees = predictedNoseBearing - snapshot.ShipForwardDegrees;
        var radians = ghostRotationDegrees * (MathF.PI / 180f);
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
        var scale = PixelsPerUnit * zoom;

        foreach (var room in snapshot.Rooms)
        {
            var local = room.Center - hullCenter;
            var rotated = new Vector2((float)(local.X * cos - local.Y * sin), (float)(local.X * sin + local.Y * cos));
            var size = new Vector2(room.Width, room.Height) * scale;
            spriteBatch.Draw(_pixel, destScreen + rotated * scale, null, Color.LimeGreen * 0.35f, radians,
                new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
        }

        var hullRadius = (float)ShipLocalFrame.GetHullHalfExtents(snapshot.Rooms).Length() * scale;
        var noseRadians = predictedNoseBearing * (MathF.PI / 180f);
        var forward = new Vector2(MathF.Cos(noseRadians), MathF.Sin(noseRadians));
        var side = new Vector2(-forward.Y, forward.X);
        var noseBase = destScreen + forward * hullRadius;
        var noseTip = noseBase + forward * 12f;
        var trianglePoints = new[] { noseTip, noseBase + side * 7f, noseBase - side * 7f };
        Primitives.FillPolygon(spriteBatch, _pixel, noseBase, trianglePoints, Color.LimeGreen * 0.7f);
    }
}
