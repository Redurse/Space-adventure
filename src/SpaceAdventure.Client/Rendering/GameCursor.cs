using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SpaceAdventure.Client.Rendering;

// Direct user request ("курсор мышки как в баротравме"): the OS's own plain system arrow doesn't
// fit this client's whole "everything drawn from one white pixel" convention, and it can't change
// shape to say "there's something here to interact with" the way Baro's own cursor does. Game1
// sets IsMouseVisible = false and draws this instead, every frame, at the very end of the HUD pass
// so it's never covered by any panel.
public static class GameCursor
{
    private const int Size = 20;
    private static readonly Color Fill = Color.White;
    private static readonly Color Outline = new(18, 20, 26);
    // The same warm gold this client already uses elsewhere for "you can act on this" (HudIcons'
    // role glyphs, the top bar's own button rings) - reusing it here instead of inventing a new
    // accent keeps "gold = interactive" reading consistently across the whole HUD.
    private static readonly Color InteractiveFill = new(255, 214, 120);

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, Vector2 position, bool interactive)
    {
        if (interactive)
            DrawHand(spriteBatch, pixel, position);
        else
            DrawArrow(spriteBatch, pixel, position);
    }

    // A plain kite-shaped pointer, tip exactly at the mouse position (matching where every click
    // hit-test already reads _designMouse from) - the same silhouette a system cursor would have,
    // just built from filled triangles instead of a bitmap.
    private static void DrawArrow(SpriteBatch spriteBatch, Texture2D pixel, Vector2 tip)
    {
        var a = tip + new Vector2(0, Size);
        var b = tip + new Vector2((int)(Size * 0.62f), (int)(Size * 0.78f));
        var c = tip + new Vector2((int)(Size * 0.38f), (int)(Size * 0.5f));
        var d = tip + new Vector2((int)(Size * 0.86f), (int)(Size * 0.62f));

        // Outline first (every edge nudged out a hair), solid fill on top - the same
        // draw-bigger-then-draw-smaller trick every panel border in this client already uses
        // instead of a real stroked-polygon primitive.
        const float o = 1.5f;
        Primitives.FillTriangle(spriteBatch, pixel, tip + new Vector2(0, -o), a + new Vector2(-o, o), b + new Vector2(o, o), Outline);
        Primitives.FillTriangle(spriteBatch, pixel, tip, a, c, Outline);
        Primitives.FillTriangle(spriteBatch, pixel, c, b, d + new Vector2(o, 0), Outline);

        Primitives.FillTriangle(spriteBatch, pixel, tip, a, b, Fill);
        Primitives.FillTriangle(spriteBatch, pixel, tip, a, c, Fill);
        Primitives.FillTriangle(spriteBatch, pixel, c, b, d, Fill);
    }

    // Hovering something the player can actually act on right now (a device, a door, a dropped
    // item, an NPC - Game1.Input.cs's own ComputeHoveredInteractable). A simple palm-plus-finger
    // silhouette rather than the arrow, so the shape change reads at a glance instead of needing a
    // colour-only tell.
    private static void DrawHand(SpriteBatch spriteBatch, Texture2D pixel, Vector2 tip)
    {
        var palmSize = new Vector2(Size * 0.6f, Size * 0.5f);
        var palm = new Rectangle((int)(tip.X - 1), (int)(tip.Y + Size * 0.32f), (int)palmSize.X, (int)palmSize.Y);
        var finger = new Rectangle((int)(tip.X - 1), (int)tip.Y, (int)(Size * 0.22f), (int)(Size * 0.55f));

        var outlinePalm = new Rectangle(palm.X - 1, palm.Y - 1, palm.Width + 2, palm.Height + 2);
        var outlineFinger = new Rectangle(finger.X - 1, finger.Y - 1, finger.Width + 2, finger.Height + 2);
        spriteBatch.Draw(pixel, outlinePalm, Outline);
        spriteBatch.Draw(pixel, outlineFinger, Outline);
        spriteBatch.Draw(pixel, palm, InteractiveFill);
        spriteBatch.Draw(pixel, finger, InteractiveFill);
    }
}
