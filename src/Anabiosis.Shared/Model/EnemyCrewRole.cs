namespace Anabiosis.Shared.Model;

// What an enemy crew member does aboard a hostile ship (direct user request: everyone works within their role).
public enum EnemyCrewRole
{
    Captain,   // flies the ship from the helm
    Scientist, // mans a gun and heals the wounded
    Engineer,  // repairs the ship
    Fighter,   // defends it against boarders
}
