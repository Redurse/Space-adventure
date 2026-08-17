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
    // the Health/OnStation/OnEnemyShip checks StepWelding applies, so the torch could read as
    // active in situations where the server had already stopped doing anything with it.
    public bool IsWelding(int playerId) =>
        _weldInput.GetValueOrDefault(playerId) &&
        _characters.TryGetValue(playerId, out var character) &&
        character.Health > 0 && !character.OnEnemyShip && !character.OnStation &&
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
            // it from outside same as from the corridor it opened onto. Only a boarded enemy hull or
            // a station has no breach of your own ship to reach at all.
            if (character.OnEnemyShip || character.OnStation)
                continue;

            WeldAlongFlame(character);
        }
    }

    private void WeldAlongFlame(Character character)
    {
        var aim = character.LookDirection.Length() > 0.01f ? character.LookDirection.Normalized() : character.FacingDirection;
        if (aim.Length() < 0.01f)
            return;

        // Indoors, the flame and the block it's aimed at share the ship's own interior coordinate
        // space. Outside, the character is tracked in world/field space (GetEvaWorldPosition) while
        // WallBlock.Position is still in that same interior frame, so the block's position has to be
        // rotated and translated out to world space the same way the hull plating itself is drawn
        // out there (World.Eva.cs) before the two can be compared.
        var origin = character.IsOutside ? GetEvaWorldPosition(character) : character.Position;
        var (hullCenter, _) = GetHullLocalBounds();

        // Sampled along the flame rather than tested at its tip - same reasoning as the cutter: a
        // breach beside the character would otherwise be missed by a flame pointed past it.
        for (var i = 1; i <= WelderSamples; i++)
        {
            var point = origin + aim * (WelderReachUnits * i / WelderSamples);
            var block = Ship.WallBlocks.FirstOrDefault(b =>
            {
                if (!_breachedWallBlockIds.Contains(b.Id))
                    return false;
                if (character.IsOutside)
                    return (_shipFieldPosition + RotateLocalToWorld(b.Position - hullCenter, _shipRotationDegrees) - point).Length() <= WeldPointRadius;
                return b.RoomId == character.RoomId && (b.Position - point).Length() <= WeldPointRadius;
            });
            if (block is null)
                continue;

            _breachedWallBlockIds.Remove(block.Id);
            return; // one block at a time: the flame welds what it is pointed at
        }
    }
}
