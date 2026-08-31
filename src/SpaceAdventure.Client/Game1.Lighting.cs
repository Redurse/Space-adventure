using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SpaceAdventure.Client.Rendering;
using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Client;

// The character's own sight-cone/room-lighting mask (BuildVisibilityMask) and the power-driven
// "mood" that feeds it: how bright the ship's lamps read at the current power level, and one
// PointLight per room for the ship/station/boarded-enemy-hull cases. Split out of Game1.cs itself -
// BuildVisibilityMask is called once per frame from Draw, but owns no state beyond _roomLightingReady
// (declared in Game1.cs, alongside _visibility/_roomLighting themselves).
public partial class Game1
{
    // Line of sight for whichever physical space the player is standing in. The occluders are that
    // space's own walls with its currently-open doorways cut out, so sight carries through an open
    // door into the next compartment and stops dead at everything else. A suit helmet keeps the
    // narrow forward cone it always had (game_design.md section 2); unsuited the light is all-round
    // but still bounded, so a corridor reads as a corridor rather than the whole deck plan.
    // Returns false for the views that replace the scene entirely (wiring/helm/info) - nothing to
    // mask there. Also builds the room-lighting mask (_roomLightingReady) alongside the sight mask,
    // over the same walls/origin - the two share every input except what they do with it.
    // Navigation used to be grouped in here too, back when it also replaced the scene - now that
    // it's a HUD overlay on top of the still-rendered real scene (Game1.cs's own BlockKind.Navigation
    // case), exempting it left the whole ship fully lit and visible regardless of the character's
    // actual sight cone while the console was open (M48 follow-up bug report - "видит места которые
    // не должен видеть из-за системы видимых зон персонажа") - the console operator sees exactly as
    // far as their own eyes/lamp would normally reach, same as standing at any other terminal.
    private bool BuildVisibilityMask(WorldSnapshot snapshot, float totalSeconds)
    {
        _roomLightingReady = false;
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        // Info takes over the viewport the same way the helm does - a HUD screen, not something
        // seen through the character's own eyes, so it reads the same regardless of where they're
        // standing or how dark the room is (matches IsAtHelm above).
        if (me is null || me.IsAtHelm || _infoPanelOpen)
            return false;

        // A gunner at a periscope is looking through the hull, not standing in a dark corridor:
        // sight goes wide open the moment they man a turret, same as the helm above, because you
        // cannot aim at something you cannot see.
        if (snapshot.TurretStates.Any(t => t.MannedByPlayerId == _client.PlayerId))
            return false;

        // Content-каталог отсеков - the whole-ship overview (talking to the Shipwright) zooms the
        // camera out to fit the entire hull, but the character's own sight cone is still just their
        // ordinary walking-around vision radius - a handful of units, nowhere near enough to cover a
        // whole hull at once. Without this exemption every room outside that radius (nearly the
        // entire ship, at the zoomed-out scale) read as pitch black even though the camera was
        // pointed right at it. Same reasoning as the turret/helm exemptions above: you cannot choose
        // where to build a compartment you cannot see.
        if (ShipBuildOverviewActive(snapshot))
            return false;

        var gaps = new List<SightGap>();
        List<WallSegment> walls;
        Vector2 origin;
        Vector2 eye;
        List<PointLight> lights;
        Color floor;

        if (me.OnEnemyShip)
        {
            foreach (var door in snapshot.EnemyShip.Doors)
                gaps.Add(Occluders.ToGap(door));
            foreach (var airlock in snapshot.EnemyShip.AirlockOuterDoors)
                gaps.Add(Occluders.ToGap(airlock));
            walls = Occluders.Build(snapshot.EnemyShip.Rooms, gaps);
            origin = ComputeStationCamera(me);
            eye = new Vector2((float)me.X, (float)me.Y);
            // A boarded ship is a hostile hull running on its own damaged grid, not the player's -
            // dim, reddish, and flickering rather than tied to the player's own power state.
            lights = BuildEnemyShipLights(snapshot.EnemyShip.Rooms, totalSeconds);
            floor = EnemyShipFloor;
        }
        else
        {
            foreach (var door in snapshot.Doors)
                if (snapshot.DoorStates.FirstOrDefault(s => s.DoorId == door.Id)?.IsOpen ?? true)
                    gaps.Add(Occluders.ToGap(door));
            foreach (var outerDoor in snapshot.AirlockOuterDoors)
                if (snapshot.DoorStates.FirstOrDefault(s => s.DoorId == outerDoor.Id)?.IsOpen ?? false)
                    gaps.Add(Occluders.ToGap(outerDoor));
            // A cockpit window is glass, not plating - sight carries through it into open space
            // exactly like an open door, even though (unlike a door) nothing can walk through it.
            foreach (var pane in CockpitWindows.Panes(snapshot.Rooms))
                gaps.Add(new SightGap(pane.Left, pane.Top, pane.Right, pane.Bottom));
            // A breached wall block is a hole, not a wall - even a single one (World.EnemyAi.cs's
            // own ApplyEnemyAttack already treats a fully-broken block as transparent to a shot
            // passing through it, exterior or interior alike; this is the same idea for the eye).
            // A small square centred on the block works for either orientation without needing to
            // know which: Occluders.AddHorizontal/AddVertical only ever cut a gap out of the one
            // wall-run whose own fixed coordinate falls inside the gap's span on that axis, so a
            // vertical wall block's 1-unit Y-span cuts cleanly out of a vertical run and a
            // horizontal block's X-span out of a horizontal one.
            const float wallBlockGapHalfWidth = 0.5f;
            foreach (var state in snapshot.WallBlockStates)
                if (state.Breached && snapshot.WallBlocks.FirstOrDefault(b => b.Id == state.Id) is { } block)
                    gaps.Add(new SightGap(block.X - wallBlockGapHalfWidth, block.Y - wallBlockGapHalfWidth,
                        block.X + wallBlockGapHalfWidth, block.Y + wallBlockGapHalfWidth));

            // While docked the station's compartments are part of the same layout, in the same
            // coordinates - its walls block the view exactly like the ship's own.
            var rooms = snapshot.Rooms;
            var docked = snapshot.Voyage.DockedPointId is not null;
            if (docked)
            {
                foreach (var door in snapshot.Station.Doors)
                    gaps.Add(Occluders.ToGap(door));
                rooms = snapshot.Rooms.Concat(snapshot.Station.Rooms).ToList();
            }
            walls = Occluders.Build(rooms, gaps);
            // Outside the hull the camera folds the player's world position back into the ship's
            // own frame, and so must the eye - otherwise the mask would sit where the ship isn't.
            var camera = ComputeCamera(snapshot, me);
            origin = camera.Origin;
            eye = new Vector2((float)camera.Anchor.X, (float)camera.Anchor.Y);

            var mood = ComputeShipPowerMood(snapshot);
            lights = BuildShipRoomLights(snapshot.Rooms, mood.PowerFraction);
            // A docked station has its own external power - always lit regardless of what shape the
            // player's own ship's grid is in.
            if (docked)
                AddStationLights(lights, snapshot.Station.Rooms);
            floor = mood.Floor;
        }

        // Outside in a suit the lamp is its own thing: a narrow torch with a long throw, not the
        // wide short cone that works for walking a corridor.
        var suitedOutside = me.WearingSuit && me.IsOutside;
        var radius = suitedOutside ? VacuumLampRadius : me.WearingSuit ? SuitVisionRadius : OpenVisionRadius;
        var halfAngle = suitedOutside ? VacuumLampHalfAngleDegrees
            : me.WearingSuit ? SuitVisionHalfAngleDegrees : 180f;
        // Facing is stored in whatever frame the character moves in - field coordinates while
        // outside - but the mask is built in the ship's frame, same as the camera.
        var facing = new Vec2(me.FacingX, me.FacingY);
        if (me.IsOutside)
            facing = ShipLocalFrame.ToLocalDirection(facing, snapshot.ShipField.RotationDegrees);
        var ambient = suitedOutside ? VacuumHaloRadius : me.WearingSuit ? SuitAmbientRadius : 0f;
        // Outside, the lamp starts dying almost immediately instead of holding full brightness for
        // three quarters of its reach, and the mask keeps a small floor so the starfield survives
        // being multiplied by it. Inside, both stay exactly as they were: a room lamp does fill a
        // room, and indoors there is no starfield to protect.
        var sightReady = me.IsOutside
            ? _visibility.Build(walls, eye, new Vector2((float)facing.X, (float)facing.Y), radius, halfAngle, ambient,
                origin, _renderScale, falloffStart: VacuumLampFalloffStart, floor: VacuumMaskFloor,
                edgeFade: VacuumLampEdgeFade, coneTint: VacuumLampTint)
            : _visibility.Build(walls, eye, new Vector2((float)facing.X, (float)facing.Y), radius, halfAngle, ambient,
                origin, _renderScale);
        // The reactor's light lever (World.cs) kills the room lighting overlay ship-wide - the
        // sight-only fallback right below already exists for exactly this ("nothing built this
        // frame"), so flipping the lever just means everything beyond the player's own lamp goes dark.
        _roomLightingReady = snapshot.ReactorLevers.LightsOn && _roomLighting.Build(walls, lights, floor, origin, _renderScale);
        // Has to happen here, before the backbuffer is touched - see MergeSight's own comment.
        if (_roomLightingReady && sightReady)
            _roomLighting.MergeSight(_spriteBatch, _visibility);
        return sightReady;
    }

    // Never above ~92% brightness even at full power - room art is already painted as if lit, so
    // this only has to darken things down from there, never brighten past the original.
    private static readonly Color PoweredFloor = new(232, 236, 244);
    // Dark and red rather than plain black: an unpowered room still has to read as a place (and as
    // an emergency, not a void) once the player's own suit lamp picks it out.
    private static readonly Color UnpoweredFloor = new(46, 16, 14);
    private static readonly Color EnemyShipFloor = new(22, 14, 16);

    private readonly record struct ShipPowerMood(float PowerFraction, Color Floor);

    // How lit the ship's own lamps/scanner/airlocks are (the "Secondary" power slider,
    // game_design.md section 1) - the slider's own allocation share of the reactor's rated output,
    // scaled down further if the reactor isn't actually delivering that much (low fuel, damage) or
    // the Secondary system itself is damaged (PowerGrid always zeroes a damaged system's output).
    private static ShipPowerMood ComputeShipPowerMood(WorldSnapshot snapshot)
    {
        var damaged = snapshot.SystemStates.FirstOrDefault(s => s.System == PowerSystemId.Secondary)?.Damaged ?? false;
        var maxOutput = snapshot.Power.ReactorMaxOutput;
        var allocFraction = maxOutput > 0f && snapshot.Power.Allocated.TryGetValue(PowerSystemId.Secondary, out var allocated)
            ? MathHelper.Clamp(allocated / maxOutput, 0f, 1f)
            : 0f;
        var outputFraction = maxOutput > 0f ? MathHelper.Clamp(snapshot.Power.ReactorOutput / maxOutput, 0f, 1f) : 0f;
        var fraction = damaged ? 0f : allocFraction * outputFraction;
        return new ShipPowerMood(fraction, Color.Lerp(UnpoweredFloor, PoweredFloor, fraction));
    }

    // One lamp per compartment, tinted with the same department colour RoomDecor paints the floor
    // with.
    //
    // Direct user request ("уменьши его визуальный вид до 4 тайлов... как он показывается в
    // редакторе") - this used to ALSO add a second, separate additive light pool for the reactor/
    // engine room specifically ("the reactor's own glow, present even with the lights off"), on top
    // of this same per-room lamp. Shrinking that extra light's own radius wasn't enough - a point
    // light isn't clipped to its own room's walls, so its glow bled straight through the doorway
    // into whichever room the reactor compartment connects to (confirmed live: the wash reached all
    // the way to the cockpit's own helm/scanner, well past any single room). Removed entirely - the
    // reactor's own device texture (ShipRenderer.DrawReactorBlock) already reads as a lit, glowing
    // machine on its own, the same flat, self-contained way it renders in the Ship Editor; a second
    // light source drawn over it was never needed to sell that. The reactor/engine room keeps the
    // same one ordinary per-room lamp every other compartment gets, nothing extra.
    private static List<PointLight> BuildShipRoomLights(IReadOnlyList<Room> rooms, float powerFraction)
    {
        var lights = new List<PointLight>(rooms.Count);
        var lampIntensity = MathHelper.Lerp(0.05f, 0.55f, powerFraction);
        foreach (var room in rooms)
        {
            var tint = Color.Lerp(Color.White, RoomDecor.Accent(room.Id, room.Name), 0.22f);
            var radius = MathF.Max(room.Width, room.Height) * 0.9f + 1.5f;
            lights.Add(new PointLight(new Vector2((float)room.Center.X, (float)room.Center.Y), radius, tint * lampIntensity));
        }
        return lights;
    }

    // A docked station runs on its own power, not the ship's - always lit, no flicker.
    private static void AddStationLights(List<PointLight> lights, IReadOnlyList<Room> stationRooms)
    {
        foreach (var room in stationRooms)
        {
            var radius = MathF.Max(room.Width, room.Height) * 0.95f + 1.5f;
            lights.Add(new PointLight(new Vector2((float)room.Center.X, (float)room.Center.Y), radius, Color.White * 0.6f));
        }
    }

    // A boarded enemy hull: no power state to read (it isn't the player's grid), so a fixed dim,
    // uneven red emergency light stands in for "this ship has taken damage and is running dark".
    // The sine offsets are seeded from room position so neighbouring compartments don't flicker in
    // lockstep.
    private static List<PointLight> BuildEnemyShipLights(IReadOnlyList<Room> rooms, float totalSeconds)
    {
        var lights = new List<PointLight>(rooms.Count);
        foreach (var room in rooms)
        {
            var flicker = 0.55f + 0.25f * MathF.Sin(totalSeconds * 9f + room.X) * MathF.Sin(totalSeconds * 2.3f + room.Y);
            var radius = MathF.Max(room.Width, room.Height) * 0.85f + 1.2f;
            lights.Add(new PointLight(new Vector2((float)room.Center.X, (float)room.Center.Y), radius,
                new Color(210, 90, 70) * MathHelper.Clamp(flicker, 0.2f, 0.85f)));
        }
        return lights;
    }
}
