using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Storage pods, abandoned ships and ship graveyards (SalvagePoints.cs): fly within a point's own
// CaptureRadius and it is looted on the spot, once. Credits go to the crew, Hyperium to the ship's
// fuel reserve. Not persisted by saves - a reload finds them again.
public sealed partial class World
{
    private const double SalvageNoticeSeconds = 6.0;

    private readonly HashSet<string> _salvagedPointIds = new();
    private string? _salvageNotice;
    private double _salvageNoticeSecondsLeft;

    public IReadOnlyCollection<string> SalvagedPointIds => _salvagedPointIds;
    public string? SalvageNotice => _salvageNoticeSecondsLeft > 0 ? _salvageNotice : null;

    private void StepSalvage(double deltaSeconds)
    {
        if (_salvageNoticeSecondsLeft > 0)
            _salvageNoticeSecondsLeft -= deltaSeconds;
        if (IsDocked || IsLandedOnPlanet)
            return;

        foreach (var point in GalaxyMap.GetSystem(_currentSystemId).Points)
        {
            if (!SalvagePoints.IsSalvage(point.Kind) || _salvagedPointIds.Contains(point.Id))
                continue;
            if ((point.Position - _shipFieldPosition).Length() > point.CaptureRadius)
                continue;

            var loot = SalvagePoints.LootFor(point);
            _salvagedPointIds.Add(point.Id);
            Credits += loot.Credits;
            _hyperiumReserve += loot.Hyperium;
            _salvageNotice = loot.Hyperium > 0
                ? $"{point.Name}: +{loot.Credits} кредитов, +{loot.Hyperium} гиперия"
                : $"{point.Name}: +{loot.Credits} кредитов";
            _salvageNoticeSecondsLeft = SalvageNoticeSeconds;
        }
    }
}
