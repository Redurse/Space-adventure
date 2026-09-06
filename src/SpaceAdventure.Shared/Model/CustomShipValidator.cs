namespace SpaceAdventure.Shared.Model;

// Gates the Ship Editor's "play" button and defensively guards Ship.FromCustomDefinition (Ship.
// Custom.cs) against a definition that skipped the editor's own click-target rules somehow. Every
// message is shown to the player as-is, so it's written for them, not for a log file.
public static class CustomShipValidator
{
    public static IReadOnlyList<string> Validate(CustomShipDefinition def)
    {
        var errors = new List<string>();

        if (def.Rooms.Count == 0)
            errors.Add("Нужен хотя бы один отсек.");

        for (var i = 0; i < def.Rooms.Count; i++)
            for (var j = i + 1; j < def.Rooms.Count; j++)
                if (Overlaps(def.Rooms[i], def.Rooms[j]))
                    errors.Add($"Отсеки «{def.Rooms[i].Name}» и «{def.Rooms[j].Name}» перекрываются.");

        int Count(CustomDeviceKind kind) => def.Devices.Count(d => d.Kind == kind);

        // "Хотя бы один", не "ровно один" (M60+ content-каталог отсеков - гуманное.plan's "бонус, не
        // список" решение): Ship.Custom.cs's BuildReactorBlock/BuildHelmConsole/etc. по-прежнему
        // строят физический объект только из ПЕРВОГО устройства каждого вида (.First(...), безопасно
        // игнорирует остальные) - лишние того же вида не ломают конструктор, а дают бонус (см.
        // World.ShipBuilding.cs's RecomputeDeviceBonuses).
        if (Count(CustomDeviceKind.Reactor) < 1)
            errors.Add("Нужен хотя бы один реактор.");
        if (Count(CustomDeviceKind.Distribution) < 1)
            errors.Add("Нужен хотя бы один распределительный блок.");
        if (Count(CustomDeviceKind.Helm) < 1)
            errors.Add("Нужна хотя бы одна навигационная панель.");
        if (Count(CustomDeviceKind.Navigation) < 1)
            errors.Add("Нужен хотя бы один сканер.");
        // A real ShipEngine (ShipEngine.cs, Cosmoteer-style marching engine) satisfies this just as
        // well as the older flat CustomDeviceKind.Engine bonus device - either is "a way to move".
        if (Count(CustomDeviceKind.Engine) == 0 && def.Engines.Count == 0)
            errors.Add("Нужен хотя бы один двигательный блок.");
        // World.StepAtmosphere looks up the single Oxygen device unconditionally (World.
        // Atmosphere.cs) - a hull with none would crash the very first tick, not just fly quiet.
        if (Count(CustomDeviceKind.Oxygen) == 0)
            errors.Add("Нужен хотя бы один генератор кислорода.");
        if (def.Airlocks.Count == 0)
            errors.Add("Нужен хотя бы один шлюзовой люк во внешний космос.");
        if (Count(CustomDeviceKind.SuitLocker) == 0)
            errors.Add("Нужен хотя бы один шкаф со скафандром.");
        // World.InitializeRackSlots always seeds the starter kit into the first shelf it finds -
        // a hull with none has nowhere for that kit to go at all (World.Storage.cs).
        if (Count(CustomDeviceKind.StorageRack) == 0)
            errors.Add("Нужен хотя бы один стеллаж для снаряжения.");

        var roomsById = def.Rooms.ToDictionary(r => r.Id);

        foreach (var device in def.Devices)
            if (!def.Rooms.Any(r => Contains(r, device.X, device.Y)))
                errors.Add("Устройство стоит вне отсека.");

        foreach (var door in def.Doors)
            if (!roomsById.ContainsKey(door.RoomAId) || !roomsById.ContainsKey(door.RoomBId))
                errors.Add("Дверь ссылается на несуществующий отсек.");

        var overlaps = ShipLayoutGeometry.FindRoomPairOverlaps(def.Rooms);
        foreach (var airlock in def.Airlocks)
        {
            if (!roomsById.TryGetValue(airlock.RoomId, out var room))
            {
                errors.Add("Люк ссылается на несуществующий отсек.");
                continue;
            }
            if (ShipLayoutGeometry.SideHasNeighbor(room, airlock.Side, overlaps))
                errors.Add($"Люк в «{room.Name}» стоит на стене, граничащей с другим отсеком.");
            // M89 (humble-soaring-cat.md, non-rectangular compartments) - a multi-rect room can have
            // more than one piece reaching its own bounding-box edge on the same cardinal side (an
            // L-shape's two arms both touching its own top edge, say); SideMidpoint/SideLength can
            // only place a real door unambiguously when exactly one piece qualifies.
            else if (ShipLayoutGeometry.SubrectsFacingSide(room, airlock.Side).Count != 1)
                errors.Add($"Люк в «{room.Name}» стоит на стороне, за которую отвечает не ровно один кусок отсека.");
        }

        return errors;
    }

    // Generalized (M89) to test every pair of the two rooms' own subrects instead of assuming one
    // rectangle per room - byte-identical to the old single-rect check whenever both rooms have
    // exactly one piece (every existing hand-authored hull/station/editor-drawn rectangular room).
    private static bool Overlaps(CustomRoomDef a, CustomRoomDef b) =>
        a.Rects.Any(ra => b.Rects.Any(rb =>
            ra.X < rb.Right && rb.X < ra.Right && ra.Y < rb.Bottom && rb.Y < ra.Bottom));

    private static bool Contains(CustomRoomDef r, float x, float y) =>
        r.Rects.Any(rect => x >= rect.X && x <= rect.Right && y >= rect.Y && y <= rect.Bottom);
}
