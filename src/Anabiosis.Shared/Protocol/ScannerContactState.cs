using Anabiosis.Shared.Model;

namespace Anabiosis.Shared.Protocol;

// One persistent NPC hull a particular player's own scanner sweep has actually picked up
// (World.Scanner.cs, M44) - frozen at wherever it was standing the moment it was last swept, not
// its live position, the same "last known point" a real passive sensor would report rather than a
// live radar. Attached to that player's own CharacterState (ScannerContacts) rather than broadcast
// globally - two crew scanning the same system can have found different hulls, or the same hull at
// different stale positions, and each should only ever see their own results
// (game_design.md/M44 - "показывает их на экране самого учёного").
// X/Y are double, not float (M58 follow-up - same fix as ShipFieldState's own doc comment): a
// scanner contact is a frozen snapshot of a real field-space position, same KSP scale as the ship.
public sealed record ScannerContactState(
    string Id,
    NpcShipKind Kind,
    FactionId FactionId,
    double X,
    double Y,
    float RotationDegrees,
    long DetectedAtTick);
