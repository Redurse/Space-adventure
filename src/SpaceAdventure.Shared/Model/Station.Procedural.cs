using System.Text;

namespace SpaceAdventure.Shared.Model;

// Procedural per-instance station layout (M49 - "станции состояли из множества отсеков и имели
// интересные формы... круговые обходы... отсеки расположены логично"). Every station in the galaxy
// gets its own generated shape, seeded purely from its own GalaxyPoint id, so two stations of the
// same StationKind never look alike and the same station always regenerates identically (no extra
// state to save - World.Save.cs already persists the point id everything else keys off).
//
// Shape: a single-thickness ring of square rooms walking the border of a rectangle (a "picture
// frame" of cells) - every room touches exactly its two ring-neighbours (one of which, for the
// first and last room in the walk, is the wrap-around edge that closes the loop), so walking the
// ring in either direction is always a route with no dead end. Every room IS a functional
// compartment (no separate bare corridor layer) - this is deliberately the simplest shape that
// still satisfies "ring, no dead ends, logically zoned", not a literal rounded/curved hull (the
// user chose a polygon of rectangular blocks over a new curved-wall renderer).
public sealed partial class Station
{
    // Every cell along a given grid column shares that column's own width, and every cell along a
    // given row shares that row's own height - rolled once per column/row (not per individual
    // cell), so the ring reads as an irregular, Barotrauma-ish silhouette of differently-sized
    // compartments instead of a uniform grid of identical squares, while every pair of grid-adjacent
    // cells still shares an exact edge automatically (a non-uniform grid is still a grid - the
    // adjacency/door-detection math below neither knows nor cares that the spacing varies).
    private const int RoomSizeMin = 4;
    private const int RoomSizeMax = 8;

    // Mandatory on every station regardless of kind - World.Recruiting.cs/World.StationCrime.cs
    // hard-require a Recruiter/Security NPC to exist SOMEWHERE on the docked station for hiring and
    // theft/arrest to work at all, so these can never be left to the random secondary pool.
    // Shipyard additionally always gets a Shipwright's office - the one service only it offers.
    private static List<StationModuleKind> MandatoryModulesFor(StationKind kind)
    {
        var mandatory = new List<StationModuleKind>
        {
            StationModuleKind.Dock,
            StationModuleKind.Trade,
            StationModuleKind.Administrator,
            StationModuleKind.Engineering,
            StationModuleKind.Storage,
            StationModuleKind.Security,
            StationModuleKind.Recruiting,
        };
        if (kind == StationKind.Shipyard)
            mandatory.Add(StationModuleKind.Shipwright);
        return mandatory;
    }

    private static IReadOnlyList<StationModuleKind> SecondaryPoolFor(StationKind kind) => kind switch
    {
        StationKind.Trade => new[]
        {
            StationModuleKind.ExtraTrade, StationModuleKind.Cantina, StationModuleKind.Brokerage,
            StationModuleKind.BondedWarehouse, StationModuleKind.PassengerLounge,
        },
        StationKind.Mining => new[]
        {
            StationModuleKind.OreRefinery, StationModuleKind.BulkStorage,
            StationModuleKind.Foundry, StationModuleKind.ProspectorsBunkroom, StationModuleKind.OreVault,
        },
        StationKind.Shipyard => new[]
        {
            StationModuleKind.DrydockBay, StationModuleKind.OutfittingBay,
            StationModuleKind.SalvageYard, StationModuleKind.CrewBunkroom, StationModuleKind.FittingDock,
        },
        StationKind.Military => new[]
        {
            StationModuleKind.Armory, StationModuleKind.Barracks, StationModuleKind.Brig,
            StationModuleKind.CommandCenter, StationModuleKind.MunitionsStorage, StationModuleKind.TrainingHall,
            StationModuleKind.RadarPost, StationModuleKind.MedBay,
        },
        StationKind.Research => new[]
        {
            StationModuleKind.Laboratory, StationModuleKind.Observatory,
            StationModuleKind.DataArchive, StationModuleKind.Greenhouse,
        },
        _ => Array.Empty<StationModuleKind>(),
    };

    // Aspirational secondary-module count range before PickSecondaryModules clamps it against the
    // 10-20-total band and the door-parity fix - widest for Military (biggest, busiest pool),
    // narrowest for Research (small science outposts, effectively always right at the 10-room
    // floor). Tune freely: nothing else depends on these exact numbers, and PickSecondaryModules'
    // own clamps keep every combination valid regardless of what's written here.
    private static (int Min, int Max) SecondaryCountRangeFor(StationKind kind) => kind switch
    {
        StationKind.Research => (0, 2),
        StationKind.Mining => (1, 4),
        StationKind.Trade => (1, 5),
        StationKind.Shipyard => (2, 5),
        StationKind.Military => (3, 8),
        _ => (0, 0),
    };

    private static (string RoomName, NpcKind? Npc, ItemType? CrateItem) InfoFor(StationModuleKind kind) => kind switch
    {
        StationModuleKind.Dock => ("Стыковочный отсек", null, null),
        StationModuleKind.Trade => ("Торговый зал", NpcKind.Trader, ItemType.MedKit),
        StationModuleKind.Administrator => ("Кабинет администратора", NpcKind.Administrator, ItemType.Mineral),
        StationModuleKind.Engineering => ("Машинный отсек", NpcKind.Mechanic, ItemType.WireSpool),
        StationModuleKind.Storage => ("Склад", null, ItemType.Mineral),
        // Deliberately no crate here (Station.Default.cs's own old comment, still true): carrying
        // an AmmoCrate makes World.Interact.cs treat every [F] as "reload a turret", and this is the
        // one room a thief has every reason to linger right next to.
        StationModuleKind.Security => ("Пост охраны", NpcKind.Security, null),
        StationModuleKind.Recruiting => ("Кадровое агентство", NpcKind.Recruiter, ItemType.Mineral),
        StationModuleKind.Shipwright => ("Верфь", NpcKind.Shipwright, ItemType.FuelRod),

        StationModuleKind.ExtraTrade => ("Дополнительный торговый зал", NpcKind.Trader, ItemType.MedKit),
        StationModuleKind.Cantina => ("Кантина", null, ItemType.Mineral),
        StationModuleKind.Brokerage => ("Брокерская контора", NpcKind.Administrator, ItemType.Mineral),
        StationModuleKind.BondedWarehouse => ("Таможенный склад", null, ItemType.Mineral),
        StationModuleKind.PassengerLounge => ("Пассажирский зал", null, null),

        StationModuleKind.OreRefinery => ("Обогатительный цех", NpcKind.Mechanic, ItemType.WireSpool),
        StationModuleKind.BulkStorage => ("Дополнительный склад", null, ItemType.Mineral),
        StationModuleKind.Foundry => ("Литейная", NpcKind.Mechanic, ItemType.WireSpool),
        StationModuleKind.ProspectorsBunkroom => ("Общежитие старателей", null, null),
        StationModuleKind.OreVault => ("Хранилище руды", null, ItemType.Mineral),

        StationModuleKind.DrydockBay => ("Сухой док", NpcKind.Mechanic, ItemType.WireSpool),
        StationModuleKind.OutfittingBay => ("Оснастка", NpcKind.Mechanic, ItemType.WireSpool),
        StationModuleKind.SalvageYard => ("Разделка на металл", null, ItemType.Mineral),
        StationModuleKind.CrewBunkroom => ("Общежитие экипажа", null, null),
        StationModuleKind.FittingDock => ("Оснастка корпуса", NpcKind.Mechanic, ItemType.WireSpool),

        StationModuleKind.Armory => ("Арсенал", NpcKind.Security, ItemType.Mineral),
        StationModuleKind.Barracks => ("Казармы", NpcKind.Security, null),
        StationModuleKind.Brig => ("Карцер", null, null),
        StationModuleKind.CommandCenter => ("Командный пункт", NpcKind.Administrator, ItemType.Mineral),
        StationModuleKind.MunitionsStorage => ("Склад боеприпасов", null, ItemType.Mineral),
        StationModuleKind.TrainingHall => ("Учебный зал", NpcKind.Security, null),
        StationModuleKind.RadarPost => ("Радиолокационный пост", NpcKind.Security, null),
        StationModuleKind.MedBay => ("Медотсек", null, ItemType.MedKit),

        StationModuleKind.Laboratory => ("Лаборатория", NpcKind.Scientist, ItemType.Mineral),
        StationModuleKind.Observatory => ("Обсерватория", NpcKind.Scientist, null),
        StationModuleKind.DataArchive => ("Архив данных", NpcKind.Administrator, ItemType.Mineral),
        StationModuleKind.Greenhouse => ("Оранжерея", null, null),

        _ => ("Отсек", null, null),
    };

    private static string NpcNameFor(NpcKind kind) => kind switch
    {
        NpcKind.Trader => "Торговец",
        NpcKind.Administrator => "Администратор станции",
        NpcKind.Mechanic => "Механик станции",
        NpcKind.Shipwright => "Корабельный мастер",
        NpcKind.Security => "Охранник",
        NpcKind.Recruiter => "Кадровик",
        NpcKind.Scientist => "Учёный",
        _ => "Сотрудник",
    };

    // connectorAnchor is where the station's own umbilical door has to sit: the exact position of
    // the ship's outer airlock door in the ship's interior coordinates (same contract Station.
    // Default.cs's old Create(kind, anchor) had). Every AirlockOuterDoor in the game is the same
    // "vertical door on a side wall" shape (Width=1, Height=StandardSpanUnits) - confirmed across
    // every hand-authored and Ship-Editor-built hull - so this connector, and therefore the whole
    // generated shape hung off it, only ever needs a straight translation to follow a hull swap,
    // never a rotation (World.cs's RebuildStationLayouts relies on exactly this).
    public static Station CreateProcedural(string pointId, StationKind kind, Vec2 connectorAnchor)
    {
        var rng = new Random(SeedFrom(pointId));
        var mandatory = MandatoryModulesFor(kind);
        var secondary = PickSecondaryModules(kind, mandatory.Count, rng);
        var order = BuildZonedOrder(mandatory, secondary, rng);

        var n = order.Count;
        var half = n / 2 + 2;
        var cols = Math.Max(2, half / 2);
        var rows = Math.Max(2, half - cols);
        var cellPositions = PerimeterCellPositions(cols, rows);

        var colWidths = new int[cols];
        for (var c = 0; c < cols; c++)
            colWidths[c] = rng.Next(RoomSizeMin, RoomSizeMax + 1);
        var rowHeights = new int[rows];
        for (var r = 0; r < rows; r++)
            rowHeights[r] = rng.Next(RoomSizeMin, RoomSizeMax + 1);
        var colOffsets = new int[cols];
        for (var c = 1; c < cols; c++)
            colOffsets[c] = colOffsets[c - 1] + colWidths[c - 1];
        var rowOffsets = new int[rows];
        for (var r = 1; r < rows; r++)
            rowOffsets[r] = rowOffsets[r - 1] + rowHeights[r - 1];

        var roomDefs = new List<CustomRoomDef>(n);
        for (var i = 0; i < n; i++)
        {
            var (col, row) = cellPositions[i];
            var slug = order[i].ToString().ToLowerInvariant();
            roomDefs.Add(new CustomRoomDef($"{pointId}-{slug}", InfoFor(order[i]).RoomName,
                colOffsets[col], rowOffsets[row], colWidths[col], rowHeights[row]));
        }

        // Every touching pair gets a door automatically (unlike Ship.Custom.cs's BuildDoors, which
        // requires each door to be separately declared) - a procedural layout has no such
        // declarations to check against, and the single-thickness ring guarantees the only pairs
        // that ever touch are consecutive-in-the-walk neighbours (plus the wrap-around pair that
        // closes the loop), so "door every touching pair" and "door every ring neighbour" are the
        // same thing here.
        var overlaps = ShipLayoutGeometry.FindRoomPairOverlaps(roomDefs);
        var doors = new List<Door>();
        var doorIndex = 0;
        foreach (var overlap in overlaps)
        {
            var span = MathF.Min(Door.StandardSpanUnits, overlap.OverlapLength);
            doors.Add(overlap.Vertical
                ? new Door($"{pointId}-door-{doorIndex++}", overlap.RoomAId, overlap.RoomBId, overlap.At, overlap.OverlapCenter, 1.0f, span)
                : new Door($"{pointId}-door-{doorIndex++}", overlap.RoomAId, overlap.RoomBId, overlap.OverlapCenter, overlap.At, span, 1.0f));
        }

        var npcs = new List<StationNpc>();
        var crates = new List<StationCrate>();
        for (var i = 0; i < n; i++)
        {
            var (_, npcKind, crateItem) = InfoFor(order[i]);
            var room = roomDefs[i];
            var centerX = room.X + room.Width / 2f;
            var centerY = room.Y + room.Height / 2f;
            var slug = order[i].ToString().ToLowerInvariant();
            if (npcKind is { } nk)
                npcs.Add(new StationNpc($"{pointId}-npc-{slug}", NpcNameFor(nk), nk, centerX, centerY));
            // Away from the room's own staffer, same reasoning Station.Default.cs always used:
            // lifting it is a deliberate walk into a corner, not something brushed past while trading.
            if (crateItem is { } item)
                crates.Add(new StationCrate($"{pointId}-crate-{slug}", room.Id, centerX, centerY + 2f, item));
        }

        // roomDefs[0] is always the (0,0) grid cell (PerimeterCellPositions starts there) and
        // order[0] is always Dock (BuildZonedOrder guarantees it) - so shifting by
        // (connectorAnchor - dock's own raw left-edge-centre) lands the dock room's left edge and
        // the connector on connectorAnchor exactly, in one pass, with no separate "build at zero
        // then re-translate" step. Dock's own cell is (col=0,row=0), so its raw left-edge-centre is
        // (0, rowHeights[0]/2) regardless of how wide/tall the rest of the ring's cells rolled.
        var offset = new Vec2(connectorAnchor.X, connectorAnchor.Y - rowHeights[0] / 2f);
        var (offsetX, offsetY) = offset.AsFloat(); // truncate the (double) offset to float ONCE,
        // so every shifted fixture below is a plain float+float add, not its own repeated cast.
        var rooms = roomDefs.Select(r => new Room(r.Id, r.Name, r.X + offsetX, r.Y + offsetY, r.Width, r.Height)).ToList();
        var shiftedDoors = doors.Select(d => d with { X = d.X + offsetX, Y = d.Y + offsetY }).ToList();
        var shiftedNpcs = npcs.Select(npc => npc with { X = npc.X + offsetX, Y = npc.Y + offsetY }).ToList();
        var shiftedCrates = crates.Select(c => c with { X = c.X + offsetX, Y = c.Y + offsetY }).ToList();
        var (anchorX, anchorY) = connectorAnchor.AsFloat();
        var shipConnector = new AirlockOuterDoor($"{pointId}-connector", rooms[0].Id, anchorX, anchorY, 1.0f, Door.StandardSpanUnits);

        return new Station(rooms, shiftedDoors, shipConnector, shiftedNpcs, shiftedCrates, WorldCenter, rooms[0].Id);
    }

    // The user's own stated range for a station's total room count (mandatory + secondary - see
    // BuildZonedOrder, there's no separate bare-corridor layer to pad the count with).
    private const int GlobalMinTotalRooms = 10;
    private const int GlobalMaxTotalRooms = 20;

    private static List<StationModuleKind> PickSecondaryModules(StationKind kind, int mandatoryCount, Random rng)
    {
        var pool = new List<StationModuleKind>(SecondaryPoolFor(kind));
        Shuffle(pool, rng);

        // The kind's own aspirational range (SecondaryCountRangeFor) only matters where it doesn't
        // conflict with the hard 10-20 total-room band - below the floor it's overridden upward,
        // above the pool's own size it's clamped down (nothing to pick without repeats). This is
        // why Research's "small station" flavor (a narrow 0-2 aspiration) still always lands on a
        // real station: the floor below always wins if the aspiration alone would undershoot it.
        var (rangeMin, rangeMax) = SecondaryCountRangeFor(kind);
        var floor = Math.Max(0, GlobalMinTotalRooms - mandatoryCount);
        var ceiling = Math.Min(pool.Count, GlobalMaxTotalRooms - mandatoryCount);
        var min = Math.Max(rangeMin, floor);
        var max = Math.Max(min, Math.Min(rangeMax, ceiling));
        max = Math.Min(max, pool.Count);

        var count = rng.Next(min, max + 1);
        var secondary = pool.Take(count).ToList();

        // The ring is a rectangle perimeter, whose cell count must be even (2*cols+2*rows-4) - fix
        // parity by adjusting the secondary count by exactly one rather than the mandatory set,
        // which every kind needs in full every time. Every pool above has at least a couple more
        // entries than the floor requires, so the "remove" branch (only reachable when the pool is
        // already fully used up) can never push the total below GlobalMinTotalRooms - at worst it
        // trims one unlucky roll back down to exactly the floor instead of one above it.
        if ((mandatoryCount + secondary.Count) % 2 != 0)
        {
            if (secondary.Count < pool.Count)
                secondary.Add(pool[secondary.Count]);
            else if (secondary.Count > 0)
                secondary.RemoveAt(secondary.Count - 1);
        }
        return secondary;
    }

    // Concatenation order, not index arithmetic - Dock first, the near-dock mandatory set (Trade/
    // Administrator/Storage/Recruiting/Shipwright) split across both ends of the list so it reads
    // as "clustered around the dock from either direction you approach it", Security placed just
    // past that cluster on one side (Station.Default.cs's old comment survives: "a thief gets a
    // few rooms of warning"), Engineering placed in the middle of the secondary run so it lands
    // roughly opposite the dock around the ring regardless of exactly how many secondary modules
    // got rolled.
    private static List<StationModuleKind> BuildZonedOrder(List<StationModuleKind> mandatory, List<StationModuleKind> secondary, Random rng)
    {
        var nearDock = mandatory.Where(m => m is not (StationModuleKind.Dock or StationModuleKind.Security or StationModuleKind.Engineering)).ToList();
        Shuffle(nearDock, rng);
        var after = new List<StationModuleKind>();
        var before = new List<StationModuleKind>();
        for (var i = 0; i < nearDock.Count; i++)
            (i % 2 == 0 ? after : before).Add(nearDock[i]);

        var shuffledSecondary = new List<StationModuleKind>(secondary);
        Shuffle(shuffledSecondary, rng);
        var splitAt = shuffledSecondary.Count / 2;

        var order = new List<StationModuleKind> { StationModuleKind.Dock };
        order.AddRange(after);
        order.Add(StationModuleKind.Security);
        order.AddRange(shuffledSecondary.Take(splitAt));
        order.Add(StationModuleKind.Engineering);
        order.AddRange(shuffledSecondary.Skip(splitAt));
        order.AddRange(before);
        return order;
    }

    // Cell coordinates walking the border of a cols x rows grid clockwise from the top-left corner
    // (always index 0 - BuildZonedOrder always puts Dock first, so Dock always lands on this corner,
    // whose west and north sides are both genuinely exterior, matching the connector's fixed
    // west-facing convention). Standard "hollow rectangle" perimeter enumeration: top row, then the
    // right column, then the bottom row backwards, then the left column backwards, each leg
    // excluding whichever corner(s) the previous leg already added.
    private static List<(int Col, int Row)> PerimeterCellPositions(int cols, int rows)
    {
        var cells = new List<(int, int)>();
        for (var c = 0; c < cols; c++)
            cells.Add((c, 0));
        for (var r = 1; r < rows; r++)
            cells.Add((cols - 1, r));
        for (var c = cols - 2; c >= 0; c--)
            cells.Add((c, rows - 1));
        for (var r = rows - 2; r >= 1; r--)
            cells.Add((0, r));
        return cells;
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Not string.GetHashCode() - .NET randomizes that per process by default, which would silently
    // break "the same station always regenerates the same layout" on every server restart. FNV-1a
    // is a plain, stable, public-domain hash with no such randomization.
    private static int SeedFrom(string id)
    {
        const uint offsetBasis = 2166136261;
        const uint prime = 16777619;
        var hash = offsetBasis;
        foreach (var b in Encoding.UTF8.GetBytes(id))
        {
            hash ^= b;
            hash *= prime;
        }
        return unchecked((int)hash);
    }
}
