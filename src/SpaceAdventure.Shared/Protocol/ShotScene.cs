namespace SpaceAdventure.Shared.Protocol;

// Which structure's coordinates a personal shot lives in. Every walkable structure in the game has
// its own rooms and its own frame - your ship, a station you're docked at, a hull you've boarded -
// and a bullet belongs to exactly one of them, both for hit detection and for deciding which
// renderer draws it.
public enum ShotScene
{
    Ship,
    Station,
    EnemyShip,
}
