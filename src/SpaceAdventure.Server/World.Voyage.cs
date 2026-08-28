using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Flight inside the current star system (game_design.md section 5, M39): there is no autopilot and
// no discrete "arrival" event any more - the ship is always hand-flown (World.ShipField.cs), and
// docking/combat are both just continuous facts about where it physically is right now, checked
// every tick, rather than an exclusive mode the ship switches into. Warping to a different system
// (World.StarSystems.cs's TryWarpTo) is the one remaining near-instant transition, and it isn't
// touched by any of this - it swaps which system's field StepVoyage even looks at.
public sealed partial class World
{
    // A station's own defensive response when you've made a real enemy of whoever runs it -
    // smaller than a dedicated hostile sector's own baseline (game_design.md section 12's squadron
    // sizes are a difficulty gradient for sectors built to be fought; a station turning hostile is
    // a consequence of standing, not a designed encounter), still scaled the same way by
    // SquadronSizeAdjustment underneath.
    private const int StationDefenseSquadronSize = 1;

    // How far the ship has to fly from whatever pulled it into a fight before that fight lets go
    // and _battleSectorPointId clears - the same "you can always just leave" rule for a hostile
    // sector's squadron or a hostile station's own defenders alike (game_design.md's own "в них
    // можно летать" turned into an actual boundary instead of a one-way trip that only ends by
    // winning). Measured from the sector/station's own marker, not from the (actively pursuing)
    // enemy ships - a squadron's formation AI tracks the ship's own position closely
    // (World.EnemyFleet.cs's SteerEnemy), so distance to them barely opens even during a real
    // retreat; every battle starts within the marker's own small CaptureRadius(8) by construction,
    // so reaching this takes a real, sustained retreat either way. A fraction of the CURRENT
    // system's own field size (M50 - each system's field is now sized to its own real generated
    // planets, no longer one shared 4800x4800) rather than an absolute constant - preserves the
    // same ratio the field's own doubling history already established (560/4800, 280/2400,
    // 35/300 - always ~11.7% of the field's own width) instead of hand-picking a new number every
    // time a system's scale changes.
    private const float BattleFleeDistanceFraction = 560f / 4800f;
    private float BattleFleeDistance => AsteroidField.Width * BattleFleeDistanceFraction;
    // However a fight is left, the ship should land somewhere flyable, not pinned to the system's
    // own outer boundary - NudgeAwayFromFieldEdge is the safety net underneath BattleFleeDistance,
    // in case a future system's own layout ever puts a marker's escape route that close to the wall.
    // Same field-size-fraction treatment as BattleFleeDistance above (192/4800, always 4%).
    private const float FieldEdgeSafetyMarginFraction = 192f / 4800f;
    private float FieldEdgeSafetyMargin => AsteroidField.Width * FieldEdgeSafetyMarginFraction;

    public GalaxyMap GalaxyMap { get; } = GalaxyMap.CreateStarter();

    // Which StarSystem the ship is currently in (World.StarSystems.cs) - changed only by warp;
    // AsteroidField resolves through it rather than through a single stored instance.
    private string _currentSystemId = "";
    private string? _dockedPointId;
    // Whichever Station point in the current system the ship is nearest to right now
    // (UpdateNearestStation, recomputed every tick while undocked) - drives which hull Station
    // resolves to (World.cs's CurrentStationKind) and which point CanDockNow/TryDockAtStation dock
    // into. Any station in the system can be docked at just by flying up to it; nothing has to be
    // deliberately targeted first.
    private string? _nearestStationPointId;
    // Who owns the ship currently being fought - captured when the fight starts, since the sector's
    // (or station's) point id is no longer around by the time it ends (World.Factions.cs).
    private FactionId? _battleFaction;
    private string? _battleSectorPointId; // same reason, for bounty quests (World.Quests.cs)
    // The sector or station just disengaged from (ResolveEnemyLosses/FleeBattle) - excluded from
    // the proximity scans above (TryEngageHostileSector, UpdateNearestStation's own defense check)
    // until the ship gets safely clear of it, even though the small position nudge alone isn't
    // enough: simply sitting still nearby would otherwise restart the same fight instantly. Cleared
    // once far enough away (StepVoyage).
    private string? _recentlyDisengagedSectorId;
    private int _remainingEnemyShips; // hulls of the squadron still flying
    private string? _crewShipId; // which enemy hull the current boarding crew belongs to

    private void StepVoyage(double deltaSeconds)
    {
        // M57 follow-up - "телепортирует в другую часть орбиты": a docked ship used to sit at a
        // single _shipFieldPosition frozen for however long the crew stayed docked (this early
        // return skipped StepShipFieldPhysics entirely) while the STATION itself kept right on
        // orbiting in the background - real time at a station (shopping, repairs, a quest, or just
        // idling with time acceleration running) is completely routine, and left the ship's own
        // frozen coordinate stale by however far the station had since moved. Undock() used to
        // paper over that with a one-time "catch up to the station's current position" snap right
        // at the moment of casting off - correct in that it kept the two together, but it read as a
        // sudden teleport to a genuinely different point along the same orbit, which is exactly
        // what it was. Keeping the ship's own field position pinned to the docked point's LIVE
        // position every tick instead - not just once at Undock() - means there's nothing left to
        // catch up on: the ship was already sitting exactly where the station now is, the whole
        // time, so undocking is a smooth continuation instead of a jump.
        if (IsDocked)
        {
            if (_dockedPointId is { } dockedId)
                _shipFieldPosition = ResolveGalaxyPointPosition(GalaxyMap.GetPoint(dockedId));
            return; // still sits at the berth until the "Стыковка" button casts off (Undock)
        }

        // M55 - landed on a planet: the ship still needs to drive around its own small surface
        // field (StepShipFieldPhysics itself branches on _landedBodyId for that), but everything
        // below reasons about the SYSTEM field (nearest station, battles, quests) - all of it
        // meaningless (and numerically nonsensical, tiny surface coordinates vs system-scale ones)
        // while sitting on a body's own ground. TakeOff (World.PlanetLanding.cs) is the only way
        // back into any of this, same shape as Undock ending IsDocked's own early return above.
        if (IsLandedOnPlanet)
        {
            StepShipFieldPhysics(deltaSeconds);
            return;
        }

        StepShipFieldPhysics(deltaSeconds);
        UpdateNearestStation();

        if (_battleSectorPointId is not null)
        {
            ResolveEnemyLosses();
            if (HasFledTheSector())
                FleeBattle();
        }
        // A persistent Military NPC's fight (World.NpcShips.cs) instead of a sector/station's -
        // mutually exclusive with the branch above, never both set at once.
        else if (_battleNpcShipId is not null)
        {
            ResolveNpcBattleLosses();
            if (HasFledTheNpcBattle())
                FleeNpcBattle();
        }
        else
        {
            TryEngageHostileSector();
        }

        // Re-arm the just-disengaged sector/station once the ship is well clear of it (3x its own
        // radius, comfortably more than sitting nearby could plausibly still count as) - otherwise
        // it would stay permanently immune to ever catching this ship again.
        if (_recentlyDisengagedSectorId is { } clearedId &&
            (ResolveGalaxyPointPosition(GalaxyMap.GetPoint(clearedId)) - _shipFieldPosition).Length() > GalaxyMap.GetPoint(clearedId).CaptureRadius * 3f)
            _recentlyDisengagedSectorId = null;
    }

    // Keeps the (shared, per-kind) Station structure anchored to whichever Station point in the
    // current system the ship is closest to, recomputed every tick - the same "dock at any station
    // by proximity" behavior CanDockNow/TryDockAtStation already assume. A controlling faction's own
    // station stands down peacefully unless the crew has actually made an enemy of it
    // (game_design.md section 12's "relatively safe" territory, M37) - but that cuts both ways: fall
    // to real hostility while closing on it and the same welcome a roaming sector's raiders give you
    // meets you here too, not just a locked airlock. World.StationDocking.cs's own WarThreshold
    // refusal is the deeper, "won't even talk to you" floor underneath this - this is the shallower
    // one that still lets you approach, just not peacefully.
    private void UpdateNearestStation()
    {
        var nearest = GalaxyMap.GetSystem(_currentSystemId).Points
            .Where(p => p.Kind == GalaxyPointKind.Station)
            .OrderBy(p => (ResolveGalaxyPointPosition(p) - _shipFieldPosition).Length())
            .FirstOrDefault();
        _nearestStationPointId = nearest?.Id;
        if (nearest is null)
            return;

        var nearestPosition = ResolveGalaxyPointPosition(nearest);
        Station.RepositionTo(nearestPosition);

        if (_battleSectorPointId is null && nearest.Id != _recentlyDisengagedSectorId &&
            (nearestPosition - _shipFieldPosition).Length() <= nearest.CaptureRadius &&
            GetStanding(OwnerOf(nearest.Id)) <= FactionDefinitions.HostileThreshold)
            StartBattle(nearest, StationDefenseSquadronSize);
    }

    // Flying within a hostile sector's own capture radius starts the fight, wherever the ship was
    // actually headed - game_design.md's two-tier map, "у каждой точки интереса есть радиус когда
    // она подхватывает игрока", now checked continuously instead of on a discrete arrival.
    private void TryEngageHostileSector()
    {
        var caught = GalaxyMap.GetSystem(_currentSystemId).Points
            .Where(p => p.Kind == GalaxyPointKind.HostileSector && p.Id != _recentlyDisengagedSectorId)
            .FirstOrDefault(p => (p.Position - _shipFieldPosition).Length() <= p.CaptureRadius);
        if (caught is not null)
            StartBattle(caught, caught.SquadronSize);
    }

    // Battle happens in real field space, so the enemy hulls are somewhere you can physically fly
    // to, shoot at and board (World.Boarding.cs) - not an abstract HP bar. Starts wherever the ship
    // already was, not re-centred on the system - snapping the ship across the map to fight reads
    // as a teleport bug, not "you got jumped". Size adjusts with standing (World.Factions.cs) -
    // hated crews draw a bigger welcome, loved ones a smaller one.
    private void StartBattle(GalaxyPoint point, int squadronSize)
    {
        _battleFaction = OwnerOf(point.Id);
        _battleSectorPointId = point.Id;
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipRotationDegrees = 0f;
        _shipAutoStabilize = true;
        SpawnEnemySquadron(Math.Max(1, squadronSize + SquadronSizeAdjustment(OwnerOf(point.Id))));
        ResetEnemyCrew(); // a fresh enemy ship means a fresh crew to board through
        _crewShipId = BoardableEnemy?.Id;
    }

    // Kills resolved, however they happened - shelled from a turret or cleared room by room by a
    // boarding party. Every ship destroyed costs its owner standing (game_design.md section 12 -
    // "групповые вражеские встречи"); only clearing the last one counts as taking the sector, so
    // that's when a bounty on it is satisfied. The whole squadron is on the board at once now, so
    // this counts what's left rather than dealing the next hull in.
    private void ResolveEnemyLosses()
    {
        var stillFlying = _enemyShips.Count(e => e.Alive);
        // A station's own defensive squadron (UpdateNearestStation, M37) only exists because
        // standing was already hostile - it isn't a raid on anyone's territory. Charging it the
        // same per-kill standing/war-effort cost as attacking a hostile sector out in the field
        // double-counts that same hostility, turning the mere act of approaching a disliked
        // station into an involuntary slide toward war (and, at outpost-gamma specifically,
        // toward flipping the very station being approached) that the raid-based standing model
        // was never meant to produce on its own.
        var isStationDefense = _battleSectorPointId is { } defendedId &&
            GalaxyMap.GetPoint(defendedId).Kind == GalaxyPointKind.Station;
        for (var killed = _remainingEnemyShips - stillFlying; killed > 0; killed--)
            if (_battleFaction is { } faction && !isStationDefense)
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
            // The fight doesn't recentre the ship on the system - it stays wherever it was caught
            // (TryEngageHostileSector/UpdateNearestStation), which means it can still be sitting
            // inside this very marker's own CaptureRadius the moment the fight ends. Left alone, the
            // very next tick would catch it again and restart the fight instantly, forever. Nudge it
            // just clear of that radius instead - reads as "disengaging", not a teleport, and small
            // enough that a real player would barely notice it.
            var sector = GalaxyMap.GetPoint(sectorId);
            var awayFromSector = _shipFieldPosition - sector.Position;
            if (awayFromSector.Length() < sector.CaptureRadius + 1f)
            {
                var direction = awayFromSector.Length() > 0.01f ? awayFromSector.Normalized() : new Vec2(1f, 0f);
                SetShipFieldPosition((sector.Position + direction * (sector.CaptureRadius + 1f))
                    .Clamp(0, 0, AsteroidField.Width, AsteroidField.Height));
            }
            // The nudge above only guarantees clearance along the one line it pushed along - the
            // exclusion (StepVoyage) is what actually protects against just drifting back within
            // this marker's radius regardless of direction.
            _recentlyDisengagedSectorId = sectorId;
            _battleSectorPointId = null;
        }
        _battleFaction = null;
        _projectiles.Clear(); // nothing left to hit; shells in flight don't follow you out of the sector
    }

    // True once the ship has actually flown clear of whatever's fighting it, not merely clear of
    // the ships themselves - their own formation AI keeps them tracking the ship's current position
    // closely (SteerEnemy), so measuring against them barely moves even during a real retreat. The
    // marker is a fixed point every battle starts within CaptureRadius of, so this only fires after
    // genuinely putting distance behind, regardless of how tightly the squadron itself manages to
    // keep pace.
    // M58 follow-up - this same method also resolves a STATION's own defensive-squadron battle
    // (UpdateNearestStation, not just a hostile sector's), and a hosted station genuinely orbits now
    // (M52) - its raw GalaxyPoint.Position is a static phase-zero anchor, not where the fight is
    // actually happening. Measuring against that stale anchor instead of the live position made
    // every station-defense battle read as "fled" on the very same tick it started (the anchor sits
    // however far the station has swept since spawn, routinely past BattleFleeDistance on its own),
    // instantly clearing the fight before anything else could ever see it. ResolveGalaxyPointPosition
    // is safe here for hostile sectors too - GalaxyPoint.PositionAt falls back to the raw X/Y
    // unchanged for anything without a host body (ResolveGalaxyPointPosition's own doc comment).
    private bool HasFledTheSector() =>
        _battleSectorPointId is { } sectorId &&
        (ResolveGalaxyPointPosition(GalaxyMap.GetPoint(sectorId)) - _shipFieldPosition).Length() > BattleFleeDistance;

    // Running rather than winning - the squadron is still out there, so no bounty progress, no
    // standing hit for them, nothing recorded as destroyed. Same anti-recapture bookkeeping a won
    // fight uses (_recentlyDisengagedSectorId) so the very next tick doesn't instantly catch the
    // ship again if it happens to still be within the marker's own CaptureRadius - flying back in
    // later legitimately restarts the same fight, exactly as it should.
    private void FleeBattle()
    {
        _recentlyDisengagedSectorId = _battleSectorPointId;
        _battleSectorPointId = null;
        _battleFaction = null;
        _projectiles.Clear();
        NudgeAwayFromFieldEdge();
    }

    // Pulls the ship back off the system's own outer boundary if disengaging left it sitting right
    // on (or clamped against) the edge - BattleFleeDistance shouldn't require touching the wall to
    // satisfy, but this is the safety net in case some future system's layout ever does.
    private void NudgeAwayFromFieldEdge()
    {
        SetShipFieldPosition(new Vec2(
            Math.Clamp(_shipFieldPosition.X, FieldEdgeSafetyMargin, AsteroidField.Width - FieldEdgeSafetyMargin),
            Math.Clamp(_shipFieldPosition.Y, FieldEdgeSafetyMargin, AsteroidField.Height - FieldEdgeSafetyMargin)));
    }

    // Station stop (game_design.md Phase1: "станция для дозаправки/ремонта") — tops fuel back
    // up, welds every hull breach and refills every room's air.
    private void EnterStation(string pointId)
    {
        // Mated, not merely "at the station": squared up on the berth so the ship's airlock and the
        // station's connector are one rectangle (World.StationDocking.cs). Set here rather than
        // only in TryDockAtStation so the game's own opening state - already docked at the home
        // station - lines up the same way as one the player flew in.
        _shipRotationDegrees = 0f;
        SetShipFieldPosition(DockBerthPosition);
        _dockedPointId = pointId;
        AutosavePending = true; // game_design.md section 5 - docking is the save point
        ResetStationCrimeState(); // a new visit finds the crates restocked and the guards calm
        PowerGrid.Reactor.Refuel();
        RefillOxygenTanks(); // a station resupplies air the same way it refuels and repairs
        InitializeWallBlocks(); // a station patches the hull back to full the same way
        RestockAmmoStorages(); // and tops the ammo crates back up too
        RestockHullPlating(); // M62 - and the hull-plating hold, same restock timing
        foreach (var room in Ship.Rooms)
            _roomOxygen[room.Id] = FullOxygen;
        RegenerateRecruitRoster();
    }
}
