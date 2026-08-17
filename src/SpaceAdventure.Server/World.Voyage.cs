using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Open galaxy navigation (game_design.md section 5): the player picks any point on the map from
// the navigation console and the ship flies there in a straight line. Arrival resolves based on
// the point's kind — a hostile sector starts a fight, a station docks and resupplies. The ship
// starts docked at the home station.
public sealed partial class World
{
    // Autopilot cruise while nobody's at the helm (World.StarSystems.cs's TryWarpTo companion
    // mechanic) - the same field flight everyone already flies during a fight or a docking
    // approach, just running several times faster while there's nothing yet to react to.
    private const float TimeCompressionFactor = 8f;
    private const float TransitArrivalRadius = 8f; // close enough to hand off to Arrive()
    private const float TransitSlowdownRadius = 25f; // start easing off the throttle before overshoot

    public GalaxyMap GalaxyMap { get; } = GalaxyMap.CreateStarter();

    // Which StarSystem the ship is currently in (World.StarSystems.cs) - changed only by warp;
    // AsteroidField resolves through it rather than through a single stored instance.
    private string _currentSystemId = "";
    private string? _dockedPointId;
    private string? _travelTargetPointId;
    // Who owns the ship currently being fought - captured on arrival, since the sector's point id
    // is no longer around by the time the fight ends (World.Factions.cs).
    private FactionId? _battleFaction;
    private string? _battleSectorPointId; // same reason, for bounty quests (World.Quests.cs)
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
                // so the player can close, kite or put a rock between themselves and the guns.
                StepShipFieldPhysics(deltaSeconds);
                ResolveEnemyLosses();
                break;

            case VoyagePhase.Station:
                break; // sits docked until the player chooses somewhere to go

            case VoyagePhase.AsteroidField:
                StepShipFieldPhysics(deltaSeconds);
                break;

            case VoyagePhase.StationApproach:
                StepStationApproachPhysics(deltaSeconds);
                break;
        }
    }

    private void StepTraveling(double deltaSeconds)
    {
        if (_travelTargetPointId is not { } targetId)
            return; // idling in open space with nowhere chosen yet

        var target = GalaxyMap.GetPoint(targetId);
        // Taking the helm by hand hands control back and drops the time compression - the same
        // rule the captain-bot's own auto-stabilize already follows (World.CrewAi.cs) for exactly
        // the same reason: a human at the console overrides automatic flight, not the other way
        // round.
        var humanAtHelm = _characters.Values.Any(c => !c.IsBot && c.IsAtHelm);
        var effectiveDelta = humanAtHelm ? deltaSeconds : deltaSeconds * TimeCompressionFactor;
        if (!humanAtHelm)
            AutopilotToward(target.Position, effectiveDelta);

        StepShipFieldPhysics(effectiveDelta, fullPower: !humanAtHelm);

        if ((target.Position - _shipFieldPosition).Length() <= TransitArrivalRadius)
            Arrive(target);
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
        if (target.Kind == GalaxyPointKind.WarpPoint)
        {
            // Parked, not drifting - same guarantee every other arrival gives, so residual
            // momentum can't carry the ship back out of WarpCaptureRadius before anyone reacts.
            _travelTargetPointId = null;
            _shipVelocity = Vec2.Zero;
            _shipThrust = Vec2.Zero;
            _shipAutoStabilize = true;
        }
        else if (target.Kind == GalaxyPointKind.HostileSector)
        {
            _travelTargetPointId = null;
            _battleFaction = OwnerOf(target.Id);
            _battleSectorPointId = target.Id;
            // Battle happens in real field space, so the enemy hulls are somewhere you can
            // physically fly to, shoot at and board (World.Boarding.cs) - not an abstract HP bar.
            _shipFieldPosition = AsteroidField.Center;
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
            // _travelTargetPointId deliberately stays set - still "heading to" this station until
            // the manual docking approach (World.StationDocking.cs) actually captures the dock.
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
            _battleSectorPointId = null;
        }
        _battleFaction = null;
        _projectiles.Clear(); // nothing left to hit; shells in flight don't follow you out of the sector
        Phase = VoyagePhase.Traveling; // sector cleared - free to pick a new destination
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
        _breachedWallBlockIds.Clear();
        foreach (var room in Ship.Rooms)
            _roomOxygen[room.Id] = FullOxygen;
        RegenerateRecruitRoster();
    }

    // From the navigation console: pick a destination. Ignored mid-fight (can't flee to the
    // map) and for an unknown point id.
    private void TryStartTravel(string? pointId)
    {
        if (pointId is null || Phase == VoyagePhase.Battle)
            return;
        // Only this system's own points - nothing in the current UI ever offers another system's
        // (GalaxyPoints is scoped the same way, World.Factions.cs's CreateGalaxyPoints), and flying
        // there without a warp would be a coordinate-space coincidence, not a real destination.
        if (GalaxyMap.GetSystem(_currentSystemId).Points.All(p => p.Id != pointId))
            return;

        // Casting off takes the station's rooms out of the docked layout, so anyone still standing
        // in them would be left walking around geometry that no longer connects to anything -
        // they get pulled back through the connector into the airlock chamber instead.
        foreach (var character in _characters.Values.Where(c => c.OnStation))
        {
            character.OnStation = false;
            character.RoomId = Ship.AirlockOuterDoors.First().RoomId;
            character.Position = Ship.GetRoom(character.RoomId).Center;
        }

        Phase = VoyagePhase.Traveling;
        _dockedPointId = null;
        _travelTargetPointId = pointId;
    }
}
