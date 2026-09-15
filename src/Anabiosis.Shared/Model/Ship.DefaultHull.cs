using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anabiosis.Shared.Model;

// Direct user request ("удали все текущие корабли в разделе начать новую игру... полностью удалить
// из кода") - Scout/Frigate/Cruiser/Corvette/Destroyer/Freighter (ShipKind's own former non-Custom
// members) are gone: their hull-construction files (Ship.Scout.cs/.Cruiser.cs/.Corvette.cs/
// .CatalogHulls.cs, and CreateStarter() here in Ship.cs) are deleted outright, not just hidden from
// the New Game screen.
//
// The one thing that couldn't simply disappear with them: World/GameServer/SoloSession's own
// parameterless constructors (and the Tutorial) used to default to ShipKind.Frigate - hundreds of
// existing tests, plus World.Tutorial.cs's own hardcoded room ids ("reactor", "cockpit", ...),
// silently depend on THAT EXACT layout without ever naming "Frigate" explicitly. Rewriting every one
// of those callers to build/expect a different hull would have been a much bigger, much riskier
// change than the one actually requested - deleting the 6 SELECTABLE kinds - so instead this file
// freezes the old CreateStarter() hull's own shape, captured ONE TIME via a diagnostic script
// (DIAG=1 dotnet run, Ship.CreateStarter().ToDefinition(), the exact same lossless round-trip
// World_ShipBuilding_ToDefinitionRoundTrip_PreservesEveryHandAuthoredHull already proved correct
// before it was deleted alongside the hull it tested) as plain JSON - a byte-exact, unnamed default
// hull that ShipKind.Custom now falls back to whenever no explicit CustomShipDefinition is supplied,
// so every one of those pre-existing callers keeps compiling AND behaving identically, with zero
// rewrite. JSON (not a hand-written C# object literal) because CustomShipDefinition is a deep tree
// of records - a mechanical capture-and-replay through the SAME JsonSerializer path CustomShipStore
// already uses is far less error-prone than hand-transcribing dozens of nested Room/Door/Device
// entries by hand.
public static class ShipDefaultHull
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static CustomShipDefinition? _cached;

    public static CustomShipDefinition Definition => _cached ??= JsonSerializer.Deserialize<CustomShipDefinition>(Json, Options)!;

    private const string Json = """
    {
      "Name": "Мой корабль",
      "Rooms": [
        { "Id": "cockpit", "Name": "Кокпит", "Rects": [ { "X": 0, "Y": 0, "Width": 5, "Height": 6 } ] },
        { "Id": "reactor", "Name": "Реакторная", "Rects": [ { "X": 5, "Y": 0, "Width": 5, "Height": 6 } ] },
        { "Id": "corridor", "Name": "Коридор", "Rects": [ { "X": 10, "Y": 0, "Width": 3, "Height": 6 } ] },
        { "Id": "quarters", "Name": "Каюты", "Rects": [ { "X": 13, "Y": 0, "Width": 5, "Height": 6 } ] },
        { "Id": "engine", "Name": "Машинное отделение", "Rects": [ { "X": 18, "Y": 0, "Width": 5, "Height": 6 } ] },
        { "Id": "airlock-chamber", "Name": "Шлюзовая камера", "Rects": [ { "X": 23, "Y": 0, "Width": 3, "Height": 6 } ] }
      ],
      "Doors": [
        { "X": 5, "Y": 3, "Vertical": true, "Wide": true, "Id": "door-cockpit-reactor" },
        { "X": 10, "Y": 3, "Vertical": true, "Wide": true, "Id": "door-reactor-corridor" },
        { "X": 13, "Y": 3, "Vertical": true, "Wide": true, "Id": "door-corridor-quarters" },
        { "X": 18, "Y": 3, "Vertical": true, "Wide": true, "Id": "door-quarters-engine" },
        { "X": 23, "Y": 3, "Vertical": true, "Wide": true, "Id": "door-engine-airlock" }
      ],
      "Airlocks": [ { "RoomId": "airlock-chamber", "Side": "Right", "Id": "door-airlock-vacuum" } ],
      "Devices": [
        { "Kind": "CardTable", "X": 4, "Y": 1 },
        { "Kind": "Reactor", "X": 9.5, "Y": 1 },
        { "Kind": "Distribution", "X": 9.5, "Y": 3 },
        { "Kind": "Battery", "X": 9.5, "Y": 5 },
        { "Kind": "Helm", "X": 1.4, "Y": 1.3 },
        { "Kind": "Navigation", "X": 2.8, "Y": 1.3 },
        { "Kind": "Shields", "X": 7.2, "Y": 0.7, "Id": "system-shields" },
        { "Kind": "Shields", "X": 8.6, "Y": 1.6, "Id": "system-shields-2" },
        { "Kind": "WeaponCharger", "X": 7.6, "Y": 2.2, "Id": "system-weapon-charger" },
        { "Kind": "Oxygen", "X": 12.5, "Y": 1.5, "Id": "system-oxygen" },
        { "Kind": "Secondary", "X": 8.5, "Y": 3.8, "Id": "system-secondary" },
        { "Kind": "Engine", "X": 7.2, "Y": 4.3, "Id": "system-engine" },
        { "Kind": "Engine", "X": 8.5, "Y": 5.2, "Id": "system-engine-2" },
        { "Kind": "TurretBallistic", "X": 1.5, "Y": 3, "Id": "turret-bow" },
        { "Kind": "TurretLaser", "X": 6.5, "Y": 3, "Id": "turret-laser" },
        { "Kind": "AmmoStorage", "X": 15, "Y": 3, "Id": "ammo-storage-quarters" },
        { "Kind": "SuitLocker", "X": 20, "Y": 3, "Id": "suit-locker-engine" },
        { "Kind": "StorageRack", "X": 16, "Y": 1.5, "Id": "rack-quarters" },
        { "Kind": "StorageRack", "X": 20, "Y": 5, "Id": "rack-engine" },
        { "Kind": "Camera", "X": 3.5, "Y": 5, "CameraSide": "Fore" },
        { "Kind": "Camera", "X": 24, "Y": 1, "CameraSide": "Aft" },
        { "Kind": "ComponentMount", "X": 1.5, "Y": 5, "Id": "mount-cockpit-1" },
        { "Kind": "ComponentMount", "X": 6, "Y": 1.5, "Id": "mount-reactor-1" },
        { "Kind": "ComponentMount", "X": 12.5, "Y": 5, "Id": "mount-corridor-1" },
        { "Kind": "ComponentMount", "X": 13.5, "Y": 5, "Id": "mount-quarters-1" },
        { "Kind": "ComponentMount", "X": 17.5, "Y": 4.5, "Id": "mount-quarters-2" },
        { "Kind": "ComponentMount", "X": 22, "Y": 4, "TargetDoorId": "door-engine-airlock", "Id": "mount-engine-door" },
        { "Kind": "Jukebox", "X": 4, "Y": 4.5 },
        { "Kind": "Terminal", "X": 0.6, "Y": 1, "WallDeviceFacingSide": "West", "Id": "terminal" }
      ],
      "ForwardDegrees": 0
    }
    """;
}
