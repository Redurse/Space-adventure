using SpaceAdventure.Shared.Model;

namespace SpaceAdventure.Shared.Protocol;

// One hostile hull as a thing with a place in the world: where it is, which way it's pointing and
// how badly hurt it is. The whole squadron defending a sector is present at once, so this is a
// list - the older single EnemyShipState still describes the one currently being fought (and
// boarded), because the HP bar and the boarding hatch both need exactly one subject.
public sealed record EnemyShipFieldState(
    string Id,
    float X,
    float Y,
    float RotationDegrees,
    float Hp,
    float MaxHp,
    bool IsRetreating,
    // The one whose interior the boarding party would climb into (World.Boarding.cs) - only ever
    // true for a single ship at a time.
    bool IsBoardable,
    // Which hull this is. The field renderer draws a different ship for each, and a boarding party
    // finds the matching interior - so this has to be the real class rather than something derived
    // from the id, or the outside would promise a freighter and the inside deliver a gunship.
    EnemyShipClass Kind = EnemyShipClass.Raider);
