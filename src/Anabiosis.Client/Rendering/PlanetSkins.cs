using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

// One baked surface per celestial body (PlanetTexture), shared by everything that draws planets - the view
// out of the ship (FieldRenderer) and the system map (GalaxyMapPanel) - so a body is baked once, not once per
// screen. A bake costs hundreds of milliseconds, so it runs on a background task; until it lands TryGet says
// no and the caller draws its own stand-in. Draw-thread only.
internal static class PlanetSkins
{
    private static readonly Dictionary<string, PlanetTexture.Skin> Ready = new();
    private static readonly Dictionary<string, Task<(Color[] Pixels, int Side, float HalfExtentUnits)>> Pending = new();

    internal static bool TryGet(GraphicsDevice device, CelestialBody body, out PlanetTexture.Skin skin)
    {
        if (Ready.TryGetValue(body.Id, out skin))
            return true;

        if (!Pending.TryGetValue(body.Id, out var pending))
        {
            pending = Task.Run(() => PlanetTexture.BakePixels(body));
            Pending[body.Id] = pending;
        }
        if (!pending.IsCompletedSuccessfully)
            return false;

        var (pixels, side, halfExtentUnits) = pending.Result;
        var texture = new Texture2D(device, side, side);
        texture.SetData(pixels);
        skin = new PlanetTexture.Skin(texture, halfExtentUnits);
        Ready[body.Id] = skin;
        Pending.Remove(body.Id);
        return true;
    }
}
