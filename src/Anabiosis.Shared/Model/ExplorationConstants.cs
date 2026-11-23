namespace Anabiosis.Shared.Model;

public static class ExplorationConstants
{
    // How far the ship's own sensors see: the fog of war clears in this radius around the hull, and a hidden
    // place inside it is identified. Shared by the server (World.Exploration.cs) and the map's sensor ring.
    public const float ShipSensorRadius = 320f;
}
