using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

// One hostile hull as a thing with a place in the world: where it is, which way it's pointing and
// how badly hurt it is. The whole squadron defending a sector is present at once, so this is a
// list - the older single EnemyShipState still describes the one currently being fought (and
// boarded), because the HP bar and the boarding hatch both need exactly one subject.
// X/Y are double, not float (M58 follow-up - same fix as ShipFieldState's own doc comment: at
// KSP-real field scale a float32 position can't resolve two points closer than ~77,000 units apart,
// and an enemy hull shares that same field with the player ship).
public sealed record EnemyShipFieldState(
    string Id,
    double X,
    double Y,
    float RotationDegrees,
    float Hp,
    float MaxHp,
    bool IsRetreating,
    // The one whose interior the boarding party would climb into (World.Boarding.cs) - only ever
    // true for a single ship at a time.
    bool IsBoardable,
    // The compartments this ship still has, in its own local frame - every hostile ship is the same hull, but each loses
    // compartments as it is shot up (World.EnemyShips.cs), and the field renderer draws exactly what is left.
    IReadOnlyList<Room>? Rooms = null);
