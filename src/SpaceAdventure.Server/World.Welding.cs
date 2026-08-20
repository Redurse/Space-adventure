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
    // land pixel-perfect on the block's own center to catch it. Widened from the cutter's original
    // 0.6 - unlike ore (a fixed target you walk up to and hold still against), a breach sits flush
    // in a wall you're standing beside at an angle, so a straight aim line grazing just past its
    // center used to read as "the welder stopped working" even while lit and pointed roughly at it.
    private const float WeldPointRadius = 0.85f;

    private readonly Dictionary<int, bool> _weldInput = new();

    // Mirrors StepWelding's own gate exactly, so the client-rendered flame (World.cs's Welding
    // flag) never shows lit while nothing is actually being welded underneath it - it used to skip
    // the Health/OnEnemyShip checks StepWelding applies, so the torch could read as active in
    // situations where the server had already stopped doing anything with it. Lights at a station
    // too, same as the cutter (World.Cutting.cs) - there's just nothing of the player's own ship
    // there for it to reach (FindAimedWallBlock only ever matches Ship.WallBlocks, keyed by the
    // ship's own room ids, never a station's), so it's never actually able to touch station walls.
    public bool IsWelding(int playerId) =>
        _weldInput.GetValueOrDefault(playerId) &&
        _characters.TryGetValue(playerId, out var character) &&
        character.Health > 0 && !character.OnEnemyShip &&
        CanWeld(character);

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

            // The torch lights anywhere the tool is held and lit, exactly like the cutter - a hull
            // breach can be welded from either side of the plating it's in, on a spacewalk patching
            // it from outside same as from the corridor it opened onto, or even standing in a
            // station's own corridors (FindAimedWallBlock just never finds anything of the player's
            // ship to aim at from there). Only a boarded enemy hull has no breach of your own ship
            // to reach at all.
            if (character.OnEnemyShip)
                continue;

            WeldAlongFlame(character, deltaSeconds);
        }
    }

    // Repairs gradually now (WelderRepairPerSecond, World.WallBlocks.cs) rather than un-breaching
    // a block the instant the flame grazes it - patching a hull is work you watch happen, the same
    // way the cutter's damage is, not a free instant fix. Aiming at an already-healthy block is
    // harmless: FindAimedWallBlock still reports it (for the client's Hp bar - GetWallToolTargetId)
    // but there's nothing to add to a block already at WallBlockMaxHp.
    private void WeldAlongFlame(Character character, double deltaSeconds)
    {
        var block = FindAimedWallBlock(character, WelderReachUnits, WelderSamples, WeldPointRadius);
        if (block is null)
            return;
        RepairWallBlock(block.Id, WelderRepairPerSecond * (float)deltaSeconds);
    }
}
