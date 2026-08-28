using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// External hull cameras (game_design.md, M46; rebuilt as real devices in M48 - "камеры как
// устройства корабля, как и любая другая система"). Each tile is one HullCamera (Ship.Cameras/
// WorldSnapshot.Cameras) - a fixed junction box wired into the power grid like any other device
// (WireGraphFactory), so a cut wire or a dead Secondary channel actually darkens its own feed
// (SystemStates, matched by DeviceId) rather than the whole mode being a single on/off toggle.
//
// The view itself is static (M48 follow-up - "статичный вид... сектор камеры широкий сам по
// себе"): no mouse-look, a fixed wide framing anchored on the camera's own physical mount position
// (HullCameraMount) rather than the ship's hull centre, so nearby objects passing close to that
// specific point of the hull show real parallax as the ship moves/turns - the same trick
// Game1.ComputeCamera already uses to anchor a manned turret's view on its own muzzle instead of
// the ship's centre. Reuses FieldRenderer.Draw as-is: a rotated copy of the snapshot's own
// ShipField (RotationDegrees offset by the mount's own fixed outward bearing) makes FieldRenderer
// believe the ship is simply facing a different way, which is all a "different point of view"
// needs on top of the position shift.
public sealed class ExternalCameraPanel
{
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

    // Roughly square: 1 camera fills the area, 2 sit side by side, 3-4 make a 2x2, 5-6 a 3x2, and
    // so on - whatever count this particular hull's Ship.Cameras actually has (M48 - fixed per
    // ship class, not a hardcoded 4).
    public static (int Cols, int Rows) GridDimensions(int count)
    {
        var cols = Math.Max(1, (int)MathF.Ceiling(MathF.Sqrt(count)));
        var rows = Math.Max(1, (int)MathF.Ceiling(count / (float)cols));
        return (cols, rows);
    }

    public static Rectangle QuadrantAt(Rectangle area, int index, int count)
    {
        var (cols, rows) = GridDimensions(count);
        var cellWidth = area.Width / cols;
        var cellHeight = area.Height / rows;
        var col = index % cols;
        var row = index / cols;
        return new Rectangle(area.X + col * cellWidth, area.Y + row * cellHeight, cellWidth, cellHeight);
    }

    public static int? QuadrantHitTest(Rectangle area, Point point, int count)
    {
        for (var i = 0; i < count; i++)
            if (QuadrantAt(area, i, count).Contains(point))
                return i;
        return null;
    }

    // outerTransform: whatever transform the caller's own batch was already using (Game1's
    // _renderScale, mapping design-space pixels to the real backbuffer) - needed twice over: the
    // scissor rectangle has to be set in real device pixels regardless of window size/letterboxing,
    // and the batch this method re-opens after its own scissored one has to leave the caller's
    // camera exactly as it found it, or every HUD element drawn afterward this frame ends up in
    // the wrong place.
    public void DrawGrid(SpriteBatch spriteBatch, GraphicsDevice device, WorldSnapshot snapshot, Rectangle area,
        Matrix outerTransform, float totalSeconds)
    {
        var cameras = snapshot.Cameras;
        if (cameras.Count == 0)
        {
            DrawNoCamerasInstalled(spriteBatch, area);
            return;
        }

        for (var i = 0; i < cameras.Count; i++)
            DrawOneCamera(spriteBatch, device, snapshot, QuadrantAt(area, i, cameras.Count), outerTransform,
                cameras[i], totalSeconds, Label(cameras, i), drawFrame: false);
    }

    public void DrawFullscreen(SpriteBatch spriteBatch, GraphicsDevice device, WorldSnapshot snapshot, Rectangle area,
        Matrix outerTransform, int cameraIndex, float totalSeconds)
    {
        var cameras = snapshot.Cameras;
        if (cameraIndex < 0 || cameraIndex >= cameras.Count)
            return;
        DrawOneCamera(spriteBatch, device, snapshot, area, outerTransform, cameras[cameraIndex], totalSeconds,
            Label(cameras, cameraIndex), drawFrame: true);
    }

    // "Камера (Нос)" for a lone camera on that side, "Камера (Нос) 2" once a second one shares it -
    // read straight off MountSide rather than a fixed per-index array, since a hull can carry any
    // number of cameras on any side (Ship.Scout.cs/Ship.cs/Ship.Cruiser.cs/Ship.Corvette.cs).
    private static string Label(IReadOnlyList<HullCamera> cameras, int index)
    {
        var camera = cameras[index];
        var sideName = camera.MountSide switch
        {
            CameraMountSide.Fore => "Нос",
            CameraMountSide.Aft => "Корма",
            CameraMountSide.Port => "Левый борт",
            _ => "Правый борт",
        };
        var sharingTheSide = cameras.Where(c => c.MountSide == camera.MountSide).ToList();
        var slot = sharingTheSide.FindIndex(c => c.Id == camera.Id) + 1;
        return sharingTheSide.Count > 1 ? $"Камера ({sideName}) {slot}" : $"Камера ({sideName})";
    }

    private void DrawOneCamera(SpriteBatch spriteBatch, GraphicsDevice device, WorldSnapshot snapshot, Rectangle area,
        Matrix outerTransform, HullCamera camera, float totalSeconds, string label, bool drawFrame)
    {
        var mount = HullCameraMount.For(snapshot.Rooms, snapshot.Cameras, camera);
        var damaged = snapshot.SystemStates.FirstOrDefault(s => s.DeviceId == camera.Id)?.Damaged ?? false;

        var rotatedSnapshot = snapshot with
        {
            ShipField = snapshot.ShipField with { RotationDegrees = snapshot.ShipField.RotationDegrees + mount.OutwardDegrees },
        };
        var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
        var center = new Vector2(area.Center.X, area.Center.Y);
        var cameraTransform =
            Matrix.CreateTranslation(-center.X, -center.Y, 0f) * Matrix.CreateScale(CameraZoomOut, CameraZoomOut, 1f) *
            Matrix.CreateTranslation(center.X, center.Y, 0f) * outerTransform;

        // Anchored on the mount's own physical position, not the hull centre (ExternalCameraPanel's
        // own doc comment) - real parallax, same formula Game1.ComputeCamera uses for a manned
        // turret's muzzle-anchored view.
        var origin = center - new Vector2((float)mount.Position.X, (float)mount.Position.Y) * ShipRenderer.PixelsPerUnit;

        var previousScissor = device.ScissorRectangle;
        device.ScissorRectangle = DeviceSpaceRect(area, outerTransform, device.Viewport);
        spriteBatch.End();
        spriteBatch.Begin(rasterizerState: new RasterizerState { ScissorTestEnable = true }, transformMatrix: cameraTransform);

        spriteBatch.Draw(_pixel, area, new Color(4, 6, 10));
        if (!damaged)
            _fieldRenderer.Draw(spriteBatch, rotatedSnapshot, origin, hullCenter,
                new Vector2(area.X, area.Y), new Vector2(area.Width, area.Height), totalSeconds);

        spriteBatch.End();
        device.ScissorRectangle = previousScissor;
        spriteBatch.Begin(transformMatrix: outerTransform);

        if (damaged)
            DrawNoSignal(spriteBatch, area, totalSeconds);
        DrawHullSliver(spriteBatch, area, camera);
        DrawFrame(spriteBatch, area, label, drawFrame, damaged);
    }

    // Everything below draws in the caller's own unscaled/unrotated batch (outerTransform only) -
    // rigidly attached to the camera's own housing rather than part of the simulated scene, so it
    // never pans or zooms with what the lens is looking at (M48 - "показывает саму себя... в самом
    // боку экрана").

    // A dark plating strip down one SIDE of the frame, with the lens housing itself overlapping in
    // from that edge (M48 follow-up - "видно часть корпуса корабля и саму камеру сбоку экрана") -
    // reads as "this camera is bolted to the hull right here, looking out past its own housing",
    // without needing to render an actual 3D-consistent slice of the real hull model. Which side
    // mirrors the camera's own MountSide (Port/Fore hug the left edge, Starboard/Aft the right) so
    // a grid of several cameras doesn't all lean the same way.
    private void DrawHullSliver(SpriteBatch spriteBatch, Rectangle area, HullCamera camera)
    {
        var onLeft = camera.MountSide is CameraMountSide.Port or CameraMountSide.Fore;
        var stripWidth = Math.Max(28, area.Width / 7);
        var strip = new Rectangle(onLeft ? area.X : area.Right - stripWidth, area.Y, stripWidth, area.Height);

        spriteBatch.Draw(_pixel, strip, new Color(22, 24, 28));
        // A couple of horizontal seams so the strip reads as plating, not a flat panel.
        for (var i = 1; i < 4; i++)
        {
            var y = area.Y + area.Height * i / 4;
            spriteBatch.Draw(_pixel, new Rectangle(strip.X, y, strip.Width, 2), new Color(46, 50, 56));
        }
        var innerEdgeX = onLeft ? strip.Right - 2 : strip.X;
        spriteBatch.Draw(_pixel, new Rectangle(innerEdgeX, area.Y, 2, area.Height), new Color(70, 76, 84));

        // The camera's own dome, overlapping the strip's inner edge at roughly mid-height - part
        // hull, part housing, the same silhouette a hull-mounted security camera would actually cast.
        var lensSize = Math.Max(36, stripWidth);
        var lensX = onLeft ? strip.Right - lensSize / 3 : strip.X - lensSize * 2 / 3;
        var lensY = area.Y + area.Height / 2 - lensSize / 2;
        var lens = new Rectangle(lensX, lensY, lensSize, lensSize);
        spriteBatch.Draw(_pixel, lens, new Color(14, 15, 17));
        var glass = new Rectangle(lens.X + 4, lens.Y + 4, lens.Width - 8, lens.Height - 8);
        spriteBatch.Draw(_pixel, glass, new Color(38, 78, 64));
        var highlight = new Rectangle(glass.X + 3, glass.Y + 3, Math.Max(2, glass.Width / 3), Math.Max(2, glass.Height / 3));
        spriteBatch.Draw(_pixel, highlight, new Color(130, 210, 180) * 0.65f);
    }

    private void DrawNoSignal(SpriteBatch spriteBatch, Rectangle area, float totalSeconds)
    {
        spriteBatch.Draw(_pixel, area, Color.Black);
        // A slow flicker rather than a static fill - reads as "dead", not just "black".
        var flicker = 0.5f + 0.5f * MathF.Sin(totalSeconds * 6f);
        var text = "НЕТ СИГНАЛА";
        var size = _font.MeasureString(text) * 0.6f;
        spriteBatch.DrawString(_font, text, new Vector2(area.Center.X - size.X / 2, area.Center.Y - size.Y / 2),
            Color.DarkRed * (0.4f + 0.4f * flicker), 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
    }

    private void DrawNoCamerasInstalled(SpriteBatch spriteBatch, Rectangle area)
    {
        spriteBatch.Draw(_pixel, area, new Color(10, 10, 12));
        var text = "НА ЭТОМ КОРПУСЕ НЕТ КАМЕР";
        var size = _font.MeasureString(text) * 0.55f;
        spriteBatch.DrawString(_font, text, new Vector2(area.Center.X - size.X / 2, area.Center.Y - size.Y / 2),
            Color.Gray, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
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

    private void DrawFrame(SpriteBatch spriteBatch, Rectangle area, string label, bool thick, bool damaged)
    {
        var thickness = thick ? 4 : 2;
        var color = damaged ? Color.DarkRed : Color.DarkSlateGray;
        spriteBatch.Draw(_pixel, new Rectangle(area.X, area.Y, area.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(area.X, area.Bottom - thickness, area.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(area.X, area.Y, thickness, area.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(area.Right - thickness, area.Y, thickness, area.Height), color);
        spriteBatch.DrawString(_font, label, new Vector2(area.X + 6, area.Y + 4),
            damaged ? Color.OrangeRed : Color.LimeGreen, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
    }
}
