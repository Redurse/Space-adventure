using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Server;

public sealed partial class World
{
    private const float MoveSpeed = 3f; // units/sec

    // Resolves X and Y separately so sliding along a wall in one axis still works while the
    // other axis is blocked (classic axis-separated AABB collision).
    private void StepCharacters(double deltaSeconds)
    {
        foreach (var character in _characters.Values)
        {
            if (character.SuitActionRemaining > 0)
            {
                character.SuitActionRemaining = Math.Max(0, character.SuitActionRemaining - (float)deltaSeconds);
                if (character.SuitActionRemaining <= 0)
                    character.Inventory.Equipped[EquipSlot.Clothing] = character.SuitActionEquipping ? ItemType.Spacesuit : null;
            }

            if (character.ManningTurretId is not null || character.SuitActionRemaining > 0)
                continue; // locked in place at the periscope, or mid-equip/unequip

            if (!_moveInput.TryGetValue(character.PlayerId, out var input) || input == Vec2.Zero)
                continue;

            character.FacingDirection = input.Normalized();
            var delta = character.FacingDirection * MoveSpeed * (float)deltaSeconds;

            var (afterX, roomAfterX) = Ship.MoveAlongAxis(character.Position, character.RoomId, new Vec2(delta.X, 0));
            var (afterY, roomAfterY) = Ship.MoveAlongAxis(afterX, roomAfterX, new Vec2(0, delta.Y));

            character.Position = afterY;
            character.RoomId = roomAfterY;
        }
    }
}
