using System.Linq;
using Microsoft.Xna.Framework;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client;

// The scene camera's own math: where it's anchored (a walking character, a manned turret's live
// aim, or a docked station's simpler frame), the Barotrauma-style cursor lookahead that eases it
// off that anchor, and the zoom/rotation a manned turret applies to the whole scene batch. Split
// out of Game1.cs itself - Update/Draw call into this every frame but own no state of their own
// here beyond the fields declared below.
public partial class Game1
{
    private const float PeriscopeViewLead = 6f;
    // Half scale = twice the reach. A gunner has to see a raider holding station 22 units out and
    // the shell crossing the gap to it; at the interior's own scale that all happens off-screen.
    private const float TurretViewZoom = 0.5f;

    // Barotrauma-style cursor lookahead: the camera doesn't center strictly on the character while
    // walking around - it eases partway toward wherever the mouse is pointing, so you see a bit
    // more of what's ahead/around a corner without losing sight of yourself. Fraction of the
    // distance to the cursor, clamped to a max offset so flinging the mouse to a screen edge
    // doesn't pull the camera arbitrarily far away; smoothed over time so a sudden mouse jump pans
    // there rather than snapping.
    private const float CameraLookAheadFactor = 0.25f;
    private const float CameraLookAheadMaxDistance = 3.5f; // ship-local units
    private const float CameraLookAheadSmoothingPerSecond = 8f;
    // Manning a turret exaggerates the same effect (bigger factor, further cap): the periscope
    // view is already zoomed out (TurretViewZoom) to show more of the field, so the cursor panning
    // it further toward whatever's at the edge of that field reads as looking where you're about
    // to shoot, the way swinging a real periscope would.
    private const float TurretLookAheadFactor = 0.5f;
    private const float TurretLookAheadMaxDistance = 10f;
    private Vec2 _cameraLookOffset = Vec2.Zero;

    // Applied to the whole scene batch, so one number moves the camera, the world and the hit
    // tests together instead of each renderer growing a scale parameter.
    private float SceneZoom(WorldSnapshot snapshot) =>
        MannedTurret(snapshot) is not null && _openBlock.Kind is not BlockKind.Navigation && !_infoPanelOpen
            ? TurretViewZoom
            : 1f;

    private (Turret Turret, TurretState State)? MannedTurret(WorldSnapshot snapshot)
    {
        var state = snapshot.TurretStates.FirstOrDefault(t => t.MannedByPlayerId == _client.PlayerId);
        if (state is null)
            return null;
        var turret = snapshot.Turrets.FirstOrDefault(t => t.Id == state.Id);
        return turret is null ? null : (turret, state);
    }

    // Degrees to spin the whole scene batch by while manning a turret, so the barrel's own live
    // facing (TurretMount.FireDegrees(AimDegrees), ship-local - outward normal plus however far
    // it's currently traversed) reads as screen-up. This is what makes the view a real gun-cam:
    // swinging the turret pans the whole scene the way looking down a swiveling barrel would,
    // rather than the view staying pinned to the mount's fixed outward side. 0 everywhere else -
    // the ship interior/field view is never rotated except behind a periscope.
    private float TurretViewRotationDegrees(WorldSnapshot snapshot)
    {
        if (MannedTurret(snapshot) is not { } manned || _openBlock.Kind is BlockKind.Navigation || _infoPanelOpen)
            return 0f;
        var mount = TurretMount.For(snapshot.Rooms, snapshot.Turrets, manned.Turret);
        return -90f - mount.FireDegrees(manned.State.AimDegrees);
    }

    private (Vector2 Origin, Vec2 HullCenter, Vec2 Anchor) ComputeCamera(WorldSnapshot snapshot, CharacterState me)
    {
        var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
        Vec2 anchorLocal;
        if (MannedTurret(snapshot) is { } manned)
        {
            var mount = TurretMount.For(snapshot.Rooms, snapshot.Turrets, manned.Turret);
            // Along the live aim direction, not the mount's fixed outward normal - the camera
            // sits out past the muzzle looking whichever way the barrel is actually pointed right
            // now, the same "camera near the barrel" TurretViewRotationDegrees rotates the view to
            // match.
            anchorLocal = mount.Position + mount.FireDirection(manned.State.AimDegrees) * PeriscopeViewLead;
        }
        else
        {
            anchorLocal = me.IsOutside
                ? ShipLocalFrame.ToLocal(new Vec2(me.X, me.Y), snapshot.ShipField, hullCenter)
                : new Vec2(me.X, me.Y);
        }
        // _cameraLookOffset (Barotrauma-style cursor pan) only ever shifts where the camera itself
        // centers on screen - never the returned Anchor, which BuildVisibilityMask uses as the
        // sight cone's true apex. Baking the pan into Anchor too would drag the cone off of the
        // character's real position the moment the camera panned away from them.
        var cameraAnchor = anchorLocal + _cameraLookOffset;
        // Divided by the zoom because the scene batch scales everything drawn at this origin: the
        // anchor has to land on the middle of the screen *after* that scaling, not before it.
        var screenCenter = (WorldViewportOrigin + WorldViewportSize / 2f) / SceneZoom(snapshot);
        var origin = screenCenter - new Vector2(cameraAnchor.X, cameraAnchor.Y) * ShipRenderer.PixelsPerUnit;
        return (origin, hullCenter, anchorLocal);
    }

    // Recomputes the target lookahead from this frame's fresh mouse/snapshot and eases the smoothed
    // offset toward it - called once per Update (not from inside ComputeCamera itself, which runs
    // several times a frame for hit-testing and would otherwise re-blend that many times over).
    private void UpdateCameraLookOffset(WorldSnapshot? snapshot, float deltaSeconds)
    {
        var me = snapshot?.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        Vec2 target;
        if (snapshot is null || me is null || me.IsAtHelm || me.OnStation || me.OnEnemyShip ||
            _openBlock.Kind == BlockKind.Navigation || _infoPanelOpen)
        {
            target = Vec2.Zero; // eases back to centered rather than freezing wherever it was
        }
        else if (MannedTurret(snapshot) is { } manned)
        {
            var mount = TurretMount.For(snapshot.Rooms, snapshot.Turrets, manned.Turret);
            var baseAnchor = mount.Position + mount.FireDirection(manned.State.AimDegrees) * PeriscopeViewLead;
            target = CursorLookAheadFrom(snapshot, baseAnchor, TurretLookAheadFactor, TurretLookAheadMaxDistance);
        }
        else
        {
            var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
            var baseAnchor = me.IsOutside
                ? ShipLocalFrame.ToLocal(new Vec2(me.X, me.Y), snapshot.ShipField, hullCenter)
                : new Vec2(me.X, me.Y);
            target = CursorLookAheadFrom(snapshot, baseAnchor, CameraLookAheadFactor, CameraLookAheadMaxDistance);
        }

        var blend = MathHelper.Clamp(deltaSeconds * CameraLookAheadSmoothingPerSecond, 0f, 1f);
        _cameraLookOffset += (target - _cameraLookOffset) * blend;
    }

    // Where the cursor sits relative to baseAnchor, converted out of screen space through that
    // anchor's own (not-yet-offset) camera - so the same formula works whether baseAnchor is a
    // walking character or a turret's barrel-lead point, each with its own zoom/scale already
    // folded in via SceneZoom.
    private Vec2 CursorLookAheadFrom(WorldSnapshot snapshot, Vec2 baseAnchor, float factor, float maxDistance)
    {
        var zoom = SceneZoom(snapshot);
        var screenCenter = (WorldViewportOrigin + WorldViewportSize / 2f) / zoom;
        var baseOrigin = screenCenter - new Vector2(baseAnchor.X, baseAnchor.Y) * ShipRenderer.PixelsPerUnit;
        var mouseLocal = (new Vector2(_designMouse.X, _designMouse.Y) / zoom - baseOrigin) / ShipRenderer.PixelsPerUnit;

        var toCursor = new Vec2(mouseLocal.X, mouseLocal.Y) - baseAnchor;
        var lookAhead = toCursor * factor;
        var length = lookAhead.Length();
        return length > maxDistance ? lookAhead * (maxDistance / length) : lookAhead;
    }

    // The station never moves or rotates (Station.cs), so its own camera is simpler than the
    // ship's: the station's room-local coordinates already are the following camera's frame,
    // no ShipLocalFrame folding needed.
    private static Vector2 ComputeStationCamera(CharacterState me)
    {
        var screenCenter = WorldViewportOrigin + WorldViewportSize / 2f;
        return screenCenter - new Vector2(me.X, me.Y) * ShipRenderer.PixelsPerUnit;
    }
}
