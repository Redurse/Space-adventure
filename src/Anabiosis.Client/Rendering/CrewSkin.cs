using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Anabiosis.Shared.Model;

namespace Anabiosis.Client.Rendering;

/// <summary>A crew member seen from straight above, in the Cosmoteer idiom.</summary>
///
/// A small, smooth figure built from a handful of rounded shapes - boots, arms, shoulders, a round
/// head - each with a thin dark outline and a soft highlight, all turned to face the way the
/// character is heading. There is no front/side/back to draw: the camera looks down, so one figure
/// rotated to the facing angle covers every direction. The walk is the limbs swinging against each
/// other, driven by how far the character has actually moved.
///
/// Everything is in world units relative to the figure's centre, facing +X and with +Y to the right
/// of the facing direction, so the shapes can be tuned without touching a pixel. The whole figure is
/// about 0.65 of a tile across - small on purpose, like a crew member in Cosmoteer.
/// <summary>What a figure is wearing: ordinary crew clothes, a spacesuit, or the armour of a boarding
/// raider / station guard (helmet, plates, coloured visor).</summary>
public enum Outfit { Crew, Suit, Raider, Guard }

public sealed class CrewSkin : IDisposable
{
    private const int DiscSize = 48;
    private static readonly Color OutlineTint = new(22, 22, 30);
    private static readonly Color Boot = new(50, 52, 60);
    private static readonly Color SuitShell = new(204, 208, 218);
    private static readonly Color SuitPack = new(150, 156, 170);
    private static readonly Color Visor = new(34, 84, 118);
    private static readonly Color VisorGlint = new(170, 220, 240);

    // Skin and hair vary per person (picked from the player id) so a crew of four is four people.
    private static readonly Color[] SkinTones =
    {
        new(240, 200, 160), new(224, 172, 128), new(198, 140, 100), new(150, 100, 70), new(110, 72, 50),
    };
    private static readonly Color[] HairColors =
    {
        new(40, 32, 30), new(78, 52, 34), new(122, 84, 44), new(190, 150, 80), new(150, 56, 40), new(210, 210, 214),
    };

    private readonly Texture2D _disc;

    public CrewSkin(GraphicsDevice graphics) => _disc = BakeDisc(graphics);

    public void Dispose() => _disc.Dispose();

    /// <summary>The uniform colour for a role. Someone who has not picked one wears the default orange
    /// (a hired hand without a role, the old blue).</summary>
    public static Color UniformFor(CrewRole? role, bool isBot) => role switch
    {
        CrewRole.Captain => new Color(48, 92, 168),
        CrewRole.Engineer => new Color(220, 132, 34),
        CrewRole.Mechanic => new Color(150, 100, 56),
        CrewRole.Security => new Color(162, 50, 54),
        CrewRole.Scientist => new Color(44, 152, 152),
        _ => isBot ? new Color(70, 110, 150) : new Color(196, 78, 44),
    };

    public static Color AccentFor(bool isBot) => isBot ? new Color(150, 200, 235) : new Color(226, 186, 70);

    // ------------------------------------------------------------------ walk state

    private sealed class Walk
    {
        public Vector2 Last;
        public bool HasLast;
        public long LastTick;
        public float Speed;   // units per second, smoothed
        public float Phase;   // radians, advances while moving
        public Vector2 MoveDir; // last direction it actually travelled, unit length
        public float Facing;    // current heading in radians, turned toward its target a little each frame
        public bool HasFacing;
    }

    private readonly Dictionary<int, Walk> _walks = new();

    // Draw can be called more than once a frame for the same person (a second pass, a reflection);
    // advancing the animation only when real time has passed keeps it from running at double speed.
    private Walk Advance(int actorId, Vector2 worldPosition)
    {
        if (!_walks.TryGetValue(actorId, out var walk))
            _walks[actorId] = walk = new Walk();

        var now = Stopwatch.GetTimestamp();
        var dt = (now - walk.LastTick) / (float)Stopwatch.Frequency;
        if (walk.HasLast && dt < 0.004f)
            return walk;

        if (walk.HasLast)
        {
            dt = Math.Min(dt, 0.1f);
            var moved = Vector2.Distance(walk.Last, worldPosition);
            // A jump of several units is a teleport (docking, respawn), not a very fast walk.
            var speed = moved > 3f ? 0f : moved / dt;
            if (moved is > 0.004f and <= 3f)
                walk.MoveDir = (worldPosition - walk.Last) / moved;
            walk.Speed += (speed - walk.Speed) * 0.2f;
            if (walk.Speed > 0.15f)
                walk.Phase += dt * (6f + 2f * walk.Speed);
        }
        walk.Last = worldPosition;
        walk.HasLast = true;
        walk.LastTick = now;
        return walk;
    }

    // ------------------------------------------------------------------ drawing

    /// <summary>Draws a crewman centred on `center` (screen pixels), turned to face `facing`.</summary>
    /// <param name="armsForward">Holding something: both arms reach out in front instead of swinging.</param>
    public void Draw(SpriteBatch spriteBatch, Vector2 center, float pixelsPerUnit, int actorId, Vector2 worldPosition,
        Color uniform, Color accent, bool suited, Vector2 facing, bool armsForward)
    {
        var walk = Advance(actorId, worldPosition);
        var amp0 = MathHelper.Clamp(walk.Speed / 1.5f, 0f, 1f);
        DrawPosed(spriteBatch, center, pixelsPerUnit, actorId, uniform, accent, suited ? Outfit.Suit : Outfit.Crew, facing, armsForward, MathF.Sin(walk.Phase) * amp0);
    }

    /// <summary>For people the snapshot gives no facing for (enemy crew, station residents): they turn the way
    /// they are walking, and when standing still look toward `lookToward` (usually the nearest player).</summary>
    public void DrawAuto(SpriteBatch spriteBatch, Vector2 center, float pixelsPerUnit, int actorId, Vector2 worldPosition,
        Color uniform, Color accent, Outfit outfit, bool armsForward, Vector2? lookToward = null)
    {
        var walk = Advance(actorId, worldPosition);
        var amp = MathHelper.Clamp(walk.Speed / 1.5f, 0f, 1f);

        float? target = null;
        if (walk.Speed > 0.4f && walk.MoveDir != Vector2.Zero)
            target = MathF.Atan2(walk.MoveDir.Y, walk.MoveDir.X);
        else if (lookToward is { } look && (look - worldPosition).LengthSquared() > 0.04f)
            target = MathF.Atan2(look.Y - worldPosition.Y, look.X - worldPosition.X);

        if (!walk.HasFacing)
        {
            walk.Facing = target ?? MathF.PI / 2f; // idle with nobody to watch: look down the screen
            walk.HasFacing = true;
        }
        else if (target is { } wanted)
        {
            // Turn the short way round, at most ~0.25 rad a frame, so a change of heading is a turn, not a snap.
            var delta = MathF.IEEERemainder(wanted - walk.Facing, MathF.PI * 2f);
            walk.Facing += Math.Clamp(delta, -0.25f, 0.25f);
        }

        DrawPosed(spriteBatch, center, pixelsPerUnit, actorId, uniform, accent, outfit,
            new Vector2(MathF.Cos(walk.Facing), MathF.Sin(walk.Facing)), armsForward, MathF.Sin(walk.Phase) * amp);
    }

    // The figure in an explicit walk pose: swing in [-1, 1] is how far the stride is through (0 standing,
    // +/-1 at the extremes). Draw derives it from movement; the contact sheet calls this directly.
    internal void DrawPosed(SpriteBatch spriteBatch, Vector2 center, float pixelsPerUnit, int actorId,
        Color uniform, Color accent, Outfit outfit, Vector2 facing, bool armsForward, float swing)
    {
        if (facing.LengthSquared() < 1e-6f)
            facing = new Vector2(1f, 0f);
        var angle = MathF.Atan2(facing.Y, facing.X);
        var suited = outfit == Outfit.Suit;
        var armored = outfit is Outfit.Raider or Outfit.Guard;
        var twist = swing * 0.07f;

        var skin = SkinTones[(uint)(actorId * 2654435761u) % SkinTones.Length];
        var hair = HairColors[(uint)(actorId * 40503u + 7u) % HairColors.Length];
        var ctx = new Ctx(this, spriteBatch, center, pixelsPerUnit, angle);

        // Ground shadow, so the figure sits on the floor instead of floating over it.
        ctx.Shape(new Vector2(0.01f, 0.02f), 0.21f, 0.31f, 0f, Color.Black * 0.16f, outline: false, highlight: false);

        // Boots, swinging fore and aft opposite each other. They start a little ahead of the body so
        // the toes always show past the torso.
        ctx.Shape(new Vector2(0.06f + swing * 0.15f, -0.09f), 0.125f, 0.075f, 0f, Boot);
        ctx.Shape(new Vector2(0.06f - swing * 0.15f, 0.09f), 0.125f, 0.075f, 0f, Boot);

        if (suited)
        {
            ctx.Shape(new Vector2(-0.205f, 0f), 0.10f, 0.17f, 0f, SuitPack);
            ctx.Shape(new Vector2(-0.235f, -0.07f), 0.022f, 0.022f, 0f, new Color(96, 232, 168), outline: false, highlight: false);
            ctx.Shape(new Vector2(-0.235f, 0.07f), 0.022f, 0.022f, 0f, accent, outline: false, highlight: false);
        }

        // Torso first, arms over its sides: the arms are a shade darker than the body so they read as
        // separate limbs instead of merging into it.
        var sleeve = suited ? SuitShell : uniform;
        ctx.Shape(Vector2.Zero, 0.15f, 0.235f, twist, sleeve);
        if (!suited)
        {
            // A darker vest panel down the back and the shoulder patches in the accent colour.
            ctx.Shape(new Vector2(-0.04f, 0f), 0.065f, 0.15f, twist, Darken(uniform, 0.3f), outline: false, highlight: false);
            if (armored) // a breastplate over the front and bigger shoulder pads below
                ctx.Shape(new Vector2(0.05f, 0f), 0.07f, 0.16f, twist, Lighten(uniform, 0.22f), highlight: false);
        }
        else
        {
            ctx.Shape(new Vector2(0.06f, 0f), 0.055f, 0.12f, twist, Darken(SuitShell, 0.12f), outline: false, highlight: false);
        }

        var armColor = Darken(sleeve, suited ? 0.08f : 0.16f);
        var hand = suited ? SuitPack : armored ? Boot : skin;
        for (var side = -1; side <= 1; side += 2)
        {
            // Left arm (side -1) swings forward when the right boot does.
            Vector2 armCentre, handAt;
            float armRotation;
            if (armsForward)
            {
                armCentre = new Vector2(0.12f, side * 0.2f);
                armRotation = -side * 0.55f;
                handAt = new Vector2(0.245f, side * 0.115f);
            }
            else
            {
                var reach = -side * swing * 0.09f;
                var shoulder = Rotate(new Vector2(0f, side * 0.265f), twist);
                armCentre = shoulder + new Vector2(reach * 0.5f + 0.02f, 0f);
                armRotation = 0f;
                handAt = shoulder + new Vector2(reach + 0.1f, -side * 0.012f);
            }
            ctx.Shape(armCentre, 0.13f, 0.058f, armRotation, armColor);
            ctx.Shape(handAt, 0.05f, 0.05f, 0f, hand);
        }

        // Shoulder patches sit on top of the arms where they meet the torso.
        for (var side = -1; side <= 1; side += 2)
            ctx.Shape(Rotate(new Vector2(0.005f, side * 0.2f), twist), suited ? 0.045f : armored ? 0.075f : 0.055f, suited ? 0.035f : armored ? 0.062f : 0.045f, twist,
                suited ? uniform : accent, outline: false);

        // Head.
        if (suited)
        {
            ctx.Shape(new Vector2(0.03f, 0f), 0.19f, 0.19f, 0f, SuitShell);
            ctx.Shape(new Vector2(0.115f, 0f), 0.082f, 0.125f, 0f, Visor, highlight: false);
            ctx.Shape(new Vector2(0.135f, -0.055f), 0.03f, 0.022f, -0.5f, VisorGlint, outline: false, highlight: false);
            ctx.Shape(new Vector2(-0.075f, 0f), 0.06f, 0.1f, 0f, Darken(SuitShell, 0.18f), outline: false, highlight: false);
        }
        else if (armored)
        {
            // A closed combat helmet: dark shell, a glowing visor slit across the front (red for raiders,
            // blue for station guards) and a crest ridge so it is not just a ball.
            var visor = outfit == Outfit.Raider ? new Color(255, 70, 50) : new Color(90, 180, 240);
            ctx.Shape(new Vector2(0.03f, 0f), 0.175f, 0.175f, 0f, Darken(uniform, 0.45f));
            ctx.Shape(new Vector2(-0.02f, 0f), 0.11f, 0.03f, 0f, Lighten(uniform, 0.1f), outline: false, highlight: false);
            ctx.Shape(new Vector2(0.11f, 0f), 0.05f, 0.125f, 0f, visor, highlight: false);
            ctx.Shape(new Vector2(0.12f, -0.05f), 0.02f, 0.015f, -0.5f, Lighten(visor, 0.55f), outline: false, highlight: false);
        }
        else
        {
            ctx.Shape(new Vector2(0.03f, 0.152f), 0.036f, 0.036f, 0f, Darken(skin, 0.1f), outline: false, highlight: false);   // ears
            ctx.Shape(new Vector2(0.03f, -0.152f), 0.036f, 0.036f, 0f, Darken(skin, 0.1f), outline: false, highlight: false);
            ctx.Shape(new Vector2(0.04f, 0f), 0.158f, 0.158f, 0f, skin);                                                       // face
            ctx.Shape(new Vector2(-0.005f, 0f), 0.152f, 0.158f, 0f, hair, outline: false);                                    // hair, covering the back of the head
        }
    }

    private static Color Darken(Color c, float t) =>
        new((int)(c.R * (1f - t)), (int)(c.G * (1f - t)), (int)(c.B * (1f - t)), c.A);

    private static Color Lighten(Color c, float t) => new(
        (int)(c.R + (255 - c.R) * t), (int)(c.G + (255 - c.G) * t), (int)(c.B + (255 - c.B) * t), c.A);

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        var cos = MathF.Cos(radians);
        var sin = MathF.Sin(radians);
        return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    // Everything for one figure drawn through the same few numbers: where it stands, how big a unit
    // is, and which way it faces. Local coordinates in, rotated screen sprites out.
    private readonly struct Ctx
    {
        private readonly CrewSkin _skin;
        private readonly SpriteBatch _batch;
        private readonly Vector2 _center;
        private readonly float _ppu;
        private readonly float _angle;
        private readonly float _cos, _sin;

        public Ctx(CrewSkin skin, SpriteBatch batch, Vector2 center, float ppu, float angle)
        {
            _skin = skin;
            _batch = batch;
            _center = center;
            _ppu = ppu;
            _angle = angle;
            _cos = MathF.Cos(angle);
            _sin = MathF.Sin(angle);
        }

        private Vector2 ToScreen(Vector2 local) =>
            _center + new Vector2(local.X * _cos - local.Y * _sin, local.X * _sin + local.Y * _cos) * _ppu;

        /// <summary>One rounded part: a dark rim, the fill, and a soft lit patch toward the top-left of
        /// the screen (the light never rotates with the figure, which is what makes it read as a solid).</summary>
        public void Shape(Vector2 local, float radiusX, float radiusY, float rotation, Color fill,
            bool outline = true, bool highlight = true)
        {
            var at = ToScreen(local);
            var spin = _angle + rotation;
            var px = radiusX * 2f * _ppu / DiscSize;
            var py = radiusY * 2f * _ppu / DiscSize;
            var origin = new Vector2(DiscSize / 2f);

            if (outline)
            {
                const float rim = 1.5f; // pixels
                var rx = px + rim * 2f / DiscSize;
                var ry = py + rim * 2f / DiscSize;
                _batch.Draw(_skin._disc, at, null, OutlineTint, spin, origin, new Vector2(rx, ry), SpriteEffects.None, 0f);
            }

            _batch.Draw(_skin._disc, at, null, fill, spin, origin, new Vector2(px, py), SpriteEffects.None, 0f);

            if (highlight)
            {
                var lift = MathF.Min(radiusX, radiusY) * 0.28f * _ppu;
                _batch.Draw(_skin._disc, at + new Vector2(-lift, -lift), null,
                    Lighten(fill, 0.35f) * 0.55f, spin, origin, new Vector2(px * 0.55f, py * 0.55f), SpriteEffects.None, 0f);
            }
        }
    }

    // A filled disc with a one-pixel soft edge, supersampled so it stays smooth when it is scaled
    // and turned. Premultiplied alpha, matching the default blend state.
    private static Texture2D BakeDisc(GraphicsDevice graphics)
    {
        var data = new Color[DiscSize * DiscSize];
        const int samples = 4;
        var radius = DiscSize / 2f - 0.5f;
        for (var y = 0; y < DiscSize; y++)
        for (var x = 0; x < DiscSize; x++)
        {
            var covered = 0;
            for (var sy = 0; sy < samples; sy++)
            for (var sx = 0; sx < samples; sx++)
            {
                var dx = x + (sx + 0.5f) / samples - DiscSize / 2f;
                var dy = y + (sy + 0.5f) / samples - DiscSize / 2f;
                if (dx * dx + dy * dy <= radius * radius)
                    covered++;
            }
            var a = covered / (float)(samples * samples);
            var v = (byte)Math.Round(a * 255f);
            data[y * DiscSize + x] = new Color(v, v, v, v);
        }
        var texture = new Texture2D(graphics, DiscSize, DiscSize);
        texture.SetData(data);
        return texture;
    }
}
