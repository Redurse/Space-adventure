using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Hostile hulls, baked once per class - the same armour HullSkin draws for the player's own ship,
// run once offscreen against each EnemyShipLayout's own real Rooms/AirlockOuterDoors instead of a
// hand-drawn silhouette unrelated to what boarding actually finds inside. A raider really is a
// squat 15x6 box; a boarded Frigate really is the same footprint as the player's own Corvette
// (EnemyShipLayout.Classes.cs's own comment says so) - this is what makes that true on screen as
// well as underfoot, and at the hull's real size rather than a uniform stand-in diameter.
public sealed class EnemyHullSkin : IDisposable
{
    // How much clear canvas to leave around the hull's own room footprint on every side - covers
    // the nose dome (HullSkin.NoseLengthUnits=2.3) whichever way it happens to point, plus the
    // radiator fins/greebles that hang a little further out still. Generous rather than tight:
    // spare canvas is free, a clipped hull baked once at load time is not something to redo.
    private const float MarginUnits = 4f;

    private readonly GraphicsDevice _graphics;
    private readonly Texture2D _pixel;
    private readonly Texture2D[] _hullPlates;
    private readonly Dictionary<EnemyShipClass, (Texture2D Texture, Vector2 Origin)> _cache = new();

    public EnemyHullSkin(GraphicsDevice graphics)
    {
        _graphics = graphics;
        _pixel = new Texture2D(graphics, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _hullPlates = TileTextures.CreateHullPlates(graphics);

        // Baked eagerly, not lazily on first Get() - baking switches the graphics device's own
        // render target, which is only safe between frames (here, during LoadContent) and not
        // mid-frame inside whatever outer SpriteBatch.Begin/End the main Draw() call is already in
        // when a class is first seen in the field.
        foreach (var kind in Enum.GetValues<EnemyShipClass>())
            _cache[kind] = Bake(kind);
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

    public (Texture2D Texture, Vector2 Origin) Get(EnemyShipClass kind) => _cache[kind];

    // Which way each hull's own room layout flies nose-first. Frigate reuses the Corvette's own
    // convention because it is, room for room, the same footprint (EnemyShipLayout.Classes.cs's
    // CreateFrigate comment); the other three are all plain rows of compartments, the same "nose to
    // the right" layout every other player ShipKind already defaults to (ShipCatalog.ForwardDegrees).
    // Public: FieldRenderer needs the same bow direction to place the engine glow and scorch marks
    // consistently with whatever this baked the hull as.
    public static float ForwardDegreesFor(EnemyShipClass kind) => kind switch
    {
        EnemyShipClass.Frigate => ShipCatalog.ForwardDegrees(ShipKind.Corvette),
        _ => 0f,
    };

    private (Texture2D Texture, Vector2 Origin) Bake(EnemyShipClass kind)
    {
        var layout = EnemyShipLayout.Of(kind);
        var (center, halfExtents) = layout.GetLocalBounds();

        var widthPx = (int)MathF.Ceiling((halfExtents.X * 2f + MarginUnits * 2f) * ShipRenderer.PixelsPerUnit);
        var heightPx = (int)MathF.Ceiling((halfExtents.Y * 2f + MarginUnits * 2f) * ShipRenderer.PixelsPerUnit);

        // The translation HullSkin.Draw itself needs (where local (0,0) lands on this canvas) is
        // NOT the same point as the sprite's own pivot below - Rooms are authored starting near
        // (0,0), not centred on it, so (0,0) is usually well off to one side of the hull's true
        // centre.
        var drawOrigin = new Vector2(widthPx / 2f - center.X * ShipRenderer.PixelsPerUnit,
            heightPx / 2f - center.Y * ShipRenderer.PixelsPerUnit);

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
            HullSkin.Draw(spriteBatch, _pixel, _hullPlates, layout.Rooms, layout.AirlockOuterDoors,
                Array.Empty<ShipSystemDevice>(), drawOrigin, ForwardDegreesFor(kind));
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
