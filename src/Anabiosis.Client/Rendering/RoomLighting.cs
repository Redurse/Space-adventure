using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// One shadowed, coloured point light - a ceiling lamp, the reactor's own glow. Position/Radius are
// in world units. Color is what the light adds on top of RoomLighting.Build's floor colour, at its
// own centre, fading to nothing at Radius - so a dim Color makes a weak lamp, not a small one.
public readonly record struct PointLight(Vector2 Position, float Radius, Color Color);

// Ship/room mood lighting: a lamp per room, tinted by the department colour RoomDecor already
// paints the floor with, that dims and reddens toward RoomLighting.Build's floor colour as ship
// power (game_design.md's "Secondary" slider - airlocks/lights/scanner) drops. Unlike VisibilityMask
// (which gates what the player can see at all - truly black beyond it), this never darkens what the
// player's own sight already shows: CompositeWithSight folds the two together with a per-pixel Max,
// so a lit room stays lit beyond the player's own lamp reach, and the player's own lamp still works
// at full strength in a room with no power at all - ship lighting is a mood layer on top of sight,
// not a second gate on it.
public sealed class RoomLighting : IDisposable
{
    private static readonly BlendState Multiply = new()
    {
        ColorSourceBlend = Blend.Zero,
        ColorDestinationBlend = Blend.SourceColor,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
    };

    private static readonly BlendState Max = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Max,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Max,
    };

    // Lower than VisibilityMask's own falloff start: a room lamp is meant to fill the compartment
    // evenly rather than read as a tight hotspot in the middle of it.
    private const float FalloffStart = 0.45f;

    // How much brighter than white the centre of a lamp is allowed to get. 1 reproduces the old
    // low dynamic range behaviour exactly.
    public float Intensity { get; set; } = 3.2f;

    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    // Null when the content build has not produced Shaders/Light - the vertex-colour path below
    // still works and looks nearly the same, just faceted along the ray fan.
    private readonly Effect? _lightEffect;
    private readonly List<float> _offsets = new();
    private RenderTarget2D? _target;
    private VertexPositionColor[] _vertices = new VertexPositionColor[3 * 512];
    private int _vertexCount;

    public RoomLighting(GraphicsDevice device, Effect? lightEffect = null)
    {
        _device = device;
        _lightEffect = lightEffect;
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
            LightingEnabled = false,
            View = Matrix.Identity,
        };
    }

    // walls/lights are in the same world units the scene is drawn from; origin/renderScale are
    // exactly what the scene's SpriteBatch used, same contract as VisibilityMask.Build. `floor` is
    // the mask's clear colour - what every pixel outside every light's reach settles to.
    public bool Build(IReadOnlyList<WallSegment> walls, IReadOnlyList<PointLight> lights, Color floor,
        Vector2 origin, Matrix renderScale)
    {
        if (!EnsureTarget())
            return false;

        if (_lightEffect is not null)
        {
            RasterizePerPixel(walls, lights, renderScale, floor, origin);
            return true;
        }

        _vertexCount = 0;
        foreach (var light in lights)
            AddLight(walls, light, origin, fadeIntoVertices: true);
        Rasterize(renderScale, floor);
        return true;
    }

    // fadeIntoVertices bakes the distance falloff into the rim vertex colours, which is what the
    // BasicEffect path needs and what the per-pixel path must not have - it measures the distance
    // itself, and pre-faded vertices would apply the curve twice.
    private void AddLight(IReadOnlyList<WallSegment> walls, PointLight light, Vector2 origin, bool fadeIntoVertices)
    {
        ShadowCast.CollectRayOffsets(_offsets, walls, light.Position, 0f, MathF.PI * 2f, full: true);

        var rayCount = _offsets.Count;
        Grow(_vertexCount + rayCount * 3);
        var center = new VertexPositionColor(
            new Vector3(origin + light.Position * ShipRenderer.PixelsPerUnit, 0f), light.Color);

        var previous = RimVertex(_offsets[0], walls, light, origin, fadeIntoVertices);
        for (var i = 1; i <= rayCount; i++)
        {
            var current = RimVertex(_offsets[i % rayCount], walls, light, origin, fadeIntoVertices);
            _vertices[_vertexCount++] = center;
            _vertices[_vertexCount++] = previous;
            _vertices[_vertexCount++] = current;
            previous = current;
        }
    }

    private VertexPositionColor RimVertex(float offset, IReadOnlyList<WallSegment> walls, PointLight light, Vector2 origin, bool fadeIntoVertices)
    {
        var direction = new Vector2(MathF.Cos(offset), MathF.Sin(offset));
        var distance = ShadowCast.Cast(light.Position, direction, walls, light.Radius);
        var point = light.Position + direction * distance;

        var fade = fadeIntoVertices ? Falloff(distance / light.Radius) : 1f;
        return new VertexPositionColor(
            new Vector3(origin + point * ShipRenderer.PixelsPerUnit, 0f),
            light.Color * fade);
    }

    // One draw per lamp, because each one needs its own centre and radius in the shader. That is a
    // handful of draw calls for a room, against the one the vertex-colour path needed - cheap for
    // a pool of light that is actually round instead of faceted along the ray fan.
    private void RasterizePerPixel(IReadOnlyList<WallSegment> walls, IReadOnlyList<PointLight> lights,
        Matrix renderScale, Color floor, Vector2 origin)
    {
        _device.SetRenderTarget(_target);
        _device.Clear(floor);
        _device.BlendState = BlendState.Additive;
        _device.DepthStencilState = DepthStencilState.None;
        _device.RasterizerState = RasterizerState.CullNone;

        var projection = Matrix.CreateOrthographicOffCenter(0, _target!.Width, _target.Height, 0, 0f, 1f);
        _lightEffect!.Parameters["WorldViewProjection"]?.SetValue(renderScale * projection);
        _lightEffect.Parameters["FalloffStart"]?.SetValue(FalloffStart);
        _lightEffect.Parameters["Intensity"]?.SetValue(Intensity);

        foreach (var light in lights)
        {
            _vertexCount = 0;
            AddLight(walls, light, origin, fadeIntoVertices: false);
            if (_vertexCount < 3)
                continue;

            _lightEffect.Parameters["LightCenter"]?.SetValue(origin + light.Position * ShipRenderer.PixelsPerUnit);
            _lightEffect.Parameters["LightRadius"]?.SetValue(light.Radius * ShipRenderer.PixelsPerUnit);
            foreach (var pass in _lightEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                _device.DrawUserPrimitives(PrimitiveType.TriangleList, _vertices, 0, _vertexCount / 3);
            }
        }

        _device.SetRenderTarget(null);
    }

    private static float Falloff(float t)
    {
        if (t <= FalloffStart)
            return 1f;
        var fade = 1f - MathHelper.Clamp((t - FalloffStart) / (1f - FalloffStart), 0f, 1f);
        return fade * fade;
    }

    private void Rasterize(Matrix renderScale, Color floor)
    {
        _device.SetRenderTarget(_target);
        _device.Clear(floor);

        if (_vertexCount >= 3)
        {
            // Vertex colours cannot exceed 1, so the headroom comes from here: BasicEffect folds
            // DiffuseColor into the vertex colour, and unlike a colour channel it is a float.
            _effect.DiffuseColor = new Vector3(Intensity);
            _effect.World = renderScale;
            _effect.Projection = Matrix.CreateOrthographicOffCenter(0, _target!.Width, _target.Height, 0, 0f, 1f);
            // Additive on top of the floor clear colour: a lamp brightens its own pool without ever
            // darkening the room outside its reach below the floor level.
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

    // Folds `sight` (the player's own vision, which can go all the way to black) into this target
    // via a per-pixel Max, so whichever of "the room is lit" and "you can personally see it" is
    // brighter wins. Must be called before the backbuffer is drawn into, same as Build - it switches
    // render targets, and the backbuffer discards its contents on a render target switch (its
    // RenderTargetUsage defaults to DiscardContents), so doing this after the scene is drawn would
    // silently blank the whole frame instead of just multiplying it.
    public void MergeSight(SpriteBatch spriteBatch, VisibilityMask sight)
    {
        if (_target is null)
            return;

        _device.SetRenderTarget(_target);
        spriteBatch.Begin(SpriteSortMode.Deferred, Max, SamplerState.LinearClamp);
        sight.DrawRaw(spriteBatch);
        spriteBatch.End();
        _device.SetRenderTarget(null);
    }

    // The finished mask itself, for passes that need to know how lit a pixel is rather than just
    // multiply by it: ScenePost reads it to decide what is allowed to glow and which way the light
    // is coming from. Null until Build has run.
    public Texture2D? Mask => _target;

    // Multiplies this mask (already merged with sight, if any, via MergeSight) over the finished
    // scene - no render target switching here, so it's safe to call after the scene is drawn.
    public void Composite(SpriteBatch spriteBatch)
    {
        if (_target is null)
            return;

        spriteBatch.Begin(SpriteSortMode.Deferred, Multiply, SamplerState.LinearClamp);
        spriteBatch.Draw(_target, Vector2.Zero, Color.White);
        spriteBatch.End();
    }

    private bool EnsureTarget()
    {
        var viewport = _device.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return false;
        if (_target is not null && _target.Width == viewport.Width && _target.Height == viewport.Height)
            return true;

        _target?.Dispose();
        // HalfVector4, not Color: a lamp has to be able to shine brighter than white. In an 8-bit
        // target everything clamps at 1, which is why bloom here used to need a fudge factor to
        // notice a lamp at all. With headroom above 1 the pool of light around a lamp is genuinely
        // brighter than the wall it falls on, and the bright pass can just look at it.
        _target = new RenderTarget2D(_device, viewport.Width, viewport.Height, false, SurfaceFormat.HalfVector4, DepthFormat.None);
        return true;
    }

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
