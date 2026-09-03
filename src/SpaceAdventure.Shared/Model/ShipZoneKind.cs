namespace SpaceAdventure.Shared.Model;

// Typed compartments (direct user request - "разные зоны 1)реакторный отсек 2)медицинский отсек
// 3)инжинерный отсек 4)рубка управления") - picked from a fixed list at zone-creation time in the
// Ship Editor (Game1.ShipEditor.cs), not auto-detected from free text. A zone the player names
// something else entirely stays untyped (null ShipZoneKind) - purely cosmetic, exactly like every
// zone before this existed.
public enum ShipZoneKind
{
    ReactorRoom,
    MedicalBay,
    EngineeringBay,
    ControlRoom,
}

// The canonical name a typed zone's Room ends up with once exported (Game1.ShipEditor.TileBridge.cs's
// ZoneNameFor) - the SAME string a real Room.Name already carries, so the server can recognize "is
// the Reactor sitting in a Reactor-typed room" with a plain name comparison (World.Reactor.cs) rather
// than needing a new field threaded through CustomRoomDef/Room/the network protocol. Only Reactor's
// own output currently reads this (direct user decision - start with just the Reactor); Medical/
// Engineering/Control room exist as real, selectable zone types now but carry no penalty logic yet -
// there's no device kind today whose output a debuff would even apply to for those three.
public static class ShipZoneKinds
{
    public static string CanonicalName(ShipZoneKind kind) => kind switch
    {
        ShipZoneKind.ReactorRoom => "Реакторный отсек",
        ShipZoneKind.MedicalBay => "Медицинский отсек",
        ShipZoneKind.EngineeringBay => "Инженерный отсек",
        ShipZoneKind.ControlRoom => "Рубка управления",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static readonly ShipZoneKind[] All = Enum.GetValues<ShipZoneKind>();
}
