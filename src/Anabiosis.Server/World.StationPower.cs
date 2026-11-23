using Anabiosis.Server.Electrical;
using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// The docked station's own power plant. The start outpost has the reactor, battery and distribution box the author drew into it
// (Station.Fixtures.cs); while the ship is berthed they can feed the ship's grid ("shore power"): the station reactor is one more source on the
// same PowerNetwork the ship's own reactor sits on, so the ship's reactor is asked for less (and burns less fuel) and the battery charges from the
// spare. It is the player's choice - the panel on the station's reactor/shield box has a connect/cut button - and it is off by default, so nothing
// about the ship's own power changes until it is plugged in. Undocking cuts it by itself.
public sealed partial class World
{
    // What the station's reactor can supply the ship's grid - far more than the ship's own reactor, so the ship's one idles while connected.
    private const float ShoreCapacity = 240f;
    private const string ShoreDeviceId = "shore";
    private const string DistributionComponentId = "distribution";

    private bool _shoreConnected;
    private float _shoreDelivered;

    // Docked at a living station that has a reactor of its own.
    private bool ShoreAvailable => IsDocked && !Station.IsAbandoned && Station.Fixtures.Reactors.Count > 0;

    // The shore feed is in the grid this tick.
    private bool ShoreActive => _shoreConnected && ShoreAvailable;

    // Plugs the ship into the station's reactor or pulls the plug - standing at the station's reactor, power box, battery or a junction box.
    private void HandleToggleShorePower(Character character)
    {
        if (!ShoreAvailable || !NearStationPowerDevice(character))
            return;
        _shoreConnected = !_shoreConnected;
    }

    private bool NearStationPowerDevice(Character character)
    {
        var fixtures = Station.Fixtures;
        return fixtures.Reactors.Any(r => (r.Position - character.Position).Length() < InteractionConstants.ReactorReach(r.SizeScale))
            || fixtures.Distributions.Any(d => (d.Position - character.Position).Length() < InteractionRadius)
            || fixtures.Batteries.Any(b => (b.Position - character.Position).Length() < InteractionRadius)
            || fixtures.JunctionBoxes.Any(j => (j.Position - character.Position).Length() < InteractionRadius);
    }

    private StationPowerState? CreateStationPowerState() =>
        Station.Fixtures.Reactors.Count == 0
            ? null
            : new StationPowerState(ShoreAvailable, ShoreActive, ShoreCapacity, ShoreActive ? _shoreDelivered : 0f);

    // Test hook: plug the ship in without walking to the station's reactor.
    public void DebugSetShorePower(bool connected) => _shoreConnected = connected;
}
