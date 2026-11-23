namespace Anabiosis.Shared.Model;

// Direct user request ("сделать карту солнечной системы более интересной в духе Cosmoteer... случайно
// сгенерированы интересные места, которые можно случайно найти"). A place in a system's local field
// that is NOT on the charts: it starts hidden under the fog of war (FogGrid.cs) and is found by flying
// near it or by sweeping it with the sonar (World.Exploration.cs).
//
// Generated per GAME, not per system name (PoiGenerator.cs): a new game rolls a new world seed, so the
// same system has different places each playthrough, and stays as it was once visited because the seed
// and what was found are both saved.
public enum PoiKind
{
    // A knot of asteroids around a point, most of them carrying ore - mined like any other rock.
    AsteroidCluster,
    // Loot-on-approach finds (the old GalaxyPointKind salvage points, moved here so they are hidden too).
    StoragePod,
    AbandonedShip,
    ShipGraveyard,
    // A blown-up hull that can be entered and searched (World.Wrecks.cs) - a real ship, not a marker.
    Wreck,
    // A dead station: dock and walk it - no crew, no power, no air, salvage in its rooms (World.AbandonedStations.cs).
    AbandonedStation,
}

/// <param name="Radius">How far the place itself reaches: the cluster's spread, or a find's capture radius.</param>
public sealed record PointOfInterest(string Id, PoiKind Kind, double X, double Y, float Radius)
{
    public Vec2 Position => new(X, Y);
}

public static class PoiCatalog
{
    public readonly record struct Loot(int Credits, int Hyperium);

    public static string NameFor(PoiKind kind) => kind switch
    {
        PoiKind.AsteroidCluster => "Скопление астероидов",
        PoiKind.StoragePod => "Контейнер с грузом",
        PoiKind.AbandonedShip => "Брошенный корабль",
        PoiKind.ShipGraveyard => "Кладбище кораблей",
        PoiKind.Wreck => "Взорванный корабль",
        PoiKind.AbandonedStation => "Заброшенная станция",
        _ => "?",
    };

    /// What a place reads as on the map before anyone has been close enough to tell what it is.
    public const string UnknownSignalName = "Неизвестный сигнал";

    /// Looted once, just by flying into its radius (World.Exploration.cs).
    public static bool IsLootOnApproach(PoiKind kind) =>
        kind is PoiKind.StoragePod or PoiKind.AbandonedShip or PoiKind.ShipGraveyard;

    public static Loot LootFor(PointOfInterest poi)
    {
        var random = new Random(AsteroidShape.StableHash(poi.Id + "-loot"));
        return poi.Kind switch
        {
            PoiKind.StoragePod => new Loot(random.Next(30, 81), random.Next(0, 3)),
            PoiKind.AbandonedShip => new Loot(random.Next(60, 151), random.Next(1, 4)),
            PoiKind.ShipGraveyard => new Loot(random.Next(120, 301), random.Next(1, 5)),
            _ => new Loot(0, 0),
        };
    }
}
