using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Anabiosis.Shared.Model;

// Direct user request ("сделай чтобы 2 текущих корабля... были вшиты в игру и были всегда
// доступны даже если их удалить из редактора") - the exact same "freeze the JSON, replay through
// JsonSerializer" pattern ShipDefaultHull.cs already established for the game's own unnamed
// fallback hull, applied here to the 2 player-drawn designs that were the only ones (of 16 saved
// slots) that actually pass CustomShipValidator.Validate at the time this was written - every
// other saved slot is a single-compartment blueprint, not a complete playable ship. Frozen here
// as plain JSON captured verbatim from %LocalAppData%\Anabiosis\custom-ships\, so these 2 ships
// keep working even if the corresponding local save is deleted, renamed, or corrupted through the
// Ship Editor's own UI - PlayableCustomShipNames/LoadPlayableCustomShip (Game1.Menu.cs) always
// resolve these 2 names to THIS frozen copy first, never touching CustomShipStore for them. The
// raw JSON payloads themselves live in the 2 sibling partial files (one per ship, ShipBuiltInFleet
// keeps files small per project convention) - this file is just the lookup/deserialize logic.
public static partial class ShipBuiltInFleet
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public const string Cosmoteer1Name = "cosmoteer1";
    public const string WorkingShipName = "рабочий корабль";

    // Display order on the ship-select screen and in the lobby's host-picked list.
    public static readonly IReadOnlyList<string> Names = new[] { Cosmoteer1Name, WorkingShipName };

    private static CustomShipDefinition? _cosmoteer1;
    private static CustomShipDefinition? _workingShip;

    public static CustomShipDefinition Cosmoteer1 =>
        _cosmoteer1 ??= JsonSerializer.Deserialize<CustomShipDefinition>(Cosmoteer1Json, Options)!;

    public static CustomShipDefinition WorkingShip =>
        _workingShip ??= JsonSerializer.Deserialize<CustomShipDefinition>(WorkingShipJson, Options)!;

    // Looks up a frozen built-in ship by name - returns null for any name that isn't one of the 2
    // (the caller then falls back to CustomShipStore for an ordinary player-saved design).
    public static CustomShipDefinition? TryGet(string name) => name switch
    {
        Cosmoteer1Name => Cosmoteer1,
        WorkingShipName => WorkingShip,
        _ => null,
    };
}
