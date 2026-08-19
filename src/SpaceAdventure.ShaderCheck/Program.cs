using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Client.Rendering;

namespace SpaceAdventure.ShaderCheck;

// Shaders fail quietly by design: Shaders.TryLoad hands back null so a content build that has not
// run costs the effect rather than the whole game. That is right for a player and wrong for us - a
// broken .fx would otherwise sit unnoticed behind the fallback path forever. This is the loud half:
// it loads every compiled effect on a real GPU, drives one through ScenePost, and exits non-zero
// if anything is off.
//
// Run it after touching a shader or the render path:
//     dotnet run --project src/SpaceAdventure.ShaderCheck
internal sealed class Checks : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private int _failures;
    private int _checks;

    public Checks()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 320,
            PreferredBackBufferHeight = 180,
        };
        Content.RootDirectory = ResolveContentRoot();
    }

    // The .xnb files are built by the client project. They normally land next to this exe too
    // (content items travel through a ProjectReference), but fall back to reading them out of the
    // client output directly so a fresh clone never fails here for a confusing reason.
    private static string ResolveContentRoot()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "Content");
        if (Directory.Exists(Path.Combine(local, "Shaders")))
            return local;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "SpaceAdventure.Client", "bin", "Debug", "net9.0-windows", "Content");
            if (Directory.Exists(Path.Combine(candidate, "Shaders")))
                return candidate;
            dir = dir.Parent;
        }
        return local;
    }

    private void Check(string name, Func<string?> body)
    {
        _checks++;
        string? problem;
        try
        {
            problem = body();
        }
        catch (Exception ex)
        {
            problem = ex.GetType().Name + ": " + ex.Message;
        }

        if (problem is null)
        {
            Console.WriteLine("OK   " + name);
            return;
        }

        Console.WriteLine("FAIL " + name);
        Console.WriteLine("     " + problem);
        _failures++;
    }

    protected override void LoadContent()
    {
        Console.WriteLine("content root: " + Content.RootDirectory);

        // Every effect the content build produced, rather than a hardcoded list - a shader added in
        // a later phase is covered the day it lands, with nobody having to remember this file.
        var shaderDir = Path.Combine(Content.RootDirectory, "Shaders");
        var assets = Directory.Exists(shaderDir)
            ? Directory.GetFiles(shaderDir, "*.xnb")
                .Select(f => "Shaders/" + Path.GetFileNameWithoutExtension(f))
                .OrderBy(a => a, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();

        Check("content build produced at least one effect",
            () => assets.Length > 0 ? null : "no .xnb under " + shaderDir + " - has the client been built?");

        foreach (var asset in assets)
        {
            Check(asset + " loads and has a technique", () =>
            {
                var effect = Shaders.TryLoad(Content, asset);
                if (effect is null)
                    return "TryLoad returned null: " + (Shaders.LastError ?? "no reason recorded");
                if (effect.CurrentTechnique.Passes.Count == 0)
                    return "technique has no passes";
                Console.WriteLine("     technique=" + effect.CurrentTechnique.Name
                    + ", passes=" + effect.CurrentTechnique.Passes.Count
                    + ", params=[" + string.Join(", ", effect.Parameters.Select(p => p.Name)) + "]");
                return null;
            });
        }

        Check("a missing asset degrades to null instead of throwing", () =>
        {
            var missing = Shaders.TryLoad(Content, "Shaders/ThisDoesNotExist");
            if (missing is not null)
                return "expected null for an asset that is not there";
            return Shaders.LastError is null ? "LastError was not recorded" : null;
        });

        var post = new ScenePost(GraphicsDevice, Shaders.TryLoad(Content, "Shaders/Post"));
        var batch = new SpriteBatch(GraphicsDevice);

        Check("ScenePost reports itself available", () => post.Available ? null : "the effect did not load");

        // Doubled further down, so no channel may exceed 127 or the comparison would be measuring
        // the clamp instead of the shader.
        var probe = new Color(30, 60, 90);

        Color? RunFrame(float exposure)
        {
            // Everything but Exposure switched off, so these checks measure the one thing they name
            // rather than the sum of the whole chain.
            post.NoPost();
            post.Exposure = exposure;
            if (!post.Begin(probe))
                return null;
            post.Present(batch, 0f);
            var vp = GraphicsDevice.Viewport;
            var data = new Color[vp.Width * vp.Height];
            GraphicsDevice.GetBackBufferData(data);
            // The bottom-right corner on purpose: a target left over at a smaller size would leave
            // this pixel untouched, which is exactly what the resize check is looking for.
            return data[(vp.Height - 1) * vp.Width + (vp.Width - 1)];
        }

        Check("Exposure = 1 is a pixel-exact identity", () =>
        {
            var got = RunFrame(1f);
            if (got is null)
                return "Begin returned false";
            return got.Value == probe ? null : "expected " + probe + ", got " + got;
        });

        Check("Exposure = 2 reaches the pixel shader", () =>
        {
            var got = RunFrame(2f);
            if (got is null)
                return "Begin returned false";
            var want = new Color(60, 120, 180);
            var off = Math.Abs(got.Value.R - want.R) + Math.Abs(got.Value.G - want.G) + Math.Abs(got.Value.B - want.B);
            return off <= 3 ? null : "expected about " + want + ", got " + got;
        });

        // The one piece of state in ScenePost that can be got wrong: the target has to follow the
        // viewport, or every frame after a resolution change blits a stale, wrong-sized image.
        Check("the target follows a viewport resize", () =>
        {
            _graphics.PreferredBackBufferWidth = 512;
            _graphics.PreferredBackBufferHeight = 288;
            _graphics.ApplyChanges();
            var got = RunFrame(1f);
            if (got is null)
                return "Begin returned false after the resize";
            if (GraphicsDevice.Viewport.Width != 512)
                return "the backbuffer did not actually resize";
            return got.Value == probe
                ? null
                : "the corner of the resized frame is " + got + ", expected " + probe + " - stale target";
        });

        // The multi-pass half of the chain - extract highlights, blur them small, add them back.
        // Nothing else in here exercises the two bloom targets or the separable blur at all.
        Check("bloom spreads light beyond its source", () =>
        {
            var vp = GraphicsDevice.Viewport;
            using var white = new Texture2D(GraphicsDevice, 1, 1);
            white.SetData(new[] { Color.White });
            var box = new Rectangle(vp.Width / 2 - 8, vp.Height / 2 - 8, 16, 16);
            // 12 pixels clear of the square: outside it, but inside the reach of a nine-tap blur
            // running on a quarter-size target.
            var probeX = vp.Width / 2;
            var probeY = box.Bottom + 12;

            Color Sample(float strength)
            {
                post.NoPost();
                post.BloomStrength = strength;
                post.Begin(Color.Black);
                batch.Begin();
                batch.Draw(white, box, Color.White);
                batch.End();
                post.Present(batch, 0f);
                var data = new Color[vp.Width * vp.Height];
                GraphicsDevice.GetBackBufferData(data);
                return data[probeY * vp.Width + probeX];
            }

            var off = Sample(0f);
            var on = Sample(1.4f);
            if (off.R > 4)
                return "with bloom off that point should be black, got " + off;
            if (on.R <= off.R + 6)
                return "with bloom on it should be lit by the square, got " + on;
            return null;
        });

        // The light mask is no longer a hint about what may glow - it is the lighting itself, applied
        // by the composite as a multiply. That makes it the one texture that can black out the whole
        // frame if it is bound wrong, so both ends of it are worth pinning down.
        Check("the light mask multiplies the scene", () =>
        {
            var vp = GraphicsDevice.Viewport;
            using var dark = new Texture2D(GraphicsDevice, 1, 1);
            dark.SetData(new[] { Color.Black });
            using var full = new Texture2D(GraphicsDevice, 1, 1);
            full.SetData(new[] { Color.White });

            Color Sample(Texture2D? mask)
            {
                post.NoPost();
                post.SetLightMask(mask);
                post.Begin(probe);
                post.Present(batch, 0f);
                var data = new Color[vp.Width * vp.Height];
                GraphicsDevice.GetBackBufferData(data);
                return data[(vp.Height / 2) * vp.Width + vp.Width / 2];
            }

            var unlit = Sample(dark);
            var lit = Sample(full);
            var absent = Sample(null);
            post.SetLightMask(null);
            if (unlit.R > 2 || unlit.G > 2 || unlit.B > 2)
                return "an unlit pixel has to go black, got " + unlit;
            if (lit != probe)
                return "a fully lit pixel has to come through untouched, got " + lit;
            // No mask at all must mean "leave it alone", not "multiply by zero" - the difference
            // between a fallback and a black screen.
            if (absent != probe)
                return "with no mask bound the scene must pass through, got " + absent;
            return null;
        });

        // True normals. Nothing else here touches the normals target, and the failure it guards is a
        // quiet one: if the alpha flag were lost the whole frame would silently fall back to guessing
        // slope from luminance and simply look a bit flatter, with nothing to point at.
        Check("a drawn normal map changes the shading, and its absence does not", () =>
        {
            var vp = GraphicsDevice.Viewport;
            using var grey = new Texture2D(GraphicsDevice, 1, 1);
            grey.SetData(new[] { new Color(140, 140, 140) });
            // Tilted hard along +x, alpha 1 so the composite treats it as real data.
            using var tilted = new Texture2D(GraphicsDevice, 1, 1);
            tilted.SetData(new[] { new Color(1f, 0.5f, 0.5f, 1f) });
            using var lightRamp = new Texture2D(GraphicsDevice, 2, 1);
            lightRamp.SetData(new[] { new Color(40, 40, 40), Color.White });

            Color Sample(bool withNormals)
            {
                post.NoPost();
                post.ReliefStrength = 1.5f;
                post.SetLightMask(lightRamp);
                post.Begin(Color.Black);
                batch.Begin();
                batch.Draw(grey, new Rectangle(0, 0, vp.Width, vp.Height), Color.White);
                batch.End();
                if (withNormals && post.BeginNormals())
                {
                    batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
                    batch.Draw(tilted, new Rectangle(0, 0, vp.Width, vp.Height), Color.White);
                    batch.End();
                    post.EndNormals();
                }
                post.Present(batch, 0f);
                var data = new Color[vp.Width * vp.Height];
                GraphicsDevice.GetBackBufferData(data);
                return data[(vp.Height / 2) * vp.Width + vp.Width / 2];
            }

            var mapped = Sample(true);
            var guessed = Sample(false);
            post.SetLightMask(null);
            if (mapped == guessed)
                return "the normal map made no difference - is the alpha flag surviving the draw?";
            // A flat fill has no luminance gradient at all, so the fallback path must leave it alone.
            if (guessed.R != 140)
                return "with no normals a flat fill must come through unshaded, got " + guessed;
            return null;
        });

        // Distortion. Striped, because a shift can only be seen against an edge - a flat fill would
        // ripple just as hard and look identical.
        Check("distortion ripples the picture where the mask is stamped", () =>
        {
            var vp = GraphicsDevice.Viewport;
            using var white = new Texture2D(GraphicsDevice, 1, 1);
            white.SetData(new[] { Color.White });

            Color[] Frame(float strength)
            {
                post.NoPost();
                post.DistortionStrength = strength;
                post.Begin(Color.Black);
                batch.Begin();
                for (var x = 0; x < vp.Width; x += 8)
                    batch.Draw(white, new Rectangle(x, 0, 4, vp.Height), Color.White);
                batch.End();
                if (post.BeginDistortion())
                {
                    batch.Begin(SpriteSortMode.Deferred, BlendState.Additive);
                    batch.Draw(post.Blob, new Rectangle(0, 0, vp.Width, vp.Height), Color.White);
                    batch.End();
                    post.EndDistortion();
                }
                post.Present(batch, 0f);
                var data = new Color[vp.Width * vp.Height];
                GraphicsDevice.GetBackBufferData(data);
                return data;
            }

            var still = Frame(0f);
            var rippled = Frame(3f);
            var moved = 0;
            for (var i = 0; i < still.Length; i++)
            {
                if (still[i] != rippled[i])
                    moved++;
            }
            return moved > 200 ? null : "expected the stripes to shift, only " + moved + " pixels changed";
        });

        // The property every tiled surface in the game rests on. Asserted on the noise itself and
        // not on finished pixels: in the floor plate the noise is a few percent of a brightness the
        // tread ridge dominates, and an earlier pixel-diff version of this check passed happily with
        // wrapping switched off. A guard that cannot fail is not a guard.
        Check("the noise lattice wraps, so tiled surfaces have no seam",
            () => TileTextures.NoiseWrapsCleanly() ? null : "Noise does not close on itself - every tiled surface will show a seam");

        post.Dispose();
        batch.Dispose();

        Console.WriteLine();
        Console.WriteLine(_failures == 0
            ? _checks + "/" + _checks + " passed"
            : (_checks - _failures) + "/" + _checks + " passed, " + _failures + " FAILED");
        Environment.ExitCode = _failures == 0 ? 0 : 1;
        Exit();
    }
}

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        using var checks = new Checks();
        checks.Run();
        return Environment.ExitCode;
    }
}
