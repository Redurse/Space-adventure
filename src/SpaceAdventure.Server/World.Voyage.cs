using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Open galaxy navigation (game_design.md section 5): the player picks any point on the map from
// the navigation console and the ship flies there in a straight line. Arrival resolves based on
// the point's kind — a hostile sector starts a fight, a station docks and resupplies. The ship
// starts docked at the home station.
public sealed partial class World
{
    private const float TransitSlowdownRadius = 25f; // start easing off the throttle before overshoot

    // How far the ship has to fly from whatever pulled it into a local encounter before that
    // encounter lets go and Phase falls back to Traveling - the same "you can always just leave"
    // rule for every location kind, game_design.md's own "в них можно летать" turned into an actual
    // boundary instead of a one-way trip that only ends by winning/docking/picking a new course.
    // Kept safely under the field's own cardinal half-extent (a 300x300 field's centre is only
    // 150 units from any edge) - the exit radius used to be 160, bigger than that, which meant
    // flying "clear" in most directions was only reachable by first hitting the field's own wall
    // and getting clamped there, leaving the ship pinned to the system's edge the moment it
    // dropped back into Traveling. 130 stays comfortably outside every default asteroid/ore
    // deposit (~114 from centre at the farthest) while leaving real room to reach it in any
    // direction without touching the boundary.
    private const float AsteroidFieldExitRadius = 130f;
    private const float StationApproachAbortDistance = 30f; // beyond StationApproachStartDistance(20) - called off the approach, not just drifted
    // Measured from the sector's own marker, not from the (actively pursuing) enemy ships - a
    // squadron's formation AI tracks the ship's own position closely (World.EnemyFleet.cs's
    // SteerEnemy), so distance to them barely opens even during a real retreat; distance from the
    // fixed point that started the fight is the same "flown clear of it" signal AsteroidField/
    // StationApproach already use, and every battle starts within the marker's own small
    // CaptureRadius(8) by construction, so reaching this takes a real, sustained retreat either way.
    // Some sectors sit close enough to the field's own edge (sol's sector-beta, 42 units from the
    // left wall) that the old 45 was unreachable in that one direction without hitting the wall
    // first - 35 leaves every sector real room in every direction.
    private const float BattleFleeDistance = 35f;
    // However a location is left, the ship should land somewhere flyable, not pinned to the
    // system's own outer boundary - NudgeAwayFromFieldEdge is the safety net underneath the three
    // radii above, in case a future system's own layout ever puts a location's escape route that
    // close to the wall again.
    private const float FieldEdgeSafetyMargin = 12f;

    public GalaxyMap GalaxyMap { get; } = GalaxyMap.CreateStarter();

    // Which StarSystem the ship is currently in (World.StarSystems.cs) - changed only by warp;
    // AsteroidField resolves through it rather than through a single stored instance.
    private string _currentSystemId = "";
    private string? _dockedPointId;
    private string? _travelTargetPointId;
    // The autopilot's actual steering target (World.Voyage.cs's StepTraveling) - set alongside
    // _travelTargetPointId for a named point, or alone for a free-form click with no point behind
    // it. Split from _travelTargetPointId because that field's other job (remembering which
    // station the ship is captured into, for CanDockNow/TryDockAtStation) has to survive even
    // when the ship wasn't actually aimed at that station in the first place (see Arrive()).
    private Vec2? _travelTargetPosition;
    // Who owns the ship currently being fought - captured on arrival, since the sector's point id
    // is no longer around by the time the fight ends (World.Factions.cs).
    private FactionId? _battleFaction;
    private string? _battleSectorPointId; // same reason, for bounty quests (World.Quests.cs)
    // The sector just cleared (ResolveEnemyLosses) - excluded from the universal incidental-capture
    // scan (StepTraveling) until the ship gets safely clear of it, even though the small position
    // nudge alone isn't enough: a redirected course to some OTHER destination can still curve back
    // within its CaptureRadius depending on geometry, and without this it would restart the same
    // fight instantly. Cleared once far enough away (StepTraveling) or once deliberately re-targeted.
    private string? _recentlyDisengagedSectorId;
    private int _remainingEnemyShips; // hulls of the squadron still flying
    private string? _crewShipId; // which enemy hull the current boarding crew belongs to

    private void StepVoyage(double deltaSeconds)
    {
        switch (Phase)
        {
            case VoyagePhase.Traveling:
                StepTraveling(deltaSeconds);
                break;

            case VoyagePhase.Battle:
                // A fight is flown, not watched: the same helm physics the asteroid field uses,
                // so the player can close, kite or put a rock between themselves and the guns -
                // including flying clear of it entirely (EnemyMaxSpeed's own doc comment already
                // promises "you can outrun them").
                StepShipFieldPhysics(deltaSeconds);
                ResolveEnemyLosses();
                if (Phase == VoyagePhase.Battle && HasFledTheSector())
                    FleeBattle();
                break;

            case VoyagePhase.Station:
                break; // sits docked until the player chooses somewhere to go

            case VoyagePhase.AsteroidField:
                StepShipFieldPhysics(deltaSeconds);
                if ((AsteroidField.Center - _shipFieldPosition).Length() > AsteroidFieldExitRadius)
                {
                    NudgeAwayFromFieldEdge();
                    Phase = VoyagePhase.Traveling; // flown clear of the belt - back in open space, free to pick a new course
                }
                break;

            case VoyagePhase.StationApproach:
                StepStationApproachPhysics(deltaSeconds);
                if ((DockBerthPosition - _shipFieldPosition).Length() > StationApproachAbortDistance)
                {
                    _travelTargetPointId = null; // called the approach off - nothing bound to head for any more
                    NudgeAwayFromFieldEdge();
                    Phase = VoyagePhase.Traveling;
                }
                break;
        }
    }

    private void StepTraveling(double deltaSeconds)
    {
        if (_travelTargetPosition is not { } target)
            return; // idling in open space with nowhere chosen yet

        // Taking the helm by hand hands control back - the same rule the captain-bot's own
        // auto-stabilize already follows (World.CrewAi.cs) for exactly the same reason: a human at
        // the console overrides automatic flight, not the other way round. Autopilot and manual
        // flight now run on the same clock at the same cruise speed (World.ShipField.cs's
        // ShipMaxSpeed) - a real, walkable trip either way, not a compressed blip while unmanned.
        var humanAtHelm = _characters.Values.Any(c => !c.IsBot && c.IsAtHelm);
        if (!humanAtHelm)
            AutopilotToward(target, deltaSeconds);

        StepShipFieldPhysics(deltaSeconds, fullPower: !humanAtHelm, ignoreAsteroids: true);

        // Re-arm the just-cleared sector once the ship is well clear of it (3x its own radius,
        // comfortably more than a redirected course could plausibly clip through by accident) -
        // otherwise it would stay permanently immune to ever ambushing this ship again.
        if (_recentlyDisengagedSectorId is { } clearedSectorId &&
            (GalaxyMap.GetPoint(clearedSectorId).Position - _shipFieldPosition).Length() > GalaxyMap.GetPoint(clearedSectorId).CaptureRadius * 3f)
            _recentlyDisengagedSectorId = null;

        // Every point of interest in the system catches the ship on its own radius, not just
        // whichever one the player actually clicked - flying near a hostile sector on the way to
        // somewhere else is exactly as much an arrival as steering straight at it (game_design.md -
        // two-tier map, "у каждой точки интереса есть радиус когда она подхватывает игрока"). A few
        // exceptions, all only capturing when deliberately targeted:
        // - AsteroidField: unlike a station or a sector, it isn't a separate scene to cut away to -
        //   the ship is already flying among these same rocks the whole time it's Traveling, and
        //   EnterAsteroidField's own re-centering is a deliberate "I want to stop here and mine"
        //   convenience, not something merely passing near the belt's marker (which several routes
        //   do, since it sits at the system's own centre) should trigger by accident.
        // - Station: casting off (TryStartTravel) leaves the ship sitting exactly on the station's
        //   own map position (Station.RepositionTo anchors docking there now) - without this
        //   exception, the very first Traveling tick after undocking would immediately re-catch the
        //   ship on the station it just left, forever, regardless of where it was actually headed.
        // - _recentlyDisengagedSectorId: the sector just cleared - a redirected course to some other
        //   destination can still pass back within its CaptureRadius depending on geometry (the
        //   small disengage nudge in ResolveEnemyLosses only guarantees clearance along one line,
        //   not every possible new heading), which would otherwise restart the same fight instantly.
        // (Warping away no longer has a discrete point to exclude here - World.StarSystems.cs's
        // CanWarpNow just watches distance from the field's own centre every tick, no capture event.)
        var caught = GalaxyMap.GetSystem(_currentSystemId).Points
            .Where(p => p.Kind is not (GalaxyPointKind.AsteroidField or GalaxyPointKind.Station) || p.Id == _travelTargetPointId)
            .Where(p => p.Id != _recentlyDisengagedSectorId || p.Id == _travelTargetPointId)
            .Where(p => (p.Position - _shipFieldPosition).Length() <= p.CaptureRadius)
            .OrderBy(p => (p.Position - _shipFieldPosition).Length())
            .FirstOrDefault();
        if (caught is not null)
            Arrive(caught);
    }

    // Flies the ship toward a point the way a player would with the joystick: full throttle once
    // roughly lined up, easing off near the target, turning input dropping to zero once the
    // heading error is tiny (SetHelmInput, World.ShipField.cs). deltaSeconds is the SAME
    // (possibly time-compressed) step StepShipFieldPhysics is about to take - the turn rate has to
    // know it, or a single compressed tick's fixed-rate turn (ShipRotationDegreesPerSecond * dt)
    // overshoots a small remaining error and the heading oscillates back and forth forever instead
    // of ever settling on the bearing.
    private void AutopilotToward(Vec2 target, double deltaSeconds)
    {
        var toTarget = target - _shipFieldPosition;
        var distance = toTarget.Length();
        if (distance < 0.01f)
        {
            SetHelmInput(0f, 0f);
            return;
        }

        var bearingDegrees = MathF.Atan2(toTarget.Y, toTarget.X) * (180f / MathF.PI) - Ship.ForwardDegrees;
        var error = ((bearingDegrees - _shipRotationDegrees) % 360f + 540f) % 360f - 180f;
        var throttle = MathF.Abs(error) >= 25f ? 0f : Math.Min(1f, distance / TransitSlowdownRadius);
        var maxStepDegrees = ShipRotationDegreesPerSecond * (float)deltaSeconds;
        var turn = MathF.Abs(error) < 2f ? 0f
            : maxStepDegrees >= MathF.Abs(error) ? error / maxStepDegrees // lands exactly on the bearing this tick, doesn't swing past it
            : MathF.Sign(error);
        SetHelmInput(throttle, turn);
    }

    private void Arrive(GalaxyPoint target)
    {
        // Whatever the player actually clicked (a named point, a free-form spot, or nothing this
        // trip - just drifted close enough), the point that caught the ship wins from here: every
        // branch below clears the steering target the same way an explicit arrival always has.
        _travelTargetPosition = null;

        if (target.Kind == GalaxyPointKind.HostileSector)
        {
            _travelTargetPointId = null;
            _battleFaction = OwnerOf(target.Id);
            _battleSectorPointId = target.Id;
            // Battle happens in real field space, so the enemy hulls are somewhere you can
            // physically fly to, shoot at and board (World.Boarding.cs) - not an abstract HP bar.
            // Starts wherever the ship already was, not re-centred on the system - the universal
            // capture-radius scan above can trigger this from anywhere near the sector's own
            // marker (which is rarely the system's centre), and snapping the ship across the map
            // to fight reads as a teleport bug, not "you got jumped".
            _shipVelocity = Vec2.Zero;
            _shipThrust = Vec2.Zero;
            _shipRotationDegrees = 0f;
            _shipAutoStabilize = true;
            // needs the ship parked first: they spawn off its bow. Size adjusts with standing
            // (World.Factions.cs) - hated crews draw a bigger welcome, loved ones a smaller one.
            SpawnEnemySquadron(Math.Max(1, target.SquadronSize + SquadronSizeAdjustment(OwnerOf(target.Id))));
            ResetEnemyCrew(); // a fresh enemy ship means a fresh crew to board through
            _crewShipId = BoardableEnemy?.Id;
            Phase = VoyagePhase.Battle;
        }
        else if (target.Kind == GalaxyPointKind.AsteroidField)
        {
            _travelTargetPointId = null;
            EnterAsteroidField();
        }
        else
        {
            // Set here rather than relied upon from before the trip started: the universal capture
            // scan above can hand this station the ship even when it wasn't the intended
            // destination (a free-form click elsewhere that happened to pass close by), so
            // CanDockNow/TryDockAtStation (World.StationDocking.cs) need it stamped fresh on every
            // arrival, not just a click-through from TryStartTravel.
            _travelTargetPointId = target.Id;
            // Physically anchor the (shared, per-kind) station structure to THIS point's own map
            // position before computing anything relative to it - otherwise every station docks at
            // the one fixed spot Station.Default.cs happened to build it at, regardless of which
            // point's marker the ship actually flew to, which reads as a teleport on arrival.
            Station.RepositionTo(target.Position);
            EnterStationApproach();
        }
    }

    // Kills resolved, however they happened - shelled from a turret or cleared room by room by a
    // boarding party. Every ship destroyed costs its owner standing (game_design.md section 12 -
    // "групповые вражеские встречи"); only clearing the last one counts as taking the sector, so
    // that's when a bounty on it is satisfied. The whole squadron is on the board at once now, so
    // this counts what's left rather than dealing the next hull in.
    private void ResolveEnemyLosses()
    {
        var stillFlying = _enemyShips.Count(e => e.Alive);
        for (var killed = _remainingEnemyShips - stillFlying; killed > 0; killed--)
            if (_battleFaction is { } faction)
                RecordShipDestroyed(faction);
        _remainingEnemyShips = stillFlying;

        // Losing the hull the boarding party was walking through hands them the next one: the crew
        // belongs to whichever ship is currently boardable, so it's rebuilt when that changes.
        var boardableId = BoardableEnemy?.Id;
        if (boardableId != _crewShipId)
        {
            EjectBoardersFromLostHull(); // their ship is gone, and the next one is a different plan
            ResetEnemyCrew();
            _crewShipId = boardableId;
        }

        if (stillFlying > 0)
            return;

        if (_battleSectorPointId is { } sectorId)
        {
            NoteBountyTargetDestroyed(sectorId);
            // Arrive() no longer recentres the ship on the system when a fight starts (it now
            // stays wherever the universal capture-radius scan caught it - StepTraveling), which
            // means it can still be sitting inside this very sector's own CaptureRadius the moment
            // the fight ends. Left alone, the very next Traveling tick would catch it again and
            // restart the fight instantly, forever. Nudge it just clear of that radius instead -
            // reads as "disengaging", not a teleport, and small enough that a real player would
            // barely notice it.
            var sector = GalaxyMap.GetPoint(sectorId);
            var awayFromSector = _shipFieldPosition - sector.Position;
            if (awayFromSector.Length() < sector.CaptureRadius + 1f)
            {
                var direction = awayFromSector.Length() > 0.01f ? awayFromSector.Normalized() : new Vec2(1f, 0f);
                _shipFieldPosition = (sector.Position + direction * (sector.CaptureRadius + 1f))
                    .Clamp(0, 0, AsteroidField.Width, AsteroidField.Height);
            }
            // The nudge above only guarantees clearance along the one line it pushed along - the
            // exclusion (StepTraveling) is what actually protects against a redirected course
            // curving back through this sector's radius regardless of geometry.
            _recentlyDisengagedSectorId = sectorId;
            _battleSectorPointId = null;
        }
        _battleFaction = null;
        _projectiles.Clear(); // nothing left to hit; shells in flight don't follow you out of the sector
        Phase = VoyagePhase.Traveling; // sector cleared - free to pick a new destination
    }

    // True once the ship has actually flown clear of the sector that's fighting it, not merely
    // clear of the ships themselves - their own formation AI keeps them tracking the ship's current
    // position closely (SteerEnemy), so measuring against them barely moves even during a real
    // retreat. The marker is a fixed point every battle starts within CaptureRadius(8) of, so this
    // only fires after genuinely putting distance behind, regardless of how tightly the squadron
    // itself manages to keep pace.
    private bool HasFledTheSector() =>
        _battleSectorPointId is { } sectorId &&
        (GalaxyMap.GetPoint(sectorId).Position - _shipFieldPosition).Length() > BattleFleeDistance;

    // Running rather than winning - the squadron is still out there, so no bounty progress, no
    // standing hit for them, nothing recorded as destroyed. Same anti-recapture bookkeeping a won
    // fight uses (_recentlyDisengagedSectorId) so the very next Traveling tick doesn't instantly
    // catch the ship again if it happens to still be within the sector marker's own CaptureRadius -
    // flying back in later legitimately restarts the same fight, exactly as it should.
    private void FleeBattle()
    {
        _recentlyDisengagedSectorId = _battleSectorPointId;
        _battleSectorPointId = null;
        _battleFaction = null;
        _projectiles.Clear();
        NudgeAwayFromFieldEdge();
        Phase = VoyagePhase.Traveling;
    }

    // Pulls the ship back off the system's own outer boundary if flying clear of a location left
    // it sitting right on (or clamped against) the edge - none of AsteroidFieldExitRadius/
    // StationApproachAbortDistance/BattleFleeDistance should require touching the wall to satisfy,
    // but this is the safety net in case one ever does for some future system's layout.
    private void NudgeAwayFromFieldEdge()
    {
        _shipFieldPosition = new Vec2(
            Math.Clamp(_shipFieldPosition.X, FieldEdgeSafetyMargin, AsteroidField.Width - FieldEdgeSafetyMargin),
            Math.Clamp(_shipFieldPosition.Y, FieldEdgeSafetyMargin, AsteroidField.Height - FieldEdgeSafetyMargin));
    }

    // Entering the field (game_design.md Phase 3, M15) always drops the ship in the middle,
    // stopped and stable — arriving mid-drift into a field full of obstacles would be needlessly
    // punishing and isn't something the player chose.
    private void EnterAsteroidField()
    {
        Phase = VoyagePhase.AsteroidField;
        _shipFieldPosition = AsteroidField.Center;
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipRotationDegrees = 0f;
        _shipAutoStabilize = true;
    }

    // Station stop (game_design.md Phase1: "станция для дозаправки/ремонта") — tops fuel back
    // up, welds every hull breach and refills every room's air.
    private void EnterStation(string pointId)
    {
        Phase = VoyagePhase.Station;
        // Mated, not merely "at the station": squared up on the berth so the ship's airlock and the
        // station's connector are one rectangle (World.StationDocking.cs). Set here rather than
        // only in TryDockAtStation so the game's own opening state - already docked at the home
        // station - lines up the same way as one the player flew in.
        _shipRotationDegrees = 0f;
        _shipFieldPosition = DockBerthPosition;
        _dockedPointId = pointId;
        _travelTargetPointId = null;
        AutosavePending = true; // game_design.md section 5 - docking is the save point
        ResetStationCrimeState(); // a new visit finds the crates restocked and the guards calm
        PowerGrid.Reactor.Refuel();
        RefillOxygenTanks(); // a station resupplies air the same way it refuels and repairs
        InitializeWallBlocks(); // a station patches the hull back to full the same way
        RestockAmmoStorages(); // and tops the ammo crates back up too
        foreach (var room in Ship.Rooms)
            _roomOxygen[room.Id] = FullOxygen;
        RegenerateRecruitRoster();
    }

    // From the navigation console: pick a destination, either a known point of interest (pointId)
    // or any free-form spot in the system's own bounded field (x/y - game_design.md, "не обязательно
    // к точкам интереса он может тыкнуть в любое место системы"). Ignored mid-fight (can't flee to
    // the map), for an unknown point id, and when neither a point nor coordinates were actually
    // clicked this frame.
    private void TryStartTravel(string? pointId, float? x, float? y)
    {
        if (Phase == VoyagePhase.Battle)
            return;

        Vec2 position;
        if (pointId is not null)
        {
            // Only this system's own points - nothing in the current UI ever offers another
            // system's (GalaxyPoints is scoped the same way, World.Factions.cs's
            // CreateGalaxyPoints), and flying there without a warp would be a coordinate-space
            // coincidence, not a real destination.
            if (GalaxyMap.GetSystem(_currentSystemId).Points.All(p => p.Id != pointId))
                return;
            position = GalaxyMap.GetPoint(pointId).Position;
        }
        else if (x is { } px && y is { } py)
        {
            // Clamped to the same hard edge the ship's own flight physics already respects
            // (StepShipFieldPhysics) - clicking past the barrier just aims at the barrier instead
            // of a spot the ship could never actually reach.
            position = new Vec2(px, py).Clamp(0, 0, AsteroidField.Width, AsteroidField.Height);
        }
        else
        {
            return; // no click this frame
        }

        PullCrewOffStation();

        Phase = VoyagePhase.Traveling;
        _dockedPointId = null;
        // A free-form click has no point behind it - _travelTargetPointId stays null (nothing named
        // to show), while _travelTargetPosition (the only thing StepTraveling actually steers by)
        // is set either way.
        _travelTargetPointId = pointId;
        _travelTargetPosition = position;
    }
}
