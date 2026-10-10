using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client.Rendering;

// How badly hurt a compartment is, and the explosions that finish it (direct user request: "при разном
// количестве хп отсек должен быть повреждён визуально", "большой красивый взрыв").
//
// A compartment's health comes with the snapshot (RoomHpState, World.RoomHp.cs); this turns it into marks on
// the floor that pile up as it falls - scorching, cracks, then flickering sparks, then flames and a pulsing red
// alarm glow near the end. All positions are picked by hashing the room's id, so the marks stay where they are
// from frame to frame (and look the same to every player) instead of shimmering.
public sealed partial class ShipRenderer
{
    // ---- damage on the floor ----

    private void DrawRoomDamage(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, float totalSeconds)
    {
        if (snapshot.RoomHp is not { Count: > 0 } states)
            return;
        foreach (var room in snapshot.Rooms)
        {
            var state = states.FirstOrDefault(s => s.RoomId == room.Id);
            if (state is null || state.MaxHp <= 0f)
                continue;
            var damage = 1f - Math.Clamp(state.Hp / state.MaxHp, 0f, 1f);
            if (damage < 0.08f)
                continue;

            var seed = StableActorId(room.Id);
            var markIndex = 0;
            foreach (var rect in room.Rects)
                DrawDamageInRect(spriteBatch, rect, origin, damage, seed, ref markIndex, totalSeconds);
        }
    }

    private void DrawDamageInRect(SpriteBatch spriteBatch, RectF rect, Vector2 origin, float damage, int seed, ref int index, float t)
    {
        // Marks stay off the wall ring: inset by a tile when the room is big enough to have an inside.
        var inset = rect.Width > 3f && rect.Height > 3f ? 1f : 0f;
        var x0 = rect.X + inset;
        var y0 = rect.Y + inset;
        var w = Math.Max(0.5f, rect.Width - inset * 2f);
        var h = Math.Max(0.5f, rect.Height - inset * 2f);
        var area = rect.Width * rect.Height;

        // The whole room darkens and warms slightly as it goes: it should look hurt before you can see any single mark.
        var roomRect = new Rectangle((int)(origin.X + rect.X * PixelsPerUnit), (int)(origin.Y + rect.Y * PixelsPerUnit),
            (int)(rect.Width * PixelsPerUnit), (int)(rect.Height * PixelsPerUnit));
        spriteBatch.Draw(_pixel, roomRect, new Color(20, 10, 6) * (0.04f + 0.16f * damage));

        // Scorch marks.
        var scorch = (int)(area * damage * 0.6f);
        for (var i = 0; i < scorch; i++)
        {
            var centre = ScreenFromUnits(origin, x0, y0, w, h, seed, index + i, 1);
            var radius = (0.18f + 0.3f * PixelCanvas.Hash(seed + i * 5, 3)) * PixelsPerUnit;
            // An irregular smudge: a few overlapping soft discs rather than one round stain.
            for (var lobe = 0; lobe < 5; lobe++)
            {
                var angle = PixelCanvas.Hash(seed + i * 3 + lobe, 17) * MathF.Tau;
                var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius * 0.7f * PixelCanvas.Hash(seed + i + lobe * 7, 19);
                HudIcons.FillCircle(spriteBatch, _pixel, centre + offset, radius * (0.5f + 0.5f * PixelCanvas.Hash(seed + lobe, 23)), new Color(24, 16, 12) * (0.07f + 0.1f * damage));
            }
        }

        // Cracks: short jagged lines.
        if (damage > 0.2f)
        {
            var cracks = (int)(area * damage * 0.35f);
            for (var i = 0; i < cracks; i++)
            {
                var start = ScreenFromUnits(origin, x0, y0, w, h, seed, index + i, 2);
                var angle = PixelCanvas.Hash(seed + i * 11, 5) * MathF.Tau;
                var length = (0.5f + 0.9f * PixelCanvas.Hash(seed + i * 7, 9)) * PixelsPerUnit;
                var mid = start + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * length * 0.5f
                    + new Vector2(-MathF.Sin(angle), MathF.Cos(angle)) * length * 0.18f * (PixelCanvas.Hash(seed + i, 21) - 0.5f);
                var end = start + new Vector2(MathF.Cos(angle + 0.25f), MathF.Sin(angle + 0.25f)) * length;
                HudIcons.DrawLine(spriteBatch, _pixel, start, mid, Color.Black * (0.45f + 0.3f * damage), 1.6f);
                HudIcons.DrawLine(spriteBatch, _pixel, mid, end, Color.Black * (0.45f + 0.3f * damage), 1.3f);
            }
        }

        // Sparks: blink on and off at their own rhythm.
        if (damage > 0.45f)
        {
            var sparks = Math.Max(1, (int)(area * damage * 0.25f));
            for (var i = 0; i < sparks; i++)
            {
                var rate = 5f + 9f * PixelCanvas.Hash(seed + i * 3, 31);
                var phase = PixelCanvas.Hash(seed + i * 13, 37) * MathF.Tau;
                var on = MathF.Sin(t * rate + phase);
                if (on < 0.55f)
                    continue;
                var centre = ScreenFromUnits(origin, x0, y0, w, h, seed, index + i, 3);
                var strength = (on - 0.55f) / 0.45f;
                HudIcons.FillCircle(spriteBatch, _pixel, centre, 7f * strength + 2f, new Color(255, 190, 90) * (0.22f * strength));
                HudIcons.FillCircle(spriteBatch, _pixel, centre, 2.2f, new Color(255, 245, 200) * strength);
            }
        }

        // Flames in a room that is nearly gone.
        if (damage > 0.7f)
        {
            var flames = Math.Max(1, (int)(area * damage * 0.15f));
            for (var i = 0; i < flames; i++)
            {
                var centre = ScreenFromUnits(origin, x0, y0, w, h, seed, index + i, 4);
                var flicker = 0.75f + 0.25f * MathF.Sin(t * (9f + i) + i * 1.7f);
                var size = (6f + 7f * damage) * flicker;
                var sway = MathF.Sin(t * 7f + i * 2.3f) * size * 0.25f;
                HudIcons.FillCircle(spriteBatch, _pixel, centre, size * 2.2f, new Color(255, 80, 20) * 0.10f);
                HudIcons.FillCircle(spriteBatch, _pixel, centre, size, new Color(235, 90, 25) * 0.62f);
                HudIcons.FillCircle(spriteBatch, _pixel, centre + new Vector2(sway * 0.5f, -size * 0.7f), size * 0.7f, new Color(255, 150, 40) * 0.7f);
                HudIcons.FillCircle(spriteBatch, _pixel, centre + new Vector2(sway, -size * 1.35f), size * 0.38f, new Color(255, 235, 150) * 0.85f);
            }
        }

        // Alarm: a red pulse over the whole room when it is about to go.
        if (damage > 0.8f)
        {
            var pulse = 0.5f + 0.5f * MathF.Sin(t * 5.5f);
            spriteBatch.Draw(_pixel, roomRect, new Color(255, 30, 20) * ((damage - 0.8f) * 0.35f + 0.05f * pulse));
        }

        index += Math.Max(scorch, 1);
    }

    // A deterministic point inside the rect, in screen pixels: the rect is given in ship units, `origin` is where unit
    // (0,0) lands on screen.
    private static Vector2 ScreenFromUnits(Vector2 origin, float x0, float y0, float w, float h, int seed, int mark, int salt) =>
        origin + new Vector2(
            x0 + w * PixelCanvas.Hash(seed + mark * 31 + salt * 101, 7),
            y0 + h * PixelCanvas.Hash(seed + mark * 17 + salt * 53, 13)) * PixelsPerUnit;

    // ---- explosions ----

    // The explosions of the last couple of seconds, drawn on top of everything after the lighting is applied (so a blast
    // is as bright in a dark compartment as anywhere), additive so overlapping ones add up to a proper fireball. The
    // age of each comes from the server, so every player sees the same frame of the same blast.
    public void DrawShipBlasts(SpriteBatch spriteBatch, WorldSnapshot snapshot, Vector2 origin, Matrix sceneTransform)
    {
        if (snapshot.ShipBlasts is not { Count: > 0 } blasts)
            return;

        var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
        spriteBatch.Begin(blendState: BlendState.Additive, transformMatrix: sceneTransform);
        foreach (var blast in blasts)
        {
            // A blast on the ship is in the ship's own frame; one out in space (a torn-off piece going up) is in field
            // coordinates and has to be carried into the same screen space the ship is drawn in.
            var centre = blast.InField
                ? WorldToScreen(new Vec2(blast.X, blast.Y), snapshot.ShipField, hullCenter, origin)
                : origin + new Vector2(blast.X, blast.Y) * PixelsPerUnit;
            DrawBlast(spriteBatch, blast, centre);
        }
        spriteBatch.End();
    }

    // A soft glow: stacked discs, each a fraction as bright, so under additive blending it falls off smoothly from a
    // bright centre instead of reading as one flat coin.
    private void Glow(SpriteBatch spriteBatch, Vector2 centre, float radius, Color color, float alpha, int layers = 10)
    {
        for (var i = 0; i < layers; i++)
        {
            var f = (i + 1f) / layers;
            HudIcons.FillCircle(spriteBatch, _pixel, centre, radius * f, color * (alpha / layers * 3.2f));
        }
    }

    private void DrawBlast(SpriteBatch spriteBatch, ShipBlastState blast, Vector2 centre)
    {
        var big = blast.Kind == ShipBlastKind.Reactor;
        var radius = blast.Radius * PixelsPerUnit;
        var age = blast.Age;
        var seed = StableActorId(blast.Id);
        static float Ease(float x) => 1f - (1f - Math.Clamp(x, 0f, 1f)) * (1f - Math.Clamp(x, 0f, 1f)); // fast at first, then settles
        float Fade(float x) => 1f - Math.Clamp(x, 0f, 1f);

        // The flash: the first instant, a blinding disc far bigger than the room.
        if (age < 0.22f)
        {
            var k = age / 0.22f;
            var flash = radius * (big ? 2.2f : 1.3f) * (0.5f + 0.5f * Ease(k));
            Glow(spriteBatch, centre, flash, new Color(255, 240, 200), 1.0f * Fade(k), 14);
            Glow(spriteBatch, centre, flash * 0.45f, Color.White, 0.9f * Fade(k), 8);
        }

        // The fireball: swells and cools from white-yellow through orange to a dull red.
        const float fireSeconds = 1.3f;
        if (age < fireSeconds)
        {
            var k = age / fireSeconds;
            var size = radius * (big ? 0.95f : 0.62f) * Ease(k * 1.15f);
            var fade = Fade(k);
            Glow(spriteBatch, centre, size * 1.3f, new Color(255, 60, 20), 0.55f * fade, 12);
            Glow(spriteBatch, centre, size * 0.95f, new Color(255, 125, 30), 0.65f * fade, 10);
            Glow(spriteBatch, centre, size * 0.6f, new Color(255, 205, 90), 0.75f * fade, 8);
            Glow(spriteBatch, centre, size * 0.3f, new Color(255, 250, 225), 0.85f * fade, 6);

            // Lumps of fire thrown off the edge of the fireball so it is not a perfect disc.
            for (var i = 0; i < 9; i++)
            {
                var angle = PixelCanvas.Hash(seed + i * 7, 3) * MathF.Tau;
                var reach = size * (0.55f + 0.5f * PixelCanvas.Hash(seed + i * 5, 9));
                var lump = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * reach;
                Glow(spriteBatch, centre + lump, size * (0.25f + 0.2f * PixelCanvas.Hash(seed + i, 11)), new Color(255, 120, 30), 0.5f * fade, 7);
            }
        }

        // The shockwave: a bright ring racing out to the edge of the blast (twice for the reactor).
        const float waveSeconds = 0.85f;
        if (age < waveSeconds)
        {
            var k = age / waveSeconds;
            var ring = radius * Ease(k) * 1.05f;
            HudIcons.DrawRingArc(spriteBatch, _pixel, centre, ring, 0f, 360f, new Color(255, 225, 170) * (0.75f * Fade(k)), 48, 4.5f * Fade(k) + 1f);
            HudIcons.DrawRingArc(spriteBatch, _pixel, centre, ring * 0.82f, 0f, 360f, new Color(255, 150, 70) * (0.4f * Fade(k)), 40, 2f);
        }
        if (big && age > 0.25f && age < 1.35f)
        {
            var k = (age - 0.25f) / 1.1f;
            HudIcons.DrawRingArc(spriteBatch, _pixel, centre, radius * 1.9f * Ease(k), 0f, 360f, new Color(255, 200, 150) * (0.6f * Fade(k)), 64, 6f * Fade(k) + 1f);
        }

        // Shrapnel: streaks flying out with a fading tail.
        const float shrapnelSeconds = 1.5f;
        if (age < shrapnelSeconds)
        {
            var k = age / shrapnelSeconds;
            var count = big ? 34 : 20;
            for (var i = 0; i < count; i++)
            {
                var angle = PixelCanvas.Hash(seed + i * 13, 17) * MathF.Tau;
                var speed = 0.55f + 0.7f * PixelCanvas.Hash(seed + i * 29, 19);
                var direction = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
                var head = centre + direction * radius * 1.1f * speed * Ease(k);
                var tail = head - direction * (10f + 22f * Fade(k));
                HudIcons.DrawLine(spriteBatch, _pixel, tail, head, new Color(255, 200, 120) * (0.8f * Fade(k)), 2f);
                HudIcons.FillCircle(spriteBatch, _pixel, head, 2.4f, Color.White * Fade(k));
            }
        }

        // The glow that lingers on whatever is left, while the smoke builds.
        if (age > 0.3f)
        {
            var k = (age - 0.3f) / 2.3f;
            Glow(spriteBatch, centre, radius * 0.6f, new Color(190, 60, 20), 0.35f * Fade(k), 9);
        }
    }
}
