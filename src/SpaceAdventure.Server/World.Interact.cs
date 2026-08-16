using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed partial class World
{
    private const float InteractionRadius = 1.0f; // units, distance to a periscope/storage/locker to interact with it
    private const float SuitActionDurationSeconds = 2f;

    // Single F key, priority-ordered: 0) ignored entirely while mid-equip/unequip (can't react
    // instantly, game_design.md section 2), 1) stand up if manning, 2) use a held MedKit if hurt
    // (game_design.md section 4, M12 - anywhere, no proximity needed, since it's self-treatment
    // not a station), 3) reload a turret if carrying a crate, 4) pick up a crate/tool at a
    // station, 5) repair a damaged turret/system or man a free turret, 6) start putting on/taking
    // off a suit at a locker, 7) weld the nearest breached hull block in the current room.
    private void HandleInteract(Character character)
    {
        if (character.SuitActionRemaining > 0)
            return;

        if (character.IsOutside)
        {
            HandleEvaInteract(character);
            return;
        }

        // A station room has none of the ship's fixtures in it - the only thing to interact with
        // is somebody else's property (World.StationCrime.cs).
        if (character.OnStation)
        {
            TryStealCrate(character);
            return;
        }

        if (character.ManningTurretId is { } manned)
        {
            _turretRuntimes[manned].MannedByPlayerId = null;
            character.ManningTurretId = null;
            return;
        }

        if (character.IsAtHelm)
        {
            character.IsAtHelm = false; // the last commanded thrust is deliberately left as-is (game_design.md Phase 3, M15)
            return;
        }

        if (character.Inventory.IsHolding(ItemType.MedKit) && character.Health < Character.MaxHealth)
        {
            TryUseMedKit(character);
            return;
        }

        var nearbyTurret = Ship.Turrets.FirstOrDefault(t =>
            t.RoomId == character.RoomId &&
            (t.PeriscopePosition - character.Position).Length() < InteractionRadius);

        if (character.CarryingAmmoCrate)
        {
            if (nearbyTurret is null || nearbyTurret.WeaponType != TurretWeaponType.Ballistic)
                return; // laser turrets don't take ammo crates

            var runtime = _turretRuntimes[nearbyTurret.Id];
            runtime.AmmoRemaining = runtime.Definition.MagazineCapacity;
            character.Inventory.TryRemove(ItemType.AmmoCrate);
            return;
        }

        var nearbyStorage = Ship.AmmoStorages.FirstOrDefault(s =>
            s.RoomId == character.RoomId &&
            (s.Position - character.Position).Length() < InteractionRadius);

        if (nearbyStorage is not null)
        {
            character.Inventory.TryAdd(ItemType.AmmoCrate); // no-op if the inventory row is full
            return;
        }

        var nearbyToolStation = Ship.ToolStations.FirstOrDefault(s =>
            s.RoomId == character.RoomId &&
            (s.Position - character.Position).Length() < InteractionRadius);

        if (nearbyToolStation is not null)
        {
            character.Inventory.TryAdd(nearbyToolStation.Item); // no-op if the inventory row is full
            return;
        }

        if (nearbyTurret is not null)
        {
            var runtime = _turretRuntimes[nearbyTurret.Id];
            if (runtime.Damaged)
            {
                // Needs a repair tool actually held in hand — matches the game's tool/hand model.
                if (character.Inventory.IsHolding(ItemType.Wrench) || character.Inventory.IsHolding(ItemType.Screwdriver))
                    runtime.Damaged = false;
                return;
            }

            if (runtime.MannedByPlayerId is null)
            {
                runtime.MannedByPlayerId = character.PlayerId;
                character.ManningTurretId = nearbyTurret.Id;
            }
            return;
        }

        // Only intercepts a damaged block — a healthy one has nothing to do here, so F falls
        // through to whatever's next (e.g. welding a breach at the same spot).
        var nearbyDamagedSystem = Ship.SystemDevices.FirstOrDefault(d =>
            d.RoomId == character.RoomId &&
            (d.Position - character.Position).Length() < InteractionRadius &&
            !IsDeviceConnected(d.Id));

        if (nearbyDamagedSystem is not null)
        {
            if (character.Inventory.IsHolding(ItemType.Wrench) || character.Inventory.IsHolding(ItemType.Screwdriver))
                RepairDeviceWiring(nearbyDamagedSystem.Id);
            return;
        }

        if (Ship.HelmConsole.RoomId == character.RoomId &&
            (Ship.HelmConsole.Position - character.Position).Length() < InteractionRadius)
        {
            character.IsAtHelm = true;
            return;
        }

        var nearbyLocker = Ship.SuitLockers.FirstOrDefault(l =>
            l.RoomId == character.RoomId &&
            (l.Position - character.Position).Length() < InteractionRadius);

        if (nearbyLocker is not null)
        {
            character.SuitActionRemaining = SuitActionDurationSeconds;
            character.SuitActionEquipping = !character.WearingSuit;
            return;
        }

        // Lowest priority: weld shut the nearest breached hull block in the room you're standing
        // in — each breach is its own block, so a room with several needs several visits. Only
        // intercepts if a breach is actually in range; needs the (two-handed) welding tool held.
        var nearbyBreachedBlock = Ship.WallBlocks
            .Where(b => b.RoomId == character.RoomId && _breachedWallBlockIds.Contains(b.Id) &&
                        (b.Position - character.Position).Length() < InteractionRadius)
            .OrderBy(b => (b.Position - character.Position).Length())
            .FirstOrDefault();

        if (nearbyBreachedBlock is not null && character.Inventory.IsHolding(ItemType.WeldingTool))
            _breachedWallBlockIds.Remove(nearbyBreachedBlock.Id);
    }
}
