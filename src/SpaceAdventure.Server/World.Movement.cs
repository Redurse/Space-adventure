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
                {
                    character.Inventory.Equipped[EquipSlot.Suit] = character.SuitActionEquipping ? ItemType.Spacesuit : null;
                    if (character.SuitActionLockerId is { } lockerId)
                        SetSuitLockerHasSuit(lockerId, hasSuit: !character.SuitActionEquipping);
                    character.SuitActionLockerId = null;
                }
            }

            if (character.ManningTurretId is not null || character.IsAtHelm || character.SuitActionRemaining > 0)
                continue; // locked in place at the periscope or helm, or mid-equip/unequip

            var hasInput = _moveInput.TryGetValue(character.PlayerId, out var input) && input != Vec2.Zero;
            if (hasInput)
                character.FacingDirection = input.Normalized();

            // A free-floating EVA character keeps drifting on momentum every tick regardless of
            // input (game_design.md Phase 3, M17) - unlike everything else here, it can't just be
            // skipped when there's no input to react to.
            if (character.IsOutside)
            {
                // Drifting close enough to the enemy hull during a battle boards it, exactly like
                // walking into an open airlock crosses into vacuum (World.Boarding.cs).
                if (hasInput && TryBoardEnemyShip(character, character.FacingDirection * MoveSpeed * (float)deltaSeconds))
                    continue;
                StepEvaCharacter(character, hasInput ? character.FacingDirection : Vec2.Zero, deltaSeconds);
                continue;
            }

            if (!hasInput)
                continue;

            var delta = character.FacingDirection * MoveSpeed * (float)deltaSeconds;

            if (TryCrossIntoVacuum(character, delta))
                continue;
            if (TryLeaveEnemyShip(character, delta))
                continue;

            var (afterX, roomAfterX) = MoveInCurrentStructure(character, character.Position, character.RoomId, new Vec2(delta.X, 0));
            var (afterY, roomAfterY) = MoveInCurrentStructure(character, afterX, roomAfterX, new Vec2(0, delta.Y));

            character.Position = afterY;
            character.RoomId = roomAfterY;
            // Which structure you're in is a consequence of which room you walked into, not a
            // separate transition step (World.StationDocking.cs).
            if (Phase == VoyagePhase.Station)
                character.OnStation = IsStationRoom(roomAfterY);
        }
    }

    // A character's RoomId is only meaningful against whichever structure it's currently standing
    // in - own ship by default, or the enemy ship once it has physically boarded. While docked the
    // ship and the station are one joined layout, so walking between them needs no special case at
    // all. All of them use the same RoomLayout collision, just different Rooms/Doors lists.
    private (Vec2 Position, string RoomId) MoveInCurrentStructure(Character character, Vec2 position, string roomId, Vec2 delta)
    {
        if (character.OnEnemyShip)
            return EnemyShipLayout.MoveAlongAxis(position, roomId, delta, IsDoorOpen);
        if (Phase == VoyagePhase.Station)
        {
            var (rooms, doors) = GetDockedLayout();
            return RoomLayout.MoveAlongAxis(rooms, doors, position, roomId, delta, IsDoorOpen);
        }
        if (character.OnStation)
            return Station.MoveAlongAxis(position, roomId, delta, IsDoorOpen);
        return Ship.MoveAlongAxis(position, roomId, delta, IsDoorOpen);
    }
}
