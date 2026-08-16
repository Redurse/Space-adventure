using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client.Rendering;

// Shown while the wiring terminal is open (game_design.md section 1, M14): the fixed wiring
// topology (WireNetwork) as a schematic - distribution at top, one junction per system in the
// middle row, one node per physical device at the bottom (Shields has two). Click a link's line
// with a wrench/screwdriver held to repair whichever half is damaged, or with a WireSpool held to
// lay a backup for it (see World.Wiring.cs - one click, tool-dependent, rather than a two-point
// drag).
public sealed class WiringPanel
{
    public const float PixelsPerUnit = 2.5f;
    private const int NodeSize = 14;
    private const int LinkClickRadius = 10;

    private readonly Texture2D _pixel;
    private readonly SpriteFont _font;

    public WiringPanel(GraphicsDevice graphicsDevice, SpriteFont font)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _font = font;
    }

    private static Vector2 NodeCenter(WireNode node, Vector2 panelOrigin) =>
        panelOrigin + new Vector2(node.X, node.Y) * PixelsPerUnit;

    private static Vector2 LinkMidpoint(WireLink link, WorldSnapshot snapshot, Vector2 panelOrigin)
    {
        var from = snapshot.WireNodes.First(n => n.Id == link.FromNodeId);
        var to = snapshot.WireNodes.First(n => n.Id == link.ToNodeId);
        return (NodeCenter(from, panelOrigin) + NodeCenter(to, panelOrigin)) / 2f;
    }

    // Hit-test region for clicking a link — a small square centered on the line's midpoint.
    public static Rectangle GetLinkClickRect(WireLink link, WorldSnapshot snapshot, Vector2 panelOrigin)
    {
        var mid = LinkMidpoint(link, snapshot, panelOrigin);
        return new Rectangle((int)mid.X - LinkClickRadius, (int)mid.Y - LinkClickRadius, LinkClickRadius * 2, LinkClickRadius * 2);
    }

    public void Draw(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 panelOrigin)
    {
        spriteBatch.DrawString(_font, "Проводка - зелёный: цел, жёлтый: держится на резерве, красный: обесточен",
            panelOrigin + new Vector2(0, -24), Color.Yellow, 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

        foreach (var link in snapshot.WireLinks)
        {
            var state = snapshot.WireLinkStates.First(s => s.LinkId == link.Id);
            var from = snapshot.WireNodes.First(n => n.Id == link.FromNodeId);
            var to = snapshot.WireNodes.First(n => n.Id == link.ToNodeId);
            DrawLink(spriteBatch, NodeCenter(from, panelOrigin), NodeCenter(to, panelOrigin), state);
        }

        foreach (var node in snapshot.WireNodes)
            DrawNode(spriteBatch, node, panelOrigin);
    }

    private void DrawLink(SpriteBatch spriteBatch, Vector2 from, Vector2 to, WireLinkState state)
    {
        var color = (state.PrimaryDamaged, state.HasBackup, state.BackupDamaged) switch
        {
            (false, _, _) => Color.LimeGreen, // primary intact - fine regardless of backup
            (true, true, false) => Color.Gold, // primary cut, backup carrying it
            _ => Color.Red, // primary cut, no (working) backup
        };

        var delta = to - from;
        var length = delta.Length();
        if (length < 0.01f)
            return;
        var rotation = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(_pixel, from, null, color, rotation, Vector2.Zero, new Vector2(length, 3f), SpriteEffects.None, 0f);

        if (state.HasBackup)
        {
            // Second, offset line to show a backup physically exists, dimmer when it's not the
            // one currently carrying power.
            var normal = new Vector2(-delta.Y, delta.X);
            normal.Normalize();
            var offset = normal * 4f;
            var backupColor = (state.PrimaryDamaged, state.BackupDamaged) switch
            {
                (true, false) => Color.Gold,
                (_, true) => Color.DarkRed,
                _ => Color.SteelBlue,
            };
            spriteBatch.Draw(_pixel, from + offset, null, backupColor, rotation, Vector2.Zero, new Vector2(length, 2f), SpriteEffects.None, 0f);
        }
    }

    private void DrawNode(SpriteBatch spriteBatch, WireNode node, Vector2 panelOrigin)
    {
        var center = NodeCenter(node, panelOrigin);
        var color = node.Kind switch
        {
            WireNodeKind.Distribution => Color.MediumPurple,
            WireNodeKind.Junction => Color.SlateGray,
            _ => Color.SteelBlue, // Device
        };

        var rect = new Rectangle((int)center.X - NodeSize / 2, (int)center.Y - NodeSize / 2, NodeSize, NodeSize);
        spriteBatch.Draw(_pixel, rect, color);
        spriteBatch.DrawString(_font, node.Label, center + new Vector2(-NodeSize / 2f, NodeSize / 2f + 2),
            Color.LightGray, 0f, Vector2.Zero, 0.4f, SpriteEffects.None, 0f);
    }
}
