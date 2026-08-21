using System;
using Microsoft.Xna.Framework;
using SpaceAdventure.Client.Rendering;

namespace SpaceAdventure.Client;

// The drawings inside the menu's icon tiles.
//
// They were single pictograms - a triangle, an X, a wrench - each one shape sitting in the middle of
// an empty square. A pictogram is the right answer when an icon has to be understood in a glance at
// the size of a toolbar button, and the wrong one here: these tiles are framed, ticked and lit like
// instruments, and an instrument face with one symbol on it looks like a label, not a readout.
//
// So each is a small schematic instead: two or three related parts that describe the thing rather
// than stand for it. A course between two markers, not an arrow. A hull in plan with its bulkheads,
// not a boat. The tile has room for it now, and detail at this size costs nothing but a few more
// primitives.
//
// Everything is placed in fractions of the tile and scaled at draw time, so the same drawing works
// whatever size the tile ends up.
public partial class Game1
{
    private static Vector2 IconAt(Rectangle box, float u, float v) =>
        new(box.X + box.Width * u, box.Y + box.Height * v);

    private void DrawMainMenuButtonIcon(MainMenuIcon icon, Rectangle box, Color color)
    {
        var s = box.Width / 20f;            // 20 is the size these were drawn against
        var thin = MathF.Max(1f, 1.1f * s);
        var dim = color * 0.55f;

        switch (icon)
        {
            // A course, not an arrow: where you left, where you are going, and the ship somewhere
            // along the way. It says "continue" because it shows a journey already under way.
            case MainMenuIcon.Play:
                // The departure marker went: four objects in a twenty-pixel square left the ship
                // itself no room, and the ship is the one part that has to be unmistakable.
                HudIcons.DrawRingArc(_spriteBatch, _pixel, IconAt(box, 0.80f, 0.22f), 2.6f * s, 0f, 360f, dim, 10, thin);
                HudIcons.FillCircle(_spriteBatch, _pixel, IconAt(box, 0.80f, 0.22f), 1.0f * s, dim);
                for (var i = 0; i < 3; i++)
                {
                    var t0 = i * 0.30f;
                    HudIcons.DrawLine(_spriteBatch, _pixel,
                        IconAt(box, 0.16f + t0 * 0.62f, 0.84f - t0 * 0.62f),
                        IconAt(box, 0.16f + (t0 + 0.16f) * 0.62f, 0.84f - (t0 + 0.16f) * 0.62f),
                        dim, thin);
                }
                Primitives.FillTriangle(_spriteBatch, _pixel,
                    IconAt(box, 0.68f, 0.34f), IconAt(box, 0.40f, 0.46f), IconAt(box, 0.50f, 0.66f), color);
                break;

            // The hull seen from above, with its bulkheads and its engines - a plan, which is what
            // you are about to start filling in.
            case MainMenuIcon.Ship:
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.50f, 0.06f), IconAt(box, 0.26f, 0.46f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.50f, 0.06f), IconAt(box, 0.74f, 0.46f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.26f, 0.46f), IconAt(box, 0.30f, 0.84f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.74f, 0.46f), IconAt(box, 0.70f, 0.84f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.30f, 0.84f), IconAt(box, 0.70f, 0.84f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.27f, 0.55f), IconAt(box, 0.73f, 0.55f), dim, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.29f, 0.70f), IconAt(box, 0.71f, 0.70f), dim, thin);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(box.X + box.Width * 0.34f), (int)(box.Y + box.Height * 0.86f),
                    (int)MathF.Max(2f, 2.4f * s), (int)MathF.Max(2f, 2.4f * s)), color);
                _spriteBatch.Draw(_pixel, new Rectangle((int)(box.X + box.Width * 0.56f), (int)(box.Y + box.Height * 0.86f),
                    (int)MathF.Max(2f, 2.4f * s), (int)MathF.Max(2f, 2.4f * s)), color);
                break;

            // A route across a grid with a marker at the end: a lesson is a path someone lays out for
            // you, which a flag never said.
            case MainMenuIcon.Flag:
                for (var gx = 0; gx < 3; gx++)
                {
                    for (var gy = 0; gy < 3; gy++)
                    {
                        _spriteBatch.Draw(_pixel, new Rectangle(
                            (int)(box.X + box.Width * (0.18f + gx * 0.32f)),
                            (int)(box.Y + box.Height * (0.18f + gy * 0.32f)), 1, 1), dim);
                    }
                }
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.18f, 0.82f), IconAt(box, 0.50f, 0.82f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.50f, 0.82f), IconAt(box, 0.50f, 0.50f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.50f, 0.50f), IconAt(box, 0.82f, 0.50f), color, thin);
                HudIcons.DrawRingArc(_spriteBatch, _pixel, IconAt(box, 0.82f, 0.20f), 2.2f * s, 0f, 360f, color, 10, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.82f, 0.50f), IconAt(box, 0.82f, 0.30f), color, thin);
                break;

            // A mast that is transmitting, with the ground plane it stands on - hosting, rather than
            // the bare waves that could equally mean receiving.
            case MainMenuIcon.Signal:
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.34f, 0.86f), IconAt(box, 0.34f, 0.18f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.20f, 0.88f), IconAt(box, 0.48f, 0.88f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.34f, 0.70f), IconAt(box, 0.22f, 0.86f), dim, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.34f, 0.70f), IconAt(box, 0.46f, 0.86f), dim, thin);
                for (var i = 0; i < 3; i++)
                {
                    HudIcons.DrawRingArc(_spriteBatch, _pixel, IconAt(box, 0.34f, 0.30f),
                        (3f + i * 3.1f) * s, -62f, 62f, i == 0 ? color : dim, 9, thin);
                }
                break;

            // Two ends and the link between them, with the handshake sitting in the middle of it.
            case MainMenuIcon.Plug:
                _spriteBatch.Draw(_pixel, new Rectangle((int)(box.X + box.Width * 0.12f), (int)(box.Y + box.Height * 0.40f),
                    (int)MathF.Max(3f, 4f * s), (int)MathF.Max(3f, 4f * s)), color);
                HudIcons.DrawRingArc(_spriteBatch, _pixel, IconAt(box, 0.82f, 0.50f), 2.8f * s, 0f, 360f, color, 12, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.26f, 0.50f), IconAt(box, 0.68f, 0.50f), dim, thin);
                HudIcons.FillCircle(_spriteBatch, _pixel, IconAt(box, 0.47f, 0.50f), 1.7f * s, color);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.38f, 0.50f), IconAt(box, 0.38f, 0.30f), dim, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.58f, 0.50f), IconAt(box, 0.58f, 0.70f), dim, thin);
                break;

            // A drafting sheet with the drawing on it and its dimension ticks. The wrench said
            // "repair"; this says "draw a ship", which is what the button does.
            case MainMenuIcon.Wrench:
                ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, new Rectangle(
                    (int)(box.X + box.Width * 0.12f), (int)(box.Y + box.Height * 0.14f),
                    (int)(box.Width * 0.76f), (int)(box.Height * 0.72f)), dim, 1);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.72f, 0.14f), IconAt(box, 0.88f, 0.30f), dim, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.50f, 0.28f), IconAt(box, 0.32f, 0.60f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.50f, 0.28f), IconAt(box, 0.68f, 0.60f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.32f, 0.60f), IconAt(box, 0.68f, 0.60f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.24f, 0.72f), IconAt(box, 0.76f, 0.72f), dim, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.24f, 0.68f), IconAt(box, 0.24f, 0.76f), dim, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.76f, 0.68f), IconAt(box, 0.76f, 0.76f), dim, thin);
                break;

            // A crew card: the head is still there, but on a document with a name written beside it,
            // which is what is actually being changed.
            case MainMenuIcon.Person:
                ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, new Rectangle(
                    (int)(box.X + box.Width * 0.10f), (int)(box.Y + box.Height * 0.22f),
                    (int)(box.Width * 0.80f), (int)(box.Height * 0.56f)), dim, 1);
                HudIcons.FillCircle(_spriteBatch, _pixel, IconAt(box, 0.31f, 0.42f), 2.1f * s, color);
                HudIcons.DrawRingArc(_spriteBatch, _pixel, IconAt(box, 0.31f, 0.72f), 3.2f * s, 190f, 350f, color, 9, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.52f, 0.40f), IconAt(box, 0.82f, 0.40f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.52f, 0.54f), IconAt(box, 0.74f, 0.54f), dim, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.52f, 0.66f), IconAt(box, 0.78f, 0.66f), dim, thin);
                break;

            // Three sliders at three different settings. Bars of equal length were a chart; bars with
            // knobs on them are something you can move.
            case MainMenuIcon.Bars:
                for (var i = 0; i < 3; i++)
                {
                    var y = 0.28f + i * 0.22f;
                    HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.14f, y), IconAt(box, 0.86f, y), dim, thin);
                    var knob = i == 0 ? 0.66f : i == 1 ? 0.34f : 0.52f;
                    _spriteBatch.Draw(_pixel, new Rectangle(
                        (int)(box.X + box.Width * knob), (int)(box.Y + box.Height * y - 2.2f * s),
                        (int)MathF.Max(2f, 2.4f * s), (int)MathF.Max(4f, 4.6f * s)), color);
                }
                break;

            // A rosette with its ribbon. Rays and a ribbon read as an award at a glance; a plain disc
            // reads as a coin.
            case MainMenuIcon.Medal:
                // Six short rays, not eight long ones, and the ribbon as a clean V. The long rays
                // plus crossed tails came out reading as a figure with arms and legs.
                for (var i = 0; i < 6; i++)
                {
                    var a2 = i * MathF.PI / 3f;
                    HudIcons.DrawLine(_spriteBatch, _pixel,
                        IconAt(box, 0.50f + MathF.Cos(a2) * 0.19f, 0.38f + MathF.Sin(a2) * 0.19f),
                        IconAt(box, 0.50f + MathF.Cos(a2) * 0.25f, 0.38f + MathF.Sin(a2) * 0.25f), dim, thin);
                }
                HudIcons.DrawRingArc(_spriteBatch, _pixel, IconAt(box, 0.50f, 0.38f), 3.4f * s, 0f, 360f, color, 12, thin);
                HudIcons.FillCircle(_spriteBatch, _pixel, IconAt(box, 0.50f, 0.38f), 1.4f * s, color);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.42f, 0.60f), IconAt(box, 0.50f, 0.90f), color, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.58f, 0.60f), IconAt(box, 0.50f, 0.90f), color, thin);
                break;

            // An airlock hatch with its dogs and its handle. Leaving a ship is a door you have to
            // undo, which is a good deal more final than a cross.
            case MainMenuIcon.Exit:
                ShipRenderer.DrawRectOutline(_spriteBatch, _pixel, new Rectangle(
                    (int)(box.X + box.Width * 0.12f), (int)(box.Y + box.Height * 0.12f),
                    (int)(box.Width * 0.76f), (int)(box.Height * 0.76f)), dim, 1);
                HudIcons.DrawRingArc(_spriteBatch, _pixel, IconAt(box, 0.50f, 0.50f), 5.2f * s, 0f, 360f, color, 14, thin);
                HudIcons.DrawLine(_spriteBatch, _pixel, IconAt(box, 0.32f, 0.62f), IconAt(box, 0.68f, 0.38f), color, thin);
                HudIcons.FillCircle(_spriteBatch, _pixel, IconAt(box, 0.50f, 0.50f), 1.3f * s, color);
                foreach (var (u, v) in new[] { (0.22f, 0.22f), (0.78f, 0.22f), (0.22f, 0.78f), (0.78f, 0.78f) })
                    _spriteBatch.Draw(_pixel, new Rectangle((int)(box.X + box.Width * u), (int)(box.Y + box.Height * v), 2, 2), dim);
                break;
        }
    }
}
