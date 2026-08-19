using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// The world - and the light mask multiplied over it - is drawn into an off-screen target instead of
// straight at the backbuffer, then put on screen through the post chain in Shaders/Post.fx. The
// indirection is the entire point: a full-screen shader can only work on a finished frame it is
// able to sample, and the backbuffer is not something you can sample.
//
// Present runs three passes: highlights are extracted into a quarter-size target, blurred there
// (horizontally, then vertically), and added back over the scene along with the grade, vignette,
// aberration and grain. Quarter size is not a compromise for speed alone - a blur that big at full
// resolution would be enormously more work for a result that is, by construction, blurry.
//
// The ordering is load-bearing, and it is the same trap RoomLighting already documents: switching
// render targets discards whatever the backbuffer held (its RenderTargetUsage defaults to
// DiscardContents). Every target switch in here happens before the backbuffer is touched, and the
// composite draw is the first thing all frame that touches it. The HUD is drawn after Present and
// therefore stays outside the chain - panels and text keep their exact pixels, no bloom, no grain.
//
// If the effect did not load (see Shaders.TryLoad) Begin returns false and the caller keeps drawing
// straight at the backbuffer, so a content build that has not run costs the post chain and nothing
// else.
public sealed class ScenePost : IDisposable
{
    // How much smaller the bloom targets are than the scene, per axis. Two levels: a tight one
    // at a quarter and a wide one at a sixteenth. One level can be either a crisp halo hugging
    // the lamp or a broad wash, never both, and a lamp in a dark corridor wants both at once -
    // the tight core and the haze it throws across the room.
    private const int BloomDivisor = 4;
    private const int WideDivisor = 16;

    private readonly GraphicsDevice _device;
    private readonly Effect? _effect;
    private readonly EffectTechnique? _composite;
    private readonly EffectTechnique? _brightPass;
    private readonly EffectTechnique? _blur;
    private RenderTarget2D? _scene;
    private RenderTarget2D? _bloomA;
    private RenderTarget2D? _bloomB;
    private RenderTarget2D? _wideA;
    private RenderTarget2D? _wideB;
    private RenderTarget2D? _distortion;
    private RenderTarget2D? _normals;
    private Texture2D? _lightMask;
    private Texture2D? _noLight;
    private Texture2D? _noNormals;
    private Texture2D? _blob;
    private bool _capturing;
    private bool _distortionDrawn;
    private bool _normalsDrawn;

    public ScenePost(GraphicsDevice device, Effect? effect)
    {
        _device = device;
        _effect = effect;
        _composite = effect?.Techniques["Composite"];
        _brightPass = effect?.Techniques["BrightPass"];
        _blur = effect?.Techniques["Blur"];
    }

    public bool Available => _effect is not null;

    // Linear scale on the scene before anything is added to it. Doubles as a flash or fade knob.
    public float Exposure { get; set; } = 1.25f;

    // Luminance a pixel has to beat before it glows, and how much of the blur is added back.
    // Strength at 0 switches bloom off without costing the passes - see NoPost for that.
    //
    // The threshold is low for a reason: room lighting is a *multiply* over the scene, so a lit
    // pixel is at most as bright as its own albedo and nothing in the frame ever exceeds 1. A
    // textbook 0.6-0.8 threshold therefore catches almost nothing here. Real lamp glow needs the
    // light pass to be able to go above 1, which arrives with per-pixel lighting, not here.
    public float BloomThreshold { get; set; } = 0.55f;
    public float BloomStrength { get; set; } = 1.0f;

    // How much of the wide, sixteenth-size level is folded into the tight one.
    public float WideBloomStrength { get; set; } = 0.55f;

    // How far the grade pushes shadows blue and highlights warm. 0 leaves the renderer colours be.
    // Most of this frame is shadow, so the shadow half of the grade is the single most visible
    // thing in the whole chain - kept below 1 so it reads as cold light rather than a blue filter.
    public float GradeStrength { get; set; } = 0.7f;

    // How far the tone curve reaches before it starts compressing. The light mask is high dynamic
    // range now, so lit pixels genuinely exceed 1 and something has to bring them back into a
    // displayable range without simply clipping every lamp into a flat white disc. 0 disables the
    // curve entirely, which is what makes the identity check below possible.
    // Raised from the first pass at this: a low white point compresses hard and the frame came out
    // dimmer than the low dynamic range version it replaced, which is the wrong trade. Higher means
    // the curve only catches what genuinely overshoots and leaves the midtones where they are.
    public float TonemapWhite { get; set; } = 3.8f;

    // Screen-space relief driven by the light mask: how much surfaces facing a lamp brighten, and
    // how hard the glint on the steepest ones is. Both fire only where the picture has texture.
    public float ReliefStrength { get; set; } = 0.35f;
    public float SpecularStrength { get; set; } = 0.12f;

    // How hard the picture ripples where the distortion mask is white. Only ever visible where the
    // game actually drew something into that mask.
    public float DistortionStrength { get; set; } = 0.9f;

    // Dither in least significant bits of the 8-bit output. One is enough to break banding in the
    // dark ramps this game is made of, and is itself invisible.
    public float DitherAmount { get; set; } = 1f;

    public float Vignette { get; set; } = 0.12f;
    public float GrainAmount { get; set; } = 0.020f;
    public float Aberration { get; set; } = 0.35f;

    // Everything off: the composite becomes a pixel-exact identity. Used by the shader check, and
    // the honest way to answer "is the post chain doing this, or was it always like that?".
    public void NoPost()
    {
        Exposure = 1f;
        BloomStrength = 0f;
        WideBloomStrength = 0f;
        GradeStrength = 0f;
        TonemapWhite = 0f;
        ReliefStrength = 0f;
        SpecularStrength = 0f;
        DistortionStrength = 0f;
        Vignette = 0f;
        GrainAmount = 0f;
        DitherAmount = 0f;
        Aberration = 0f;
    }

    // A soft round blob to stamp into the distortion mask, one per venting particle. Generated
    // rather than loaded: it is a radial gradient, and the renderer owns no bitmap art.
    public Texture2D Blob => _blob ??= CreateBlob();

    // Binds the distortion mask so the caller can stamp Blob wherever the picture should ripple.
    // Draw in the same batch transform as the scene - the mask is the same size as the frame, so
    // world coordinates land exactly where they landed in the scene itself. Must be called after
    // the scene is finished and before Present, and the caller has to End its batch before
    // Present, same as any other target. False means there is no post chain to ripple.
    public bool BeginDistortion()
    {
        if (_effect is null || _distortion is null || !_capturing)
            return false;

        _device.SetRenderTarget(_distortion);
        _device.Clear(Color.Black);
        return true;
    }

    // Marks the mask as filled for this frame. Deliberately does not unbind: Present switches to
    // the backbuffer immediately afterwards anyway, and an extra switch in between would be one
    // more chance to discard something.
    public void EndDistortion() => _distortionDrawn = true;

    // Binds the normals target so the caller can draw true surface normals for whatever geometry
    // actually has a normal map. Everything not drawn into it keeps alpha 0, which the composite
    // reads as "no data here" and falls back to estimating the slope from scene luminance - so
    // covering part of the screen is useful on its own, and there is no all-or-nothing G-buffer to
    // maintain across every renderer.
    public bool BeginNormals()
    {
        if (_effect is null || _normals is null || !_capturing)
            return false;

        _device.SetRenderTarget(_normals);
        _device.Clear(Color.Transparent);
        return true;
    }

    public void EndNormals() => _normalsDrawn = true;

    // The light mask the chain reads to decide what glows and which way the light is coming from -
    // RoomLighting.Mask normally, the plain sight mask when room lighting did not build, null when
    // neither did. Null falls back to a black texture, which zeroes every term that reads it.
    public void SetLightMask(Texture2D? mask) => _lightMask = mask;

    // Starts capturing the scene. False means there is no post chain this frame and the caller
    // should clear and draw the backbuffer itself, exactly as it did before this class existed.
    public bool Begin(Color clear)
    {
        if (_effect is null || !EnsureTargets())
            return false;

        _device.SetRenderTarget(_scene);
        _device.Clear(clear);
        _capturing = true;
        // Last frame is still sitting in the distortion target; nothing may read it until something
        // has drawn into it again this frame.
        _distortionDrawn = false;
        _normalsDrawn = false;
        return true;
    }

    // Ends the capture and puts the frame on screen through the chain. No-op when Begin returned
    // false, so the call site needs no second condition around it. totalSeconds only animates the
    // grain.
    public void Present(SpriteBatch spriteBatch, float totalSeconds)
    {
        if (!_capturing || _effect is null)
            return;

        _capturing = false;

        SetParam("Exposure", Exposure);
        SetParam("BloomThreshold", BloomThreshold);
        SetParam("BloomStrength", BloomStrength);
        SetParam("GradeStrength", GradeStrength);
        SetParam("Vignette", Vignette);
        SetParam("GrainAmount", GrainAmount);
        SetParam("DitherAmount", DitherAmount);
        SetParam("Aberration", Aberration);
        SetParam("Time", totalSeconds);
        SetParam("TonemapWhite", TonemapWhite);
        SetParam("ReliefStrength", ReliefStrength);
        SetParam("SpecularStrength", SpecularStrength);
        SetParam("TexelSize", new Vector2(1f / _scene!.Width, 1f / _scene.Height));

        _noLight ??= CreateNoLight();
        _effect.Parameters["LightTexture"]?.SetValue(_lightMask ?? _noLight);
        // Black stands in whenever nothing was stamped this frame, which is a shimmer of zero.
        SetParam("DistortionStrength", _distortionDrawn ? DistortionStrength : 0f);
        _effect.Parameters["DistortionTexture"]?.SetValue(_distortionDrawn ? _distortion : _noLight);
        _noNormals ??= CreateNoNormals();
        _effect.Parameters["NormalTexture"]?.SetValue(_normalsDrawn ? _normals : _noNormals);

        // Highlights out of the scene and into the small target, then blurred along one axis at a
        // time. LinearClamp throughout: these passes are all about smearing, and the blur taps sit
        // between texels on purpose (see BlurPS).
        Pass(spriteBatch, _brightPass, _scene!, _bloomA!);
        SetParam("BlurDirection", new Vector2(1f / _bloomA!.Width, 0f));
        Pass(spriteBatch, _blur, _bloomA, _bloomB!);
        SetParam("BlurDirection", new Vector2(0f, 1f / _bloomB!.Height));
        Pass(spriteBatch, _blur, _bloomB, _bloomA);

        // The wide level: the tight result shrunk again and blurred there, so the same nine taps
        // reach four times further across the screen for a quarter of the pixels.
        SetParam("BlurDirection", new Vector2(1f / _wideA!.Width, 0f));
        Pass(spriteBatch, _blur, _bloomA, _wideA);
        SetParam("BlurDirection", new Vector2(0f, 1f / _wideB!.Height));
        Pass(spriteBatch, _blur, _wideA, _wideB);
        SetParam("BlurDirection", new Vector2(1f / _wideA.Width, 0f));
        Pass(spriteBatch, _blur, _wideB, _wideA);

        // Folded back into the tight target rather than handed to the composite as a second
        // texture: one more sampler in CompositePS is one more chance to disturb the sampler
        // ordering the whole effect depends on (see the note at the top of Post.fx). _bloomA is
        // created with PreserveContents precisely so binding it again here does not wipe it.
        _device.SetRenderTarget(_bloomA);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp);
        spriteBatch.Draw(_wideA, new Rectangle(0, 0, _bloomA.Width, _bloomA.Height), Color.White * WideBloomStrength);
        spriteBatch.End();

        // Back to the backbuffer for the composite. PointClamp here and not Linear: this one is a
        // 1:1 blit of a target the same size as the backbuffer, so any filtering would only soften
        // pixels that already line up exactly.
        _device.SetRenderTarget(null);
        _effect.Parameters["BloomTexture"]?.SetValue(_bloomA);
        if (_composite is not null)
            _effect.CurrentTechnique = _composite;
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, effect: _effect);
        spriteBatch.Draw(_scene, Vector2.Zero, Color.White);
        spriteBatch.End();
    }

    private void SetParam(string name, float value) => _effect?.Parameters[name]?.SetValue(value);
    private void SetParam(string name, Vector2 value) => _effect?.Parameters[name]?.SetValue(value);

    // White, not black: the chain now *multiplies* the scene by this mask, so the meaning of
    // "no lighting information" is "leave the scene alone", which is 1. Black here would black
    // out the whole frame.
    private Texture2D CreateNoLight()
    {
        var texture = new Texture2D(_device, 1, 1);
        texture.SetData(new[] { Color.White });
        return texture;
    }

    // A flat normal with alpha 0: pointing straight at the viewer, and marked as carrying no
    // information, so the composite ignores it entirely rather than flattening the picture.
    private Texture2D CreateNoNormals()
    {
        var texture = new Texture2D(_device, 1, 1);
        texture.SetData(new[] { new Color(0.5f, 0.5f, 1f, 0f) });
        return texture;
    }

    private Texture2D CreateBlob()
    {
        const int size = 64;
        var texture = new Texture2D(_device, size, size);
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = (x + 0.5f) / size * 2f - 1f;
                var dy = (y + 0.5f) / size * 2f - 1f;
                // Smoothstep falloff rather than a linear one: a linear edge leaves a visible rim
                // where the ripple stops dead.
                var t = MathHelper.Clamp(1f - (float)Math.Sqrt(dx * dx + dy * dy), 0f, 1f);
                var v = t * t * (3f - 2f * t);
                pixels[y * size + x] = new Color(v, v, v, v);
            }
        }
        texture.SetData(pixels);
        return texture;
    }

    private void Pass(SpriteBatch spriteBatch, EffectTechnique? technique, Texture2D source, RenderTarget2D destination)
    {
        if (technique is null)
            return;

        _device.SetRenderTarget(destination);
        _device.Clear(Color.Black);
        _effect!.CurrentTechnique = technique;
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp, effect: _effect);
        spriteBatch.Draw(source, new Rectangle(0, 0, destination.Width, destination.Height), Color.White);
        spriteBatch.End();
    }

    private bool EnsureTargets()
    {
        var viewport = _device.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0)
            return false;
        if (_scene is not null && _scene.Width == viewport.Width && _scene.Height == viewport.Height)
            return true;

        _scene?.Dispose();
        _bloomA?.Dispose();
        _bloomB?.Dispose();
        _wideA?.Dispose();
        _wideB?.Dispose();
        _distortion?.Dispose();
        _normals?.Dispose();
        _scene = new RenderTarget2D(_device, viewport.Width, viewport.Height, false, SurfaceFormat.Color, DepthFormat.None);
        // Same size as the frame so the caller can stamp into it with the scene transform unchanged.
        _distortion = new RenderTarget2D(_device, viewport.Width, viewport.Height, false, SurfaceFormat.Color, DepthFormat.None);
        _normals = new RenderTarget2D(_device, viewport.Width, viewport.Height, false, SurfaceFormat.Color, DepthFormat.None);
        var bw = Math.Max(1, viewport.Width / BloomDivisor);
        var bh = Math.Max(1, viewport.Height / BloomDivisor);
        // PreserveContents on the one target that gets bound twice in a frame - the wide level is
        // added into it after it already holds the tight one, and the default DiscardContents
        // would throw the tight one away at that moment.
        _bloomA = new RenderTarget2D(_device, bw, bh, false, SurfaceFormat.Color, DepthFormat.None,
            0, RenderTargetUsage.PreserveContents);
        _bloomB = new RenderTarget2D(_device, bw, bh, false, SurfaceFormat.Color, DepthFormat.None);
        var ww = Math.Max(1, viewport.Width / WideDivisor);
        var wh = Math.Max(1, viewport.Height / WideDivisor);
        _wideA = new RenderTarget2D(_device, ww, wh, false, SurfaceFormat.Color, DepthFormat.None);
        _wideB = new RenderTarget2D(_device, ww, wh, false, SurfaceFormat.Color, DepthFormat.None);
        return true;
    }

    public void Dispose()
    {
        _scene?.Dispose();
        _bloomA?.Dispose();
        _bloomB?.Dispose();
        _wideA?.Dispose();
        _wideB?.Dispose();
        _distortion?.Dispose();
        _normals?.Dispose();
        _noLight?.Dispose();
        _noNormals?.Dispose();
        _blob?.Dispose();
        // _effect is owned by the ContentManager that loaded it, and _lightMask by whoever built
        // it - disposing either here would be a second Dispose on somebody else's object.
    }
}
