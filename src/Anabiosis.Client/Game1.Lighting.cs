using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Anabiosis.Client.Rendering;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Client;

// The character's own sight-cone/room-lighting mask (BuildVisibilityMask) and the power-driven
// "mood" that feeds it: how bright the ship's lamps read at the current power level, and one
// PointLight per room for the ship/station/boarded-enemy-hull cases. Split out of Game1.cs itself -
// BuildVisibilityMask is called once per frame from Draw. Besides _roomLightingReady (declared in
// Game1.cs, alongside _visibility/_roomLighting themselves) it also owns the docked station's own
// cached tile rasterization - see the fields' own doc comment where they're used, just below.
public partial class Game1
{
    // See GetLiveShipTiles's own doc comment (ShipRenderer.Rooms.cs) for why this is cached at all -
    // same reasoning, just for the station side, which has no equivalent persistent-instance home of
    // its own to live on (StationRenderer never rasterizes tiles - only ShipRenderer/Game1's own
    // BuildVisibilityMask/voice-muffling check ever need a station's TileGrid), so it lives here.
    private TileGrid? _cachedStationTiles;
    private int? _cachedStationTilesFingerprint;

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
    private long _shipWallsTick = -1;
    private IReadOnlyList<DoorState>? _shipWallsDoorStates;
    private List<WallSegment>? _shipWalls;
    private List<WallSegment>? _shipRoomSeams;
    private List<WallSegment>? _shipAndStationWalls;

    private bool BuildVisibilityMask(WorldSnapshot snapshot, float totalSeconds)
    {
        _roomLightingReady = false;
        _shipRenderer.SightTest = null; // set again below once there is a sight to test against
        var me = snapshot.Characters.FirstOrDefault(c => c.PlayerId == _client.PlayerId);
        // Info takes over the viewport the same way the helm does - a HUD screen, not something
        // seen through the character's own eyes, so it reads the same regardless of where they're
        // standing or how dark the room is (matches IsAtHelm above).
        // Direct user request ("чтобы в этом режиме убирались все невидимые зоны игрока когда он
        // жив") - a dead character's own former sight cone/lamp reach means nothing to a free-
        // floating spectator camera; the ship-interior scene itself still draws exactly as normal
        // (DrawCore's own scene pass runs unconditionally - only the darkening COMPOSITE afterward
        // is what this return value skips), so returning false here just means "show it all lit",
        // not "show nothing".
        if (me is null || me.IsAtHelm || _infoPanelOpen || _spectatorMode)
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

        // Looking into the hostile ship from outside (cheat panel) - the whole interior is shown.
        if (EnemyInteriorViewActive(snapshot))
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
            foreach (var hatch in snapshot.EnemyShip.OuterHatches)
                gaps.Add(Occluders.ToGap(hatch));
            // M78 (humble-soaring-cat.md) - tile-native occlusion; rasterized fresh from the same
            // Rooms/Doors the snapshot already carries (OuterHatches included - humble-soaring-cat.md,
            // "убрать AirlockOuterDoor как отдельный тип" - they're ordinary Doors now), same approach
            // ClientTileGrid uses for the player's own ship.
            var enemyDoors = snapshot.EnemyShip.Doors.Concat(snapshot.EnemyShip.OuterHatches).ToList();
            var enemyTiles = TileGridRasterizer.FromRooms(snapshot.EnemyShip.Rooms, enemyDoors);
            // Same live-door-state overlay the ship/station sides need (humble-soaring-cat.md,
            // "не вижу через открытые двери" follow-up) - without it every enemy-ship door tile is
            // permanently "closed" to TileOccluders.IsOccluding, leaving a sliver of wall behind even
            // though the SightGap above is unconditionally cut through it.
            ClientTileGrid.ApplyLiveDoorState(enemyTiles, snapshot.EnemyShip.Rooms, enemyDoors, snapshot.DoorStates);
            // The hull's doorways (door edges, always open): the wall tiles flanking each one are cut away so sight passes through.
            foreach (var edge in snapshot.EnemyShip.DoorEdges ?? Array.Empty<ShipDoorEdge>())
            {
                var other = edge.Side switch
                {
                    TileSide.North => new TileCoord(edge.Coord.X, edge.Coord.Y - 1),
                    TileSide.South => new TileCoord(edge.Coord.X, edge.Coord.Y + 1),
                    TileSide.East => new TileCoord(edge.Coord.X + 1, edge.Coord.Y),
                    _ => new TileCoord(edge.Coord.X - 1, edge.Coord.Y),
                };
                foreach (var tile in new[] { edge.Coord, other })
                    if (enemyTiles.CellAt(tile) is { Wall: not TileWallKind.None })
                    {
                        enemyTiles.SetFloor(tile, true);
                        enemyTiles.SetWall(tile, TileWallKind.None);
                    }
            }
            walls = TileOccluders.Build(enemyTiles, gaps);
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
                if (snapshot.DoorStates.FirstOrDefault(s => s.DoorId == door.Id)?.IsOpen ?? !door.LeadsToVacuum)
                    gaps.Add(Occluders.ToGap(door));
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
            var docked = snapshot.Voyage.DockedPointId is not null;
            if (docked)
                foreach (var door in snapshot.Station.Doors)
                    gaps.Add(Occluders.ToGap(door));
            // M78 (humble-soaring-cat.md) - tile-native occlusion. The ship and the (optional)
            // station are rasterized into two SEPARATE TileGrids rather than merged into one: their
            // Room/Door id namespaces aren't guaranteed disjoint (TileGridRasterizer keys internally
            // by those ids), but a WallSegment carries no id/region identity of its own, so
            // concatenating the two already-rasterized segment lists is safe regardless - the tile-
            // native equivalent of the old code's `rooms.Concat(station.Rooms)`, just moved to after
            // rasterization instead of before. Both grids are cut against the SAME gaps list - a gap
            // is just a world-space rectangle, it applies correctly to either structure's segments
            // regardless of which one it logically came from.
            var shipTiles = _shipRenderer.GetLiveShipTiles(snapshot);
            // The same snapshot is on screen for several frames, and the walls depend on nothing else - build them once per snapshot.
            if (snapshot.Tick != _shipWallsTick || !ReferenceEquals(snapshot.DoorStates, _shipWallsDoorStates) || _shipWalls is null)
            {
                _shipWalls = TileOccluders.Build(shipTiles, gaps);
                _shipRoomSeams = BuildRoomSeams(shipTiles, snapshot.Rooms);
                _shipWallsTick = snapshot.Tick;
                _shipWallsDoorStates = snapshot.DoorStates;
                _shipAndStationWalls = null; // built from these walls - rebuilt below
            }
            walls = _shipWalls;
            if (!docked)
                _shipAndStationWalls = null;
            else if (_shipAndStationWalls is not null)
                walls = _shipAndStationWalls; // the station's walls depend on the same things as the ship's (tick, door states): once per snapshot, not per frame
            else
            {
                // Cached the same way GetLiveShipTiles is (see that method's own doc comment) - a
                // procedural station is generated once and never rebuilt for the rest of the session,
                // so after the first dock this fingerprint check hits every single frame; it's
                // usually the LARGER of the two structures (a station grows with the ship's own
                // size), so this half used to be the bigger share of the ~89ms "Маска" cost.
                // ShipConnector is an ordinary vacuum-facing Door now (humble-soaring-cat.md, "убрать
                // AirlockOuterDoor как отдельный тип") - included alongside Station.Doors here since
                // rasterization/fingerprinting/live-state overlay all need it, same as Station.cs's
                // own constructor does for wall-block generation.
                var stationDoors = snapshot.Station.Doors.Append(snapshot.Station.ShipConnector).ToList();
                var stationFingerprint = ClientTileGrid.ComputeStructuralFingerprint(snapshot.Station.Rooms, stationDoors);
                if (_cachedStationTiles is null || _cachedStationTilesFingerprint != stationFingerprint)
                {
                    _cachedStationTiles = TileGridRasterizer.FromRooms(snapshot.Station.Rooms, stationDoors);
                    // The drawing's half-block walls and extra wall tiles (Station.TileFixes) - the same stamp the server's tiles and the drawn ones got.
                    snapshot.Station.TileFixes?.Apply(_cachedStationTiles);
                    _cachedStationTilesFingerprint = stationFingerprint;
                }
                // Bug fix (humble-soaring-cat.md, "не вижу через открытые двери", follow-up) - the
                // ship-side ClientTileGrid.Build already overlays live door-open state (see its own
                // doc comment); the station side was missed the first time around, so a station door
                // or the ship<->station connector was still permanently "closed" to TileOccluders no
                // matter how it actually stood - the identical bug, just on the other structure.
                ClientTileGrid.ApplyLiveDoorState(_cachedStationTiles, snapshot.Station.Rooms, stationDoors, snapshot.DoorStates);
                walls = _shipAndStationWalls = walls.Concat(TileOccluders.Build(_cachedStationTiles, gaps)).ToList();
            }
            // Outside the hull the camera folds the player's world position back into the ship's
            // own frame, and so must the eye - otherwise the mask would sit where the ship isn't.
            var camera = ComputeCamera(snapshot, me);
            origin = camera.Origin;
            eye = new Vector2((float)camera.Anchor.X, (float)camera.Anchor.Y);

            var mood = ComputeShipPowerMood(snapshot);
            // The reactor's light lever, off: kills the ship's OWN lamps (Barotrauma's own manual
            // light switch reads the same way - a deliberate blackout, not a power shortage), but
            // must not touch a docked station's independent supply below, and must not make the
            // compositor bail out to VisibilityMask's own true-black floor either (see `floor` just
            // below) - a totally unpowered sub still reads as a dim, recognisable space in
            // Barotrauma, never a hidden one.
            lights = snapshot.ReactorLevers.LightsOn ? BuildShipRoomLights(snapshot.Rooms, mood.PowerFraction) : new List<PointLight>();
            // Direct user request ("настенную лампу... когда она установлена, она излучает свет") -
            // one small extra pool per placed WallLamp, same LightsOn/power gating as the ambient
            // room lamps above (a deliberate light-lever blackout kills these too, same as a real
            // wall sconce would go dark) - purely additive, no per-lamp state to read at all.
            if (snapshot.ReactorLevers.LightsOn)
                lights.AddRange(BuildWallLampLights(snapshot.WallLamps?.Where(l => !(snapshot.Wiring.LampsOff?.Contains(l.Id) ?? false)).ToList(), mood.PowerFraction));
            lights.AddRange(BuildDeviceLights(snapshot, totalSeconds));
            // A docked station has its own external power - always lit regardless of what shape the
            // player's own ship's grid is in, or whether its own light lever is on.
            if (docked && snapshot.Station.IsAbandoned)
                lights.AddRange(BuildAbandonedStationLights(snapshot.Station.Rooms, totalSeconds)); // dead: emergency red only
            else if (docked)
                AddStationLights(lights, snapshot.Station.Rooms);
            // Open space has its own ambient darkness, independent of the ship's interior power mood -
            // the same VacuumMaskFloor the sight cone below uses, so the two masks agree on how dark
            // space itself is instead of the hull's own Secondary-power reading leaking out through
            // the render target's uniform clear colour.
            floor = me.IsOutside ? VacuumMaskFloor : snapshot.ReactorLevers.LightsOn ? mood.Floor : LightsOffFloor;
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
        // Sight (not light) also stops where two compartments meet without a door - you see into the next
        // room only through an open door, a breach or a window (direct user choice, "вариант 3").
        var sightWalls = !me.OnEnemyShip && _shipRoomSeams is { Count: > 0 } seams ? walls.Concat(seams).ToList() : walls;
        var sightReady = me.IsOutside
            ? _visibility.Build(sightWalls, eye, new Vector2((float)facing.X, (float)facing.Y), radius, halfAngle, ambient,
                origin, _renderScale, falloffStart: VacuumLampFalloffStart, floor: VacuumMaskFloor,
                edgeFade: VacuumLampEdgeFade, coneTint: VacuumLampTint)
            : _visibility.Build(sightWalls, eye, new Vector2((float)facing.X, (float)facing.Y), radius, halfAngle, ambient,
                origin, _renderScale);
        // Always built, even with the light lever off or no power at all - `lights`/`floor` above
        // already encode "how dark", so RoomLighting's own dim floor (never literal black) is what
        // shows beyond the player's own lamp, matching Barotrauma's own unpowered-but-not-hidden subs.
        // The VisibilityMask-only fallback (Game1.cs's own `maskReady` branch) still exists for the
        // genuine edge case of RoomLighting failing to build at all (e.g. a zero-size viewport).
        // Barotrauma-style dusk instead of pitch black: the hull itself stays faintly readable outside the
        // light (cold, dim), while anyone behind a wall from you is simply not drawn (SightTest below) -
        // the darkness used to be the only thing hiding them, and a lifted floor must not turn into a
        // wall-hack.
        if (!me.IsOutside)
            floor = LiftToDusk(floor, me.OnEnemyShip ? EnemyDuskFloor : snapshot.ReactorLevers.LightsOn ? DuskFloor : DuskFloorLightsOff);
        _roomLightingReady = _roomLighting.Build(walls, lights, floor, origin, _renderScale);
        // Has to happen here, before the backbuffer is touched - see MergeSight's own comment.
        if (_roomLightingReady && sightReady)
            _roomLighting.MergeSight(_spriteBatch, _visibility);
        if (sightReady)
        {
            var testWalls = sightWalls;
            var sightEye = eye;
            var sightOrigin = origin;
            _shipRenderer.SightTest = screenPoint =>
                !ShadowCast.IsBlocked(sightEye, (screenPoint - sightOrigin) / ShipRenderer.PixelsPerUnit, testWalls);
        }
        return sightReady;
    }

    // Direct user request ("точь-в-точь как в Barotrauma") - a near-white floor (the old 232,236,244)
    // left no room between "away from any lamp" and "standing under one": the whole scene read as
    // uniformly bright regardless of where the room's own PointLight actually was, which is the
    // opposite of Barotrauma's own moody, high-contrast pools of light in real darkness. A dim,
    // cool-toned floor gives the additive lamp light somewhere to actually stand out against.
    private static readonly Color PoweredFloor = new(52, 58, 76);
    // Dark and red rather than plain black: an unpowered room still has to read as a place (and as
    // an emergency, not a void) once the player's own suit lamp picks it out.
    private static readonly Color UnpoweredFloor = new(46, 16, 14);
    private static readonly Color EnemyShipFloor = new(22, 14, 16);
    // The light lever switched off on purpose - darker than any power-derived UnpoweredFloor (a
    // deliberate near-blackout, not merely low power), but never literal zero. Real Barotrauma's own
    // fully unpowered submarines still read as a dim, recognisable space; only the sight cone/lamp
    // brings detail back, the floor itself never hides the room's existence. Direct user report
    // ("темных зонах... ничего не видно") - the original (10, 8, 14) multiplied the scene down to a
    // few percent of its own brightness, which on most monitors rounds straight to indistinguishable-
    // from-black rather than "very poorly lit". Raised enough to stay readable as genuinely awful
    // visibility instead of no visibility at all - still the darkest floor in the game by a wide margin.
    private static readonly Color LightsOffFloor = new(26, 22, 30);

    // The least light the deck ever gets indoors: enough to read the hull, the doors and the machines
    // as cold shapes, never enough to work by. Lights off is dimmer still; a hostile hull keeps its red.
    private static readonly Color DuskFloor = new(60, 70, 96);
    private static readonly Color DuskFloorLightsOff = new(38, 44, 62);
    private static readonly Color EnemyDuskFloor = new(62, 36, 38);

    private static Color LiftToDusk(Color floor, Color dusk)
    {
        return new Color(Math.Max(floor.R, dusk.R), Math.Max(floor.G, dusk.G), Math.Max(floor.B, dusk.B));
    }

    private readonly record struct ShipPowerMood(float PowerFraction, Color Floor);

    // How lit the ship's own lamps/scanner/airlocks are (the "Secondary" system, game_design.md
    // section 1) - how much of its full draw Secondary is actually getting from the ship's grid,
    // scaled down further if the reactor isn't actually delivering (low fuel, damage) or the
    // Secondary system itself is damaged.
    private static ShipPowerMood ComputeShipPowerMood(WorldSnapshot snapshot)
    {
        var damaged = snapshot.SystemStates.FirstOrDefault(s => s.System == PowerSystemId.Secondary)?.Damaged ?? false;
        // W2 of wiring-plan.md: Secondary now gets its full draw (SecondaryFullPower) whenever the grid
        // can supply it, instead of a slider share of the reactor - so the mood is that share of a
        // full draw, scaled to the same 0.2 the old even split of a 60-unit reactor gave the lights
        // (the tuned look), and falls off in a brownout or with Secondary's wiring cut.
        const float SecondaryFullPower = 12f;
        const float FullyPoweredMood = 0.2f;
        var allocFraction = snapshot.Power.Allocated.TryGetValue(PowerSystemId.Secondary, out var allocated)
            ? MathHelper.Clamp(allocated / SecondaryFullPower, 0f, 1f) * FullyPoweredMood
            : 0f;
        // W3: no reactor-output factor any more - the auto-mode reactor's output tracks its load,
        // so it says nothing about brightness; a shortfall already shows up in "allocated".
        var fraction = damaged ? 0f : allocFraction;
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
        var lampIntensity = MathHelper.Lerp(0.02f, 0.25f, powerFraction); // a dim fill - the wall lamps and machines make the pools of light
        foreach (var room in rooms)
        {
            var tint = Color.Lerp(Color.White, RoomDecor.Accent(room.Id, room.Name), 0.22f);
            var radius = MathF.Max(room.Width, room.Height) * 0.9f + 1.5f;
            lights.Add(new PointLight(new Vector2((float)room.Center.X, (float)room.Center.Y), radius, tint * lampIntensity));
        }
        return lights;
    }

    // Direct user request ("настенную лампу... когда она установлена, она излучает свет") - a
    // small, warm-toned pool right at the fixture itself, distinct from the room's own ambient
    // lamp (RoomDecor-tinted, centred on the room) - a real, localized light source you placed, not
    // just flavor furniture. Deliberately modest radius (a wall sconce, not a second room lamp) so
    // several of them along a corridor read as a lit row rather than one big flat wash.
    private static IEnumerable<PointLight> BuildWallLampLights(IReadOnlyList<WallLamp>? wallLamps, float powerFraction)
    {
        if (wallLamps is null)
            yield break;
        // Direct user request ("сделай чтобы было видно что она излучает свет как в баротравме") -
        // brighter, warmer and wider than the first version, and cast from just in front of the
        // fitting's lens rather than the wall tile's own centre: that centre sits right on the
        // half-thick wall's own sight-blocking edge (TileOccluders), which shadowed half of the pool.
        var intensity = MathHelper.Lerp(0.15f, 1f, powerFraction);
        var tint = new Color(255, 206, 136) * intensity;
        foreach (var lamp in wallLamps)
        {
            var (dx, dy) = lamp.FacingSide switch
            {
                TileSide.North => (0f, -1f),
                TileSide.South => (0f, 1f),
                TileSide.East => (1f, 0f),
                _ => (-1f, 0f),
            };
            yield return new PointLight(new Vector2((float)lamp.Position.X + dx * 0.32f, (float)lamp.Position.Y + dy * 0.32f), 6.5f, tint);
        }
    }

    // A docked station runs on its own power, not the ship's - always lit, no flicker.
    private static void AddStationLights(List<PointLight> lights, IReadOnlyList<Room> stationRooms)
    {
        foreach (var room in stationRooms)
        {
            var radius = MathF.Max(room.Width, room.Height) * 0.95f + 1.5f;
            lights.Add(new PointLight(new Vector2((float)room.Center.X, (float)room.Center.Y), radius, Color.White * 0.3f));
        }
    }

    // A boarded enemy hull: no power state to read (it isn't the player's grid), so a fixed dim,
    // uneven red emergency light stands in for "this ship has taken damage and is running dark".
    // The sine offsets are seeded from room position so neighbouring compartments don't flicker in
    // lockstep.
    // A dead station (World.AbandonedStations.cs) has no working lamps - only a weak red emergency glow in each
    // room, guttering, so the place is found by the suit lamp and its own dim red rather than lit like a live one.
    private static List<PointLight> BuildAbandonedStationLights(IReadOnlyList<Room> rooms, float totalSeconds)
    {
        var lights = new List<PointLight>(rooms.Count);
        foreach (var room in rooms)
        {
            var flicker = 0.5f + 0.5f * MathF.Sin(totalSeconds * 6f + room.X * 1.7f) * MathF.Sin(totalSeconds * 1.9f + room.Y);
            var radius = MathF.Max(room.Width, room.Height) * 0.7f + 1f;
            lights.Add(new PointLight(new Vector2((float)room.Center.X, (float)room.Center.Y), radius,
                new Color(200, 60, 48) * MathHelper.Clamp(0.1f + 0.22f * flicker, 0.06f, 0.34f)));
        }
        return lights;
    }

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

public partial class Game1
{
    // Where two compartments meet across bare floor with no door between them: an invisible wall for the
    // eye only (lamp light still spills through the opening). Built from the same live tile grid as the
    // walls, one unit-long segment per seam edge; doors (tile or edge) decide sight by themselves.
    private static List<WallSegment> BuildRoomSeams(TileGrid tiles, IReadOnlyList<Room> rooms)
    {
        var seams = new List<WallSegment>();
        string? RoomOf(TileCoord c) => TileMovement.RoomIdAt(rooms, new Vec2(c.X + 0.5, c.Y + 0.5));
        // Both tiles a door edge sits between are its doorway: a door in a wall line stands on one face of a
        // floor tile that rect-wise belongs to one room, so the tile's other face would read as a door-less
        // seam and blind an open door (direct user report - "с открытой дверью не вижу соседний отсек").
        var doorway = new HashSet<TileCoord>();
        foreach (var (coord, side) in tiles.DoorEdges.Keys)
        {
            doorway.Add(coord);
            doorway.Add(side.Offset(coord));
        }
        foreach (var (coord, cell) in tiles.Cells)
        {
            if (!cell.HasFloor || cell.Wall != TileWallKind.None || RoomOf(coord) is not { } here)
                continue;
            foreach (var side in new[] { TileSide.East, TileSide.South })
            {
                var next = side.Offset(coord);
                if (tiles.CellAt(next) is not { HasFloor: true, Wall: TileWallKind.None } || tiles.DoorEdgeAt(coord, side) is not null)
                    continue;
                if (doorway.Contains(coord) || doorway.Contains(next) || RoomOf(next) is not { } there || there == here)
                    continue;
                seams.Add(side == TileSide.East
                    ? new WallSegment(coord.X + 1, coord.Y, coord.X + 1, coord.Y + 1)
                    : new WallSegment(coord.X, coord.Y + 1, coord.X + 1, coord.Y + 1));
            }
        }
        return seams;
    }
}
