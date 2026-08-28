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
            errors.Add("Нужна хотя бы одна консоль штурвала.");
        if (Count(CustomDeviceKind.Navigation) < 1)
            errors.Add("Нужна хотя бы одна консоль навигации.");
        if (Count(CustomDeviceKind.Engine) == 0)
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
        }

        return errors;
    }

    private static bool Overlaps(CustomRoomDef a, CustomRoomDef b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;

    private static bool Contains(CustomRoomDef r, float x, float y) =>
        x >= r.X && x <= r.X + r.Width && y >= r.Y && y <= r.Y + r.Height;
}
