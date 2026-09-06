using System;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// Loading a compiled effect can fail for reasons that have nothing to do with the game being wrong:
// the content build not having run, or a driver that won't accept the shader profile the .fx was
// compiled for. None of those are worth a crash or a black window, so everything here hands back
// null and the caller falls back to the plain SpriteBatch path the renderer already had.
public static class Shaders
{
    // Why the last load failed, for the debug overlay - null while nothing has gone wrong.
    public static string? LastError { get; private set; }

    public static Effect? TryLoad(ContentManager content, string assetName)
    {
        try
        {
            return content.Load<Effect>(assetName);
        }
        catch (Exception ex)
        {
            LastError = $"{assetName}: {ex.Message}";
            return null;
        }
    }
}
