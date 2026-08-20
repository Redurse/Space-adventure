using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// The asteroid field's own rocks/ore veins, grouped the same way StationSnapshot groups the
// station - FieldRenderer/HelmPanel's radar/EffectTracker always want the whole trio together.
// Named "Field" on WorldSnapshot (not "AsteroidField") to stay distinct from the server-only
// Shared.Model.AsteroidField class this is built from (World.AsteroidField).
public sealed record AsteroidFieldSnapshot(
    IReadOnlyList<Asteroid> Asteroids,
    IReadOnlyList<OreDeposit> OreDeposits,
    IReadOnlyList<OreDepositState> OreDepositStates);
