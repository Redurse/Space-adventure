using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The fog of war over the system map (FogGrid.cs, World.Exploration.cs). Unexplored space is a haze rather
// than plain black - black would be indistinguishable from empty explored space - with a soft, faintly lit
// edge where it meets what the crew has seen, and slow lumps of cloud drifting inside it. The haze is one
// texture rebuilt only when the explored cells change; the drift is a few sprites on top, so it costs nothing
// to animate.
public sealed partial class GalaxyMapPanel
{
    private const int FogTexelsPerCell = 4;
    private const int FogTextureSize = FogGrid.Size * FogTexelsPerCell;
    private static readonly Color SensorRingColor = new(110, 200, 220);

    // The helm fog is deliberately not opaque: the nebula shows through it, and what it must hide (planets,
    // belt rocks) is simply not drawn until explored (GalaxyMapPanel.Bodies.cs).
    // Haze, lit edge and drifting cloud: for the helm's map, and for the console's green screen.
    private static readonly (Color Haze, Color Edge, Color Cloud, float Opacity) HelmFog = (new Color(30, 38, 54), new Color(125, 190, 230), new Color(128, 146, 176), 0.8f);
    private static readonly (Color Haze, Color Edge, Color Cloud, float Opacity) SonarFog = (new Color(7, 24, 15), new Color(70, 170, 110), new Color(40, 90, 60), 0.88f);

    private Texture2D? _fogTexture;
    private byte[]? _fogBitsShown;
    private bool _fogBuiltForSonar;

    private void DrawFog(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 mapOrigin, float zoom, float totalSeconds)
    {
        if (snapshot.Fog is not { } fog)
            return;
        var palette = _sonar ? SonarFog : HelmFog;
        if (_fogTexture is null || !ReferenceEquals(fog.Bits, _fogBitsShown) || _fogBuiltForSonar != _sonar)
        {
            _fogTexture ??= new Texture2D(spriteBatch.GraphicsDevice, FogTextureSize, FogTextureSize);
            _fogTexture.SetData(BuildFogPixels(fog.Bits, palette.Haze, palette.Edge, palette.Opacity));
            _fogBitsShown = fog.Bits;
            _fogBuiltForSonar = _sonar;
        }

        var topLeft = FieldToScreen(mapOrigin, new Vec2(0, 0), zoom);
        var size = fog.FieldSize * PixelsPerUnit * zoom;
        spriteBatch.Draw(_fogTexture, topLeft, null, Color.White, 0f, Vector2.Zero, size / FogTextureSize, SpriteEffects.None, 0f);

        // Past the edge of the charted field everything is unknown too.
        const float far = 6000f;
        var veil = palette.Haze * palette.Opacity;
        spriteBatch.Draw(_pixel, new Rectangle((int)(topLeft.X - far), (int)(topLeft.Y - far), (int)(size + far * 2), (int)far), veil);
        spriteBatch.Draw(_pixel, new Rectangle((int)(topLeft.X - far), (int)(topLeft.Y + size), (int)(size + far * 2), (int)far), veil);
        spriteBatch.Draw(_pixel, new Rectangle((int)(topLeft.X - far), (int)topLeft.Y, (int)far, (int)size), veil);
        spriteBatch.Draw(_pixel, new Rectangle((int)(topLeft.X + size), (int)topLeft.Y, (int)far, (int)size), veil);

        DrawFogDrift(spriteBatch, fog.Bits, topLeft, size / FogGrid.Size, palette.Cloud, totalSeconds);
    }

    // Lumps of cloud, one per block of cells that is fogged right through, each swaying around its own spot.
    private void DrawFogDrift(SpriteBatch spriteBatch, byte[] bits, Vector2 topLeft, float cellPixels, Color cloud, float totalSeconds)
    {
        const int step = 8;
        const int probe = 5;
        var radius = cellPixels * 6f;
        if (radius < 10f)
            return;
        static bool Fogged(byte[] b, int x, int y) =>
            x < 0 || y < 0 || x >= FogGrid.Size || y >= FogGrid.Size || !FogGrid.IsRevealed(b, x, y);

        for (var cy = step / 2; cy < FogGrid.Size; cy += step)
        for (var cx = step / 2; cx < FogGrid.Size; cx += step)
        {
            // A lump is as wide as its probes reach, so it never spills onto explored space.
            if (!Fogged(bits, cx, cy) || !Fogged(bits, cx - probe, cy) || !Fogged(bits, cx + probe, cy)
                || !Fogged(bits, cx, cy - probe) || !Fogged(bits, cx, cy + probe)
                || !Fogged(bits, cx - 4, cy - 4) || !Fogged(bits, cx + 4, cy - 4)
                || !Fogged(bits, cx - 4, cy + 4) || !Fogged(bits, cx + 4, cy + 4))
                continue;
            var a = MapArt.Hash(cx, cy, 5);
            var b = MapArt.Hash(cx, cy, 9);
            var sway = new Vector2(MathF.Sin(totalSeconds * 0.11f + a * MathF.Tau), MathF.Cos(totalSeconds * 0.09f + b * MathF.Tau)) * cellPixels * 1.2f;
            var center = topLeft + new Vector2(cx + 0.5f, cy + 0.5f) * cellPixels + sway;
            if (!IsOnScreen(center, radius))
                continue;
            var alpha = 0.17f + 0.07f * MathF.Sin(totalSeconds * 0.2f + a * 9f);
            spriteBatch.Draw(_cloudPuff, center, null, cloud * alpha, a * MathF.Tau + totalSeconds * 0.01f,
                new Vector2(64f, 64f), radius * 2f / 128f, SpriteEffects.None, 0f);
        }
    }

    // FogTexelsPerCell texels per cell: cover is interpolated between cell centres (a soft edge), broken up
    // with two octaves of noise so the haze reads as cloud, and lit along the boundary.
    private static Color[] BuildFogPixels(byte[] bits, Color haze, Color edge, float opacity)
    {
        // Each cell first takes the average of itself and its eight neighbours, which rounds the stair-step
        // outline of a block of cells into something closer to a curve.
        var soft = new float[FogGrid.Size * FogGrid.Size];
        for (var y = 0; y < FogGrid.Size; y++)
        for (var x = 0; x < FogGrid.Size; x++)
        {
            var sum = 0;
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
                if (!FogGrid.IsRevealed(bits, Math.Clamp(x + dx, 0, FogGrid.Size - 1), Math.Clamp(y + dy, 0, FogGrid.Size - 1)))
                    sum++;
            soft[y * FogGrid.Size + x] = sum / 9f;
        }
        float Covered(byte[] b, int x, int y) => soft[Math.Clamp(y, 0, FogGrid.Size - 1) * FogGrid.Size + Math.Clamp(x, 0, FogGrid.Size - 1)];

        var hazeColor = haze.ToVector3();
        var edgeColor = edge.ToVector3();
        var pixels = new Color[FogTextureSize * FogTextureSize];
        for (var ty = 0; ty < FogTextureSize; ty++)
        for (var tx = 0; tx < FogTextureSize; tx++)
        {
            var cx = (tx + 0.5f) / FogTexelsPerCell - 0.5f;
            var cy = (ty + 0.5f) / FogTexelsPerCell - 0.5f;
            var x0 = (int)MathF.Floor(cx);
            var y0 = (int)MathF.Floor(cy);
            var fx = cx - x0;
            var fy = cy - y0;
            var blurred = MathHelper.Lerp(
                MathHelper.Lerp(Covered(bits, x0, y0), Covered(bits, x0 + 1, y0), fx),
                MathHelper.Lerp(Covered(bits, x0, y0 + 1), Covered(bits, x0 + 1, y0 + 1), fx), fy);
            var raw = MapArt.Smooth(0.2f, 0.8f, blurred);
            if (raw <= 0f)
                continue; // stays transparent

            var cover = raw * raw * (3f - 2f * raw);
            var rim = 4f * raw * (1f - raw); // 1 on the boundary itself, 0 well inside and outside
            var cloud = 0.6f * MapArt.Noise(tx / 9f, ty / 9f, 0) + 0.4f * MapArt.Noise(tx / 3.5f, ty / 3.5f, 0);
            var alpha = MathF.Min(1f, cover * opacity * (0.82f + 0.18f * cloud) + rim * 0.1f);
            var color = Vector3.Lerp(hazeColor * (0.85f + 0.3f * cloud), edgeColor, rim * 0.5f);
            pixels[ty * FogTextureSize + tx] = new Color(color.X * alpha, color.Y * alpha, color.Z * alpha, alpha);
        }
        return pixels;
    }

    private static bool IsExplored(WorldSnapshot snapshot, Vec2 position)
    {
        if (snapshot.Fog is not { } fog)
            return true;
        var cell = fog.FieldSize / FogGrid.Size;
        return FogGrid.IsRevealed(fog.Bits, (int)Math.Floor(position.X / cell), (int)Math.Floor(position.Y / cell));
    }

    // How far the ship's own sensors see: fog clears inside this circle as the ship moves.
    private void DrawSensorRing(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 shipCenter, float zoom)
    {
        if (snapshot.Fog is not { } fog)
            return;
        var radius = fog.SensorRadius * PixelsPerUnit * zoom;
        if (radius < 6f)
            return;
        var color = Ink(SensorRingColor);
        HudIcons.DrawRingArc(spriteBatch, _pixel, shipCenter, radius, 0f, 360f, color * 0.1f, 64, 4f);
        const int dashes = 48;
        for (var i = 0; i < dashes; i++)
            HudIcons.DrawRingArc(spriteBatch, _pixel, shipCenter, radius, i * 360f / dashes, (i + 0.55f) * 360f / dashes, color * 0.5f, 3, 1.5f);
    }
}
