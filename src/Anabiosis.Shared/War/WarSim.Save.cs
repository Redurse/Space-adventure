namespace Anabiosis.Shared.War;

// Saving and loading the war: Capture takes everything that is not rebuilt from the galaxy, Restore puts it back onto a WarSim
// built on the same galaxy (the constructor has seeded a fresh war; this overwrites all of it).
public sealed partial class WarSim
{
    public WarSave Capture()
    {
        var save = new WarSave
        {
            SystemCount = _nodes.Length,
            LayoutHash = LayoutHash(),
            Clock = Clock,
            GovernmentClock = _governmentClock,
            LogisticsClock = _logisticsClock,
            MilitaryClock = _militaryClock,
            CombatClock = _combatClock,
            VirusClock = _virusClock,
            OreMined = OreMined,
            OreWasted = OreWasted,
            NextMoverId = _nextMoverId,
            NextFleetId = _nextFleetId,
            NextOperationId = _nextOperationId,
            CapitalIndex = CapitalIndex,
            FoundingTargets = _foundingTargets.ToList(),
            Abandoned = _abandoned.ToList(),
            NestIndex = NestIndex,
            VirusRevealed = _virusRevealed,
            VirusCaution = _virusCaution,
            VirusLastTarget = _virusLastTarget,
            VirusWaitingSince = _virusWaitingSince,
            VirusExtinctSince = _virusExtinctSince,
            PrecursorIndex = PrecursorIndex,
            VirusDestroyed = VirusDestroyed,
        };

        foreach (var node in _nodes)
        {
            var saved = new WarNodeSave
            {
                Owner = node.Owner, Planet = node.Planet, Outpost = node.Outpost, Yard = node.Yard, Nest = node.Nest,
                Stock = node.Stock, Reserved = node.Reserved, Incoming = node.Incoming, Miners = node.Miners, Drones = node.Drones,
                WorkshopBuilding = node.WorkshopBuilding, WorkshopSecondsLeft = node.WorkshopSecondsLeft,
                Garrison = (float[])node.Garrison.Clone(), FortHp = node.FortHp, OccupationSeconds = node.OccupationSeconds, SinceCombat = node.SinceCombat,
            };
            foreach (var order in node.Queue)
                saved.Queue.Add(new WarOrderSave { Class = order.Class, Target = order.Target, Started = order.Started, SecondsLeft = order.SecondsLeft });
            if (node.Project is { } project)
            {
                saved.ProjectKind = project.Kind;
                saved.ProjectStarted = project.Started;
                saved.ProjectSecondsLeft = project.SecondsLeft;
            }
            save.Nodes.Add(saved);
        }

        foreach (var mover in _movers)
            save.Movers.Add(new WarMoverSave
            {
                Id = mover.Id, Class = mover.Class, Owner = mover.Owner, Node = mover.Node, Next = mover.Next, Along = mover.Along,
                LaneLength = mover.LaneLength, Route = mover.Route.ToList(), Task = mover.Task, Source = mover.Source, Sink = mover.Sink,
                Planned = mover.Planned, Cargo = mover.Cargo,
            });

        foreach (var fleet in _fleets)
            save.Fleets.Add(new WarFleetSave
            {
                Id = fleet.Id, Owner = fleet.Owner, Ships = (float[])fleet.Ships.Clone(), Node = fleet.Node, Next = fleet.Next, Along = fleet.Along,
                LaneLength = fleet.LaneLength, Route = fleet.Route.ToList(), Destination = fleet.Destination, Task = fleet.Task,
                OperationId = fleet.OperationId, Fighting = fleet.Fighting,
            });

        foreach (var op in _operations)
            save.Operations.Add(new WarOperationSave
            {
                Id = op.Id, Target = op.Target, Rally = op.Rally, Counter = op.Counter, Striking = op.Striking, Started = op.Started,
                EnemyStrength = op.EnemyStrength,
            });

        if (_mother is { } mother)
            save.Mother = new WarMotherSave
            {
                Node = mother.Node, Next = mother.Next, Along = mother.Along, LaneLength = mother.LaneLength, Route = mother.Route.ToList(),
                Destination = mother.Destination, Hp = mother.Hp, MaxHp = mother.MaxHp, Alive = mother.Alive,
            };
        return save;
    }

    private int LayoutHash()
    {
        unchecked
        {
            var hash = 17;
            for (var i = 0; i < _nodes.Length; i++)
                foreach (var lane in Galaxy.LanesOf(i))
                    hash = hash * 31 + i * 1009 + lane.To;
            return hash == 0 ? 1 : hash;
        }
    }

    // False (and nothing touched) when the save is not of this galaxy.
    public bool Restore(WarSave save)
    {
        if (save.SystemCount != _nodes.Length || save.Nodes.Count != _nodes.Length || save.LayoutHash != LayoutHash())
            return false;

        Clock = save.Clock;
        _governmentClock = save.GovernmentClock;
        _logisticsClock = save.LogisticsClock;
        _militaryClock = save.MilitaryClock;
        _combatClock = save.CombatClock;
        _virusClock = save.VirusClock;
        OreMined = save.OreMined;
        OreWasted = save.OreWasted;
        _nextMoverId = save.NextMoverId;
        _nextFleetId = save.NextFleetId;
        _nextOperationId = save.NextOperationId;
        CapitalIndex = save.CapitalIndex;

        for (var i = 0; i < _nodes.Length; i++)
        {
            var node = _nodes[i];
            var saved = save.Nodes[i];
            node.Owner = saved.Owner;
            node.Planet = saved.Planet;
            node.Outpost = saved.Outpost;
            node.Yard = saved.Yard;
            node.Nest = saved.Nest;
            node.Stock = saved.Stock;
            node.Reserved = saved.Reserved;
            node.Incoming = saved.Incoming;
            node.Miners = saved.Miners;
            node.Drones = saved.Drones;
            node.WorkshopBuilding = saved.WorkshopBuilding;
            node.WorkshopSecondsLeft = saved.WorkshopSecondsLeft;
            node.FortHp = saved.FortHp;
            node.OccupationSeconds = saved.OccupationSeconds;
            node.SinceCombat = saved.SinceCombat;
            Array.Clear(node.Garrison);
            Array.Copy(saved.Garrison, node.Garrison, Math.Min(saved.Garrison.Length, node.Garrison.Length));
            node.Queue.Clear();
            foreach (var order in saved.Queue)
                node.Queue.Add(new YardOrder { Class = order.Class, Target = order.Target, Started = order.Started, SecondsLeft = order.SecondsLeft });
            node.Project = saved.ProjectKind is { } kind
                ? new WarProject { Kind = kind, Started = saved.ProjectStarted, SecondsLeft = saved.ProjectSecondsLeft }
                : null;
            node.Fighting = false;
        }

        _movers.Clear();
        foreach (var saved in save.Movers)
        {
            var mover = new WarMover
            {
                Id = saved.Id, Class = saved.Class, Owner = saved.Owner, Node = saved.Node, Next = saved.Next, Along = saved.Along,
                LaneLength = saved.LaneLength, Task = saved.Task, Source = saved.Source, Sink = saved.Sink, Planned = saved.Planned, Cargo = saved.Cargo,
            };
            foreach (var step in saved.Route)
                mover.Route.Enqueue(step);
            _movers.Add(mover);
        }

        _fleets.Clear();
        foreach (var saved in save.Fleets)
        {
            var fleet = new WarFleet
            {
                Id = saved.Id, Owner = saved.Owner, Node = saved.Node, Next = saved.Next, Along = saved.Along, LaneLength = saved.LaneLength,
                Destination = saved.Destination, Task = saved.Task, OperationId = saved.OperationId, Fighting = saved.Fighting,
            };
            Array.Copy(saved.Ships, fleet.Ships, Math.Min(saved.Ships.Length, fleet.Ships.Length));
            foreach (var step in saved.Route)
                fleet.Route.Enqueue(step);
            _fleets.Add(fleet);
        }

        _operations.Clear();
        foreach (var saved in save.Operations)
            _operations.Add(new Operation
            {
                Id = saved.Id, Target = saved.Target, Rally = saved.Rally, Counter = saved.Counter, Striking = saved.Striking,
                Started = saved.Started, EnemyStrength = saved.EnemyStrength,
            });

        _foundingTargets.Clear();
        foreach (var target in save.FoundingTargets)
            _foundingTargets.Add(target);
        _abandoned.Clear();
        foreach (var target in save.Abandoned)
            _abandoned.Add(target);

        NestIndex = save.NestIndex;
        _virusRevealed = save.VirusRevealed;
        _virusCaution = save.VirusCaution;
        _virusLastTarget = save.VirusLastTarget;
        _virusWaitingSince = save.VirusWaitingSince;
        _virusExtinctSince = save.VirusExtinctSince;
        PrecursorIndex = save.PrecursorIndex;
        VirusDestroyed = save.VirusDestroyed;

        if (save.Mother is { } mother)
        {
            _mother = new WarMotherShip { Node = mother.Node, Next = mother.Next, Along = mother.Along, LaneLength = mother.LaneLength, Destination = mother.Destination, Hp = mother.Hp, MaxHp = mother.MaxHp, Alive = mother.Alive };
            foreach (var step in mother.Route)
                _mother.Route.Enqueue(step);
        }
        else
        {
            _mother = null;
        }
        return true;
    }
}
