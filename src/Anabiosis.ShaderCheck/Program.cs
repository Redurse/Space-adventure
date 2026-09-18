using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Client.Rendering;

namespace Anabiosis.ShaderCheck;

// Shaders fail quietly by design: Shaders.TryLoad hands back null so a content build that has not
// run costs the effect rather than the whole game. That is right for a player and wrong for us - a
// broken .fx would otherwise sit unnoticed behind the fallback path forever. This is the loud half:
// it loads every compiled effect on a real GPU, drives one through ScenePost, and exits non-zero
// if anything is off.
//
// Run it after touching a shader or the render path:
//     dotnet run --project src/Anabiosis.ShaderCheck
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
            var candidate = Path.Combine(dir.FullName, "Anabiosis.Client", "bin", "Debug", "net9.0-windows", "Content");
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

            Color Sample(bool withNormals, float relief = 1.5f)
            {
                post.NoPost();
                post.ReliefStrength = relief;
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
            // Baseline with the relief term switched off entirely. Compared against rather than a
            // fixed number, because the light mask multiplies the scene now (it is the lighting, not
            // a hint) - so the "unshaded" value is albedo times light, not the albedo alone. An
            // earlier version of this check hardcoded the albedo and started failing the day HDR
            // lighting landed, which is exactly the kind of test that teaches people to ignore red.
            var baseline = Sample(false, relief: 0f);
            post.SetLightMask(null);
            if (mapped == guessed)
                return "the normal map made no difference - is the alpha flag surviving the draw?";
            // A flat fill has no luminance gradient, so with no normal map the relief term has
            // nothing to act on and must leave the pixel exactly where the baseline put it.
            if (guessed != baseline)
                return "with no normals a flat fill must be untouched by relief: got " + guessed + ", baseline " + baseline;
            return null;
        });

        // Distortion. Striped, because a shift can only be seen against an edge - a flat fill would
        // ripple just as hard and look identical.
        // This used to vary DistortionStrength between the two frames and stamp the mask in both,
        // which cannot fail when the mask itself never reaches the shader: the strength alone changes
        // the result as long as *something* non-zero is in that sampler. It passed for years while
        // the shimmer did nothing in the game. Now the strength is fixed and the mask is bound in
        // both frames - only its contents differ - so what is under test is the mask arriving.
        Check("the distortion mask itself reaches the composite", () =>
        {
            var vp = GraphicsDevice.Viewport;
            using var white = new Texture2D(GraphicsDevice, 1, 1);
            white.SetData(new[] { Color.White });

            Color[] Frame(bool stamp)
            {
                post.NoPost();
                post.DistortionStrength = 3f;
                post.Begin(Color.Black);
                batch.Begin();
                for (var x = 0; x < vp.Width; x += 8)
                    batch.Draw(white, new Rectangle(x, 0, 4, vp.Height), Color.White);
                batch.End();
                if (post.BeginDistortion())
                {
                    if (stamp)
                    {
                        batch.Begin(SpriteSortMode.Deferred, BlendState.Additive);
                        batch.Draw(post.Blob, new Rectangle(0, 0, vp.Width, vp.Height), Color.White);
                        batch.End();
                    }
                    // Marked as drawn either way, so both frames carry the same strength and the same
                    // bound target and differ only in what was painted into it.
                    post.EndDistortion();
                }
                post.Present(batch, 0f);
                var data = new Color[vp.Width * vp.Height];
                GraphicsDevice.GetBackBufferData(data);
                return data;
            }

            var blank = Frame(false);
            var stamped = Frame(true);
            var moved = 0;
            for (var i = 0; i < blank.Length; i++)
            {
                if (blank[i] != stamped[i])
                    moved++;
            }
            return moved > 200 ? null : "the stripes did not shift: only " + moved + " pixels changed";
        });

        // Music belongs here for the same reason the shaders do: it needs a real window and a real
        // media stack, which the headless suite in Anabiosis.Tests cannot have. The failure this
        // catches is the quiet one - the content build dropping the tracks, which GameMusic swallows
        // by design so a missing file never costs the game.
        Check("every music track loads and the player starts one", () =>
        {
            // Routes through AudioEngine/NAudio now (direct user request, real output-device
            // selection) instead of MonoGame's ContentManager/MediaPlayer - a throwaway engine here
            // is fine, this check never needs to actually be heard.
            using var audioEngine = new Anabiosis.Client.Audio.GameAudioEngine();
            var music = new Anabiosis.Client.Audio.GameMusic(audioEngine);
            Console.WriteLine("     tracks loaded: " + music.TrackCount);
            if (music.TrackCount != 5)
                return "expected 5 tracks, loaded " + music.TrackCount;

            music.SetMasterVolume(0f);          // verify without making noise
            music.Update(0.0);                  // arms, schedules the first track
            music.Update(10_000.0);             // well past any gap - must start something
            var started = music.IsPlaying;
            Console.WriteLine("     playing after the first start: " + started);
            music.Stop();
            var stopped = music.IsPlaying;
            Console.WriteLine("     playing after Stop: " + stopped);
            if (!started)
                return "the player did not start a track";
            if (stopped)
                return "Stop left a track playing";
            return null;
        });

        // Direct user request ("сделай чтобы 2 вкладка настроек была почти точь в точь как в
        // Baротравме") - the other half of GameAudioEngine that GameMusic's own check above doesn't
        // exercise: real output-device enumeration, and a one-shot .wav SFX (pitch/pan/volume, swept
        // off the mixer once finished) rather than a looping .mp3 track.
        Check("GameAudioEngine enumerates output devices and plays a one-shot SFX", () =>
        {
            using var audioEngine = new Anabiosis.Client.Audio.GameAudioEngine { SoundVolume = 0f };
            var devices = audioEngine.EnumerateOutputDevices();
            Console.WriteLine("     output devices found: " + devices.Count);
            foreach (var (id, name) in devices)
                Console.WriteLine("       - " + name);

            var sounds = new Anabiosis.Client.Audio.GameSounds(audioEngine);
            sounds.Play(Anabiosis.Client.Audio.GameSounds.DoorOpen, 0.0);
            // A ~1-second .wav one-shot should still be mid-flight immediately after Play, and
            // should have swept itself off the mixer well before this.
            System.Threading.Thread.Sleep(50);
            audioEngine.SweepFinishedOneShots();
            return null;
        });

        // Direct user report ("почему в устройствах ввода я не могу найти микрофон наушников?") -
        // proves the fix on this actual machine rather than just trusting the diagnosis: input
        // devices now come from the same WASAPI enumerator as output (GameAudioEngine.
        // EnumerateInputDevices, VoiceCapture no longer touches MonoGame's own Microphone class at
        // all), and a real WasapiCapture can actually open and record from whatever the default
        // capture device is.
        Check("GameAudioEngine/VoiceCapture enumerate input devices and can actually record", () =>
        {
            using var audioEngine = new Anabiosis.Client.Audio.GameAudioEngine { SoundVolume = 0f };
            var devices = audioEngine.EnumerateInputDevices();
            Console.WriteLine("     input devices found: " + devices.Count);
            foreach (var (id, name) in devices)
                Console.WriteLine("       - " + name);
            if (devices.Count == 0)
                return null; // headless/no-mic machine - nothing further to prove here

            var capture = new Anabiosis.Client.Audio.VoiceCapture();
            capture.BeginTalking(isRadio: false);
            if (!capture.IsRecording)
                return "BeginTalking did not start recording against the default input device";
            System.Threading.Thread.Sleep(150);
            capture.StopTalking();
            return null;
        });

        // The backdrop is painted in code now, so nothing catches a bad index or a divide-by-zero
        // in it until the menu draws - and by then the load has already swallowed the exception.
        // Set BACKDROP_DUMP to a path to also write the baked image out, which is how the art gets
        // looked at without launching the game.
        // Same reasoning as the backdrop below: the wordmark is drawn in code, so a bad index in
        // the glyph table would otherwise surface as a missing title and nothing else. LOGO_DUMP
        // writes the baked image out, which is how the letterforms get looked at without launching.
        // The icon is drawn per size rather than downscaled from one image, so a size that throws
        // would otherwise show up as a missing icon on somebody's taskbar and nowhere else. Set
        // ICON_DUMP to a directory to write one PNG per size out of it; those get packed into
        // Anabiosis.Client/Icon.ico.
        Check("the app icon bakes at every size", () =>
        {
            var dump = Environment.GetEnvironmentVariable("ICON_DUMP");
            if (!string.IsNullOrEmpty(dump))
                Directory.CreateDirectory(dump);

            foreach (var size in AppIconArt.Sizes)
            {
                var icon = AppIconArt.Bake(GraphicsDevice, size);
                if (icon.Width != size || icon.Height != size)
                    return "asked for " + size + ", got " + icon.Width + "x" + icon.Height;
                if (!string.IsNullOrEmpty(dump))
                {
                    using var stream = File.Create(Path.Combine(dump, "icon_" + size + ".png"));
                    icon.SaveAsPng(stream, size, size);
                }
            }
            if (!string.IsNullOrEmpty(dump))
                Console.WriteLine("     wrote " + AppIconArt.Sizes.Length + " sizes to " + dump);
            return null;
        });

        // Direct user request ("текстуры мне скинь") - every DeviceSkin face is baked in code, so
        // the only way to actually look at one outside the running game is to dump it - same
        // ICON_DUMP/LOGO_DUMP/BACKDROP_DUMP convention above/below, just for DeviceSkin. Set
        // DEVICE_DUMP to a directory to write one PNG per face (all of them, not just the new
        // ones - direct user request to review the shared Housing detail pass across every
        // existing face too) out of it.
        Check("every device face bakes, non-square footprints at their own aspect ratio", () =>
        {
            var dump = Environment.GetEnvironmentVariable("DEVICE_DUMP");
            if (!string.IsNullOrEmpty(dump))
                Directory.CreateDirectory(dump);

            using var deviceSkin = new DeviceSkin(GraphicsDevice);
            var faces = Enum.GetValues<DeviceSkin.Face>();
            const int unit = 32;
            // Direct user request ("текстура была в соответствии с этой формой") - dump each face at
            // the same tile aspect ratio its own CustomDeviceFootprint.Size actually uses, so the fix
            // (Housing spans the full rect, hero art stays centered/square) is visible here rather
            // than in a square PNG that hides the whole point. Anything not listed is 1x1 (square).
            var footprintTiles = new Dictionary<DeviceSkin.Face, (int Width, int Height)>
            {
                [DeviceSkin.Face.Helm] = (3, 2),
                [DeviceSkin.Face.Navigation] = (3, 2),
                [DeviceSkin.Face.ConstructionBench] = (2, 3),
                [DeviceSkin.Face.Fabricator] = (3, 3),
                [DeviceSkin.Face.Deconstructor] = (3, 3),
                [DeviceSkin.Face.WeaponWorkbench] = (2, 4),
                [DeviceSkin.Face.Turret] = (3, 3),
                [DeviceSkin.Face.Bed] = (1, 2),
                [DeviceSkin.Face.ShuttleHangar] = (5, 6),
            };
            foreach (var face in faces)
            {
                var (tw, th) = footprintTiles.TryGetValue(face, out var wh) ? wh : (1, 1);
                var width = tw * unit;
                var height = th * unit;
                var baked = deviceSkin.Get(face, width, height, lit: true);
                if (baked.Width != width || baked.Height != height)
                    return "asked for " + width + "x" + height + ", got " + baked.Width + "x" + baked.Height + " (" + face + ")";
                if (!string.IsNullOrEmpty(dump))
                {
                    using var stream = File.Create(Path.Combine(dump, "device_" + face + ".png"));
                    baked.SaveAsPng(stream, width, height);
                }
            }
            if (!string.IsNullOrEmpty(dump))
                Console.WriteLine("     wrote " + faces.Length + " faces to " + dump);
            return null;
        });

        // Direct user request ("хочу чтобы в игре пол был как в редакторе") - DeckPlates.Create bakes
        // 10 individual plate textures per Deck kind, but judging "does the panel grid actually read
        // from a normal camera distance" needs to see several of them TILED together (DeckPlates.
        // DrawTiled is exactly what ShipRenderer.Rooms.cs's DrawRoomFloor calls at runtime), not one
        // plate in isolation - so this renders a small multi-tile room-sized patch per Deck kind into
        // an offscreen target and dumps THAT, the same composite a player would actually see.
        Check("the floor deck plates tile into a readable panel grid", () =>
        {
            var dump = Environment.GetEnvironmentVariable("FLOOR_DUMP");
            if (!string.IsNullOrEmpty(dump))
                Directory.CreateDirectory(dump);

            const int tilesAcross = 6;
            const int previewSize = DeckPlates.TileSize * tilesAcross;
            using var target = new RenderTarget2D(GraphicsDevice, previewSize, previewSize);
            foreach (var deck in Enum.GetValues<DeckPlates.Deck>())
            {
                var plates = DeckPlates.Create(GraphicsDevice, deck);
                if (plates.Length == 0 || plates.Any(p => p.Width != DeckPlates.TileSize || p.Height != DeckPlates.TileSize))
                    return "unexpected plate size for " + deck;

                GraphicsDevice.SetRenderTarget(target);
                GraphicsDevice.Clear(Color.Black);
                batch.Begin();
                DeckPlates.DrawTiled(batch, plates, new Rectangle(0, 0, previewSize, previewSize), Color.White, Point.Zero);
                batch.End();
                GraphicsDevice.SetRenderTarget(null);

                if (!string.IsNullOrEmpty(dump))
                {
                    using var stream = File.Create(Path.Combine(dump, "floor_" + deck + ".png"));
                    target.SaveAsPng(stream, previewSize, previewSize);
                }
            }
            if (!string.IsNullOrEmpty(dump))
                Console.WriteLine("     wrote " + Enum.GetValues<DeckPlates.Deck>().Length + " deck previews to " + dump);
            return null;
        });

        Check("the menu wordmark bakes", () =>
        {
            var logo = MenuLogo.Get(GraphicsDevice);
            if (logo.Width < 200 || logo.Height < 40)
                return "baked at " + logo.Width + "x" + logo.Height;

            var dump = Environment.GetEnvironmentVariable("LOGO_DUMP");
            if (!string.IsNullOrEmpty(dump))
            {
                using var stream = File.Create(dump);
                logo.SaveAsPng(stream, logo.Width, logo.Height);
                Console.WriteLine("     wrote " + dump + " (" + logo.Width + "x" + logo.Height + ")");
            }
            return null;
        });

        Check("the menu backdrop bakes", () =>
        {
            var drawn = Content.Load<Texture2D>("Textures/MenuBackdrop");
            var backdrop = MenuBackdropArt.Bake(GraphicsDevice, drawn);
            if (backdrop.Width != MenuBackdropArt.Width || backdrop.Height != MenuBackdropArt.Height)
                return "baked at " + backdrop.Width + "x" + backdrop.Height;

            var dump = Environment.GetEnvironmentVariable("BACKDROP_DUMP");
            if (!string.IsNullOrEmpty(dump))
            {
                using var stream = File.Create(dump);
                backdrop.SaveAsPng(stream, backdrop.Width, backdrop.Height);
                Console.WriteLine("     wrote " + dump);
            }
            return null;
        });

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
