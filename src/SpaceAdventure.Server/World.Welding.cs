using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// The welding tool as a held, aimed flame - the same shape as the cutter (World.Cutting.cs), just
// pointed at a breached hull block instead of a vein of ore. Holding the button lights a
// yellow-orange flame from the character toward the cursor; whatever breached block it passes
// through gets welded shut. This replaces the old "stand next to it and press F" instant repair -
// like the cutter, the tool now burns its own tank while lit, so patching a hull is a resource you
// spend rather than a free action.
public sealed partial class World
{
    public const float WelderReachUnits = 1.7f;
    private const int WelderSamples = 6;
    // A wall block is a 1x1 segment (WallBlock.cs) - generous enough that the flame doesn't have to
    // land pixel-perfect on the block's own center to catch it.
    private const float WeldPointRadius = 0.6f;

    private readonly Dictionary<int, bool> _weldInput = new();

    public bool IsWelding(int playerId) => _weldInput.GetValueOrDefault(playerId) && CanWeld(_characters[playerId]);

    // Holding a welding tool is not enough: it needs a tank with something left in it, same as the
    // cutter's socket (World.OxygenTanks.cs's sibling, WeldingTankDefinitions).
    private bool CanWeld(Character character)
    {
        var slot = character.Inventory.HeldSlotOf(ItemType.WeldingTool);
        return slot >= 0 && character.Inventory.HasWorkingTank(slot);
    }

    private void StepWelding(double deltaSeconds)
    {
        foreach (var (playerId, held) in _weldInput)
        {
            if (!held || !_characters.TryGetValue(playerId, out var character) || character.Health <= 0)
                continue;

            var slot = character.Inventory.HeldSlotOf(ItemType.WeldingTool);
            if (slot < 0 || !character.Inventory.HasWorkingTank(slot))
                continue;

            character.Inventory.DrainTank(slot, WeldingTankDefinitions.DrainPerSecond * (float)deltaSeconds);

            // The torch lights anywhere the tool is held and lit, exactly like the cutter - but a
            // hull breach only exists in the player's own ship interior, so that's the only place
            // the flame has anything to weld shut.
            if (character.IsOutside || character.OnEnemyShip || character.OnStation)
                continue;

            WeldAlongFlame(character);
        }
    }

    private void WeldAlongFlame(Character character)
    {
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection.Normalized() : character.FacingDirection;
        if (aim.Length() < 0.01f)
            return;

        // Sampled along the flame rather than tested at its tip - same reasoning as the cutter: a
        // breach beside the character would otherwise be missed by a flame pointed past it.
        for (var i = 1; i <= WelderSamples; i++)
        {
            var point = character.Position + aim * (WelderReachUnits * i / WelderSamples);
            var block = Ship.WallBlocks.FirstOrDefault(b =>
                b.RoomId == character.RoomId && _breachedWallBlockIds.Contains(b.Id) &&
                (b.Position - point).Length() <= WeldPointRadius);
            if (block is null)
                continue;

            _breachedWallBlockIds.Remove(block.Id);
            return; // one block at a time: the flame welds what it is pointed at
        }
    }
}
