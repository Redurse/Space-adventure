namespace SpaceAdventure.Shared.Model;

// M60 - the reverse of Ship.Custom.cs's FromCustomDefinition: turns a LIVE Ship (hand-authored or
// already-Custom) back into a CustomShipDefinition. This is what makes every existing hull, not just
// already-Custom ones, buildable-on-top-of at runtime (World.ShipBuilding.cs appends one more room
// to whatever ToDefinition() returns, then rebuilds via FromCustomDefinition the same way a fresh
// Custom hull already does). Room ids are preserved exactly (CustomRoomDef.Id carries Room.Id
// through unchanged), so wall-block/oxygen/door state keyed by a pre-existing room id keeps lining
// up after a rebuild; device ids are NOT preserved (FromCustomDefinition always renumbers them from
// scratch, same as a whole-hull swap already does) - acceptable for now, same known simplification
// World.ShipBuilding.cs's own doc comment calls out.
public sealed partial class Ship
{
    public CustomShipDefinition ToDefinition()
    {
        var rooms = Rooms.Select(r => new CustomRoomDef(r.Id, r.Name, r.X, r.Y, r.Width, r.Height)).ToList();
        var doors = Doors.Select(d => new CustomDoorDef(d.RoomAId, d.RoomBId)).ToList();
        var airlocks = AirlockOuterDoors
            .Select(a => new CustomAirlockDef(a.RoomId, InferAirlockSide(GetRoom(a.RoomId), a.X, a.Y)))
            .ToList();

        var devices = new List<CustomDeviceDef> { new(CustomDeviceKind.CardTable, CardTable.X, CardTable.Y) };
        // ReactorDeviceCount/etc. (Ship.cs's own doc comment - content-каталог "бонус, не список")
        // are always 1 for a hand-authored hull, so this is exactly the single entry ToDefinition()
        // always emitted before. For a Custom hull with extra bonus-only reactor rooms, ExtraReactor
        // Positions/ExtraDistributionPositions (Ship.cs's own doc comment) carry each bonus device's
        // own real position - required for M63's structural detachment to ever be able to tell a
        // bonus device apart from the ship's original one by bounds-checking its position.
        devices.Add(new CustomDeviceDef(CustomDeviceKind.Reactor, ReactorBlock.X, ReactorBlock.Y));
        devices.AddRange(ExtraReactorPositions.Select(p => new CustomDeviceDef(CustomDeviceKind.Reactor, p.AsFloat().X, p.AsFloat().Y)));
        devices.Add(new CustomDeviceDef(CustomDeviceKind.Distribution, DistributionBlock.X, DistributionBlock.Y));
        devices.AddRange(ExtraDistributionPositions.Select(p => new CustomDeviceDef(CustomDeviceKind.Distribution, p.AsFloat().X, p.AsFloat().Y)));
        devices.Add(new CustomDeviceDef(CustomDeviceKind.Battery, BatteryBlock.X, BatteryBlock.Y));
        // Helm/Navigation extras DO carry their own real position (ExtraHelmConsoles/
        // ExtraNavigationConsoles, Ship.cs's own doc comment) - a second bridge room is somewhere a
        // player can actually walk to and pilot from, unlike a bonus-only extra reactor.
        devices.Add(new CustomDeviceDef(CustomDeviceKind.Helm, HelmConsole.X, HelmConsole.Y));
        devices.AddRange(ExtraHelmConsoles.Select(c => new CustomDeviceDef(CustomDeviceKind.Helm, c.X, c.Y)));
        devices.Add(new CustomDeviceDef(CustomDeviceKind.Navigation, NavigationConsole.X, NavigationConsole.Y));
        devices.AddRange(ExtraNavigationConsoles.Select(c => new CustomDeviceDef(CustomDeviceKind.Navigation, c.X, c.Y)));
        devices.AddRange(SystemDevices.Select(d => new CustomDeviceDef(SystemDeviceKindFor(d.System), d.X, d.Y,
            ThrustBonus: d.ThrustBonus, TurnBonus: d.TurnBonus, CapacityBonus: d.CapacityBonus)));
        devices.AddRange(Turrets.Select(t => new CustomDeviceDef(TurretDeviceKindFor(t.WeaponType), t.PeriscopeX, t.PeriscopeY, MountSide: t.MountSide)));
        devices.AddRange(AmmoStorages.Select(a => new CustomDeviceDef(CustomDeviceKind.AmmoStorage, a.X, a.Y)));
        devices.AddRange(SuitLockers.Select(s => new CustomDeviceDef(CustomDeviceKind.SuitLocker, s.X, s.Y)));
        devices.AddRange(StorageRacks.Select(s => new CustomDeviceDef(CustomDeviceKind.StorageRack, s.X, s.Y)));
        devices.AddRange(Cameras.Select(c => new CustomDeviceDef(CustomDeviceKind.Camera, c.X, c.Y, CameraSide: c.MountSide)));
        devices.AddRange(ComponentMounts.Select(m => new CustomDeviceDef(CustomDeviceKind.ComponentMount, m.X, m.Y, TargetDoorId: m.TargetDoorId)));
        if (Jukebox is { } jukebox)
            devices.Add(new CustomDeviceDef(CustomDeviceKind.Jukebox, jukebox.X, jukebox.Y));

        return new CustomShipDefinition("Мой корабль", rooms, doors, airlocks, devices, ForwardDegrees);
    }

    private static readonly IReadOnlyDictionary<PowerSystemId, CustomDeviceKind> SystemDeviceKindsReverse =
        new Dictionary<PowerSystemId, CustomDeviceKind>
        {
            [PowerSystemId.Engine] = CustomDeviceKind.Engine,
            [PowerSystemId.Shields] = CustomDeviceKind.Shields,
            [PowerSystemId.WeaponCharger] = CustomDeviceKind.WeaponCharger,
            [PowerSystemId.Oxygen] = CustomDeviceKind.Oxygen,
            [PowerSystemId.Secondary] = CustomDeviceKind.Secondary,
        };

    private static CustomDeviceKind SystemDeviceKindFor(PowerSystemId system) => SystemDeviceKindsReverse[system];

    private static CustomDeviceKind TurretDeviceKindFor(TurretWeaponType weaponType) => weaponType switch
    {
        TurretWeaponType.Magnetic => CustomDeviceKind.TurretBallistic,
        TurretWeaponType.MachineGun => CustomDeviceKind.TurretMachineGun,
        _ => CustomDeviceKind.TurretLaser,
    };

    // An AirlockOuterDoor always sits ON one specific wall - its dominant coordinate (X for a
    // Left/Right door, Y for a Top/Bottom one) is always EXACTLY that wall's own boundary value,
    // even though hand-authored placements don't always center it along the wall's own span
    // (Ship.Corvette.cs's own two airlocks sit well off-centre, by design - "docking port... right
    // next to them"). Nearest-side-midpoint distance used to be tried here first and got this wrong
    // for exactly that off-centre case (Top's own midpoint measured closer than Right's, even though
    // the door is genuinely on the Right wall) - exact-boundary matching is what the placement
    // itself actually guarantees, so it's what has to be checked, not proximity.
    private static EdgeSide InferAirlockSide(Room room, float x, float y)
    {
        const float Epsilon = 0.01f;
        if (MathF.Abs(x - room.Right) < Epsilon) return EdgeSide.Right;
        if (MathF.Abs(x - room.Left) < Epsilon) return EdgeSide.Left;
        if (MathF.Abs(y - room.Bottom) < Epsilon) return EdgeSide.Bottom;
        return EdgeSide.Top;
    }
}
