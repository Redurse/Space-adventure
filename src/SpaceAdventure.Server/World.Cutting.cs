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

            // The torch lights anywhere and burns its tank while it does. Outside, the only thing
            // it has to bite on is ore (field space); indoors, aboard your own ship, it bites into
            // a closed door or the ship's own wall blocks instead (CutIndoorAlongFlame below). It
            // lights the same way standing in a station's own corridors, but neither a door nor a
            // wall block there ever matches (AllShipDoors/Ship.WallBlocks are keyed to the ship's
            // own ids, never a station's), so there's nothing there for it to actually cut into. A
            // boarded enemy hull has none of those or ore, so there's nothing for it to do there
            // either.
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

    private void CutAlongFlame(Character character, double deltaSeconds)
    {
        var origin = GetEvaWorldPosition(character);
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection.Normalized() : character.FacingDirection;
        if (aim.Length() < 0.01f)
            return;

        // Sampled along the flame rather than tested at its tip: a block half a metre wide sitting
        // beside the character would otherwise be missed by a flame pointed past it.
        for (var i = 1; i <= CutterSamples; i++)
        {
            var point = origin + aim * (CutterReachUnits * i / CutterSamples);
            var block = AsteroidField.OreDeposits.FirstOrDefault(d =>
                _oreDepositHp.GetValueOrDefault(d.Id) > 0f && d.DistanceFrom(point) <= 0f);
            if (block is null)
                continue;

            var hp = _oreDepositHp[block.Id] - CutterDamagePerSecond * (float)deltaSeconds;
            _oreDepositHp[block.Id] = Math.Max(0f, hp);
            if (hp <= 0f)
                _droppedItems.Add(new DroppedItem($"drop-{_nextDroppedItemId++}", ItemType.Mineral, block.X, block.Y));
            return; // one block at a time: the flame cuts what it is pointed at
        }
    }

    private IReadOnlyList<OreDepositState> CreateOreDepositStates() =>
        AsteroidField.OreDeposits
            .Select(d => new OreDepositState(d.Id, _oreDepositHp.GetValueOrDefault(d.Id), d.MaxHp))
            .ToArray();
}
