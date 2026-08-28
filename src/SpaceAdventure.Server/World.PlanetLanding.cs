using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Landing on a planet/moon's own surface (M55 - "сесть на планету... собственный ландшафт"):
// mirrors World.StationDocking.cs's own "approach → arm → press → transition" shape, just onto
// PlanetSurface's small, unrelated-scale local field instead of a station's berth. Only bodies
// CelestialBodyGenerator.IsLandable accepts (Rocky/Moon) ever arm the button - touching a gas
// giant/star still just stops the ship dead (World.ShipField.cs's own HullOverlapsCelestialBody
// check), exactly as before this file existed.
public sealed partial class World
{
    private const float LandMaxSpeed = 2f;
    // How far clear of the body's own surface the ship reappears on take-off - generous next to
    // the hull's own half-extents so the very next tick's HullOverlapsCelestialBody reads clear
    // regardless of which corner of the hull box ends up nearest the body.
    private const float TakeOffClearanceMargin = 100f;
    // Small departure kick, same spirit as StationDocking.cs's own UndockDriftSpeed - this game's
    // ship physics has no passive drag anywhere, so even a slow nudge coasts outward on its own
    // until the pilot takes the stick, rather than leaving the ship dead-stopped right at the
    // body's own gravity well.
    private const float TakeOffDriftSpeed = 5f;

    // Which body (if any) the ship is currently sitting on - null means "in the system's own
    // field", exactly like _dockedPointId means "not docked" when null. Set only by
    // TryLandOnPlanet/TakeOff below.
    private string? _landedBodyId;
    public bool IsLandedOnPlanet => _landedBodyId is not null;
    public string? LandedBodyId => _landedBodyId;

    // World.ShipField.cs's own StepShipFieldPhysics/DebugPlaceShip consult these instead of
    // AsteroidField directly, so the exact same physics code drives both "flying the system field"
    // and "driving around a landed planet's own small surface field" - only the data source
    // (bounds + obstacles) changes underneath it.
    private float ActiveFieldWidth => _landedBodyId is null ? AsteroidField.Width : PlanetSurface.Width;
    private float ActiveFieldHeight => _landedBodyId is null ? AsteroidField.Height : PlanetSurface.Height;
    private IReadOnlyList<Asteroid> ActiveObstacles =>
        _landedBodyId is { } bodyId ? PlanetSurface.Generate(bodyId) : AsteroidField.Asteroids;

    // What arms the helm's "Посадка" button - the client mirrors this to decide whether to draw
    // it, same as every other CanXNow gate here. Re-derives "currently touching a landable body"
    // fresh from the ship's own CURRENT (already-settled) position rather than caching it from
    // whatever StepShipFieldPhysics's last candidate check happened to see - cheap (a handful of
    // bodies per system) and avoids a second piece of state that could drift out of sync with
    // _shipFieldPosition.
    public bool CanLandNow =>
        !IsDocked && !IsInBattle && _landedBodyId is null &&
        HullOverlapsCelestialBody(_shipFieldPosition) is { } body && CelestialBodyGenerator.IsLandable(body) &&
        _shipVelocity.Length() < LandMaxSpeed;

    // Same button either way (the helm's "Посадка"/"Взлёт" toggle, mirrors HandleDockButtonPressed)
    // - lands while touching a landable body's surface, takes off once already landed.
    private void HandleLandingButtonPressed()
    {
        if (_landedBodyId is not null)
            TakeOff();
        else
            TryLandOnPlanet();
    }

    private void TryLandOnPlanet()
    {
        if (!CanLandNow)
            return;
        var body = HullOverlapsCelestialBody(_shipFieldPosition)!;
        _landedBodyId = body.Id;
        _shipRotationDegrees = 0f;
        SetShipFieldPosition(PlanetSurface.Center);
        _shipVelocity = Vec2.Zero;
        _shipThrust = Vec2.Zero;
        _shipAutoStabilize = true;
    }

    // Leaves the surface - the ship reappears in the SYSTEM field, next to the body's fixed
    // position (CelestialBodyGenerator.PositionAt; bodies don't move, M59).
    private void TakeOff()
    {
        if (_landedBodyId is not { } bodyId)
            return;

        var system = GalaxyMap.GetSystem(_currentSystemId);
        var body = system.BodiesById[bodyId];
        var bodyPosition = CelestialBodyGenerator.PositionAt(body, system.BodiesById) + system.Field.Center;
        // No natural "which side did we land from" survives the trip through PlanetSurface's own
        // unrelated coordinate space - the same fixed fallback heading TryWarpTo already uses when
        // it has nothing better to go on.
        var departDirection = new Vec2(0f, -1f);

        _landedBodyId = null;
        _shipRotationDegrees = 0f;
        SetShipFieldPosition(bodyPosition + departDirection * (body.Radius + TakeOffClearanceMargin));
        _shipVelocity = departDirection * TakeOffDriftSpeed;
        _shipThrust = Vec2.Zero;
        _shipAutoStabilize = false;
    }
}
