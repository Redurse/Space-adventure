using System;
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
            : ShipBuildOverviewActive(snapshot)
                ? ShipOverviewZoom(snapshot)
                : 1f;

    // Content-каталог отсеков - "видно весь корабль" build screen: talking to the Shipwright pulls
    // the whole scene back far enough to fit the entire hull on screen at once (plus a margin for
    // pointing at empty space just outside it, where a new compartment would actually attach), the
    // same "one number moves camera+world+hit-tests together" SceneZoom trick TurretViewZoom already
    // uses - so StationBuildPanel's placement overlay, HandleMouseClick's confirm-click, and the
    // ordinary room/device rendering all agree on the same view with no separate code path.
    private const float ShipOverviewMarginUnits = 15f; // ~5 tiles of empty space around the hull to build into
    public bool ShipBuildOverviewActive(WorldSnapshot snapshot) =>
        snapshot.Station.Npcs.FirstOrDefault(n => n.Id == _talkingToNpcId)?.Kind == NpcKind.Shipwright;

    private float ShipOverviewZoom(WorldSnapshot snapshot)
    {
        var half = ShipLocalFrame.GetHullHalfExtents(snapshot.Rooms);
        var width = (float)half.X * 2f + ShipOverviewMarginUnits * 2f;
        var height = (float)half.Y * 2f + ShipOverviewMarginUnits * 2f;
        var fitX = WorldViewportSize.X / (width * ShipRenderer.PixelsPerUnit);
        var fitY = WorldViewportSize.Y / (height * ShipRenderer.PixelsPerUnit);
        // _shipOverviewZoomMultiplier (Game1.cs's own scroll-wheel handling) rides on top of this
        // auto-fit baseline rather than replacing it, so scrolling in/out never has to fight the
        // "start centred on the whole hull" default the moment the dialogue opens.
        return MathF.Min(fitX, fitY) * _shipOverviewZoomMultiplier;
    }

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

    // M55 - "чтобы при близости к поверхности планеты камера поворачивалась вертикально...
    // чтобы было проще садиться": the same whole-scene rotation trick TurretViewRotationDegrees
    // uses, blended in as the ship nears a landable body's surface so "away from that body" reads
    // as screen-up - a landing approach aid, not physics (FieldRenderer already regenerates body
    // positions from the same pure functions World.Gravity.cs computes gravity from, so this needs
    // no protocol field of its own). Only while still actually flying - once landed the surface's
    // own fixed camera (ComputeStationCamera-style) takes over and this never runs.
    private const float LandingApproachBlendRadii = 4f;

    private float LandingApproachRotationDegrees(WorldSnapshot snapshot)
    {
        // M59 follow-up - "корабль относительно персонажа не расположен прямо" while docked: bodies
        // got rescaled back down to Cosmoteer size (small planets/moons, M59), so a station riding
        // close to its own host planet can now sit well within this blend's own radius - a docked,
        // perfectly stationary ship has no business tilting the whole scene as if it were on final
        // approach to landing. LandedBodyId already guards the "already landed" case; docked needs
        // the exact same early-out.
        if (snapshot.Voyage.LandedBodyId is not null || snapshot.Voyage.DockedPointId is not null)
            return 0f;
        var shipPosition = new Vec2(snapshot.ShipField.X, snapshot.ShipField.Y);
        if (_fieldRenderer.NearestLandableBodyApproach(snapshot, shipPosition) is not { } approach)
            return 0f;

        // M59 follow-up - "после отстыковки камера странно повернулась": the DockedPointId guard
        // above only covers the instant still sitting AT the berth - a station can now sit as close
        // as ~1.4 body-radii above its own host planet's surface (GalaxyMap.cs's own
        // StationHostOffsetClearanceFraction(0.8) against CelestialBodyGenerator.ClearanceRadius,
        // Radius*3), comfortably inside this blend's own multi-radius start distance at the new
        // Cosmoteer scale - so the ship kept tilting the instant it left the berth, still sitting
        // right next to that same station. This is a landing-approach aid, not a "near any planet"
        // one: it should only ever engage while the body's own surface is genuinely the closer thing
        // to head for than the station just left behind (or any other station in range) - never while
        // still effectively in a station's own docking neighbourhood.
        var nearestStationDistance = snapshot.GalaxyPoints
            .Where(p => p.Kind == GalaxyPointKind.Station)
            .Select(p => (p.Position - shipPosition).Length())
            .DefaultIfEmpty(double.MaxValue)
            .Min();
        if (approach.SurfaceDistance >= nearestStationDistance)
            return 0f;

        // Starts blending in a few body-radii out, fully aligned by the time the surface is
        // actually reached. M59 follow-up: bodies are Cosmoteer-scale now (small radii), not the
        // huge KSP-scale ones this comment used to assume - "a few body-radii out" is correspondingly
        // much closer in absolute terms, so this only ever engages on a real, close approach to a
        // SMALL body, same intent as before just at the new scale.
        var blendDistance = approach.BodyRadius * LandingApproachBlendRadii;
        var blend = MathHelper.Clamp(1f - approach.SurfaceDistance / blendDistance, 0f, 1f);
        if (blend <= 0f)
            return 0f;

        var localAway = ShipLocalFrame.ToLocalDirection(approach.AwayFromBody, snapshot.ShipField.RotationDegrees);
        var localAngleDegrees = MathF.Atan2((float)localAway.Y, (float)localAway.X) * (180f / MathF.PI);
        var targetRotation = -90f - localAngleDegrees;
        return targetRotation * blend;
    }

    private (Vector2 Origin, Vec2 HullCenter, Vec2 Anchor) ComputeCamera(WorldSnapshot snapshot, CharacterState me)
    {
        var hullCenter = ShipLocalFrame.GetHullCenter(snapshot.Rooms);
        Vec2 anchorLocal;
        if (ShipBuildOverviewActive(snapshot))
        {
            // Content-каталог отсеков - centered on the hull itself rather than the character, who
            // is off talking to the Shipwright somewhere on the station and isn't the point of this
            // view. ShipOverviewZoom above already shrank the whole scene to fit the hull - anchoring
            // on anything else would immediately scroll it back out of frame. _shipOverviewPanOffset
            // (Game1.cs's own right-drag handling) is already stored in these same ship-local units
            // (divided by SceneZoom at drag time, not screen pixels), so it just adds straight onto
            // the anchor like any other offset.
            anchorLocal = hullCenter + new Vec2(_shipOverviewPanOffset.X, _shipOverviewPanOffset.Y);
        }
        else if (MannedTurret(snapshot) is { } manned)
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
        var origin = screenCenter - new Vector2((float)cameraAnchor.X, (float)cameraAnchor.Y) * ShipRenderer.PixelsPerUnit;
        return (origin, hullCenter, anchorLocal);
    }

    // Inverse of the forward mapping ReadTurretAimTowardCursor already relies on (mountOnScreen =
    // (origin + local*PixelsPerUnit) * SceneZoom) - the scene batch scales the WHOLE drawn position
    // by zoom, origin included, so recovering a ship-local point from a raw design-space cursor
    // position has to divide the cursor by that same zoom before subtracting origin, not after.
    // Every zoom-1 caller (walking the ship interior, laying wire) never noticed the difference -
    // dividing by 1 is a no-op - which is exactly why the whole-ship overview's own placement click
    // (the first real zoom<1 caller of this inverse) landed nowhere near the highlighted tile: the
    // old inline formula skipped this division and used ONLY the design mouse and origin as-is.
    private static Vec2 ScreenToShipLocal(Vector2 designPos, Vector2 origin, float zoom) =>
        new((designPos.X / zoom - origin.X) / ShipRenderer.PixelsPerUnit, (designPos.Y / zoom - origin.Y) / ShipRenderer.PixelsPerUnit);

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
        var baseOrigin = screenCenter - new Vector2((float)baseAnchor.X, (float)baseAnchor.Y) * ShipRenderer.PixelsPerUnit;
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
        return screenCenter - new Vector2((float)me.X, (float)me.Y) * ShipRenderer.PixelsPerUnit;
    }
}
