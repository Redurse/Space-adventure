using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// A bullet or bolt in flight from a personal weapon (World.PersonalShots.cs). Scene says which
// structure's coordinates X/Y are in, so each renderer draws only its own.
public sealed record PersonalShotState(string Id, float X, float Y, bool FromEnemy, ShotScene Scene, ItemType Weapon)
{
    public Vec2 Position => new(X, Y);
}
