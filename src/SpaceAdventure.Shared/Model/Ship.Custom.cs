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

        var reactorDevice = def.Devices.First(d => d.Kind == CustomDeviceKind.Reactor);
        var distributionDevice = def.Devices.First(d => d.Kind == CustomDeviceKind.Distribution);
        var helmDevice = def.Devices.First(d => d.Kind == CustomDeviceKind.Helm);
        var navigationDevice = def.Devices.First(d => d.Kind == CustomDeviceKind.Navigation);

        var reactorBlock = new ReactorBlock("reactor-block", RoomIdAt(rooms, reactorDevice), reactorDevice.X, reactorDevice.Y);
        var distributionBlock = new PowerDistributionBlock("distribution-block", RoomIdAt(rooms, distributionDevice), distributionDevice.X, distributionDevice.Y);
        // No dedicated CustomDeviceKind for it (the Ship Editor doesn't offer placing one) - it
        // rides along next to the reactor block instead, same room, offset just enough not to overlap.
        var batteryBlock = new BatteryBlock("battery-block", RoomIdAt(rooms, reactorDevice), reactorDevice.X + 1f, reactorDevice.Y + 1f);
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
            : new CardTable("card-table-auto", rooms[0].Id, rooms[0].Center.X, rooms[0].Center.Y);

        return new Ship(rooms, doors, airlockOuterDoors, turrets, ammoStorages, suitLockers, systemDevices, wallBlocks,
            reactorBlock, distributionBlock, batteryBlock, navigationConsole, helmConsole, storageRacks,
            helmConsole.Position, helmConsole.RoomId, cardTable, def.ForwardDegrees);
    }

    private static string RoomIdAt(List<Room> rooms, CustomDeviceDef device) =>
        rooms.First(r => r.Contains(new Vec2(device.X, device.Y))).Id;

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

                var span = MathF.Min(1.8f, overlap.OverlapLength);
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
            var span = MathF.Min(1.8f, ShipLayoutGeometry.SideLength(room, airlockDef.Side));
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

    private static bool IsUnitCovered(IReadOnlyList<CustomRoomDef> rooms, CustomRoomDef room, EdgeSide side, int unitStart)
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
                devices.Add(new ShipSystemDevice($"system-{system}-{i + 1}".ToLowerInvariant(), RoomIdAt(rooms, placed[i]), placed[i].X, placed[i].Y, system));
        }
        return devices;
    }

    private static List<Turret> BuildTurrets(CustomShipDefinition def, List<Room> rooms)
    {
        var turrets = new List<Turret>();
        var index = 0;
        foreach (var device in def.Devices.Where(d => d.Kind is CustomDeviceKind.TurretBallistic or CustomDeviceKind.TurretLaser))
        {
            var roomId = RoomIdAt(rooms, device);
            turrets.Add(device.Kind == CustomDeviceKind.TurretBallistic
                ? new Turret($"turret-{index++}", roomId, device.X, device.Y, MinAimDegrees: -45f, MaxAimDegrees: 45f,
                    DamagePerShot: 10f, CooldownSeconds: 0.5f, WeaponType: TurretWeaponType.Ballistic,
                    MagazineCapacity: 6, MountSide: device.MountSide)
                : new Turret($"turret-{index++}", roomId, device.X, device.Y, MinAimDegrees: -45f, MaxAimDegrees: 45f,
                    DamagePerShot: 8f, CooldownSeconds: 0.4f, WeaponType: TurretWeaponType.Laser,
                    MaxCharge: 30f, ChargePerShot: 10f, RechargePerPowerUnitPerSecond: 0.5f, MountSide: device.MountSide));
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
