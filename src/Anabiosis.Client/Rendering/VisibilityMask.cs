using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

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
    // Scratch buffer for ShadowCast.FilterNearby - reused across both AddLightPolygon calls in one
    // Build (cone, then the ambient halo) rather than a fresh List each time.
    private readonly List<WallSegment> _nearbyWalls = new();
    private RenderTarget2D? _target;
    // Indexed rather than a flat triangle list: one center vertex plus one Inner vertex per ray
    // (see WallBleedDepth's own doc comment for the separate, capped fringe/bleed layer), each
    // shared by every triangle that touches it via _indices instead of being duplicated per
    // triangle.
    private VertexPositionColor[] _vertices = new VertexPositionColor[3 * 256];
    private int _vertexCount;
    private short[] _indices = new short[9 * 256];
    private int _indexCount;
    // The base fan's own rim samples, one per ray in `_offsets` - kept around after the fan is built
    // so BuildFringe can interpolate exactly ALONG that same boundary at its own fixed angles instead
    // of re-casting (own doc comment there for why that matters).
    private RaySample[] _raySamples = new RaySample[256];

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
        _indexCount = 0;
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

        // See ShadowCast.FilterNearby's own doc comment - a wall farther than this polygon's own
        // radius could never be hit by its own cast anyway (direct user report, "проблема из-за
        // низкого фпс" - this is the same fix RoomLighting.AddLight needed, just for the player's
        // own sight cone/ambient halo instead of a room's lamp).
        ShadowCast.FilterNearby(_nearbyWalls, walls, eye, radius);
        ShadowCast.CollectRayOffsets(_offsets, _nearbyWalls, eye, start, span, full);
        BuildTriangles(_nearbyWalls, eye, start, span, radius, origin, full, flatBrightness);
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

    // Direct user request ("видимость не заканчивалась тут же на блоке стены, а немного проходила
    // внутрь... затухание за четверть блока") - a ray that actually stopped at a wall (as opposed to
    // one that simply ran out of radius in open space) no longer ends the polygon dead on the wall's
    // own surface. It gets one extra, fading sliver past that point instead, so a wall a hair's width
    // away no longer reads as a razor-sharp black edge.
    private const float WallBleedDepth = 0.25f;
    // A first version gave every ray of the BASE polygon its own fringe quad - direct user report
    // ("все еще та же проблема"): that made the fringe's own cost scale with ShadowCast.
    // CollectRayOffsets' corner-nudge count, i.e. with nearby wall/corner COUNT, exactly the kind of
    // scaling this renderer already had to fight once before (ShadowCast.FilterNearby's own doc
    // comment) - measured directly (ShaderCheck's own "[diagnostic]" check) as roughly DOUBLING
    // Маска's frame cost in a dense, corner-heavy room. The fringe is a soft cosmetic touch, not a
    // gameplay-critical boundary (that's the base polygon below, untouched), so it doesn't need
    // corner-exact precision - it's capped to at most this many segments around the full sweep,
    // regardless of scene complexity, bounding its own added cost to a small constant instead of
    // letting it inherit the base polygon's own (already scene-dependent) ray count. Occasionally
    // means the fringe cuts a straight chord across an unusually dense cluster of corners instead of
    // hugging every one - invisible in an ordinary room, and never affects what's actually visible
    // (that's still decided by the base polygon/ShadowCast.Cast, same as always).
    private const int FringeArcCap = 96;

    private void BuildTriangles(IReadOnlyList<WallSegment> walls, Vector2 eye, float start, float span, float radius,
        Vector2 origin, bool full, float? flatBrightness)
    {
        var rayCount = _offsets.Count;
        var edgeCount = full ? rayCount : rayCount - 1;
        // One center vertex plus one Inner vertex per ray - the exact same shape/cost the base
        // polygon had before the wall-bleed feature existed; each is written once here and then
        // referenced by every triangle that touches it via _indices.
        Grow(_vertexCount + 1 + rayCount);
        GrowIndices(_indexCount + edgeCount * 3);
        GrowSamples(rayCount);

        var baseVertex = _vertexCount;
        var centerShade = flatBrightness ?? 1f;
        _vertices[_vertexCount++] = new VertexPositionColor(
            new Vector3(origin + eye * ShipRenderer.PixelsPerUnit, 0f), Shade(centerShade));

        for (var i = 0; i < rayCount; i++)
        {
            var sample = SampleRay(_offsets[i], walls, eye, start, radius, origin, flatBrightness);
            _raySamples[i] = sample;
            _vertices[_vertexCount++] = new VertexPositionColor(
                new Vector3(origin + sample.WorldPoint * ShipRenderer.PixelsPerUnit, 0f), sample.Color);
        }

        for (var i = 1; i <= edgeCount; i++)
        {
            _indices[_indexCount++] = (short)baseVertex;
            _indices[_indexCount++] = (short)(baseVertex + 1 + (i - 1) % rayCount);
            _indices[_indexCount++] = (short)(baseVertex + 1 + i % rayCount);
        }

        BuildFringe(eye, span, radius, origin, full);
    }

    // The fading sliver just past the wall (WallBleedDepth's own doc comment) - a ring of quads at
    // FringeArcCap FIXED, evenly-spaced target angles around the sweep. Direct user reports, in
    // order: (1) "все еще та же проблема" - giving every ray of the base fan its own fringe quad
    // scaled the fringe's own cost with corner/wall COUNT, so it got capped to a small constant
    // instead; (2) "края зон видимости искажаются при ходьбе" - stepping through the base fan's own
    // ray INDICES at that cap still swam frame to frame, because CollectRayOffsets' corner-nudged
    // list shifts by a ray or two as the player moves and different corners drift in/out of
    // FilterNearby's own radius, so "every Nth ray" kept landing on different actual angles; (3)
    // "какие-то треугольнички чёрные" - switching to fixed angles independently RE-CAST against the
    // walls fixed that, but a fresh cast at a fixed angle doesn't necessarily land exactly on the
    // fan's own boundary (which is corner-nudged, i.e. deliberately NOT a plain even-angle sampling) -
    // near a corner the two could disagree enough to leave a sliver the fan's own fill doesn't reach
    // and the fringe's own quad doesn't cover either, a real gap reading as a stray dark wedge.
    // The actual fix: never re-cast. For each fixed target angle, interpolate ALONG the base fan's
    // own already-built boundary (GetBoundaryPoint, between whichever two ACTUAL rays bracket that
    // angle this frame) - the fringe's own inner edge is then a literal point on the fan's boundary,
    // so it can never gap or overlap it, while still asking for the same fixed set of angles every
    // frame (only the bracketing PAIR and the interpolation fraction drift smoothly as the player
    // moves, never a discontinuous jump). Zero extra Cast calls - the interpolation is real cheap.
    private void BuildFringe(Vector2 eye, float span, float radius, Vector2 origin, bool full)
    {
        var sampleCount = full ? FringeArcCap : FringeArcCap + 1;
        var edgeCount = full ? sampleCount : sampleCount - 1;
        Grow(_vertexCount + sampleCount * 2);
        GrowIndices(_indexCount + edgeCount * 6);

        var baseVertex = _vertexCount;
        var denom = full ? sampleCount : sampleCount - 1;
        for (var i = 0; i < sampleCount; i++)
        {
            var targetOffset = span * i / denom;
            var (worldPoint, color) = GetBoundaryPoint(targetOffset, span, full);

            var toPoint = worldPoint - eye;
            var distance = toPoint.Length();
            var hitWall = distance < radius - 1e-3f;
            var bleed = hitWall && distance > 1e-4f ? MathF.Min(WallBleedDepth, radius - distance) : 0f;
            var outerPoint = distance > 1e-4f ? worldPoint + toPoint / distance * bleed : worldPoint;

            _vertices[_vertexCount++] = new VertexPositionColor(new Vector3(origin + worldPoint * ShipRenderer.PixelsPerUnit, 0f), color);
            _vertices[_vertexCount++] = new VertexPositionColor(new Vector3(origin + outerPoint * ShipRenderer.PixelsPerUnit, 0f), Shade(0f));
        }

        for (var i = 1; i <= edgeCount; i++)
        {
            var prev = baseVertex + (i - 1) % sampleCount * 2;
            var cur = baseVertex + i % sampleCount * 2;
            var prevInner = (short)prev;
            var prevOuter = (short)(prev + 1);
            var curInner = (short)cur;
            var curOuter = (short)(cur + 1);

            _indices[_indexCount++] = prevInner;
            _indices[_indexCount++] = prevOuter;
            _indices[_indexCount++] = curOuter;
            _indices[_indexCount++] = prevInner;
            _indices[_indexCount++] = curOuter;
            _indices[_indexCount++] = curInner;
        }
    }

    // Finds the point ON the base fan's own already-built boundary at `targetOffset` (an angle
    // offset from `start`, same convention _offsets uses) - the two ACTUAL samples in `_offsets`
    // that bracket it, linearly interpolated. `_offsets` is sorted ascending (ShadowCast.
    // CollectRayOffsets' own contract), so a binary search finds the bracket in O(log rayCount).
    private (Vector2 WorldPoint, Color Color) GetBoundaryPoint(float targetOffset, float span, bool full)
    {
        var rayCount = _offsets.Count;
        var idx = _offsets.BinarySearch(targetOffset);
        int lower, upper;
        float t;
        if (idx >= 0)
        {
            lower = idx; upper = idx; t = 0f;
        }
        else
        {
            var insertAt = ~idx; // first index with _offsets[insertAt] > targetOffset
            if (insertAt <= 0)
            {
                lower = 0; upper = 0; t = 0f;
            }
            else if (insertAt >= rayCount)
            {
                if (full)
                {
                    // Past the last sample, before wrapping back to the first - the same seam the
                    // base fan's own `i % rayCount` closes for its last edge.
                    lower = rayCount - 1; upper = 0;
                    var wrapSpan = span - _offsets[lower] + _offsets[upper];
                    t = wrapSpan > 1e-6f ? (targetOffset - _offsets[lower]) / wrapSpan : 0f;
                }
                else
                {
                    lower = upper = rayCount - 1; t = 0f;
                }
            }
            else
            {
                lower = insertAt - 1; upper = insertAt;
                var d = _offsets[upper] - _offsets[lower];
                t = d > 1e-6f ? (targetOffset - _offsets[lower]) / d : 0f;
            }
        }

        var a = _raySamples[lower];
        var b = _raySamples[upper];
        return (Vector2.Lerp(a.WorldPoint, b.WorldPoint, t), Color.Lerp(a.Color, b.Color, t));
    }

    private readonly struct RaySample
    {
        public readonly Vector2 WorldPoint;
        public readonly Color Color;
        public RaySample(Vector2 worldPoint, Color color)
        {
            WorldPoint = worldPoint;
            Color = color;
        }
    }

    private RaySample SampleRay(float offset, IReadOnlyList<WallSegment> walls, Vector2 eye,
        float start, float radius, Vector2 origin, float? flatBrightness)
    {
        var angle = start + offset;
        var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        var distance = ShadowCast.Cast(eye, direction, walls, radius);
        var point = eye + direction * distance;

        // Direct user report ("резкий чёрный экран") - the ambient halo (flatBrightness, e.g. the
        // suit's own AmbientBrightness pool) used to hold its one flat shade right out to the exact
        // edge of ambientRadius and then stop dead: nothing here ever called Falloff for it, so a
        // pool as small as SuitAmbientRadius (0.5 units) or VacuumHaloRadius (1.15 units) read as a
        // disc with a wall around it, not a light. EdgeFalloff below guarantees the same "at least
        // the last quarter-unit fades to black" for this flat pool that Falloff already gives the
        // cone/lamp naturally (its own curve always reaches exactly 0 at the true radius).
        var shade = flatBrightness is { } flat
            ? flat * EdgeFalloff(distance, radius)
            : Falloff(distance / radius) * EdgeFade(offset);
        return new RaySample(point, Shade(shade));
    }

    // Guarantees a smooth fade over (at least) the last quarter world-unit before `radius`, instead
    // of a hard cutoff - "ещё четверть блока" (direct user request). Squared like Falloff's own
    // curve, for the same "holds near its own value, only really gives way right at the end" feel.
    private const float MinFadeWidth = 0.25f;

    private static float EdgeFalloff(float distance, float radius)
    {
        var width = MathF.Min(radius, MinFadeWidth);
        if (width <= 0f) return 1f;
        var start = radius - width;
        if (distance <= start) return 1f;
        var fade = 1f - MathHelper.Clamp((distance - start) / width, 0f, 1f);
        return fade * fade;
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

        if (_indexCount >= 3)
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
                _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, _vertexCount, _indices, 0, _indexCount / 3);
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

    private void GrowIndices(int needed)
    {
        if (_indices.Length < needed)
            Array.Resize(ref _indices, needed * 2);
    }

    private void GrowSamples(int needed)
    {
        if (_raySamples.Length < needed)
            Array.Resize(ref _raySamples, needed * 2);
    }

    public void Dispose()
    {
        _target?.Dispose();
        _effect.Dispose();
    }
}
