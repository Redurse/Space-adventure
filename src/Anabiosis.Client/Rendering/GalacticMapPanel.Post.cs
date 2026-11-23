using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// What is done to the whole picture once the map is drawn: it goes into a four-times multisampled texture the size of the
// screen (so the edges of every badge, ship and ring are smooth, not stepped), and a glow is made of its bright parts
// the way Stellaris does - without a shader: the picture is darkened by a threshold (a subtracting blend leaves only what is
// brighter than it), shrunk again and again (each step blurs it by the texture filter) and the shrunk copies are laid back
// over the picture as light. If anything of this is not available the map is drawn plainly, as before.
public sealed partial class GalacticMapPanel
{
    private const float BloomThreshold = 0.5f;

    private static readonly BlendState SubtractBlend = new()
    {
        ColorBlendFunction = BlendFunction.ReverseSubtract,
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
    };

    private RenderTarget2D? _scene;
    private RenderTarget2D? _bloomA; // half size: the bright parts
    private RenderTarget2D? _bloomB; // quarter
    private RenderTarget2D? _bloomC; // eighth
    private RenderTarget2D? _bloomD; // sixteenth
    private int _postWidth;
    private int _postHeight;
    private bool _postFailed;

    // The matrix the caller draws the map with (the design screen into the real one); the picture is made at real size.
    public Matrix OutputTransform { get; set; } = Matrix.Identity;

    private bool EnsureTargets()
    {
        var width = _device.PresentationParameters.BackBufferWidth;
        var height = _device.PresentationParameters.BackBufferHeight;
        if (width < 64 || height < 64)
            return false;
        if (_scene is not null && width == _postWidth && height == _postHeight)
            return true;
        try
        {
            _scene?.Dispose();
            _bloomA?.Dispose();
            _bloomB?.Dispose();
            _bloomC?.Dispose();
            _bloomD?.Dispose();
            _scene = new RenderTarget2D(_device, width, height, false, SurfaceFormat.Color, DepthFormat.None, 4, RenderTargetUsage.DiscardContents);
            _bloomA = new RenderTarget2D(_device, Math.Max(1, width / 2), Math.Max(1, height / 2), false, SurfaceFormat.Color, DepthFormat.None);
            _bloomB = new RenderTarget2D(_device, Math.Max(1, width / 4), Math.Max(1, height / 4), false, SurfaceFormat.Color, DepthFormat.None);
            _bloomC = new RenderTarget2D(_device, Math.Max(1, width / 8), Math.Max(1, height / 8), false, SurfaceFormat.Color, DepthFormat.None);
            _bloomD = new RenderTarget2D(_device, Math.Max(1, width / 16), Math.Max(1, height / 16), false, SurfaceFormat.Color, DepthFormat.None);
            _postWidth = width;
            _postHeight = height;
            return true;
        }
        catch (Exception)
        {
            _postFailed = true;
            return false;
        }
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin, float zoom, Vector2 panOffset,
        float totalSeconds = 0f, Vector2? pointer = null)
    {
        if (_postFailed || !EnsureTargets())
        {
            DrawScene(spriteBatch, snapshot, panelOrigin, zoom, panOffset, totalSeconds, pointer);
            return;
        }

        var previous = _device.GetRenderTargets();
        try
        {
            spriteBatch.End();
            _device.SetRenderTarget(_scene);
            _device.Clear(Color.Black);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null, null, OutputTransform);
            DrawScene(spriteBatch, snapshot, panelOrigin, zoom, panOffset, totalSeconds, pointer);
            spriteBatch.End();

            BuildBloom(spriteBatch);

            _device.SetRenderTargets(previous);
            var full = new Rectangle(0, 0, _postWidth, _postHeight);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
            spriteBatch.Draw(_scene, full, Color.White);
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp);
            spriteBatch.Draw(_bloomB, full, Color.White * 0.5f);
            spriteBatch.Draw(_bloomC, full, Color.White * 0.5f);
            spriteBatch.Draw(_bloomD, full, Color.White * 0.5f);
            spriteBatch.End();
        }
        catch (Exception)
        {
            _postFailed = true;
            _device.SetRenderTargets(previous);
        }
        // The caller goes on drawing its HUD with a batch it began itself.
        spriteBatch.Begin(transformMatrix: OutputTransform);
    }

    private void BuildBloom(SpriteBatch spriteBatch)
    {
        // Each step shrinks by half and adds the picture to itself `gain` times, since a small bright thing blurred over a
        // large area would otherwise fade to nothing.
        void Shrink(Texture2D from, RenderTarget2D to, int gain)
        {
            _device.SetRenderTarget(to);
            var target = new Rectangle(0, 0, to.Width, to.Height);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp);
            spriteBatch.Draw(from, target, Color.White);
            spriteBatch.End();
            if (gain > 1)
            {
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp);
                for (var k = 1; k < gain; k++)
                    spriteBatch.Draw(from, target, Color.White);
                spriteBatch.End();
            }
        }

        Shrink(_scene!, _bloomA!, 1);
        // Keep only what is brighter than the threshold: subtract it from every pixel.
        var level = (byte)(BloomThreshold * 255f);
        spriteBatch.Begin(SpriteSortMode.Deferred, SubtractBlend, SamplerState.LinearClamp);
        spriteBatch.Draw(_pixel, new Rectangle(0, 0, _bloomA!.Width, _bloomA.Height), new Color(level, level, level, (byte)255));
        spriteBatch.End();
        Shrink(_bloomA, _bloomB!, 2);
        Shrink(_bloomB, _bloomC!, 1);
        Shrink(_bloomC, _bloomD!, 1);
    }
}
