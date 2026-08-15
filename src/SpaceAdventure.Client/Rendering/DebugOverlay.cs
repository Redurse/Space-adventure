using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

public sealed class DebugOverlay
{
    private readonly SpriteFont _font;

    public DebugOverlay(SpriteFont font) => _font = font;

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot? snapshot)
    {
        var text = snapshot is null ? "Connecting..." : $"Tick: {snapshot.Tick}";
        spriteBatch.DrawString(_font, text, new Vector2(10, 10), Color.White);
    }
}
