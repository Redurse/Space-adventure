using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// External hull cameras (game_design.md, M46) - 4 fixed directions around the hull, each just the
// existing FieldRenderer view from a different angle. Deliberately reuses FieldRenderer.Draw as-is
// rather than adding any new object-drawing logic: a rotated copy of the snapshot's own ShipField
// (RotationDegrees offset by the camera's own bearing, plus however far the viewer has panned
// within it) makes FieldRenderer believe the ship is simply facing a different way, which is all a
// "different point of view" actually needs - ShipLocalFrame.ToLocal already rotates every world
// point by -ShipField.RotationDegrees to place it on screen, so adding to that value rotates the
// apparent view by exactly that much. Purely client state throughout (Game1.cs's own
// _externalCameraMode/_cameraLookOffsetDegrees) - a camera's own look direction isn't a physical
// thing other players or the server need to know about, unlike a manned turret's aim.
public sealed class ExternalCameraPanel
{
    // Bow, starboard, stern, port - the 4 fixed 90-degree sectors game_design.md calls for.
    public static readonly float[] CameraBaseBearings = { 0f, 90f, 180f, 270f };
    public static readonly string[] CameraLabels = { "Камера 1: Нос", "Камера 2: Правый борт", "Камера 3: Корма", "Камера 4: Левый борт" };
    // How far a viewer can pan within one camera's own sector once inside it fullscreen - each
    // camera's own quarter of the full circle, not the turret's much narrower firing arc.
    public const float MaxLookOffsetDegrees = 45f;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;
    private readonly FieldRenderer _fieldRenderer;

    public ExternalCameraPanel(GraphicsDevice graphicsDevice, SpriteFont font, FieldRenderer fieldRenderer)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
        _fieldRenderer = fieldRenderer;
    }

    // The zoomed-out framing every camera feed shares - external cameras are meant to watch for
    // what's approaching from a distance, not read as the same close-in cockpit/turret scale.
    private const float CameraZoomOut = 0.35f;

    // outerTransform: whatever transform the caller's own batch was already using (Game1's
    // _renderScale, mapping design-space pixels to the real backbuffer) - needed twice over: the
    // scissor rectangle has to be set in real device pixels regardless of window size/letterboxing,
    // and the batch this method re-opens after its own scissored one has to leave the caller's
    // camera exactly as it found it, or every HUD element drawn afterward this frame ends up in
    // the wrong place.
    public void DrawGrid(SpriteBatch spriteBatch, GraphicsDevice device, WorldSnapshot snapshot, Rectangle area,
        Matrix outerTransform, float totalSeconds)
    {
        var halfWidth = area.Width / 2;
        var halfHeight = area.Height / 2;
        for (var i = 0; i < 4; i++)
        {
            var quadrant = new Rectangle(
                area.X + (i % 2) * halfWidth, area.Y + (i / 2) * halfHeight, halfWidth, halfHeight);
            DrawOneCamera(spriteBatch, device, snapshot, quadrant, outerTransform, CameraBaseBearings[i], 0f, totalSeconds, CameraLabels[i], drawFrame: false);
        }
    }

    public void DrawFullscreen(SpriteBatch spriteBatch, GraphicsDevice device, WorldSnapshot snapshot, Rectangle area,
        Matrix outerTransform, int cameraIndex, float lookOffsetDegrees, float totalSeconds) =>
        DrawOneCamera(spriteBatch, device, snapshot, area, outerTransform, CameraBaseBearings[cameraIndex], lookOffsetDegrees, totalSeconds,
            CameraLabels[cameraIndex], drawFrame: true);

    private void DrawOneCamera(SpriteBatch spriteBatch, GraphicsDevice device, WorldSnapshot snapshot, Rectangle area,
        Matrix outerTransform, float baseBearing, float lookOffsetDegrees, float totalSeconds, string label, bool drawFrame)
    {
        var rotatedSnapshot = snapshot with
        {
            ShipField = snapshot.ShipField with { RotationDegrees = snapshot.ShipField.RotationDegrees + baseBearing + lookOffsetDegrees },
        };
        var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
        var center = new Vector2(area.Center.X, area.Center.Y);
        var cameraTransform =
            Matrix.CreateTranslation(-center.X, -center.Y, 0f) * Matrix.CreateScale(CameraZoomOut, CameraZoomOut, 1f) *
            Matrix.CreateTranslation(center.X, center.Y, 0f) * outerTransform;

        var previousScissor = device.ScissorRectangle;
        device.ScissorRectangle = DeviceSpaceRect(area, outerTransform, device.Viewport);
        spriteBatch.End();
        spriteBatch.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true }, transformMatrix: cameraTransform);

        spriteBatch.Draw(_pixel, area, new Color(4, 6, 10));
        _fieldRenderer.Draw(spriteBatch, rotatedSnapshot, center, hullCenter,
            new Vector2(area.X, area.Y), new Vector2(area.Width, area.Height), totalSeconds);

        spriteBatch.End();
        device.ScissorRectangle = previousScissor;
        spriteBatch.Begin(transformMatrix: outerTransform);

        DrawFrame(spriteBatch, area, label, drawFrame);
    }

    // ScissorRectangle is always in real backbuffer pixels, unlike every other coordinate this
    // whole scene batch works in (design-space, mapped up by outerTransform) - transform the
    // area's own corners through the same matrix instead of assuming design space ever equals
    // device space, and clamp to the viewport since a corner can otherwise land a pixel outside it
    // from float rounding, which the scissor rectangle rejects outright rather than clamping itself.
    private static Rectangle DeviceSpaceRect(Rectangle area, Matrix outerTransform, Viewport viewport)
    {
        var topLeft = Vector2.Transform(new Vector2(area.X, area.Y), outerTransform);
        var bottomRight = Vector2.Transform(new Vector2(area.Right, area.Bottom), outerTransform);
        var x = Math.Clamp((int)MathF.Round(topLeft.X), 0, viewport.Width);
        var y = Math.Clamp((int)MathF.Round(topLeft.Y), 0, viewport.Height);
        var right = Math.Clamp((int)MathF.Round(bottomRight.X), 0, viewport.Width);
        var bottom = Math.Clamp((int)MathF.Round(bottomRight.Y), 0, viewport.Height);
        return new Rectangle(x, y, Math.Max(0, right - x), Math.Max(0, bottom - y));
    }

    private void DrawFrame(SpriteBatch spriteBatch, Rectangle area, string label, bool thick)
    {
        var thickness = thick ? 4 : 2;
        var color = Color.DarkSlateGray;
        spriteBatch.Draw(_pixel, new Rectangle(area.X, area.Y, area.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(area.X, area.Bottom - thickness, area.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(area.X, area.Y, thickness, area.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(area.Right - thickness, area.Y, thickness, area.Height), color);
        spriteBatch.DrawString(_font, label, new Vector2(area.X + 6, area.Y + 4), Color.LimeGreen, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }

    public static Rectangle QuadrantAt(Rectangle area, int index)
    {
        var halfWidth = area.Width / 2;
        var halfHeight = area.Height / 2;
        return new Rectangle(area.X + (index % 2) * halfWidth, area.Y + (index / 2) * halfHeight, halfWidth, halfHeight);
    }

    public static int? QuadrantHitTest(Rectangle area, Point point)
    {
        for (var i = 0; i < 4; i++)
            if (QuadrantAt(area, i).Contains(point))
                return i;
        return null;
    }
}
