using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

// Simplified injury/medic mechanic (game_design.md section 4, M12 scope). Once a character's
// Health drops below Character.BleedingThreshold, it keeps draining on its own regardless of
// whether the original cause (e.g. a decompressed room) is still active - an injury that
// lingers rather than one that stops the moment you walk away. The only treatment is a MedKit
// held in hand, used via the same F key as everything else (World.Interact.cs); it's a one-shot
// consumable, not a bandage that can be reused. No named wound types (bleeding/burns/etc.) and no
// death/respawn handling yet - both are bigger, separate decisions left for later.
public sealed partial class World
{
    private const float BleedingDamagePerSecond = 2f;
    private const float MedKitHealAmount = 50f;

    private void StepInjuries(double deltaSeconds)
    {
        foreach (var character in _characters.Values)
        {
            if (character.IsBleeding)
                character.Health = Math.Max(0, character.Health - BleedingDamagePerSecond * (float)deltaSeconds);
        }
    }

    // No-ops if not holding a MedKit, or if there's nothing to heal - so a lucky F press never
    // wastes it while at full health.
    private void TryUseMedKit(Character character)
    {
        if (!character.Inventory.IsHolding(ItemType.MedKit))
            return;
        if (character.Health >= Character.MaxHealth)
            return;

        character.Health = Math.Min(Character.MaxHealth, character.Health + MedKitHealAmount);
        character.Inventory.TryTakeHeldItem(ItemType.MedKit);
    }
}
