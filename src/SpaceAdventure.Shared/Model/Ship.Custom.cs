namespace SpaceAdventure.Shared.Model;

// Builds a real, playable Ship out of a CustomShipDefinition (the Ship Editor's save format) -
// the runtime equivalent of CreateStarter()/CreateScout()/etc., except every coordinate comes from
// the player's own grid placements instead of a hand-authored literal. WireGraphFactory.
// CreateDefaultForHull already derives the whole power grid from Ship.SystemDevices/DistributionBlock
// alone (see its own doc comment), so nothing here needs to know about wiring at all - a hull
// missing a device simply has no wires for it.
public sealed partial class Ship
{
    public static Ship FromCustomDefinition(CustomShipDefinition def)
    {
        var errors = CustomShipValidator.Validate(def);
        if (errors.Count > 0)
            throw new InvalidOperationException("Invalid custom ship definition: " + string.Join("; ", errors));

        var rooms = def.Rooms.Select(r => new Room(r.Id, r.Name, r.X, r.Y, r.Width, r.Height)).ToList();
        var doors = BuildDoors(def);
        var airlockOuterDoors = BuildAirlockOuterDoors(def);
        var wallBlocks = BuildWallBlocks(def);
        wallBlocks.AddRange(GenerateInteriorWallBlocks(rooms));
        ApplyWallMaterials(wallBlocks, rooms, def.WallMaterials);

        var reactorDevice = def.Devices.First(d => d.Kind == CustomDeviceKind.Reactor);
        var distributionDevice = def.Devices.First(d => d.Kind == CustomDeviceKind.Distribution);
        var helmDevice = def.Devices.First(d => d.Kind == CustomDeviceKind.Helm);
        var navigationDevice = def.Devices.First(d => d.Kind == CustomDeviceKind.Navigation);
        // Content-каталог отсеков - "bonus, not list" (Ship.cs's own doc comment on ReactorDeviceCount/
        // ExtraHelmConsoles/etc.): a 2nd+ device of one of these kinds never gets its own physical
        // object (still just the first one, above), only counts toward World.ShipBuilding.cs's bonus
        // recompute, PLUS (this session's own bug fix) its own real position - Ship.ToDefinition()
        // needs that to re-emit a bonus device where it actually is, not collapsed onto the primary's
        // position, or M63's structural-detachment bounds check can never tell the two apart and the
        // bonus silently survives destroying the room it's actually built in. Every kind that follows
        // this "one physical fixture + N bonus copies" shape goes through the ONE helper below -
        // ExtraPositionsOfKind - rather than each hand-rolling its own Where/Skip(1); a future kind
        // added the hand-rolled way is exactly how this session's bug happened in the first place.
        var reactorDeviceCount = def.Devices.Count(d => d.Kind == CustomDeviceKind.Reactor);
        var distributionDeviceCount = def.Devices.Count(d => d.Kind == CustomDeviceKind.Distribution);
        var helmDeviceCount = def.Devices.Count(d => d.Kind == CustomDeviceKind.Helm);
        var navigationDeviceCount = def.Devices.Count(d => d.Kind == CustomDeviceKind.Navigation);
        var extraReactorPositions = ExtraPositionsOfKind(def, CustomDeviceKind.Reactor);
        var extraDistributionPositions = ExtraPositionsOfKind(def, CustomDeviceKind.Distribution);
        var extraHelmConsoles = ExtraPositionsOfKind(def, CustomDeviceKind.Helm)
            .Select((p, i) => new HelmConsole($"helm-console-extra-{i + 1}", RoomIdAt(rooms, p), p.AsFloat().X, p.AsFloat().Y)).ToList();
        var extraNavigationConsoles = ExtraPositionsOfKind(def, CustomDeviceKind.Navigation)
            .Select((p, i) => new NavigationConsole($"navigation-console-extra-{i + 1}", RoomIdAt(rooms, p), p.AsFloat().X, p.AsFloat().Y)).ToList();

        // The reactor now always draws its own fixed 4x4-tile texture (ShipRenderer.ReactorBlockSize),
        // guaranteed by the Ship Editor's own footprint placement rules - so SizeScale stays at its
        // default 1f here too, same as every hand-authored hull except Corvette's own hand-tuned
        // SizeScale: 1.8f (untouched by this, that hull is built directly, never through here).
        var reactorBlock = new ReactorBlock("reactor-block", RoomIdAt(rooms, reactorDevice), reactorDevice.X, reactorDevice.Y);
        var distributionBlock = new PowerDistributionBlock("distribution-block", RoomIdAt(rooms, distributionDevice), distributionDevice.X, distributionDevice.Y);
        // Genuinely optional like CardTable/Jukebox: a placed CustomDeviceKind.Battery wins; a hull
        // that never places one falls back to the old auto-placement right next to the reactor, so
        // an editor-drawn hull from before this device kind existed keeps working unchanged.
        var batteryDevice = def.Devices.FirstOrDefault(d => d.Kind == CustomDeviceKind.Battery);
        var batteryBlock = batteryDevice is not null
            ? new BatteryBlock("battery-block", RoomIdAt(rooms, batteryDevice), batteryDevice.X, batteryDevice.Y)
            : new BatteryBlock("battery-block", RoomIdAt(rooms, reactorDevice), reactorDevice.X + 1f, reactorDevice.Y + 1f);
        var helmConsole = new HelmConsole("helm-console", RoomIdAt(rooms, helmDevice), helmDevice.X, helmDevice.Y);
        var navigationConsole = new NavigationConsole("navigation-console", RoomIdAt(rooms, navigationDevice), navigationDevice.X, navigationDevice.Y);

        var systemDevices = BuildSystemDevices(def, rooms);
        var turrets = BuildTurrets(def, rooms);
        var ammoStorages = BuildSimpleDevices(def, rooms, CustomDeviceKind.AmmoStorage,
            (id, roomId, x, y) => new AmmoStorage(id, roomId, x, y));
        var suitLockers = BuildSimpleDevices(def, rooms, CustomDeviceKind.SuitLocker,
            (id, roomId, x, y) => new SuitLocker(id, roomId, x, y));
        var storageRacks = BuildSimpleDevices(def, rooms, CustomDeviceKind.StorageRack,
            (id, roomId, x, y) => new StorageRack(id, roomId, x, y));

        var cardTableDevice = def.Devices.FirstOrDefault(d => d.Kind == CustomDeviceKind.CardTable);
        var cardTable = cardTableDevice is not null
            ? new CardTable("card-table", RoomIdAt(rooms, cardTableDevice), cardTableDevice.X, cardTableDevice.Y)
            : new CardTable("card-table-auto", rooms[0].Id, (float)rooms[0].Center.X, (float)rooms[0].Center.Y);

        // The Ship Editor still doesn't offer placing either of these (M48 only wired up the
        // hand-authored classes), so an editor-drawn hull simply has none - but CustomDeviceKind.
        // Camera/ComponentMount (M60 follow-up) let Ship.ToDefinition() round-trip a hand-authored
        // hull's existing ones instead of silently deleting them the moment it goes through a
        // build/definition round trip.
        var cameras = def.Devices.Where(d => d.Kind == CustomDeviceKind.Camera)
            .Select((d, i) => new HullCamera($"camera-{i + 1}", RoomIdAt(rooms, d), d.X, d.Y, d.CameraSide ?? CameraMountSide.Aft))
            .ToList();
        var componentMounts = def.Devices.Where(d => d.Kind == CustomDeviceKind.ComponentMount)
            .Select((d, i) => new ComponentMount($"mount-{i + 1}", RoomIdAt(rooms, d), d.X, d.Y, d.TargetDoorId))
            .ToList();

        // Unlike CardTable, genuinely optional - a hull the player never dropped one onto simply
        // has no jukebox at all rather than an auto-placed fallback.
        var jukeboxDevice = def.Devices.FirstOrDefault(d => d.Kind == CustomDeviceKind.Jukebox);
        var jukebox = jukeboxDevice is not null
            ? new Jukebox("jukebox", RoomIdAt(rooms, jukeboxDevice), jukeboxDevice.X, jukeboxDevice.Y)
            : null;

        var engines = def.Engines
            .Select((e, i) => new ShipEngine($"engine-{i + 1}", RoomIdAt(rooms, new Vec2(e.X, e.Y)), e.X, e.Y, e.Facing, e.MaxThrust, e.Role))
            .ToList();

        return new Ship(rooms, doors, airlockOuterDoors, turrets, cameras, ammoStorages, suitLockers, systemDevices, wallBlocks,
            reactorBlock, distributionBlock, batteryBlock, navigationConsole, helmConsole, storageRacks,
            helmConsole.Position, helmConsole.RoomId, cardTable, def.ForwardDegrees, componentMounts: componentMounts, jukebox: jukebox,
            reactorDeviceCount: reactorDeviceCount, distributionDeviceCount: distributionDeviceCount,
            helmDeviceCount: helmDeviceCount, navigationDeviceCount: navigationDeviceCount,
            extraHelmConsoles: extraHelmConsoles, extraNavigationConsoles: extraNavigationConsoles,
            extraReactorPositions: extraReactorPositions, extraDistributionPositions: extraDistributionPositions,
            engines: engines, isCustomBuilt: true);
    }

    private static string RoomIdAt(List<Room> rooms, CustomDeviceDef device) =>
        rooms.First(r => r.Contains(new Vec2(device.X, device.Y))).Id;

    private static string RoomIdAt(List<Room> rooms, Vec2 position) =>
        rooms.First(r => r.Contains(position)).Id;

    // Content-каталог отсеков - the one shared shape every "bonus, not list" device kind (Reactor,
    // Distribution, Helm, Navigation - Ship.cs's own doc comment) follows: the FIRST device of a
    // kind becomes the one physical fixture (built separately, above), every device AFTER it is a
    // bonus-only extra whose own real position still has to survive Ship.ToDefinition()'s round
    // trip (this session's own bug: Reactor/Distribution used to lose that position and collapse
    // onto the fixture's own, so M63's structural detachment could never remove a bonus reactor by
    // destroying the room it actually lived in). Route any FUTURE bonus-only device kind through
    // this helper rather than a fresh hand-rolled Where/Skip(1) - that hand-rolling is exactly how
    // Reactor/Distribution lost their positions in the first place.
    private static List<Vec2> ExtraPositionsOfKind(CustomShipDefinition def, CustomDeviceKind kind) =>
        def.Devices.Where(d => d.Kind == kind).Skip(1).Select(d => new Vec2(d.X, d.Y)).ToList();

    private static List<Door> BuildDoors(CustomShipDefinition def)
    {
        var overlaps = ShipLayoutGeometry.FindRoomPairOverlaps(def.Rooms);
        var doors = new List<Door>();
        var index = 0;
        foreach (var doorDef in def.Doors)
        {
            var found = false;
            foreach (var overlap in overlaps)
            {
                var matches = (overlap.RoomAId == doorDef.RoomAId && overlap.RoomBId == doorDef.RoomBId)
                    || (overlap.RoomAId == doorDef.RoomBId && overlap.RoomBId == doorDef.RoomAId);
                if (!matches)
                    continue;

                var span = MathF.Min(Door.StandardSpanUnits, overlap.OverlapLength);
                doors.Add(overlap.Vertical
                    ? new Door($"door-{index++}", overlap.RoomAId, overlap.RoomBId, overlap.At, overlap.OverlapCenter, 1.0f, span)
                    : new Door($"door-{index++}", overlap.RoomAId, overlap.RoomBId, overlap.OverlapCenter, overlap.At, span, 1.0f));
                found = true;
                break;
            }
            if (!found)
                throw new InvalidOperationException($"Door between {doorDef.RoomAId} and {doorDef.RoomBId} has no shared wall.");
        }
        return doors;
    }

    private static List<AirlockOuterDoor> BuildAirlockOuterDoors(CustomShipDefinition def)
    {
        var roomsById = def.Rooms.ToDictionary(r => r.Id);
        var airlocks = new List<AirlockOuterDoor>();
        var index = 0;
        foreach (var airlockDef in def.Airlocks)
        {
            var room = roomsById[airlockDef.RoomId];
            var (midX, midY) = ShipLayoutGeometry.SideMidpoint(room, airlockDef.Side);
            var span = MathF.Min(Door.StandardSpanUnits, ShipLayoutGeometry.SideLength(room, airlockDef.Side));
            var vertical = airlockDef.Side is EdgeSide.Left or EdgeSide.Right;
            airlocks.Add(vertical
                ? new AirlockOuterDoor($"airlock-{index++}", room.Id, midX, midY, 1.0f, span)
                : new AirlockOuterDoor($"airlock-{index++}", room.Id, midX, midY, span, 1.0f));
        }
        return airlocks;
    }

    // One 1x1 block per unit segment of a room's boundary that has no neighboring room on that
    // side at all AND isn't the dedicated airlock side (matches GenerateOuterWallBlocks' own rule
    // plus the airlock chambers' convention of never mixing ordinary hull into the airlock's wall -
    // see Ship.cs's CreateStarter comment on the original hand-authored hulls).
    private static List<WallBlock> BuildWallBlocks(CustomShipDefinition def)
    {
        var blocks = new List<WallBlock>();
        foreach (var room in def.Rooms)
        {
            var hasAirlock = new HashSet<EdgeSide>(def.Airlocks.Where(a => a.RoomId == room.Id).Select(a => a.Side));
            var index = 0;

            if (!hasAirlock.Contains(EdgeSide.Top))
                for (var x = room.X; x < room.X + room.Width; x++)
                    if (!IsUnitCovered(def.Rooms, room, EdgeSide.Top, x))
                        blocks.Add(new WallBlock($"{room.Id}-wall-{index++}", room.Id, x + 0.5f, room.Y));

            if (!hasAirlock.Contains(EdgeSide.Bottom))
                for (var x = room.X; x < room.X + room.Width; x++)
                    if (!IsUnitCovered(def.Rooms, room, EdgeSide.Bottom, x))
                        blocks.Add(new WallBlock($"{room.Id}-wall-{index++}", room.Id, x + 0.5f, room.Y + room.Height));

            if (!hasAirlock.Contains(EdgeSide.Left))
                for (var y = room.Y; y < room.Y + room.Height; y++)
                    if (!IsUnitCovered(def.Rooms, room, EdgeSide.Left, y))
                        blocks.Add(new WallBlock($"{room.Id}-wall-{index++}", room.Id, room.X, y + 0.5f));

            if (!hasAirlock.Contains(EdgeSide.Right))
                for (var y = room.Y; y < room.Y + room.Height; y++)
                    if (!IsUnitCovered(def.Rooms, room, EdgeSide.Right, y))
                        blocks.Add(new WallBlock($"{room.Id}-wall-{index++}", room.Id, room.X + room.Width, y + 0.5f));
        }
        return blocks;
    }

    // unitStart: float, not int (M60 follow-up) - a half-unit hand-authored hull (Ship.Corvette.cs)
    // round-tripped through a CustomShipDefinition walks this in 1-unit steps starting from a
    // fractional room edge (e.g. 4.5, 5.5, ...), same as Ship.cs's own GenerateOuterWallBlocks
    // already does directly - this just needed to stop assuming the start was always whole.
    private static bool IsUnitCovered(IReadOnlyList<CustomRoomDef> rooms, CustomRoomDef room, EdgeSide side, float unitStart)
    {
        foreach (var other in rooms)
        {
            if (other.Id == room.Id)
                continue;
            var covers = side switch
            {
                EdgeSide.Top => other.Y + other.Height == room.Y && other.X <= unitStart && other.X + other.Width >= unitStart + 1,
                EdgeSide.Bottom => other.Y == room.Y + room.Height && other.X <= unitStart && other.X + other.Width >= unitStart + 1,
                EdgeSide.Left => other.X + other.Width == room.X && other.Y <= unitStart && other.Y + other.Height >= unitStart + 1,
                EdgeSide.Right => other.X == room.X + room.Width && other.Y <= unitStart && other.Y + other.Height >= unitStart + 1,
                _ => false,
            };
            if (covers)
                return true;
        }
        return false;
    }

    // Copies a painted tile's non-Standard material (def.WallMaterials, keyed by the SAME hull-local
    // tile coordinate the Ship Editor's canvas uses) onto whichever generated WallBlock actually
    // landed on that tile - reusing TileGridRasterizer.WallBlockTileCoord (M72's own "block position
    // -> tile coordinate" helper) rather than re-deriving that mapping here. Runs over BOTH exterior
    // (BuildWallBlocks) and interior (GenerateInteriorWallBlocks) blocks uniformly since both place
    // every block on its own RoomId's edge - a no-op (empty materials list) for every hull that never
    // painted a Reinforced/Window tile, hand-authored or custom alike.
    private static void ApplyWallMaterials(List<WallBlock> wallBlocks, List<Room> rooms, IReadOnlyList<CustomWallMaterialDef> materials)
    {
        if (materials.Count == 0)
            return;
        var materialByTile = materials.ToDictionary(m => new TileCoord(m.X, m.Y), m => m.Material);
        var roomsById = rooms.ToDictionary(r => r.Id);
        for (var i = 0; i < wallBlocks.Count; i++)
        {
            var block = wallBlocks[i];
            var coord = TileGridRasterizer.WallBlockTileCoord(block, rooms, roomsById[block.RoomId]);
            if (materialByTile.TryGetValue(coord, out var material))
                wallBlocks[i] = block with { Material = material };
        }
    }

    private static readonly IReadOnlyDictionary<CustomDeviceKind, PowerSystemId> SystemDeviceKinds = new Dictionary<CustomDeviceKind, PowerSystemId>
    {
        [CustomDeviceKind.Engine] = PowerSystemId.Engine,
        [CustomDeviceKind.Shields] = PowerSystemId.Shields,
        [CustomDeviceKind.WeaponCharger] = PowerSystemId.WeaponCharger,
        [CustomDeviceKind.Oxygen] = PowerSystemId.Oxygen,
        [CustomDeviceKind.Secondary] = PowerSystemId.Secondary,
    };

    private static List<ShipSystemDevice> BuildSystemDevices(CustomShipDefinition def, List<Room> rooms)
    {
        var devices = new List<ShipSystemDevice>();
        foreach (var (kind, system) in SystemDeviceKinds)
        {
            var placed = def.Devices.Where(d => d.Kind == kind).ToList();
            for (var i = 0; i < placed.Count; i++)
                devices.Add(new ShipSystemDevice($"system-{system}-{i + 1}".ToLowerInvariant(), RoomIdAt(rooms, placed[i]), placed[i].X, placed[i].Y, system,
                    ThrustBonus: placed[i].ThrustBonus, TurnBonus: placed[i].TurnBonus, CapacityBonus: placed[i].CapacityBonus));
        }
        return devices;
    }

    private static List<Turret> BuildTurrets(CustomShipDefinition def, List<Room> rooms)
    {
        var turrets = new List<Turret>();
        var index = 0;
        foreach (var device in def.Devices.Where(d => d.Kind is CustomDeviceKind.TurretBallistic or CustomDeviceKind.TurretLaser or CustomDeviceKind.TurretMachineGun))
        {
            var roomId = RoomIdAt(rooms, device);
            // The editor's own placeable catalog (CustomDeviceKind) still only offers the first two
            // slots - the Magnetic cannon just sits behind the same "ballistic" icon it always has.
            // TurretMachineGun (M60 follow-up) isn't offered by the editor either, but round-trips
            // the Cruiser's own hand-authored 3rd turret (Ship.Cruiser.cs) instead of dropping it.
            turrets.Add(device.Kind switch
            {
                CustomDeviceKind.TurretBallistic => new Turret($"turret-{index++}", roomId, device.X, device.Y, MinAimDegrees: -45f, MaxAimDegrees: 45f,
                    DamagePerShot: TurretBalance.MagneticDamage, CooldownSeconds: TurretBalance.MagneticCooldownSeconds,
                    WeaponType: TurretWeaponType.Magnetic, MagazineCapacity: TurretBalance.MagneticMagazineCapacity,
                    MountSide: device.MountSide),
                CustomDeviceKind.TurretMachineGun => new Turret($"turret-{index++}", roomId, device.X, device.Y, MinAimDegrees: -45f, MaxAimDegrees: 45f,
                    DamagePerShot: TurretBalance.MachineGunDamagePerPellet, CooldownSeconds: TurretBalance.MachineGunCooldownSeconds,
                    WeaponType: TurretWeaponType.MachineGun, MagazineCapacity: TurretBalance.MachineGunMagazineCapacity,
                    PelletsPerBurst: TurretBalance.MachineGunPelletsPerBurst, PelletSpreadDegrees: TurretBalance.MachineGunPelletSpreadDegrees,
                    MountSide: device.MountSide),
                _ => new Turret($"turret-{index++}", roomId, device.X, device.Y, MinAimDegrees: -45f, MaxAimDegrees: 45f,
                    DamagePerShot: TurretBalance.LaserDamagePerTick, CooldownSeconds: TurretBalance.LaserTickIntervalSeconds,
                    WeaponType: TurretWeaponType.Laser, MaxCharge: TurretBalance.LaserMaxCharge,
                    ChargePerShot: TurretBalance.LaserChargePerTick,
                    RechargePerPowerUnitPerSecond: TurretBalance.LaserRechargePerPowerUnitPerSecond, MountSide: device.MountSide),
            });
        }
        return turrets;
    }

    private static List<T> BuildSimpleDevices<T>(CustomShipDefinition def, List<Room> rooms, CustomDeviceKind kind,
        Func<string, string, float, float, T> create)
    {
        var placed = def.Devices.Where(d => d.Kind == kind).ToList();
        var result = new List<T>();
        for (var i = 0; i < placed.Count; i++)
            result.Add(create($"{kind}-{i + 1}".ToLowerInvariant(), RoomIdAt(rooms, placed[i]), placed[i].X, placed[i].Y));
        return result;
    }
}
