using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// The realms painted over the sky, the way Stellaris draws them. Each held system throws a smooth bump of "influence" around
// itself; the bumps of one realm add up, so close systems run together into one body with rounded edges and holes where a
// free system sits among them. A realm lies where its influence passes a threshold (and beats the other realm by a small
// margin, so two borders do not lie on one line). Inside, a faint dusty fill glows a little brighter toward the edge and
// the edge itself is a thin bright line of the same width at any zoom (found by marching squares and drawn as lines),
// with a soft glow on both sides. The fill is a small texture over the whole galaxy, the lines are kept in map units;
// both are redone whenever who holds what changes.
public sealed partial class GalacticMapPanel
{
    private const int TerritorySize = 768; // a galaxy of 4000 stars is some 7000 units across: 9 units a cell keeps the borders smooth
    // The radius of one system's bump, the influence a realm needs, and how far one realm must lead another to own a point.
    private const float TerritoryReach = 190f;
    private const float TerritoryThreshold = 0.42f;
    private const float TerritoryGap = 0.05f;
    private const float TerritoryMargin = 230f;
    private const float TerritoryRebakeSeconds = 0.5f;

    private Texture2D? _territory;
    private float[] _territoryNoise = Array.Empty<float>();
    private int _territoryForSystems = -1;
    private Vector2 _territoryOrigin;     // map position of the texture top-left corner
    private Vector2 _territoryExtent;     // map size the texture covers
    private byte[] _ownersShown = Array.Empty<byte>();
    private float _territoryBakedAt = -100f;
    private Vector2[] _borderFrom = Array.Empty<Vector2>();
    private Vector2[] _borderTo = Array.Empty<Vector2>();
    private byte[] _borderOwner = Array.Empty<byte>();

    internal static Color OwnerColor(byte owner) => owner switch
    {
        1 => new Color(72, 152, 238),   // the Colonial Union
        2 => new Color(150, 232, 72),   // the virus
        _ => new Color(124, 136, 158),  // nobody
    };

    private void DrawTerritory(SpriteBatch spriteBatch, IReadOnlyList<StarSystemSummary> systems, IReadOnlyList<WarNodeState> nodes,
        Vector2 origin, float scale)
    {
        if (_territoryForSystems != systems.Count)
            PrepareTerritory(systems);

        var owners = new byte[systems.Count];
        for (var i = 0; i < owners.Length; i++)
            owners[i] = nodes[i].Owner;
        if (_territory is null || (!owners.AsSpan().SequenceEqual(_ownersShown) && _time - _territoryBakedAt >= TerritoryRebakeSeconds))
        {
            BakeTerritory(systems, owners);
            _ownersShown = owners;
            _territoryBakedAt = _time;
        }

        var topLeft = origin + _territoryOrigin * scale;
        var size = _territoryExtent * scale;
        spriteBatch.Draw(_territory, new Rectangle((int)topLeft.X, (int)topLeft.Y, (int)MathF.Ceiling(size.X), (int)MathF.Ceiling(size.Y)), Color.White * (0.94f + 0.06f * MathF.Sin(_time * 0.6f)));
        DrawBorders(spriteBatch, origin, scale);
    }

    // The edge of every realm: a wide faint line for the glow and a thin bright one for the edge, in the realm colour.
    private void DrawBorders(SpriteBatch spriteBatch, Vector2 origin, float scale)
    {
        for (var i = 0; i < _borderFrom.Length; i++)
        {
            var a = origin + _borderFrom[i] * scale;
            var b = origin + _borderTo[i] * scale;
            if (!InView(a, 20f) && !InView(b, 20f))
                continue;
            // Opaque and a touch long, so the joins between the short pieces neither beads nor gaps.
            var along = b - a;
            if (along.LengthSquared() > 1e-6f)
                along.Normalize();
            // A dark underlay lifts the edge off the fill and keeps two realms apart; the edge itself shimmers a little.
            DrawSoftLine(spriteBatch, a - along * 0.8f, b + along * 0.8f, Color.Black * 0.38f, 7.5f);
            var shimmer = 0.3f + 0.07f * MathF.Sin(_time * 1.4f + (_borderFrom[i].X + _borderFrom[i].Y) * 0.004f);
            DrawSoftLine(spriteBatch, a - along * 0.8f, b + along * 0.8f, Color.Lerp(OwnerColor(_borderOwner[i]), Color.White, shimmer), 4.2f);
        }
    }

    private void PrepareTerritory(IReadOnlyList<StarSystemSummary> systems)
    {
        _territoryForSystems = systems.Count;
        var minX = systems.Min(s => s.GalaxyX) - TerritoryMargin;
        var minY = systems.Min(s => s.GalaxyY) - TerritoryMargin;
        var maxX = systems.Max(s => s.GalaxyX) + TerritoryMargin;
        var maxY = systems.Max(s => s.GalaxyY) + TerritoryMargin;
        _territoryOrigin = new Vector2(minX, minY);
        _territoryExtent = new Vector2(maxX - minX, maxY - minY);

        // Slow dust in the fill, worked out once: a realm is not a flat wash.
        _territoryNoise = new float[TerritorySize * TerritorySize];
        for (var ty = 0; ty < TerritorySize; ty++)
        for (var tx = 0; tx < TerritorySize; tx++)
        {
            var wx = minX + (tx + 0.5f) / TerritorySize * _territoryExtent.X;
            var wy = minY + (ty + 0.5f) / TerritorySize * _territoryExtent.Y;
            var warpX = wx + 70f * (MapArt.Fbm(wx / 160f, wy / 160f, 4411, 3) - 0.5f);
            var warpY = wy + 70f * (MapArt.Fbm(wx / 160f + 7f, wy / 160f + 3f, 5521, 3) - 0.5f);
            _territoryNoise[ty * TerritorySize + tx] = 0.6f * MapArt.Fbm(wx / 230f, wy / 230f, 7311, 4) + 0.4f * MapArt.Fbm(warpX / 70f, warpY / 70f, 8123, 3);
        }

        _territory?.Dispose();
        _territory = new Texture2D(_device, TerritorySize, TerritorySize);
        _ownersShown = Array.Empty<byte>();
    }

    private void BakeTerritory(IReadOnlyList<StarSystemSummary> systems, byte[] owners)
    {
        const int n = TerritorySize;
        var cellX = _territoryExtent.X / n;
        var cellY = _territoryExtent.Y / n;

        // The influence of each realm over the whole picture.
        var field = new float[3][];
        for (var o = 1; o <= 2; o++)
        {
            var sum = new float[n * n];
            for (var i = 0; i < owners.Length; i++)
            {
                if (owners[i] != o)
                    continue;
                var cx = (systems[i].GalaxyX - _territoryOrigin.X) / cellX;
                var cy = (systems[i].GalaxyY - _territoryOrigin.Y) / cellY;
                var rx = (int)MathF.Ceiling(TerritoryReach / cellX);
                var ry = (int)MathF.Ceiling(TerritoryReach / cellY);
                for (var y = Math.Max(0, (int)cy - ry); y <= Math.Min(n - 1, (int)cy + ry); y++)
                for (var x = Math.Max(0, (int)cx - rx); x <= Math.Min(n - 1, (int)cx + rx); x++)
                {
                    var dx = (x + 0.5f - cx) * cellX;
                    var dy = (y + 0.5f - cy) * cellY;
                    var t = (dx * dx + dy * dy) / (TerritoryReach * TerritoryReach);
                    if (t >= 1f)
                        continue;
                    var k = 1f - t;
                    sum[y * n + x] += k * k * k;
                }
            }
            for (var t = 0; t < sum.Length; t++)
                sum[t] = 1f - MathF.Exp(-1.6f * sum[t]);
            field[o] = sum;
        }

        // How far each realm leads at every point; positive is inside it. The lead over the other realm needs a small margin.
        var lead = new float[3][];
        for (var o = 1; o <= 2; o++)
        {
            var other = field[3 - o];
            var h = new float[n * n];
            for (var t = 0; t < h.Length; t++)
                h[t] = field[o][t] - MathF.Max(TerritoryThreshold, other[t] + TerritoryGap);
            lead[o] = h;
        }

        BakeTerritoryFill(lead, cellX, cellY);
        BakeTerritoryBorders(lead, cellX, cellY);
    }

    // The fill: a dusty wash that glows toward the edge, and a soft halo just outside it.
    private void BakeTerritoryFill(float[][] lead, float cellX, float cellY)
    {
        const int n = TerritorySize;
        var pixels = new Color[n * n];
        for (var ty = 1; ty < n - 1; ty++)
        for (var tx = 1; tx < n - 1; tx++)
        {
            var t = ty * n + tx;
            var owner = lead[1][t] >= lead[2][t] ? 1 : 2;
            var h = lead[owner];
            var value = h[t];
            var gx = (h[t + 1] - h[t - 1]) / (2f * cellX);
            var gy = (h[t + n] - h[t - n]) / (2f * cellY);
            var gradient = MathF.Max(1e-5f, MathF.Sqrt(gx * gx + gy * gy));
            var distance = value / gradient; // map units to the edge: positive inside, negative outside

            float alpha;
            var color = OwnerColor((byte)owner).ToVector3();
            if (distance >= 0f)
            {
                var dust = 0.58f + 0.95f * _territoryNoise[t];
                alpha = (0.19f + 0.30f * MathF.Exp(-distance / 40f)) * dust;
                color = Vector3.Lerp(color, Vector3.One, 0.18f * MathF.Exp(-distance / 12f));
            }
            else if (distance > -34f)
            {
                alpha = 0.16f * MathF.Exp(-(-distance) / 11f);
            }
            else
            {
                continue;
            }
            alpha = Math.Clamp(alpha, 0f, 0.9f);
            pixels[t] = new Color(color.X * alpha, color.Y * alpha, color.Z * alpha, alpha);
        }
        _territory!.SetData(pixels);
    }

    // The edge lines: where each realm lead crosses zero, by marching squares, in map units.
    private void BakeTerritoryBorders(float[][] lead, float cellX, float cellY)
    {
        const int n = TerritorySize;
        var from = new List<Vector2>();
        var to = new List<Vector2>();
        var owner = new List<byte>();
        // Which edges of a cell (0 top, 1 right, 2 bottom, 3 left) the line joins, by which corners are inside.
        int[][] joins =
        {
            Array.Empty<int>(), new[] { 3, 0 }, new[] { 0, 1 }, new[] { 3, 1 }, new[] { 1, 2 }, new[] { 3, 0, 1, 2 }, new[] { 0, 2 }, new[] { 3, 2 },
            new[] { 2, 3 }, new[] { 0, 2 }, new[] { 0, 1, 2, 3 }, new[] { 1, 2 }, new[] { 1, 3 }, new[] { 0, 1 }, new[] { 3, 0 }, Array.Empty<int>(),
        };

        for (var o = 1; o <= 2; o++)
        {
            var h = lead[o];
            for (var y = 0; y < n - 1; y++)
            for (var x = 0; x < n - 1; x++)
            {
                var tl = h[y * n + x];
                var tr = h[y * n + x + 1];
                var br = h[(y + 1) * n + x + 1];
                var bl = h[(y + 1) * n + x];
                var index = (tl > 0f ? 1 : 0) | (tr > 0f ? 2 : 0) | (br > 0f ? 4 : 0) | (bl > 0f ? 8 : 0);
                var join = joins[index];
                if (join.Length == 0)
                    continue;

                Vector2 Cross(int edge)
                {
                    float a, b;
                    Vector2 pa, pb;
                    switch (edge)
                    {
                        case 0: a = tl; b = tr; pa = new Vector2(x, y); pb = new Vector2(x + 1, y); break;
                        case 1: a = tr; b = br; pa = new Vector2(x + 1, y); pb = new Vector2(x + 1, y + 1); break;
                        case 2: a = bl; b = br; pa = new Vector2(x, y + 1); pb = new Vector2(x + 1, y + 1); break;
                        default: a = tl; b = bl; pa = new Vector2(x, y); pb = new Vector2(x, y + 1); break;
                    }
                    var span = a - b;
                    var f = MathF.Abs(span) < 1e-9f ? 0.5f : Math.Clamp(a / span, 0f, 1f);
                    var cell = Vector2.Lerp(pa, pb, f) + new Vector2(0.5f, 0.5f);
                    return _territoryOrigin + new Vector2(cell.X * cellX, cell.Y * cellY);
                }

                for (var k = 0; k < join.Length; k += 2)
                {
                    from.Add(Cross(join[k]));
                    to.Add(Cross(join[k + 1]));
                    owner.Add((byte)o);
                }
            }
        }
        _borderFrom = from.ToArray();
        _borderTo = to.ToArray();
        _borderOwner = owner.ToArray();
    }
}
