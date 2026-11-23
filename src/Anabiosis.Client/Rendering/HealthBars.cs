using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Anabiosis.Client.Rendering;

// The health bar over a head, the way Barotrauma shows it: nobody carries one around all the time - it comes up
// over whoever has just been hurt, stays a few seconds, and fades. One rule for everybody who can be hurt: the
// crew and its bots, a boarded ship's defenders, a station's guards, staff and residents.
// Every draw call both notes the person's health (to notice the next drop) and draws the bar if one is due, so
// a renderer only has to call Draw once per person per frame.
internal static class HealthBars
{
    // The "health indicators" graphics setting: off hides every bar, the bookkeeping goes on.
    public static bool Enabled { get; set; } = true;

    private const double ShowSeconds = 5.0;
    private const double FadeSeconds = 1.0;
    private const int Width = 30;
    private const int Height = 4;

    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly Dictionary<string, (float Fraction, double HurtAt)> Seen = new();

    // key names the person across frames ("char:3", "crew:enemy-crew-1"); fraction is their health from 0 to 1;
    // top is the middle of where the bar goes, just above the head.
    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, string key, float fraction, Vector2 top)
    {
        fraction = Math.Clamp(fraction, 0f, 1f);
        var now = Clock.Elapsed.TotalSeconds;
        var hurtAt = double.NegativeInfinity;
        if (Seen.TryGetValue(key, out var seen))
            hurtAt = fraction < seen.Fraction - 0.001f ? now : seen.HurtAt;
        Seen[key] = (fraction, hurtAt);

        var age = now - hurtAt;
        if (!Enabled || fraction <= 0f || age > ShowSeconds)
            return;
        var alpha = (float)Math.Clamp((ShowSeconds - age) / FadeSeconds, 0.0, 1.0);
        var x = (int)top.X - Width / 2;
        var y = (int)top.Y;
        spriteBatch.Draw(pixel, new Rectangle(x - 1, y - 1, Width + 2, Height + 2), Color.Black * (0.75f * alpha));
        // Green when healthy, through yellow, to red when nearly gone.
        var color = fraction > 0.5f
            ? Color.Lerp(new Color(232, 200, 60), new Color(96, 204, 96), (fraction - 0.5f) * 2f)
            : Color.Lerp(new Color(214, 60, 48), new Color(232, 200, 60), fraction * 2f);
        spriteBatch.Draw(pixel, new Rectangle(x, y, Math.Max(1, (int)(Width * fraction)), Height), color * alpha);
    }
}
