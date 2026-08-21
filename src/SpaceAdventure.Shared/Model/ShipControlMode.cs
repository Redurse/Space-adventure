namespace SpaceAdventure.Shared.Model;

// Two ways to fly the ship by hand (game_design.md section 5, M41). Arc is the default: turning
// only banks the nose at a rate tied to current speed (no pivoting in place), the way a real
// vessel carrying real momentum would come about. Rcs is the free rotation this game always had
// before this milestone - useful for precision work (docking, lining up a shot) where you need to
// point the bow somewhere the ship isn't actually travelling.
public enum ShipControlMode
{
    Arc,
    Rcs,
}
