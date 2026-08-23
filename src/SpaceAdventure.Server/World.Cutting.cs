using SpaceAdventure.Shared.Model;
using SpaceAdventure.Shared.Protocol;

namespace SpaceAdventure.Server;

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
            // ore first and the player's own hull second if nothing's out there to mine
            // (CutAlongFlame - breaching it from the outside in, same as WeldAlongFlame already
            // patches it from either side); indoors, aboard your own ship, it bites into a closed
            // door or the ship's own wall blocks instead (CutIndoorAlongFlame below). It lights the
            // same way standing in a station's own corridors, but neither a door nor a wall block
            // there ever matches (AllShipDoors/Ship.WallBlocks are keyed to the ship's own ids,
            // never a station's), so there's nothing there for it to actually cut into. A boarded
            // enemy hull has none of those or ore, so there's nothing for it to do there either.
            if (character.IsOutside)
                CutAlongFlame(character, deltaSeconds);
            else if (!character.OnEnemyShip)
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
        var enemyTarget = FindAimedEnemyHullBlock(character, WallCutReachUnits, WallCutSamples, WallCutPointRadius);
        if (enemyTarget is { } target)
            target.Enemy.DamageWallBlock(target.Block.Id, WallCutDamagePerSecond * (float)deltaSeconds);
    }

    // Local-frame bounding-box centre of an enemy hull's own Rooms - the anchor a WallBlock's local
    // position rotates around to reach world space, same convention World.GetHullLocalBounds already
    // uses for the player's own ship (EnemyShipLayout.GetLocalBounds is the shared-model twin of it).
    private static Vec2 EnemyHullLocalCenter(EnemyShipLayout layout) => layout.GetLocalBounds().Center;

    // Mirrors FindAimedWallBlock's outside branch exactly, just against whichever enemy hull is
    // currently boardable (BoardableEnemy) instead of the player's own Ship - an already-breached
    // block is skipped so the flame reaches past it to whatever's still intact, the same "a hole
    // isn't a wall anymore" rule combat fire already follows (World.EnemyAi.cs).
    private (EnemyShipRuntime Enemy, WallBlock Block)? FindAimedEnemyHullBlock(Character character, float reachUnits, int samples, float pointRadius)
    {
        if (BoardableEnemy is not { } enemy)
            return null;
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection.Normalized() : character.FacingDirection;
        if (aim.Length() < 0.01f)
            return null;

        var origin = GetEvaWorldPosition(character);
        var localCenter = EnemyHullLocalCenter(enemy.Layout);

        for (var i = 1; i <= samples; i++)
        {
            var point = origin + aim * (reachUnits * i / samples);
            var block = enemy.Layout.WallBlocks.FirstOrDefault(b => !enemy.IsWallBlockBreached(b.Id) &&
                (enemy.Position + RotateLocalToWorld(b.Position - localCenter, enemy.RotationDegrees) - point).Length() <= pointRadius);
            if (block is not null)
                return (enemy, block);
        }
        return null;
    }

    private IReadOnlyList<OreDepositState> CreateOreDepositStates() =>
        AsteroidField.OreDeposits
            .Select(d => new OreDepositState(d.Id, _oreDepositHp.GetValueOrDefault(d.Id), d.MaxHp))
            .ToArray();
}
