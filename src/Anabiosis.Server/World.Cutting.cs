using Anabiosis.Shared.Model;
using Anabiosis.Shared.Protocol;

namespace Anabiosis.Server;

// The cutter as a tool rather than a keypress. Holding the button lights a short blue flame from
// the character toward the cursor; anything the flame touches gets cut, and out here the only thing
// that can be cut is a block of ore, which comes apart over a few seconds and drops an item where
// it stood.
//
// This replaces the old "stand next to a marker and press F, three times, done" mining: the flame
// is aimed, it burns tank oxygen while lit, and the block's remaining hit points are shown as a bar
// so the work is visible while it happens.
public sealed partial class World
{
    // Short: this is a hand torch, not a beam weapon. Long enough to reach a block from a
    // comfortable standing distance, short enough that you have to be at the rock.
    public const float CutterReachUnits = 1.7f;
    private const float CutterDamagePerSecond = 34f; // a block of 100 takes about three seconds
    private const int CutterSamples = 6; // points along the flame tested against the blocks

    private readonly Dictionary<int, bool> _cutInput = new();
    private readonly Dictionary<string, float> _oreDepositHp = new();

    public bool IsCutting(int playerId) => _cutInput.GetValueOrDefault(playerId) && CanCut(_characters[playerId]);

    // Holding a cutter is not enough: it needs a tank with something left in it, which is the whole
    // point of the socket (World.OxygenTanks.cs).
    private bool CanCut(Character character)
    {
        var slot = character.Inventory.HeldSlotOf(ItemType.Cutter);
        return slot >= 0 && character.Inventory.HasWorkingTank(slot);
    }

    private void StepCutting(double deltaSeconds)
    {
        foreach (var (playerId, held) in _cutInput)
        {
            if (!held || !_characters.TryGetValue(playerId, out var character) || character.Health <= 0)
                continue;

            var slot = character.Inventory.HeldSlotOf(ItemType.Cutter);
            if (slot < 0 || !character.Inventory.HasWorkingTank(slot))
                continue;

            character.Inventory.DrainTank(slot, OxygenTankDefinitions.CutterDrainPerSecond * (float)deltaSeconds);

            // The torch lights anywhere and burns its tank while it does. Outside, it bites into
            // ore first, then the player's own hull, then the currently boardable enemy's hull if
            // nothing else is out there to mine (CutAlongFlame - breaching it from the outside in,
            // same as WeldAlongFlame already patches it from either side); indoors, it bites into a
            // closed door or wall block belonging to whichever hull the character is actually
            // standing in - their own ship (CutIndoorAlongFlame) or a boarded enemy one
            // (CutIndoorAlongFlameOnEnemyShip). It lights the same way standing in a station's own
            // corridors, but nothing there ever matches (FindAimedWallBlock/AllShipDoors are keyed
            // to the ship's own ids, never a station's), so there's nothing there for it to cut.
            if (character.IsOutside)
                CutAlongFlame(character, deltaSeconds);
            else if (character.OnEnemyShip)
                CutIndoorAlongFlameOnEnemyShip(character, deltaSeconds);
            else
                CutIndoorAlongFlame(character, deltaSeconds);
        }
    }

    // A door or a hull block, whichever the flame actually reaches first - sampled together
    // (rather than checking every door along the whole ray before ever looking at a block) so a
    // near block can't lose out to a farther door on the same aim line. Doors and wall blocks
    // never overlap in the first place (Ship's own constructor drops any block landing on a
    // door's footprint), so in practice this only ever matches one or the other per sample.
    private readonly record struct AimedCutTarget(string? DoorId, string? WallBlockId);

    private AimedCutTarget FindAimedCutTarget(Character character)
    {
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection.Normalized() : character.FacingDirection;
        if (aim.Length() < 0.01f)
            return default;

        for (var i = 1; i <= WallCutSamples; i++)
        {
            var point = character.Position + aim * (WallCutReachUnits * i / WallCutSamples);

            var door = AllShipDoors().FirstOrDefault(d => d.Connects(character.RoomId) && !IsDoorOpen(d.Id) &&
                !IsDoorDestroyed(d.Id) && (d.Position - point).Length() <= WallCutPointRadius);
            if (door.Id is not null)
                return new AimedCutTarget(door.Id, null);

            var block = Ship.WallBlocks.FirstOrDefault(b =>
                b.RoomId == character.RoomId && (b.Position - point).Length() <= WallCutPointRadius);
            if (block is not null)
                return new AimedCutTarget(null, block.Id);
        }
        return default;
    }

    // A door cuts open the same gradual way a wall block does (ChopDoor - same call the axe makes,
    // World.Doors.cs), just fed by the torch's own per-second rate instead of a hand swing's flat
    // chunk. Forces it open at 0 Hp exactly like an axe finishing it off or combat damage wrecking
    // it outright - a cut-through door doesn't stay sealed either.
    private void CutIndoorAlongFlame(Character character, double deltaSeconds)
    {
        var target = FindAimedCutTarget(character);
        if (target.DoorId is { } doorId)
            ChopDoor(doorId, WallCutDamagePerSecond * (float)deltaSeconds);
        else if (target.WallBlockId is { } blockId)
            DamageWallBlock(blockId, WallCutDamagePerSecond * (float)deltaSeconds);
    }

    // Same shape as AimedCutTarget above, plus an AirlockId slot - a boarded hull's own two hatches
    // are reachable from the inside too (World.Boarding.cs's EnemyShipLayout.AirlockOuterDoors), so
    // a boarding party can cut one open (or weld one shut) from either side, not just from EVA.
    // Doesn't discriminate already-breached targets the way the cutting-only lookups elsewhere do -
    // shared by both tools, and repairing something back from 0 Hp is the one case where "already
    // breached" is exactly what you're aiming for.
    private readonly record struct EnemyToolTarget(string? DoorId, string? WallBlockId, string? AirlockId);

    // Mirrors FindAimedCutTarget, just against whichever enemy hull is currently boarded
    // (character.RoomId is meaningless against the player's own Ship.Doors/WallBlocks while
    // OnEnemyShip) - the interior Doors still go through the same World._doorHp/ChopDoor as the
    // player's own (door ids are globally unique per class, World.cs's own constructor comment), only
    // the hull's exterior (WallBlocks, AirlockOuterDoors) needs EnemyShipRuntime's per-instance Hp.
    private EnemyToolTarget FindAimedEnemyIndoorTarget(Character character, float reachUnits, int samples, float pointRadius)
    {
        if (BoardableEnemy is not { } enemy)
            return default;
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection.Normalized() : character.FacingDirection;
        if (aim.Length() < 0.01f)
            return default;

        for (var i = 1; i <= samples; i++)
        {
            var point = character.Position + aim * (reachUnits * i / samples);

            var door = enemy.Layout.Doors.FirstOrDefault(d => d.Connects(character.RoomId) && !IsDoorOpen(d.Id) &&
                !IsDoorDestroyed(d.Id) && (d.Position - point).Length() <= pointRadius);
            if (door is not null)
                return new EnemyToolTarget(door.Id, null, null);

            var airlock = enemy.Layout.AirlockOuterDoors.FirstOrDefault(d =>
                d.RoomId == character.RoomId && (d.Position - point).Length() <= pointRadius);
            if (airlock is not null)
                return new EnemyToolTarget(null, null, airlock.Id);

            var block = enemy.Layout.WallBlocks.FirstOrDefault(b =>
                b.RoomId == character.RoomId && (b.Position - point).Length() <= pointRadius);
            if (block is not null)
                return new EnemyToolTarget(null, block.Id, null);
        }
        return default;
    }

    // The boarded hull's own counterpart to CutIndoorAlongFlame above - cutting a defended door open
    // from behind it, or a wall panel/hatch, works exactly the same way it does on the player's own
    // ship, just aimed at whichever enemy is actually being boarded.
    private void CutIndoorAlongFlameOnEnemyShip(Character character, double deltaSeconds)
    {
        if (BoardableEnemy is not { } enemy)
            return;
        var target = FindAimedEnemyIndoorTarget(character, WallCutReachUnits, WallCutSamples, WallCutPointRadius);
        if (target.DoorId is { } doorId)
            ChopDoor(doorId, WallCutDamagePerSecond * (float)deltaSeconds);
        else if (target.AirlockId is { } airlockId)
            enemy.DamageAirlock(airlockId, WallCutDamagePerSecond * (float)deltaSeconds);
        else if (target.WallBlockId is { } blockId)
            enemy.DamageWallBlock(blockId, WallCutDamagePerSecond * (float)deltaSeconds);
    }

    // Shared by CutAlongFlame and the snapshot query that tells the client which target to show a
    // health bar over (World.WallBlocks.cs's GetWallToolTargetId) - one sampling routine so the two
    // can never disagree about what the flame is actually about to bite into out here.
    private OreDeposit? FindAimedOreDeposit(Character character)
    {
        var origin = GetEvaWorldPosition(character);
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection.Normalized() : character.FacingDirection;
        if (aim.Length() < 0.01f)
            return null;

        // Sampled along the flame rather than tested at its tip: a block half a metre wide sitting
        // beside the character would otherwise be missed by a flame pointed past it.
        for (var i = 1; i <= CutterSamples; i++)
        {
            var point = origin + aim * (CutterReachUnits * i / CutterSamples);
            var block = AsteroidField.OreDeposits.FirstOrDefault(d =>
                _oreDepositHp.GetValueOrDefault(d.Id) > 0f && d.DistanceFrom(point) <= 0f);
            if (block is not null)
                return block;
        }
        return null;
    }

    private void CutAlongFlame(Character character, double deltaSeconds)
    {
        var block = FindAimedOreDeposit(character);
        if (block is not null)
        {
            var hp = _oreDepositHp[block.Id] - CutterDamagePerSecond * (float)deltaSeconds;
            _oreDepositHp[block.Id] = Math.Max(0f, hp);
            if (hp <= 0f)
                _droppedItems.Add(new DroppedItem($"drop-{_nextDroppedItemId++}", ItemType.Mineral, block.X, block.Y));
            return; // one target at a time: the flame cuts what it is pointed at
        }

        // Nothing to mine along the flame - try the player's own hull instead, the same way the
        // welder already patches it from outside (World.Welding.cs's WeldAlongFlame): same reach/
        // rate as cutting it open from indoors (WallCutReachUnits/WallCutDamagePerSecond,
        // World.WallBlocks.cs). FindAimedWallBlock already knows how to test an EVA aim against the
        // hull's real position out here (it rotates each block's local position out to world
        // space), so this reuses it rather than re-deriving the same geometry a second time.
        var hullBlock = FindAimedWallBlock(character, WallCutReachUnits, WallCutSamples, WallCutPointRadius);
        if (hullBlock is not null)
        {
            DamageWallBlock(hullBlock.Id, WallCutDamagePerSecond * (float)deltaSeconds);
            return;
        }

        // Still nothing - try the currently boardable enemy hull, same reach/rate again: cutting an
        // enemy raider open works exactly like cutting your own ship's hull, not a separate tool or
        // timing ("резак работает так же, как обшивка корабля игрока").
        var enemyTarget = FindAimedEnemyOuterTarget(character, WallCutReachUnits, WallCutSamples, WallCutPointRadius);
        if (enemyTarget is { } target)
        {
            if (target.AirlockId is { } airlockId)
                target.Enemy.DamageAirlock(airlockId, WallCutDamagePerSecond * (float)deltaSeconds);
            else if (target.WallBlockId is { } blockId)
                target.Enemy.DamageWallBlock(blockId, WallCutDamagePerSecond * (float)deltaSeconds);
        }
    }

    // Local-frame bounding-box centre of an enemy hull's own Rooms - the anchor a WallBlock's local
    // position rotates around to reach world space, same convention World.GetHullLocalBounds already
    // uses for the player's own ship (EnemyShipLayout.GetLocalBounds is the shared-model twin of it).
    private static Vec2 EnemyHullLocalCenter(EnemyShipLayout layout) => layout.GetLocalBounds().Center;

    // Mirrors FindAimedWallBlock's outside branch, just against whichever enemy hull is currently
    // boardable (BoardableEnemy) instead of the player's own Ship, and checking both of its locked
    // AirlockOuterDoors alongside its wall panels - a hatch is just as cuttable (or weldable) as any
    // other bit of plating, it's simply the one the game calls out by name. Shared by cutting and
    // welding alike (EnemyToolTarget's own doc comment), so it doesn't discriminate on Hp itself.
    private (EnemyShipRuntime Enemy, string? AirlockId, string? WallBlockId)? FindAimedEnemyOuterTarget(
        Character character, float reachUnits, int samples, float pointRadius)
    {
        if (BoardableEnemy is not { } enemy)
            return null;
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection.Normalized() : character.FacingDirection;
        if (aim.Length() < 0.01f)
            return null;

        var origin = GetEvaWorldPosition(character);
        var localCenter = EnemyHullLocalCenter(enemy.Layout);
        Vec2 ToWorld(Vec2 local) => enemy.Position + RotateLocalToWorld(local - localCenter, enemy.RotationDegrees);

        for (var i = 1; i <= samples; i++)
        {
            var point = origin + aim * (reachUnits * i / samples);

            var airlock = enemy.Layout.AirlockOuterDoors.FirstOrDefault(d => (ToWorld(d.Position) - point).Length() <= pointRadius);
            if (airlock is not null)
                return (enemy, airlock.Id, null);

            var block = enemy.Layout.WallBlocks.FirstOrDefault(b => (ToWorld(b.Position) - point).Length() <= pointRadius);
            if (block is not null)
                return (enemy, null, block.Id);
        }
        return null;
    }

    private IReadOnlyList<OreDepositState> CreateOreDepositStates() =>
        AsteroidField.OreDeposits
            .Select(d => new OreDepositState(d.Id, _oreDepositHp.GetValueOrDefault(d.Id), d.MaxHp))
            .ToArray();
}
