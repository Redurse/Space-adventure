using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client;

// The menu has no RoomLighting behind it - there is no ship, no walls and nothing to cast a shadow -
// so the light it is lit by is painted here by hand: a few soft pools stamped into ScenePost's own
// light target. That is enough for the whole post chain to come alive on this screen: the relief
// term finds the panel plate and the button bevels, the specular catches their top edges, and the
// bright pass finally has somewhere bright to bloom from.
public partial class Game1
{
    private void DrawMenuLightMask(float totalSeconds)
    {
        // Ambient is what everything untouched by a pool settles at, and it has to sit near white.
        // The mask MULTIPLIES the scene, and unlike the ship interior the menu draws itself already
        // lit - its own colours are the finished look. A dim ambient here does not read as mood, it
        // just divides the whole screen down (tried 0.2 and the menu went five times darker). The
        // pools below then push past 1 where they land, which is what the bright pass blooms on.
        if (!_scenePost.BeginOwnLight(Color.White))
            return;

        // The pools below (planet glow, cabin rim light, top sliver) are hand-placed for the actual
        // main menu's own planet backdrop - direct user report ("непонятное размытие") traced to
        // these still lighting up a screen that has nothing to do with them, like the Ship Editor's
        // own flat grid. A neutral, evenly-white mask (the BeginOwnLight call above) is still correct
        // everywhere else - see this file's own doc comment on why every non-session screen needs
        // SOME baseline light mask - just without any of the decorative pools added on top of it.
        if (_menuScreen != MenuScreen.Main)
        {
            _scenePost.EndOwnLight();
            return;
        }

        var pane = MenuArtPane;
        var blob = _scenePost.Blob;
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, transformMatrix: _renderScale);

        // Key light: the planet itself, in the same place MenuPlanetScene puts it. Breathes very
        // slowly so the screen is never entirely static even when nothing is happening.
        var planet = new Vector2(DesignWidth * 0.6f, DesignHeight * 0.56f);
        var breath = 1f + MathF.Sin(totalSeconds * 0.35f) * 0.06f;
        Pool(blob, planet, 470f * breath, new Color(96, 132, 205), 0.55f);
        Pool(blob, planet, 190f * breath, new Color(150, 186, 240), 0.70f);

        // The ship's own hull (baked into the backdrop, DrawMenuScene's own doc comment) sat under
        // no pool at all - direct user report ("корабль почти не видно"). Centred on the midpoint
        // between the bake's tail (0.10, 0.62) and nose (0.52, 0.20), same pane-fraction coordinates
        // DrawMenuEnginePlume's own NozzleMouths use, wide enough to soften across the whole hull
        // rather than spotlighting one point on it.
        var hull = new Vector2(pane.X + pane.Width * 0.31f, pane.Y + pane.Height * 0.41f);
        Pool(blob, hull, pane.Width * 0.42f, new Color(196, 206, 226), 0.5f);

        // Warm rim from the left, behind the button column - the cabin's own lamp. Without something
        // on this side the buttons sit in flat ambient and the relief term has no direction to work
        // with over there.
        Pool(blob, new Vector2(DesignWidth * 0.06f, DesignHeight * 0.42f), 430f, new Color(224, 150, 82), 0.45f);

        // A cold sliver along the top edge, so the panel plate has a lit upper lip.
        Pool(blob, new Vector2(DesignWidth * 0.35f, -60f), 320f, new Color(120, 150, 190), 0.35f);

        _spriteBatch.End();
        _scenePost.EndOwnLight();
    }

    // The menu is lit and graded brighter than the ship interior on purpose: the interior is a dark
    // place you survive in, the front screen is a poster. Saved and restored around Present rather
    // than changed permanently, so none of it leaks into the game itself.
    private (float Exposure, float TonemapWhite, float Vignette, float Grade, float Aberration) ApplyMenuPostLook()
    {
        var saved = (_scenePost.Exposure, _scenePost.TonemapWhite, _scenePost.Vignette,
            _scenePost.GradeStrength, _scenePost.Aberration);
        // Aberration grows with distance from the centre of the frame, and the planet sits hard
        // against the right edge - the worst place for it. On features one or two pixels across, which
        // is exactly what the city lights are, splitting the channels does not read as a lens: it
        // reads as broken pixels, red and green confetti scattered over the night side. Turned most
        // of the way down here, which leaves it doing its job on the long edges that are big enough
        // to carry it.
        _scenePost.Aberration = 0.10f;
        _scenePost.Exposure = 1.75f;
        // A high white point barely compresses anything, so the extra exposure above stays as
        // brightness instead of being folded straight back down by the curve.
        _scenePost.TonemapWhite = 6.5f;
        _scenePost.Vignette = 0.06f;
        _scenePost.GradeStrength = 0.45f;
        return saved;
    }

    private void RestorePostLook(
        (float Exposure, float TonemapWhite, float Vignette, float Grade, float Aberration) saved)
    {
        _scenePost.Aberration = saved.Aberration;
        _scenePost.Exposure = saved.Exposure;
        _scenePost.TonemapWhite = saved.TonemapWhite;
        _scenePost.Vignette = saved.Vignette;
        _scenePost.GradeStrength = saved.Grade;
    }

    private void Pool(Texture2D blob, Vector2 centre, float radius, Color colour, float strength)
    {
        _spriteBatch.Draw(blob,
            new Rectangle((int)(centre.X - radius), (int)(centre.Y - radius), (int)(radius * 2), (int)(radius * 2)),
            colour * strength);
    }
}
