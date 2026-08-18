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

    private const float FalloffStart = 0.72f; // fraction of the radius where the light starts fading

    // The ambient pool reads as poor, uniform visibility right around the character - not a second
    // good-visibility cone - so unlike the cone's own bright-centre-fading-to-black it's one flat, dim
    // value everywhere inside its own reach.
    private const float AmbientBrightness = 0.3f;

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
        AddLightPolygon(walls, eye, baseAngle, coneHalfAngleDegrees, radius, origin, flatBrightness: null);
        if (ambientRadius > 0f && coneHalfAngleDegrees < 179.9f)
            AddLightPolygon(walls, eye, 0f, 180f, ambientRadius, origin, AmbientBrightness);
        Rasterize(renderScale);
        return true;
    }

    // flatBrightness: null keeps the cone's own bright-near/fading-far look (Falloff); a value makes
    // the whole polygon that one uniform shade instead - the ambient pool's "dim, not really seeing
    // detail" read, rather than a second cone of good visibility wrapped around the character.
    private void AddLightPolygon(IReadOnlyList<WallSegment> walls, Vector2 eye, float baseAngle,
        float halfAngleDegrees, float radius, Vector2 origin, float? flatBrightness)
    {
        var full = halfAngleDegrees >= 179.9f;
        var span = full ? MathF.PI * 2f : halfAngleDegrees * 2f * MathF.PI / 180f;
        var start = full ? 0f : baseAngle - span / 2f;

        ShadowCast.CollectRayOffsets(_offsets, walls, eye, start, span, full);
        BuildTriangles(walls, eye, start, radius, origin, full, flatBrightness);
    }

    public void Composite(SpriteBatch spriteBatch)
    {
        if (_target is null)
            return;

        spriteBatch.Begin(SpriteSortMode.Deferred, Multiply, SamplerState.LinearClamp);
        spriteBatch.Draw(_target, Vector2.Zero, Color.White);
        spriteBatch.End();
    }

    // Draws the mask's own render target as-is, with whatever blend state the caller has already
    // set up - used by RoomLighting to fold the player's own sight into the combined light/sight
    // mask via a Max blend instead of this class's own multiply-onto-backbuffer Composite.
    public void DrawRaw(SpriteBatch spriteBatch)
    {
        if (_target is not null)
            spriteBatch.Draw(_target, Vector2.Zero, Color.White);
    }

    private void BuildTriangles(IReadOnlyList<WallSegment> walls, Vector2 eye, float start, float radius,
        Vector2 origin, bool full, float? flatBrightness)
    {
        var rayCount = _offsets.Count;
        var edgeCount = full ? rayCount : rayCount - 1;
        Grow(_vertexCount + edgeCount * 3);

        var centerShade = flatBrightness ?? 1f;
        var center = new VertexPositionColor(
            new Vector3(origin + eye * ShipRenderer.PixelsPerUnit, 0f), new Color(centerShade, centerShade, centerShade));

        var previous = RimVertex(_offsets[0], walls, eye, start, radius, origin, flatBrightness);
        for (var i = 1; i <= edgeCount; i++)
        {
            var current = RimVertex(_offsets[i % rayCount], walls, eye, start, radius, origin, flatBrightness);
            _vertices[_vertexCount++] = center;
            _vertices[_vertexCount++] = previous;
            _vertices[_vertexCount++] = current;
            previous = current;
        }
    }

    private VertexPositionColor RimVertex(float offset, IReadOnlyList<WallSegment> walls, Vector2 eye,
        float start, float radius, Vector2 origin, float? flatBrightness)
    {
        var angle = start + offset;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var distance = ShadowCast.Cast(eye, direction, walls, radius);
        var point = eye + direction * distance;

        var shade = flatBrightness ?? Falloff(distance / radius);
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

    public void Dispose()
    {
        _target?.Dispose();
        _effect.Dispose();
    }
}
