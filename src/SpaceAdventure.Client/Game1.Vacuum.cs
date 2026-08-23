using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client;

// How the picture changes the moment the player is outside the hull.
//
// Darkness on its own does not press on anyone. A uniformly black screen is dull, not frightening.
// It presses when it takes something away and when there is a lit thing beside it to measure against
// - so what happens out here is not "turn the brightness down", it is three separate moves:
//
//   * the blacks are crushed rather than lifted. In vacuum there is no air to scatter light, so a
//     shadow is not dim, it is nothing. Games that add a little fill "so you can see" are the ones
//     whose space looks like a soundstage.
//   * grain goes up, and only here. A dark room is not silent to the eye - it fizzes, because at low
//     light the retina is running out of signal. A little noise in the black reads as straining to
//     see; a clean black reads as a monitor that is switched off.
//   * the vignette closes in. With the lamp doing the rest, the world shrinks to a patch.
//
// Everything is saved and restored around Present, the same way the menu's own look is, so none of
// it leaks back inside the ship where a lit corridor should still look like a lit corridor.
public partial class Game1
{
    // A helmet lamp is a point source: brightness falls away from the first metre, it does not hold
    // and then stop. 0.06 means the fade covers essentially the whole reach.
    private const float VacuumLampFalloffStart = 0.06f;

    // The beam, outside. Narrow and long rather than wide and short - the reference this is chasing
    // throws something like twenty-five degrees, and a hundred and ten is not a torch, it is a room
    // light strapped to a helmet. Narrow also means it has to reach further, or there is nothing at
    // the end of it worth pointing at.
    private const float VacuumLampHalfAngleDegrees = 17f;
    private const float VacuumLampRadius = 14f;

    // And a small pool around the wearer that the beam has nothing to do with.
    //
    // This is a correction: the first pass took the suit's own close-range glow away outside on the
    // grounds that nothing beyond the lamp should be readable. That went too far. In the reference
    // the railing, the door and the deck plates are all there in the dark - dim to the point of
    // being almost guessed at, but there - and that is what makes the darkness feel like a place
    // rather than a void with a torch in it. What should stay invisible is the distance, not the
    // handhold you are gripping.
    // Close enough to be the handhold you are gripping and nothing beyond it. At 2.6 this read as a
    // lit circle following the player around, which is a different thing from being able to make out
    // what you are touching.
    private const float VacuumHaloRadius = 1.15f;

    // Warm lamp, cold dark. This is the strongest thing left between what we have and the reference,
    // and it is not about brightness at all: there, everything the torch finds comes back warm while
    // the water stays green-blue, and the two readings of the same wall are what make the beam feel
    // like a beam. A neutral lamp on a neutral dark just makes some of it lighter.
    //
    // The cold half is already in place - the mask floor is a blue grey - so this is the other end
    // of the same idea.
    private static readonly Vector3 VacuumLampTint = new(1.0f, 0.90f, 0.75f);

    // What the light mask holds where the lamp does not reach. Not black: the mask multiplies the
    // whole scene, and at zero the starfield goes with everything else - which loses both the depth
    // of the shot and the one image worth having out here, the hull as a hole in the stars. At this
    // level a star still shows and mid-grey plating does not.
    //
    // Raised after the first try: at (16, 18, 24) the floor was there, but the vacuum grade below -
    // exposure down, white point low - crushed it back out again, so the stars survived inside the
    // beam and nowhere else. Two changes of mine pulling against each other. Plating is around a
    // fifth of a star's brightness, so this level still swallows the hull.
    private static readonly Color VacuumMaskFloor = new(38, 42, 52);

    // How much of each side of the beam is spent fading out. Without it the cone ends on two straight
    // lines meeting at a point, which is what made it read as a grey shape laid over the screen
    // rather than as light - the single biggest thing wrong with the first two attempts.
    private const float VacuumLampEdgeFade = 0.55f;

    private readonly record struct PostLook(
        float Exposure, float TonemapWhite, float Vignette, float Grain, float Grade, float BloomThreshold);

    private PostLook CapturePostLook() => new(
        _scenePost.Exposure, _scenePost.TonemapWhite, _scenePost.Vignette,
        _scenePost.GrainAmount, _scenePost.GradeStrength, _scenePost.BloomThreshold);

    private void RestorePostLook(PostLook saved)
    {
        _scenePost.Exposure = saved.Exposure;
        _scenePost.TonemapWhite = saved.TonemapWhite;
        _scenePost.Vignette = saved.Vignette;
        _scenePost.GrainAmount = saved.Grain;
        _scenePost.GradeStrength = saved.Grade;
        _scenePost.BloomThreshold = saved.BloomThreshold;
    }

    /// <summary>The vacuum look, applied for a frame drawn from outside the hull.</summary>
    private PostLook ApplyVacuumPostLook()
    {
        var saved = CapturePostLook();

        // Down, not up. The lamp is the only real light out here and everything it misses should
        // fall off a cliff rather than settle into a readable grey.
        _scenePost.Exposure *= 0.72f;
        // A low white point compresses hard, which is what pushes the midtones out of the picture:
        // outside there should be almost nothing between "lit by the lamp" and "gone".
        _scenePost.TonemapWhite = 1.9f;
        // The few things that are lit are the only things worth blooming, so the threshold drops to
        // catch them - a lamp on plating, a running light, a weld.
        _scenePost.BloomThreshold = 0.42f;
        _scenePost.Vignette = 0.34f;
        _scenePost.GrainAmount = 0.055f;
        return saved;
    }

    // The helmet lamp, drawn into the scene rather than only into the sight mask.
    //
    // The mask already decides what is visible; this is what makes the lamp read as a lamp. Two
    // parts, and the second is the one that matters: a cone of light on whatever it lands on, and a
    // faint wedge of the beam itself. Without the wedge a suit lamp is just a region of the world
    // that happens to be brighter, which is a lighting model, not an object.
    //
    // The beam is very weak on purpose. In vacuum there is nothing for a beam to scatter off - it
    // should be a hint that the lamp is pointing somewhere, not a searchlight in fog.
    // One pass of the beam. spread scales the cone angle, so the core is a narrower, brighter version
    // of the same shape rather than a second unrelated light.
    private void DrawBeam(Texture2D soft, Vector2 softOrigin, Vector2 head, Vector2 aim, float rotation,
        float reach, float spread, float strength)
    {
        const int steps = 14;
        var slice = reach / steps * 2.2f;
        var tan = MathF.Tan(MathHelper.ToRadians(VacuumLampHalfAngleDegrees)) * spread;
        for (var i = 0; i < steps; i++)
        {
            var t = i / (float)steps;
            var pos = head + aim * (reach * t);
            var halfWidth = ShipRenderer.PixelsPerUnit * (0.22f + t * VacuumLampRadius * tan);
            var fade = MathF.Pow(1f - t, 2.0f) * strength;
            _spriteBatch.Draw(soft, pos, null, LampColour * fade, rotation, softOrigin,
                new Vector2(slice / soft.Width, halfWidth * 2f / soft.Height), SpriteEffects.None, 0f);
        }
    }

    // The same warmth the mask is tinted with, so the shaft of light and the surfaces it lands on are
    // lit by one lamp rather than by two.
    private static readonly Color LampColour = new(255, 230, 196);

    private float _lampSeconds;

    // Cold gas, deliberately: the lamp is warm and this is not, so a burst of it reads as something
    // the suit did rather than as more lamplight. Real manoeuvring thrusters vent nitrogen, which is
    // colourless - what you would actually see is the frost that flashes out of it.
    private static readonly Color RcsColour = new(198, 222, 255);

    // What the gas looks like the instant it leaves the nozzle, before it has spread and cooled.
    // Nearly white and well over the vacuum bloom threshold on purpose: outside, the post chain is
    // crushing everything it can, and a plume that sits under the threshold gets flattened into the
    // dark with the rest of it. Being bright enough to bloom is what makes it read at all.
    private static readonly Color RcsHotColour = new(244, 250, 255);

    private readonly List<(Vector2 Position, Vector2 Velocity, float Age, float Life, float Size)> _rcsPuffs = new();

    // The last direction the pack was pushed, and how long ago. The raw input vector is cleared at
    // the top of every Update and only refilled while the outside branch runs, so reading it
    // directly means any frame that misses leaves the thruster off - and a jet that stutters out on
    // single frames is one you never see. Latching it for a moment costs nothing and makes the
    // plume continuous while a key is held.
    // Half of ShipRenderer.CharacterDiameter, which is private to it: the suit's outer shell, and
    // where the thruster ports live.
    private const float CharacterRadiusUnits = 0.35f;

    private Vector2 _rcsLastPush;
    private float _rcsPushAge = 99f;
    // Up to two ports fire together, so both the port list and their emission budgets are per-port.
    private readonly Vector2[] _rcsPorts = new Vector2[2];
    private readonly float[] _rcsEmitCarry = new float[2];
    private readonly Random _rcsRandom = new();

    // Exhaust from the manoeuvring pack, in the ship's own frame.
    //
    // Fired opposite the way the player is pushing, because that is what a thruster does and because
    // it is the only cue out here that says which way you are about to go. Without a suit there is no
    // pack at all (World.Eva.cs gates thrust on the suit being sealed), and an attached character is
    // walking on magnets rather than flying, so neither gets one.
    private void DrawRcsPlume(WorldSnapshot snapshot, CharacterState me, Vector2 origin, Matrix sceneTransform, float deltaSeconds)
    {
        if (!me.IsOutside)
        {
            _rcsPuffs.Clear();
            return;
        }

        if (MathF.Abs(_evaThrustLocal.X) > 0.01f || MathF.Abs(_evaThrustLocal.Y) > 0.01f)
        {
            _rcsLastPush = Vector2.Normalize(new Vector2(_evaThrustLocal.X, _evaThrustLocal.Y));
            _rcsPushAge = 0f;
        }
        else
        {
            _rcsPushAge += deltaSeconds;
        }

        var thrusting = me.WearingSuit && !me.IsEvaAttached && me.JetpackFuel > 0f && _rcsPushAge < 0.15f;

        var body = BodyPixels(snapshot, me, origin);

        // A suit has four fixed thruster ports, not a steerable nozzle, so the push is resolved onto
        // the two axes instead of being aimed freely. Straight along an axis that is one port; on a
        // diagonal it is two, which is simply how you move diagonally with jets that cannot turn.
        //
        // Both fire at full. A cold-gas port is a valve - open or shut - so throttling each to its
        // share of a diagonal would be inventing hardware that is not in the suit, and it would make
        // exactly the manoeuvre that uses two thrusters look weaker than the one that uses one.
        var away = -_rcsLastPush;
        var portCount = 0;
        if (MathF.Abs(away.X) > 0.05f)
            _rcsPorts[portCount++] = new Vector2(MathF.Sign(away.X), 0f);
        if (MathF.Abs(away.Y) > 0.05f)
            _rcsPorts[portCount++] = new Vector2(0f, MathF.Sign(away.Y));

        // The vent sits on the shell, not out in the vacuum beside it. The body is 0.7 units across,
        // so this puts it just on the surface - the gap this used to have was more than twice the
        // character's own radius, which is what made the jet look like it belonged to nobody.
        Vector2 Vent(Vector2 direction) =>
            body + direction * (ShipRenderer.PixelsPerUnit * (CharacterRadiusUnits - 0.02f));

        if (thrusting)
        {
            for (var pi = 0; pi < portCount; pi++)
            {
                var out_ = _rcsPorts[pi];
                var vent = Vent(out_);
                var side = new Vector2(-out_.Y, out_.X);

                // Carried between frames rather than one puff per frame: at sixty frames a second
                // that would tie the density of the exhaust to the frame rate. One accumulator per
                // port, or the two would share a budget and each come out at half strength.
                _rcsEmitCarry[pi] += deltaSeconds * 58f;
                while (_rcsEmitCarry[pi] >= 1f)
                {
                    _rcsEmitCarry[pi] -= 1f;
                    var spread = (float)(_rcsRandom.NextDouble() - 0.5) * 0.55f;
                    var speed = ShipRenderer.PixelsPerUnit * (1.8f + (float)_rcsRandom.NextDouble() * 1.6f);
                    var direction = Vector2.Normalize(out_ + side * spread);
                    _rcsPuffs.Add((
                        vent,
                        direction * speed,
                        0f,
                        0.24f + (float)_rcsRandom.NextDouble() * 0.24f,
                        ShipRenderer.PixelsPerUnit * (0.095f + (float)_rcsRandom.NextDouble() * 0.085f)));
                }
            }
        }

        for (var i = _rcsPuffs.Count - 1; i >= 0; i--)
        {
            var p = _rcsPuffs[i];
            var age = p.Age + deltaSeconds;
            if (age >= p.Life)
            {
                _rcsPuffs.RemoveAt(i);
                continue;
            }
            // Vented gas keeps going - there is nothing out here to slow it down. It thins out
            // instead, which is why the puffs grow as they fade rather than stopping.
            _rcsPuffs[i] = (p.Position + p.Velocity * deltaSeconds, p.Velocity, age, p.Life, p.Size);
        }

        if (_rcsPuffs.Count == 0 && !thrusting)
            return;

        var soft = _scenePost.Blob;
        var softOrigin = new Vector2(soft.Width * 0.5f, soft.Height * 0.5f);
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            transformMatrix: sceneTransform);
        // The root of the jet, while it is firing. Particles alone give a cloud that drifts away from
        // a character with nothing happening at the character - the flash at the nozzle is the part
        // that says the gas is being pushed out rather than merely floating there.
        if (thrusting)
        {
            for (var pi = 0; pi < portCount; pi++)
            {
                var out_ = _rcsPorts[pi];
                var vent = Vent(out_);
                var rotation = MathF.Atan2(out_.Y, out_.X);
                // Rolled per port, so two firing at once do not flicker in lockstep like one light
                // split in two.
                var flicker = 0.82f + (float)_rcsRandom.NextDouble() * 0.36f;
                for (var i = 0; i < 4; i++)
                {
                    var t = i / 4f;
                    var pos = vent + out_ * (ShipRenderer.PixelsPerUnit * (0.07f + t * 0.62f));
                    var half = ShipRenderer.PixelsPerUnit * (0.06f + t * 0.15f);
                    _spriteBatch.Draw(soft, pos, null,
                        Color.Lerp(RcsHotColour, RcsColour, t) * ((1f - t) * 0.45f * flicker), rotation, softOrigin,
                        new Vector2(ShipRenderer.PixelsPerUnit * 0.22f / soft.Width, half * 2f / soft.Height),
                        SpriteEffects.None, 0f);
                }
            }
        }

        foreach (var p in _rcsPuffs)
        {
            var t = p.Age / p.Life;
            var size = p.Size * (1f + t * 1.7f);
            // Drawn additively onto a picture that is nearly black, so the eye reads this brighter
            // than the number suggests. 0.95 was a floodlight welded to the character's hip and 0.30
            // was too shy to notice mid-manoeuvre; this sits between them. The slower falloff matters
            // as much as the level - it is what keeps the tail of the plume on screen long enough to
            // be seen rather than dying a few pixels out.
            var fade = MathF.Pow(1f - t, 1.25f) * 0.55f;
            // Hot and white where it leaves, cold and blue once it has spread. A plume of one colour
            // reads as a puff of smoke; the shift is what says the gas came out of something.
            var colour = Color.Lerp(RcsHotColour, RcsColour, MathF.Min(1f, t * 1.8f));
            _spriteBatch.Draw(soft, p.Position, null, colour * fade, 0f, softOrigin,
                new Vector2(size * 2f / soft.Width, size * 2f / soft.Height), SpriteEffects.None, 0f);
        }
        _spriteBatch.End();
    }

    // Where the wearer is, in the frame the scene is actually drawn in.
    //
    // Inside the hull a character X/Y already are ship-local, so this changes nothing there. Outside
    // they are field coordinates - absolute, and on a ship out in a sector they run to thousands -
    // and multiplying those by PixelsPerUnit put the exhaust some eighty screens past the right-hand
    // edge. It was never too dim; it was never in the picture. The camera and the sight mask have
    // always done this conversion, and these two draws were the only things that skipped it.
    private static Vector2 BodyPixels(WorldSnapshot snapshot, CharacterState me, Vector2 origin)
    {
        var here = new Vec2(me.X, me.Y);
        var local = me.IsOutside
            ? ShipLocalFrame.ToLocal(here, snapshot.ShipField, ShipLocalFrame.GetHullCenter(snapshot.Rooms))
            : here;
        return origin + new Vector2(local.X, local.Y) * ShipRenderer.PixelsPerUnit;
    }

    private void DrawSuitLamp(WorldSnapshot snapshot, CharacterState me, Vector2 origin, Matrix sceneTransform)
    {
        if (!me.IsOutside || !me.WearingSuit)
            return;

        _lampSeconds += 1f / 60f;
        var soft = _scenePost.Blob;
        var softOrigin = new Vector2(soft.Width * 0.5f, soft.Height * 0.5f);

        // Facing arrives in the frame the character moves in - field coordinates while outside -
        // and has to come back into the ship's frame, exactly like the sight mask's own eye does.
        var facing = ShipLocalFrame.ToLocalDirection(
            new Vec2(me.FacingX, me.FacingY), snapshot.ShipField.RotationDegrees);
        var aim = new Vector2(facing.X, facing.Y);
        if (aim.LengthSquared() < 0.0001f)
            return;
        aim.Normalize();

        var head = BodyPixels(snapshot, me, origin);
        var rotation = MathF.Atan2(aim.Y, aim.X);
        var reach = VacuumLampRadius * ShipRenderer.PixelsPerUnit;

        // The scene transform, not the plain render scale: these positions are world units folded
        // through the same camera the ship was drawn with, and under the HUD matrix the beam would
        // land somewhere else at the wrong size.
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
            transformMatrix: sceneTransform);

        // Real lamps are not perfectly steady, and a beam that is reads as a rendered shape. Two
        // unrelated rates, both tiny - this should never be noticed as flicker, only as the light
        // being alive.
        var alive = 1f
            + MathF.Sin(_lampSeconds * 7.3f) * 0.035f
            + MathF.Sin(_lampSeconds * 19.7f) * 0.02f;

        // The beam: slices across the flow, widening with distance and fading fast. Drawn twice - a
        // wide spill and a narrow hot core inside it. A torch has a hotspot; a single even wedge is
        // a shape, and no amount of tuning its brightness turns it into light.
        DrawBeam(soft, softOrigin, head, aim, rotation, reach, 1.0f, 0.14f * alive);
        DrawBeam(soft, softOrigin, head, aim, rotation, reach * 0.82f, 0.42f, 0.16f * alive);
        // The wearer, lit by their own lamp. Nothing else out here will light them, and a figure
        // that stays a silhouette while everything they point at is lit reads as a camera effect
        // rather than as a person carrying a torch.
        _spriteBatch.Draw(soft, head, null, new Color(150, 172, 200) * 0.30f, 0f, softOrigin,
            new Vector2(ShipRenderer.PixelsPerUnit * 1.0f / soft.Width,
                ShipRenderer.PixelsPerUnit * 1.0f / soft.Height), SpriteEffects.None, 0f);

        // The housing itself, so the lamp has a source you can point to.
        _spriteBatch.Draw(soft, head + aim * (ShipRenderer.PixelsPerUnit * 0.35f), null,
            LampColour * 0.55f, 0f, softOrigin,
            new Vector2(ShipRenderer.PixelsPerUnit * 0.5f / soft.Width, ShipRenderer.PixelsPerUnit * 0.5f / soft.Height),
            SpriteEffects.None, 0f);
        _spriteBatch.End();
    }
}
