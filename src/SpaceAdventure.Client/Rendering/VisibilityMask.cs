using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// True line-of-sight: the player only sees what an unobstructed straight line reaches from where
// they stand, so a wall hides whatever is behind it and an open doorway lets the view through into
// the next compartment (game_design.md section 2 - "всё остальное — чёрное"). Everything outside
// that reach is painted absolutely black, not merely dimmed.
//
// Done as a light mask rather than as shadow shapes drawn on top: the visibility polygon is
// rasterized white-on-black into a render target (a classic corner-sweep polygon - one ray per
// wall corner plus a nudge either side of it, so shadow edges land exactly on the corner instead
// of a stair-stepped approximation), then multiplied over the finished scene. Multiplying by zero
// is what makes the unseen parts truly black - no alpha to leak detail through.
public sealed class VisibilityMask : IDisposable
{
    private static readonly BlendState Multiply = new()
    {
        ColorSourceBlend = Blend.Zero,
        ColorDestinationBlend = Blend.SourceColor,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
    };

    // Uniform rays on top of the corner rays: they round off the light's outer rim where no wall
    // is in the way (a corner sweep alone would give a polygon with long straight chords there).
    private const int ArcSamples = 72;
    private const float CornerNudge = 0.0008f;
    private const float FalloffStart = 0.72f; // fraction of the radius where the light starts fading

    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly List<float> _offsets = new();
    private RenderTarget2D? _target;
    private VertexPositionColor[] _vertices = new VertexPositionColor[3 * 256];
    private int _vertexCount;

    public VisibilityMask(GraphicsDevice device)
    {
        _device = device;
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
            LightingEnabled = false,
            View = Matrix.Identity,
        };
    }

    // eye/walls are in the same world units the scene is drawn from; origin/renderScale are exactly
    // what the scene's SpriteBatch used, so the mask lines up pixel for pixel with what it hides.
    // coneHalfAngleDegrees >= 180 means an all-round light (unsuited); anything less is the suit
    // helmet's forward cone.
    // ambientRadius adds a second, all-round pool of light around the character on top of the cone -
    // what spills off the helmet lamp onto whatever is right next to you. Zero disables it (an
    // all-round light needs no companion).
    public bool Build(IReadOnlyList<WallSegment> walls, Vector2 eye, Vector2 facing, float radius,
        float coneHalfAngleDegrees, float ambientRadius, Vector2 origin, Matrix renderScale)
    {
        if (!EnsureTarget())
            return false;

        var baseAngle = facing.LengthSquared() > 1e-6f ? MathF.Atan2(facing.Y, facing.X) : 0f;
        _vertexCount = 0;
        AddLightPolygon(walls, eye, baseAngle, coneHalfAngleDegrees, radius, origin);
        if (ambientRadius > 0f && coneHalfAngleDegrees < 179.9f)
            AddLightPolygon(walls, eye, 0f, 180f, ambientRadius, origin);
        Rasterize(renderScale);
        return true;
    }

    private void AddLightPolygon(IReadOnlyList<WallSegment> walls, Vector2 eye, float baseAngle,
        float halfAngleDegrees, float radius, Vector2 origin)
    {
        var full = halfAngleDegrees >= 179.9f;
        var span = full ? MathF.PI * 2f : halfAngleDegrees * 2f * MathF.PI / 180f;
        var start = full ? 0f : baseAngle - span / 2f;

        CollectRayOffsets(walls, eye, start, span, full);
        BuildTriangles(walls, eye, start, radius, origin, full);
    }

    public void Composite(SpriteBatch spriteBatch)
    {
        if (_target is null)
            return;

        spriteBatch.Begin(SpriteSortMode.Deferred, Multiply, SamplerState.LinearClamp);
        spriteBatch.Draw(_target, Vector2.Zero, Color.White);
        spriteBatch.End();
    }

    // Angles are kept as offsets from the sweep's start so a cone and a full circle sort the same
    // way and never wrap in the middle of the fan.
    private void CollectRayOffsets(IReadOnlyList<WallSegment> walls, Vector2 eye, float start, float span, bool full)
    {
        _offsets.Clear();
        for (var i = 0; i <= ArcSamples; i++)
            _offsets.Add(span * i / ArcSamples);

        foreach (var wall in walls)
        {
            AddCorner(wall.Ax - eye.X, wall.Ay - eye.Y, start, span, full);
            AddCorner(wall.Bx - eye.X, wall.By - eye.Y, start, span, full);
        }

        _offsets.Sort();
    }

    private void AddCorner(float dx, float dy, float start, float span, bool full)
    {
        var offset = Wrap(MathF.Atan2(dy, dx) - start);
        if (!full && offset > span)
            return;

        if (offset > CornerNudge)
            _offsets.Add(offset - CornerNudge);
        _offsets.Add(offset);
        if (full || offset + CornerNudge <= span)
            _offsets.Add(offset + CornerNudge);
    }

    private void BuildTriangles(IReadOnlyList<WallSegment> walls, Vector2 eye, float start, float radius,
        Vector2 origin, bool full)
    {
        var rayCount = _offsets.Count;
        var edgeCount = full ? rayCount : rayCount - 1;
        Grow(_vertexCount + edgeCount * 3);

        var center = new VertexPositionColor(
            new Vector3(origin + eye * ShipRenderer.PixelsPerUnit, 0f), Color.White);

        var previous = RimVertex(_offsets[0], walls, eye, start, radius, origin);
        for (var i = 1; i <= edgeCount; i++)
        {
            var current = RimVertex(_offsets[i % rayCount], walls, eye, start, radius, origin);
            _vertices[_vertexCount++] = center;
            _vertices[_vertexCount++] = previous;
            _vertices[_vertexCount++] = current;
            previous = current;
        }
    }

    private VertexPositionColor RimVertex(float offset, IReadOnlyList<WallSegment> walls, Vector2 eye,
        float start, float radius, Vector2 origin)
    {
        var angle = start + offset;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var distance = Cast(eye, direction, walls, radius);
        var point = eye + direction * distance;

        var shade = Falloff(distance / radius);
        return new VertexPositionColor(
            new Vector3(origin + point * ShipRenderer.PixelsPerUnit, 0f),
            new Color(shade, shade, shade));
    }

    // Fades near the edge of the light's reach instead of ending on a hard circle; a wall lit at
    // point-blank range stays fully bright.
    private static float Falloff(float t)
    {
        if (t <= FalloffStart)
            return 1f;
        var fade = 1f - MathHelper.Clamp((t - FalloffStart) / (1f - FalloffStart), 0f, 1f);
        return fade * fade;
    }

    // Nearest wall the ray meets, or maxDistance if it reaches that far unobstructed.
    private static float Cast(Vector2 eye, Vector2 direction, IReadOnlyList<WallSegment> walls, float maxDistance)
    {
        var best = maxDistance;
        foreach (var wall in walls)
        {
            var sx = wall.Bx - wall.Ax;
            var sy = wall.By - wall.Ay;
            var denominator = direction.X * sy - direction.Y * sx;
            if (MathF.Abs(denominator) < 1e-6f)
                continue;

            var qx = wall.Ax - eye.X;
            var qy = wall.Ay - eye.Y;
            var t = (qx * sy - qy * sx) / denominator;
            if (t <= 1e-4f || t >= best)
                continue;

            var u = (qx * direction.Y - qy * direction.X) / denominator;
            if (u < -1e-4f || u > 1f + 1e-4f)
                continue;

            best = t;
        }
        return best;
    }

    private void Rasterize(Matrix renderScale)
    {
        _device.SetRenderTarget(_target);
        _device.Clear(Color.Black);

        if (_vertexCount >= 3)
        {
            _effect.World = renderScale;
            _effect.Projection = Matrix.CreateOrthographicOffCenter(0, _target!.Width, _target.Height, 0, 0f, 1f);
            // Additive so the cone and the ambient pool combine where they overlap instead of the
            // second one punching the first back down to its own dimmer level.
            _device.BlendState = BlendState.Additive;
            _device.DepthStencilState = DepthStencilState.None;
            _device.RasterizerState = RasterizerState.CullNone;

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserPrimitives(PrimitiveType.TriangleList, _vertices, 0, _vertexCount / 3);
            }
        }

        _device.SetRenderTarget(null);
    }

    private bool EnsureTarget()
    {
        var viewport = _device.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return false;
        if (_target is not null && _target.Width == viewport.Width && _target.Height == viewport.Height)
            return true;

        _target?.Dispose();
        _target = new RenderTarget2D(_device, viewport.Width, viewport.Height, false, SurfaceFormat.Color, DepthFormat.None);
        return true;
    }

    // Resize, not reallocate: the cone's triangles are already in there when the ambient pool grows
    // the array for its own.
    private void Grow(int needed)
    {
        if (_vertices.Length < needed)
            Array.Resize(ref _vertices, needed * 2);
    }

    private static float Wrap(float angle)
    {
        const float twoPi = MathF.PI * 2f;
        angle %= twoPi;
        return angle < 0 ? angle + twoPi : angle;
    }

    public void Dispose()
    {
        _target?.Dispose();
        _effect.Dispose();
    }
}
