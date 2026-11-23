using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The names of the realms, as Stellaris writes them over its empires when the map is seen from far: the crest of the realm
// and its name in spaced capitals, at the middle of its largest unbroken body (the systems joined by lanes of one owner).
// They fade out as the map is zoomed in, when the names of the systems take over.
public sealed partial class GalacticMapPanel
{
    private Texture2D? _unionCrest;
    private Texture2D? _virusCrest;
    private byte[]? _realmsFor;
    private readonly Vector2[] _realmCenter = new Vector2[3];  // map units
    private readonly int[] _realmSystems = new int[3];

    private static string RealmName(byte owner) => owner == 1 ? "КОЛОНИАЛЬНЫЙ СОЮЗ" : "ВОССТАНИЕ";

    private void FindRealms(WorldSnapshot snapshot)
    {
        _realmsFor = _ownersShown;
        var nodes = snapshot.WarNodes!;
        var systems = snapshot.StarSystems;
        var parent = Enumerable.Range(0, nodes.Count).ToArray();
        int Find(int x)
        {
            while (parent[x] != x)
            {
                parent[x] = parent[parent[x]];
                x = parent[x];
            }
            return x;
        }
        if (snapshot.WarLanes is { } lanes)
            foreach (var lane in lanes)
                if (nodes[lane.A].Owner != 0 && nodes[lane.A].Owner == nodes[lane.B].Owner)
                    parent[Find(lane.A)] = Find(lane.B);

        for (var owner = 1; owner <= 2; owner++)
        {
            var counts = new Dictionary<int, int>();
            for (var i = 0; i < nodes.Count; i++)
                if (nodes[i].Owner == owner)
                    counts[Find(i)] = counts.GetValueOrDefault(Find(i)) + 1;
            if (counts.Count == 0)
            {
                _realmSystems[owner] = 0;
                continue;
            }
            var best = counts.OrderByDescending(p => p.Value).First();
            var sum = Vector2.Zero;
            for (var i = 0; i < nodes.Count; i++)
                if (nodes[i].Owner == owner && Find(i) == best.Key)
                    sum += new Vector2(systems[i].GalaxyX, systems[i].GalaxyY);
            _realmCenter[owner] = sum / best.Value;
            _realmSystems[owner] = best.Value;
        }
    }

    private void DrawRealmNames(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, float zoom, float scale)
    {
        var alpha = Math.Clamp((1.0f - zoom) / 0.45f, 0f, 1f);
        if (alpha <= 0.01f)
            return;
        if (!ReferenceEquals(_realmsFor, _ownersShown))
            FindRealms(snapshot);
        _unionCrest ??= MapArt.UnionCrest(_device);
        _virusCrest ??= MapArt.VirusCrest(_device);

        for (byte owner = 1; owner <= 2; owner++)
        {
            if (_realmSystems[owner] < 3)
                continue;
            var at = origin + _realmCenter[owner] * scale;
            if (!InView(at, 160f))
                continue;
            var color = OwnerColor(owner);
            var crestSize = Math.Clamp(54f * (0.7f + zoom), 44f, 86f);
            var crest = owner == 1 ? _unionCrest : _virusCrest;
            var crestScale = crestSize / 128f;
            spriteBatch.Draw(crest, at + new Vector2(2f, 2f), null, Color.Black * (0.55f * alpha), 0f, new Vector2(64f, 64f), crestScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(crest, at, null, Color.Lerp(color, Color.White, 0.5f) * (0.95f * alpha), 0f, new Vector2(64f, 64f), crestScale, SpriteEffects.None, 0f);
            // A great realm is written larger than a small one.
            var textScale = Math.Clamp(0.62f + 0.05f * MathF.Sqrt(_realmSystems[owner]), 0.75f, 1.4f);
            DrawSpaced(spriteBatch, RealmName(owner), new Vector2(at.X, at.Y + crestSize / 2f + 8f), textScale * _titleUnit, Color.Lerp(color, Color.White, 0.65f) * alpha, 3f);
        }
    }

    // Text with room between the letters, centred on `top.X`.
    private void DrawSpaced(SpriteBatch spriteBatch, string text, Vector2 top, float textScale, Color color, float spacing)
    {
        var width = 0f;
        foreach (var c in text)
            width += _titleFont.MeasureString(c.ToString()).X * textScale + spacing;
        width -= spacing;
        var x = top.X - width / 2f;
        foreach (var c in text)
        {
            var glyph = c.ToString();
            spriteBatch.DrawString(_titleFont, glyph, new Vector2(x + 1.2f, top.Y + 1.2f), Color.Black * (color.A / 255f * 0.8f), 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(_titleFont, glyph, new Vector2(x, top.Y), color, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
            x += _titleFont.MeasureString(glyph).X * textScale + spacing;
        }
    }
}
