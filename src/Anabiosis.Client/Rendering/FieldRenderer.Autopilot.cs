using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Direct user request ("то, как корабль должен повернуться и прийти к концу пути... как в
// cosmoteer" / "призрак начинает поворачиваться, а так ведь быть не должно") - a translucent green
// preview of the player's own hull sitting at the autopilot's current destination, oriented to
// AutopilotState.PredictedFacingDegrees - the SAME desiredBearingDegrees World.Autopilot.cs's own
// StepAutopilot actually steers toward (RMB override, velocity-direction, or its own shorter-
// reverse-angle pick for a hull with no real strafe), relayed straight from the server rather than
// a client-side bearing-to-destination guess, which used to visibly swing as the ship's own
// overshoot carried it past the point.
public sealed partial class FieldRenderer
{
    private void DrawAutopilotGhost(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vec2 hullCenter, Vec2 destination, Func<Vec2, Vector2> worldToScreen, float? predictedFacingDegrees)
    {
        if (snapshot.Rooms.Count == 0)
            return;

        var shipPosition = new Vec2(snapshot.ShipField.X, snapshot.ShipField.Y);
        var toDestination = destination - shipPosition;
        if (toDestination.Length() < 0.5)
            return; // already basically there - nothing meaningful to preview

        var predictedNoseBearing = predictedFacingDegrees
            ?? MathF.Atan2((float)toDestination.Y, (float)toDestination.X) * (180f / MathF.PI);
        // Ship.ForwardDegrees is the hull's own "which way is the nose" offset from RotationDegrees
        // (the same convention World.Autopilot.cs's own heading-error math uses) - subtracting it
        // back out here gives the GHOST's own RotationDegrees equivalent, so ToWorldDirection below
        // rotates its room offsets the same way the live hull's own rooms are rotated.
        var ghostRotationDegrees = predictedNoseBearing - snapshot.ShipForwardDegrees;

        // Direct user bug report ("призрак... при повороте корабля поворачивается так же как и
        // корабль, хотя такого быть не должно... корабль должен прийти в конце маршрута именно в
        // таком положении") - unlike a decorative world object (the sun, an asteroid), which counter-
        // rotates by the LIVE ship's own -RotationDegrees so it reads as staying put IN THE WORLD
        // while the always-upright ship turns past it, this ghost is a fixed destination PREVIEW: its
        // whole point is to show the one true final orientation and hold perfectly still on screen
        // while the live ship (still free to turn, overshoot, correct course, etc. on the way there)
        // does whatever it does. Confirmed via direct clarifying question - the live rotation term is
        // dropped entirely here, not just recomputed differently.
        var screenRotation = ghostRotationDegrees * (MathF.PI / 180f);

        // The destination's OWN position still needs the ordinary ship-relative worldToScreen (it's
        // a real point in the world, same as any other marker - it correctly appears to move as the
        // live ship travels/turns, exactly like an asteroid would). Each ROOM's offset from that
        // anchor, though, must NOT go through worldToScreen too - that would silently reintroduce the
        // very rotation this fix removes, since worldToScreen's own ToLocal bakes in the live ship's
        // -RotationDegrees for any point it's given. Scaling the offset directly (world units ->
        // pixels, no rotation) keeps every room's position relative to the anchor fixed on screen,
        // matching screenRotation's own fixed orientation above - the whole silhouette holds still as
        // one rigid shape instead of just each room's own sprite stopping short of spinning in place.
        var destScreen = worldToScreen(destination);
        foreach (var room in snapshot.Rooms)
        {
            var roomOffset = ShipLocalFrame.ToWorldDirection(room.Center - hullCenter, ghostRotationDegrees);
            var roomScreen = destScreen + new Vector2((float)roomOffset.X, (float)roomOffset.Y) * ShipRenderer.PixelsPerUnit;
            var size = new Vector2(room.Width, room.Height) * ShipRenderer.PixelsPerUnit;
            spriteBatch.Draw(_pixel, roomScreen, null, Color.LimeGreen * 0.35f, screenRotation,
                new Vector2(0.5f, 0.5f), size, SpriteEffects.None, 0f);
        }

        // A small "beak" at the predicted nose, same construction as GalaxyMapPanel's own ghost/
        // heading marker - GetHullHalfExtents' diagonal guarantees it clears the hull regardless of
        // its size, rather than a fixed offset that could land buried inside a bigger ship's rooms.
        var hullRadius = (float)ShipLocalFrame.GetHullHalfExtents(snapshot.Rooms).Length() * ShipRenderer.PixelsPerUnit;
        // Same fixed-on-screen reasoning as screenRotation above - no live-rotation term.
        var noseRadians = predictedNoseBearing * (MathF.PI / 180f);
        var forward = new Vector2(MathF.Cos(noseRadians), MathF.Sin(noseRadians));
        var side = new Vector2(-forward.Y, forward.X);
        var noseBase = destScreen + forward * hullRadius;
        var noseTip = noseBase + forward * 14f;
        var trianglePoints = new[] { noseTip, noseBase + side * 8f, noseBase - side * 8f };
        Primitives.FillPolygon(spriteBatch, _pixel, noseBase, trianglePoints, Color.LimeGreen * 0.6f);
    }
}
