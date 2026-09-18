using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Direct user request ("то, как корабль должен повернуться и прийти к концу пути... как в
// cosmoteer") - a translucent green preview of the player's own hull sitting at the autopilot's
// current destination, oriented to a PREDICTED final heading - not an exact one, since
// World.Autopilot.cs's own steering blends in obstacle avoidance and any live RMB facing override,
// neither of which the client can see from WorldSnapshot.Autopilot (only DestinationX/Y - no
// predicted-facing field exists server-side). The straight-line bearing from the ship's own
// current position to the destination is the same "desired" direction StepAutopilot itself seeks
// by default (its own doc comment: "nose points where it's going") whenever nothing is bending or
// overriding it - close enough for a preview, not claimed as exact.
public sealed partial class FieldRenderer
{
    private void DrawAutopilotGhost(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vec2 hullCenter, Vec2 destination, Func<Vec2, Vector2> worldToScreen)
    {
        if (snapshot.Rooms.Count == 0)
            return;

        var shipPosition = new Vec2(snapshot.ShipField.X, snapshot.ShipField.Y);
        var toDestination = destination - shipPosition;
        if (toDestination.Length() < 0.5)
            return; // already basically there - nothing meaningful to preview

        var predictedNoseBearing = MathF.Atan2((float)toDestination.Y, (float)toDestination.X) * (180f / MathF.PI);
        // Ship.ForwardDegrees is the hull's own "which way is the nose" offset from RotationDegrees
        // (the same convention World.Autopilot.cs's own heading-error math uses) - subtracting it
        // back out here gives the GHOST's own RotationDegrees equivalent, so ToWorldDirection below
        // rotates its room offsets the same way the live hull's own rooms are rotated.
        var ghostRotationDegrees = predictedNoseBearing - snapshot.ShipForwardDegrees;

        // Same base de-rotation FieldRenderer.cs's own enemy-ship loop already applies (this scene's
        // camera keeps the LIVE ship always upright, so anything else needs the live rotation
        // cancelled out before adding its own).
        var screenRotation = -snapshot.ShipField.RotationDegrees * (MathF.PI / 180f) + ghostRotationDegrees * (MathF.PI / 180f);

        foreach (var room in snapshot.Rooms)
        {
            var roomOffset = ShipLocalFrame.ToWorldDirection(room.Center - hullCenter, ghostRotationDegrees);
            var roomScreen = worldToScreen(destination + roomOffset);
            var size = new Vector2(room.Width, room.Height) * ShipRenderer.PixelsPerUnit;
            spriteBatch.Draw(_pixel, roomScreen, null, Color.LimeGreen * 0.35f, screenRotation,
                new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
        }

        // A small "beak" at the predicted nose, same construction as GalaxyMapPanel's own ghost/
        // heading marker - GetHullHalfExtents' diagonal guarantees it clears the hull regardless of
        // its size, rather than a fixed offset that could land buried inside a bigger ship's rooms.
        var hullRadius = (float)ShipLocalFrame.GetHullHalfExtents(snapshot.Rooms).Length() * ShipRenderer.PixelsPerUnit;
        var noseRadians = predictedNoseBearing * (MathF.PI / 180f) - snapshot.ShipField.RotationDegrees * (MathF.PI / 180f);
        var forward = new Vector2(MathF.Cos(noseRadians), MathF.Sin(noseRadians));
        var side = new Vector2(-forward.Y, forward.X);
        var destScreen = worldToScreen(destination);
        var noseBase = destScreen + forward * hullRadius;
        var noseTip = noseBase + forward * 14f;
        var trianglePoints = new[] { noseTip, noseBase + side * 8f, noseBase - side * 8f };
        Primitives.FillPolygon(spriteBatch, _pixel, noseBase, trianglePoints, Color.LimeGreen * 0.6f);
    }
}
