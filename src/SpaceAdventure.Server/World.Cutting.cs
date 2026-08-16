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

            // The torch lights anywhere - inside the ship, aboard a station, on a boarded hull - and
            // burns its tank while it does. Ore only exists in field space, so that's the only place
            // the flame has anything to bite on; there is nothing special about being outdoors
            // beyond that.
            if (!character.IsOutside)
                continue;

            CutAlongFlame(character, deltaSeconds);
        }
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
