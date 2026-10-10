using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// Hostile hulls, baked offscreen - the same armour HullSkin draws for the player's own ship, run once against the enemy
// ship's own real Rooms (every enemy is the same "крутой корабль" hull, EnemyShipLayout.cs, but a ship that has lost
// compartments is a different shape and needs its own bake). Baked by room-set signature and cached, so two fresh enemies
// share one texture and a damaged one gets its own.
//
// Baking switches the graphics device's render target, which is only safe between frames - so the owner calls Prepare
// (Game1.Update) for whatever is about to be drawn, and drawing only ever calls Get.
public sealed class EnemyHullSkin : IDisposable
{
    // How much clear canvas to leave around the hull's own room footprint on every side - covers
    // the nose dome (HullSkin.NoseLengthUnits=2.3) whichever way it happens to point, plus the
    // radiator fins/greebles that hang a little further out still.
    private const float MarginUnits = 4f;

    private readonly GraphicsDevice _graphics;
    private readonly Texture2D _pixel;
    private readonly Texture2D[] _hullPlates;
    private readonly Dictionary<string, (Texture2D Texture, Vector2 Origin)> _cache = new();

    public EnemyHullSkin(GraphicsDevice graphics)
    {
        _graphics = graphics;
        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _hullPlates = TileTextures.CreateHullPlates(graphics);
        Prepare(EnemyShipLayout.Default.Rooms); // the undamaged hull, ready before the first fight
    }

    public void Dispose()
    {
        foreach (var (texture, _) in _cache.Values)
            texture.Dispose();
        _cache.Clear();
        foreach (var plate in _hullPlates)
            plate.Dispose();
        _pixel.Dispose();
    }

    // A compact, stable key for a set of rooms - ids plus their rectangles.
    private static string Signature(IReadOnlyList<Room> rooms) =>
        string.Join("|", rooms.OrderBy(r => r.Id, StringComparer.Ordinal)
            .Select(r => r.Id + ":" + string.Join(",", r.Rects.Select(q => $"{q.X},{q.Y},{q.Width},{q.Height}"))));

    // Bakes the hull for these rooms if it is not cached yet; drops baked hulls nothing in `inUse` still needs.
    public void Prepare(IReadOnlyList<Room> rooms)
    {
        var key = Signature(rooms);
        if (!_cache.ContainsKey(key))
            _cache[key] = Bake(rooms);
    }

    // Forget every baked hull except the ones in `keep` (called with the shapes currently in the field).
    public void Trim(IEnumerable<IReadOnlyList<Room>> keep)
    {
        var keepKeys = keep.Select(Signature).ToHashSet();
        keepKeys.Add(Signature(EnemyShipLayout.Default.Rooms));
        foreach (var key in _cache.Keys.Where(k => !keepKeys.Contains(k)).ToList())
        {
            _cache[key].Texture.Dispose();
            _cache.Remove(key);
        }
    }

    public (Texture2D Texture, Vector2 Origin)? Get(IReadOnlyList<Room> rooms) =>
        _cache.TryGetValue(Signature(rooms), out var baked) ? baked : null;

    // The direction the hull flies nose-first, in its own local frame - the saved hull's own forward.
    public static float ForwardDegrees => EnemyShipLayout.Default.Ship.ForwardDegrees;

    private (Texture2D Texture, Vector2 Origin) Bake(IReadOnlyList<Room> rooms)
    {
        var minX = rooms.Min(r => r.Left);
        var maxX = rooms.Max(r => r.Right);
        var minY = rooms.Min(r => r.Top);
        var maxY = rooms.Max(r => r.Bottom);
        var center = new Vec2((minX + maxX) / 2, (minY + maxY) / 2);
        var halfExtents = new Vec2((maxX - minX) / 2, (maxY - minY) / 2);

        var widthPx = (int)MathF.Ceiling((float)((halfExtents.X * 2f + MarginUnits * 2f) * ShipRenderer.PixelsPerUnit));
        var heightPx = (int)MathF.Ceiling((float)((halfExtents.Y * 2f + MarginUnits * 2f) * ShipRenderer.PixelsPerUnit));

        // The translation HullSkin.Draw itself needs (where local (0,0) lands on this canvas) is
        // NOT the same point as the sprite's own pivot below - Rooms are authored starting near
        // (0,0), not centred on it, so (0,0) is usually well off to one side of the hull's true
        // centre.
        var drawOrigin = new Vector2((float)(widthPx / 2f - center.X * ShipRenderer.PixelsPerUnit),
            (float)(heightPx / 2f - center.Y * ShipRenderer.PixelsPerUnit));

        // The hull's own local centre - the same point EnemyShipRuntime.Position/RotationDegrees
        // rotate everything else around (World.Eva.cs's EnemyHullLocalCenter) - always lands
        // exactly on this canvas's own centre, by construction (the margin is symmetric on every
        // side). This, not drawOrigin above, is the pivot spriteBatch.Draw needs: get it wrong and
        // the drawn hull sits rotated/offset from where TryAutoAttach's own hull-silhouette check
        // (which uses the real centre) actually reacts to contact - the ship looks like it's
        // somewhere the boots don't actually grab.
        var pivot = new Vector2(widthPx / 2f, heightPx / 2f);

        using var target = new RenderTarget2D(_graphics, widthPx, heightPx, false, SurfaceFormat.Color, DepthFormat.None);
        _graphics.SetRenderTarget(target);
        _graphics.Clear(Color.Transparent);

        using (var spriteBatch = new SpriteBatch(_graphics))
        {
            spriteBatch.Begin(blendState: BlendState.AlphaBlend);
            // No system devices: an enemy hull has no wired power grid, just rooms and a shell - the
            // engine-nozzle fitting (which only draws for a device it's actually given) simply
            // contributes nothing, and the damage-scorch overlay (which needs systemStates) never
            // lights, the same "nothing to show" outcome null/empty already gives the player's ship.
            HullSkin.Draw(spriteBatch, _pixel, _hullPlates, rooms, Array.Empty<Door>(),
                Array.Empty<ShipSystemDevice>(), drawOrigin, ForwardDegrees);
            spriteBatch.End();
        }

        _graphics.SetRenderTarget(null);

        // Copied out to a plain Texture2D rather than keeping the RenderTarget2D itself: a render
        // target is a scratch surface meant to be disposed once its pixels are read out, not a
        // long-lived cached asset.
        var texture = new Texture2D(_graphics, widthPx, heightPx);
        var pixels = new Color[widthPx * heightPx];
        target.GetData(pixels);
        texture.SetData(pixels);

        return (texture, pivot);
    }
}
