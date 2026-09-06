using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Each locker holds exactly one physical suit now, instead of the old E-key toggle that let the
// whole crew re-equip from the same spot forever with nothing ever run out ("убери бесконечные
// скафандры"). Taking one (World.Interact.cs) empties that locker until someone walks up wearing
// a suit and puts it back - the same "pull it out, it's gone until returned" shape as the
// reactor's rod slots or a ComponentMount, just with a bool instead of an installed part.
public sealed partial class World
{
    private readonly Dictionary<string, bool> _suitLockerHasSuit = new();

    // Called from InitializeShipState (constructor + every hull swap) - a bought hull starts every
    // locker stocked, same as a starting one.
    private void InitializeSuitLockers()
    {
        _suitLockerHasSuit.Clear();
        foreach (var locker in Ship.SuitLockers)
            _suitLockerHasSuit[locker.Id] = true;
    }

    private bool SuitLockerHasSuit(string lockerId) => _suitLockerHasSuit.GetValueOrDefault(lockerId, true);

    private void SetSuitLockerHasSuit(string lockerId, bool hasSuit) => _suitLockerHasSuit[lockerId] = hasSuit;

    private List<SuitLockerState> CreateSuitLockerStates() =>
        Ship.SuitLockers.Select(l => new SuitLockerState(l.Id, SuitLockerHasSuit(l.Id))).ToList();
}
