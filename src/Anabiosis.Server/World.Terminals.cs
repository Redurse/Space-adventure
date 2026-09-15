using System.Collections.Generic;
using System.Linq;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// Direct user request ("это в будущем будет одно из главных устройств, их будет много") - many
// independent wall terminals per hull, each with its own on/off, replacing the old single
// Ship.Terminal/World.TerminalOn pair. Same "Dictionary<id, state> + list-of-states snapshot" shape
// World.SuitLockers.cs already uses for its own multi-instance device.
public sealed partial class World
{
    private readonly Dictionary<string, bool> _terminalOn = new();

    // Called from InitializeShipState (constructor + every hull swap) - a bought hull starts every
    // terminal off, same as a starting one.
    private void InitializeTerminals()
    {
        _terminalOn.Clear();
        foreach (var terminal in Ship.Terminals)
            _terminalOn[terminal.Id] = false;
    }

    private bool TerminalOn(string terminalId) => _terminalOn.GetValueOrDefault(terminalId, false);

    private void SetTerminalOn(string terminalId, bool on) => _terminalOn[terminalId] = on;

    private List<TerminalState> CreateTerminalStates() =>
        Ship.Terminals.Select(t => new TerminalState(t, TerminalOn(t.Id))).ToList();
}
