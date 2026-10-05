using Anabiosis.Shared.Model;

namespace Anabiosis.Server;

// Cosmoteer-style heat around the system's star: every compartment of a ship inside the ring takes
// damage scaled by how deep it is (CelestialBodyGenerator.SunZoneIntensity). Docked or landed ships
// are exempt - the station / planet surface is not in the star's field space.
public sealed partial class World
{
    public const float SunZoneMaxDamagePerSecond = 30f;

    public float SunZoneIntensityNow
    {
        get
        {
            if (IsDocked || IsLandedOnPlanet)
                return 0f;
            var system = GalaxyMap.GetSystem(_currentSystemId);
            var star = system.Bodies.First(b => b.ParentId is null);
            var distance = (float)(_shipFieldPosition - system.Field.Center).Length();
            return CelestialBodyGenerator.SunZoneIntensity(star, distance);
        }
    }

    private void StepSunZone(double deltaSeconds)
    {
        var intensity = SunZoneIntensityNow;
        if (intensity <= 0f)
            return;
        var amount = SunZoneMaxDamagePerSecond * intensity * (float)deltaSeconds;
        // Snapshot the ids first: a room reaching 0 HP explodes and rewrites Ship.Rooms mid-loop.
        foreach (var roomId in Ship.Rooms.Select(r => r.Id).ToList())
            DamageRoom(roomId, amount);
    }
}
