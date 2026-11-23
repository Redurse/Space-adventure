using System.Text;

namespace Anabiosis.Shared.War;

// One line of the report: the state of the war at a moment of war time.
public sealed class WarSample
{
    public double Hours { get; init; }
    public UnionCensus Union { get; init; } = new();
    public double OreMinedPerHour { get; init; }
    public double OreWastedTotal { get; init; }
    public int FreightersMoving { get; init; }
}

public sealed partial class WarSim
{
    private double _lastSampleMined;
    private double _lastSampleClock;

    // Runs the war forward by this many hours of war time with nothing drawn, calling back with a sample every so often.
    // Fast enough that a whole campaign of 100-200 hours is a matter of minutes.
    public void FastForward(double hours, double sampleEveryHours = 1.0, Action<WarSample>? onSample = null)
    {
        var end = Clock + hours * 3600.0;
        var next = Clock + sampleEveryHours * 3600.0;
        while (Clock < end - 1e-6)
        {
            var target = Math.Min(end, next);
            Step(target - Clock);
            if (Clock >= next - 1e-6 || Clock >= end - 1e-6)
            {
                onSample?.Invoke(TakeSample());
                next += sampleEveryHours * 3600.0;
            }
        }
    }

    public WarSample TakeSample()
    {
        var elapsedHours = Math.Max(1e-9, (Clock - _lastSampleClock) / 3600.0);
        var sample = new WarSample
        {
            Hours = Clock / 3600.0,
            Union = TakeCensus(),
            OreMinedPerHour = (OreMined - _lastSampleMined) / elapsedHours,
            OreWastedTotal = OreWasted,
            FreightersMoving = _movers.Count(m => m.Class == ShipClass.Freighter && !m.IsIdleFreighter),
        };
        _lastSampleMined = OreMined;
        _lastSampleClock = Clock;
        return sample;
    }
}

public static class WarReport
{
    public static string Header() =>
        "  часы | систем кол. авп. верф.(мега) | добыча/ч  запас руды | грузов. (занято) | корв. фрег. крейс. дред. | лимит флота | враг:флот(очк.) бои опер. потеряно | восстание:очк. верф.";

    public static string Row(WarSample s)
    {
        var u = s.Union;
        return string.Format(
            "{0,6:0.0} | {1,6} {2,4} {3,4} {4,4}({5,2})   | {6,8:0} {7,11:0} | {8,6} ({9,5})     | {10,5:0} {11,5:0} {12,6:0} {13,4:0} | {14,4}/{15,-5}    | {16,5}({17,4}) {18,4} {19,4} {20,7} | {21,9} {22,5}",
            s.Hours, u.Owned, u.Colonies, u.Outposts, u.Yards + u.MegaYards, u.MegaYards,
            s.OreMinedPerHour, u.OrePool, u.Freighters, s.FreightersMoving,
            u.Garrison[(int)ShipClass.Corvette], u.Garrison[(int)ShipClass.Frigate], u.Garrison[(int)ShipClass.Cruiser], u.Garrison[(int)ShipClass.Dreadnought],
            u.Supply, u.SupplyCap, u.EnemyFleets, u.EnemySupply, u.Battles, u.Operations, u.LostSystems, u.VirusSupply, u.VirusYards);
    }

    public static string Format(IEnumerable<WarSample> samples)
    {
        var text = new StringBuilder();
        text.AppendLine(Header());
        foreach (var sample in samples)
            text.AppendLine(Row(sample));
        return text.ToString();
    }
}
