namespace Anabiosis.Shared.War;

// Ships on the lanes.
public sealed partial class WarSim
{
    public float SpeedOf(WarMover mover) => mover.Class == ShipClass.Outposter ? Config.OutposterSpeed : Config.FreighterSpeed;

    private void StepMovers(double dt)
    {
        for (var i = _movers.Count - 1; i >= 0; i--)
        {
            var mover = _movers[i];
            var left = SpeedOf(mover) * dt;
            while (left > 1e-6 && !mover.Standing)
            {
                var room = mover.LaneLength - mover.Along;
                if (left < room)
                {
                    mover.Along += (float)left;
                    left = 0;
                }
                else
                {
                    left -= room;
                    Arrive(mover);
                }
            }
        }
    }

    // Sends a standing ship to a system by the shortest route. A ship already there is at its destination at once.
    private void SendMover(WarMover mover, int target)
    {
        if (!mover.Standing)
            return;
        if (mover.Node == target)
        {
            ReachedEnd(mover);
            return;
        }
        var path = Galaxy.Path(mover.Node, target);
        mover.Route.Clear();
        for (var i = 1; i < path.Count; i++)
            mover.Route.Enqueue(path[i]);
        BeginLane(mover, path[0]);
    }

    private void BeginLane(WarMover mover, int next)
    {
        mover.Next = next;
        mover.LaneLength = Galaxy.LaneLength(mover.Node, next);
        mover.Along = 0f;
    }

    private void Arrive(WarMover mover)
    {
        mover.Node = mover.Next;
        mover.Along = 0f;
        if (mover.Route.Count > 0)
        {
            BeginLane(mover, mover.Route.Dequeue());
            return;
        }
        mover.Next = -1;
        mover.LaneLength = 0f;
        ReachedEnd(mover);
    }

    // A ship has come to the end of its route: what it does there depends on its task.
    private void ReachedEnd(WarMover mover)
    {
        var node = _nodes[mover.Node];
        switch (mover.Task)
        {
            case MoverTask.ToSource:
            {
                if (!node.IsUnion)
                {
                    _nodes[mover.Sink].Incoming = Math.Max(0f, _nodes[mover.Sink].Incoming - mover.Planned);
                    mover.Planned = 0f;
                    mover.Task = MoverTask.Idle;
                    return;
                }
                // Collect what is there (the workshop may have spent some since the freighter was sent) and carry it on.
                var take = Math.Min(mover.Planned, node.Stock);
                node.Stock -= take;
                node.Reserved = Math.Max(0f, node.Reserved - mover.Planned);
                _nodes[mover.Sink].Incoming = Math.Max(0f, _nodes[mover.Sink].Incoming - (mover.Planned - take));
                mover.Cargo = take;
                mover.Planned = take;
                if (take <= 0f)
                {
                    mover.Task = MoverTask.Idle;
                    return;
                }
                mover.Task = MoverTask.Deliver;
                SendMover(mover, mover.Sink);
                break;
            }
            case MoverTask.Deliver:
            {
                node.Incoming = Math.Max(0f, node.Incoming - mover.Cargo);
                var space = Math.Max(0f, StockCap(node) - node.Stock);
                var stored = node.IsUnion ? Math.Min(mover.Cargo, space) : 0f;
                node.Stock += stored;
                OreWasted += mover.Cargo - stored;
                mover.Cargo = 0f;
                mover.Planned = 0f;
                mover.Task = MoverTask.Idle;
                break;
            }
            case MoverTask.Found:
                FoundOutpost(mover, node);
                break;
        }
    }

    // An outposter that arrives at a free system founds an outpost there and is used up; if someone beat it to the place it
    // just waits there to be sent elsewhere.
    private void FoundOutpost(WarMover mover, WarNode node)
    {
        _foundingTargets.Remove(node.Index);
        mover.Task = MoverTask.Idle;
        if (node.Owner != WarFaction.None)
            return;
        node.Owner = WarFaction.Union;
        ChangeInstallations(node, () => node.Outpost = true);
        node.Drones = 1;
        node.Stock = 0f;
        _movers.Remove(mover);
    }
}
