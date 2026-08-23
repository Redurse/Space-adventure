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
    private float _falloffStart = FalloffStart;
    private Color _floor = Color.Black;
    // How much of each side of the cone is spent fading out, as a fraction of its half-angle. Zero
    // is the old behaviour: brightness holds right up to the last ray and then stops, which draws
    // two straight edges meeting at a point - a shape, not a light. Nothing that emits light has a
    // hard boundary; what it has is a rim you cannot find the end of.
    private float _edgeFade;
    private float _coneSpan = MathF.PI * 2f;
    // What colour the cone lights things. The mask multiplies the scene, so this is not decoration:
    // a warm tint here means surfaces the lamp finds come back warm, which is what a torch does and
    // what a neutral grey mask never can.
    private Vector3 _tint = Vector3.One;

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
    /// <param name="falloffStart">Where the light starts dying, as a fraction of its reach. A room
    /// lamp really does fill a room fairly evenly, so indoors this stays high; a helmet lamp does
    /// not, and a cone held at full brightness for three quarters of its length is what makes it read
    /// as a grey slab drawn over the screen rather than as light.</param>
    /// <param name="floor">What the mask holds where no light reaches. Black hides everything
    /// equally, which is wrong outside: the mask multiplies the whole picture, stars included, and a
    /// star is a light source at infinity - a lamp on your helmet has no say in whether you can see
    /// one. A small floor keeps the pinpricks and still swallows plating, because the difference
    /// between them is a factor of twenty in brightness.</param>
    public bool Build(IReadOnlyList<WallSegment> walls, Vector2 eye, Vector2 facing, float radius,
        float coneHalfAngleDegrees, float ambientRadius, Vector2 origin, Matrix renderScale,
        float? falloffStart = null, Color? floor = null, float edgeFade = 0f, Vector3? coneTint = null)
    {
        if (!EnsureTarget())
            return false;

        _falloffStart = falloffStart ?? FalloffStart;
        _floor = floor ?? Color.Black;
        _edgeFade = edgeFade;
        _tint = coneTint ?? Vector3.One;

        var baseAngle = facing.LengthSquared() > 1e-6f ? MathF.Atan2(facing.Y, facing.X) : 0f;
        _vertexCount = 0;
        AddLightPolygon(walls, eye, baseAngle, coneHalfAngleDegrees, radius, origin, flatBrightness: null);
        if (ambientRadius > 0f && coneHalfAngleDegrees < 179.9f)
        {
            // The halo is not the lamp - it is what being close to something gets you - so it is not
            // tinted with the lamp's colour, and the cold floor underneath shows through it.
            var lampTint = _tint;
            _tint = Vector3.One;
            AddLightPolygon(walls, eye, 0f, 180f, ambientRadius, origin, AmbientBrightness);
            _tint = lampTint;
        }
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
        // A full circle has no sides to fade, so the angular term is only ever applied to a cone.
        _coneSpan = full ? 0f : span;

        ShadowCast.CollectRayOffsets(_offsets, walls, eye, start, span, full);
        BuildTriangles(walls, eye, start, radius, origin, full, flatBrightness);
    }

    // The finished mask itself, for passes that need to know how lit a pixel is rather than just
    // multiply by it: ScenePost reads it to decide what is allowed to glow and which way the light
    // is coming from. Null until Build has run.
    public Texture2D? Mask => _target;

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
            new Vector3(origin + eye * ShipRenderer.PixelsPerUnit, 0f), Shade(centerShade));

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

        var shade = flatBrightness ?? Falloff(distance / radius) * EdgeFade(offset);
        return new VertexPositionColor(
            new Vector3(origin + point * ShipRenderer.PixelsPerUnit, 0f), Shade(shade));
    }

    // Fades near the edge of the light's reach instead of ending on a hard circle; a wall lit at
    // point-blank range stays fully bright.
    // Fades a ray by how close it lies to the edge of the cone. Squared, so the middle of the beam
    // keeps its brightness and only the last part of the sweep gives way - a linear fade across the
    // whole span would flatten the beam into a smear with no direction to it.
    private Color Shade(float value) => new(value * _tint.X, value * _tint.Y, value * _tint.Z);

    private float EdgeFade(float offset)
    {
        if (_edgeFade <= 0f || _coneSpan <= 0f)
            return 1f;
        var across = MathF.Abs(offset / _coneSpan - 0.5f) * 2f;   // 0 down the middle, 1 at either edge
        var into = MathHelper.Clamp((across - (1f - _edgeFade)) / _edgeFade, 0f, 1f);
        var fade = 1f - into;
        return fade * fade;
    }

    private float Falloff(float t)
    {
        if (t <= _falloffStart)
            return 1f;
        var fade = 1f - MathHelper.Clamp((t - _falloffStart) / (1f - _falloffStart), 0f, 1f);
        return fade * fade;
    }

    private void Rasterize(Matrix renderScale)
    {
        _device.SetRenderTarget(_target);
        _device.Clear(_floor);

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
