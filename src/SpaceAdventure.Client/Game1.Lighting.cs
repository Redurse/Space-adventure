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
            gaps.Add(Occluders.ToGap(snapshot.EnemyShip.BoardingHatch));
            walls = Occluders.Build(snapshot.EnemyShip.Rooms, gaps);
            origin = ComputeStationCamera(me);
            eye = new Vector2(me.X, me.Y);
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
            eye = new Vector2(camera.Anchor.X, camera.Anchor.Y);

            var mood = ComputeShipPowerMood(snapshot);
            lights = BuildShipRoomLights(snapshot.Rooms, mood.PowerFraction, snapshot.Power, totalSeconds);
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
            ? _visibility.Build(walls, eye, new Vector2(facing.X, facing.Y), radius, halfAngle, ambient,
                origin, _renderScale, falloffStart: VacuumLampFalloffStart, floor: VacuumMaskFloor,
                edgeFade: VacuumLampEdgeFade, coneTint: VacuumLampTint)
            : _visibility.Build(walls, eye, new Vector2(facing.X, facing.Y), radius, halfAngle, ambient,
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
    // with, plus the reactor's own glow (present even with the lights off, as long as it's actually
    // producing power) - flickering once its fuel runs critically low.
    private static List<PointLight> BuildShipRoomLights(IReadOnlyList<Room> rooms, float powerFraction, PowerState power, float totalSeconds)
    {
        var lights = new List<PointLight>(rooms.Count + 1);
        var lampIntensity = MathHelper.Lerp(0.05f, 0.55f, powerFraction);
        foreach (var room in rooms)
        {
            var tint = Color.Lerp(Color.White, RoomDecor.Accent(room.Id), 0.22f);
            var radius = MathF.Max(room.Width, room.Height) * 0.9f + 1.5f;
            lights.Add(new PointLight(new Vector2(room.Center.X, room.Center.Y), radius, tint * lampIntensity));
        }

        var reactorRoom = rooms.FirstOrDefault(r => r.Id.Contains("reactor") || r.Id.Contains("engine"));
        if (reactorRoom is not null && power.ReactorMaxOutput > 0f)
        {
            var outputFraction = MathHelper.Clamp(power.ReactorOutput / power.ReactorMaxOutput, 0f, 1f);
            var fuelFraction = power.ReactorMaxFuel > 0f ? power.ReactorFuel / power.ReactorMaxFuel : 1f;
            var flicker = fuelFraction < 0.15f
                ? 0.7f + 0.3f * MathF.Sin(totalSeconds * 17f) * MathF.Sin(totalSeconds * 6.1f)
                : 1f;
            var radius = MathF.Max(reactorRoom.Width, reactorRoom.Height) * 0.75f + 1f;
            lights.Add(new PointLight(new Vector2(reactorRoom.Center.X, reactorRoom.Center.Y), radius,
                new Color(255, 150, 70) * (0.35f * outputFraction * flicker)));
        }

        return lights;
    }

    // A docked station runs on its own power, not the ship's - always lit, no flicker.
    private static void AddStationLights(List<PointLight> lights, IReadOnlyList<Room> stationRooms)
    {
        foreach (var room in stationRooms)
        {
            var radius = MathF.Max(room.Width, room.Height) * 0.95f + 1.5f;
            lights.Add(new PointLight(new Vector2(room.Center.X, room.Center.Y), radius, Color.White * 0.6f));
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
            lights.Add(new PointLight(new Vector2(room.Center.X, room.Center.Y), radius,
                new Color(210, 90, 70) * MathHelper.Clamp(flicker, 0.2f, 0.85f)));
        }
        return lights;
    }
}
