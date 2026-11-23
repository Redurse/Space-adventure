namespace Anabiosis.Shared.War;

// Yards build ships out of the ore stored in them; projects build installations out of the ore stored in their node.
public sealed partial class WarSim
{
    public int SlotsOf(WarNode node) => node.Nest ? Config.VirusNestSlots : node.Yard switch
    {
        YardTier.MegaYard => Config.MegaYardSlots,
        YardTier.Yard => Config.YardSlots,
        _ => 0,
    };

    private void StepYards(double dt)
    {
        foreach (var node in _nodes)
        {
            if (node.Owner == WarFaction.None || !node.HasYard || node.Queue.Count == 0)
                continue;

            var building = 0;
            for (var i = node.Queue.Count - 1; i >= 0; i--)
            {
                var order = node.Queue[i];
                if (!order.Started)
                    continue;
                order.SecondsLeft -= (float)dt;
                if (order.SecondsLeft > 0f)
                {
                    building++;
                    continue;
                }
                node.Queue.RemoveAt(i);
                Complete(node, order);
            }

            // Start the next orders in line while there are free slots and the ore is there to pay for them.
            var slots = SlotsOf(node);
            foreach (var order in node.Queue)
            {
                if (building >= slots)
                    break;
                if (order.Started)
                    continue;
                var cost = ShipStats.Cost(order.Class);
                if (node.Stock < cost)
                    break;
                node.Stock -= cost;
                order.Started = true;
                order.SecondsLeft = ShipStats.BuildSeconds(order.Class);
                building++;
            }
        }
    }

    private void Complete(WarNode node, YardOrder order)
    {
        switch (order.Class)
        {
            case ShipClass.Freighter:
                SpawnMover(ShipClass.Freighter, node.Index);
                break;
            case ShipClass.Outposter:
            {
                var outposter = SpawnMover(ShipClass.Outposter, node.Index);
                outposter.Task = MoverTask.Found;
                SendMover(outposter, order.Target);
                break;
            }
            default:
                node.Garrison[(int)order.Class] += 1f;
                break;
        }
    }

    private void StepProjects(double dt)
    {
        foreach (var node in _nodes)
        {
            if (node.Project is not { } project)
                continue;
            if (!node.IsUnion)
            {
                node.Project = null;
                continue;
            }

            if (!project.Started)
            {
                var cost = ProjectStats.Cost(project.Kind);
                if (node.Stock < cost)
                    continue;
                node.Stock -= cost;
                project.Started = true;
                project.SecondsLeft = ProjectStats.Seconds(project.Kind);
                continue;
            }

            project.SecondsLeft -= (float)dt;
            if (project.SecondsLeft > 0f)
                continue;
            ChangeInstallations(node, () =>
            {
                switch (project.Kind)
                {
                    case ProjectKind.Colony:
                        node.Planet = PlanetKind.Colony;
                        break;
                    case ProjectKind.Yard:
                        node.Yard = YardTier.Yard;
                        break;
                    default:
                        node.Yard = YardTier.MegaYard;
                        break;
                }
            });
            node.Project = null;
        }
    }
}
