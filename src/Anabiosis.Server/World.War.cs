using Anabiosis.Shared.Protocol;
using Anabiosis.Shared.War;

namespace Anabiosis.Server;

// The galactic war (Anabiosis.Shared/War) as a part of the running game: it is built once for a real session, stepped with the
// world clock - at the speed the map asks for - and summed up into the snapshot properties the galactic map draws.
// A test world never builds it (a whole galaxy of 4000 systems costs too much to create for every test), and neither does the
// tutorial. The war is not saved yet: a loaded game starts it over.
public sealed partial class World
{
    private static readonly int[] WarSpeedLevels = { 0, 1, 10, 100, 1000, 10000 };
    private const double WarSnapshotIntervalSeconds = 0.25;

    private WarSim? _war;
    // The war from a loaded save, put back when the war is switched on (EnableWar).
    private WarSave? _pendingWarSave;
    private int _warSpeedLevel = 1;
    private double _warSnapshotClock;
    private IReadOnlyList<WarLaneState>? _warLanes;
    private IReadOnlyList<WarNodeState>? _warNodes;
    private IReadOnlyList<WarMoverState>? _warMovers;
    private WarClockState? _warClock;
    private IReadOnlyList<WarFleetState>? _warFleets;
    private WarMotherState? _warMother;

    public WarSim? War => _war;
    public int WarSpeedLevel => _warSpeedLevel;

    // Builds the war: the whole galaxy has to exist for it, so the rest of it is generated here.
    public void EnableWar()
    {
        if (_war is not null)
            return;
        GalaxyMap.EnsureAtLeast(Anabiosis.Shared.Model.GalaxyMap.MaxProceduralSystems); // all 4000 stars: about two seconds
        _war = new WarSim(WarGalaxy.FromGalaxyMap(GalaxyMap), new WarConfig { Seed = GalaxyMap.WorldSeed });
        if (_pendingWarSave is { } pending)
        {
            _war.Restore(pending);
            _pendingWarSave = null;
        }
        RefreshWarSnapshot();
    }

    public void SetWarSpeed(int level)
    {
        if (Array.IndexOf(WarSpeedLevels, level) >= 0)
            _warSpeedLevel = level;
    }

    private void StepWar(double deltaSeconds)
    {
        if (_war is null)
            return;
        if (_warSpeedLevel > 0)
            _war.Step(deltaSeconds * _warSpeedLevel);
        _warSnapshotClock += deltaSeconds;
        if (_warSnapshotClock >= WarSnapshotIntervalSeconds)
        {
            _warSnapshotClock = 0;
            RefreshWarSnapshot();
        }
    }

    private void RefreshWarSnapshot()
    {
        var war = _war!;
        var galaxy = war.Galaxy;

        if (_warLanes is null)
        {
            var lanes = new List<WarLaneState>();
            for (var i = 0; i < galaxy.Count; i++)
                foreach (var lane in galaxy.LanesOf(i))
                    if (lane.To > i)
                        lanes.Add(new WarLaneState((short)i, (short)lane.To));
            _warLanes = lanes.ToArray();
        }

        var nodes = new WarNodeState[war.Nodes.Count];
        for (var i = 0; i < nodes.Length; i++)
        {
            var node = war.Nodes[i];
            var fleet = (int)Math.Round(CombatStats.Supply(node.Garrison));
            var project = node.Project is { } p ? (byte)((int)p.Kind + 1) : (byte)0;
            nodes[i] = new WarNodeState((byte)node.Owner, node.Nest ? (byte)3 : (byte)node.Planet, (byte)node.Yard, node.Outpost, project,
                (int)node.Stock, (short)node.Miners, (short)node.Drones, (short)fleet,
                galaxy.Systems[i].Richness, (byte)galaxy.Systems[i].PlanetCount, galaxy.Systems[i].HasRockyPlanet,
                (byte)(war.FortMax(node) > 0f ? Math.Clamp(100f * node.FortHp / war.FortMax(node), 0f, 100f) : 0f), node.Fighting,
                (byte)Math.Clamp(100f * node.OccupationSeconds / war.OccupationNeeded(node), 0f, 100f));
        }
        _warNodes = nodes;

        var movers = new WarMoverState[war.Movers.Count];
        for (var i = 0; i < movers.Length; i++)
        {
            var mover = war.Movers[i];
            movers[i] = mover.Standing
                ? new WarMoverState(mover.Id, (byte)mover.Class, (byte)mover.Owner, (short)mover.Node, (short)mover.Node, 0f, 0f, 0f)
                : new WarMoverState(mover.Id, (byte)mover.Class, (byte)mover.Owner, (short)mover.Node, (short)mover.Next,
                    mover.Along / mover.LaneLength, mover.LaneLength, war.SpeedOf(mover));
        }
        _warMovers = movers;

        var fleets = new WarFleetState[war.Fleets.Count];
        for (var i = 0; i < fleets.Length; i++)
        {
            var fleet = war.Fleets[i];
            fleets[i] = new WarFleetState(fleet.Id, (byte)fleet.Owner, (short)fleet.Node, (short)(fleet.Standing ? fleet.Node : fleet.Next),
                fleet.Standing ? 0f : fleet.Along / fleet.LaneLength, fleet.LaneLength, fleet.Speed, (int)Math.Round(fleet.Supply),
                (byte)fleet.Task, fleet.Fighting);
        }
        _warFleets = fleets;

        _warMother = war.Mother is { } mother
            ? new WarMotherState((short)mother.Node, (short)(mother.Standing ? mother.Node : mother.Next), mother.Standing ? 0f : mother.Along / mother.LaneLength,
                mother.LaneLength, war.Config.MotherSpeed, (byte)Math.Clamp(100f * mother.Hp / mother.MaxHp, 0f, 100f), mother.Alive, mother.Fighting)
            : null;

        var census = war.TakeCensus();
        _warClock = new WarClockState(war.Clock / 3600.0, _warSpeedLevel, census.Owned, census.Colonies,
            census.Yards + census.MegaYards, census.Supply, census.Freighters, census.OrePool,
            war.Nodes.Count(n => n.Owner == WarFaction.Virus), census.EnemyFleets, census.Battles, census.Operations);
    }
}
